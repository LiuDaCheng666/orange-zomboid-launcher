/* Real launcher DOM with the synthetic native boundary; no real game or server access. */
const assert=require('node:assert/strict');
const {setup,route,routes,chromium}=require('./refresh-fixture.cjs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0,passed=0;
 async function test(name,body){try{await body();passed++;console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}
 try{
  await test('same-route status refresh preserves entrance ownership and dirty editor focus',async()=>{
   const {page,errors}=await setup(browser);try{
    await route(page,'settings');await page.waitForFunction(()=>orange.state.settings);
    const input=page.locator('[data-setting]').first();await input.fill('P');
    await page.evaluate(()=>{window.beforeAnimations=document.getAnimations().filter(a=>a.effect?.target?.id==='page');window.reply({data:{event:'progress',data:{active:false,text:'已刷新'}}});document.querySelector('[data-page="settings"]').click();});
    assert.equal(await input.inputValue(),'P');assert.equal(await input.evaluate(e=>document.activeElement===e),true);
    assert.equal(await page.evaluate(()=>document.getAnimations().filter(a=>a.effect?.target?.id==='page').every(a=>beforeAnimations.includes(a))),true,'same route restarted entrance');assert.deepEqual(errors,[]);
   }finally{await page.close();}
  });
  await test('rapid route switches cancel older entrances and retain an interactive final page',async()=>{
   const {page}=await setup(browser);try{
    const result=await page.evaluate(()=>{document.querySelector('[data-page="components"]').click();const older=document.getAnimations().filter(a=>a.effect?.target?.id==='page');document.querySelector('[data-page="settings"]').click();document.querySelector('[data-page="memory"]').click();return {oldStates:older.map(a=>a.playState),live:document.getAnimations().filter(a=>a.effect?.target?.id==='page').length};});
    await route(page,'settings');
    assert.ok(result.oldStates.length,'navigation needs a visible finite entrance');assert.ok(result.oldStates.every(s=>s==='idle'),'old route entrance remains active');assert.ok(result.live<=1,'multiple route entrances overlap');
    await page.waitForFunction(()=>!!document.querySelector('[data-setting]'));await page.locator('[data-setting]').first().fill('L');assert.equal(await page.locator('[data-setting]').first().inputValue(),'L');
   }finally{await page.close();}
  });
  await test('reduced motion cancels active entrance and suppresses new route/dialog motion',async()=>{
   const {page}=await setup(browser);try{
    await page.evaluate(()=>{document.querySelector('[data-page="components"]').click();document.getAnimations().filter(a=>a.effect?.target?.id==='page').forEach(a=>a.pause());});await page.emulateMedia({reducedMotion:'reduce'});
    await page.waitForFunction(()=>document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).length===0);
    assert.equal(await page.evaluate(()=>document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).length),0,'preference change left an entrance running');
    await page.locator('[data-components-restore]').click();await page.locator('dialog[open]').waitFor();
    assert.equal(await page.evaluate(()=>document.getAnimations().length),0);
    await page.locator('#confirm-no').click();await route(page,'settings');assert.equal(await page.evaluate(()=>document.getAnimations().length),0);
   }finally{await page.close();}
  });
  await test('hidden document cancels presentation and starts no new decorative entrance',async()=>{
   const {page}=await setup(browser);try{
    const result=await page.evaluate(()=>{document.querySelector('[data-page="components"]').click();const old=document.getAnimations().filter(a=>a.effect?.target?.id==='page');Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));document.querySelector('[data-page="settings"]').click();return {old:old.map(a=>a.playState),count:document.getAnimations().filter(a=>a.effect?.target?.id==='page').length};});
    assert.ok(result.old.every(s=>s==='idle'));assert.equal(result.count,0);
   }finally{await page.close();}
  });
  for(const theme of ['dark','light'])await test(theme+' all routes readable and contained at compact viewport sizes',async()=>{
   const {page,errors}=await setup(browser,{theme});try{
    await page.emulateMedia({reducedMotion:'reduce'});
    for(const viewport of [{width:768,height:900},{width:1100,height:650}]){await page.setViewportSize(viewport);for(const name of routes){await route(page,name);await page.waitForFunction(()=>!Object.values(orange.state.loading).some(Boolean));
     const result=await page.evaluate(()=>({overflow:document.querySelector('main').scrollWidth>document.querySelector('main').clientWidth,small:[...document.querySelectorAll('#page button')].filter(e=>e.getClientRects().length&&!e.disabled).filter(e=>{const s=getComputedStyle(e);return parseFloat(s.fontSize)<12||e.getBoundingClientRect().height<30;}).map(e=>e.textContent),status:document.querySelector('#notice').getAttribute('role')}));
     assert.equal(result.overflow,false,`${name} ${viewport.width} overflow`);assert.deepEqual(result.small,[],`${name} ${viewport.width} unreadable controls`);assert.equal(result.status,'status','notice must expose status semantics');
    }}assert.deepEqual(errors,[]);
   }finally{await page.close();}
  });
 }finally{await browser.close();}
 console.log(`${passed} passed; ${failed} failed`);if(failed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
