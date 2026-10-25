const assert=require('node:assert/strict');
const {setup,route,chromium}=require('./refresh-fixture.cjs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
test('Fluent navigation leaves room for the cover and launch strip',async browser=>{
 for(const theme of ['dark','light']){
  const {page,errors}=await setup(browser,{theme});
  const boxes=await page.evaluate(()=>{const box=s=>document.querySelector(s).getBoundingClientRect().toJSON();return {nav:box('nav'),main:box('main'),cover:box('[data-home-cover]'),launch:box('.play-launch')};});
  assert.ok(boxes.nav.right<=boxes.main.left+1,'navigation must not overlap the work area');
  assert.ok(boxes.cover.width>=boxes.main.width*.9,'artwork occupies the main work area');
  assert.ok(boxes.launch.top>=boxes.cover.bottom-1,'launch strip remains below artwork');
  assert.deepEqual(errors,[]);await page.close();
 }
});
test('Fluent compact navigation and keyboard utilities remain reachable after resize',async browser=>{
 const {page}=await setup(browser);
 for(const [width,height] of [[1440,900],[1280,900],[768,900],[1100,650],[733,507],[500,760]]){
  await page.setViewportSize({width,height});
  const bounds=await page.locator('nav [data-page]').evaluateAll(es=>es.map(e=>{const r=e.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom};}));
  assert.equal(bounds.length,8);for(const r of bounds)assert.ok(r.left>=0&&r.right<=width&&r.top>=0&&r.bottom<=height,'all eight routes in bounds');
  for(const name of ['memory','settings','help']){const b=page.locator('nav [data-page="'+name+'"]');await b.focus();await page.keyboard.press('Enter');await page.waitForFunction(n=>document.querySelector('#page').dataset.route===n,name);}
  const summary=page.locator('.header-menu>summary');if(await summary.isVisible()&&!await page.locator('.header-menu').evaluate(e=>e.open)){await summary.focus();await page.keyboard.press('Enter');}
  const wasLight=await page.evaluate(()=>document.documentElement.classList.contains('light'));
  for(let step=0;step<80&&await page.evaluate(()=>document.activeElement.id!=='theme');step++)await page.keyboard.press('Tab');
  assert.equal(await page.evaluate(()=>document.activeElement.id),'theme','theme reachable in actual Tab order');await page.keyboard.press('Enter');assert.equal(await page.evaluate(()=>document.documentElement.classList.contains('light')),!wasLight);
  await page.locator('#task-center-open').focus();await page.keyboard.press('Enter');assert.equal(await page.locator('#task-center').isVisible(),true);await page.locator('[data-task-close]').click();
  await route(page,'home');
  assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true);
 }
 await page.close();
});
test('Fluent status refresh preserves the focused dirty memory editor',async browser=>{
 const {page}=await setup(browser);await route(page,'memory');await page.locator('#memory-number').fill('12');
 await page.evaluate(()=>orange.acceptStatus({...demoStatus,running:true}));
 assert.equal(await page.locator('#memory-number').inputValue(),'12');assert.equal(await page.evaluate(()=>document.activeElement.id),'memory-number');assert.equal(await page.evaluate(()=>orange.state.memoryDirty),true);await page.close();
});
module.exports={tests};
test('Memory allocation leads compact system metrics',async browser=>{
 const {page}=await setup(browser);await route(page,'memory');const editor=await page.locator('#memory-range').boundingBox(),metrics=await page.locator('.metrics').boundingBox();assert.ok(editor.y<metrics.y,'allocation controls precede system metrics');assert.ok(metrics.height<=110,'metrics fit one compact desktop row');await page.close();
});
test('Management catalog exposes six readable rows without page scrolling',async browser=>{
 const {page,errors}=await setup(browser,{catalogRows:8});await page.setViewportSize({width:1280,height:900});await route(page,'mods');
 const geometry=await page.locator('[data-mod-row]').evaluateAll(rows=>rows.map(e=>{const r=e.getBoundingClientRect();return {top:r.top,bottom:r.bottom,font:parseFloat(getComputedStyle(e.querySelector('.mod-row-title span')).fontSize)};}));
 assert.ok(geometry.filter(r=>r.top>=0&&r.bottom<=900).length>=6,JSON.stringify(geometry));assert.ok(geometry.every(r=>r.font>=12));assert.deepEqual(errors,[]);await page.close();
});
test('Core cards bound author art and collapsed management height',async browser=>{
 const {page}=await setup(browser);await route(page,'components');
 const cards=await page.locator('[data-core-components]>.component-card').evaluateAll(es=>es.map(e=>({height:e.getBoundingClientRect().height,cover:e.querySelector('.cover').getBoundingClientRect().height,fit:e.querySelector('img')?getComputedStyle(e.querySelector('img')).objectFit:'contain'})));
 assert.equal(cards.length,3);for(const c of cards){assert.ok(c.height<=250,JSON.stringify(c));assert.ok(c.cover>=72&&c.cover<=88,JSON.stringify(c));assert.equal(c.fit,'contain');}await page.close();
});
test('Core component primary operations align across independent cards',async browser=>{
 const {page}=await setup(browser);await route(page,'components');const positions=await page.locator('[data-core-components] .component-actions').evaluateAll(es=>es.map(e=>e.getBoundingClientRect().top));assert.ok(Math.max(...positions)-Math.min(...positions)<=1,JSON.stringify(positions));await page.close();
});
test('Short viewport confirmation keeps long path data and return reachable',async browser=>{
 const {page}=await setup(browser);await page.setViewportSize({width:733,height:507});
 await route(page,'maintenance');await page.locator('[data-maintenance="cache"]').click();
 await page.evaluate(()=>{document.querySelector('#confirm-body').textContent=Array.from({length:25},(_,i)=>'路径 '+i+' C:/Example/Java/file.jar · SHA256 '+ 'a'.repeat(64)).join('\n');});
 const d=await page.locator('#confirm').boundingBox();assert.ok(d.y>=8&&d.y+d.height<=499,JSON.stringify(d));await page.locator('#confirm-no').click();assert.equal(await page.locator('#confirm').isVisible(),false);await page.close();
});
test('Short server editor exposes return and save without scrolling past fields',async browser=>{
 const {page}=await setup(browser);await page.setViewportSize({width:733,height:507});await route(page,'servers');await page.locator('[data-server-new]').click();
 for(const selector of ['[data-server-close]','[data-server-write]']){const r=await page.locator(selector).boundingBox();assert.ok(r.y>=0&&r.y+r.height<=507,selector+' '+JSON.stringify(r));}
 await page.locator('[data-server-close]').click();assert.equal(await page.locator('#server-editor').count(),0);await page.close();
});
test('Long server workshop panel keeps import and running-job cancellation reachable',async browser=>{
 for(const theme of ['dark','light'])for(const [width,height] of [[1440,900],[733,507],[500,760]]){
  const {page,errors}=await setup(browser,{theme});
  try{
   await page.setViewportSize({width,height});await route(page,'servers');
   await page.evaluate(()=>{
    const original=chrome.webview.postMessage;
    let manifest={id:'demo',endpoint:'play.example.invalid:16261',stamp:'manifest-1',source:'connection',capturedUtc:'2026-10-23T08:00:00Z',items:Array.from({length:20},(_,i)=>({id:String(1234567890+i),title:'合成工坊项目 '+i,localPresent:false}))};
    let job={protocol:1,jobId:'synthetic-workshop-job',revision:1,serverId:'demo',endpoint:manifest.endpoint,manifestStamp:'manifest-2',state:'running',completedItems:0,totalItems:20,currentItem:'1234567890',progress:5,message:'正在准备合成清单',items:[]};
    chrome.webview.postMessage=m=>{
     if(!['serverWorkshopGet','serverWorkshopStatus','serverWorkshopImport','serverWorkshopPrepare','serverWorkshopCancel'].includes(m.action))return original(m);
     sent.push(m);let data={};
     if(m.action==='serverWorkshopGet')data=manifest;
     if(m.action==='serverWorkshopImport')data=manifest={...manifest,stamp:'manifest-2',source:'imported'};
     if(m.action==='serverWorkshopPrepare')data=job;
     if(m.action==='serverWorkshopCancel')data=job={...job,revision:2,state:'cancelled',message:'已取消合成任务'};
     queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:structuredClone(data)}}));
    };
   });
   await page.locator('[data-server-workshop="demo"]').click();await page.locator('[data-server-workshop-item]').last().waitFor();
   const panel=page.locator('#server-workshop-panel');
   const box=await panel.boundingBox();
   await panel.locator('summary').click();
   assert.ok(box.y>=0&&box.y+box.height<=height-16,`${theme} ${width}x${height}: ${JSON.stringify(box)}`);
   await page.locator('#server-workshop-import').fill('WorkshopItems=1234567890;1234567891');
   await page.locator('[data-server-workshop-import]').click();
   await page.waitForFunction(()=>orange.state.serverWorkshopManifest.stamp==='manifest-2');
   assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='serverWorkshopImport').payload),{id:'demo',stamp:'manifest-1',text:'WorkshopItems=1234567890;1234567891',confirmed:true});
   assert.equal(await page.locator('#server-workshop-import').inputValue(),'');
   await page.locator('[data-server-workshop-prepare]').click();await page.locator('#confirm-yes').click();await page.locator('[data-server-workshop-cancel]').waitFor();
   assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='serverWorkshopPrepare').payload),{id:'demo',stamp:'manifest-2',confirmed:true});
   if(process.env.OZL_CAPTURE_WORKSHOP_PANEL){
    const fs=require('node:fs'),path=require('node:path'),folder=path.resolve(__dirname,'../../artifacts/fluent-ui');fs.mkdirSync(folder,{recursive:true});
    await page.locator('[data-server-workshop-cancel]').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(folder,`${theme}-server-workshop-${width}x${height}.png`),animations:'disabled'});
   }
   await page.locator('[data-server-workshop-cancel]').click();
   await page.waitForFunction(()=>orange.state.serverWorkshopJob.state==='cancelled');
   assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='serverWorkshopCancel').payload),{jobId:'synthetic-workshop-job'});
   assert.equal(await page.locator('[data-server-workshop-cancel]').count(),0);assert.deepEqual(errors,[]);
  }finally{await page.close();}
 }
});
test('Reorganized component and profile actions retain bridge payloads',async browser=>{
 const {page}=await setup(browser);await route(page,'components');await page.locator('[data-component="buddy"][data-operation="uninstall"]').click();if(await page.locator('#confirm').isVisible())await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>sent.some(m=>m.action==='componentsAction'));assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='componentsAction').payload),{component:'buddy',operation:'uninstall',confirmed:true});
 await route(page,'mods');await page.locator('#mod-workspace-select').selectOption('relax');await page.waitForFunction(()=>sent.some(m=>m.action==='modProfileGet'));assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='modProfileGet').payload),{id:'relax'});await page.close();
});
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}if(failed)process.exitCode=1;})();
