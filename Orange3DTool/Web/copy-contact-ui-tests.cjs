const assert=require('node:assert/strict');
const {setup,route,routes,chromium}=require('./refresh-fixture.cjs');
const tests=[];
tests.push({name:'route headings omit procedural filler while preserving titles and controls',fn:async browser=>{
 const {page,errors}=await setup(browser);
 for(const name of routes){await route(page,name);if(name!=='home'){assert.equal(await page.locator('.heading h1').count(),1);assert.equal(await page.locator('.heading p').count(),0,name);}assert.ok(await page.locator('#page button').count());assert.doesNotMatch(await page.locator('#page').innerText(),/加载支持、全新视角|原作者标题、封面与依赖；|原作者封面与元数据；|汇总游戏与启动器日志，帮助定位问题|选择配置，开始游戏/);}
 assert.deepEqual(errors,[]);await page.close();
}});
tests.push({name:'help contact copies exact official group and joins through an empty native payload only on click',fn:async browser=>{
 const {page}=await setup(browser);await page.evaluate(()=>{window.copied=[];Object.defineProperty(navigator,'clipboard',{value:{writeText:async t=>copied.push(t)},configurable:true});});
 await route(page,'help');assert.equal(await page.evaluate(()=>sent.filter(m=>m.action==='openContact').length),0);
 assert.match(await page.locator('[data-contact]').innerText(),/1124858561/);
 await page.locator('[data-contact-copy]').click();assert.deepEqual(await page.evaluate(()=>copied),['1124858561']);
 await page.locator('[data-action="openContact"]').click();assert.deepEqual(await page.evaluate(()=>sent.filter(m=>m.action==='openContact').map(m=>m.payload)),[{}]);await page.close();
}});
tests.push({name:'missing covers use a compact accessible native icon and retain original image covers',fn:async browser=>{
 for(const theme of ['dark','light'])for(const width of [768,1440]){const {page}=await setup(browser,{theme});await page.setViewportSize({width,height:900});await route(page,'mods');assert.equal(await page.locator('.cover-missing[role="img"]').first().getAttribute('aria-label'),'暂无封面');assert.equal(await page.locator('.cover-missing').first().evaluate(e=>e.getBoundingClientRect().height<=72),true);await route(page,'components');assert.ok(await page.locator('.cover img').count());await page.close();}
}});
tests.push({name:'contact clipboard rejection displays manual copy feedback',fn:async browser=>{
 const {page}=await setup(browser);await page.evaluate(()=>Object.defineProperty(navigator,'clipboard',{value:{writeText:async()=>{throw new Error('denied');}},configurable:true}));
 await route(page,'help');await page.locator('[data-contact-copy]').click();
 assert.match(await page.locator('[data-contact]').innerText(),/手动复制.*1124858561/);await page.close();
}});
module.exports={tests};
if(require.main===module)(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});let failed=0;try{for(const t of tests)try{await t.fn(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} copy/contact groups passed`);if(failed)process.exitCode=1;})();
