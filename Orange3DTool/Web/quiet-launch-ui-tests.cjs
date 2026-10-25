const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const tests=[],test=(name,run)=>tests.push({name,run}),shots=path.resolve(__dirname,'../../artifacts/quiet-launch-128');fs.mkdirSync(shots,{recursive:true});
const settled=p=>p.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length&&!orange.state.busy);
test('advisory warnings never demand another launch confirmation and passing details start collapsed',async browser=>{
 const{page,errors}=await setup(browser);try{await settled(page);await page.evaluate(()=>fx.preflight='warning');await page.locator('#launch').click();
  await page.waitForFunction(()=>sent.some(m=>m.action==='launch')||document.querySelector('#confirm[open]'));
  assert.equal(await page.locator('#confirm[open]').count(),0,'ordinary warnings must not open a confirmation');
  await page.waitForFunction(()=>!orange.state.launchAttempt);assert.equal(await page.locator('.preflight-panel .preflight-item:visible').count(),0,'passing report must not expand the checklist');
  const detail=page.locator('.preflight-panel details');assert.equal(await detail.count(),1);await detail.locator('summary').click();assert.ok(await page.locator('.preflight-item:visible').count()>0);assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='javaSave').length),0);
  await page.locator('#launch').click();await page.waitForFunction(()=>sent.filter(m=>m.action==='launch').length===2&&!orange.state.launchAttempt);assert.equal(await page.locator('#confirm[open]').count(),0);assert.equal(await detail.getAttribute('open'),null);await page.screenshot({path:path.join(shots,'quiet-home.png')});assert.deepEqual(errors,[]);
 }finally{await page.close();}
});
test('confirmed save deletion remains available with a running client flag and native errors retain the card',async browser=>{
 const{page,errors}=await setup(browser);try{await settled(page);await page.evaluate(()=>fx.saves=[{id:'Sandbox/Test',name:'测试存档',mode:'Sandbox',editable:true,modifiedUtc:'2026-10-15T00:00:00Z',modCount:1}]);await route(page,'saves');await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:true,launch:{pending:true,stage:'submitted-save'}}));const button=page.locator('[data-save-delete="Sandbox/Test"]');assert.equal(await button.isEnabled(),true,'global runtime or active direct-launch session cannot disable selected-save deletion');await button.click();await page.locator('#confirm-no').click();assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='saveDelete').length),0);
  await page.evaluate(()=>fx.failure='saveDelete');await button.click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.saveDeleting);assert.equal(await button.count(),1);assert.ok((await page.locator('#notice').innerText()).length>0);
  await page.evaluate(()=>fx.failure='');await button.click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.saveDeleting);assert.equal(await button.count(),0);assert.deepEqual(errors,[]);
 }finally{await page.close();}
});
test('passing reports remain compact below the cover in both themes and blocked reports remain visible',async browser=>{
 for(const theme of ['dark','light']){const{page,errors}=await setup(browser,theme);try{await settled(page);await page.setViewportSize({width:733,height:507});await page.locator('#launch').click();await page.waitForFunction(()=>sent.some(m=>m.action==='launch')&&!orange.state.launchAttempt);
  const measurements=await page.locator('.preflight-panel').evaluate(el=>({height:el.getBoundingClientRect().height,afterCover:el.getBoundingClientRect().top>=document.querySelector('[data-home-cover]').getBoundingClientRect().bottom,overflow:document.querySelector('main').scrollWidth>document.querySelector('main').clientWidth}));assert.ok(measurements.height<100);assert.equal(measurements.afterCover,true);assert.equal(measurements.overflow,false);
  await page.locator('.preflight-panel').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(shots,theme+'-compact-733.png')});
  await page.evaluate(()=>fx.preflight='blocked');await page.locator('#launch').click();await page.waitForFunction(()=>!orange.state.launchAttempt);assert.equal(await page.locator('.preflight-panel details').getAttribute('open'),'');assert.match(await page.locator('.preflight-panel').innerText(),/缺少 Buddy 启动文件/);assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='launch').length),2);assert.equal(await page.locator('#confirm[open]').count(),0);assert.deepEqual(errors,[]);
 }finally{await page.close();}}
});
(async()=>{const browser=await chromium.launch({headless:true});let failed=0;try{for(const t of tests){try{await t.run(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} quiet-launch groups passed`);if(failed)process.exitCode=1;})();
