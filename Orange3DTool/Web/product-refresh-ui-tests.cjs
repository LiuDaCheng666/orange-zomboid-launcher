const assert=require('node:assert/strict');
const {setup,route,chromium}=require('./refresh-fixture.cjs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
test('three independent core cards dispatch only the selected component; Chinese installs alone',async browser=>{
 const {page,errors}=await setup(browser,{missing:true});await route(page,'components');
 assert.deepEqual(await page.locator('[data-core-components] [data-card]').evaluateAll(es=>es.map(e=>e.dataset.card)),['buddy','viewpoint','chinese']);
 assert.equal(await page.locator('[data-optional-components]').evaluate(e=>e.open),false);
 for(const [id,title] of [['chinese','老余汉化 · JAVA 版'],['buddy','ZombieBuddy'],['viewpoint','Project Viewpoint']]){
  await page.getByRole('button',{name:'安装 '+title,exact:true}).click();
  await page.waitForFunction(id=>orange.state.status.components.find(c=>c.id===id).configured,id);
 }
 assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='componentsAction').map(m=>m.payload)),['chinese','buddy','viewpoint'].map(component=>({component,operation:'install',confirmed:true})));
 await page.locator('[data-card="chinese"] details>summary').click();await page.locator('[data-card="chinese"] [data-operation="update"]').click();
 await page.waitForFunction(()=>sent.filter(m=>m.action==='componentsAction').length===4);
 await page.getByRole('button',{name:'卸载 ZombieBuddy',exact:true}).click();await page.waitForFunction(()=>!orange.state.status.components.find(c=>c.id==='buddy').configured);
 assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='componentsAction').slice(-2).map(m=>m.payload)),[{component:'chinese',operation:'update',confirmed:true},{component:'buddy',operation:'uninstall',confirmed:true}]);
 await page.evaluate(()=>orange.acceptStatus({...demoStatus,components:[...demoStatus.components.map(c=>c.id==='buddy'?{...c,version:''}:c),{id:'base',version:'combined-only',configured:true}]}));
 assert.doesNotMatch(await page.locator('[data-card="buddy"]').innerText(),/combined-only/);assert.match(await page.locator('[data-card="buddy"]').innerText(),/未声明/);
 assert.deepEqual(errors,[]);await page.close();
});
test('next step is contextual and existing tool routes remain accessible in both themes',async browser=>{
 for(const theme of ['dark','light']){
  const {page}=await setup(browser,{theme});assert.equal(await page.locator('[data-next-step]').count(),0);
  await page.evaluate(()=>orange.acceptStatus({...demoStatus,validGame:false}));assert.match(await page.locator('[data-next-step]').innerText(),/选择游戏目录/);
  await page.evaluate(()=>orange.acceptStatus({...demoStatus,components:demoStatus.components.map(c=>({...c,configured:false}))}));assert.match(await page.locator('[data-next-step]').innerText(),/组件/);
  for(const name of ['memory','maintenance','mods','saves','servers','help']){await route(page,name);assert.ok(await page.locator('#page').innerText());}
  await page.close();
 }
});
test('startup checks once after native readiness, silently handles Latest and Error without locking editors',async browser=>{
 for(const update of ['Latest','Error']){
  const {page}=await setup(browser,{update});await page.waitForFunction(()=>sent.some(m=>m.action==='coreUpdateCheck'));
  await page.evaluate(()=>pollCallbacks.forEach(f=>f()));await route(page,'memory');await page.locator('#memory-number').fill('12');
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='coreUpdateCheck').length),1);
  assert.equal(await page.locator('dialog[open]').count(),0);assert.equal(await page.locator('#startup-update-notice').isVisible(),false);
  assert.equal(await page.evaluate(()=>orange.state.busy),false);assert.equal(await page.locator('#notice').isVisible(),false);await page.close();
 }
 for(const core of [{state:'Ready',taskId:'a'.repeat(32),latestVersion:'1.2.1'},{state:'Downloading',taskId:'a'.repeat(32),latestVersion:'1.2.1'}]){
  const {page}=await setup(browser,{core});await page.waitForFunction(()=>orange.state.coreUpdate);
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='coreUpdateCheck').length),0,'resumed updater work is observed without starting another check');assert.equal(await page.locator('dialog[open]').count(),0);await page.close();
 }
 const malformed=await setup(browser,{core:{protocol:9}});await malformed.page.waitForTimeout(40);assert.equal(await malformed.page.locator('#notice').isVisible(),false);assert.equal(await malformed.page.locator('#startup-update-notice').isVisible(),false);await malformed.page.close();
});
test('available startup notice is dismissible, preserves a dirty draft, and opens the updater only explicitly',async browser=>{
 const {page}=await setup(browser,{hold:'coreUpdateCheck'});await page.waitForFunction(()=>held.length===1);await route(page,'memory');await page.locator('#memory-number').fill('12');
 const deliver=()=>page.evaluate(()=>reply({data:{id:held[0].id,ok:true,data:{...core,revision:2,state:'Available',latestVersion:'1.2.1'}}}));await deliver();
 await page.locator('#startup-update-notice').waitFor();assert.equal(await page.locator('#memory-number').inputValue(),'12');assert.equal(await page.evaluate(()=>orange.state.memoryDirty),true);
 await page.locator('[data-startup-dismiss]').click();await deliver();await page.evaluate(()=>reply({data:{event:'coreUpdateProgress',data:{...core,revision:3,state:'Available',latestVersion:'1.2.1'}}}));
 assert.equal(await page.locator('#startup-update-notice').isVisible(),false);assert.equal(await page.evaluate(()=>sent.some(m=>/coreUpdateDownload|coreUpdateApply/.test(m.action))),false);await page.close();
 const next=await setup(browser,{update:'Available'});await next.page.locator('#startup-update-notice [data-core-open]').click();assert.equal(await next.page.locator('#core-update-panel').isVisible(),true);await next.page.close();
});
test('late automatic responses cannot replace manual download or Ready state, including progress events',async browser=>{
 const {page}=await setup(browser,{hold:'coreUpdateCheck'});await page.waitForFunction(()=>held.length===1);await route(page,'help');
 await page.evaluate(()=>reply({data:{event:'coreUpdateProgress',data:{...core,revision:3,state:'Available',latestVersion:'1.2.1'}}}));await page.locator('[data-core-download]').click();
 for(const phase of ['Downloading','Ready']){
  if(phase==='Ready')await page.evaluate(()=>{orange.state.memoryDirty=true;reply({data:{event:'coreUpdateProgress',data:{...core,revision:6,state:'Ready',latestVersion:'1.2.1',taskId:'a'.repeat(32)}}});});
  await page.evaluate(()=>{const data={...core,revision:99,state:'Available',latestVersion:'9.9.9'};reply({data:{id:held[0].id,ok:true,data}});reply({data:{event:'coreUpdateProgress',data}});});
  assert.equal(await page.evaluate(()=>orange.state.coreUpdate.state),phase);assert.equal(await page.locator('#startup-update-notice').isVisible(),false);
 }await page.close();
});
module.exports={tests};
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}if(failed)process.exitCode=1;})();
