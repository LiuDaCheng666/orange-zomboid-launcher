// Real DOM; synthetic transport. File restoration is exercised separately against the native controller.
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const playwright={chromium};
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const tests=[],test=(name,fn)=>tests.push({name,fn});
const shots=path.resolve(__dirname,'../../artifacts/dlss-retirement/screenshots');
async function fixture(browser,{installed=false,fail=false,theme='dark',width=1100}={}){
 const {page,errors}=await setup(browser,theme);await page.setViewportSize({width,height:760});
 await page.evaluate(({installed,fail})=>{
  window.oldDlssInstalled=installed;window.dlssFail=fail;const original=chrome.webview.postMessage;
  chrome.webview.postMessage=m=>{
   const wrapped=m.action==='configurationRequest',action=wrapped?m.payload.action:m.action,payload=wrapped?m.payload.payload:m.payload;
   const answer=(data,error)=>queueMicrotask(()=>emitNative(error?{id:m.id,ok:false,error}:{id:m.id,ok:true,data:wrapped?{configurationId:m.payload.id,data}:data}));
   if(action==='settingsGet'){sent.push({...m,action,payload});answer({stamp:'retired',dlssRecovery:window.oldDlssInstalled,rows:[{key:'view|enabled',name:'第一人称视角',group:'视角设置',description:'游戏原有视角设置',kind:'bool',value:'true',editable:true}],warnings:[]});return;}
   if(action==='dlss'){sent.push({...m,action,payload});if(window.dlssFail){answer(null,'文件正在使用，请关闭游戏后重试。');return;}window.oldDlssInstalled=false;answer({message:'旧版 DLSS 已关闭，原文件已恢复。',dlssRecovery:false});return;}
   original(m);
  };
 },{installed,fail});
 await route(page,'settings');await page.waitForFunction(()=>orange.state.settings?.stamp==='retired');return {page,errors};
}
test('normal settings has no retired feature or download controls',async browser=>{
 const {page,errors}=await fixture(browser);assert.equal(await page.locator('[data-dlss-recovery],[data-action="dlss"]').count(),0);
 assert.doesNotMatch(await page.locator('main').innerText(),/启用实验 DLSS|下载.*DLSS|DLSS 实验功能/);
 assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='dlss')),false);assert.deepEqual(errors,[]);
 fs.mkdirSync(shots,{recursive:true});await page.screenshot({animations:'disabled',path:path.join(shots,'normal-settings.png')});await page.close();
});
test('legacy restoration can be cancelled without mutation',async browser=>{
 const {page,errors}=await fixture(browser,{installed:true});await page.locator('[data-dlss-recovery]>summary').click();
 assert.equal(await page.locator('[data-action="dlss"][data-operation="enable"]').count(),0);
 await page.getByRole('button',{name:'恢复原文件',exact:true}).click();await page.locator('#confirm').waitFor({state:'visible'});await page.screenshot({animations:'disabled',path:path.join(shots,'recovery-confirmation.png')});await page.locator('#confirm-no').click();await page.screenshot({animations:'disabled',path:path.join(shots,'recovery-cancelled.png')});
 assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='dlss').length),0);assert.equal(await page.getByRole('button',{name:'恢复原文件',exact:true}).isEnabled(),true);assert.deepEqual(errors,[]);await page.close();
});
test('legacy recovery confirms once then removes stale controls after success',async browser=>{
 const {page,errors}=await fixture(browser,{installed:true});await page.locator('[data-dlss-recovery]>summary').click();await page.getByRole('button',{name:'恢复原文件',exact:true}).click();await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>sent.some(m=>m.action==='dlss')&&!orange.state.busy&&!orange.state.settings?.dlssRecovery);
 assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='dlss').map(m=>m.payload)),[{operation:'disable',confirmed:true}]);
 assert.equal(await page.locator('[data-dlss-recovery]').count(),0);await page.screenshot({animations:'disabled',path:path.join(shots,'recovery-completed.png')});await route(page,'help');assert.doesNotMatch(await page.locator('main').innerText(),/实验 DLSS 来源说明|启用实验 DLSS/);await route(page,'settings');assert.equal(await page.locator('[data-dlss-recovery]').count(),0);assert.deepEqual(errors,[]);await page.close();
});
test('recovery failure leaves a usable retry and preserves the old installation state',async browser=>{
 const {page,errors}=await fixture(browser,{installed:true,fail:true});await page.locator('[data-dlss-recovery]>summary').click();await page.getByRole('button',{name:'恢复原文件',exact:true}).click();await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>sent.some(m=>m.action==='dlss')&&!orange.state.busy);await page.locator('[data-dlss-recovery]>summary').evaluate(e=>e.parentElement.open=true);
 assert.equal(await page.getByRole('button',{name:'恢复原文件',exact:true}).isEnabled(),true);assert.equal(await page.evaluate(()=>orange.state.settings.dlssRecovery),true);
 await page.locator('[data-dlss-recovery]').scrollIntoViewIfNeeded();await page.screenshot({animations:'disabled',path:path.join(shots,'recovery-failed-retry.png')});
 await page.evaluate(()=>window.dlssFail=false);await page.getByRole('button',{name:'恢复原文件',exact:true}).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.settings?.dlssRecovery);assert.deepEqual(errors,[]);await page.close();
});
test('pending recovery shows activity and preserves unsaved Viewpoint edits on completion',async browser=>{
 const {page,errors}=await fixture(browser,{installed:true});await page.locator('[data-setting="view|enabled"]').selectOption('false');
 await page.evaluate(()=>{const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{const action=m.action==='configurationRequest'?m.payload.action:m.action;if(action==='dlss'){window.releaseDlss=()=>original(m);return;}original(m);};});
 await page.locator('[data-dlss-recovery]>summary').click();await page.getByRole('button',{name:'恢复原文件',exact:true}).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>orange.state.busy&&typeof releaseDlss==='function');
 assert.equal(await page.getByRole('button',{name:'恢复原文件',exact:true}).isDisabled(),true);assert.equal(await page.locator('#task').isVisible(),true);await page.screenshot({animations:'disabled',path:path.join(shots,'recovery-in-progress.png')});
 await page.evaluate(()=>releaseDlss());await page.waitForFunction(()=>!orange.state.busy&&!orange.state.settings.dlssRecovery);assert.equal(await page.locator('[data-setting="view|enabled"]').inputValue(),'false');assert.equal(await page.evaluate(()=>orange.state.settingsDirty),true);assert.equal(await page.locator('[data-save-settings]').isEnabled(),true);assert.deepEqual(errors,[]);await page.close();
});
test('legacy recovery fits existing settings layout in both themes and narrow windows',async browser=>{
 fs.mkdirSync(shots,{recursive:true});for(const theme of ['dark','light'])for(const width of [1100,500]){
  const {page,errors}=await fixture(browser,{installed:true,theme,width});await page.locator('[data-dlss-recovery]>summary').click();await page.locator('[data-dlss-recovery]').scrollIntoViewIfNeeded();
  assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true);assert.equal(await page.locator('[data-action="dlss"]').count(),1);await page.screenshot({animations:'disabled',path:path.join(shots,`${theme}-${width}-recovery.png`)});assert.deepEqual(errors,[]);await page.close();
 }
});
module.exports={tests};
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const {name,fn}of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} DLSS retirement UI groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1;});
