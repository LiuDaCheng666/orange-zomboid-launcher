const {chromium}=require('./playwright-runtime.cjs');
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const page=await browser.newPage({viewport:{width:820,height:600}});
 await page.addInitScript(()=>{window.sent=[];window.reply=null;window.revision=1;window.chrome={webview:{addEventListener:(e,f)=>reply=f,postMessage:m=>{sent.push(m);let data;if(m.action==='status')data={validGame:true,theme:'dark',components:[],task:{active:true,text:'Blocked tool operation',value:0}};else if(m.action==='coreUpdateStatus')data={protocol:1,revision:1,state:'Idle',currentVersion:'1.1.0'};else if(m.action==='coreUpdateCheck')data={protocol:1,revision:++revision,state:'Available',currentVersion:'1.1.0',latestVersion:'1.1.1',notes:'Verified notes <script>bad</script>',checkedUtc:'2026-10-13T10:00:00Z'};else if(m.action==='coreUpdateDownload')data={protocol:1,revision:++revision,state:'Downloading',currentVersion:'1.1.0',latestVersion:'1.1.1',receivedBytes:3,totalBytes:6,currentFile:'Web/app.js',speedBytes:2};else data={};queueMicrotask(()=>reply({data:{id:m.id,ok:true,data}}));}}};});
 await page.goto('file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/'));
 await page.getByRole('button',{name:'启动器更新',exact:true}).click();
 await page.getByRole('button',{name:'检查启动器更新',exact:true}).click();
 await page.getByText('可更新至 1.1.1',{exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'下载启动器更新',exact:true}).isEnabled(),true,'blocked controller leaves update lane available');
 assert.match(await page.locator('#core-update-panel').innerText(),/1.1.0/);
 await page.evaluate(()=>{const send=chrome.webview.postMessage;window.oldRequest=null;chrome.webview.postMessage=m=>{if(m.action==='coreUpdateCheck'&&!oldRequest){oldRequest=m;return;}send(m);};});
 await page.getByRole('button',{name:'检查启动器更新',exact:true}).click();
 await page.getByRole('button',{name:'检查启动器更新',exact:true}).click();
 await page.evaluate(()=>reply({data:{id:oldRequest.id,ok:false,error:'stale earlier request failed'}}));
 await page.waitForTimeout(50);
 assert.doesNotMatch(await page.locator('#core-update-panel').innerText(),/stale earlier request failed/,'old rejected request cannot obscure newer success');
 await page.getByRole('button',{name:'下载启动器更新',exact:true}).click();
 await page.getByText('Web/app.js',{exact:true}).waitFor();
 await page.evaluate(()=>reply({data:{event:'coreUpdateProgress',data:{protocol:1,revision:1,state:'Available',currentVersion:'1.1.0',latestVersion:'9.9.9'}}}));
 assert.doesNotMatch(await page.locator('#core-update-panel').innerText(),/9.9.9/,'stale native revision cannot replace current update');
 await page.evaluate(()=>{orange.state.memoryDirty=true;reply({data:{event:'coreUpdateProgress',data:{protocol:1,revision:10,state:'Ready',currentVersion:'1.1.0',latestVersion:'1.1.1',totalBytes:6,receivedBytes:6}}});});
 await page.getByRole('button',{name:'重启并更新',exact:true}).click();assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='coreUpdateApply').length),0,'unsaved editor prevents native apply');
 assert.match(await page.locator('#notice').innerText(),/保存/);
 fs.mkdirSync(path.resolve(__dirname,'../../artifacts/core-update-shots'),{recursive:true});
 for(const theme of ['dark','light']){await page.evaluate(theme=>document.documentElement.classList.toggle('light',theme==='light'),theme);await page.evaluate(()=>document.querySelector('main').scrollTo(0,0));await page.waitForTimeout(220);assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);await page.screenshot({path:path.resolve(__dirname,'../../artifacts/core-update-shots/'+theme+'-820.png'),fullPage:true});}
 for(const [state,message,revision] of [['Succeeded','健康验证已完成',11],['RolledBack','Updated launcher did not confirm healthy startup.',12]]){await page.evaluate(({state,message,revision})=>reply({data:{event:'coreUpdateProgress',data:{protocol:1,revision,state,currentVersion:'1.1.0',message,history:[{TaskId:'abc',State:state,Version:'1.1.1',Message:message,UpdatedUtc:'2026-10-13T10:00:00Z'}]}}}),{state,message,revision});assert.match(await page.locator('#core-update-panel').innerText(),state==='Succeeded'?/更新成功/:/已回退/);assert.match(await page.locator('[data-core-history]').innerText(),new RegExp(message.replaceAll('.','\\.')));}
 await page.evaluate(()=>reply({data:{event:'coreUpdateProgress',data:{protocol:1,revision:13,state:'PendingHealth',currentVersion:'1.1.1',history:[{TaskId:'pending',State:'PendingHealth',Version:'1.1.1',Message:'',UpdatedUtc:'2026-10-13T10:00:00Z'}]}}}));assert.doesNotMatch(await page.locator('[data-core-history]').innerText(),/健康验证已完成/,'pending history cannot claim completed health');
 console.log('CORE UPDATE UI: PASS independent lane, native versions, stale events, drafts, both small themes, durable outcomes/history');
 }finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
