'use strict';
// Real app.js clicks -> production policy/controller -> real isolated file writes.
// Initial navigation/status metadata and package bytes are fixtures; no real game or Steam starts.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),readline=require('node:readline'),{spawn}=require('node:child_process');
const {setup,route,playwright}=require('../Orange3DTool/Web/full-ui-tests.cjs');
const repo=path.resolve(__dirname,'..'),out=path.join(repo,'artifacts/unused-bat-regression');
const child=spawn(path.join(fs.readFileSync(path.join(out,'runtime-path.txt'),'utf8').trim(),'NativeUi.exe'),[],{windowsHide:true,stdio:['pipe','pipe','pipe']});
const calls=[],pending=new Map();let readyResolve,stderr='',latestFiles;
const ready=new Promise(r=>readyResolve=r);child.stderr.on('data',d=>stderr+=d);
const exited=new Promise(resolve=>child.on('exit',code=>resolve(code)));
readline.createInterface({input:child.stdout}).on('line',line=>{const m=JSON.parse(line.replace(/^\uFEFF/,''));if(m.ready){readyResolve(m);return;}const job=pending.get(m.id);if(job){pending.delete(m.id);if(m.files)latestFiles=m.files;calls.push({request:job.message,response:m});job.resolve(m);}});
const nativeRequest=message=>new Promise(resolve=>{pending.set(message.id,{resolve,message});child.stdin.write(JSON.stringify(message)+'\n');});
(async()=>{const info=await ready;const browser=await playwright.chromium.launch({channel:'msedge',headless:true});let page;
 try{
  const f=await setup(browser,'light');page=f.page;await page.exposeFunction('unusedBatNative',nativeRequest);
  await page.evaluate(info=>{
   const base=chrome.webview.postMessage;window.nativeBuddyConfigured=false;
   chrome.webview.postMessage=m=>{
    if(['buddyPreview','buddyTakeover','componentsAction'].includes(m.action)){sent.push(m);unusedBatNative(m).then(result=>{if(result.files)nativeBuddyConfigured=result.files.frameworkConfigured;reply({data:result});});return;}
    if(m.action==='status'){sent.push(m);queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:{...orange.state.status,gamePath:info.game,validGame:true,configured:false,buddyRecovery:{pending:false},components:[{id:'buddy',title:'ZombieBuddy',item:'3619862853',downloaded:true,configured:nativeBuddyConfigured}],task:{active:false}}}}));return;}base(m);
   };
   orange.acceptStatus({...orange.state.status,gamePath:info.game,components:[{id:'buddy',title:'ZombieBuddy',item:'3619862853',downloaded:true,configured:false}],buddyRecovery:{pending:false}});
  },info);
  await route(page,'maintenance');await page.locator('[data-buddy-scan]').click();await page.locator('[data-buddy-takeover]').waitFor();
  await page.locator('[data-buddy-untouched]>summary').click();await page.locator('[data-buddy-panel]').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'native-scan.png')});
  await page.locator('[data-buddy-takeover]').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>orange.state.buddyResult?.cleanupCompleted);
  assert.equal(latestFiles.batUnchanged,true);assert.equal(latestFiles.pendingRecovery,false);assert.doesNotMatch(latestFiles.config,/-javaagent:/);
  await page.locator('[data-buddy-result] [data-buddy-untouched]>summary').click();await page.locator('[data-buddy-panel]').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(out,'native-takeover-complete.png')});
  await page.locator('nav [data-page="components"]').click();await page.locator('[data-component="buddy"][data-operation="install"]').click();
  await page.waitForFunction(()=>!orange.state.busy&&sent.some(m=>m.action==='componentsAction'));
  const installed=calls.find(c=>c.request.action==='componentsAction');assert.equal(installed.response.ok,true,installed.response.error);
  assert.equal(installed.response.data.status,'completed');assert.equal(latestFiles.frameworkConfigured,true);assert.equal(latestFiles.jarExists,true);assert.equal(latestFiles.nativeExists,true);assert.equal(latestFiles.batUnchanged,true);assert.equal(latestFiles.pendingRecovery,false);assert.match(latestFiles.mods,/OtherMod/);assert.match(latestFiles.mods,/ZombieBuddy/);assert.match(latestFiles.config,/-agentlib:zbNative/);
  assert.deepEqual(f.errors,[]);assert.match(await page.locator('#notice').innerText(),/ZombieBuddy 已启用，下次启动生效/);assert.doesNotMatch(await page.locator('#notice').innerText(),/备份|TestFixtures/);await page.screenshot({path:path.join(out,'native-component-enabled.png')});
  fs.writeFileSync(path.join(out,'native-ui-result.json'),JSON.stringify({passed:true,scope:'Real DOM clicks, production ModernHostPolicy and ModernController, TestMode=false, actual isolated native writes; fixture game/package bytes and process inventory; no actual game launch, Steam operation or foreground interaction',info,calls,files:latestFiles,errors:f.errors},null,2));
  console.log('PASS native UI: scan -> confirm takeover -> install Buddy -> actual files/config/default list verified; spare BAT unchanged; no pending recovery');
 }finally{if(page)await page.close();await browser.close();child.stdin.end();assert.equal(await exited,0,stderr);}
})().catch(e=>{console.error(e);child.stdin.end();process.exitCode=1;});
