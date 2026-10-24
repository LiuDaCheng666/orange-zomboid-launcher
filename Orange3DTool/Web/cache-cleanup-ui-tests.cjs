const assert=require('node:assert/strict');
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const tests=[{name:'full multiplayer cleanup requires selected option and explicit confirmation',fn:async browser=>{
 const {page,errors}=await setup(browser);
 try{
  await route(page,'maintenance');
  const option=page.locator('[data-cache-all]');
  assert.equal(await option.isChecked(),false);
  await page.locator('[data-maintenance="cache"]').click();
  assert.match(await page.locator('#confirm-body').textContent(),/角色.*保留/);
  await page.locator('#confirm-no').click();
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='maintenance').length),0);
  await option.check();
  await page.locator('[data-maintenance="cache"]').click();
  assert.match(await page.locator('#confirm-title').textContent(),/彻底/);
  assert.match(await page.locator('#confirm-body').textContent(),/Saves\/Multiplayer/);
  assert.match(await page.locator('#confirm-body').textContent(),/角色.*车辆.*地图标记/);
  assert.match(await page.locator('#confirm-body').textContent(),/可能无法恢复/);
  await page.locator('#confirm-no').click();
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='maintenance').length),0);
  await page.locator('[data-maintenance="cache"]').click();await page.locator('#confirm-yes').click();
  await page.waitForFunction(()=>sent.some(m=>m.action==='maintenance'));
  assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='maintenance').payload),{kind:'cacheAll',confirmed:true});
  assert.deepEqual(errors,[]);
 }finally{await page.close();}
}},{name:'ordinary multiplayer cleanup retains its limited action when full option is unchecked',fn:async browser=>{
 const {page}=await setup(browser);
 try{
  await route(page,'maintenance');await page.locator('[data-cache-all]').check();await page.locator('[data-cache-all]').uncheck();
  await page.locator('[data-maintenance="cache"]').click();await page.locator('#confirm-yes').click();
  await page.waitForFunction(()=>sent.some(m=>m.action==='maintenance'));
  assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='maintenance').payload),{kind:'cache',confirmed:true});
 }finally{await page.close();}
}}];
module.exports={tests};
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});try{for(const t of tests){await t.fn(browser);console.log('PASS '+t.name);}}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1});
