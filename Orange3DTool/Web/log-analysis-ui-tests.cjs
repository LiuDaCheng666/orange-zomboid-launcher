/* Synthetic native boundary; no original player logs or filesystem operations. */
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
const hostile='<img src=x onerror="window.injected=true">';
const report={origin:'imported',state:'entered-with-issues',stage:'runtime',summary:'合成测试：已进入游戏，但记录了运行错误。',incomplete:true,fileCount:2,errorCount:3,warningCount:1,bytesRead:4096,checkedUtc:'2026-10-25T08:00:00Z',reportText:'脱敏测试报告\n运行错误 × 3',warnings:['片段不完整 '+hostile],issues:[{id:'one',type:'lua-null',severity:'error',impact:'runtime',title:'模组脚本读取了空值 '+hostile,meaning:'脚本访问的对象为空，相关功能可能中断。',advice:'核对相关模组版本，并逐个排查冲突。',phase:'runtime',attemptId:'a2',count:3,mods:['Unseen '+hostile],frames:[{mod:'Unseen '+hostile,file:'health.lua '+hostile,function:'update '+hostile,line:43}],evidence:[{source:'console '+hostile,line:120,text:'attempted index: health of non-table: null '+hostile}]}],attempts:[{id:'a1',sessionKey:'session '+hostile,state:'failed',stage:'world-loading',summary:'首次尝试失败',firstLine:1,lastLine:50},{id:'a2',sessionKey:'session2',state:'entered-with-issues',stage:'runtime',summary:'重试已进入游戏',firstLine:51,lastLine:130}],sources:[{id:'s1',name:'console '+hostile,kind:'imported',encoding:'UTF-8',status:'read',modifiedUtc:'2026-10-25T07:00:00Z',bytesRead:4096,truncated:true}]};
async function open(browser,theme='dark'){
 const result=await setup(browser,theme);const {page}=result;
 await page.evaluate(()=>{
  window.analysisReply={jobId:'',state:'idle',message:'',progress:0,report:null};window.analysisStart=null;window.analysisFailure='';window.analysisHeld=[];
  const post=chrome.webview.postMessage;
  chrome.webview.postMessage=m=>{
   if(!m.action.startsWith('logAnalysis'))return post(m);
   sent.push(m);let data=analysisReply;
   if(m.action==='logAnalysisExport')data={message:'脱敏报告已导出'};
   if(m.action==='logAnalysisCancel')data={...analysisReply,state:'cancelled',message:'分析已取消'};
   if(['logAnalysisLocal','logAnalysisImport','logAnalysisFolder'].includes(m.action))data=analysisStart||{jobId:'job1',state:'running',message:'正在读取日志',progress:26,report:null};
   const response={data:{id:m.id,ok:!analysisFailure,data,error:analysisFailure}};
   if(window.analysisHold===m.action)analysisHeld.push(response);else queueMicrotask(()=>reply(response));
  };
 });
 await route(page,'diagnostics');return result;
}
async function complete(page,value=report){await page.evaluate(r=>{analysisStart={jobId:'job1',state:'completed',progress:100,message:'完成',report:r};},value);await page.locator('[data-log-analysis-local]').click();await page.waitForSelector('[data-log-analysis-summary]');}
test('independent navigation and all three native intake actions keep payloads empty',async browser=>{
 const {page,errors}=await open(browser);assert.equal(await page.locator('nav > button[data-page="diagnostics"][aria-current="page"]').count(),1);
 assert.match(await page.locator('h1').innerText(),/日志分析/);assert.equal(await page.locator('#secondary-navigation [data-page]').count(),0);
 for(const [selector,action] of [['local','logAnalysisLocal'],['import','logAnalysisImport'],['folder','logAnalysisFolder']]){
  await page.evaluate(()=>analysisStart={cancelled:true});await page.locator('[data-log-analysis-'+selector+']').click();
  await page.waitForFunction(a=>sent.some(m=>m.action===a),action);assert.deepEqual(await page.evaluate(a=>sent.filter(m=>m.action===a).at(-1).payload,action),{});
 }
 assert.deepEqual(errors,[]);await page.close();
});
test('analysis polls outside its page, supports cancel and never blocks launching',async browser=>{
 const {page}=await open(browser);await page.locator('[data-log-analysis-local]').click();await page.waitForSelector('[data-log-analysis-cancel]');assert.equal(await page.locator('[data-log-analysis-local]').isDisabled(),true);assert.equal(await page.evaluate(()=>orange.state.busy),false);
 assert.equal(await page.locator('[data-log-analysis-progress]').getAttribute('value'),'26');
 await route(page,'memory');await page.evaluate(r=>{analysisReply={jobId:'job1',state:'completed',message:'完成',progress:100,report:r};pollCallbacks.forEach(f=>f());},report);
 await page.waitForFunction(()=>orange.state.logAnalysis?.state==='completed');await route(page,'diagnostics');assert.match(await page.locator('[data-log-analysis-summary]').innerText(),/已进入游戏/);
 await page.evaluate(()=>analysisStart=null);await page.locator('[data-log-analysis-local]').click();await page.locator('[data-log-analysis-cancel]').click();await page.waitForFunction(()=>sent.some(m=>m.action==='logAnalysisCancel'));assert.match(await page.locator('[data-log-analysis-status]').innerText(),/已取消/);await page.close();
});
test('report explains Chinese meaning with escaped evidence, mods, frames, attempts and sources',async browser=>{
 const {page,errors}=await open(browser);await complete(page);assert.match(await page.locator('[data-log-analysis-origin]').innerText(),/导入/);assert.match(await page.locator('[data-log-issue]').innerText(),/脚本访问的对象为空.*核对相关模组版本/s);
 assert.match(await page.locator('[data-log-issue]').innerText(),/3/);await page.locator('[data-log-evidence] > summary').click();assert.match(await page.locator('[data-log-evidence]').innerText(),/attempted index.*health.lua.*43/s);
 await page.locator('[data-log-sources] > summary').click();await page.locator('[data-log-attempts] > summary').click();assert.match(await page.locator('[data-log-sources]').innerText(),/UTF-8.*已截取/s);assert.match(await page.locator('[data-log-attempts]').innerText(),/首次尝试失败.*重试已进入游戏/s);
 assert.equal(await page.locator('#page img,#page svg,#page a[href^="javascript:"]').count(),0);assert.equal(await page.evaluate(()=>window.injected===true),false);assert.deepEqual(errors,[]);await page.close();
});
test('unknown and incomplete zero-detection reports never declare health; outcomes differ',async browser=>{
 const {page}=await open(browser);
 for(const [outcome,want] of [['unknown',/无法确认/],['failed',/失败/],['entered',/已进入游戏/],['disconnected',/连接.*断开/]]){
  await complete(page,{...report,origin:'local',state:outcome,summary:'',issues:[],errorCount:0,warningCount:0});const summary=await page.locator('[data-log-analysis-summary]').innerText();assert.match(summary,want);assert.doesNotMatch(summary,/一切正常|健康|没有问题/);assert.match(await page.locator('[data-log-analysis-empty]').innerText(),/无法.*排除|不代表/);
 }
 await page.close();
});
test('picker cancellation preserves report, errors hide exception secrets, copy/export use sanitized text',async browser=>{
 const {page}=await open(browser);await complete(page);await page.evaluate(()=>{analysisStart={cancelled:true};Object.defineProperty(navigator,'clipboard',{value:{writeText:async text=>window.copied=text},configurable:true});});
 for(const source of ['import','folder']){await page.locator('[data-log-analysis-'+source+']').click();assert.match(await page.locator('[data-log-analysis-summary]').innerText(),/已进入游戏/);assert.equal(await page.locator('[data-log-issue]').count(),1);}
 await page.locator('[data-log-analysis-copy]').click();assert.equal(await page.evaluate(()=>window.copied),report.reportText);await page.locator('[data-log-analysis-export]').click();assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='logAnalysisExport').payload),{});
 await page.evaluate(()=>analysisFailure='System.IO.Exception: C:\\Users\\Private\\secret.log token=SECRET');await page.locator('[data-log-analysis-folder]').click();await page.waitForSelector('[data-log-analysis-error]');assert.doesNotMatch(await page.locator('#page').innerText(),/Private|SECRET|System.IO/);assert.equal(await page.locator('[data-log-analysis-local]').isDisabled(),false);
 await route(page,'help');await page.locator('[data-log-analysis-entry]').click();await page.waitForFunction(()=>orange.state.route==='diagnostics');await page.close();
});
test('light/dark analysis remains readable at minimum size with details open',async browser=>{
 const shots=path.resolve(__dirname,'../../artifacts/log-analysis-ui');fs.mkdirSync(shots,{recursive:true});
 for(const theme of ['dark','light']){const {page,errors}=await open(browser,theme);await page.emulateMedia({reducedMotion:'reduce'});await complete(page);
  for(const viewport of [{width:1180,height:780},{width:820,height:600}]){await page.setViewportSize(viewport);await page.locator('[data-log-evidence]').evaluate(el=>el.open=true);await page.locator('[data-log-sources]').evaluate(el=>el.open=true);assert.equal(await page.locator('main').evaluate(el=>el.scrollWidth<=el.clientWidth),true);assert.equal(await page.locator('nav [data-page="help"]').evaluate(el=>el.getBoundingClientRect().bottom<=innerHeight),true);await page.screenshot({path:path.join(shots,`${theme}-${viewport.width}.png`)});}assert.deepEqual(errors,[]);await page.close();
 }
});
test('synthetic preview is explicitly labelled and never replaces live native analysis',async browser=>{
 const page=await browser.newPage({viewport:{width:1180,height:780}});
 const url='file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/');
 await page.goto(url+'?preview=1&analysisSample=1#diagnostics');await page.waitForSelector('[data-log-analysis-summary]');assert.match(await page.locator('.log-preview').innerText(),/合成示例数据/);assert.match(await page.locator('[data-log-analysis-origin]').innerText(),/示例/);assert.match(await page.locator('[data-log-issue]').first().innerText(),/这意味着什么.*建议下一步/s);await page.close();
});
test('a later successful status clears a transient polling error without losing the report',async browser=>{
 const {page}=await open(browser);await page.locator('[data-log-analysis-local]').click();await page.evaluate(()=>{analysisReply={jobId:'job1',state:'running',message:'正在读取日志',progress:26,report:null};analysisFailure='System.IO.Exception private';pollCallbacks.forEach(f=>f());});await page.waitForSelector('[data-log-analysis-error]');
 await page.evaluate(()=>{analysisFailure='';pollCallbacks.forEach(f=>f());});await page.waitForFunction(()=>!document.querySelector('[data-log-analysis-error]'));assert.equal(await page.locator('[data-log-analysis-cancel]').count(),1);await page.close();
});
test('native file pickers remain pending beyond the ordinary read timeout',async browser=>{
 const {page}=await open(browser);await page.evaluate(()=>{analysisHold='logAnalysisImport';const timer=window.setTimeout;window.setTimeout=(f,ms,...args)=>ms===60000?(queueMicrotask(f),0):timer(f,ms,...args);});
 await page.locator('[data-log-analysis-import]').click();await page.waitForFunction(()=>analysisHeld.length===1);assert.equal(await page.evaluate(()=>orange.state.logAnalysisPending),true,'the native dialog still owns the pending request');assert.equal(await page.locator('[data-log-analysis-error]').count(),0);
 await page.evaluate(()=>{reply(analysisHeld.shift());});await page.waitForSelector('[data-log-analysis-cancel]');await page.close();
});
test('changing the game source clears the old report and ignores an old picker reply',async browser=>{
 const {page}=await open(browser);await page.evaluate(()=>orange.acceptStatus({...orange.state.status,gamePath:'fixture-one'}));await complete(page);await page.evaluate(()=>{analysisHold='logAnalysisImport';analysisStart=null;});await page.locator('[data-log-analysis-import]').click();await page.waitForFunction(()=>analysisHeld.length===1);
 await page.evaluate(()=>orange.acceptStatus({...orange.state.status,gamePath:'fixture-two'}));assert.equal(await page.locator('[data-log-analysis-summary]').count(),0);assert.equal(await page.evaluate(()=>orange.state.logAnalysisPending),false);
 await page.evaluate(()=>reply(analysisHeld.shift()));await page.waitForTimeout(50);assert.equal(await page.locator('[data-log-analysis-cancel]').count(),0);assert.equal(await page.locator('[data-log-analysis-summary]').count(),0);await page.close();
});
module.exports={tests};
test('real analyzer stages are Chinese and evidence resolves source filenames',async browser=>{
 const {page}=await open(browser);const stages=[['runtime','\u6e38\u620f\u8fd0\u884c\u4e2d'],['world-loading','\u52a0\u8f7d\u4e16\u754c'],['connection','\u8fde\u63a5\u670d\u52a1\u5668'],['disconnection','\u8fde\u63a5\u65ad\u5f00']];
 for(const [phase,want]of stages){await complete(page,{...report,stage:phase,issues:[{...report.issues[0],phase,evidence:[{source:'s1',line:12,text:'unknown callback failed'}]}],sources:[{...report.sources[0],id:'s1',name:'player-console.txt'}]});assert.ok((await page.locator('.log-issue-context').innerText()).includes(want));await page.locator('[data-log-evidence]').click();assert.ok((await page.locator('.log-evidence').innerText()).includes('player-console.txt'));}
 await page.close();
});
test('completed result takes priority over intake instructions in a small window',async browser=>{const {page}=await open(browser);await page.setViewportSize({width:820,height:600});await complete(page);const bounds=await page.locator('[data-log-analysis-summary] h2').boundingBox();assert.ok(bounds.y+bounds.height<560,'result headline stays visible without scrolling past instructions');await page.close();});
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const {name,fn}of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} log analysis UI groups passed`);if(failed)process.exitCode=1;})();
