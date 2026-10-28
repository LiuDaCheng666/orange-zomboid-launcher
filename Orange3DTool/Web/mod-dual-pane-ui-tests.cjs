const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const{setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/mod-dual-pane-1239');fs.mkdirSync(out,{recursive:true});
const cases=[],test=(name,run)=>cases.push({name,run});
async function populate(page){await page.evaluate(()=>{
 fx.catalog=Array.from({length:1000},(_,index)=>({id:'Mod'+String(index).padStart(4,'0'),name:'模组 '+String(index).padStart(4,'0'),author:'作者',componentManaged:false,requires:index===7?['ZombieBuddy']:[]}));
 fx.defaultMods=['Mod0001','Mod0007'];orange.state.modCatalog=null;orange.state.modProfiles=null;
});await route(page,'mods');await page.waitForSelector('[data-mod-available-pane]');}
test('available and enabled lists search independently without making the player scroll through 1000 entries',async page=>{
 await populate(page);
 assert.equal(await page.locator('#profile-mod-available [data-mod-row]').count(),1000);
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row]').count(),2);
 await page.locator('#profile-mod-search').fill('0050');
 assert.equal(await page.locator('#profile-mod-available [data-mod-row]').count(),1);
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row]').count(),2);
 await page.locator('#profile-enabled-search').fill('0007');
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row]').count(),1);
 await page.locator('#profile-mod-available [data-mod-membership="Mod0050"]').check();
 await page.locator('#profile-enabled-search').fill('');
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row]').count(),3);
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row="Mod0007"] [data-buddy-dependency]').innerText(),'Java 模组 · 依赖 Buddy');
 await page.locator('[data-profile-mod-remove="Mod0050"]').click();
 assert.equal(await page.locator('#profile-mod-catalog [data-mod-row]').count(),2);
 assert.equal(await page.locator('#profile-mod-available [data-mod-membership="Mod0050"]').isChecked(),false);
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 await page.screenshot({path:path.join(out,'dual-pane-1000-mods.png')});
});
test('reorder and Save use the selected list once and keep other navigation available',async page=>{
 await populate(page);await page.locator('[data-profile-mod-down="Mod0001"]').click();
 assert.deepEqual(await page.evaluate(()=>orange.state.modProfileMods),['Mod0007','Mod0001']);
 await page.locator('[data-mod-save-activate]').click();
 await page.waitForFunction(()=>fx.defaultMods[0]==='Mod0007');
 assert.deepEqual(await page.evaluate(()=>fx.defaultMods),['Mod0007','Mod0001']);
 assert.equal(await page.locator('[data-page="servers"]').isEnabled(),true);
});
test('configuration editor keeps available and enabled search separate, with Save always visible',async page=>{
 await populate(page);await route(page,'home');await page.locator('[data-configuration-edit]').first().click();
 await page.waitForFunction(()=>orange.launchConfigurationUi.editor()?.catalog?.length===1000);
 assert.equal(await page.locator('[data-configuration-tab="components"]').count(),0);
 await page.locator('[data-configuration-search]').fill('0050');
 assert.equal(await page.locator('[data-configuration-available] .configuration-source').count(),1);
 await page.locator('[data-configuration-available] [data-configuration-add]').click();
 await page.locator('[data-configuration-enabled-search]').fill('0050');
 assert.equal(await page.locator('[data-configuration-mods] [data-configuration-mod]').count(),1);
 assert.equal(await page.locator('[data-configuration-search]').inputValue(),'0050');
 const box=await page.locator('[data-configuration-save]').boundingBox();assert(box&&box.y+box.height<=768,'Save must remain in view');
 await page.screenshot({path:path.join(out,'configuration-dual-pane.png')});
 await page.locator('[data-configuration-close]').click();await page.locator('#confirm-yes').click();
 await page.waitForSelector('#configuration-editor',{state:'detached'});
 assert.equal(await page.locator('#launch').isEnabled(),true);
});
test('declared dependency chips update red, yellow and green after manual ordering and removal without disabling Save',async page=>{
 await page.evaluate(()=>{fx.catalog=[{id:'A',name:'基础模组A',requires:[]},{id:'C',name:'C模组',requires:['A','Missing']},{id:'D',name:'D模组',requires:['\\A']}];fx.defaultMods=['C','A','D'];orange.state.modCatalog=null;orange.state.modProfiles=null;});
 await route(page,'mods');await page.waitForSelector('[data-mod-enabled-pane]');
 const dependency=(id,dep)=>page.locator(`#profile-mod-catalog [data-mod-row="${id}"] [data-dependency-id="${dep}"]`);
 assert.equal(await dependency('C','A').getAttribute('data-dependency-state'),'late');
 assert.equal(await dependency('C','Missing').getAttribute('data-dependency-state'),'missing');
 assert.equal(await dependency('D','A').getAttribute('data-dependency-state'),'ready');
 await page.locator('[data-profile-mod-up="A"]').click();assert.equal(await dependency('C','A').getAttribute('data-dependency-state'),'ready');
 await page.locator('[data-profile-mod-remove="A"]').click();assert.equal(await dependency('D','A').getAttribute('data-dependency-state'),'missing');
 assert.equal(await page.locator('[data-mod-save-activate]').isEnabled(),true);assert.equal(await page.locator('[data-page="servers"]').isEnabled(),true);
 await page.screenshot({path:path.join(out,'dependency-states.png')});
});
test('desktop workspace dedicates space to at least eight complete rows per pane',async page=>{
 await page.evaluate(()=>{fx.catalog=Array.from({length:1000},(_,i)=>({id:'Mod'+String(i).padStart(4,'0'),name:'模组 '+String(i).padStart(4,'0'),requires:i%4?['Mod0000']:[]}));fx.defaultMods=fx.catalog.slice(0,40).map(m=>m.id);orange.state.modCatalog=null;orange.state.modProfiles=null;});
 await route(page,'mods');await page.waitForSelector('#profile-mod-catalog [data-mod-row]');
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 for(const selector of ['#profile-mod-catalog','#profile-mod-available']){const count=await page.locator(selector).evaluate(e=>{const box=e.getBoundingClientRect();return [...e.querySelectorAll(':scope>[data-mod-row]')].filter(r=>{const b=r.getBoundingClientRect();return b.top>=box.top&&b.bottom<=box.bottom;}).length;});assert(count>=8,`${selector} only shows ${count} complete rows`);}
 const save=await page.locator('[data-mod-save-activate]').boundingBox();assert(save.y+save.height<=768,'Save must stay visible');
 await page.screenshot({path:path.join(out,'full-height-1366.png')});await page.setViewportSize({width:1769,height:1170});await page.screenshot({path:path.join(out,'full-height-1769.png')});
});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];try{for(const item of cases){const{page,errors}=await setup(browser);try{await item.run(page);assert.deepEqual(errors,[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}catch(error){results.push({name:item.name,status:'FAIL',error:error.stack});console.error('FAIL '+item.name+'\n'+error.stack);}finally{await page.close();}}}finally{await browser.close();}fs.writeFileSync(path.join(out,(process.env.OZL_DUAL_TEST_TAG||'current')+'-results.json'),JSON.stringify(results,null,2));if(results.some(row=>row.status!=='PASS'))process.exitCode=1;})().catch(error=>{console.error(error);process.exitCode=1;});
