/* Production DOM decisions; only WebView I/O is synthetic. No real saves or Steam changes. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/confirmation-lifecycle-1238');fs.mkdirSync(out,{recursive:true});
const cases=[],test=(name,run)=>cases.push({name,run});
const nativeClose=(page,selector)=>page.locator(selector).evaluate(dialog=>new Promise(resolve=>{
 dialog.addEventListener('close',()=>queueMicrotask(resolve),{once:true});dialog.close();
}));
const calls=(page,action)=>page.evaluate(action=>sent.filter(message=>(message.action==='configurationRequest'?message.payload.action:message.action)===action).length,action);
const settled=page=>page.evaluate(()=>Promise.all(document.getAnimations().filter(animation=>Number.isFinite(animation.effect?.getComputedTiming().endTime)).map(animation=>animation.finished.catch(()=>{}))));
async function javaFixture(page){
 await page.evaluate(()=>{
  const base=chrome.webview.postMessage;
  chrome.webview.postMessage=message=>{
   const action=message.action==='configurationRequest'?message.payload.action:message.action;
   let data;
   if(action==='javaComponentsList')data={stamp:'lifecycle-java',rows:[{token:'lifecycle-token',name:'生命周期测试组件',modId:'fixture',enabled:true,canDisable:true,jarState:'verified',limitations:[],conflicts:[],requires:[]}],warnings:[]};
   else if(action==='javaComponentsPreview')data={supported:true,previewStamp:'lifecycle-preview',message:'屏蔽此测试组件',warnings:[],changes:['隔离测试变更']};
   else if(action==='javaComponentsApply')data={status:'completed',message:'测试组件已屏蔽'};
   else if(action==='forceTakeoverPreview')data={supported:true,stamp:'lifecycle-force',requiresSteamExit:true,locations:[],unresolved:[]};
   else if(action==='forceTakeoverApply')data={status:'completed',message:'隔离夹具已接管'};
   else return base(message);
   sent.push(message);queueMicrotask(()=>emitNative({id:message.id,ok:true,data:message.action==='configurationRequest'?{configurationId:message.payload.id,data}:data}));
  };
 });
 await route(page,'javaMods');await page.waitForFunction(()=>!orange.library.model.javaLoading);
}

test('native close of save deletion releases the action without deleting; retry confirms once',async page=>{
 await page.evaluate(()=>{fx.saves=[{id:'Sandbox/Test',name:'隔离测试存档',mode:'Sandbox',editable:true}];orange.state.saves=null;});
 await route(page,'saves');
 await page.locator('[data-save-delete="Sandbox/Test"]').click();await page.locator('#confirm[open]').waitFor();
 await nativeClose(page,'#confirm');
 await page.waitForFunction(()=>!orange.state.saveDeleting);
 assert.equal(await calls(page,'saveDelete'),0);
 assert.equal(await page.locator('[data-save-delete="Sandbox/Test"]').isEnabled(),true);
 await page.locator('[data-save-delete="Sandbox/Test"]').click();await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>!orange.state.saveDeleting);
 assert.equal(await calls(page,'saveDelete'),1);
 assert.equal(await page.locator('[data-save-delete="Sandbox/Test"]').count(),0);
 await settled(page);await page.screenshot({path:path.join(out,'save-delete-finished.png')});
});

for(const kind of ['force','java'])test(kind+' confirmation native close, Cancel and Escape release busy; Confirm executes once',async page=>{
 if(kind==='force')await page.locator('#launch-configuration-select').selectOption('steam');
 await javaFixture(page);
 const button=kind==='force'?'[data-force-takeover]':'[data-java-component-action="disable"]',dialog=kind==='force'?'#force-takeover-dialog':'.java-component-confirm',cancel=kind==='force'?'[data-force-cancel]':'[data-java-plan-cancel]',yes=kind==='force'?'[data-force-confirm]':'[data-java-plan-confirm]',action=kind==='force'?'forceTakeoverApply':'javaComponentsApply';
 for(const exit of ['native','cancel','escape']){
  await page.locator(button).click();await page.locator(dialog+'[open]').waitFor();
  if(exit==='native')await nativeClose(page,dialog);
  else if(exit==='cancel')await page.locator(cancel).click();
  else await page.keyboard.press('Escape');
  await page.waitForFunction(()=>!orange.library.model.working);
  assert.equal(await calls(page,action),0,exit+' cannot execute the mutation');
  assert.equal(await page.locator(dialog).count(),0);
  assert.equal(await page.locator(button).isEnabled(),true);
 }
 await page.locator(button).click();await page.locator(yes).click();
 await page.waitForFunction(()=>!orange.library.model.working);
 assert.equal(await calls(page,action),1);
 await settled(page);await page.screenshot({path:path.join(out,kind+'-finished.png')});
});

test('the old close event cannot cancel a newly opened shared confirmation',async page=>{
 await route(page,'components');
 await page.locator('[data-components-restore]').click();await page.locator('#confirm[open]').waitFor();
 await page.evaluate(()=>{
  document.querySelector('#confirm-no').click();
  window.restoreRetry=Promise.resolve().then(()=>document.querySelector('[data-components-restore]').click());
 });
 await page.locator('#confirm[open]').waitFor();
 await settled(page);
 assert.equal(await page.locator('#confirm').evaluate(dialog=>dialog.open),true);
 await page.locator('#confirm-no').click();
 assert.equal(await calls(page,'componentsAction'),0);
});

(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});const results=[];try{
 for(const item of cases){const{page,errors}=await setup(browser);await page.evaluate(()=>{window.promiseErrors=[];addEventListener('unhandledrejection',event=>promiseErrors.push(String(event.reason)));});
  try{await item.run(page);assert.deepEqual(errors,[]);assert.deepEqual(await page.evaluate(()=>promiseErrors),[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}
  catch(error){results.push({name:item.name,status:'FAIL',error:error.stack});console.error('FAIL '+item.name+'\n'+error.stack);}
  finally{await page.close();}
 }
}finally{await browser.close();}fs.writeFileSync(path.join(out,(process.env.OZL_LIFECYCLE_RUN_TAG||'current')+'-results.json'),JSON.stringify(results,null,2));const passed=results.filter(result=>result.status==='PASS').length;console.log(passed+'/'+results.length+' confirmation lifecycle groups passed');if(passed!==results.length)process.exitCode=1;
})().catch(error=>{console.error(error);process.exitCode=1;});
