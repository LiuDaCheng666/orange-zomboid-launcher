/* Actual launcher DOM, original local author covers, synthetic Chinese native data. */
const {setup,route,routes,chromium}=require('./refresh-fixture.cjs');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const folder=path.resolve(__dirname,'../../artifacts/product-refresh-ui');
const assets=path.resolve(__dirname,'../../website/assets');
(async()=>{fs.mkdirSync(folder,{recursive:true});fs.mkdirSync(assets,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});try{
 for(const theme of ['dark','light']){
  const {page,errors}=await setup(browser,{theme,capture:true});
  for(const width of [1440,1280,768]){
   await page.setViewportSize({width,height:900});
   for(const name of routes){
    await route(page,name);await page.waitForFunction(()=>!Object.values(orange.state.loading).some(Boolean));
    if(name==='servers'){await page.locator('[data-server-query="demo"]').click();await page.waitForFunction(()=>orange.state.serverQueries.demo?.status==='online');}
    await page.evaluate(async()=>{document.querySelector('main').scrollTo(0,0);await document.fonts.ready;await Promise.all(document.getAnimations().filter(a=>a.effect?.getTiming().iterations!==Infinity).map(a=>a.finished.catch(()=>{})));});await page.waitForTimeout(220);
    assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true,`${theme} ${width} ${name} overflow`);
    const dest=path.join(folder,`${theme}-${name}-${width}.png`);await page.screenshot({path:dest});
    if(width===1440&&['home','components','mods','servers','maintenance'].includes(name))fs.copyFileSync(dest,path.join(assets,`launcher-${name}-${theme}.png`));
   }
  }
  await page.setViewportSize({width:1280,height:900});await route(page,'components');await page.locator('[data-optional-components]>summary').click();await page.locator('[data-optional-components]').scrollIntoViewIfNeeded();await page.screenshot({path:path.join(folder,`${theme}-optional-expanded-1280.png`)});
  assert.deepEqual(errors,[]);await page.close();
  const notice=await setup(browser,{theme,capture:true,update:'Available',missing:true});await notice.page.setViewportSize({width:768,height:900});await notice.page.locator('#startup-update-notice').waitFor();await notice.page.waitForTimeout(220);await notice.page.screenshot({path:path.join(folder,`${theme}-startup-missing-768.png`)});await notice.page.close();
 }
 // Contact sheets retain every captured route for composition review; originals remain available for text QA.
 for(const theme of ['dark','light'])for(const width of [1440,1280,768]){
  const sheet=await browser.newPage({viewport:{width:1440,height:1560}});
  await sheet.setContent('<body style="margin:0;background:#cbd5e1;font:14px sans-serif;display:grid;grid-template-columns:repeat(3,1fr);gap:8px">'+routes.map(name=>'<figure style="margin:0"><figcaption>'+theme+' '+name+' '+width+'</figcaption><img style="width:100%;height:360px;object-fit:contain;object-position:top;background:#dde4ed" src="data:image/png;base64,'+fs.readFileSync(path.join(folder,`${theme}-${name}-${width}.png`)).toString('base64')+'"></figure>').join('')+'</body>');
  await sheet.screenshot({path:path.join(folder,`contact-${theme}-${width}.png`),fullPage:true});await sheet.close();
 }
 console.log('PASS 66 route/theme/width screenshots; 10 sanitized new launcher images exported; 6 contact sheets.');
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
