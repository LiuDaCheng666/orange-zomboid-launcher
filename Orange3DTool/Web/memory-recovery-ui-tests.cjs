/* Synthetic native failures only; no game files are read or modified. */
const assert=require('node:assert/strict');
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const tests=[{name:'rejected memory save unlocks draft, retry and reread before a delayed status response',fn:async browser=>{
 const {page,errors}=await setup(browser);
 try {
  await route(page,'memory');await page.waitForSelector('#memory-number');
  await page.locator('#memory-number').fill('12');
  await page.evaluate(()=>{failure='memorySave';holdAction='status';});
  await page.locator('[data-save-memory]').click();
  await page.waitForFunction(()=>held.some(x=>x.request.action==='status')&&orange.state.notice.includes('Snapshot changed'));
  assert.equal(await page.locator('#memory-number').isEnabled(),true,'failed save must release editor lock without waiting for status');
  assert.equal(await page.locator('[data-save-memory]').isEnabled(),true);
  assert.equal(await page.locator('[data-reload="memory"]').isEnabled(),true);
  assert.equal(await page.locator('#memory-number').inputValue(),'12');
  assert.equal(await page.evaluate(()=>orange.state.memoryDirty),true);
  assert.doesNotMatch(await page.locator('[data-save-memory]').innerText(),/保存中/);
  await page.evaluate(()=>{failure='';holdAction='';});
  await page.locator('#memory-number').fill('16');await page.locator('[data-save-memory]').click();
  await page.waitForFunction(()=>sent.filter(m=>m.action==='memorySave').length===2&&!orange.state.memoryDirty);
  assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='memorySave').at(-1).payload),{value:16,stamp:'m'});
  await page.locator('#memory-number').fill('20');
  await page.evaluate(()=>{failure='memorySave';holdAction='status';});
  await page.locator('[data-save-memory]').click();
  await page.waitForFunction(()=>held.filter(x=>x.request.action==='status').length===2);
  await page.locator('[data-reload="memory"]').click();await page.locator('#confirm-no').click();
  assert.equal(await page.locator('#memory-number').inputValue(),'20','cancel reread preserves failed draft');
  await page.locator('[data-reload="memory"]').click();await page.locator('#confirm-yes').click();
  await page.waitForFunction(()=>!orange.state.loading.memory&&!orange.state.memoryDirty);
  assert.equal(await page.locator('#memory-number').inputValue(),'6');
  await page.evaluate(()=>{failure='';holdAction='';held.splice(0).forEach(x=>reply(x.response));});
  assert.deepEqual(errors,[]);
 } finally {await page.close();}
}},{name:'shared rejected profile save releases controls and retains draft while status is delayed',fn:async browser=>{
 const {page,errors}=await setup(browser);
 try {
  await route(page,'mods');await page.locator('#mod-workspace-select').selectOption('p1');
  await page.locator('#mod-profile-name').fill('Keep rejected draft');
  await page.evaluate(()=>{failure='modProfileSave';holdAction='status';});
  await page.locator('[data-mod-profile-save]').click();
  await page.waitForFunction(()=>held.some(x=>x.request.action==='status')&&orange.state.notice.includes('Snapshot changed'));
  assert.equal(await page.locator('#mod-profile-name').isEnabled(),true);
  assert.equal(await page.locator('[data-mod-profile-save]').isEnabled(),true);
  assert.equal(await page.locator('#mod-profile-name').inputValue(),'Keep rejected draft');
  assert.equal(await page.evaluate(()=>orange.state.modProfileDirty),true);
  assert.doesNotMatch(await page.locator('[data-mod-profile-save]').innerText(),/保存中/);
  await page.evaluate(()=>{failure='';holdAction='';});
  await page.locator('#mod-profile-name').fill('Retry edited draft');await page.locator('[data-mod-profile-save]').click();
  await page.waitForFunction(()=>sent.filter(m=>m.action==='modProfileSave').length===2);
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='modProfileSave').at(-1).payload.name),'Retry edited draft');
  assert.deepEqual(errors,[]);
 } finally {await page.close();}
}},{name:'memory is a main navigation peer and repair labels replace maintenance recovery',fn:async browser=>{
 for(const theme of ['dark','light']){
  const {page,errors}=await setup(browser,theme);
  try {
   const nav=page.locator('nav[aria-label="主导航"]');
   assert.deepEqual(await nav.locator('>button').evaluateAll(es=>es.map(e=>e.textContent)),['启动','模组与组件','存档','服务器','内存','设置','日志分析','帮助']);
   await nav.locator('[data-page="memory"]').click();await page.waitForSelector('#memory-number');
   assert.equal(await nav.locator('[data-page="memory"]').getAttribute('aria-current'),'page');
   assert.equal(await page.locator('#secondary-navigation [data-page="memory"]').count(),0);
   await nav.locator('[data-page="settings"]').click();
   assert.equal(await page.locator('#secondary-navigation [data-page="memory"]').count(),0);
   await nav.locator('[data-page="help"]').click();
   await page.locator('#secondary-navigation [data-page="maintenance"]').click();
   assert.equal(await page.locator('#page h1').textContent(),'修复');
   assert.doesNotMatch(await page.locator('body').innerText(),/维护与恢复/);
   assert.deepEqual(errors,[]);
  } finally {await page.close();}
 }
}}];
module.exports={tests};
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const {name,fn} of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} memory recovery groups passed`);if(failed)process.exitCode=1;})();
