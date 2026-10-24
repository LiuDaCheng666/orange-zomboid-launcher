/* Actual DOM/material/motion contracts; synthetic native boundary only. */
const assert=require('node:assert/strict');
const {setup,route,routes,chromium}=require('./refresh-fixture.cjs');
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});let passed=0,failed=0;
 async function test(name,fn){try{await fn();console.log('PASS '+name);passed++;}catch(e){console.error('FAIL '+name+'\n'+e.stack);failed++;}}
 try{
  for(const theme of ['dark','light'])await test(theme+' core glass transmits ambient layers with inner and outer edge depth',async()=>{
   const {page}=await setup(browser,{theme});try{await route(page,'components');
    const surfaces=await page.locator('.core-components .component-card,header,.workspace-bar').evaluateAll(es=>es.map(e=>{const s=getComputedStyle(e);return {background:s.backgroundColor,image:s.backgroundImage,shadow:s.boxShadow};}));
    for(const s of surfaces){const shadows=s.shadow.split(/,(?![^()]*\))/);assert.ok(s.background.startsWith('rgba('),'surface remains opaque: '+JSON.stringify(s));assert.ok(shadows.some(s=>s.includes('inset'))&&shadows.some(s=>!s.includes('inset')),'missing inner/outer material depth');assert.notEqual(s.image,'none','missing bounded lens highlight');}
    assert.notEqual(await page.locator('body').evaluate(e=>getComputedStyle(e).backgroundImage),'none','no ambient field behind glass');
   }finally{await page.close();}
  });
  await test('route has distinct scale/displacement at 35% and 70% while inputs remain usable',async()=>{
   const {page}=await setup(browser);try{
    await page.evaluate(()=>document.querySelector('nav [data-page="settings"]').click());await page.waitForSelector('[data-setting]');
    const sampled=await page.evaluate(()=>{const a=document.getAnimations().find(a=>a.effect?.target?.id==='page');if(!a)return null;a.pause();const duration=a.effect.getTiming().duration;return [0,.35,.7,1].map(t=>{a.currentTime=duration*t;const s=getComputedStyle(document.querySelector('#page'));return {duration,transform:s.transform,opacity:s.opacity};});});
    assert.ok(sampled,'missing route presentation');assert.ok(sampled[0].duration>=320&&sampled[0].duration<=420,'route motion too brief');
    assert.notEqual(sampled[1].transform,sampled[2].transform);assert.notEqual(sampled[1].transform,sampled[3].transform);
    assert.ok(!sampled[0].transform.startsWith('matrix(1, 0, 0, 1,'),'route needs depth as well as translation');
    await page.locator('[data-setting]').first().fill('P');assert.equal(await page.locator('[data-setting]').first().inputValue(),'P');
   }finally{await page.close();}
  });
  await test('navigation selection travels continuously and realigns to selected control after resize and interruption',async()=>{
   const {page}=await setup(browser);try{
    const result=await page.evaluate(()=>{const nav=document.querySelector('nav');const before=nav.querySelector('[aria-hidden="true"]');document.querySelector('nav [data-page="settings"]').click();const indicator=nav.querySelector('[aria-hidden="true"]');if(!indicator)return null;const a=indicator.getAnimations()[0];if(!a)return {animated:false};a.pause();a.currentTime=a.effect.getTiming().duration*.35;const first=indicator.getBoundingClientRect().y;a.currentTime=a.effect.getTiming().duration*.7;return {animated:true,first,second:indicator.getBoundingClientRect().y};});
    assert.ok(result?.animated,'selection has no moving surface');assert.ok(Math.abs(result.first-result.second)>2,'selection did not travel');
    await page.evaluate(()=>{document.querySelector('nav [data-page="components"]').click();document.querySelector('nav [data-page="help"]').click();});
    for(const width of [1440,768,500]){await page.setViewportSize({width,height:900});await page.waitForTimeout(450);const r=await page.evaluate(()=>{const a=document.querySelector('nav .active').getBoundingClientRect(),b=document.querySelector('nav [aria-hidden="true"]').getBoundingClientRect();return {dx:a.x-b.x,dy:a.y-b.y,dw:a.width-b.width,dh:a.height-b.height};});for(const delta of Object.values(r))assert.ok(Math.abs(delta)<1,JSON.stringify(r));}
    await route(page,'home');await page.setViewportSize({width:1440,height:900});await page.waitForTimeout(450);await page.setViewportSize({width:1440,height:507});await page.waitForTimeout(50);
    const heights=await page.evaluate(()=>[document.querySelector('nav .active'),document.querySelector('nav [aria-hidden="true"]')].map(e=>e.getBoundingClientRect().height));assert.equal(heights[0],heights[1],'height-only resize lost navigation alignment');
   }finally{await page.close();}
  });
  await test('pointer hover click and leave keeps selection aligned with Settings',async()=>{
   const {page}=await setup(browser);try{
    const settings=page.locator('nav [data-page="settings"]');await settings.hover();await page.waitForTimeout(280);await settings.click();await page.waitForTimeout(500);await page.mouse.move(1200,850);await page.waitForTimeout(300);
    const deltas=await page.evaluate(()=>{const a=document.querySelector('nav .active').getBoundingClientRect(),b=document.querySelector('nav [aria-hidden="true"]').getBoundingClientRect();return [a.x-b.x,a.y-b.y,a.width-b.width,a.height-b.height];});
    assert.ok(deltas.every(d=>Math.abs(d)<1),'pointer navigation left selection offset: '+JSON.stringify(deltas));
   }finally{await page.close();}
  });
  await test('all presentation cancels for reduced motion and hidden documents',async()=>{
   for(const mode of ['reduced','hidden']){const {page}=await setup(browser);try{
    await page.evaluate(()=>document.querySelector('nav [data-page="components"]').click());
    if(mode==='reduced')await page.emulateMedia({reducedMotion:'reduce'});else await page.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));});
    await page.waitForTimeout(80);assert.equal(await page.evaluate(()=>document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).length),0);
   }finally{await page.close();}}
  });
  await test('route intermediate frames never create horizontal overflow in narrow content',async()=>{
   const {page}=await setup(browser);try{for(const width of [1100,733,500]){await page.setViewportSize({width,height:760});await route(page,'home');
    const samples=await page.evaluate(()=>{document.querySelector('nav [data-page="components"]').click();const a=document.getAnimations().find(a=>a.effect?.target?.id==='page');a.pause();return [0,.1,.35,.7].map(f=>{a.currentTime=a.effect.getTiming().duration*f;const main=document.querySelector('main');return {fraction:f,scroll:main.scrollWidth,client:main.clientWidth};});});
    for(const sample of samples)assert.ok(sample.scroll<=sample.client,width+' '+JSON.stringify(sample));
   }}finally{await page.close();}
  });
  await test('same-route rendering restores open disclosure without replaying its entrance',async()=>{
   const {page}=await setup(browser);try{await route(page,'components');await page.locator('[data-optional-components]>summary').click();await page.waitForTimeout(450);
    await page.evaluate(()=>document.querySelector('nav [data-page="components"]').click());
    const result=await page.locator('[data-optional-components]').evaluate(e=>({open:e.open,effects:e.querySelector(':scope > :not(summary)').getAnimations().length}));
    assert.equal(result.open,true,'restored disclosure must remain open');assert.equal(result.effects,0,'restoration replayed disclosure entrance');
   }finally{await page.close();}
  });
  await test('rapid disclosure toggles cancel closed and superseded content effects',async()=>{
   const {page}=await setup(browser);try{await route(page,'components');
    const result=await page.evaluate(async()=>{const details=document.querySelector('[data-optional-components]'),target=details.querySelector(':scope > :not(summary)');details.open=true;await Promise.resolve();const first=target.getAnimations();first.forEach(a=>a.pause());details.open=false;await Promise.resolve();const closed=first.map(a=>a.playState);details.open=true;await Promise.resolve();const second=target.getAnimations();second.forEach(a=>a.pause());details.open=false;details.open=true;await Promise.resolve();return {firstCount:first.length,closed,secondStates:second.map(a=>a.playState),live:target.getAnimations().length,open:details.open};});
    assert.equal(result.firstCount,1,'initial opening should animate');assert.ok(result.closed.every(s=>s==='idle'),'closing left an owned effect');assert.ok(result.secondStates.every(s=>s==='idle'),'reopening kept obsolete effect');assert.equal(result.live,1,'rapid reopening accumulated effects');assert.equal(result.open,true);
   }finally{await page.close();}
  });
  for(const theme of ['dark','light'])await test(theme+' all routes and dialogs fit 733×507 and 500×760',async()=>{
   const {page,errors}=await setup(browser,{theme});try{await page.emulateMedia({reducedMotion:'reduce'});
    for(const [width,height] of [[733,507],[500,760]]){await page.setViewportSize({width,height});for(const name of routes){await route(page,name);assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true,name+' horizontal overflow');}
     await route(page,'maintenance');await page.locator('[data-maintenance="cache"]').click();const box=await page.locator('dialog[open]').boundingBox();assert.ok(box.x>=0&&box.y>=0&&box.x+box.width<=width&&box.y+box.height<=height);await page.locator('#confirm-no').click();
    }assert.deepEqual(errors,[]);
   }finally{await page.close();}
  });
 }finally{await browser.close();}console.log(`${passed} passed; ${failed} failed`);if(failed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
