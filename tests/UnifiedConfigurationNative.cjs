const fs = require("node:fs"),
  path = require("node:path"),
  os = require("node:os"),
  assert = require("node:assert/strict"),
  readline = require("node:readline"),
  { spawn } = require("node:child_process");
const { chromium } = require("../Orange3DTool/Web/playwright-runtime.cjs");
const webRoot = path.resolve(__dirname, "../Orange3DTool/Web");
const moduleSource = fs
  .readFileSync(
    path.join(webRoot, "launch-configurations-module.inc.js"),
    "utf8",
  )
  .replace(/\r\n/g, "\n");
const shipped = fs
  .readFileSync(path.join(webRoot, "app.js"), "utf8")
  .replace(/\r\n/g, "\n");
assert.equal(
  shipped
    .slice(
      shipped.indexOf("// Source mirrored inline"),
      shipped.indexOf("const modOrderingUi="),
    )
    .trimEnd(),
  moduleSource.trimEnd(),
  "shipped inline configuration module matches its editable source",
);
const out = path.resolve(process.argv[3]),
  child = spawn(
    path.join(process.argv[2], "NativeUi.exe"),
    [path.join(os.tmpdir(), "o6-" + Date.now().toString(36))],
    { windowsHide: true, stdio: ["pipe", "pipe", "pipe"] },
  );
let resolveReady,
  sink = () => {},
  sequence = 0;
const ready = new Promise((r, j) => {
    resolveReady = r;
    child.once("exit", (code) =>
      j(new Error("native exited before ready: " + code)),
    );
  }),
  pending = new Map(),
  calls = [],
  events = [];
child.stderr.on("data", (d) => process.stderr.write(d));
readline.createInterface({ input: child.stdout }).on("line", (line) => {
  const r = JSON.parse(line.replace(/^\uFEFF/, ""));
  if (r.ready) resolveReady(r);
  else if (r.ev) {
    events.push({ ...r, receivedAt: Date.now() });
    sink(r);
  } else {
    const p = pending.get(r.id);
    if (p) {
      pending.delete(r.id);
      calls.push({
        ...p.m,
        startedAt: p.started,
        durationMs: Date.now() - p.started,
        response: r,
      });
      p.resolve({ ...r, id: p.m.id });
    }
  }
});
const native = (m) =>
  new Promise((resolve) => {
    const id = String(++sequence);
    pending.set(id, { m, resolve, started: Date.now() });
    child.stdin.write(JSON.stringify({ ...m, id }) + "\n");
  });
