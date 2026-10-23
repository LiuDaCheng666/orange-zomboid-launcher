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
 const geometry=await page.locator('.catalog-row').evaluateAll(rows=>rows.map(e=>{const r=e.getBoundingClientRect();return {top:r.top,bottom:r.bottom,font:parseFloat(getComputedStyle(e.querySelector('.key')).fontSize)};}));
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
test('Reorganized component and profile actions retain bridge payloads',async browser=>{
 const {page}=await setup(browser);await route(page,'components');await page.locator('[data-component="buddy"][data-operation="uninstall"]').click();if(await page.locator('#confirm').isVisible())await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>sent.some(m=>m.action==='componentsAction'));assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='componentsAction').payload),{component:'buddy',operation:'uninstall',confirmed:true});
 await route(page,'mods');await page.locator('[data-mod-profile-open="relax"]').click();await page.waitForFunction(()=>sent.some(m=>m.action==='modProfileGet'));assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='modProfileGet').payload),{id:'relax'});await page.close();
});
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}if(failed)process.exitCode=1;})();
