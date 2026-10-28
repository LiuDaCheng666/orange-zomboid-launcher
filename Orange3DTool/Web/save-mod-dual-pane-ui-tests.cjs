/* Production DOM and controls; native transport is synthetic. Real native file
 * writes are covered separately by tests/SaveModEditorNativeUi.cjs. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/save-mod-editor-1239');fs.mkdirSync(out,{recursive:true});
const tests=[],test=(name,run)=>tests.push({name,run}),available='#save-mod-available',enabled='#save-mod-catalog';
async function fixture(page,count=400){await page.evaluate(count=>{
 fx.catalog=Array.from({length:count},(_,i)=>({id:'SaveMod'+String(i).padStart(4,'0'),name:'存档资源与载具兼容模组 '+String(i).padStart(4,'0'),author:'工坊作者',description:'本地介绍预览',coverToken:i.toString(16).padStart(64,'0'),requires:i%4===1?['SaveMod0000']:i%4===2?['SaveMod0050']:i%4===3?['MissingDependency']:[],componentManaged:false}));
 fx.saveRows={'Sandbox/Test':fx.catalog.slice(0,123).map(m=>m.id),'Sandbox/Second':['SaveMod0002'],'Multiplayer/ReadOnly':['SaveMod0000']};
 fx.saves=Object.entries(fx.saveRows).map(([id,mods])=>({id,name:id.split('/')[1],mode:id.split('/')[0],editable:!id.startsWith('Multiplayer'),modCount:mods.length,modifiedUtc:'2026-10-30T00:00:00Z'}));
 fx.detailText='<RGB:1,1,1>作者白色正文<LINE>'+'完整介绍段落。'.repeat(10000)+'END-OF-FULL-LOCAL-DESCRIPTION';
 const base=chrome.webview.postMessage;chrome.webview.postMessage=m=>{
  const wrapped=m.action==='configurationRequest',action=wrapped?m.payload.action:m.action,p=wrapped?m.payload.payload:m.payload;
  if(!['saveGet','saveSave','modDetail'].includes(action))return base(m);sent.push(m);
  const reply=()=>{if(fx.failure===action){emitNative({id:m.id,ok:false,error:'隔离读取或保存失败'});return;}
   let data;if(action==='saveGet')data={id:p.id,name:p.id.split('/')[1],mode:p.id.split('/')[0],stamp:'save-'+p.id,enabled:[...fx.saveRows[p.id]],maps:['KeepMap'],requiresConfirmation:false,warnings:[]};
   else if(action==='saveSave'){fx.saveRows[p.id]=[...p.mods];data={message:'存档清单已保存'};}
   else data={token:p.token,id:fx.catalog.find(row=>row.coverToken===p.token)?.id,description:fx.detailText};
   emitNative({id:m.id,ok:true,data:wrapped?{configurationId:m.payload.id,data}:data});};
  if(fx.hold===action)held.push({message:m,reply});else queueMicrotask(reply);
 };
 orange.state.saves=null;orange.state.modCatalog=null;orange.state.modProfiles=null;
 },count);await route(page,'saves');await page.waitForSelector('[data-save-open="Sandbox/Test"]');}
async function open(page,id='Sandbox/Test'){await page.locator('[data-save-open="'+id+'"]').click();await page.waitForSelector('[data-save-mod-editor]');await page.waitForFunction(()=>!orange.state.loading.saveDetail);}
async function settle(page){await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));}
const ids=async(page,pane)=>page.locator(pane+' > [data-mod-row]').evaluateAll(rows=>rows.map(row=>row.dataset.modRow));

test('save opens a dense dedicated dual pane and preserves the original list maintenance entries',async page=>{
 await fixture(page);for(const selector of ['[data-save-delete="Sandbox/Test"]','[data-save-backups="Sandbox/Test"]','[data-environment-open="Sandbox/Test"]'])assert.equal(await page.locator(selector).count(),1);
 await open(page);assert.equal(await page.locator(available).count(),1);assert.equal(await page.locator(enabled).count(),1);assert.equal(await page.locator('.save-card,#mod-catalog,.save-editor>ol.enabled-mods').count(),0);
 assert.equal((await ids(page,enabled)).length,123);assert.equal((await ids(page,available)).length,400);assert.equal(await page.locator('[data-save-write]').isVisible(),true);
 const metrics=await page.evaluate(()=>{const q=s=>{const e=document.querySelector(s),r=e.getBoundingClientRect();return{complete:[...e.children].filter(row=>{const b=row.getBoundingClientRect();return b.top>=r.top&&b.bottom<=r.bottom;}).length,overflow:e.scrollWidth>e.clientWidth};};return{left:q('#save-mod-available'),right:q('#save-mod-catalog'),save:document.querySelector('[data-save-write]').getBoundingClientRect().bottom,viewport:innerHeight};});
 assert.ok(metrics.left.complete>=8&&metrics.right.complete>=8,JSON.stringify(metrics));assert.equal(metrics.left.overflow||metrics.right.overflow,false);assert.ok(metrics.save<=metrics.viewport);
 await settle(page);await page.screenshot({path:path.join(out,'save-editor-dark-1366.png')});
 await page.locator('[data-save-editor-back]').click();for(const selector of ['[data-save-delete="Sandbox/Test"]','[data-save-backups="Sandbox/Test"]','[data-environment-open="Sandbox/Test"]'])assert.equal(await page.locator(selector).count(),1);
});
test('independent search, membership and arrows edit only this save draft with live dependency colors',async page=>{
 await fixture(page);await open(page);await page.locator('#save-mod-search').fill('0399');await page.locator('#save-enabled-search').fill('0001');
 assert.deepEqual(await ids(page,available),['SaveMod0399']);assert.deepEqual(await ids(page,enabled),['SaveMod0001']);
 await page.locator('[data-save-mod-membership="SaveMod0399"]').check();assert.equal(await page.evaluate(()=>orange.state.saveMods.at(-1)),'SaveMod0399');assert.deepEqual(await page.evaluate(()=>fx.defaultMods),undefined);
 await page.locator('#save-enabled-search').fill('');await page.locator('#save-mod-catalog [data-mod-row="SaveMod0001"] [data-mod-up]').click();
 assert.deepEqual(await page.evaluate(()=>orange.state.saveMods.slice(0,2)),['SaveMod0001','SaveMod0000']);
 assert.equal(await page.locator('#save-mod-catalog [data-mod-row="SaveMod0001"] [data-dependency-id="SaveMod0000"]').getAttribute('data-dependency-state'),'late');
 assert.equal(await page.evaluate(()=>sent.some(m=>['saveSave','componentsAction','modProfileActivate'].includes(m.payload?.action||m.action))),false);
});
test('filtered pointer drag preserves hidden positions and both independent list scrolls',async page=>{
 await fixture(page);await open(page);await page.locator('#save-mod-search').fill('001');await page.locator('#save-enabled-search').fill('001');
 const before=await page.evaluate(()=>[...orange.state.saveMods]),from=await page.locator(enabled+' [data-mod-drag="SaveMod0010"]').boundingBox(),to=await page.locator(enabled+' [data-mod-row="SaveMod0015"]').boundingBox();
 await page.mouse.move(from.x+from.width/2,from.y+from.height/2);await page.mouse.down();await page.mouse.move(from.x+from.width/2,to.y+to.height-2,{steps:7});await page.locator('.mod-drag-ghost').waitFor();await page.mouse.up();await page.locator('.mod-drag-ghost').waitFor({state:'detached'});
 const after=await page.evaluate(()=>orange.state.saveMods);assert.ok(after.indexOf('SaveMod0010')>after.indexOf('SaveMod0015'));assert.equal(after.indexOf('SaveMod0009'),before.indexOf('SaveMod0009'));assert.equal(after.indexOf('SaveMod0020'),before.indexOf('SaveMod0020'));
 await page.locator('#save-mod-search').fill('');await page.locator('#save-enabled-search').fill('');
 await page.evaluate(()=>{document.querySelector('#save-mod-available').scrollTop=150;document.querySelector('#save-mod-catalog').scrollTop=500;});await page.locator('[data-save-sort]').click();await page.waitForFunction(()=>!orange.state.saveSorting);
 assert.equal(await page.locator(available).evaluate(e=>e.scrollTop),150);assert.equal(await page.locator(enabled).evaluate(e=>e.scrollTop),500);
});
test('return keeps the draft, another save requires one discard confirmation, and named profile is draft only',async page=>{
 await fixture(page);await open(page);await page.locator('[data-mod-remove="SaveMod0001"]').click();const draft=await page.evaluate(()=>[...orange.state.saveMods]);
 await page.locator('[data-save-editor-back]').click();await open(page);assert.deepEqual(await page.evaluate(()=>orange.state.saveMods),draft);
 await page.locator('[data-save-editor-back]').click();await page.locator('[data-save-open="Sandbox/Second"]').click();await page.locator('#confirm[open]').waitFor();await page.locator('#confirm-no').click();assert.equal(await page.locator('[data-save-mod-editor]').count(),0);
 await open(page);await page.locator('#save-profile-select').selectOption('p1');await page.locator('[data-save-profile-load]').click();await page.waitForFunction(()=>orange.state.saveMods.length===1&&orange.state.saveMods[0]==='base');
 assert.equal(await page.evaluate(()=>sent.some(m=>(m.payload?.action||m.action)==='saveSave')),false);
});
test('save confirmation cancellation and a failed save retain a usable draft and retry writes only the target',async page=>{
 await fixture(page);await open(page);await page.locator('[data-mod-remove="SaveMod0001"]').click();const draft=await page.evaluate(()=>[...orange.state.saveMods]);
 await page.locator('[data-save-write]').click();await page.locator('#confirm[open]').waitFor();await page.locator('#confirm-no').click();assert.equal(await page.evaluate(()=>sent.some(m=>(m.payload?.action||m.action)==='saveSave')),false);
 await page.evaluate(()=>fx.failure='saveSave');await page.locator('[data-save-write]').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.busy&&!!orange.state.saveSaveError);
 assert.equal(await page.locator('[data-save-write]').isEnabled(),true);assert.deepEqual(await page.evaluate(()=>orange.state.saveMods),draft);await settle(page);await page.screenshot({path:path.join(out,'save-editor-save-failure.png')});
 await page.evaluate(()=>fx.failure='');await page.locator('[data-save-write]').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.saveDirty&&!orange.state.busy);
 assert.deepEqual(await page.evaluate(()=>fx.saveRows['Sandbox/Test']),draft);assert.deepEqual(await page.evaluate(()=>fx.saveRows['Sandbox/Second']),['SaveMod0002']);assert.equal(await page.locator('[data-save-editor-back]').isEnabled(),true);
});
test('a held read shows cancel immediately and cancellation releases the page without discarding an existing draft',async page=>{
 await fixture(page);await page.evaluate(()=>fx.hold='saveGet');await page.locator('[data-save-open="Sandbox/Test"]').click();await page.locator('[data-save-read-cancel]').waitFor();
 assert.equal(await page.locator('[data-save-read-progress]').isVisible(),true);await page.locator('[data-save-read-cancel]').click();assert.equal(await page.locator('[data-save-open="Sandbox/Test"]').isEnabled(),true);
 await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(row=>row.reply?.());});await open(page);assert.equal(await page.locator('[data-save-editor-back]').isEnabled(),true);
});
test('a background save reader and save-only sorting do not lock ordinary workspace drag controls',async page=>{
 await fixture(page);await page.evaluate(()=>fx.hold='saveGet');await page.locator('[data-save-open="Sandbox/Test"]').click();await page.locator('[data-save-read-cancel]').waitFor();await page.evaluate(()=>orange.state.saveSorting=true);await route(page,'mods');await page.waitForSelector('[data-mod-drag-kind="profile"]');assert.ok(await page.locator('[data-mod-drag-kind="profile"]:enabled').count()>0);assert.equal(await page.evaluate(()=>orange.state.busy),false);await page.evaluate(()=>{orange.state.saveSorting=false;fx.hold='';held.splice(0).forEach(row=>row.reply?.());});
});

test('ordinary profile reads and sorting do not lock local save draft ordering',async page=>{
 await fixture(page);await open(page);await page.evaluate(()=>{orange.state.loading.modProfile=true;orange.state.modSorting=true;});await page.locator('#save-enabled-search').fill('000');assert.ok(await page.locator('[data-mod-drag-kind="save"]:enabled').count()>0);await page.evaluate(()=>{orange.state.loading.modProfile=false;orange.state.modSorting=false;});
});

test('pending save reads stay with their configuration and late A-B-A replies cannot open an obsolete editor',async page=>{
 await fixture(page);await page.evaluate(()=>fx.hold='saveGet');await page.locator('[data-save-open="Sandbox/Test"]').click();await page.locator('[data-save-read-cancel]').waitFor();await route(page,'home');await page.locator('#launch-configuration-select').selectOption('steam');await page.waitForFunction(()=>!orange.launchConfigurationUi.model.selecting);await route(page,'saves');assert.equal(await page.locator('[data-save-mod-editor]').count(),0);await page.locator('[data-save-open="Sandbox/Test"]').waitFor();await route(page,'home');await page.locator('#launch-configuration-select').selectOption('launcher');await page.waitForFunction(()=>!orange.launchConfigurationUi.model.selecting);await route(page,'saves');await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(row=>row.reply?.());});await page.waitForFunction(()=>!orange.state.loading.saveDetail);assert.equal(await page.locator('[data-save-mod-editor]').count(),0);assert.equal(await page.evaluate(()=>orange.state.saveDetail),null);
});

test('late save sort and named-profile replies after A-B-A keep the current draft and release local controls',async page=>{
 await fixture(page);await open(page);const draft=await page.evaluate(()=>[...orange.state.saveMods]);
 const cycle=async()=>{await route(page,'home');await page.locator('#launch-configuration-select').selectOption('steam');await page.waitForFunction(()=>!orange.launchConfigurationUi.model.selecting);await page.locator('#launch-configuration-select').selectOption('launcher');await page.waitForFunction(()=>!orange.launchConfigurationUi.model.selecting);await route(page,'saves');};
 await page.evaluate(()=>{fx.hold='modSort';fx.sort={mods:[...orange.state.saveMods].reverse(),errors:[],warnings:[],violations:[],cycles:[]};});await page.locator('[data-save-sort]').click();await page.waitForFunction(()=>held.length>0);await cycle();await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(row=>row.reply?row.reply():replyNative(row));});await page.waitForFunction(()=>!orange.state.saveSorting);assert.deepEqual(await page.evaluate(()=>orange.state.saveMods),draft);assert.equal(await page.locator('[data-save-sort]').isEnabled(),true);assert.equal(await page.locator('[data-save-feedback]').textContent(),'');
 await page.evaluate(()=>fx.hold='modProfileGet');await page.locator('#save-profile-select').selectOption('p1');await page.locator('[data-save-profile-load]').click();await page.waitForFunction(()=>held.length>0);await cycle();await page.evaluate(()=>{fx.hold='';held.splice(0).forEach(row=>row.reply?row.reply():replyNative(row));});await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));assert.deepEqual(await page.evaluate(()=>orange.state.saveMods),draft);assert.equal(await page.locator('[data-save-profile-load]').isEnabled(),true);
});

test('successful explicit reread restores its cached list row and clears only that save warning',async page=>{
 await fixture(page);await open(page);await page.evaluate(()=>{orange.state.saves={...orange.state.saves,saves:orange.state.saves.saves.filter(row=>row.id!=='Sandbox/Test'),warnings:['Sandbox/Test：之前的配置格式无效','Sandbox/Other：保留其他读取问题']};});await page.locator('[data-save-reread]').click();await page.waitForFunction(()=>!orange.state.loading.saveDetail);await page.locator('[data-save-editor-back]').click();assert.equal(await page.locator('[data-save-open="Sandbox/Test"]').count(),1);assert.equal(await page.getByText('Sandbox/Test：之前的配置格式无效',{exact:true}).count(),0);assert.equal(await page.getByText('Sandbox/Other：保留其他读取问题',{exact:true}).count(),1);
});

test('multiplayer remains read only and author RGB cannot make local introductions invisible in light theme',async page=>{
 await fixture(page);await open(page,'Multiplayer/ReadOnly');assert.equal(await page.locator('[data-save-write]').isDisabled(),true);assert.equal(await page.locator('[data-save-mod-membership]:enabled').count(),0);assert.equal(await page.locator('[data-mod-drag]:enabled').count(),0);
 await page.locator('[data-save-editor-back]').click();await open(page);await page.evaluate(()=>{document.documentElement.classList.add('light');orange.state.status.theme='light';});await page.locator('#save-mod-search').fill('0000');
 const details=page.locator(available+' [data-mod-row="SaveMod0000"] details');await details.locator('summary').click();await page.waitForFunction(()=>document.querySelector('#save-mod-available [data-mod-detail-description]')?.textContent.includes('END-OF-FULL-LOCAL-DESCRIPTION'));
 const colors=await details.locator('[data-mod-detail-description]').evaluate(e=>({text:getComputedStyle(e).color,span:getComputedStyle(e.querySelector('span')).color,tail:e.textContent.includes('END-OF-FULL-LOCAL-DESCRIPTION')}));assert.equal(colors.span,colors.text);assert.notEqual(colors.span,'rgb(255, 255, 255)');assert.equal(colors.tail,true);
 await page.setViewportSize({width:1000,height:768});await settle(page);await page.screenshot({path:path.join(out,'save-editor-light-details-1000.png')});assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth>e.clientWidth),false);
});

(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];try{for(const item of tests.filter(t=>!process.env.OZL_SAVE_TEST_FILTER||t.name.includes(process.env.OZL_SAVE_TEST_FILTER))){const{page,errors}=await setup(browser);try{await item.run(page);assert.deepEqual(errors,[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}catch(e){results.push({name:item.name,status:'FAIL',error:e.stack});console.error('FAIL '+item.name+'\n'+e.stack);await page.screenshot({path:path.join(out,'failure-'+results.length+'.png')}).catch(()=>{});}finally{await page.close();}}}finally{await browser.close();}fs.writeFileSync(path.join(out,'ui-results.json'),JSON.stringify(results,null,2));console.log(results.filter(r=>r.status==='PASS').length+'/'+results.length+' save-mod DOM groups passed');if(results.some(r=>r.status==='FAIL'))process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1;});
