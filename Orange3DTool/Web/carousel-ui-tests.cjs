/* Real packaged HTML, image bytes, DOM and lifecycle; only native I/O is synthetic. */
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),cp=require('node:child_process');
const {setup,route,chromium}=require('./experience-ui-tests.cjs');
const root=path.resolve(__dirname,'../..'),stage=path.join(root,'artifacts/cover-gallery-fixture');
fs.mkdirSync(stage,{recursive:true});
for(const f of ['index.html','app.js','app.css','home.png','orange-app.ico'])fs.copyFileSync(path.join(__dirname,f),path.join(stage,f));
if(fs.existsSync(path.join(root,'Build-CoverGallery.ps1')))cp.execFileSync('powershell.exe',['-NoProfile','-File',path.join(root,'Build-CoverGallery.ps1'),'-OutputHtml',path.join(stage,'index.html')]);
const url='file:///'+path.join(stage,'index.html').replaceAll('\\','/'),tests=[];
const test=(name,run)=>tests.push({name,run}),id=p=>p.locator('[data-home-cover]').getAttribute('data-scene-id');
async function fixture(b,{theme='dark',reduced=false,pref=null}={}){
 const f=await setup(b,theme),p=f.page;
 await p.emulateMedia({reducedMotion:reduced?'reduce':'no-preference'});
 await p.addInitScript(pref=>{if(!sessionStorage.getItem('galleryInitialized')){localStorage.removeItem('orange.homeScene');if(pref!==null)localStorage.setItem('orange.homeScene',pref);sessionStorage.setItem('galleryInitialized','true');}},pref);
 const time=new Date('2026-10-15T00:00:00Z');await p.clock.install({time});await p.clock.pauseAt(new Date(time.getTime()+1000));await p.goto(url);await p.waitForFunction(()=>!!orange.state.status&&!Object.keys(orange.state.experienceReads).length);await p.mouse.move(0,0);return f;
}
async function tick(p,ms=8000){await p.clock.fastForward(ms);await p.waitForTimeout(30);}
async function holdCrossroadsDecode(p){
 // Preserve real image decoding, delaying only its observable completion.
 await p.evaluate(()=>{const decode=HTMLImageElement.prototype.decode;HTMLImageElement.prototype.decode=function(){const decoded=decode.call(this);return this.src===JSON.parse(document.querySelector('#home-scene-data').textContent).crossroads?decoded.then(()=>new Promise(resolve=>{window.releaseSceneDecode=resolve;})):decoded;};});
}
test('packaged gallery decodes unchanged candidates and rotates at eight seconds without replacing launch or actual nodes',async b=>{
 const{page:p,errors}=await fixture(b);try{
  assert.equal(await id(p),'original');
  const resources=await p.locator('#home-scene-data').evaluate(e=>JSON.parse(e.textContent));
  assert.deepEqual(Object.keys(resources),['crossroads','riverside','safehouse']);
  for(const name of Object.keys(resources)){
   assert.deepEqual(Buffer.from(resources[name].split(',')[1],'base64'),fs.readFileSync(path.join(root,'docs/assets/cover-candidates',name+'.png')));
   assert.deepEqual(await p.evaluate(async src=>{const i=new Image();i.src=src;await i.decode();return[i.naturalWidth,i.naturalHeight];},resources[name]),[1672,941]);
  }
  await p.locator('#play-profile-select').selectOption('a'.repeat(32));await p.waitForFunction(()=>!orange.state.playSelecting);await p.locator('#launch-target-kind').focus();
  await p.evaluate(()=>{window.launchNode=document.querySelector('#launch');window.actualNode=document.querySelector('[data-home-actual]');window.focusNode=document.activeElement;window.actualRect=actualNode.getBoundingClientRect().toJSON();window.actualText=actualNode.textContent;});
  await tick(p,7999);assert.equal(await id(p),'original');await tick(p,1);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');
  assert.deepEqual(await p.evaluate(()=>[launchNode===document.querySelector('#launch'),actualNode===document.querySelector('[data-home-actual]'),focusNode===document.activeElement,actualNode.textContent===actualText,JSON.stringify(actualNode.getBoundingClientRect().toJSON())===JSON.stringify(actualRect),document.querySelector('#play-profile-select').value]),[true,true,true,true,true,'a'.repeat(32)]);
  assert.deepEqual(errors,[]);
 }finally{await p.close();}
});
test('manual previous/next, pause and known selection persist; malformed preferences cannot become URLs',async b=>{
 const{page:p}=await fixture(b);try{
  await p.locator('[data-scene-prev]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='safehouse');
  await p.locator('[data-scene-next]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='original');
  await p.locator('[data-scene-next]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');
  await p.locator('[data-scene-pause]').click();await p.mouse.move(0,0);await p.locator('#launch').focus();await tick(p,24000);assert.equal(await id(p),'crossroads');
  const saved=await p.evaluate(()=>localStorage.getItem('orange.homeScene'));assert.deepEqual(JSON.parse(saved),{id:'crossroads',paused:true});
  await p.reload();await p.waitForFunction(()=>document.querySelector('[data-home-cover]')?.dataset.sceneId==='crossroads');assert.equal(await p.locator('[data-scene-pause]').getAttribute('aria-pressed'),'true');
 }finally{await p.close();}
 for(const pref of ['{',JSON.stringify({id:'https://invalid.test/evil.png',paused:'yes'})]){const{page}=await fixture(b,{pref});assert.equal(await id(page),'original');await page.close();}
});
test('pointer, keyboard focus, page visibility and route pause; repeated renders retain one scheduled advance',async b=>{
 const{page:p}=await fixture(b);try{
  await p.locator('[data-home-cover]').hover();await tick(p,16000);assert.equal(await id(p),'original');await p.mouse.move(0,0);
  await p.locator('[data-scene-next]').focus();await tick(p,16000);assert.equal(await id(p),'original');await p.locator('#launch').focus();
  await p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));});await tick(p,16000);assert.equal(await id(p),'original');
  await p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>false});document.dispatchEvent(new Event('visibilitychange'));});
  await route(p,'help');await tick(p,16000);await route(p,'home');await p.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
  await tick(p,4000);await p.evaluate(()=>{for(let i=0;i<5;i++)orange.acceptStatus({...orange.state.status,launch:{stage:'waiting',message:String(i)}});});await tick(p,4000);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');await tick(p);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='riverside');
 }finally{await p.close();}
});
test('reduced motion starts paused, can resume, and failed candidates fall back without blanking original',async b=>{
 const{page:p}=await fixture(b,{reduced:true});try{assert.equal(await p.locator('[data-scene-pause]').getAttribute('aria-pressed'),'true');await tick(p,16000);assert.equal(await id(p),'original');await p.locator('[data-scene-pause]').click();await p.mouse.move(0,0);await p.locator('#launch').focus();await tick(p);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');}finally{await p.close();}
 // Invalid PNG payloads exercise the real asynchronous image error/fallback path.
 const badHtml=fs.readFileSync(path.join(stage,'index.html'),'utf8').replace(/data:image\/png;base64,[A-Za-z0-9+/=]+/g,'data:image/png;base64,broken');
 fs.writeFileSync(path.join(stage,'broken.html'),badHtml);
 const f=await fixture(b);try{await f.page.goto(url.replace('index.html','broken.html'));await f.page.waitForFunction(()=>!!orange.state.status&&!Object.keys(orange.state.experienceReads).length);await tick(f.page);assert.equal(await id(f.page),'original');assert.match(await f.page.locator('[data-home-cover]').evaluate(e=>getComputedStyle(e).backgroundImage),/home\.png/);assert.deepEqual(f.errors,[]);}finally{await f.page.close();}
});
test('gallery stays usable and continues rotating while game-running and mutation locks disable launch',async b=>{
 const{page:p}=await fixture(b);try{
  await p.evaluate(()=>{orange.state.coreApplying=true;orange.acceptStatus({...orange.state.status,running:true,task:{active:true,text:'Synthetic native work'},gameVersion:{value:'42.21.0'}});window.beforeSceneActions=sent.length;});
  assert.equal(await p.locator('#launch').isDisabled(),true);assert.equal(await p.locator('[data-scene-next]').isEnabled(),true);await p.locator('[data-scene-next]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');
  await p.mouse.move(0,0);await p.evaluate(()=>document.activeElement.blur());await tick(p);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='riverside');
  assert.deepEqual(await p.evaluate(()=>sent.slice(beforeSceneActions)),[],'scene controls and ticks cannot send native actions');
 }finally{await p.close();}
});
test('a delayed decoded image cannot overwrite a newer manual choice or a route teardown',async b=>{
 for(const scenario of ['manual','route']){const{page:p}=await fixture(b);try{
  await holdCrossroadsDecode(p);
  await p.locator('[data-scene-next]').click();await p.waitForFunction(()=>!!window.releaseSceneDecode);
  if(scenario==='manual'){await p.locator('[data-scene-prev]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='safehouse');await p.evaluate(()=>releaseSceneDecode());await p.waitForTimeout(30);assert.equal(await id(p),'safehouse');}
  else{await route(p,'help');await p.evaluate(()=>releaseSceneDecode());await route(p,'home');await p.waitForTimeout(30);assert.equal(await id(p),'original');}
 }finally{await p.close();}}
});
const sceneSuspensions={
 hidden:{suspend:p=>p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>true});document.dispatchEvent(new Event('visibilitychange'));}),resume:p=>p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,get:()=>false});document.dispatchEvent(new Event('visibilitychange'));})},
 user:{suspend:p=>p.locator('[data-scene-pause]').click(),resume:async p=>{await p.locator('[data-scene-pause]').click();await p.mouse.move(0,0);await p.locator('#launch').focus();}},
 pointer:{suspend:p=>p.locator('[data-home-cover]').hover(),resume:p=>p.mouse.move(0,0)},
 focus:{suspend:p=>p.locator('[data-scene-next]').focus(),resume:p=>p.locator('#launch').focus()},
 reduced:{suspend:p=>p.emulateMedia({reducedMotion:'reduce'}),resume:async p=>{await p.locator('[data-scene-pause]').click();await p.mouse.move(0,0);await p.locator('#launch').focus();}},
 route:{suspend:p=>route(p,'help'),resume:async p=>{await route(p,'home');await p.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);}}
};
for(const [cause,change] of Object.entries(sceneSuspensions))for(const resumed of [false,true])test(`pending autoplay is cancelled by ${cause}${resumed?' even after resume':' while suspended'}`,async b=>{
 const{page:p,errors}=await fixture(b);try{
  await holdCrossroadsDecode(p);await tick(p);await p.waitForFunction(()=>!!window.releaseSceneDecode);
  await change.suspend(p);if(cause==='reduced')await p.waitForFunction(()=>document.querySelector('[data-scene-pause]').getAttribute('aria-pressed')==='true');
  if(resumed)await change.resume(p);
  const saved=await p.evaluate(()=>localStorage.getItem('orange.homeScene'));
  await p.evaluate(()=>releaseSceneDecode());await p.waitForTimeout(30);
  if(cause!=='route'||resumed)assert.equal(await id(p),'original',`${cause} must invalidate the old automatic paint even after resume`);
  assert.equal(await p.evaluate(()=>localStorage.getItem('orange.homeScene')),saved,'discarded autoplay cannot persist a new selection');
  if(resumed){await tick(p,7999);assert.equal(await id(p),'original');await tick(p,1);await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='crossroads');}
  else if(cause!=='route'){await p.locator('[data-scene-prev]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId==='safehouse');}
  assert.deepEqual(errors,[]);
 }finally{await p.close();}
});
test('real gallery screenshots keep readable controls and stable actual overlay in both themes at required sizes',async b=>{
 for(const theme of ['dark','light']){const{page:p,errors}=await fixture(b,{theme});try{for(const size of [{width:733,height:507},{width:1366,height:768},{width:1920,height:1080}]){await p.setViewportSize(size);await p.locator('main').evaluate(e=>e.scrollTop=0);await p.locator('[data-scene-next]').click();await p.waitForFunction(()=>document.querySelector('[data-home-cover]').dataset.sceneId!=='original');await p.screenshot({path:path.join(stage,`${theme}-${size.width}.png`)});const cover=await p.locator('[data-home-cover]').boundingBox(),controls=await p.locator('[data-scene-controls]').boundingBox(),actual=await p.locator('[data-home-actual]').boundingBox();assert.ok(controls.y+controls.height<=actual.y,'controls remain above actual summary');assert.ok(controls.x>=cover.x&&controls.x+controls.width<=cover.x+cover.width+1);assert.equal(await p.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true);}assert.deepEqual(errors,[]);}finally{await p.close();}}
});
(async()=>{const browser=await chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const t of tests){try{await t.run(browser);console.log('PASS '+t.name);}catch(e){failed++;console.error('FAIL '+t.name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} carousel groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1;});
