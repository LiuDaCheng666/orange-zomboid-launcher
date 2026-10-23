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
  assert.equal(bounds.length,6);for(const r of bounds)assert.ok(r.left>=0&&r.right<=width&&r.top>=0&&r.bottom<=height,'all six routes in bounds');
  for(const name of ['settings','help']){const b=page.locator('nav [data-page="'+name+'"]');await b.focus();await page.keyboard.press('Enter');await page.waitForFunction(n=>document.querySelector('#page').dataset.route===n,name);}
  const summary=page.locator('.header-menu>summary');if(await summary.isVisible()){await summary.focus();await page.keyboard.press('Enter');}
  await page.locator('#theme').focus();await page.keyboard.press('Enter');assert.equal(await page.locator('#theme').isVisible(),true);
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
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}if(failed)process.exitCode=1;})();
