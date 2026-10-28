/* Independent review regressions. Real shipped DOM/events; native WebView replies
 * are synthetic. These tests do not validate native file writes or game launch. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { setup, route, chromium } = require('./experience-ui-tests.cjs');
const output = path.resolve(process.env.OZL_REVIEW_ARTIFACT_DIR || path.join(__dirname, '../../artifacts/mod-manager-state-review-1239'));
const B = 'live:' + 'a'.repeat(32);
const tests = [], results = [], observations = [];
const test = (name, run) => tests.push({ name, run });
const inspect = () => ({ native: fx.configurationId, selected: orange.launchConfigurationUi.model.selected?.id, stored: orange.launchConfigurationUi.model.list?.selectedId, status: orange.state.status.configurationId, ids: orange.state.modProfileMods, dirty: orange.state.modProfileDirty, error: orange.launchConfigurationUi.model.error });

async function withPage(browser, fn, theme = 'dark') {
  const { page, errors } = await setup(browser, theme);
  try { await fn(page); assert.deepEqual(errors, [], 'No uncaught page errors'); }
  finally { await page.close(); }
}
async function select(page, id) {
  await route(page, 'home');
  await page.locator('#launch-configuration-select').selectOption(id);
  await page.waitForFunction(id => orange.launchConfigurationUi.model.cover?.status.configurationId === id && !orange.launchConfigurationUi.model.selecting, id);
}
async function configFixture(page) {
  await page.evaluate(() => {
    fx.catalog = ['A', 'B', 'C'].map(id => ({ id, name: id, requires: [] }));
    fx.defaultMods = ['A']; fx.configurationMods.launcher = ['A'];
    fx.configurationMods['live:' + 'a'.repeat(32)] = ['C'];
    const base = chrome.webview.postMessage;
    chrome.webview.postMessage = m => {
      if (m.action === 'configurationRequest' && m.payload.action === 'modProfiles') {
        sent.push(m);
        queueMicrotask(() => emitNative({ id: m.id, ok: true, data: { configurationId: m.payload.id, data: { stamp: 'review-stamp', profiles: [], activeMods: fx.configurationMods[m.payload.id] || [] } } }));
      } else base(m);
    };
    orange.state.modCatalog = null; orange.state.modProfiles = null;
  });
}

test('configuration drafts survive A-B-A; rejected selection and reload release correctly', browser => withPage(browser, async page => {
  await configFixture(page);
  await route(page, 'mods'); await page.locator('[data-mod-membership="B"]').check();
  await select(page, B); await route(page, 'mods');
  await page.waitForFunction(() => orange.state.modProfiles?.activeMods?.includes('C'));
  assert.deepEqual(await page.locator('#profile-mod-catalog [data-mod-row]').evaluateAll(rows => rows.map(row => row.dataset.modRow)), ['C']);
  await select(page, 'launcher'); await route(page, 'mods');
  const restored = await page.evaluate(inspect);
  assert.deepEqual(restored.ids, ['A', 'B']); assert.equal(restored.dirty, true);
  await route(page, 'home'); await page.evaluate(() => fx.failure = 'launchConfigurationSelect');
  await page.locator('#launch-configuration-select').selectOption(B);
  await page.waitForFunction(() => !orange.launchConfigurationUi.model.selecting && !!orange.launchConfigurationUi.model.error);
  await route(page, 'mods'); const failed = await page.evaluate(inspect);
  assert.deepEqual(failed.ids, ['A', 'B']); assert.equal(failed.selected, 'launcher'); assert.equal(failed.native, 'launcher');
  await route(page, 'home'); await page.evaluate(() => fx.failure = 'launchConfigurations');
  await page.locator('[data-configuration-reload]').click();
  await page.waitForFunction(() => !orange.launchConfigurationUi.model.reading && !!orange.launchConfigurationUi.model.error);
  observations.push({ name: 'configuration-draft-ownership', restored, failed, afterReload: await page.evaluate(inspect) });
}));

test('successful native selection is retained when its subsequent cover read fails', browser => withPage(browser, async page => {
  await page.evaluate(() => fx.failure = 'memoryGet');
  await page.locator('#launch-configuration-select').selectOption(B);
  await page.waitForFunction(() => !orange.launchConfigurationUi.model.selecting && !!orange.launchConfigurationUi.model.error);
  const state = await page.evaluate(inspect);
  observations.push({ name: 'post-commit-cover-failure', state });
  assert.equal(state.native, B); assert.equal(state.selected, B); assert.equal(state.stored, B);
  assert.equal(await page.locator('#launch-configuration-select').inputValue(), B);
}));

test('late old catalog cannot replace the new configuration or report its obsolete error', browser => withPage(browser, async page => {
  await page.evaluate(() => { fx.hold = 'modCatalog'; orange.state.modCatalog = null; });
  await route(page, 'mods');
  await page.waitForFunction(() => held.some(m => m.payload?.action === 'modCatalog' && m.payload.id === 'launcher'));
  await select(page, B); await route(page, 'mods');
  await page.waitForFunction(() => held.some(m => m.payload?.action === 'modCatalog' && m.payload.id.startsWith('live:')));
  await page.evaluate(() => { const i = held.findIndex(m => m.payload?.action === 'modCatalog' && m.payload.id.startsWith('live:')); fx.hold = ''; replyNative(held.splice(i, 1)[0]); });
  await page.waitForFunction(() => orange.state.modCatalog && !orange.state.loading.modCatalog);
  await page.evaluate(() => { const i = held.findIndex(m => m.payload?.action === 'modCatalog' && m.payload.id === 'launcher'); replyNative(held.splice(i, 1)[0]); });
  await page.waitForTimeout(40);
  assert.equal(await page.evaluate(() => orange.state.status.configurationId), B);
  assert.equal(await page.evaluate(() => orange.state.errors.modCatalog || ''), '');
  assert.equal(await page.evaluate(() => orange.state.notice || ''), '');
}));

test('late failed saved-profile read cannot write an error into the next configuration', browser => withPage(browser, async page => {
  await route(page, 'mods'); await page.evaluate(() => fx.hold = 'modProfileGet');
  await page.locator('#mod-workspace-select').selectOption('p1');
  await page.waitForFunction(() => held.some(m => m.action === 'modProfileGet' || m.payload?.action === 'modProfileGet'));
  await select(page, B); await route(page, 'mods');
  await page.evaluate(() => { fx.hold = ''; fx.failure = 'modProfileGet'; replyNative(held.splice(held.findIndex(m => m.action === 'modProfileGet' || m.payload?.action === 'modProfileGet'), 1)[0]); });
  await page.waitForTimeout(40);
  observations.push({ name: 'late-profile-error', state: await page.evaluate(() => ({ error: orange.state.errors.modProfile, notice: orange.state.notice, loading: orange.state.loading.modProfile })) });
  assert.equal(await page.evaluate(() => orange.state.errors.modProfile || ''), '');
  assert.equal(await page.evaluate(() => orange.state.notice || ''), '');
  assert.equal(await page.evaluate(() => orange.state.loading.modProfile), false);
}));

test('drag changes the right order while preserving both independently scrolled lists', browser => withPage(browser, async page => {
  await page.evaluate(() => {
    const ids = Array.from({ length: 60 }, (_, i) => 'Review' + String(i).padStart(3, '0'));
    fx.defaultMods = ids; fx.catalog = ids.map(id => ({ id, name: id, requires: [] }));
    orange.state.modCatalog = null; orange.state.modProfiles = null;
  });
  await route(page, 'mods'); await page.waitForSelector('#profile-mod-catalog [data-mod-drag]');
  const before = await page.evaluate(() => {
    const right = document.querySelector('#profile-mod-catalog'), left = document.querySelector('#profile-mod-available'); right.scrollTop = 600; left.scrollTop = 150;
    const box = right.getBoundingClientRect(), visible = [...right.querySelectorAll('[data-mod-row]')].filter(row => { const b = row.getBoundingClientRect(); return b.top > box.top + 10 && b.bottom < box.bottom - 10; }).map(row => row.dataset.modRow);
    return { right: right.scrollTop, left: left.scrollTop, visible };
  });
  const first = before.visible[0], last = before.visible.at(-1);
  const a = await page.locator('#profile-mod-catalog [data-mod-drag="' + first + '"]').boundingBox();
  const b = await page.locator('#profile-mod-catalog [data-mod-row="' + last + '"]').boundingBox();
  await page.mouse.move(a.x + a.width / 2, a.y + a.height / 2); await page.mouse.down();
  await page.mouse.move(a.x + a.width / 2, b.y + b.height - 3, { steps: 6 });
  await page.locator('.mod-drag-ghost').waitFor(); await page.mouse.up();
  await page.locator('.mod-drag-ghost').waitFor({ state: 'detached' });
  const after = await page.evaluate(() => ({ left: document.querySelector('#profile-mod-available').scrollTop, right: document.querySelector('#profile-mod-catalog').scrollTop, ids: orange.state.modProfileMods, dirty: orange.state.modProfileDirty }));
  assert.equal(after.left, before.left); assert.ok(Math.abs(after.right - before.right) < 40);
  assert.ok(after.ids.indexOf(first) > after.ids.indexOf(last)); assert.equal(after.dirty, true);
  observations.push({ name: 'dual-scroll-drag', before, after: { left: after.left, right: after.right, dirty: after.dirty } });
}));

test('independent searches and filtered drag preserve hidden order and refresh dependency states', browser => withPage(browser, async page => {
  await page.evaluate(() => {
    fx.catalog = [
      { id: 'A', name: 'Alpha Visible', requires: [] },
      { id: 'H', name: 'Hidden only', requires: [] },
      { id: 'B', name: 'Bravo Visible', requires: ['A'], conflict: true },
      { id: 'C', name: 'Charlie Visible', requires: ['B'] },
    ];
    fx.defaultMods = ['A', 'H', 'B', 'C']; orange.state.modCatalog = null; orange.state.modProfiles = null;
  });
  await route(page, 'mods');
  await page.locator('#profile-mod-search').fill('Hidden');
  await page.locator('#profile-enabled-search').fill('Visible');
  assert.deepEqual(await page.locator('#profile-mod-available [data-mod-row]').evaluateAll(rows => rows.map(row => row.dataset.modRow)), ['H']);
  assert.deepEqual(await page.locator('#profile-mod-catalog [data-mod-row]').evaluateAll(rows => rows.map(row => row.dataset.modRow)), ['A', 'B', 'C']);
  assert.equal(await page.locator('#profile-mod-catalog [data-mod-drag="B"]').isEnabled(), true, 'Source ambiguity does not prevent order-only editing');
  const from = await page.locator('#profile-mod-catalog [data-mod-drag="C"]').boundingBox();
  const to = await page.locator('#profile-mod-catalog [data-mod-row="A"]').boundingBox();
  await page.mouse.move(from.x + from.width / 2, from.y + from.height / 2); await page.mouse.down();
  await page.mouse.move(from.x + from.width / 2, to.y + 2, { steps: 6 });
  await page.locator('.mod-drag-ghost').waitFor(); await page.mouse.up(); await page.locator('.mod-drag-ghost').waitFor({ state: 'detached' });
  assert.deepEqual(await page.evaluate(() => orange.state.modProfileMods), ['C', 'H', 'A', 'B']);
  assert.equal(await page.locator('#profile-mod-search').inputValue(), 'Hidden');
  assert.equal(await page.locator('#profile-enabled-search').inputValue(), 'Visible');
  assert.deepEqual(await page.locator('#profile-mod-available [data-mod-row]').evaluateAll(rows => rows.map(row => row.dataset.modRow)), ['H']);
  assert.equal(await page.locator('#profile-mod-catalog [data-mod-row="C"] [data-dependency-id="B"]').getAttribute('data-dependency-state'), 'late');
  assert.equal(await page.locator('#profile-mod-catalog [data-mod-row="B"] [data-dependency-id="A"]').getAttribute('data-dependency-state'), 'ready');
}));

test('shared active-list refresh merges into the draft without losing local additions', browser => withPage(browser, async page => {
  await page.evaluate(() => { fx.catalog = ['A', 'B', 'C'].map(id => ({ id, name: id, requires: [] })); fx.defaultMods = ['A']; orange.state.modCatalog = null; orange.state.modProfiles = null; });
  await route(page, 'mods'); await page.locator('[data-mod-membership="B"]').check();
  await page.evaluate(() => fx.defaultMods = ['A', 'C']); await page.locator('[data-profiles-refresh]').click();
  await page.waitForFunction(() => orange.state.modProfileMods.includes('C'));
  assert.deepEqual(await page.evaluate(() => orange.state.modProfileMods), ['A', 'B', 'C']);
  await page.evaluate(() => fx.defaultMods = ['C']); await page.locator('[data-profiles-refresh]').click();
  await page.waitForFunction(() => !orange.state.modProfileMods.includes('A'));
  assert.deepEqual(await page.evaluate(() => orange.state.modProfileMods), ['B', 'C']);
}));

test('editor default-JAR refresh cannot overwrite the save-specific Java review owner', browser => withPage(browser, async page => {
  await page.evaluate(() => {
    const base = chrome.webview.postMessage;
    chrome.webview.postMessage = m => {
      const wrapped = m.action === 'configurationRequest', action = wrapped ? m.payload.action : m.action, payload = wrapped ? m.payload.payload : m.payload;
      if (action !== 'javaList') return base(m); sent.push(m);
      const save = !!payload.saveId, data = { supported: true, stamp: save ? 'save-stamp' : 'default-stamp', jars: [{ id: save ? 'SaveOnly' : 'DefaultOnly', name: save ? '存档专用JAR' : '默认配置JAR', sha256: (save ? 'a' : 'b').repeat(64), approvalToken: (save ? 'c' : 'd').repeat(64), path: 'D:/fixture.jar', decision: null }], warnings: [] };
      queueMicrotask(() => emitNative({ id: m.id, ok: true, data: wrapped ? { configurationId: m.payload.id, data } : data }));
    };
    orange.state.launchKind = 'save'; orange.state.launchId = 'Sandbox/Test'; orange.state.java = null; orange.state.javaOpen = true;
  });
  await route(page, 'help'); await page.waitForFunction(() => orange.state.java?.jars[0]?.id === 'SaveOnly');
  const before = await page.evaluate(() => ({ row: orange.state.java.jars[0].id, key: orange.state.javaOwnerKey }));
  await route(page, 'home'); await page.locator('[data-configuration-edit]').first().click();
  await page.waitForFunction(() => !orange.launchConfigurationUi.editor()?.reading);
  await page.locator('[data-configuration-editor-reload]').click(); await page.waitForFunction(() => !orange.launchConfigurationUi.editor()?.reading);
  await page.evaluate(() => document.querySelector('#configuration-editor').close()); await route(page, 'help');
  const after = await page.evaluate(() => ({ row: orange.state.java.jars[0].id, key: orange.state.javaOwnerKey, save: orange.state.launchId }));
  assert.equal(after.row, 'SaveOnly'); assert.equal(after.key, before.key); assert.equal(after.save, 'Sandbox/Test');
  observations.push({ name: 'java-save-owner', before, after });
}));

test('reload shows immediate progress/cancel; cancellation, native close and reopen release the editor', browser => withPage(browser, async page => {
  await route(page, 'home'); await page.locator('[data-configuration-edit]').first().click();
  await page.waitForFunction(() => !orange.launchConfigurationUi.editor()?.reading);
  await page.evaluate(() => fx.hold = 'configurationRead'); await page.locator('[data-configuration-editor-reload]').click();
  await page.waitForFunction(() => held.some(m => m.action === 'configurationRead'));
  const pendingRead = await page.evaluate(() => held.find(m => m.action === 'configurationRead').payload);
  assert.equal(await page.locator('[data-configuration-cancel]').isVisible(), true);
  assert.equal(await page.locator('[data-configuration-progress]').isVisible(), true);
  await page.locator('[data-configuration-cancel]').click();
  await page.waitForFunction(read => sent.some(m => m.action === 'configurationReadCancel' && m.payload.id === read.id && m.payload.readId === read.readId), pendingRead);
  await page.evaluate(() => { const m = held.splice(held.findIndex(m => m.action === 'configurationRead'), 1)[0]; emitNative({ id: m.id, ok: false, error: '读取已取消 · 合成取消回复' }); });
  await page.waitForFunction(() => !orange.launchConfigurationUi.editor().reading);
  assert.equal(await page.locator('[data-configuration-save]').isEnabled(), true);
  assert.equal(await page.locator('[data-configuration-cancel]').isHidden(), true);
  await page.locator('[data-configuration-editor-reload]').click();
  await page.waitForFunction(() => held.some(m => m.action === 'configurationRead'));
  await page.evaluate(() => document.querySelector('#configuration-editor').close());
  await page.waitForFunction(() => !orange.launchConfigurationUi.editor());
  assert.ok(await page.evaluate(() => sent.filter(m => m.action === 'configurationReadCancel').length >= 2));
  await page.evaluate(() => { fx.hold = ''; const m = held.splice(held.findIndex(m => m.action === 'configurationRead'), 1)[0]; emitNative({ id: m.id, ok: false, error: '读取已取消 · 关闭后的迟到回复' }); });
  await page.locator('[data-configuration-edit]').first().click(); await page.waitForFunction(() => orange.launchConfigurationUi.editor() && !orange.launchConfigurationUi.editor().reading);
  assert.equal(await page.locator('[data-configuration-save]').isEnabled(), true);
}));

test('failed ordinary-list save keeps its draft and permits close/reopen and retry', browser => withPage(browser, async page => {
  await page.evaluate(() => { fx.catalog = ['A', 'B'].map(id => ({ id, name: id, requires: [] })); fx.configurationMods.launcher = ['A']; });
  await route(page, 'home'); await page.locator('[data-configuration-edit]').first().click();
  await page.waitForFunction(() => orange.launchConfigurationUi.editor() && !orange.launchConfigurationUi.editor().reading);
  await page.locator('[data-configuration-available] [data-source-key="B"]').click(); await page.evaluate(() => fx.failure = 'configurationSaveOrdinary');
  await page.locator('[data-configuration-save]').click(); await page.waitForFunction(() => !orange.launchConfigurationUi.editor().saving);
  assert.equal(await page.locator('[data-configuration-save]').isEnabled(), true);
  assert.deepEqual(await page.evaluate(() => orange.launchConfigurationUi.editor().ids), ['A', 'B']);
  assert.match(await page.locator('[data-configuration-editor-status]').innerText(), /查询失败.*草稿已保留/);
  await page.screenshot({ path: path.join(output, 'editor-save-failure.png') });
  await page.evaluate(() => document.querySelector('#configuration-editor').close()); await page.waitForFunction(() => !orange.launchConfigurationUi.editor());
  await page.locator('[data-configuration-edit]').first().click(); await page.waitForFunction(() => orange.launchConfigurationUi.editor() && !orange.launchConfigurationUi.editor().reading);
  assert.equal(await page.locator('[data-configuration-save]').isEnabled(), true);
}));

test('dependency-rich final layout meets desktop row density and saves six visual-review screenshots', async browser => {
  for (const theme of ['dark', 'light']) await withPage(browser, async page => {
    await page.evaluate(() => {
      fx.catalog = Array.from({ length: 1000 }, (_, i) => ({ id: 'Review' + String(i).padStart(4, '0'), name: (i % 3 ? '动态资源与武器兼容模组 ' : '【工具基础】前置资源 ') + String(i).padStart(4, '0'), author: '工坊作者', description: '与原版保留一致的模组说明。', requires: i % 4 === 1 ? ['Review0000'] : i % 4 === 2 ? ['Review0050'] : i % 4 === 3 ? ['ReviewMissing'] : [], requiresBuddy: i === 4 }));
      fx.defaultMods = fx.catalog.slice(0, 70).map(m => m.id); orange.state.modCatalog = null; orange.state.modProfiles = null;
    });
    await route(page, 'mods'); await page.waitForSelector('#profile-mod-catalog [data-mod-row]');
    for (const viewport of [{ width: 1366, height: 768 }, { width: 1769, height: 1170 }, { width: 733, height: 768 }]) {
      await page.setViewportSize(viewport);
      await page.evaluate(() => Promise.all(document.getAnimations().filter(a => Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a => a.finished.catch(() => {}))));
      const metrics = await page.evaluate(() => {
        const q = selector => { const e = document.querySelector(selector), r = e.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height, complete: [...e.querySelectorAll(':scope > [data-mod-row]')].filter(row => { const b = row.getBoundingClientRect(); return b.top >= r.top && b.bottom <= r.bottom; }).length, scrollWidth: e.scrollWidth, clientWidth: e.clientWidth }; };
        const save = document.querySelector('[data-mod-save-activate]').getBoundingClientRect();
        return { available: q('#profile-mod-available'), enabled: q('#profile-mod-catalog'), saveBottom: save.bottom, documentWidth: document.documentElement.scrollWidth, states: [...new Set([...document.querySelectorAll('[data-dependency-state]')].map(e => e.dataset.dependencyState))] };
      });
      assert.ok(metrics.documentWidth <= viewport.width); assert.ok(metrics.saveBottom <= viewport.height);
      for (const pane of [metrics.available, metrics.enabled]) { assert.ok(pane.scrollWidth <= pane.clientWidth); if (viewport.width >= 1366) assert.ok(pane.complete >= 8, JSON.stringify({ viewport, pane })); }
      assert.deepEqual(metrics.states.sort(), ['late', 'missing', 'ready']);
      const file = theme + '-' + viewport.width + '.png'; await page.screenshot({ path: path.join(output, file) });
      observations.push({ name: 'visual-layout', theme, viewport, metrics, screenshot: file });
    }
  }, theme);
});

(async () => {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    for (const { name, run } of tests) {
      const started = Date.now();
      try { await run(browser); results.push({ name, passed: true, elapsedMs: Date.now() - started }); console.log('PASS ' + name); }
      catch (error) { results.push({ name, passed: false, elapsedMs: Date.now() - started, error: error.stack || String(error) }); console.error('FAIL ' + name + '\n' + (error.stack || error)); }
    }
  } finally { await browser.close(); }
  const report = { scope: 'Headless Edge against shipped HTML/JS; actual DOM controls, pointers and native dialog close. Synthetic native replies only; no native file-result or game-launch acceptance, no user data, no publish.', results, observations };
  fs.writeFileSync(path.join(output, 'results.json'), JSON.stringify(report, null, 2));
  console.log(results.filter(r => r.passed).length + '/' + results.length + ' passed; artifacts: ' + output);
  if (results.some(r => !r.passed)) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
