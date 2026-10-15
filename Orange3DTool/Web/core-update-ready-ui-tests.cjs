const {chromium}=require('./playwright-runtime.cjs');
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const A='a'.repeat(32),B='b'.repeat(32);
const status=(overrides={})=>({protocol:1,revision:10,state:'Available',currentVersion:'1.2.3',latestVersion:'1.2.4',checkedUtc:'2026-10-15T10:00:00Z',notes:'Fixture release',taskId:'',receivedBytes:0,totalBytes:6,speedBytes:0,currentFile:'',message:'Fixture update',history:[],...overrides});
async function setup(browser,options={}){
 const page=await browser.newPage({viewport:{width:820,height:600}});
 await page.addInitScript(({initial,options})=>{
  window.sent=[];window.held={};window.options=options;window.base=initial;
  window.chrome={webview:{addEventListener:(event,fn)=>window.reply=fn,postMessage:m=>{
   sent.push(m);if(options.hold===m.action){held[m.action]=m;return;}
   let data={};
   if(m.action==='status')data={validGame:true,running:options.running??false,theme:'dark',components:[],task:{active:false,value:0,text:''},launch:{pending:false},buddyRecovery:{pending:false}};
   if(m.action==='coreUpdateStatus')data=initial;
   if(m.action==='coreUpdateDownload')data={...initial,revision:11,state:'Downloading',taskId:options.downloadingTask||''};
   if(m.action==='coreUpdateCancel')data={...initial,revision:12,state:'Cancelled'};
   if(m.action==='coreUpdateCheck')data={...initial,revision:12,state:'Checking'};
   if(m.action==='coreUpdatePreflight')data=options.preflight??{protocol:1,allowed:true,message:''};
   if(m.action==='coreUpdateApply'){
    if(options.applyError){queueMicrotask(()=>reply({data:{id:m.id,ok:false,error:options.applyError}}));return;}
    data={...initial,revision:30,state:'Restarting',taskId:'a'.repeat(32)};
   }
   queueMicrotask(()=>reply({data:{id:m.id,ok:true,data}}));
  }}};
 },{initial:status(options.initial),options});
 await page.goto('file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/')+'#help');
 await page.locator('#core-update-panel').waitFor();
 await page.waitForFunction(()=>orange.state.status&&orange.state.coreUpdate);
 return page;
}
async function emit(page,overrides={}){await page.evaluate(value=>reply({data:{event:'coreUpdateProgress',data:value}}),status({revision:20,state:'Ready',taskId:A,receivedBytes:6,...overrides}));}
async function count(page,action){return page.evaluate(action=>sent.filter(m=>m.action===action).length,action);}
async function download(page){await page.getByRole('button',{name:'下载启动器更新',exact:true}).click();}
async function noPrompt(page,reason){await page.waitForTimeout(70);assert.equal(await page.locator('#confirm[open]').count(),0,reason);assert.equal(await count(page,'coreUpdateApply'),0,reason+' cannot apply');}
async function release(page,action,data,ok=true){await page.evaluate(({action,data,ok})=>reply({data:{id:held[action].id,ok,data,error:ok?'':'Fixture boundary failure'}}),{action,data,ok});}
async function manual(page){await page.getByRole('button',{name:'重启并更新',exact:true}).click();await page.locator('#confirm[open]').waitFor();}
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 // Removing download-bound prompting must fail here, before any negative cases.
 const page=await setup(browser);await download(page);await emit(page);
 await page.waitForTimeout(100);
 assert.equal(await page.locator('#confirm[open]').count(),1,'matching Ready after explicit download offers one confirmation');
 await page.locator('#confirm-no').click();await emit(page,{revision:21});
 await noPrompt(page,'declined Ready is not offered again');
 assert.equal(await page.getByRole('button',{name:'重启并更新',exact:true}).isEnabled(),true);
 await manual(page);await page.locator('#confirm-yes').click();await page.waitForFunction(()=>sent.some(m=>m.action==='coreUpdateApply'));
 assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='coreUpdateApply').map(m=>m.payload)),[{protocol:1,confirmed:true,drafts:false}]);
 assert.equal(await count(page,'coreUpdateDownload'),1,'manual continuation never downloads again');await page.close();

 const running=await setup(browser,{running:true,downloadingTask:A});await download(running);await emit(running);await running.locator('#confirm[open]').waitFor();
 assert.match(await running.locator('#core-update-panel').innerText(),/下载完成，等待重启安装/);
 assert.match(await running.locator('#confirm-body').innerText(),/只重启启动器/);assert.match(await running.locator('#confirm-body').innerText(),/游戏可以继续/);
 const shots=path.resolve(__dirname,'../../artifacts/TestFixtures/core-update-ready-shots');fs.mkdirSync(shots,{recursive:true});
 for(const theme of ['dark','light']){
  await running.evaluate(theme=>document.documentElement.classList.toggle('light',theme==='light'),theme);
  assert.equal(await running.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);
  await running.locator('#confirm').evaluate(async dialog=>{await Promise.all(dialog.getAnimations({subtree:true}).map(animation=>animation.finished));});
  assert.equal(await running.locator('#confirm').evaluate(dialog=>dialog.getAnimations({subtree:true}).filter(animation=>animation.playState==='running').length),0,theme+' confirmation transitions settled before evidence capture');
  await running.screenshot({path:path.join(shots,theme+'-820x600.png'),fullPage:true});
 }
 await running.evaluate(()=>{document.querySelector('[data-core-apply]').click();document.querySelector('#confirm-yes').click();document.querySelector('#confirm-yes').click();});
 await running.waitForFunction(()=>sent.some(m=>m.action==='coreUpdateApply'));assert.equal(await count(running,'coreUpdateApply'),1,'automatic/manual overlap and double consent apply once');
 assert.deepEqual(await running.evaluate(()=>sent.filter(m=>m.action==='coreUpdatePreflight').map(m=>m.payload)),[{protocol:1}]);await running.close();

 for(const [name,options,prepare,event] of [
  ['startup Ready',{initial:{state:'Ready',taskId:A}},null,null],
  ['wrong version',{},null,{latestVersion:'9.9.9'}],
  ['wrong observed task',{downloadingTask:A},null,{taskId:B}],
  ['empty task',{},null,{taskId:''}],
  ['invalid task',{},null,{taskId:'unsafe-id'}],
  ['reused historical task',{initial:{taskId:A}},null,{}],
  ['malformed protocol',{},null,{protocol:2}],
  ['malformed revision',{},null,{revision:'20'}],
  ['stale Ready',{},null,{revision:10}],
  ['cancelled late Ready',{},async p=>p.getByRole('button',{name:'取消启动器更新',exact:true}).click(),{}],
  ['error invalidates intent',{},async p=>emit(p,{revision:12,state:'Error'}),{}],
  ['other modal',{},async p=>p.evaluate(()=>document.querySelector('#task-center').showModal()),{}],
  ['unsaved drafts',{},async p=>p.evaluate(()=>orange.state.memoryDirty=true),{}],
  ['active write task',{},async p=>p.evaluate(()=>reply({data:{event:'progress',data:{active:true,value:0,text:'Fixture writing'}}})),{}],
  ['pending launch recovery',{},async p=>p.evaluate(()=>orange.state.status.launch.pending=true),{}],
  ['pending Buddy recovery',{},async p=>p.evaluate(()=>orange.state.status.buddyRecovery.pending=true),{}],
  ['profile recovery',{},async p=>p.evaluate(()=>orange.state.playProfiles={pendingRecovery:{pending:true}}),{}],
  ['local configuration transaction',{},async p=>p.evaluate(()=>orange.state.playApplying=true),{}],
  ['native recovery denial',{preflight:{protocol:1,allowed:false,message:'Fixture pending recovery'}},null,{}],
  ['unknown native readiness',{preflight:{}},null,{}],
  ['wrong readiness protocol',{preflight:{protocol:2,allowed:true,message:''}},null,{}],
  ['wrong readiness types',{preflight:{protocol:1,allowed:'true',message:''}},null,{}],
  ['missing readiness message',{preflight:{protocol:1,allowed:true}},null,{}],
 ]){
  const p=await setup(browser,options);if(name!=='startup Ready')await download(p);if(prepare)await prepare(p);if(event)await emit(p,event);
  await noPrompt(p,name);await emit(p,name==='stale Ready'?{revision:10}:{revision:21});await noPrompt(p,name+' duplicate');
  if(['other modal','unsaved drafts','active write task','pending launch recovery','pending Buddy recovery','profile recovery','local configuration transaction','native recovery denial','unknown native readiness','wrong readiness protocol','wrong readiness types','missing readiness message'].includes(name)){
   assert.equal(await p.getByRole('button',{name:'重启并更新',exact:true}).count(),1,name+' retains Ready');
   assert.ok((await p.locator('#core-update-panel').innerText()).includes('Fixture pending recovery')||await p.locator('#notice').innerText(),name+' explains blocker');
  }
  await p.close();
 }
 for(const fail of [false,true]){
  const p=await setup(browser,{hold:'coreUpdateDownload'});await download(p);await emit(p);await noPrompt(p,'Ready waits for successful download acknowledgement');
  await release(p,'coreUpdateDownload',status({revision:11,state:'Downloading'}),!fail);
  if(fail)await noPrompt(p,'failed download acknowledgement');else{await p.locator('#confirm[open]').waitFor();await p.keyboard.press('Escape');await emit(p,{revision:21});await noPrompt(p,'Escape cancels once');}
  await p.close();
 }
 const readyReply=await setup(browser,{hold:'coreUpdateDownload'});await download(readyReply);await release(readyReply,'coreUpdateDownload',status({revision:20,state:'Ready',taskId:A}));await readyReply.locator('#confirm[open]').waitFor();await readyReply.close();
 const check=await setup(browser,{hold:'coreUpdateDownload'});await download(check);await check.getByRole('button',{name:'检查启动器更新',exact:true}).click();await release(check,'coreUpdateDownload',status({revision:20,state:'Ready',taskId:A}));await noPrompt(check,'new check invalidates old generation');await check.close();
 for(const during of ['task','version','draft','write','recovery','modal','protocol']){
  const p=await setup(browser);await download(p);await emit(p);await p.locator('#confirm[open]').waitFor();
  if(during==='task')await emit(p,{revision:21,taskId:B});
  if(during==='version')await emit(p,{revision:21,latestVersion:'1.2.5'});
  if(during==='protocol')await emit(p,{revision:21,protocol:2});
  if(during==='draft')await p.evaluate(()=>orange.state.settingsDirty=true);
  if(during==='write')await p.evaluate(()=>orange.state.busy=true);
  if(during==='recovery')await p.evaluate(()=>orange.state.status.launch.pending=true);
  if(during==='modal')await p.evaluate(()=>{document.querySelector('#task-center').showModal();document.querySelector('#confirm-yes').click();});else await p.locator('#confirm-yes').click();
  assert.equal(await count(p,'coreUpdateApply'),0,during+' changes invalidate captured consent');await p.close();
 }
 const cleared=await setup(browser);await download(cleared);await cleared.evaluate(()=>orange.state.memoryDirty=true);await emit(cleared);await noPrompt(cleared,'draft blocked automatic offer');await cleared.evaluate(()=>orange.state.memoryDirty=false);await manual(cleared);await cleared.locator('#confirm-yes').click();await cleared.waitForFunction(()=>sent.some(m=>m.action==='coreUpdateApply'));assert.equal(await count(cleared,'coreUpdateApply'),1,'manual path works after blocked automatic offer');await cleared.close();
 for(const acknowledgement of [null,{},status({protocol:2,revision:11,state:'Downloading'}),status({revision:11,state:'Ready',taskId:B})]){
  const p=await setup(browser,{hold:'coreUpdateDownload',downloadingTask:A});await download(p);await emit(p,{revision:19,state:'Downloading',taskId:A});await emit(p);await release(p,'coreUpdateDownload',acknowledgement);await noPrompt(p,'malformed or conflicting download acknowledgement');await p.close();
 }
 for(const mode of ['failed','blocked','identity','modal','cancel']){
  const p=await setup(browser,{hold:'coreUpdatePreflight'});await download(p);await emit(p);await p.waitForFunction(()=>held.coreUpdatePreflight);
  if(mode==='identity')await emit(p,{revision:21,taskId:B});
  if(mode==='modal')await p.evaluate(()=>document.querySelector('#task-center').showModal());
  if(mode==='cancel')await emit(p,{revision:21,state:'Cancelled'});
  await release(p,'coreUpdatePreflight',mode==='blocked'?{protocol:1,allowed:false,message:'Fixture blocked'}:{protocol:1,allowed:true,message:''},mode!=='failed');
  await noPrompt(p,'preflight '+mode);await emit(p,{revision:22});await noPrompt(p,'preflight '+mode+' never retries');await p.close();
 }
 const rejected=await setup(browser,{applyError:'Fixture queued mutation'});await download(rejected);await emit(rejected);await rejected.locator('#confirm[open]').waitFor();await rejected.locator('#confirm-yes').click();await rejected.getByText('Fixture queued mutation',{exact:true}).first().waitFor();await emit(rejected,{revision:21});await rejected.waitForTimeout(60);assert.equal(await count(rejected,'coreUpdateApply'),1);assert.equal(await rejected.locator('#confirm[open]').count(),0);assert.equal(await rejected.getByRole('button',{name:'重启并更新',exact:true}).isEnabled(),true);await rejected.close();
 console.log('CORE UPDATE READY UI: PASS one-time/manual consent, native boundary payloads, identity/generation/async guards, local/native blockers, stale consent, double clicks, game running, Escape, both 820x600 themes');
 }finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
