/* Real homepage/CSS and local artwork; native I/O uses the existing synthetic bridge. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const shots=path.resolve(__dirname,'../../artifacts/home-cover-122');
const stage=process.env.ORANGE_HOME_COVER_STAGE||'green';
const sizes=[{width:733,height:507},{width:1366,height:768},{width:1920,height:1080}];

async function homePage(browser,theme){
 const fixture=await setup(browser,theme);
 if(process.env.ORANGE_HOME_COVER_PAGE_URL){
  await fixture.page.goto(process.env.ORANGE_HOME_COVER_PAGE_URL);
  await fixture.page.waitForFunction(()=>!!orange.state.status);
 }
 await route(fixture.page,'home');
 await fixture.page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
 return fixture;
}

(async()=>{
 fs.mkdirSync(shots,{recursive:true});
 const browser=await chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});
 let failed=0,passed=0;
 try{
  for(const theme of ['dark','light']){
   const {page,errors}=await homePage(browser,theme);
   try{
    for(const size of sizes){
     const name=`${theme}-${size.width}x${size.height}`;
     try{
      await page.setViewportSize(size);
      await page.locator('main').evaluate(e=>e.scrollTop=0);
      await page.screenshot({path:path.join(shots,`${stage}-${name}.png`)});
      const cover=page.locator('[data-home-cover]');
      assert.equal(await cover.count(),1,'one homepage artwork banner must be present');
      assert.equal(await cover.isVisible(),true,'homepage artwork must be visible');
      const image=await cover.evaluate(async e=>{
       const css=getComputedStyle(e),match=css.backgroundImage.match(/url\(["']?(.*?)["']?\)/);
       if(!match)return null;
       const image=new Image();image.src=match[1];await image.decode();
       return {url:image.src,width:image.naturalWidth,height:image.naturalHeight,backgroundSize:css.backgroundSize};
      });
      assert.ok(image,'banner must paint the existing local home.png');
      assert.equal(new URL(image.url).protocol,'file:');
      assert.equal(path.basename(new URL(image.url).pathname),'home.png');
      assert.ok(image.width>0&&image.height>0,'local artwork must decode successfully');
      const bounds=await cover.boundingBox();
      assert.ok(bounds.height>=(size.width===733?270:400),`dominant photo height: ${bounds.height}`);
      const launchBounds=await page.locator('.play-launch').boundingBox();
      assert.ok(bounds.width>launchBounds.width&&bounds.x>=launchBounds.x+launchBounds.width,'photo dominates the right side of launch');
      assert.equal(await page.getByRole('button',{name:'启动游戏',exact:true}).count(),1,'homepage keeps a single launch button');
      assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true,'main must have no horizontal overflow');
      await page.locator('#play-profile-select').scrollIntoViewIfNeeded();
      assert.equal(await page.locator('#play-profile-select').isVisible(),true,'configuration remains reachable');
      await page.locator('#launch').scrollIntoViewIfNeeded();
      assert.equal(await page.locator('#launch').evaluate(e=>{const r=e.getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect();return r.top>=main.top&&r.bottom<=main.bottom;}),true,'single launch control remains reachable inside main');
      assert.deepEqual(errors,[]);
      passed++;console.log('PASS homepage cover '+name+' '+JSON.stringify({height:bounds.height,image}));
     }catch(e){failed++;console.error('FAIL homepage cover '+name+'\n'+e.stack);}
    }
    try{
     await page.locator('#play-profile-select').selectOption('a'.repeat(32));
     await page.waitForFunction(()=>fx.selected==='a'.repeat(32)&&!orange.state.playSelecting);
     assert.equal(await page.evaluate(()=>sent.some(m=>['playProfileApply','launch','profileLaunch','javaSave','componentsAction'].includes(m.action))),false,'profile selection cannot apply settings or launch');
     await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:true}));
     await page.waitForFunction(()=>orange.state.status.running);
     assert.equal(await page.locator('#launch').isDisabled(),true,'existing launch control is disabled while running');
     assert.equal(await page.getByRole('button',{name:'启动游戏',exact:true}).count(),1);
     assert.equal(await page.locator('#play-profile-select').isEnabled(),true);
     assert.equal(await page.locator('#launch-target-kind').isEnabled(),true);
     passed++;console.log('PASS homepage selection and running state '+theme);
    }catch(e){failed++;console.error('FAIL homepage selection and running state '+theme+'\n'+e.stack);}
   }finally{await page.close();}
  }
 }finally{await browser.close();}
 console.log(`${passed}/${passed+failed} homepage cover groups passed`);
 if(failed)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1;});
