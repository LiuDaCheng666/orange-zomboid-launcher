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
    if(m.action==='status'||m.action==='configurationRequest'&&m.payload.action==='status'){
     sent.push(m);
     const status={...orange.state.status,launch:{pending:recoveryPending},task:{active:false}};
     queueMicrotask(()=>emitNative({id:m.id,ok:true,data:m.action==='configurationRequest'?{configurationId:m.payload.id,data:status}:status}));
     return;
    }
    original(m);
   };
   orange.acceptStatus({...orange.state.status,launch:{pending:true}});
  });
  const recover=page.locator('[data-profile-recover]');
  await recover.waitFor();
  await recover.evaluate(button=>button.dataset.retained='same-button');
  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,launch:{pending:true,stage:'恢复需检查',warning:'旧连接记录内容不完整，请查看启动器日志。'}}));
  assert.match(await page.locator('[data-launch-recovery]').innerText(),/旧连接记录内容不完整/,'native recovery error is visible inside the existing recovery area');
  assert.equal(await recover.getAttribute('data-retained'),'same-button','updating a reason retains the action and focus target');
  const fs=require('node:fs'),path=require('node:path');
  const evidence=path.resolve(__dirname,'../artifacts/session-upgrade-fix');fs.mkdirSync(evidence,{recursive:true});
  await page.waitForTimeout(350);
  await page.screenshot({path:path.join(evidence,'recovery-error-dark.png')});
  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,launch:{pending:true,stage:'恢复需检查',warning:'第二次读取失败 <img src=x onerror=alert(1)>'}}));
  assert.match(await page.locator('[data-launch-recovery]').innerText(),/第二次读取失败 <img/,'later errors update as text');
  assert.equal(await page.locator('[data-launch-recovery] img').count(),0,'error text does not become markup');
  await page.evaluate(()=>orange.acceptStatus({...orange.state.status,launch:{pending:true,stage:'游戏主菜单已就绪。',warning:'正常连接提示'}}));
  assert.doesNotMatch(await page.locator('[data-launch-recovery]').innerText(),/第二次读取失败|正常连接提示/,'returning to an ordinary active record clears stale error text');
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
