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

(async()=>{const info=await ready;let browser,page,passed=false;const cases=process.argv[4]||'all';try{
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

const chosen=c=>cases==='all'||cases===c;
const frozenId='snapshot:'+info.snapshotId;
async function opened(){await page.locator('[data-configuration-edit]').click();await page.waitForFunction(()=>!!orange.launchConfigurationUi.editor()?.view);}
async function idle(){await page.waitForFunction(()=>!orange.launchConfigurationUi.editor()?.reading&&!orange.launchConfigurationUi.editor()?.saving);}
async function waitProgress(start,predicate){const deadline=Date.now()+10000;while(!events.slice(start).some(predicate)){assert.ok(Date.now()<deadline,'native read progress must start');await page.waitForTimeout(20);}}
if(chosen('R1')){
 await page.locator('#launch-configuration-select').selectOption(frozenId);await opened();await idle();
 const readingStart=events.length;await ask('fixtureSlow');await page.locator('[data-configuration-editor-reload]').click();await waitProgress(readingStart,e=>e.ev==='frozenConfigurationProgress');
 const cancelStart=calls.length;await page.locator('[data-configuration-cancel]').click();await idle();assert.ok(calls.slice(cancelStart).some(c=>c.action==='frozenConfigurationSources'&&!c.response.ok),'actual source read cancelled');assert.ok(!calls.slice(cancelStart).some(c=>c.action==='frozenConfigurationCancel'),'read Cancel leaves writer lifetime intact');
 await page.locator('[data-configuration-editor-reload]').click();await idle();assert.ok(await page.locator('.configuration-source').count(),'cancelled source read retries');
 await page.locator('[data-configuration-close]').click();await opened();await idle();assert.ok(await page.locator('.configuration-source').count(),'reopened source read succeeds');
 await ask('frozenConfigurationCancel',{snapshotId:info.snapshotId});
 const view=await ask('configurationRead',{id:frozenId});await ask('frozenConfigurationSources',{snapshotId:info.snapshotId,manifestHash:view.frozen.manifestHash,readId:'f'.repeat(32)});
 assert.ok(calls.some(c=>c.action==='frozenConfigurationReadCancel'),'read Cancel uses scoped token, never writer cancellation');
 console.log('PASS R1 UI read Cancel/Reload/reopen and cancelled writer lifetime');
 await page.locator('[data-configuration-close]').click();
}
if(chosen('R2')){
 const normal=await ask('configurationCreate',{name:'扫描切换',versionId:'steam'});await page.evaluate(()=>orange.launchConfigurationUi.reload());await page.locator('#launch-configuration-select').selectOption(normal.id);
 await ask('fixtureInvalidate');await ask('fixtureSlowRead');const scanStart=events.length;await opened();await waitProgress(scanStart,e=>e.ev==='configurationProgress'&&e.data.configurationId===normal.id&&e.data.readId&&e.data.text.includes('扫描模组'));assert.equal(await page.evaluate(()=>orange.launchConfigurationUi.editor().reading),true);
 await page.locator('[data-configuration-tab="components"]').click();await idle();
 assert.equal(await page.locator('[data-configuration-tab="components"]').getAttribute('aria-pressed'),'true');
 assert.equal(await page.evaluate(()=>!!orange.launchConfigurationUi.editor()?.inventory),true,'selected component tab fulfills load after initial mod scan');
 assert.ok(await page.locator('[data-configuration-component]').count());await page.screenshot({path:path.join(out,'review-R2-components.png')});
 console.log('PASS R2 in-flight mod scan -> components loads');await page.locator('[data-configuration-close]').click();
}
if(chosen('R3')){
 const normal=await ask('configurationCreate',{name:'已知版本',versionId:'steam'});await page.evaluate(()=>orange.launchConfigurationUi.reload());await page.locator('#launch-configuration-select').selectOption(normal.id);await page.waitForFunction(id=>orange.launchConfigurationUi.model.cover?.status.configurationId===id,normal.id);
 for(const selector of ['[data-home-actual]','[data-current-config]','#launch-configuration-select option:checked'])assert.match(await page.locator(selector).innerText(),/42\.21\.0/,'known selected-owner version wins unknown inventory');
 await page.screenshot({path:path.join(out,'review-R3-home-known.png')});console.log('PASS R3 selected known version cover/summary/selector');
}
if(chosen('R4')){
 for(const kind of ['index','manifest','critical']){
 const view=await ask('configurationRead',{id:frozenId}),old=await ask('fixtureHoldMetadata',{kind});
 const ids=view.frozen.ordinaryIds[0]==='B'?['A','B']:['B','A'];
 const result=await native({action:'frozenConfigurationSave',payload:{snapshotId:info.snapshotId,manifestHash:view.frozen.manifestHash,ordinaryIds:ids,componentIds:[],sourceTokens:[]}});
 const retained=await ask('fixtureReleaseMetadata');assert.equal(result.ok,true,'optimistic held '+kind+' handle allows real publisher replacement: '+result.error);assert.deepEqual(retained,old,'open reader retains its complete pre-replacement generation');
 const after=(await ask('configurationRead',{id:frozenId})).frozen;assert.deepEqual(after.ordinaryIds,ids);assert.notEqual(after.manifestHash,view.frozen.manifestHash);
 }console.log('PASS R4 held production index/manifest/critical handles -> real publisher replacement');
}
assert.deepEqual(fixture.errors,[]);passed=true;
fs.writeFileSync(path.join(out,'review-fixes-'+cases+'.json'),JSON.stringify({info,calls,events},null,2));
}finally{fs.writeFileSync(path.join(out,'review-fixes-'+cases+'-trace.json'),JSON.stringify({info,calls,events},null,2));if(!passed&&page)console.log(await page.evaluate(()=>({editor:orange.launchConfigurationUi.editor()?{reading:orange.launchConfigurationUi.editor().reading,tab:orange.launchConfigurationUi.editor().tab,message:orange.launchConfigurationUi.editor().message}:null,last:sent.slice(-5)})).catch(()=>({})));sink=()=>{};await ask('fixtureDispose').catch(()=>{});if(page)await page.close();if(browser)await browser.close();child.stdin.end();}})().catch(e=>{console.error(e);child.stdin.end();process.exitCode=1;});
