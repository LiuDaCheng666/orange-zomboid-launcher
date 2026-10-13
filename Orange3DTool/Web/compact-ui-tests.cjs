/* Synthetic WebView transport only: no real game, account, network, or file changes. */
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const assert=require('node:assert/strict'),path=require('node:path'),fs=require('node:fs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
const shots=path.resolve(__dirname,'../../artifacts/compact-ui-shots');
const hashA='a'.repeat(64),hashB='b'.repeat(64),hashC='c'.repeat(64);
async function javaFixture(page){
  await page.evaluate(({hashA,hashB,hashC})=>{
    window.javaRows=[{id:'waiting-a',name:'Pending Buddy',author:'Claimed Author',workshopId:'123',path:'C:\\Fixture\\Long original path\\Buddy.jar',sha256:hashA,decision:null,warning:'Verify this file'},{id:'waiting-b',name:'Pending Extra',author:'Claimed Author',workshopId:'124',path:'C:\\Fixture\\Extra.jar',sha256:hashB,decision:null,warning:''},{id:'allowed',name:'Previously allowed',author:'Claimed Author',workshopId:'125',path:'C:\\Fixture\\Allowed.jar',sha256:hashC,decision:true,warning:''}];
    const original=chrome.webview.postMessage;
    chrome.webview.postMessage=m=>{
      if(m.action==='javaList'){sent.push(m);queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:{supported:true,stamp:'java-1',jars:javaRows,warnings:[]}}}));return;}
      if(m.action==='javaSave'){sent.push(m);window.javaRequest=m;return;}
      original(m);
    };
  },{hashA,hashB,hashC});
  await page.locator('[data-java-open]').first().click();
  await page.waitForSelector(`[data-java-allow="${hashA}"]`);
  assert.equal(await page.locator(`[data-java-allow="${hashA}"]`).evaluate(e=>{const r=e.getBoundingClientRect();return r.top>=77&&r.bottom<=innerHeight;}),true,'opening Java review brings the first undecided action into view');
}
async function populate(page){
  await page.evaluate(()=>{
    const components=Array.from({length:12},(_,i)=>({id:['base','chinese','models','shoot','shelves','surface','door','roofs','foraging','fishing','weather','trees'][i],item:['3809306528','3810637496','3810302175','3813032735','3815931973','3814398576','3812151347','3814178814','3814994364','3814560827','3813908957','3813172420'][i],title:['Project Viewpoint + Buddy','老余汉化 · JAVA','6244 3D Models','Shoot to Thrill','KK 动态货架','Surface Fix','Door Fix','Complete Roofs','Viewpoint 搜寻','Viewpoint 钓鱼','True Weathers','KK 季节树木'][i],description:'Original author description with full provenance retained.',optional:i>1,downloaded:i<5,configured:i<3,version:'42.1.8',sourceState:'same',runtimeState:'本次游戏加载待验证',needsReapply:i===4}));
    orange.acceptStatus({...orange.state.status,components,hardware:{gpu:'Fixture GPU',vramGiB:8,driver:'Fixture driver'}});
    orange.state.modCatalog={mods:Array.from({length:24},(_,i)=>({id:'mod-'+i,title:'Original mod '+String(i).padStart(2,'0'),author:'Original Author '+i,version:'42.'+i,source:'workshop',workshopId:String(1000+i),description:'Full original description '+i+' remains available in the selected detail.\nSecond paragraph.',cover:'',requires:i?['mod-0']:[],conflict:false,map:i===3})),warnings:[]};
    orange.state.modProfiles={stamp:'profiles-1',profiles:[{id:'p1',name:'Compact fixture',modCount:2,updated:'2026-10-13'}],activeMods:['mod-0']};
    orange.state.updates={items:[{item:'3813032735',state:'available'}]};
  });
}
test('hall batches render before final completion, deduplicate ids and ignore stale generations',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);
  await route(page,'servers');await page.evaluate(()=>window.holdAction='serverHall');await page.locator('[data-server-tab="hall"]').click();await page.waitForFunction(()=>held.length===1);
  assert.equal(await page.evaluate(()=>typeof orange.state.hallRequestId),'string','hall has an explicit client generation');
  assert.equal(await page.evaluate(()=>held[0].request.payload.requestId===orange.state.hallRequestId),true,'native request receives the same generation');
  await page.evaluate(()=>{reply({data:{event:'hallBatch',data:{requestId:'stale',servers:[{id:'stale',name:'Stale server',players:4,maxPlayers:32,ping:10}]}}});reply({data:{event:'hallBatch',data:{requestId:orange.state.hallRequestId,servers:[{id:'hall:first',name:'First valid server',host:'first.invalid',port:16261,players:4,maxPlayers:32,ping:22}]}}});});
  assert.match(await page.locator('#page').innerText(),/First valid server/,'first batch is visible while final promise is held');
  assert.doesNotMatch(await page.locator('#page').innerText(),/Stale server/);assert.equal(await page.evaluate(()=>orange.state.loading.serverHall),true);
  await page.evaluate(()=>reply({data:{event:'hallBatch',data:{requestId:orange.state.hallRequestId,servers:[{id:'hall:first',name:'Updated first server',host:'first.invalid',port:16261,players:8,maxPlayers:32,ping:20}]}}}));
  assert.equal(await page.locator('[data-hall-id="hall:first"]').count(),1);assert.match(await page.locator('[data-hall-id="hall:first"]').innerText(),/Updated first server/);
  await page.evaluate(()=>{held[0].response.data.data.servers=[{id:'hall:first',name:'Final first server',host:'first.invalid',port:16261,players:8,maxPlayers:32,ping:20}];reply(held[0].response);});await page.waitForFunction(()=>!orange.state.loading.serverHall);assert.match(await page.locator('#page').innerText(),/Final first server/);await page.close();
});
test('hall filters and sorts received rows locally with unknown latency last',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);await route(page,'servers');await page.locator('[data-server-tab="hall"]').click();await page.waitForSelector('[data-hall-add]');
  await page.evaluate(()=>{orange.state.serverHall={servers:[{id:'alpha',name:'Alpha',players:0,maxPlayers:32,ping:12,passwordRequired:false},{id:'beta',name:'Beta',players:14,maxPlayers:32,ping:80,passwordRequired:true},{id:'gamma',name:'Gamma',players:7,maxPlayers:32,ping:null,passwordRequired:false},{id:'delta',name:'Delta',players:4,maxPlayers:32,ping:26,passwordRequired:false}],message:'Synthetic listing'};});await route(page,'home');await route(page,'servers');
  const initial=await page.evaluate(()=>sent.filter(m=>m.action==='serverHall').length);
  await page.locator('#hall-sort').selectOption('players');assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['beta','gamma','delta','alpha']);
  await page.locator('#hall-sort').selectOption('ping');assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['alpha','delta','beta','gamma']);
  await page.locator('#hall-hide-empty').check();await page.locator('#hall-hide-password').check();assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['delta','gamma']);
  await page.locator('#hall-min-players').fill('5');assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['gamma']);
  await page.locator('#hall-min-players').fill('0');await page.locator('#hall-max-ping').fill('30');assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['delta']);
  await page.locator('#hall-query').fill('DEL');assert.deepEqual(await page.locator('[data-hall-id]').evaluateAll(es=>es.map(e=>e.dataset.hallId)),['delta']);
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='serverHall').length),initial,'local display controls do not issue native queries');await page.close();
});
test('cancelled hall generations cannot append into a refreshed listing',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);await route(page,'servers');await page.evaluate(()=>window.holdAction='serverHall');await page.locator('[data-server-tab="hall"]').click();await page.waitForFunction(()=>held.length===1);const cancelledId=await page.evaluate(()=>orange.state.hallRequestId);await page.locator('[data-hall-cancel]').click();
  assert.equal(await page.evaluate(()=>orange.state.loading.serverHall),false);assert.match(await page.locator('#page').innerText(),/已取消/);assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='cancel').payload),{scope:'hall',requestId:cancelledId},'hall cancellation targets its captured generation and cannot cancel file mutations');
  await page.locator('[data-server-source-refresh]').click();await page.waitForFunction(()=>held.length===2);await page.evaluate(()=>{reply({data:{event:'hallBatch',data:{requestId:held[0].request.payload.requestId,servers:[{id:'old',name:'Cancelled old batch'}]}}});reply({data:{event:'hallBatch',data:{requestId:held[1].request.payload.requestId,servers:[{id:'new',name:'Current batch',players:2,ping:10}]}}});held[0].response.data.data.servers=[{id:'old',name:'Cancelled old final'}];reply(held[0].response);});
  assert.match(await page.locator('#page').innerText(),/Current batch/);assert.doesNotMatch(await page.locator('#page').innerText(),/Cancelled old/);await page.close();
});
test('Java review collapses allowed hashes and publishes authoritative decisions before slow status',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);await javaFixture(page);
  assert.equal(await page.locator(`[data-java-allow="${hashC}"]`).count(),0,'allowed hash has no repeat allow action');assert.equal(await page.locator('.java-allowed').evaluate(e=>e.open),false,'allowed files start collapsed');
  await page.locator(`[data-java-allow="${hashA}"]`).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!!javaRequest);
  assert.equal(await page.locator(`[data-java-allow="${hashA}"]`).isDisabled(),true);assert.equal(await page.locator(`[data-java-allow="${hashB}"]`).isEnabled(),true,'other file remains reviewable');assert.equal(await page.locator('[data-java-refresh]').isEnabled(),true,'reading remains available');
  await page.evaluate(()=>{window.holdAction='status';javaRows=javaRows.map(j=>j.sha256===javaRequest.payload.hash?{...j,decision:true}:j);reply({data:{id:javaRequest.id,ok:true,data:{supported:true,stamp:'java-2',jars:javaRows,warnings:[]}}});});
  await page.waitForFunction(()=>orange.state.java.stamp==='java-2');assert.equal(await page.locator(`[data-java-allow="${hashA}"]`).count(),0);assert.equal(await page.locator('.java-allowed [data-java-state="allowed"]').count(),2,'authoritative result shown without status response');
  assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='javaSave').length),1,'no blanket or repeated grant');await page.locator('.java-allowed>summary').click();await page.locator('.java-allowed .java-evidence>summary').first().click();assert.match(await page.locator('.java-allowed').innerText(),/已允许此哈希/);assert.match(await page.locator('.java-allowed').innerText(),/a{64}/);await page.close();
});
test('failed Java decision remains pending with an actionable row and no optimistic grant',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);await javaFixture(page);await page.locator(`[data-java-allow="${hashA}"]`).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!!javaRequest);await page.evaluate(()=>reply({data:{id:javaRequest.id,ok:false,error:'Hash changed externally'}}));await page.waitForFunction(()=>orange.state.notice.includes('Hash changed'));
  assert.equal(await page.evaluate(()=>orange.state.java.jars[0].decision),null);assert.equal(await page.locator(`[data-java-allow="${hashA}"]`).isEnabled(),true);await page.close();
});
test('queued exact-file approvals serialize writes and use the latest authoritative snapshot',async browser=>{
  const {page}=await setup(browser);await javaFixture(page);await page.locator(`[data-java-allow="${hashA}"]`).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!!javaRequest);await page.locator(`[data-java-allow="${hashB}"]`).click();await page.locator('#confirm-yes').click();assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='javaSave').length),1,'second write waits for the first');
  await page.evaluate(()=>{javaRows=javaRows.map(j=>j.sha256===javaRequest.payload.hash?{...j,decision:true}:j);reply({data:{id:javaRequest.id,ok:true,data:{supported:true,stamp:'java-2',jars:javaRows,warnings:[]}}});});await page.waitForFunction(()=>sent.filter(m=>m.action==='javaSave').length===2);assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='javaSave')[1].payload),{stamp:'java-2',hash:hashB,decision:true});await page.close();
});
test('a Java list read started before a successful decision cannot overwrite its authoritative snapshot',async browser=>{
  const {page}=await setup(browser);page.setDefaultTimeout(2000);await javaFixture(page);await page.evaluate(()=>{const original=chrome.webview.postMessage;chrome.webview.postMessage=m=>{if(m.action==='javaList'){sent.push(m);window.oldJavaRead=m;return;}original(m);};});await page.locator('[data-java-refresh]').click();await page.waitForFunction(()=>!!oldJavaRead);await page.locator(`[data-java-allow="${hashA}"]`).click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!!javaRequest);
  await page.evaluate(()=>{const oldRows=javaRows;javaRows=javaRows.map(j=>j.sha256===javaRequest.payload.hash?{...j,decision:true}:j);reply({data:{id:javaRequest.id,ok:true,data:{supported:true,stamp:'java-2',jars:javaRows,warnings:[]}}});window.oldJavaRows=oldRows;});await page.waitForFunction(()=>orange.state.java.stamp==='java-2');await page.evaluate(()=>reply({data:{id:oldJavaRead.id,ok:true,data:{supported:true,stamp:'java-1',jars:oldJavaRows,warnings:[]}}}));await page.waitForFunction(()=>!orange.state.loading.java);assert.equal(await page.evaluate(()=>orange.state.java.stamp),'java-2');assert.equal(await page.locator(`[data-java-allow="${hashA}"]`).count(),0);await page.close();
});
test('global mutation busy locks writes while navigation, local filters and reads remain usable',async browser=>{
  const {page}=await setup(browser);await route(page,'mods');await page.locator('[data-mod-profile-open="p1"]').click();await page.locator('#mod-profile-name').fill('Unsaved profile');await page.evaluate(()=>orange.acceptStatus({...orange.state.status,task:{active:true,text:'Synthetic install',value:-1}}));
  assert.equal(await page.locator('[data-mod-profile-save]').isDisabled(),true);assert.equal(await page.locator('#profile-mod-search').isEnabled(),true,'local filter is not a file mutation');assert.equal(await page.locator('[data-profiles-refresh]').isEnabled(),true,'local snapshot reads remain usable');await page.locator('#profile-mod-search').fill('Furniture');assert.equal(await page.locator('.catalog-card').count(),1);await route(page,'home');assert.equal(await page.locator('[data-java-open]').isEnabled(),true);await page.close();
});
test('a server editor opened during a mutation keeps its draft readable but disables its write action',async browser=>{
  const {page}=await setup(browser);await route(page,'servers');await page.waitForSelector('[data-server-edit="own"]');await page.evaluate(()=>orange.acceptStatus({...orange.state.status,task:{active:true,text:'Synthetic mutation',value:-1}}));await page.locator('[data-server-edit="own"]').click();assert.equal(await page.locator('#server-name').isEnabled(),true);assert.equal(await page.locator('[data-server-write]').isDisabled(),true);await page.close();
});
test('compact components and mod master/detail show dense real metadata while preserving drafts in both themes',async browser=>{
  fs.mkdirSync(shots,{recursive:true});
  for(const theme of ['dark','light']){
    const {page,errors}=await setup(browser,theme);await page.setViewportSize({width:1920,height:1080});await populate(page);await route(page,'home');assert.match(await page.locator('.home-grid').innerText(),/3.*12.*本次.*待验证/s,'home summary separates configured counts from runtime verification');await route(page,'components');
    const visible=await page.locator('[data-card]').evaluateAll(es=>es.filter(e=>{const r=e.getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight;}).length);assert.ok(visible>=6,'at least six complete compact cards visible, saw '+visible);
    assert.equal(await page.locator('[data-card] .cover img').first().evaluate(e=>getComputedStyle(e).objectFit),'contain','author image preserves its full ratio');assert.equal(await page.locator('[data-card="base"] .status-positive').count()>0,true);assert.equal(await page.locator('[data-card="shoot"] .status-negative').count()>0,true);assert.match(await page.locator('[data-card="base"]').innerText(),/本次游戏加载待验证/);await page.screenshot({path:path.join(shots,theme+'-components-populated.png')});
    await route(page,'mods');await page.locator('[data-mod-profile-open="p1"]').click();await page.locator('#mod-profile-name').fill('Preserved compact draft');await page.locator('[data-profile-mod-up="base"]').click();await page.locator('#profile-mod-search').fill('Original');await page.locator('.catalog-card').first().scrollIntoViewIfNeeded();
    await page.locator('.mod-workspace').evaluate(e=>e.scrollIntoView({block:'start'}));const modVisible=await page.locator('.catalog-card').evaluateAll(es=>es.filter(e=>{const r=e.getBoundingClientRect(),list=e.closest('.mod-list').getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect();return r.top>=Math.max(list.top,main.top)&&r.bottom<=Math.min(list.bottom,main.bottom,innerHeight);}).length);assert.ok(modVisible>=10,'ten or more complete mod rows visible in 1080 viewport, saw '+modVisible);
    await page.locator('[data-mod-select="mod-3"]').click();assert.match(await page.locator('.mod-detail').innerText(),/Full original description 3.*workshop.*mod-0/s);assert.equal(await page.locator('#mod-profile-name').inputValue(),'Preserved compact draft');assert.equal(await page.evaluate(()=>orange.state.modProfileDirty),true);assert.deepEqual(await page.evaluate(()=>orange.state.modProfileMods),['base','extra']);assert.equal(await page.locator('#profile-mod-search').inputValue(),'Original');await page.screenshot({path:path.join(shots,theme+'-mods-populated.png')});
    await page.locator('[data-profile-mod-add="mod-3"]').click();assert.deepEqual(await page.evaluate(()=>orange.state.modProfileMods),['base','extra','mod-3']);await page.evaluate(()=>orange.acceptStatus({...orange.state.status,configStamp:'external'}));assert.deepEqual(await page.evaluate(()=>orange.state.modProfileMods),['base','extra','mod-3']);assert.deepEqual(errors,[]);await page.close();
  }
});
test('populated Java review and streamed hall remain contained in both themes',async browser=>{
  fs.mkdirSync(shots,{recursive:true});for(const theme of ['dark','light']){const {page,errors}=await setup(browser,theme);await page.setViewportSize({width:1100,height:760});await populate(page);await route(page,'home');assert.equal(await page.locator('[data-java-open]').evaluate(e=>e.getBoundingClientRect().bottom<=innerHeight),true,'home puts Java approval beside the other summaries within the desktop viewport');await page.screenshot({path:path.join(shots,theme+'-home-populated.png')});await javaFixture(page);assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true);await page.screenshot({path:path.join(shots,theme+'-java-populated.png')});await route(page,'servers');await page.evaluate(()=>window.holdAction='serverHall');await page.locator('[data-server-tab="hall"]').click();await page.waitForFunction(()=>held.length===1);await page.evaluate(()=>reply({data:{event:'hallBatch',data:{requestId:orange.state.hallRequestId,servers:Array.from({length:9},(_,i)=>({id:'hall:'+i,name:'Public fixture '+i,host:'fixture.invalid',port:16261,queryPort:16262,players:12-i,maxPlayers:32,ping:i===8?null:12+i*11,passwordRequired:i===2,description:'Original server description',steam:true}))}}}));assert.equal(await page.locator('[data-hall-id]').count(),9);await page.screenshot({path:path.join(shots,theme+'-hall-streaming.png')});assert.deepEqual(errors,[]);await page.close();}
});
module.exports={tests,javaFixture,populate,shots};
if(require.main===module)(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const {name,fn}of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} compact UI test groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1});
