/* Real DOM with a synthetic WebView transport. No game or account access. */
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
const shots=path.resolve(__dirname,'../../artifacts/retire-aim-129/ui-shots');
async function shootFixture(page,extra={}){
  await page.evaluate(extra=>{
    const components=[{id:'base',item:'3809306528',title:'Project Viewpoint + Buddy',downloaded:true,configured:true},{id:'chinese',item:'3810637496',title:'老余汉化',downloaded:true,configured:true},{id:'shoot',item:'3813032735',title:'Shoot to Thrill',optional:true,downloaded:true,configured:true,description:'Author multiplayer gun mod',...extra}];
    orange.acceptStatus({...orange.state.status,components});
    const original=chrome.webview.postMessage;
    chrome.webview.postMessage=m=>{
      if(m.action==='status'){sent.push(m);queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:{...orange.state.status,task:{active:false}}}}));return;}
      original(m);
    };
  },extra);
  await route(page,'components');
  await page.waitForFunction(()=>!orange.state.loading.modCatalog);
  await page.evaluate(()=>{orange.state.modCatalog={mods:[{id:'author-gun',workshopId:'3813032735',cover:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6ZyAAAAAASUVORK5CYII='}],warnings:[]};});
}
async function checkReadOnlyHelp(page){
  const card=page.locator('[data-card="shoot"]');
  assert.equal(await card.getByRole('button',{name:'使用说明',exact:true}).count(),1);
  await card.getByRole('button',{name:'使用说明',exact:true}).click();
  const help=card.locator('[data-shoot-help]');
  await help.waitFor();
  assert.match(await help.innerText(),/右键/);
  assert.match(await help.innerText(),/Ctrl\+V/);
  assert.match(await help.innerText(),/服务器.*客户端.*匹配|客户端.*服务器.*匹配/);
  assert.equal(await help.locator('input,select,textarea,button').count(),0,'author controls are read-only guidance');
  assert.equal(await page.locator('#reticle-native,#reticle-key,[data-save-reticle]').count(),0,'retired launcher mode and fallback shortcut cannot be edited');
  assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='reticleSave'||m.action==='reticleGet')),false,'opening author guidance never reads or writes retired preferences');
}
test('gun help exposes author controls without launcher aim editing or persistence',async browser=>{
  const {page,errors}=await setup(browser);await shootFixture(page);await checkReadOnlyHelp(page);
  await route(page,'home');assert.equal(await page.locator('#launch').isEnabled(),true,'reading help creates no unsaved launch blocker');
  assert.deepEqual(errors,[]);await page.close();
});
test('old preference data cannot restore the retired mode controls',async browser=>{
  const {page}=await setup(browser);await page.evaluate(()=>{orange.state.reticle={startNative:false,keyText:'CTRL+F8',retired:true};});
  await shootFixture(page);await checkReadOnlyHelp(page);await page.close();
});
test('retirement waits for explicit gun reapply and uses the normal component install route',async browser=>{
  const {page}=await setup(browser);await shootFixture(page,{bridgeRetirementNeeded:true,needsReapply:true});
  const card=page.locator('[data-card="shoot"]');
  assert.match(await card.innerText(),/旧.*瞄准.*清理/);
  assert.equal(await page.evaluate(()=>sent.filter(m=>['componentsAction','reticleSave'].includes(m.action)).length),0,'displaying the retirement prompt does not mutate game configuration');
  await card.getByRole('button',{name:'重新应用',exact:true}).click();
  await page.waitForFunction(()=>sent.some(m=>m.action==='componentsAction'));
  assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='componentsAction').map(m=>m.payload)),[{component:'shoot',operation:'install',confirmed:true}]);
  assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='reticleSave')),false);await page.close();
});
test('read-only gun help stays contained with author covers in both themes and narrow windows',async browser=>{
  fs.mkdirSync(shots,{recursive:true});
  for(const theme of ['dark','light'])for(const width of [1100,500]){
    const {page,errors}=await setup(browser,theme);await page.setViewportSize({width,height:760});await shootFixture(page,{bridgeRetirementNeeded:true,needsReapply:true});await checkReadOnlyHelp(page);
    assert.equal(await page.locator('[data-card="shoot"] > .cover img').count(),1,'the locally supplied author cover remains in the gun card');
    assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true,'help does not overflow the scroll panel');
    await page.locator('[data-card="shoot"]').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(shots,`${theme}-${width}-gun-help.png`)});assert.deepEqual(errors,[]);await page.close();
  }
});
module.exports={tests,shootFixture,checkReadOnlyHelp};
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const{name,fn}of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} retired aim UI groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1;});
