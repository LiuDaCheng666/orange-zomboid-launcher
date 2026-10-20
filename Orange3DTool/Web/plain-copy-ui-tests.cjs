/* Exercise rendered copy and retained interactions; only native I/O is synthetic. */
const assert=require('node:assert/strict');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const tests=[];
const test=(name,run)=>tests.push({name,run});
test('cover gives configuration values without a disclaimer footer',async browser=>{
 const {page,errors}=await setup(browser);
 await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 const cover=page.locator('[data-home-actual]');
 assert.match(await cover.innerText(),/6 GiB/);
 assert.match(await cover.innerText(),/Original Viewpoint/);
 assert.doesNotMatch(await cover.innerText(),/不代表|不等于|待验证/);
 assert.equal(await cover.locator('small').count(),0);
 assert.deepEqual(errors,[]);await page.close();
});
test('selecting a profile clearly remains pending application',async browser=>{
 const {page}=await setup(browser);
 await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 await page.locator('#play-profile-select').selectOption('a'.repeat(32));
 await page.waitForFunction(()=>!orange.state.playSelecting);
 assert.match(await page.locator('[data-selected-profile]').innerText(),/待应用/);
 assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='playProfileApply')),false);
 await page.close();
});
test('a completed version read preserves unknown as distinct from still loading',async browser=>{
 const {page}=await setup(browser);await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 await page.evaluate(()=>orange.acceptStatus({...orange.state.status,gameVersion:{state:'unknown',value:'',warning:'未找到版本文件'}}));
 const text=await page.locator('[data-home-actual]').innerText();
 assert.match(text,/游戏版本\n未知/);assert.match(text,/未找到版本文件/);
 await page.close();
});
test('a recovery conflict points to its actual file warning rather than a wrong directory',async browser=>{
 const {page}=await setup(browser);await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 await page.evaluate(()=>{orange.state.playProfiles.pendingRecovery={pending:true,canRecover:false,locations:['viewpoint-live.properties'],warnings:['外部编辑与原配置不同，请保留当前文件']};orange.acceptStatus({...orange.state.status,configStamp:'changed'});});
 const text=await page.locator('.recovery-panel').innerText();assert.match(text,/外部编辑/);assert.doesNotMatch(text,/请选择原游戏目录/);
 await page.close();
});
test('component details describe requirements without generic disclaimers',async browser=>{
 const {page}=await setup(browser);await route(page,'components');
 await page.locator('[data-card="base"] details').evaluate(e=>e.open=true);
 const text=await page.locator('[data-card="base"]').innerText();
 assert.doesNotMatch(text,/不代表|不等于|不冒充|待验证|需.*实测|游戏内验证/);
 assert.equal(await page.locator('[data-card="base"] [data-operation="uninstall"]').count(),1);
 await page.close();
});
test('launch feedback reports submission without claiming server entry',async browser=>{
 const {page}=await setup(browser);await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 await page.locator('#launch').click();await page.waitForFunction(()=>!orange.state.launchAttempt);
 const text=await page.locator('[data-launch-feedback]').innerText();
 assert.match(text,/已提交|请求已发送/);
 assert.doesNotMatch(text,/待验证|等待游戏确认|已进入服务器|已进入存档/);
 await page.close();
});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.run(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} direct-copy UI groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1});
