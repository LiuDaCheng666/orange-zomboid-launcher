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
// Each case needs an untouched installation; keep its real files independent.
if (!process.argv[4] || process.argv[4] === "all") {
  const { spawnSync } = require("node:child_process");
  for (const name of ["R1", "R2", "R3", "Minor1", "Builtins"]) {
    const result = spawnSync(process.execPath, [__filename, process.argv[2], process.argv[3], name], { stdio: "inherit", windowsHide: true });
    if (result.status !== 0) process.exit(result.status || 1);
  }
  process.exit(0);
}
assert.ok(["R1", "R2", "R3", "Minor1", "Builtins"].includes(process.argv[4]), "unknown final review case");
const out = path.resolve(process.argv[3]),
  child = spawn(
    path.join(process.argv[2], "NativeUi.exe"),
    [path.join(os.tmpdir(), "o-final-fix-" + Date.now().toString(36)), ...(process.argv[4] === "R2" ? ["health"] : [])],
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
async function opened(){await page.locator('[data-configuration-edit]').click();await page.waitForFunction(()=>!!orange.launchConfigurationUi.editor()?.view&&!orange.launchConfigurationUi.editor()?.reading);}
async function idle(){await page.waitForFunction(()=>!orange.launchConfigurationUi.editor()?.reading&&!orange.launchConfigurationUi.editor()?.saving);}
async function saved(){await page.locator('[data-configuration-save]').click();await idle();assert.match(await page.locator('[data-configuration-editor-status]').innerText(),/已保存/);}
const owner=(action,payload={})=>ask('configurationRequest',{id:'launcher',action,payload}).then(r=>r.data);
const bytes=f=>fs.readFileSync(f).toString('base64');
// A managed row makes the original Steam read distinct from owned projection.
if(cases==='Builtins')fs.writeFileSync(path.join(info.user,'mods/default.txt'),'mods\n{\n mod = B,\n mod = ZombieBuddy,\n mod = A,\n}\nmaps\n{\n}\n');
const originalPaths=[path.join(info.user,'mods/default.txt'),path.join(info.game,'ProjectZomboid64.json'),path.join(info.game,'ProjectZomboid64.bat'),path.join(info.root,'steam/userdata/44/config/localconfig.vdf')];
for(const item of ['999111','999112'])originalPaths.push(path.join(info.root,'steam/steamapps/workshop/content/108600',item,'mods/Java/42/mod.info'));
const originals=Object.fromEntries(originalPaths.map(f=>[f,bytes(f)]));
if(chosen('Builtins')){
 for(const [id,expected] of [['launcher',['B','A']],['steam',['B','ZombieBuddy','A']],['vanilla',['B','A']]]){
  const view=await ask('configurationRead',{id});assert.equal(view.id,id);assert.equal(view.available,true);assert.deepEqual(view.ordinaryIds,expected,id+' preserves its own read contract');
 }
 assert.equal(fs.existsSync(info.ownedDefault),false,'builtin reads do not initialize owned defaults');assert.equal(fs.existsSync(info.selection),false,'builtin reads do not initialize component state');
 console.log('PASS Builtins native launcher/steam/vanilla reads; Steam retains source managed rows, owned modes project, originals unchanged');
}
if(chosen('R1')){
 await opened();await page.locator('[data-configuration-tab="components"]').click();await idle();
 const twin=item=>page.locator('[data-configuration-component="Twin"]').filter({hasText:'工坊 '+item});
 await twin('999111').locator('[data-configuration-component-toggle]').click();await idle();
 await twin('999111').locator('[data-configuration-approve]').click();await idle();
 const selectedBefore=bytes(info.selection),ordinaryBefore=bytes(info.ownedDefault);
 let inventory=await owner('javaComponentsList'),other=inventory.rows.find(r=>r.modId==='Twin'&&r.workshopId==='999112');
 for(const operation of ['disable','uninstall']){const refused=await native({action:'configurationRequest',payload:{id:'launcher',action:'javaComponentsPreview',payload:{token:other.token,stamp:inventory.stamp,operation}}});assert.equal(refused.ok,false,'unselected B '+operation+' must not remove selected A');}
 const remove=twin('999112').locator('[data-configuration-component-remove]');assert.equal(await remove.isDisabled(),true,'unselected alternative cannot be removed');
 const beforeCalls=calls.length;await remove.evaluate(b=>b.click());await page.waitForTimeout(100);assert.equal(calls.length,beforeCalls,'disabled removal does not submit');
 assert.equal(bytes(info.selection),selectedBefore);assert.equal(bytes(info.ownedDefault),ordinaryBefore);
 await page.screenshot({path:path.join(out,'final-R1-duplicate-refused.png')});
 await twin('999111').locator('[data-configuration-component-toggle]').click();await idle();assert.equal(await twin('999111').locator('[data-configuration-component-remove]').isEnabled(),true,'disabled stored source remains removable');
 const decisions=JSON.parse(fs.readFileSync(info.selection,'utf8')).Decisions;
 fs.unlinkSync(path.join(info.root,'steam/steamapps/workshop/content/108600/999111/mods/Java/42/payload.jar'));
 await page.locator('[data-configuration-editor-reload]').click();await idle();
 await twin('999111').locator('[data-configuration-component-remove]').click();await page.locator('#confirm-yes').click();await idle();
 const state=JSON.parse(fs.readFileSync(info.selection,'utf8'));assert.ok(!state.Selections.some(r=>r.Id==='Twin'));assert.deepEqual(state.Decisions,decisions);assert.equal(bytes(info.ownedDefault),ordinaryBefore);
 await page.screenshot({path:path.join(out,'final-R1-disabled-missing-removed.png')});await page.locator('[data-configuration-close]').click();console.log('PASS R1 exact-source native refusal, DOM disabled alternative and stored-disabled missing-JAR removal');
}
if(chosen('R2')){
 const health=await ask('fixtureHealthState');assert.equal(health.healthPending,false);assert.ok(health.healthStatusAcknowledgments>0,'wrapped status reaches bridge-ready handshake');
 assert.ok(calls.some(c=>c.action==='launchConfigurations'&&c.response.ok));assert.ok(calls.some(c=>c.action==='configurationRequest'&&c.payload.action==='status'&&c.response.ok));
 for(const [action,payload] of [['launchConfigurations',{}],['configurationRead',{id:'launcher'}],['configurationRequest',{id:'launcher',action:'status',payload:{}}],['configurationRequest',{id:'launcher',action:'memoryGet',payload:{}}]])assert.equal((await ask('fixturePolicy',{action,payload})).capture,true);
 for(const [action,payload] of [['configurationRequest',{id:'launcher',action:'memorySave',payload:{stamp:'x',value:4096}}],['configurationRequest',{id:'launcher',action:'javaComponentsApply',payload:{previewStamp:'x',confirmed:true}}],['configurationRequest',{id:'launcher',action:'configurationRequest',payload:{id:'launcher',action:'status',payload:{}}}],['configurationRequest',{id:'launcher',action:'status',payload:{path:'forbidden'}}],['launchConfigurationSelect',{id:'steam'}]])assert.equal((await native({action:'fixturePolicy',payload:{action,payload}})).ok,false,'pending health refuses mutation/invalid recursive inner');
 for(const [action,payload] of [['configurationRequest',{id:'launcher',action:'memorySave',payload:{stamp:'x',value:4096}}],['launchConfigurationSelect',{id:'steam'}]])assert.equal((await ask('fixtureCapturePolicy',{action,payload})).capture,false,'capture separately refuses valid mutations');
 await page.screenshot({path:path.join(out,'final-R2-health-boot.png')});console.log('PASS R2 actual frontend PendingHealth boot plus production capture/read classification and strict mutation/recursive refusal');
}
if(chosen('R3')){
 assert.equal(fs.existsSync(info.ownedDefault),false,'initial read must not initialize owned file');await opened();assert.deepEqual(await page.evaluate(()=>orange.launchConfigurationUi.editor().ids),['B','A']);assert.equal(fs.existsSync(info.ownedDefault),false,'editor initial fallback stays read-only');await saved();assert.equal(bytes(info.ownedDefault),originals[path.join(info.user,'mods/default.txt')]);
 const initialBytes=bytes(info.ownedDefault),initialMtime=fs.statSync(info.ownedDefault).mtimeMs;await saved();assert.equal(bytes(info.ownedDefault),initialBytes);assert.equal(fs.statSync(info.ownedDefault).mtimeMs,initialMtime);await page.locator('[data-configuration-tab="components"]').click();await idle();
 await page.locator('[data-configuration-component="Java"] [data-configuration-component-toggle]').click();await idle();const componentBefore=bytes(info.selection);
 await page.locator('[data-configuration-close]').click();fs.writeFileSync(info.ownedDefault,'mods\n{\n mod = B,\n mod = ZombieBuddy,\n mod = Java,\n mod = A,\n}\nmaps\n{\n}\n');
 const imported=bytes(info.ownedDefault),importMtime=fs.statSync(info.ownedDefault).mtimeMs;await opened();assert.deepEqual(await page.evaluate(()=>orange.launchConfigurationUi.editor().ids),['B','A'],'known and selected dynamic Java IDs project out');await saved();assert.equal(bytes(info.ownedDefault),imported);assert.equal(fs.statSync(info.ownedDefault).mtimeMs,importMtime);
 await page.locator('[data-source-key="Added"]').click();await page.locator('[data-configuration-mod="Added"] [data-configuration-up]').click();await saved();assert.deepEqual((await ask('configurationRead',{id:'launcher'})).ordinaryIds,['B','Added','A']);assert.equal(bytes(info.selection),componentBefore,'ordinary save cannot alter component selections/approvals');
 await page.screenshot({path:path.join(out,'final-R3-effective-ordinary.png')});await page.locator('[data-configuration-close]').click();console.log('PASS R3 first open/save preserves imported bytes; known/dynamic component projection, ordinary add/order/save and no-op mtime');
}
if(chosen('Minor1')){
 for(const id of ['snapshot:'+info.snapshotId,'launcher']){await page.locator('#launch-configuration-select').selectOption(id);await page.waitForFunction(id=>orange.state.status?.configurationId===id,id);assert.doesNotMatch(await page.locator('#page').innerText(),/游戏目录已变化，请重新核对配置/);}
 const named=await ask('configurationCreate',{name:'版本反馈',versionId:'steam'});await page.evaluate(()=>orange.launchConfigurationUi.reload());await page.locator('#launch-configuration-select').selectOption(named.id);await page.waitForFunction(id=>orange.state.status?.configurationId===id,named.id);await opened();await page.locator('[data-configuration-version]').selectOption(info.version2);await page.locator('[data-configuration-set-version]').click();await idle();assert.match(await page.locator('[data-configuration-editor-status]').innerText(),/版本已保存/);await page.locator('[data-configuration-close]').click();assert.doesNotMatch(await page.locator('#page').innerText(),/游戏目录已变化，请重新核对配置/);
 await page.screenshot({path:path.join(out,'final-Minor1-intentional-switch.png')});await page.evaluate(()=>orange.acceptStatus({...orange.state.status,gamePath:orange.state.status.gamePath+'-externally-changed'}));assert.match(await page.locator('#page').innerText(),/游戏目录已变化，请重新核对配置/);await page.evaluate(()=>{const data={...orange.state.status,gamePath:orange.state.status.gamePath+'-legacy-external'};delete data.configurationId;orange.state.launchFeedback='';orange.acceptStatus(data);});assert.match(await page.locator('#page').innerText(),/游戏目录已变化，请重新核对配置/,'legacy status without an owner ID still invalidates');console.log('PASS Minor1 accepted snapshot/live switch clears misleading feedback; external path change retains invalidation');
}
for(const [file,before] of Object.entries(originals))assert.equal(bytes(file),before,file+' original bytes preserved');assert.deepEqual(fixture.errors,[]);passed=true;
fs.writeFileSync(path.join(out,'final-fixes-'+cases+'.json'),JSON.stringify({info,calls,events,originals},null,2));
}finally{fs.writeFileSync(path.join(out,'final-fixes-'+cases+'-trace.json'),JSON.stringify({info,calls,events},null,2));if(!passed&&page){await page.screenshot({path:path.join(out,'final-fixes-'+cases+'-failure.png')}).catch(()=>{});console.log(await page.evaluate(()=>({editor:orange.launchConfigurationUi.editor()?{reading:orange.launchConfigurationUi.editor().reading,tab:orange.launchConfigurationUi.editor().tab,message:orange.launchConfigurationUi.editor().message}:null,last:sent.slice(-5)})).catch(()=>({})));}sink=()=>{};await ask('fixtureDispose').catch(()=>{});if(page)await page.close();if(browser)await browser.close();child.stdin.end();}})().catch(e=>{console.error(e);child.stdin.end();process.exitCode=1;});
