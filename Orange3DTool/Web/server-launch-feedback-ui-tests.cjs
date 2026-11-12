const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const output=path.resolve(process.env.OZL_TEST_ARTIFACT_ROOT||path.join(__dirname,'../../artifacts/server-launch-feedback'));fs.mkdirSync(output,{recursive:true});
const native=process.env.OZL_NATIVE_ERROR_FILE?JSON.parse(fs.readFileSync(process.env.OZL_NATIVE_ERROR_FILE,'utf8')):null;
const message=native?native.connectionModes.find(row=>row.mode==='force-local').error:'Fixture Models（PZVoxelStudioViewpoint）：当前游戏版本 42.22；作者要求最低 42.22.2。当前版本不在作者声明的兼容范围内。';
assert.ok(message.includes('PZVoxelStudioViewpoint')&&message.includes('42.22.2'));
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];try{
 for(const theme of ['dark','light'])for(const entry of ['personal','recommended']){
  const {page,errors}=await setup(browser,theme);page.setDefaultTimeout(4000);
  await page.evaluate(({message,entry})=>{const original=chrome.webview.postMessage;window.compatibilityFailure=true;chrome.webview.postMessage=m=>{
   const wrapped=m.action==='configurationRequest',action=wrapped?m.payload.action:m.action;
   const send=data=>{sent.push(m);queueMicrotask(()=>emitNative({id:m.id,ok:true,data:wrapped?{configurationId:m.payload.id,data}:data}));};
   if(entry==='recommended'&&action==='recommendedServers')return send({schema:1,revision:1,updatedUtc:new Date().toISOString(),servers:[{id:'a'.repeat(32),name:'推荐服务器',host:'example.invalid',port:16261,queryPort:0,description:'',cover:'',tags:[],order:0,useSteamRelay:false}]});
   if(entry==='recommended'&&action==='recommendedQuery')return send({id:'a'.repeat(32),status:'unknown',name:null,players:null,maxPlayers:null});
   if(entry==='recommended'&&action==='serverList')return send({stamp:'server-1',servers:[{id:'s1',name:'合成测试服务器',host:'example.invalid',port:16261,queryPort:0,username:'synthetic-user',hasUserPassword:false,hasServerPassword:false,useSteamRelay:false,description:''}]});
   if(m.action==='profileLaunch'&&compatibilityFailure){sent.push(m);queueMicrotask(()=>emitNative({id:m.id,ok:false,error:message,preflight:{canLaunch:false,message,checkedUtc:new Date().toISOString(),items:[{id:'mods',title:'服务器模组',status:'error',message,route:'mods',details:[]}]}}));return;}return original(m);
  };if(entry==='recommended'){orange.state.serverTab='recommended';orange.state.servers=null;}},{message,entry});
  await route(page,'servers');const connect=page.locator(entry==='recommended'?'[data-recommended-connect]':'[data-profile-launch="server"]').first();
  await connect.click();await page.locator('[data-server-components-choice="force-local"]').click();
  await page.waitForFunction(()=>sent.some(m=>m.action==='profileLaunch')&&!orange.state.launchAttempt);
  await page.waitForFunction(message=>!document.querySelector('#notice').hidden&&document.querySelector('#notice').textContent.includes(message),message);
  assert.equal(await page.evaluate(()=>orange.state.route),'servers');assert.equal(await connect.isEnabled(),true);
  assert.equal(await page.evaluate(()=>orange.state.pendingMutations),0);
  assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth>e.clientWidth+1),false);
  await page.screenshot({path:path.join(output,`failure-${theme}${entry==='recommended'?'-recommended':''}.png`),animations:'disabled'});
  await page.evaluate(()=>compatibilityFailure=false);await connect.click();await page.locator('[data-server-components-choice="follow-server"]').click();
  await page.waitForFunction(()=>sent.filter(m=>m.action==='profileLaunch').length===2&&!orange.state.launchAttempt);
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='profileLaunch')[1].payload.serverComponents),'follow-server');assert.deepEqual(errors,[]);
  results.push({theme,entry,visibleError:true,retry:true,routePreserved:true,jsErrors:errors});await page.close();
 }
}finally{await browser.close();}
fs.writeFileSync(path.join(output,'ui-regression.json'),JSON.stringify({scope:'Real Web DOM clicks with synthetic native failure/success. No game or foreground use.',results},null,2));console.log('PASS visible server launch refusal and retry in both themes');
})().catch(error=>{console.error(error);process.exitCode=1;});
