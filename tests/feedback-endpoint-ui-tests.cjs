const assert=require('node:assert/strict');
const {setup,route,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const {page,errors}=await setup(browser);try{
  await route(page,'feedback');await page.locator('[data-feedback-root]').waitFor();
  assert.doesNotMatch(await page.locator('[data-feedback-root]').innerText(),/orangezl\.com|\/api\/feedback|发送至 https:/,'players do not see the feedback transport endpoint');
  assert.deepEqual(errors,[]);console.log('PASS feedback form hides transport URL');
 }finally{await page.close();}
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
