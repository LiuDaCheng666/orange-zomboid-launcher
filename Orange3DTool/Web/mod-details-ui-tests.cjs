/* Shipped DOM and real disclosure/click events. Native replies are fixtures;
 * this suite does not prove native metadata reads or a game launch. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/mod-details-1239');fs.mkdirSync(out,{recursive:true});
const tests=[],test=(name,run)=>tests.push({name,run});
const settle=page=>page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
async function populate(page){await page.evaluate(()=>{
 const canvas=document.createElement('canvas');canvas.width=480;canvas.height=270;
 const c=canvas.getContext('2d');c.fillStyle='#344d42';c.fillRect(0,0,480,270);c.fillStyle='#e8c78f';c.font='bold 36px sans-serif';c.fillText('MOD COVER',100,145);
 fx.catalog=[{id:'Mini',name:"'69 Mini Mk2 Italian Job",author:'KI5',version:'2.1',source:'workshop',workshopId:'123456789',item:'123456789',cover:canvas.toDataURL(),requires:['CarBase'],description:'This mod adds spawns for Italian Job Minis.\n\n'+('完整介绍：包含车型、刷新位置和使用说明。\n'.repeat(85))+'END-OF-DESCRIPTION'},{id:'CarBase',name:'车辆基础',description:'',requires:[]}];
 fx.defaultMods=['CarBase','Mini'];orange.state.modCatalog=null;orange.state.modProfiles=null;
 });await route(page,'mods');await page.waitForSelector('#profile-mod-available [data-mod-row="Mini"]');}
test('both panes expand a full-width in-flow cover and complete description below the chosen row',async page=>{
 await populate(page);
 for(const pane of ['#profile-mod-available','#profile-mod-catalog']){
  const row=page.locator(pane+' [data-mod-row="Mini"]'),details=row.locator('.mod-row-details');
  await details.locator('summary').click();
  const body=details.locator('[data-mod-detail-body]');assert.equal(await body.isVisible(),true,'Expanded details must expose the detail body');
  const cover=body.locator('.mod-detail-cover img');assert.equal(await cover.count(),1,'Expanded details must have its own large cover');
  const metrics=await body.evaluate(el=>{const b=el.getBoundingClientRect(),row=el.closest('[data-mod-row]').getBoundingClientRect(),cover=el.querySelector('.mod-detail-cover').getBoundingClientRect();return{width:b.width,rowWidth:row.width,coverWidth:cover.width,coverHeight:cover.height,position:getComputedStyle(el).position,nestedScroll:el.scrollHeight>el.clientHeight+2,overflow:getComputedStyle(el).overflowY};});
  assert(metrics.width>=metrics.rowWidth-24,'Detail panel must span its row');assert(metrics.coverWidth>=130&&metrics.coverHeight>=90,'Cover must be substantially larger than list thumbnail');
  assert.notEqual(metrics.position,'absolute');assert.equal(metrics.nestedScroll,false,'The list owns scrolling; the description must not create another tiny scroll box');
  assert((await body.innerText()).includes('END-OF-DESCRIPTION'),'Do not silently cut the introduction at 1500 characters');
  assert.equal(await body.locator('[data-mod-workshop="123456789"]').count(),1);
  await details.locator('summary').click();assert.equal(await body.isVisible(),false);
 }
 await page.locator('#profile-mod-available [data-mod-row="Mini"] summary').click();
 await settle(page);await page.screenshot({path:path.join(out,'expanded-dark-1366.png')});
 await page.evaluate(()=>{const d=document.querySelector('#profile-mod-available [data-mod-row="Mini"] [data-mod-detail-description]');d.textContent='This mod adds spawns for Italian Job Minis.\n\n在地图中添加电影《偷天换日》风格的 Mini 车型。需要启用车辆基础模组。\n\n请在工坊原页查看作者的车型说明、兼容信息和更新记录。';});
 await page.screenshot({path:path.join(out,'expanded-introduction-dark.png')});
 await page.evaluate(()=>document.documentElement.classList.add('light'));await settle(page);await page.screenshot({path:path.join(out,'expanded-introduction-light.png')});
});
test('expanded details survive membership redraw and remain readable with a missing cover at narrow width',async page=>{
 await populate(page);const row=()=>page.locator('#profile-mod-available [data-mod-row="Mini"]');
 await row().locator('summary').click();await row().locator('[data-mod-membership]').uncheck();
 assert.equal(await row().locator('details').getAttribute('open'),'','Changing membership must not close the introduction being read');
 await page.setViewportSize({width:1000,height:768});
 assert.equal(await row().locator('summary').isVisible(),true,'Narrow layouts must retain the detail action');
 const noCover=page.locator('#profile-mod-available [data-mod-row="CarBase"]');await noCover.locator('summary').click();
 assert((await noCover.locator('[data-mod-detail-body]').innerText()).includes('作者暂未提供介绍'));
 const overflow=await page.locator('#profile-mod-available').evaluate(el=>el.scrollWidth>el.clientWidth+2);assert.equal(overflow,false);
 await settle(page);await page.screenshot({path:path.join(out,'expanded-narrow-missing.png')});
});
test('full introduction is read on expansion only, with visible progress, retry and no global task lock',async page=>{
 await populate(page);await page.evaluate(()=>{
  fx.catalog[0].description='本地介绍预览';fx.catalog[0].coverToken='detail-mini';
  const base=chrome.webview.postMessage;chrome.webview.postMessage=m=>{const action=m.payload?.action||m.action;if(action!=='modDetail'){base(m);return;}sent.push(m);window.detailMessage=m;};
  orange.state.modCatalog=null;orange.render();
 });await route(page,'home');await route(page,'mods');
 assert.equal(await page.evaluate(()=>sent.filter(m=>(m.payload?.action||m.action)==='modDetail').length),0);
 const row=()=>page.locator('#profile-mod-available [data-mod-row="Mini"]');await row().locator('summary').click();
 await page.waitForFunction(()=>!!window.detailMessage);
 assert((await row().innerText()).includes('正在读取完整介绍'));
 assert.equal(await page.locator('[data-page="servers"]').isEnabled(),true);
 await page.evaluate(()=>emitNative({id:detailMessage.id,ok:false,error:'本次读取失败'}));
 await row().locator('[data-mod-detail-retry]').click();
 await page.evaluate(()=>{const m=detailMessage,data={token:'detail-mini',id:'Mini',description:'作者完整介绍<LINE>完整末尾'};emitNative({id:m.id,ok:true,data:m.action==='configurationRequest'?{configurationId:m.payload.id,data}:data});});
 await page.waitForFunction(()=>document.querySelector('[data-mod-detail-description]')?.textContent.includes('完整末尾'));
 assert((await row().locator('[data-mod-detail-description]').innerText()).includes('作者完整介绍\n完整末尾'));
 assert.equal(await page.evaluate(()=>orange.state.busy),false);
});
test('detail reads preserve long text and refresh cached bodies after a catalog re-read',async page=>{
 await populate(page);await page.evaluate(()=>{
  const m=fx.catalog[0];m.coverToken='long-mini';fx.longDetail='x'.repeat(70000)+'LONG-TAIL';
  const base=chrome.webview.postMessage;chrome.webview.postMessage=m=>{if((m.payload?.action||m.action)!=='modDetail'){base(m);return;}sent.push(m);queueMicrotask(()=>{const data={token:'long-mini',id:'Mini',description:fx.longDetail};emitNative({id:m.id,ok:true,data:m.action==='configurationRequest'?{configurationId:m.payload.id,data}:data});});};
  orange.state.modCatalog=null;
 });await route(page,'home');await route(page,'mods');
 const row=()=>page.locator('#profile-mod-available [data-mod-row="Mini"]');await row().locator('summary').click();
 await page.waitForFunction(()=>!document.querySelector('[data-mod-detail-status]')?.textContent.includes('正在读取'));
 assert((await row().locator('[data-mod-detail-description]').innerText()).endsWith('LONG-TAIL'),'Success must not silently truncate long native text');
 await page.evaluate(()=>{fx.longDetail='当前新的完整介绍 V2';orange.state.modCatalog=null;});await route(page,'home');await route(page,'mods');
 const details=row().locator('details');if(await details.getAttribute('open')===null)await details.locator('summary').click();
 await page.waitForFunction(()=>document.querySelector('[data-mod-detail-description]')?.textContent==='当前新的完整介绍 V2');
 assert.equal(await page.evaluate(()=>sent.filter(m=>(m.payload?.action||m.action)==='modDetail').length),2);
});
test('same-ID source disclosures remain independent after redraw',async page=>{
 await populate(page);await page.evaluate(()=>{fx.catalog=[{id:'Duplicate',name:'来源 A',coverToken:'source-a',description:'A'},{id:'Duplicate',name:'来源 B',coverToken:'source-b',description:'B'}];fx.defaultMods=[];orange.state.modCatalog=null;});await route(page,'home');await route(page,'mods');
 await page.locator('#profile-mod-available [data-mod-row="Duplicate"] .mod-row-details').first().locator('summary').click();await page.evaluate(()=>orange.render());
 assert.deepEqual(await page.locator('#profile-mod-available [data-mod-row="Duplicate"] .mod-row-details').evaluateAll(rows=>rows.map(r=>r.open)),[true,false]);
});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];try{for(const item of tests){const {page,errors}=await setup(browser);try{await item.run(page);assert.deepEqual(errors,[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}catch(error){results.push({name:item.name,status:'FAIL',error:error.stack});console.error('FAIL '+item.name+'\n'+error.stack);}finally{await page.close();}}}finally{await browser.close();}fs.writeFileSync(path.join(out,'results.json'),JSON.stringify(results,null,2));if(results.some(r=>r.status!=='PASS'))process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1;});
