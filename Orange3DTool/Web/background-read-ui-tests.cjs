/* Production DOM and request transport; native replies are isolated fixtures. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/background-read-ui-1239');fs.mkdirSync(out,{recursive:true});
const cases=[],test=(name,run)=>cases.push({name,run});
async function interceptDeadlines(page){await page.evaluate(()=>{
 const original=setTimeout;window.requestDeadlines=[];
 window.setTimeout=(callback,delay,...args)=>{const id=original(callback,delay,...args);if(delay>=60000)requestDeadlines.push({delay,expire:()=>{clearTimeout(id);callback(...args);}});return id;};
});}
test('a scoped Java read times out as a read, cancels its owner and settles',async page=>{
 await interceptDeadlines(page);
 await page.evaluate(()=>{
  fx.hold='javaComponentsList';
  window.readResult='pending';
  orange.request('configurationRequest',{id:'launcher',action:'javaComponentsList',readId:'a'.repeat(32),payload:{}})
   .then(()=>readResult='resolved',error=>readResult=error.message);
 });
 await page.waitForFunction(()=>held.some(row=>row.payload?.action==='javaComponentsList'));
 assert.equal(await page.evaluate(()=>requestDeadlines.at(-1).delay),600000,'a wrapped read must not inherit the two-hour mutation timeout');
 await page.evaluate(()=>requestDeadlines.at(-1).expire());
 await page.waitForFunction(()=>readResult!=='pending');
 assert.match(await page.evaluate(()=>readResult),/超时/);
 await page.waitForFunction(()=>sent.some(row=>row.action==='configurationReadCancel'&&row.payload.id==='launcher'&&row.payload.readId==='a'.repeat(32)));
 await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(replyNative);});
 assert.match(await page.evaluate(()=>readResult),/超时/,'late native replies cannot resurrect an expired request');
 assert.equal(await page.locator('#launch').isEnabled(),true);
});
test('a pending background thumbnail does not stop a server launch',async page=>{
 await page.evaluate(()=>fx.hold='serverThumb');await route(page,'servers');
 await page.waitForFunction(()=>held.some(row=>row.action==='serverThumb'));
 const button=page.locator('[data-profile-launch="server"]').first();
 assert.equal(await button.isEnabled(),true);
 await button.click();
 await page.waitForFunction(()=>sent.some(row=>row.action==='profileLaunch'));
 assert.equal(await page.evaluate(()=>sent.filter(row=>row.action==='profileLaunch').length),1);
});
test('status polling clears a completed native task instead of leaving all launch buttons disabled',async page=>{
 await page.evaluate(()=>orange.acceptStatus({...orange.state.status,task:{active:true,value:75,text:'正在安装组件'}}));
 assert.equal(await page.locator('#launch').isDisabled(),true);
 await page.evaluate(()=>pollCallbacks.find(callback=>String(callback).includes('pollPlaytime'))());
 await page.waitForFunction(()=>!orange.state.busy&&!orange.state.nativeBusy);
 assert.equal(await page.locator('#launch').isEnabled(),true);
 await route(page,'home');assert.equal(await page.locator('[data-configuration-edit]').first().isEnabled(),true);
});
test('an actual scoped write keeps its commit reply and never sends a read cancellation',async page=>{
 await interceptDeadlines(page);
 await page.evaluate(()=>{
  fx.hold='javaComponentsApply';window.writeResult='pending';
  orange.request('configurationRequest',{id:'launcher',action:'javaComponentsApply',payload:{previewStamp:'fixture',confirmed:true}})
   .then(()=>writeResult='committed',error=>writeResult=error.message);
 });
 await page.waitForFunction(()=>held.some(row=>row.payload?.action==='javaComponentsApply'));
 assert.equal(await page.evaluate(()=>requestDeadlines.at(-1).delay),7200000);
 await page.evaluate(()=>requestDeadlines.at(-1).expire());
 assert.equal(await page.evaluate(()=>writeResult),'pending');
 assert.equal(await page.evaluate(()=>sent.some(row=>row.action==='configurationReadCancel')),false);
 await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(replyNative);});
 await page.waitForFunction(()=>writeResult==='committed');
});
test('thumbnail errors have a compact retry and never insert task diagnostics into covers',async page=>{
 await page.evaluate(()=>{
  const original=chrome.webview.postMessage;
  chrome.webview.postMessage=message=>{if(['saveThumb','serverThumb'].includes(message.action)){
   sent.push(message);queueMicrotask(()=>emitNative({id:message.id,ok:false,error:'正在执行任务，请等待完成或取消。'}));
  }else original(message);};
 });
 await route(page,'servers');const cover=page.locator('[data-server-thumb="s1"]');
 await cover.locator('[data-thumb-retry]').waitFor();
 assert.doesNotMatch(await cover.innerText(),/正在执行任务|等待完成|取消/);
 assert.equal(await cover.locator('[data-thumb-retry]').getAttribute('aria-label'),'重新读取服务器封面');
 assert.equal(await cover.evaluate(element=>element.scrollHeight<=element.clientHeight+1),true);
 await page.evaluate(()=>fx.hold='');
 await page.locator('[data-thumb-retry]').click();
 assert.equal(await page.evaluate(()=>sent.filter(row=>row.action==='serverThumb').length)>=2,true);
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 await page.screenshot({path:path.join(out,'server-cover-failed-retry.png')});
});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];
 try{for(const item of cases){const{page,errors}=await setup(browser);try{await item.run(page);assert.deepEqual(errors,[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}catch(error){results.push({name:item.name,status:'FAIL',error:error.stack});console.error('FAIL '+item.name+'\n'+error.stack);}finally{await page.close();}}}
 finally{await browser.close();}fs.writeFileSync(path.join(out,(process.env.OZL_READ_TEST_TAG||'current')+'-results.json'),JSON.stringify(results,null,2));if(results.some(row=>row.status!=='PASS'))process.exitCode=1;
})().catch(error=>{console.error(error);process.exitCode=1;});