const ask = async (action, payload = {}) => {
  const r = await native({ action, payload });
  assert.equal(r.ok, true, r.error);
  return r.data;
};
(async () => {
  const info = await ready;
  let browser, page;
  try {
    const originalFiles = [
        path.join(info.game, "ProjectZomboid64.json"),
        path.join(info.game, "ProjectZomboid64.bat"),
        path.join(info.user, "mods/default.txt"),
        path.join(info.user, "Saves/Sandbox/Fixture/mods.txt"),
      ],
      original = Object.fromEntries(
        originalFiles.map((f) => [f, fs.readFileSync(f).toString("base64")]),
      );
    const inventory = await ask("launchConfigurations");
    assert.equal(inventory.selectedId, "launcher");
    assert.deepEqual(
      inventory.configurations.slice(0, 3).map((r) => r.id),
      ["launcher", "steam", "vanilla"],
    );
    const a = await ask("configurationCreate", {
      name: "普通 A",
      versionId: "steam",
    });
    assert.match(a.id, /^live:[a-f0-9]{32}$/);
    const b = await ask("configurationCreate", {
      name: "普通 B",
      versionId: "steam",
    });
    await ask("launchConfigurationSelect", { id: b.id });
    await ask("configurationSaveOrdinary", {
      id: a.id,
      ordinaryIds: ["B", "A"],
    });
    const view = await ask("configurationRead", { id: a.id });
    assert.deepEqual(view.ordinaryIds, ["B", "A"]);
    assert.equal((await ask("launchConfigurations")).selectedId, b.id);
    for (const action of ["configurationRead", "configurationRequest"]) {
      const invalid = await native({
        action,
        payload:
          action === "configurationRead"
            ? { id: a.id, path: info.game }
            : { id: a.id, action: "setGame", payload: { path: info.game } },
      });
      assert.equal(invalid.ok, false, "reject arbitrary paths and routes");
    }

    const forcePreview = await ask("configurationRequest", {
      id: "steam",
      action: "forceTakeoverPreview",
      payload: {},
    });
    assert.equal(
      forcePreview.data.supported,
      true,
      "Steam controller must use injected isolated native OS boundary",
    );
    const frozenId = "snapshot:" + info.snapshotId,
      frozenBefore = (await ask("configurationRead", { id: frozenId })).frozen;
    await ask("fixtureSlow");
    const paused = native({
      action: "frozenConfigurationSave",
      payload: {
        snapshotId: info.snapshotId,
        manifestHash: frozenBefore.manifestHash,
        ordinaryIds: ["A", "B"],
        componentIds: [],
        sourceTokens: [],
      },
    });
    await new Promise((resolve) => {
      const poll = () =>
        events.some((e) => e.ev === "frozenConfigurationProgress")
          ? resolve()
          : setTimeout(poll, 10);
      poll();
    });
    const writerTask = (await ask("taskGet")).active;
    await ask("configurationRequest", {
      id: b.id,
      action: "javaComponentsList",
      payload: {},
      readId: "f".repeat(32),
    });
    const sameRead = await native({
      action: "configurationRequest",
      payload: { id: frozenId, action: "status", payload: {} },
    });
    assert.equal(
      sameRead.ok,
      true,
      "A readonly status must not be blocked by A copy: " + sameRead.error,
    );
    const sameCatalog = await ask("configurationRequest", {
      id: frozenId,
      action: "modCatalog",
      payload: {},
    });
    assert.ok(sameCatalog.data.mods.length >= 2);
    assert.equal(
      (
        await ask("configurationRequest", {
          id: b.id,
          action: "status",
          payload: {},
        })
      ).configurationId,
      b.id,
    );
    assert.equal((await paused).ok, true);
    const writerDone = (await ask("taskGet")).history.find(
      (r) => r.id === writerTask.id,
    );
    assert.equal(writerDone.status, "completed");
    assert.ok(
      writerDone.events.every((e) => !e.message.includes("正在核对组件来源")),
      "read progress never replaces writer journal events",
    );

    const frozenNow = (await ask("configurationRead", { id: frozenId })).frozen;
    await ask("frozenConfigurationSave", {
      snapshotId: info.snapshotId,
      manifestHash: frozenNow.manifestHash,
      ordinaryIds: ["B", "A"],
      componentIds: [],
      sourceTokens: [],
    });
    await ask("fixtureSlowRead");
    const readToken = "c".repeat(32),
      beforeReadEvents = events.length;
    const canceledRead = native({
      action: "configurationRequest",
      payload: {
        id: a.id,
        action: "modCatalog",
        payload: { refresh: true },
        readId: readToken,
      },
    });
    await new Promise((resolve) => {
      const deadline = Date.now() + 5000;
      const poll = () =>
        events
          .slice(beforeReadEvents)
          .some(
            (e) =>
              e.ev === "configurationProgress" &&
              e.data.text.includes("扫描模组"),
          )
          ? resolve()
          : Date.now() > deadline
            ? resolve()
            : setTimeout(poll, 10);
      poll();
    });
    await ask("configurationReadCancel", { id: a.id, readId: readToken });
    assert.equal((await canceledRead).ok, false, "cancel only this reader");
    assert.equal(
      (
        await ask("configurationRequest", {
          id: b.id,
          action: "memoryGet",
          payload: {},
        })
      ).configurationId,
      b.id,
    );
    fs.writeFileSync(
      path.join(out, "native-contract.json"),
      JSON.stringify({ info, a, b, view, calls }, null, 2),
    );
    browser = await chromium.launch({ channel: "msedge", headless: true });
    const fixture = { errors: [] };
    page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    page.setDefaultTimeout(45000);
    page.on("pageerror", (e) => fixture.errors.push(e.message));
    await page.exposeFunction("configurationNative", native);
    await page.addInitScript(() => {
      window.testErrors = [];
      window.addEventListener("error", (e) => testErrors.push(e.message));
      window.addEventListener("unhandledrejection", (e) =>
        testErrors.push(e.reason?.message),
      );
      window.sent = [];
      window.pollCallbacks = [];
      window.setInterval = (f) => {
        pollCallbacks.push(f);
        return pollCallbacks.length;
      };
      let receive;
      window.emitNative = (data) => receive({ data });
      window.chrome = {
        webview: {
          addEventListener: (_, f) => (receive = f),
          postMessage: (m) => {
            sent.push(m);
            configurationNative(m).then(emitNative);
          },
        },
      };
    });
    await page.goto(
      "file:///" +
        path
          .resolve(__dirname, "../Orange3DTool/Web/index.html")
          .replaceAll("\\", "/"),
    );
    await page.waitForFunction(() => !!window.orange);
    await page.waitForFunction(
      () => !!orange.launchConfigurationUi.model.cover,
    );
    if (await page.locator("[data-guide-close]").isVisible())
      await page.locator("[data-guide-close]").click();
    sink = (r) =>
      page
        .evaluate((r) => emitNative({ event: r.ev, data: r.data }), r)
        .catch(() => {});
    await page.waitForSelector("#launch-configuration-select");
    assert.equal(
      await page.locator("#play-profile-select,#home-version-select").count(),
      0,
    );
    await ask("fixtureHoldSelection");
    await page.locator("#launch-configuration-select").selectOption(b.id);
    await page.locator("[data-configuration-edit]").click();
    await page.waitForFunction(
      () =>
        !!orange.launchConfigurationUi.editor()?.view &&
        !orange.launchConfigurationUi.editor()?.reading,
    );
    assert.equal(
      await page.evaluate(() => orange.launchConfigurationUi.editor().id),
      b.id,
      "immediate Edit owns pending B",
    );
    await page.locator("[data-configuration-close]").click();
    await page.locator("#launch-configuration-select").selectOption("vanilla");
    await page.waitForFunction(
      () =>
        orange.launchConfigurationUi.model.cover?.status.configurationId ===
        "vanilla",
    );
    await ask("fixtureReleaseSelection");
    await page.waitForTimeout(50);
    assert.equal(
      (await ask("launchConfigurations")).selectedId,
      "vanilla",
      "older delayed selection cannot land after newer choice",
    );
    await page.locator("#launch-configuration-select").selectOption(a.id);
    await page.waitForFunction(
      (id) =>
        document.querySelector("#launch-configuration-select").value === id,
      a.id,
    );
    await page.locator("[data-configuration-edit]").click();
    await page.waitForSelector(
      '#configuration-editor [data-configuration-mod="B"]',
    );
    assert.equal(
      await page
        .locator("#configuration-editor [data-configuration-mod]")
        .count(),
      2,
    );
    {
      const handle = page.locator(
        '[data-mod-drag-kind="configuration"][data-mod-drag="B"]',
      );
      const hb = await handle.boundingBox(),
        target = await page
          .locator('[data-configuration-mod="A"]')
          .boundingBox();
      await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2);
      await page.mouse.down();
      await page.mouse.move(hb.x + hb.width / 2, target.y + target.height - 3, {
        steps: 10,
      });
      await page.locator(".mod-drag-ghost").waitFor();
      await page.keyboard.press("Escape");
      await page.mouse.up();
      assert.deepEqual(
        await page.evaluate(() => orange.launchConfigurationUi.editor().ids),
        ["B", "A"],
        "Escape restores configuration drag preview",
      );
    }
    await page.locator('[data-configuration-down="B"]').click();
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () =>
        !orange.launchConfigurationUi.editor()?.saving &&
        document
          .querySelector("[data-configuration-editor-status]")
          .textContent.includes("已保存"),
    );
    assert.deepEqual(
      (await ask("configurationRead", { id: a.id })).ordinaryIds,
      ["A", "B"],
    );

    await page.locator("[data-configuration-sort]").click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.deepEqual(
      await page.evaluate(() => orange.launchConfigurationUi.editor().ids),
      ["A", "B"],
    );
    const normalRoot = path.join(
      info.data,
      "launcher-configurations",
      require("node:crypto")
        .createHash("sha256")
        .update("profile\n" + info.user.toUpperCase() + "\n" + a.id)
        .digest("hex"),
    );
    const normalDefault = path.join(normalRoot, "mods/default.txt"),
      noOpBytes = fs.readFileSync(normalDefault),
      noOpTime = fs.statSync(normalDefault).mtimeMs;
    const noOpStart = calls.length;
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.deepEqual(fs.readFileSync(normalDefault), noOpBytes);
    assert.equal(
      fs.statSync(normalDefault).mtimeMs,
      noOpTime,
      "unchanged ordinary save preserves original mtime",
    );
    assert.ok(
      !calls
        .slice(noOpStart)
        .some(
          (c) =>
            c.payload?.action === "componentsAction" ||
            c.payload?.action === "javaSave",
        ),
    );
    assert.ok(
      !fs
        .readdirSync(normalRoot, { recursive: true })
        .some((f) => /\.(exe|jar)$/i.test(f)),
      "normal profile references current files without copying game or Java payloads",
    );
    await page.locator('[data-configuration-tab="components"]').click();
    assert.equal(
      await page.locator("[data-configuration-save]").isVisible(),
      false,
      "component tab has immediate changes without ordinary save footer",
    );
    const twins = page.locator('[data-configuration-component="Twin"]');
    await twins.first().waitFor();
    assert.equal(await twins.count(), 2);
    assert.ok(
      (await twins.allTextContents()).some((t) => t.includes("999112")),
      "duplicate sources show readable Workshop identity",
    );
    await twins
      .filter({ hasText: "999112" })
      .locator("[data-configuration-component-toggle]")
      .click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    const twinInventory = (
      await ask("configurationRequest", {
        id: a.id,
        action: "javaComponentsList",
        payload: {},
      })
    ).data;
    assert.deepEqual(
      twinInventory.rows
        .filter((r) => r.modId === "Twin" && r.enabled)
        .map((r) => r.workshopId),
      ["999112"],
    );
    assert.equal(
      await twins
        .filter({ hasText: "999111" })
        .locator("[data-configuration-approve]")
        .count(),
      0,
    );
    await page.screenshot({
      path: path.join(out, "normal-components-enabled.png"),
    });
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-component-toggle]',
      )
      .click();
    await page.waitForFunction(() =>
      document
        .querySelector('[data-configuration-component="Java"]')
        ?.textContent.includes("已启用"),
    );
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-approve]',
      )
      .click();
    await page.waitForFunction(() =>
      document
        .querySelector('[data-configuration-component="Java"]')
        ?.textContent.includes("已允许"),
    );
    assert.deepEqual(
      (await ask("configurationRead", { id: a.id })).ordinaryIds,
      ["A", "B"],
    );
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    await page.screenshot({
      path: path.join(out, "normal-components-approved.png"),
    });
    const ownedStatus = (
      await ask("configurationRequest", {
        id: a.id,
        action: "status",
        payload: {},
      })
    ).data;
    assert.ok(
      ownedStatus.components.some(
        (c) => c.configured && c.title === "测试 Java 组件",
      ),
      "cover includes generic selected components",
    );
    await page.locator("[data-configuration-close]").click();
    assert.doesNotMatch(
      await page.locator("#page").innerText(),
      /核心组件尚未配置/,
    );
    await page.screenshot({ path: path.join(out, "home-normal-selected.png") });
    for (const name of ["界面配置 C", "界面配置 D"]) {
      await page.locator("[data-configuration-create]").click();
      await page.locator("[data-configuration-new-name]").fill(name);
      await page.locator("[data-create-confirm]").click();
      await page.waitForFunction(
        () =>
          !!orange.launchConfigurationUi.editor()?.view &&
          !orange.launchConfigurationUi.editor()?.reading,
      );
      assert.match(
        await page.locator("#configuration-editor h2").innerText(),
        new RegExp(name),
      );
      await page.screenshot({
        path: path.join(
          out,
          name.endsWith("C") ? "normal-editor.png" : "normal-editor-second.png",
        ),
      });
      await page.locator("[data-configuration-name]").fill(name + " 改名");
      await page.locator("[data-configuration-rename]").click();
      await page.waitForFunction(
        () => !orange.launchConfigurationUi.editor()?.saving,
      );
      assert.match(
        await page.locator("#configuration-editor h2").innerText(),
        /改名/,
      );
      await page
        .locator("[data-configuration-version]")
        .selectOption(info.version2);
      await page.locator("[data-configuration-set-version]").click();
      await page.waitForFunction(
        () => !orange.launchConfigurationUi.editor()?.saving,
      );
      const owner = await page.evaluate(
        () => orange.launchConfigurationUi.editor().id,
      );
      assert.equal(
        (await ask("configurationRead", { id: owner })).versionId,
        info.version2,
      );
      await page.locator("[data-configuration-close]").click();
    }
    await ask("fixtureMissing");
    await page.locator("[data-configuration-reload]").click();
    await page.waitForFunction(
      () => orange.launchConfigurationUi.model.selected?.available === false,
    );
    assert.equal(
      await page.locator("#launch-configuration-select").isEnabled(),
      true,
    );
    await page.locator("[data-configuration-edit]").click();
    await page.waitForSelector("[data-configuration-version]");
    await page.locator("[data-configuration-version]").selectOption("steam");
    await page.locator("[data-configuration-set-version]").click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.match(
      await page.locator("[data-configuration-editor-status]").innerText(),
      /游戏版本已保存/,
    );
    await page.locator("[data-configuration-close]").click();
    await ask("fixtureRestoreVersion");
    const deletedId = await page
      .locator("#launch-configuration-select")
      .inputValue();
    await page.locator("[data-configuration-edit]").click();
    await page.waitForSelector("[data-configuration-delete]");
    await page.locator("[data-configuration-delete]").click();
    await page.locator("#confirm-yes").click();
    await page.waitForSelector("#configuration-editor", { state: "detached" });
    assert.ok(
      !(await ask("launchConfigurations")).configurations.some(
        (r) => r.id === deletedId,
      ),
      "confirmed native named delete removes registered choice",
    );

    await page
      .locator("#launch-configuration-select")
      .selectOption("snapshot:" + info.snapshotId);
    await page.waitForFunction(
      () =>
        orange.launchConfigurationUi.model.selected?.kind === "frozen" &&
        !!orange.launchConfigurationUi.model.cover,
    );
    await page.locator("[data-configuration-edit]").click();
    await page.waitForSelector('[data-configuration-mod="B"]');
    {
      const handle = page.locator(
        '[data-mod-drag-kind="configuration"][data-mod-drag="B"]',
      );
      const hb = await handle.boundingBox(),
        target = await page
          .locator('[data-configuration-mod="A"]')
          .boundingBox();
      await page.mouse.move(hb.x + hb.width / 2, hb.y + hb.height / 2);
      await page.mouse.down();
      await page.mouse.move(hb.x + hb.width / 2, target.y + target.height - 3, {
        steps: 10,
      });
      await page.locator(".mod-drag-ghost").waitFor();
      await page.keyboard.press("Escape");
      await page.mouse.up();
      assert.deepEqual(
        await page.evaluate(() => orange.launchConfigurationUi.editor().ids),
        ["B", "A"],
        "Escape restores configuration drag preview",
      );
    }
    await page.locator('[data-configuration-down="B"]').click();
    await page.locator('[data-source-key="Added"]').count();
    await page
      .locator(".configuration-source")
      .filter({ hasText: "模组 Added" })
      .getByRole("button")
      .click();
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () =>
        !orange.launchConfigurationUi.editor()?.saving &&
        document
          .querySelector("[data-configuration-editor-status]")
          .textContent.includes("已保存"),
    );
    let frozen = (
      await ask("configurationRead", { id: "snapshot:" + info.snapshotId })
    ).frozen;
    assert.deepEqual(frozen.ordinaryIds, ["A", "B", "Added"]);
    const manifest = JSON.parse(
      fs
        .readFileSync(
          path.join(info.snapshotRoot, "environment-manifest.json"),
          "utf8",
        )
        .replace(/^\uFEFF/, ""),
    );
    const added = manifest.Record.ModRoots.find((r) =>
      r.ModIds.includes("Added"),
    );
    assert.equal(
      fs.readFileSync(path.join(added.CopiedRoot, "payload.bin"), "utf8"),
      "current source Added",
    );
    await ask("fixtureUpdate");
    assert.equal(
      fs.readFileSync(path.join(added.CopiedRoot, "payload.bin"), "utf8"),
      "current source Added",
    );
    await page.locator('[data-configuration-remove="Added"]').click();
    await ask("fixtureLock");
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () =>
        !orange.launchConfigurationUi.editor()?.saving &&
        document
          .querySelector("[data-configuration-editor-status]")
          .textContent.includes("草稿已保留"),
    );
    assert.equal(
      await page.locator('[data-configuration-mod="Added"]').count(),
      0,
    );
    assert.deepEqual(
      (await ask("configurationRead", { id: "snapshot:" + info.snapshotId }))
        .frozen.ordinaryIds,
      ["A", "B", "Added"],
    );
    await page.screenshot({
      path: path.join(out, "snapshot-failure-draft.png"),
    });
    await ask("fixtureUnlock");
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () =>
        !orange.launchConfigurationUi.editor()?.saving &&
        document
          .querySelector("[data-configuration-editor-status]")
          .textContent.includes("已保存"),
    );
    assert.equal(fs.existsSync(added.CopiedRoot), false);
    await page
      .locator(".configuration-source")
      .filter({ hasText: "模组 Big" })
      .getByRole("button")
      .click();
    await ask("fixtureSlowCopy");
    const copyStart = events.length;
    await page.locator("[data-configuration-save]").click();
    while (
      !events.slice(copyStart).some((e) => e.data?.progress?.stage === "copy")
    )
      await page.waitForTimeout(20);
    const progressStyle = await page.locator(".configuration-editor progress").evaluate(p => ({
      height: p.getBoundingClientRect().height,
      track: getComputedStyle(p).backgroundColor,
    }));
    assert.equal(progressStyle.height, 8, "copy progress has a visible 8px track");
    assert.notEqual(progressStyle.track, "rgba(0, 0, 0, 0)");
    assert.ok(fs.readFileSync(path.join(webRoot, "app.css"), "utf8").includes(".configuration-editor progress::-webkit-progress-value{background:var(--accent)"), "copy progress fill uses the theme accent");
    await page.screenshot({
      path: path.join(out, "snapshot-copy-progress.png"),
    });
    await page.locator("[data-configuration-cancel]").click();
    await page.waitForFunction(
      () =>
        !orange.launchConfigurationUi.editor()?.saving &&
        document
          .querySelector("[data-configuration-editor-status]")
          .textContent.includes("草稿已保留"),
    );
    assert.ok(
      !(
        await ask("configurationRead", { id: "snapshot:" + info.snapshotId })
      ).frozen.ordinaryIds.includes("Big"),
    );
    assert.equal(
      await page.locator('[data-configuration-mod="Big"]').count(),
      1,
    );
    await page.screenshot({
      path: path.join(out, "snapshot-cancel-draft.png"),
    });
    await ask("fixtureSlowCopy");
    const retryStart = events.length;
    await page.locator("[data-configuration-save]").click();
    const retryDeadline = Date.now() + 10000;
    while (
      !events.slice(retryStart).some((e) => e.data?.progress?.stage === "copy")
    ) {
      if (Date.now() > retryDeadline)
        throw Error("retry did not enter real copy");
      await page.waitForTimeout(20);
    }
    await page.locator("[data-configuration-close]").click();
    await page.locator("#launch-configuration-select").selectOption(b.id);
    await page.locator("[data-configuration-edit]").click();
    await page.waitForFunction(
      () =>
        !!orange.launchConfigurationUi.editor()?.view &&
        !orange.launchConfigurationUi.editor()?.reading,
    );
    await page.locator('[data-source-key="A"]').click();
    await page.locator("[data-configuration-search]").fill("B focused draft");
    await page.waitForFunction(
      () =>
        ![...orange.launchConfigurationUi.model.jobs.values()].some(
          (j) => j.active,
        ),
    );
    assert.deepEqual(
      await page.evaluate(() => orange.launchConfigurationUi.editor().ids),
      ["A"],
    );
    assert.equal(
      await page.locator("[data-configuration-search]").inputValue(),
      "B focused draft",
    );
    assert.equal(
      await page
        .locator("[data-configuration-search]")
        .evaluate((e) => e === document.activeElement),
      true,
    );
    assert.deepEqual(
      (await ask("configurationRead", { id: b.id })).ordinaryIds,
      [],
    );
    await page.screenshot({
      path: path.join(out, "owner-B-draft-after-A-copy.png"),
    });
    await page.locator("[data-configuration-close]").click();
    await page.locator("#confirm-yes").click();
    await page
      .locator("#launch-configuration-select")
      .selectOption("snapshot:" + info.snapshotId);
    await page.locator("[data-configuration-edit]").click();
    await page.waitForFunction(
      () =>
        !!orange.launchConfigurationUi.editor()?.view &&
        !orange.launchConfigurationUi.editor()?.reading,
    );
    assert.ok(
      (
        await ask("configurationRead", { id: "snapshot:" + info.snapshotId })
      ).frozen.ordinaryIds.includes("Big"),
    );
    await page
      .locator(".configuration-source")
      .filter({ hasText: "测试 Java 组件" })
      .getByRole("button")
      .click();
    await page.locator("[data-configuration-save]").click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    await page.locator('[data-configuration-tab="components"]').click();
    await page.locator('[data-configuration-component="Java"]').waitFor();
    await ask("fixtureLock");
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-component-toggle]',
      )
      .click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.ok(
      !(
        await ask("configurationRead", { id: frozenId })
      ).frozen.componentIds.includes("Java"),
    );
    await page.screenshot({
      path: path.join(out, "snapshot-component-failure.png"),
    });
    await ask("fixtureUnlock");

    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-component-toggle]',
      )
      .click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.match(
      await page.locator('[data-configuration-component="Java"]').innerText(),
      /已启用/,
      "explicit snapshot component enable moves only this component out of ordinary list",
    );
    await page
      .locator("[data-known-component-sources]")
      .evaluate((e) => (e.open = true));
    await page.locator('[data-configuration-known="buddy"]').click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-approve]',
      )
      .click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    let javaView = (await ask("configurationRead", { id: frozenId })).frozen;
    assert.equal(javaView.java.find((r) => r.id === "Java").decision, true);
    const javaRoot = JSON.parse(
      fs
        .readFileSync(
          path.join(info.snapshotRoot, "environment-manifest.json"),
          "utf8",
        )
        .replace(/^\uFEFF/, ""),
    ).Record.ModRoots.find((r) => r.ModIds.includes("Java")).CopiedRoot;
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-component-toggle]',
      )
      .click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.equal(fs.existsSync(javaRoot), true, "disable retains copied root");
    await page
      .locator(
        '[data-configuration-component="Java"] [data-configuration-component-remove]',
      )
      .click();
    await page.locator("#confirm-yes").click();
    await page.waitForFunction(
      () => !orange.launchConfigurationUi.editor()?.saving,
    );
    assert.equal(
      fs.existsSync(javaRoot),
      false,
      "uninstall removes last unreferenced copied root",
    );
    await page.locator('[data-configuration-tab="mods"]').click();
    for (const [file, bytes] of Object.entries(original))
      assert.equal(fs.readFileSync(file).toString("base64"), bytes);
    assert.deepEqual(
      (await ask("configurationRead", { id: b.id })).ordinaryIds,
      [],
    );
    for (const theme of ["dark", "light"])
      for (const width of [1366, 420]) {
        await ask("theme", { value: theme });
        await page.setViewportSize({ width, height: 900 });
        await page.evaluate(
          (theme) =>
            document.documentElement.classList.toggle(
              "light",
              theme === "light",
            ),
          theme,
        );
        assert.equal(
          await page.evaluate(() =>
            document.documentElement.classList.contains("light"),
          ),
          theme === "light",
        );
        await page.screenshot({
          path: path.join(out, `editor-${theme}-${width}.png`),
        });
        assert.equal(
          await page.evaluate(
            () => document.documentElement.scrollWidth > innerWidth,
          ),
          false,
        );
      }
    await page.locator("[data-configuration-close]").click();
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.locator("#launch-configuration-select").selectOption(a.id);
    await page.locator("[data-configuration-edit]").click();
    await page.waitForFunction(
      () =>
        !!orange.launchConfigurationUi.editor()?.view &&
        !orange.launchConfigurationUi.editor()?.reading,
    );
    console.log("native long catalog start");
    await ask("fixtureMany");
    await ask("fixtureSlowRead");
    const scanStart = events.length;
    await page.locator("[data-configuration-editor-reload]").click();
    const scanDeadline = Date.now() + 10000;
    while (
      !events
        .slice(scanStart)
        .some((e) => e.ev === "configurationProgress" && e.data.readId)
    ) {
      if (Date.now() > scanDeadline)
        throw Error(
          "editor reload produced no scoped progress: " +
            (await page
              .locator("[data-configuration-editor-status]")
              .innerText()),
        );
      await page.waitForTimeout(20);
    }
    assert.equal(
      (await native({ action: "fixtureCanRestart", payload: {} })).ok,
      false,
      "updater drain sees active owned read",
    );
    await page.locator("[data-configuration-close]").click();
    await page.locator("#launch-configuration-select").selectOption(b.id);
    await page.locator("[data-configuration-edit]").click();
    await page.waitForFunction(
      () =>
        !!orange.launchConfigurationUi.editor()?.view &&
        !orange.launchConfigurationUi.editor()?.reading,
    );
    assert.ok(
      calls.some(
        (c) => c.action === "configurationReadCancel" && c.payload.id === a.id,
      ),
      "close cancels only A read token",
    );
    await ask("fixtureCanRestart");
    assert.equal(await page.locator(".configuration-source").count(), 807);
    for (const width of [1366, 420]) {
      await page.setViewportSize({ width, height: 900 });
      await page
        .locator("[data-configuration-editor-body]")
        .evaluate((e) => (e.scrollTop = e.scrollHeight));
      await page
        .locator("[data-configuration-available]")
        .evaluate((e) => (e.scrollTop = e.scrollHeight));
      const visible = await page
        .locator(
          "[data-configuration-close],[data-configuration-save],#configuration-editor h2",
        )
        .evaluateAll((es) =>
          es.every((e) => {
            const r = e.getBoundingClientRect();
            return r.top >= 0 && r.bottom <= innerHeight;
          }),
        );
      assert.equal(
        visible,
        true,
        "800 source list preserves title, Close and primary save",
      );
      await page.screenshot({
        path: path.join(out, "long-catalog-light-" + width + ".png"),
      });
    }
    await page.locator("[data-configuration-search]").fill("Extra0799");
    await page.locator(".configuration-source").getByRole("button").click();
    await page.locator("[data-configuration-search]").fill("keep focus");
    await page.waitForTimeout(1600);
    assert.equal(
      await page.locator("[data-configuration-search]").inputValue(),
      "keep focus",
    );
    assert.equal(
      await page
        .locator("[data-configuration-search]")
        .evaluate((e) => e === document.activeElement),
      true,
    );
    assert.deepEqual(
      await page.evaluate(() => orange.launchConfigurationUi.editor().ids),
      ["Extra0799"],
    );
    await page.locator("[data-configuration-close]").click();
    await page.locator("#confirm-yes").click();
    assert.deepEqual(
      (await ask("configurationRead", { id: b.id })).ordinaryIds,
      [],
    );
    await page.setViewportSize({ width: 1366, height: 900 });
    await page.locator("#launch-configuration-select").selectOption("steam");
    await page.waitForFunction(
      () => orange.state.status?.configurationId === "steam",
    );
    await page.locator("[data-force-takeover]").click();
    await page.locator("[data-force-confirm]").click();
    await page.waitForFunction(() =>
      sent.some(
        (m) =>
          m.action === "configurationRequest" &&
          m.payload.action === "forceTakeoverApply",
      ),
    );
    await page.waitForFunction(() => !orange.state.pendingMutations);
    const vdf = fs.readFileSync(
      path.join(info.root, "steam/userdata/44/config/localconfig.vdf"),
      "utf8",
    );
    assert.ok(
      !vdf.includes("-fixture-option") && vdf.includes("-keep-other-game"),
      "one-confirmation native takeover clears only fixture game options",
    );
    await page.locator("#launch-configuration-select").selectOption(b.id);
    await page.waitForFunction(
      () =>
        orange.state.status?.configurationId ===
        document.querySelector("#launch-configuration-select").value,
    );
    await page.locator("#launch").click();
    await page.waitForFunction(() => sent.some((m) => m.action === "launch"));
    await page.waitForFunction(() => !orange.state.launchAttempt);
    assert.ok(
      calls.some((c) => c.action === "launch" && !c.response.ok),
      "production launch entry deliberately stops in TestMode",
    );
    assert.equal(
      await page.locator("#launch-configuration-select").isEnabled(),
      true,
    );
    const buddyCall = calls.find(
      (c) =>
        c.action === "configurationRequest" &&
        c.payload.action === "componentsAction",
    );
    const commitEvent = events.find(
      (e) =>
        e.ev === "frozenConfigurationProgress" &&
        e.data.progress?.stage === "committed" &&
        e.receivedAt >= buddyCall.startedAt,
    );
    assert.ok(
      buddyCall.startedAt + buddyCall.durationMs - commitEvent.receivedAt <
        5000,
      "trusted same-writer finalization avoids large journal parse",
    );
    const closeOwner=(await ask('configurationRead',{id:frozenId})).frozen;
    await ask('fixtureSlow');const closingStart=events.length;
    const closingRead=native({action:'frozenConfigurationSources',payload:{snapshotId:info.snapshotId,manifestHash:closeOwner.manifestHash,readId:'d'.repeat(32)}});
    const closingDeadline=Date.now()+10000;
    while(!events.slice(closingStart).some(e=>e.ev==='frozenConfigurationProgress'&&e.data.readId==='d'.repeat(32))){if(Date.now()>closingDeadline)throw Error('frozen close read did not begin');await page.waitForTimeout(20);}
    assert.equal((await native({action:'fixtureCanRestart',payload:{}})).ok,false);
    await ask('fixtureDispose');assert.equal((await closingRead).ok,false,'root dispose cancels active frozen source reads');
    assert.deepEqual(fixture.errors, []);
    console.log(
      "PASS unified native contracts, DOM ordinary/components/approval and real snapshot copy/failure/cancel/retry",
    );
    fs.writeFileSync(
      path.join(out, "report.json"),
      JSON.stringify(
        {
          scope:
            "headless DOM -> production controller -> isolated real files; no gameplay",
          info,
          calls,
          events,
          errors: fixture.errors,
        },
        null,
        2,
      ),
    );
  } finally {
    fs.writeFileSync(
      path.join(out, "trace.json"),
      JSON.stringify({ calls, events }, null, 2),
    );
    if (page) {
      await page
        .screenshot({ path: path.join(out, "last-state.png") })
        .catch(() => {});
      console.log(
        await page
          .evaluate(() => ({
            ui: orange.launchConfigurationUi.model.error,
            status: orange.state.status?.configurationId,
            saving: orange.launchConfigurationUi.editor()?.saving,
            message: orange.launchConfigurationUi.editor()?.message,
            errors: window.testErrors,
            sent: sent.slice(-3),
          }))
          .catch(() => ({})),
      );
    }
    sink = () => {};
    if (page) await page.close();
    if (browser) await browser.close();
    child.stdin.end();
  }
})().catch((e) => {
  console.error(e);
  child.stdin.end();
  process.exitCode = 1;
});
