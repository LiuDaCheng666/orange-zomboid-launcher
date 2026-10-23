const assert=require('node:assert/strict');
const {setup,route,chromium}=require('./refresh-fixture.cjs');
async function boundary(page){await page.evaluate(()=>{
 window.manifest={id:'demo',endpoint:'play.example.invalid:16261',stamp:'manifest-1',source:'connection',capturedUtc:'2026-10-23T08:00:00Z',items:[{id:'123',title:'生存工具箱',localPresent:true},{id:'456',title:'地图扩展',localPresent:false}],summary:'上次连接记录；服务器可能已更新。'};window.job=null;
 const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{if(!m.action.startsWith('serverWorkshop'))return original(m);sent.push(m);let data;
 if(m.action==='serverWorkshopGet')data=manifest;
 if(m.action==='serverWorkshopImport'){manifest={...manifest,source:'imported',stamp:'manifest-2',summary:'上次记录的工坊清单；服务器可能已更新，不代表模组加载顺序。'};data=manifest;}
 if(m.action==='serverWorkshopPrepare'){job={protocol:1,jobId:'job-1',revision:1,serverId:'demo',endpoint:manifest.endpoint,manifestStamp:manifest.stamp,state:'running',totalItems:2,completedItems:0,currentItem:'123',progress:-1,message:'等待 Steam 实际进度',items:manifest.items.map(i=>({id:i.id,state:'pending',message:''}))};data=job;}
 if(m.action==='serverWorkshopStatus'){if(window.holdWorkshopStatus){window.workshopStatusRequest=m;return;}data=job;}
 if(m.action==='serverWorkshopCancel'){job={...job,revision:5,state:'cancelled',message:'已取消'};data=job;}
 queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:structuredClone(data)}}));};
 });}
const tests=[{name:'server manifest imports explicitly and preparation stays navigable with truthful stale-safe progress',fn:async browser=>{
 const {page,errors}=await setup(browser);await boundary(page);await route(page,'servers');await page.locator('[data-server-workshop="demo"]').click();await page.waitForSelector('[data-server-workshop-item="123"]');assert.match(await page.locator('#server-workshop-panel').innerText(),/连接记录/);assert.match(await page.locator('#server-workshop-panel').innerText(),/2 个工坊项目 · 本地已有 1 个/);assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='serverWorkshopPrepare')),false);
 await page.locator('#server-workshop-panel summary').click();await page.locator('#server-workshop-import').fill('WorkshopItems=123;456');await page.locator('[data-server-workshop-import]').click();assert.equal((await page.evaluate(()=>sent.find(m=>m.action==='serverWorkshopImport').payload)).confirmed,true);await page.waitForFunction(()=>orange.state.serverWorkshopManifest.source==='imported');
 await page.locator('[data-server-workshop-prepare]').click();assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='serverWorkshopPrepare')),false);await page.locator('#confirm-yes').click();await page.waitForSelector('[data-server-workshop-progress]');assert.equal(await page.locator('[data-server-workshop-progress] progress').getAttribute('value'),null);
 await page.evaluate(()=>reply({data:{event:'serverWorkshopProgress',data:{...job,revision:3,completedItems:1,currentItem:'456',progress:37,message:'37 / 100 bytes'}}}));assert.match(await page.locator('[data-server-workshop-progress]').innerText(),/1.*2.*456.*37 \/ 100 bytes/s);
 await page.evaluate(()=>reply({data:{event:'serverWorkshopProgress',data:{...job,revision:2,message:'stale'}}}));assert.doesNotMatch(await page.locator('[data-server-workshop-progress]').innerText(),/stale/);
 await page.locator('[data-server-workshop-close]').click();await route(page,'settings');await page.locator('[data-setting]').first().fill('P');await route(page,'servers');await page.locator('[data-server-workshop="demo"]').click();await page.locator('[data-server-workshop-cancel]').click();assert.match(await page.locator('[data-server-workshop-progress]').innerText(),/已取消/);await page.evaluate(()=>reply({data:{event:'serverWorkshopProgress',data:{...job,jobId:'retired-job',revision:900,message:'retired event'}}}));assert.doesNotMatch(await page.locator('[data-server-workshop-progress]').innerText(),/retired event/);assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='profileLaunch'||m.action==='launch')),false);assert.deepEqual(errors,[]);await page.close();
}},{name:'selected home server exposes the same manifest entry',fn:async browser=>{const {page}=await setup(browser);await boundary(page);await page.locator('#launch-target-kind').selectOption('server');await page.locator('#launch-target-id').selectOption('demo');await page.locator('[data-server-workshop="demo"]').first().click();await page.waitForSelector('[data-server-workshop-item="123"]');await page.close();}},{name:'delayed status cannot replace newly confirmed preparation',fn:async browser=>{const {page}=await setup(browser);await boundary(page);await page.evaluate(()=>window.holdWorkshopStatus=true);await route(page,'servers');await page.locator('[data-server-workshop="demo"]').click();await page.waitForSelector('[data-server-workshop-item="123"]');await page.locator('[data-server-workshop-prepare]').click();await page.locator('#confirm-yes').click();await page.waitForSelector('[data-server-workshop-progress]');await page.evaluate(()=>reply({data:{id:workshopStatusRequest.id,ok:true,data:{...job,jobId:'previous-job',revision:900,state:'completed',message:'old status'}}}));assert.doesNotMatch(await page.locator('[data-server-workshop-progress]').innerText(),/old status/);await page.close();}}];
module.exports={tests,boundary};
tests.push({name:'second job completion before its start response wins over the older running snapshot',fn:async browser=>{
 const {page,errors}=await setup(browser);await boundary(page);await route(page,'servers');await page.locator('[data-server-workshop="demo"]').click();await page.waitForSelector('[data-server-workshop-item="123"]');
 await page.locator('[data-server-workshop-prepare]').click();await page.locator('#confirm-yes').click();await page.waitForSelector('[data-server-workshop-progress]');
 await page.evaluate(()=>{
  job={...job,revision:5,state:'completed',completedItems:2,progress:100,message:'first completed'};reply({data:{event:'serverWorkshopProgress',data:job}});
  const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{
   if(m.action!=='serverWorkshopPrepare')return original(m);sent.push(m);
   const running={...job,jobId:'job-2',revision:1,state:'running',completedItems:0,progress:-1,message:'second running'};
   job={...running,revision:5,state:'completed',completedItems:2,progress:100,message:'second completed'};
   reply({data:{event:'serverWorkshopProgress',data:job}});
   queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:running}}));
  };
 });
 await page.locator('[data-server-workshop-prepare]').click();await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>orange.state.serverWorkshopJob?.jobId==='job-2');
 assert.equal(await page.evaluate(()=>orange.state.serverWorkshopJob.state),'completed');
 assert.match(await page.locator('[data-server-workshop-progress]').innerText(),/已完成.*2 \/ 2.*second completed/s);
 assert.deepEqual(errors,[]);await page.close();
}});
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests)try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} server workshop UI groups passed`);if(failed)process.exitCode=1;})();
