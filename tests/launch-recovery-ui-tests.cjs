const {setup,route,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
const assert=require('node:assert/strict');

(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const {page,errors}=await setup(browser);
 try{
  await route(page,'maintenance');
  await page.evaluate(()=>{
   const original=chrome.webview.postMessage;
   window.recoveryPending=true;
   chrome.webview.postMessage=m=>{
    if(m.action==='profileRecover'){sent.push(m);held.push(m);return;}
    if(m.action==='status'){
     sent.push(m);
     queueMicrotask(()=>emitNative({id:m.id,ok:true,data:{...orange.state.status,launch:{pending:recoveryPending},task:{active:false}}}));
     return;
    }
    original(m);
   };
   orange.acceptStatus({...orange.state.status,launch:{pending:true}});
  });
  const recover=page.locator('[data-profile-recover]');
  await recover.waitFor();
  assert.equal(await recover.evaluate(e=>{const r=e.getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight;}),true,'required recovery is visible without scrolling');
  await recover.click();
  await page.waitForFunction(()=>held.some(m=>m.action==='profileRecover'));
  assert.equal(await recover.isDisabled(),true,'recovery is disabled while native cleanup is running');
  await page.evaluate(()=>{
   document.querySelector('[data-profile-recover]').dispatchEvent(new MouseEvent('click',{bubbles:true}));
   recoveryPending=false;
   const m=held.shift();emitNative({id:m.id,ok:true,data:{message:'启动状态已修复'}});
  });
  await page.waitForFunction(()=>orange.state.status.launch?.pending===false&&!orange.state.busy);
  assert.equal(await recover.count(),0,'successful cleanup removes recovery action immediately');
  assert.doesNotMatch(await page.locator('#page').innerText(),/上次启动清理未完成/);
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='profileRecover').length),1,'duplicate click sends no second cleanup');

  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,launch:{pending:true}}));
  await recover.waitFor();
  await recover.click();
  await page.waitForFunction(()=>held.length>0);
  await page.evaluate(()=>{recoveryPending=true;const m=held.shift();emitNative({id:m.id,ok:false,error:'文件正在被占用，请稍后重试'});});
  await page.waitForFunction(()=>!orange.state.busy);
  assert.equal(await recover.isEnabled(),true,'failed cleanup retains a retry action');
  assert.match(await page.locator('#notice').innerText(),/文件正在被占用/);

  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:true,launch:{pending:true}}));
  assert.equal(await recover.count(),0,'live game never offers ended-session cleanup');
  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:false,launch:{pending:false}}));
  assert.equal(await recover.count(),0,'passive status also clears the old action');
  assert.deepEqual(errors,[]);
  console.log('PASS launch recovery busy, completion, retry, live-game and passive-refresh DOM contracts');
 }finally{await page.close();await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
