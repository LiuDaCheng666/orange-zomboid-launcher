/* Production DOM/events; native approval responses are isolated fixtures, not Java execution. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const{setup,route,chromium}=require('./experience-ui-tests.cjs');
const output=path.resolve(__dirname,'../../artifacts/java-approval-target-1239');fs.mkdirSync(output,{recursive:true});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});const{page,errors}=await setup(browser);try{
 await route(page,'help');await page.waitForFunction(()=>!orange.state.loading.java);
 await page.evaluate(()=>{
  const hash='a'.repeat(64);window.approvalRows=[{id:'First',name:'第一个来源',path:'D:\\test\\First.jar',sha256:hash,approvalToken:'source:first',decision:null},{id:'Second',name:'第二个来源',path:'D:\\test\\Second.jar',sha256:hash,approvalToken:'source:second',decision:null}];
  const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{const wrapped=m.action==='configurationRequest',action=wrapped?m.payload.action:m.action,payload=wrapped?m.payload.payload:m.payload;if(!['javaList','javaSave'].includes(action))return original(m);sent.push(m);if(action==='javaSave'){const row=approvalRows.find(j=>j.approvalToken===payload.hash);if(row)row.decision=payload.decision;}const data={supported:true,stamp:'approval-current',jars:structuredClone(approvalRows),warnings:[]};queueMicrotask(()=>emitNative({id:m.id,ok:true,data:wrapped?{configurationId:m.payload.id,data}:data}));};
  orange.state.launchKind='save';orange.state.launchId='Sandbox/Test';orange.state.javaOpen=true;orange.render();
 });
 if(!await page.locator('#java-help').evaluate(e=>e.open))await page.locator('#java-help>summary').click();
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 await page.locator('#java-help [data-java-refresh]').click();
 await page.waitForFunction(()=>orange.state.java?.jars?.length===2);
 const first=page.locator('[data-java-allow="source:first"]');assert.equal(await first.count(),1,'source token, not common hash, must identify the file');
 const read=await page.evaluate(()=>sent.filter(m=>(m.payload?.action||m.action)==='javaList').at(-1));assert.equal((read.payload.payload||read.payload).saveId,'Sandbox/Test');
 await first.click();await page.locator('#confirm-yes').click();
 await page.waitForFunction(()=>orange.state.java?.jars?.[0]?.decision===true);
 assert.equal(await page.evaluate(()=>orange.state.java.jars[1].decision),null,'same bytes do not approve a different source');
 const write=await page.evaluate(()=>sent.filter(m=>(m.payload?.action||m.action)==='javaSave').at(-1));assert.equal((write.payload.payload||write.payload).hash,'source:first');assert.equal((write.payload.payload||write.payload).saveId,'Sandbox/Test');
 assert.deepEqual(errors,[]);await page.screenshot({path:path.join(output,'source-specific-approval.png')});fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({status:'PASS',scope:'Production DOM; synthetic native responses; no game launch'}));console.log('PASS source token and save target remain isolated through actual approval buttons');
 }catch(error){console.error(error);fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({status:'FAIL',error:error.stack}));process.exitCode=1;}finally{await page.close();await browser.close();}})().catch(error=>{console.error(error);process.exitCode=1;});
