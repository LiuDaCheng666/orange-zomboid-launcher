const assert=require('node:assert/strict');
const {setup,route,chromium}=require('./refresh-fixture.cjs');
const {boundary}=require('./server-workshop-ui-tests.cjs');
const tests=[{name:'pending import preserves newer text and protects its draft',fn:async browser=>{
 const {page}=await setup(browser);await boundary(page);await route(page,'servers');await page.locator('[data-server-workshop="demo"]').click();await page.waitForSelector('[data-server-workshop-item="123"]');
 await page.evaluate(()=>{const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{if(m.action==='serverWorkshopImport'){window.pendingImport=m;return;}original(m);};});
 await page.locator('#server-workshop-panel summary').click();await page.locator('#server-workshop-import').fill('123');await page.locator('[data-server-workshop-import]').click();await page.locator('#server-workshop-import').fill('789');
 await page.evaluate(()=>reply({data:{id:pendingImport.id,ok:true,data:{...manifest,stamp:'new',source:'imported'}}}));await page.waitForFunction(()=>orange.state.serverWorkshopManifest.stamp==='new');
 assert.equal(await page.locator('#server-workshop-import').inputValue(),'789');assert.equal(await page.evaluate(()=>orange.hasUnsavedDrafts()),true);await page.close();
}},{name:'manual catalog refresh replaces stable cover identity and ignores outstanding old response',fn:async browser=>{
 const {page}=await setup(browser,{clock:true});await page.evaluate(()=>{
  window.coverRequests=[];const original=chrome.webview.postMessage;
  chrome.webview.postMessage=m=>{if(m.action==='modCover'){coverRequests.push(m);return;}if(m.action==='modCatalog'){queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:{mods:[{id:'poster',title:'Poster',coverToken:'stable',cover:'',requires:[]}],warnings:[]}}}));return;}original(m);};
  window.coverImages={};
  for(const color of ['red','green','blue']){const canvas=document.createElement('canvas');canvas.width=canvas.height=2;const context=canvas.getContext('2d');context.fillStyle=color;context.fillRect(0,0,2,2);coverImages[color]=canvas.toDataURL();}
  window.answerCover=(index,color)=>reply({data:{id:coverRequests[index].id,ok:true,data:{token:'stable',cover:coverImages[color]||''}}});
 });
 await route(page,'mods');await page.waitForFunction(()=>coverRequests.length===1);
 await page.evaluate(()=>{orange.state.modCatalog=null;});await route(page,'settings');await route(page,'mods');
 await page.evaluate(()=>answerCover(0,'red'));await page.waitForTimeout(100);
 assert.equal(await page.evaluate(()=>[...document.querySelectorAll('[data-cover-token="stable"] img')].some(i=>i.src===coverImages.red)),false,'obsolete pixels must never be installed');
 await page.waitForFunction(()=>coverRequests.length===2);await page.evaluate(()=>answerCover(1,'green'));await page.waitForFunction(()=>[...document.querySelectorAll('[data-cover-token="stable"] img')].some(i=>i.src===coverImages.green&&i.naturalWidth===2));
 await page.evaluate(()=>{orange.state.modCatalog=null;});await route(page,'settings');await route(page,'mods');await page.waitForFunction(()=>coverRequests.length===3);await page.evaluate(()=>answerCover(2,'blue'));await page.waitForFunction(()=>[...document.querySelectorAll('[data-cover-token="stable"] img')].some(i=>i.src===coverImages.blue&&i.naturalWidth===2));
 await page.clock.fastForward(31000);await page.waitForFunction(()=>coverRequests.length===4);await page.evaluate(()=>answerCover(3,'missing'));await page.waitForSelector('[data-cover-token="stable"] .cover-missing');
 await page.clock.fastForward(31000);await page.waitForFunction(()=>coverRequests.length===5);await page.evaluate(()=>answerCover(4,'green'));await page.waitForFunction(()=>[...document.querySelectorAll('[data-cover-token="stable"] img')].some(i=>i.src===coverImages.green&&i.naturalWidth===2));await page.close();
}}];
module.exports={tests};
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests)try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}finally{await browser.close();}if(failed)process.exitCode=1;})();
