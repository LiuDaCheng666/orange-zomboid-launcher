const {chromium}=require('./playwright-runtime.cjs');
const assert=require('node:assert/strict'),path=require('node:path');
const initial={protocol:1,revision:0,state:'Idle',currentVersion:'1.2.16',latestVersion:'',taskId:'',checkedUtc:'',notes:'',history:[]};
async function fixture(browser,{help=false,holdStatus=false,holdSnapshot=false,alreadyChecked=false}={}){
 const page=await browser.newPage({viewport:{width:820,height:600}});
 await page.addInitScript(({initial,holdStatus,holdSnapshot,alreadyChecked})=>{
  window.sent=[];window.held={};window.current={...initial};
  window.refreshIntervals=[];const nativeInterval=window.setInterval;window.setInterval=(callback,delay,...args)=>{if(delay===15000){refreshIntervals.push(callback);return 0;}return nativeInterval(callback,delay,...args);};
  if(alreadyChecked)current={...current,state:'Available',latestVersion:'1.2.19',checkedUtc:'2026-10-25T09:00:00Z'};
  window.answer=(request,data)=>queueMicrotask(()=>reply({data:{id:request.id,ok:true,data}}));
  window.chrome={webview:{addEventListener:(event,callback)=>window.reply=callback,postMessage:request=>{
   sent.push(request);
   if(request.action==='status'){
    const status={validGame:true,running:false,theme:'dark',components:[],task:{active:false},launch:{pending:false}};
    if(holdStatus){held.status=()=>answer(request,status);return;}answer(request,status);return;
   }
   if(request.action==='coreUpdateStatus'){
    if(holdSnapshot&&!held.snapshot){held.snapshot=()=>answer(request,{...initial});return;}answer(request,{...current});return;
   }
   if(request.action==='coreUpdateCheck'){
    current={...current,revision:current.revision+1,state:'Checking'};answer(request,{...current});
    setTimeout(()=>{current={...current,revision:current.revision+1,state:'Available',latestVersion:'1.2.19',checkedUtc:'2026-10-25T09:00:00Z',message:'可更新至 1.2.19'};reply({data:{event:'coreUpdateProgress',data:{...current}}});},10);return;
   }
   answer(request,{});
  }}};
 },{initial,holdStatus,holdSnapshot,alreadyChecked});
 await page.goto('file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/')+(help?'#help':''));
 return page;
}
async function count(page,action){return page.evaluate(action=>sent.filter(request=>request.action===action).length,action);}
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{
 const tests=[
  ['actual periodic refresh callbacks never repeat startup discovery',async()=>{const p=await fixture(browser);try{await p.waitForFunction(()=>orange.state.coreUpdate?.state==='Available');await p.evaluate(()=>{for(let i=0;i<3;i++)for(const refresh of refreshIntervals)refresh();});await p.waitForTimeout(50);assert.equal(await count(p,'status'),4,'three real refresh calls issue three new native state requests');assert.equal(await count(p,'coreUpdateCheck'),1);assert.equal(await count(p,'coreUpdateDownload'),0);}finally{await p.close();}}],
  ['existing checked result is displayed without interrupting download availability',async()=>{const p=await fixture(browser,{help:true,alreadyChecked:true});try{await p.waitForFunction(()=>orange.state.status&&orange.state.coreUpdate);await p.waitForTimeout(50);assert.equal(await count(p,'coreUpdateCheck'),0);assert.equal(await p.getByRole('button',{name:'下载启动器更新',exact:true}).isEnabled(),true);assert.equal(await p.locator('#startup-update-notice').isVisible(),true);}finally{await p.close();}}],
  ['initial help page still discovers updates',async()=>{const p=await fixture(browser,{help:true});try{await p.waitForFunction(()=>orange.state.status&&orange.state.coreUpdate);await p.waitForTimeout(80);assert.equal(await count(p,'coreUpdateCheck'),1,'passive help status read must not cancel startup discovery');assert.match(await p.locator('#core-update-panel').innerText(),/可更新至 1.2.19/);assert.equal(await count(p,'coreUpdateDownload'),0);assert.equal(await count(p,'coreUpdateApply'),0);}finally{await p.close();}}],
  ['help opened before first status reply still checks once',async()=>{const p=await fixture(browser,{holdStatus:true});try{await p.evaluate(()=>{location.hash='help';});await p.locator('#core-update-panel').waitFor();await p.waitForTimeout(30);await p.evaluate(()=>held.status());await p.waitForTimeout(80);assert.equal(await count(p,'coreUpdateCheck'),1,'opening updates while native status is pending must not disable startup');await p.evaluate(()=>{held.status();held.status();});await p.waitForTimeout(40);assert.equal(await count(p,'coreUpdateCheck'),1);}finally{await p.close();}}],
  ['normal home checks once and never downloads',async()=>{const p=await fixture(browser);try{await p.waitForFunction(()=>orange.state.coreUpdate?.state==='Available');assert.equal(await count(p,'coreUpdateCheck'),1);assert.equal(await count(p,'coreUpdateDownload'),0);assert.equal(await count(p,'coreUpdateApply'),0);assert.match(await p.locator('#connection').textContent(),/本机服务已连接/);}finally{await p.close();}}],
  ['manual check before initial status owns discovery',async()=>{const p=await fixture(browser,{help:true,holdStatus:true});try{await p.getByRole('button',{name:'检查启动器更新',exact:true}).click();await p.waitForFunction(()=>orange.state.coreUpdate?.state==='Available');await p.evaluate(()=>held.status());await p.waitForTimeout(40);assert.equal(await count(p,'coreUpdateCheck'),1,'startup must not duplicate an explicit check');}finally{await p.close();}}],
  ['late passive snapshot cannot replace manual result',async()=>{const p=await fixture(browser,{help:true,holdStatus:true,holdSnapshot:true});try{await p.getByRole('button',{name:'检查启动器更新',exact:true}).click();await p.waitForFunction(()=>orange.state.coreUpdate?.state==='Available');await p.evaluate(()=>held.snapshot());await p.waitForTimeout(30);assert.equal(await p.evaluate(()=>orange.state.coreUpdate.state),'Available');assert.equal(await p.evaluate(()=>orange.state.coreUpdate.latestVersion),'1.2.19');}finally{await p.close();}}],
  ['preview remains offline',async()=>{const p=await browser.newPage();try{await p.goto('file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/')+'?preview=1#help');await p.waitForTimeout(70);assert.equal(await p.evaluate(()=>!!window.chrome?.webview),false);assert.match(await p.locator('#connection').textContent(),/界面测试预览/);}finally{await p.close();}}]
 ];
 for(const[name,run]of tests){try{await run();console.log('PASS '+name);}catch(error){failed++;console.error('FAIL '+name+'\n'+error.message);}}
 console.log(`${tests.length-failed}/${tests.length} startup update groups passed`);
}finally{await browser.close();}if(failed)process.exitCode=1;})().catch(error=>{console.error(error);process.exitCode=1;});
