/* Headless application evidence only. Never exports or modifies website assets. */
const {setup,route,routes,chromium}=require('./refresh-fixture.cjs');
const fs=require('node:fs'),path=require('node:path'),assert=require('node:assert/strict');
const root=path.resolve(__dirname,'../../artifacts/c-glass-ui');
const before=process.argv.includes('--before'),folder=before?path.join(root,'before'):root;
async function settled(page){await page.waitForFunction(()=>!Object.values(orange.state.loading).some(Boolean));await page.evaluate(async()=>{await document.fonts.ready;await Promise.all(document.getAnimations().map(a=>a.finished.catch(()=>{})));document.querySelector('main').scrollTo(0,0);});await page.waitForTimeout(100);}
async function recording(browser){
 const context=await browser.newContext({viewport:{width:1440,height:900},recordVideo:{dir:path.join(root,'video'),size:{width:1440,height:900}}});
 const start=Date.now(),events=[];const {page,errors}=await setup({newPage:()=>context.newPage()},{capture:true});
 async function at(seconds,name,fn){await page.waitForTimeout(Math.max(0,start+seconds*1000-Date.now()));events.push({name,time:(Date.now()-start)/1000});await fn();}
 const click=selector=>page.evaluate(s=>document.querySelector(s).click(),selector);
 await at(1.5,'components',()=>click('nav [data-page="components"]'));
 await at(3,'optional components',()=>click('[data-optional-components]>summary'));
 await at(4.2,'component restore dialog',()=>click('[data-components-restore]'));
 await at(5.5,'close dialog',()=>click('#confirm-no'));
 await at(6.5,'light theme',()=>click('#theme'));
 await at(8,'home',()=>click('nav [data-page="home"]'));
 await at(10,'settings',()=>click('nav [data-page="settings"]'));
 await at(12,'memory',()=>page.evaluate(()=>location.hash='memory'));
 await at(13.4,'dark theme',()=>click('#theme'));
 await at(14,'home return',()=>click('nav [data-page="home"]'));
 await at(15.5,'end',async()=>{});assert.deepEqual(errors,[]);
 const video=page.video();await page.close();await context.close();await video.saveAs(path.join(root,'software-motion.webm'));
 fs.writeFileSync(path.join(root,'video-events.json'),JSON.stringify(events,null,2));
 await decode(browser);
}
async function decode(browser){
 const http=require('node:http'),crypto=require('node:crypto'),bytes=fs.readFileSync(path.join(root,'software-motion.webm'));
 const server=http.createServer((request,response)=>{const range=/^bytes=(\d+)-(\d*)$/.exec(request.headers.range||'');if(range){const start=+range[1],end=range[2]?Math.min(+range[2],bytes.length-1):bytes.length-1;response.writeHead(206,{'Content-Type':'video/webm','Accept-Ranges':'bytes','Content-Range':`bytes ${start}-${end}/${bytes.length}`,'Content-Length':end-start+1});response.end(bytes.subarray(start,end+1));}else{response.writeHead(200,{'Content-Type':'video/webm','Accept-Ranges':'bytes','Content-Length':bytes.length});response.end(bytes);}});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const page=await browser.newPage({viewport:{width:1440,height:900}});
 try{await page.setContent('<style>body{margin:0}video{width:1440px;height:900px;display:block}</style><video muted preload="auto"></video>');await page.locator('video').evaluate((v,url)=>v.src=url,`http://127.0.0.1:${server.address().port}/video.webm`);await page.waitForFunction(()=>document.querySelector('video').readyState>=2);
  const metadata=await page.locator('video').evaluate(v=>({width:v.videoWidth,height:v.videoHeight,duration:v.duration}));assert.ok(metadata.duration>=12&&metadata.duration<=18,JSON.stringify(metadata));
  metadata.frames=[];
  for(const time of [1.4,1.52,4.35,6.9,12.15]){await page.locator('video').evaluate((v,t)=>new Promise(r=>{v.addEventListener('seeked',r,{once:true});v.currentTime=t;}),time);const file=path.join(root,`video-frame-${time}.png`);await page.locator('video').screenshot({path:file});metadata.frames.push({time,sha256:crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex')});}
  assert.notEqual(metadata.frames[0].sha256,metadata.frames[1].sha256,'intermediate video frames must differ');fs.writeFileSync(path.join(root,'video-review.json'),JSON.stringify(metadata,null,2));console.log('PASS actual video decoded '+JSON.stringify(metadata));
 }finally{await page.close();await new Promise(r=>server.close(r));}
}
(async()=>{fs.mkdirSync(folder,{recursive:true});const browser=await chromium.launch({channel:'msedge',headless:true});try{
 for(const theme of ['dark','light']){const {page,errors}=await setup(browser,{theme,capture:true});
  for(const [width,height] of before?[[1440,900]]:[[1440,900],[1280,900],[768,900],[733,507],[500,760]]){
   await page.setViewportSize({width,height});
   for(const name of before?['home','components','mods']:routes){await route(page,name);await settled(page);assert.equal(await page.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true,`${theme} ${name} ${width} overflow`);await page.screenshot({path:path.join(folder,`${theme}-${name}-${height===900?width:width+'x'+height}.png`)});}
  }
  await route(page,'maintenance');await page.locator('[data-maintenance="cache"]').click();await page.waitForTimeout(300);await page.screenshot({path:path.join(folder,`${theme}-dialog.png`)});await page.locator('#confirm-no').click();
  if(!before){await page.setViewportSize({width:733,height:507});await page.locator('[data-maintenance="cache"]').click();await page.evaluate(()=>document.querySelector('#confirm-body').textContent=Array.from({length:25},(_,i)=>'路径 '+i+' C:/Example/Java/file.jar · SHA256 '+ 'a'.repeat(64)).join('\n'));await page.waitForTimeout(300);await page.screenshot({path:path.join(folder,`${theme}-long-dialog-733x507.png`)});await page.locator('#confirm-no').click();}
  assert.deepEqual(errors,[]);await page.close();
  if(!before){const dense=await setup(browser,{theme,capture:true,catalogRows:8});await dense.page.setViewportSize({width:1280,height:900});await route(dense.page,'mods');await settled(dense.page);const visible=await dense.page.locator('.catalog-row').evaluateAll(es=>es.filter(e=>{const r=e.getBoundingClientRect();return r.top>=0&&r.bottom<=900;}).length);assert.ok(visible>=6);await dense.page.screenshot({path:path.join(root,`${theme}-catalog-density-1280.png`)});await dense.page.close();}
 }
 if(!before){
  const {page}=await setup(browser,{capture:true});await page.evaluate(()=>document.querySelector('nav [data-page="components"]').click());
  for(const fraction of [.35,.7]){await page.evaluate(f=>{for(const a of document.getAnimations()){a.pause();a.currentTime=a.effect.getTiming().duration*f;}},fraction);await page.screenshot({path:path.join(root,`intermediate-route-${fraction}.png`)});}await page.close();
  await recording(browser);
  const crypto=require('node:crypto'),hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
  const manifest={source:Object.fromEntries(['app.css','app.js','index.html'].map(f=>[f,hash(path.join(__dirname,f))])),captures:{}};
  for(const theme of ['dark','light'])for(const name of ['home','components','mods','servers','maintenance']){const file=`${theme}-${name}-1440.png`;manifest.captures[file]=hash(path.join(root,file));}
  fs.writeFileSync(path.join(root,'capture-provenance.json'),JSON.stringify(manifest,null,2));
 }
 console.log('PASS '+(before?'8 BEFORE screenshots':'110 route/theme/size screenshots plus dialogs'));
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
