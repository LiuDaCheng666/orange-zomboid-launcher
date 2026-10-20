const assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const {setup,chromium}=require('./experience-ui-tests.cjs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 for(const theme of ['dark','light']){
  const {page}=await setup(browser,theme);await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
  await page.locator('[data-home-manage],[data-home-details]').evaluateAll(es=>es.forEach(e=>e.open=false));
  const tools=page.locator('[data-launcher-tools]');assert.equal(await tools.count(),1,'home exposes one compact set of the requested tools');
  const evidence=path.resolve(__dirname,'../../artifacts/launcher-tools');fs.mkdirSync(evidence,{recursive:true});await page.screenshot({path:path.join(evidence,'home-'+theme+'.png'),fullPage:true});
  await tools.getByRole('button',{name:'修改内存',exact:true}).click();await page.locator('#memory-number').waitFor();assert.equal(await page.locator('[data-save-memory]').isVisible(),true);
  await page.locator('nav[aria-label="主导航"] [data-page="home"]').click();await page.locator('[data-launcher-tools]').getByRole('button',{name:'清除缓存',exact:true}).click();
  assert.equal(await page.locator('[data-maintenance="cache"]').isVisible(),true);assert.equal(await page.locator('[data-maintenance="cache"]').evaluate(e=>e===document.activeElement),true);
  await page.locator('nav[aria-label="主导航"] [data-page="home"]').click();await page.locator('[data-launcher-tools]').getByRole('button',{name:'重下工坊模组',exact:true}).click();assert.equal(await page.locator('[data-maintenance="workshop"]').isVisible(),true);
  await page.locator('nav[aria-label="主导航"] [data-page="home"]').click();await page.locator('[data-launcher-tools]').getByRole('button',{name:'更换模组',exact:true}).click();assert.equal(await page.locator('#page').getAttribute('data-route'),'mods');
  await page.locator('nav[aria-label="主导航"] [data-page="settings"]').click();assert.equal(await page.locator('[data-launcher-tools]').isVisible(),true,'settings exposes the same tools without requiring help navigation');
  assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='maintenance')),false,'opening a tool never clears or downloads automatically');await page.close();console.log('PASS tool navigation '+theme);
 }
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
