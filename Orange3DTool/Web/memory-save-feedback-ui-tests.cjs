const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const assert=require('node:assert/strict');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 for(const mode of ['own-save','failed-save','external-change']){
  const {page,errors}=await setup(browser);try{
   await route(page,'memory');await page.locator('#memory-number').fill('12');
   if(mode==='external-change'){
    await page.evaluate(()=>orange.acceptStatus({...orange.state.status,configStamp:'external-fixture-change'}));
    assert.equal(await page.evaluate(()=>orange.state.memoryDirty&&orange.state.memoryConflict),true);
    assert.match(await page.locator('#notice').innerText(),/外部配置已变化/);assert.equal(await page.locator('[data-save-memory]').isDisabled(),true);
   }else{
    await page.evaluate(mode=>{if(mode==='failed-save'){fx.failure='memorySave';return;}const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{if((m.action==='configurationRequest'?m.payload.action:m.action)==='memorySave')fx.configStamp='own-memory-write';original(m);};},mode);
    await page.locator('[data-save-memory]').click();
    if(mode==='own-save'){
     await page.waitForFunction(()=>orange.state.memory?.currentGiB===12&&!orange.state.busy&&!orange.state.memoryDirty&&!orange.state.memoryConflict);
     assert.doesNotMatch(await page.locator('#notice').innerText(),/外部配置已变化/);
    }else{
     await page.waitForFunction(()=>!orange.state.busy&&orange.state.notice.includes('查询失败'));
     assert.equal(await page.evaluate(()=>orange.state.memoryDirty&&fx.heap===6),true);assert.match(await page.locator('#notice').innerText(),/查询失败/);
    }
   }
   assert.deepEqual(errors,[]);console.log('PASS memory feedback '+mode);
  }finally{await page.close();}
 }
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
