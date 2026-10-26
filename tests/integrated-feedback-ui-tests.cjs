const assert=require('node:assert/strict');
const {setup,route,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});try{
 const {page,errors}=await setup(b);try{
  await page.evaluate(()=>{const base=orange.request;orange.request=(a,p)=>a==='feedbackStatus'?new Promise(resolve=>{window.releaseFeedbackStatus=()=>resolve({available:{gameLog:true,launcherLog:true},history:[]});}):base(a,p);});
  await page.locator('nav [data-page=feedback]').click();await page.locator('[data-feedback-root]').waitFor();
  await page.waitForFunction(()=>typeof window.releaseFeedbackStatus==='function');
  await page.locator('[data-feedback-description]').fill('读取记录期间输入的问题');await page.evaluate(()=>releaseFeedbackStatus());
  await page.waitForFunction(()=>!document.querySelector('[data-feedback-reload]').disabled);
  assert.equal(await page.locator('[data-feedback-category=gameLog]').isEnabled(),true,'typing cannot discard independent log availability');
  assert.equal(await page.locator('[data-feedback-description]').inputValue(),'读取记录期间输入的问题');
  assert.deepEqual(await page.locator('nav .active').evaluateAll(nodes=>nodes.map(n=>n.dataset.page)),['feedback'],'feedback has its own active navigation');
  assert.equal(await page.locator('#secondary-navigation').innerText(),'','feedback does not show game tools');
  await route(page,'mods');await page.waitForFunction(()=>orange.state.modCatalog&&!orange.state.loading.modCatalog);
  assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='modCatalog').at(-1).payload),{},'ordinary catalog reads use the display cache');
  await page.locator('[data-profiles-refresh=modProfiles]').click();await page.waitForFunction(()=>sent.filter(m=>m.action==='modCatalog').at(-1)?.payload.refresh===true);
  assert.deepEqual(errors,[]);console.log('PASS actual feedback navigation and explicit catalog refresh integration');
 }finally{await page.close();}
}finally{await b.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
