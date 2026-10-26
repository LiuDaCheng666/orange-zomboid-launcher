const assert=require('node:assert/strict');
const {setup,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{const {page,errors}=await setup(browser);try{
 await page.evaluate(()=>orange.acceptStatus({...orange.state.status,gameVersion:{state:'unknown',value:'',warning:'尚未找到游戏根目录 version.txt，版本未知。'}}));
 assert.doesNotMatch(await page.locator('[data-home-actual]').innerText(),/version\.txt|重新核对/,'home hides implementation-only version-file messages');
 assert.equal(await page.locator('#launch').isEnabled(),true,'optional display metadata never disables launch');
 assert.deepEqual(errors,[]);console.log('PASS unknown optional version metadata leaves launch enabled without file-system warning');
}finally{await page.close();}}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
