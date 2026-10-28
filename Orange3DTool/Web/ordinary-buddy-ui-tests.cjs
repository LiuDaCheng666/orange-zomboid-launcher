/* Buddy is a dependency label, never evidence that installation is unnecessary. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const{setup,route,chromium}=require('./experience-ui-tests.cjs');
const out=path.resolve(__dirname,'../../artifacts/ordinary-buddy-ui-1239');fs.mkdirSync(out,{recursive:true});
const tests=[],test=(name,run)=>tests.push({name,run});
async function catalog(page){await page.evaluate(()=>{
 fx.catalog=[
  {id:'ZombieBuddy',name:'ZombieBuddy',requires:[],installationComponent:true,componentManaged:true},
  {id:'Rain',name:'天气模组',requires:['\\ZombieBuddy'],javaComponent:true,componentManaged:false},
  {id:'Cloud',name:'云层模组',requires:['Rain'],componentManaged:false},
  {id:'BuddyInName',name:'名字含 Buddy 的普通模组',requires:[],componentManaged:false},
  {id:'JarOnly',name:'只有 Java 文件的模组',requires:[],javaComponent:true,componentManaged:false}
 ];orange.state.modCatalog=null;orange.state.modProfiles=null;
});}
test('one normal mod list shows exact Buddy dependencies without classifying arbitrary JARs as components',async page=>{
 await catalog(page);await route(page,'mods');await page.waitForSelector('[data-mod-row="Rain"]');
 assert.equal(await page.locator('[data-mod-row="Rain"] [data-buddy-dependency]').innerText(),'Java 模组 · 依赖 Buddy');
 assert.equal(await page.locator('[data-mod-row="Cloud"] [data-buddy-dependency]').innerText(),'Java 模组 · 依赖 Buddy');
 assert.equal(await page.locator('[data-mod-row="JarOnly"] [data-buddy-dependency]').count(),0);
 assert.equal(await page.locator('[data-mod-row="BuddyInName"] [data-buddy-dependency]').count(),0);
 assert.equal(await page.locator('[data-mod-row="ZombieBuddy"]').count(),0);
 assert.equal(await page.locator('[data-page="javaMods"]').count(),0);
 await page.locator('[data-mod-membership="Rain"]').check();
 assert.equal(await page.locator('[data-mod-membership="Rain"]').isChecked(),true);
 assert.equal(await page.evaluate(()=>sent.some(row=>(row.payload?.action||row.action)==='javaComponentsEnable')),false);
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().endTime)).map(a=>a.finished.catch(()=>{}))));
 await page.screenshot({path:path.join(out,'normal-mod-list-buddy-tags.png')});
});
test('configuration editor can add a Buddy-dependent Java mod through its normal list once',async page=>{
 await catalog(page);await route(page,'home');await page.locator('[data-configuration-edit]').first().click();
 await page.waitForFunction(()=>orange.launchConfigurationUi.editor()?.catalog?.length===5);
 const row=page.locator('.configuration-source').filter({hasText:'天气模组'});
 assert.equal(await row.locator('[data-buddy-dependency]').innerText(),'Java 模组 · 依赖 Buddy');
 await row.locator('[data-configuration-add]').click();
 await page.locator('[data-configuration-mod="Rain"]').waitFor();
 assert.equal(await page.locator('[data-configuration-mod="Rain"] [data-buddy-dependency]').innerText(),'Java 模组 · 依赖 Buddy');
 assert.equal(await page.evaluate(()=>orange.launchConfigurationUi.editor().ids.filter(id=>id==='Rain').length),1);
});
test('old Java-page links open the ordinary mod list',async page=>{
 await catalog(page);await page.evaluate(()=>location.hash='javaMods');
 await page.waitForFunction(()=>document.querySelector('#page').dataset.route==='mods');
 assert.equal(await page.locator('#page').getAttribute('data-route'),'mods');
 assert.equal(await page.locator('.java-manager').count(),0);
 await page.waitForSelector('[data-mod-membership="Rain"]');
});
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true}),results=[];try{for(const item of tests){const{page,errors}=await setup(browser);try{await item.run(page);assert.deepEqual(errors,[]);results.push({name:item.name,status:'PASS'});console.log('PASS '+item.name);}catch(error){results.push({name:item.name,status:'FAIL',error:error.stack});console.error('FAIL '+item.name+'\n'+error.stack);}finally{await page.close();}}}finally{await browser.close();}fs.writeFileSync(path.join(out,(process.env.OZL_BUDDY_TEST_TAG||'current')+'-results.json'),JSON.stringify(results,null,2));if(results.some(row=>row.status!=='PASS'))process.exitCode=1;})().catch(error=>{console.error(error);process.exitCode=1;});
