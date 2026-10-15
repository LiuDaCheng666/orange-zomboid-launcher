const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=path.resolve(__dirname,'../..');
const {setup,route,chromium}=require(path.join(root,'Orange3DTool/Web/experience-ui-tests.cjs'));
const shots=path.join(__dirname,'visual');fs.mkdirSync(shots,{recursive:true});
(async()=>{const b=await chromium.launch({channel:'msedge',headless:true});let total=0;try{
 for(const theme of ['dark','light']){const {page:p,errors}=await setup(b,theme);try{
  await p.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
  await p.evaluate(()=>{const c=document.createElement('canvas');c.width=c.height=128;let x=c.getContext('2d');x.fillStyle='#153b36';x.fillRect(0,0,128,128);x.fillStyle='#efab55';x.fillRect(8,8,50,112);x.fillStyle='#91c4da';x.fillRect(70,8,50,112);fx.thumb=c.toDataURL('image/png');});
  for(const width of [733,1366,1920]){await p.setViewportSize({width,height:width===733?507:width===1920?1080:768});
   for(const kind of ['home','saves','servers'])for(const empty of kind==='home'?[false]:[false,true]){
    await route(p,'help');
    await p.evaluate(({kind,empty})=>{if(kind==='saves')orange.state.saves={saves:empty?[]:[{id:'Sandbox/Long',name:'跨季节生存记录_LongUnbrokenSaveName'.repeat(12),mode:'Sandbox',editable:true,modifiedUtc:'2026-10-15T00:00:00Z',modCount:24}],warnings:[]};if(kind==='servers')orange.state.servers={servers:empty?[]:[{id:'s1',name:'长期生存_ServerWithVeryLongName'.repeat(14),host:'example.invalid',port:16261,description:'长介绍与服务器规则 '.repeat(300)}]};}, {kind,empty});
    await route(p,kind);await p.waitForTimeout(100);
    if(kind!=='home'&&!empty){const img=p.locator('.original-thumb img').first();await img.waitFor();await p.waitForFunction(()=>document.querySelector('.original-thumb img')?.naturalWidth>0);assert.deepEqual(await img.evaluate(e=>[e.naturalWidth,e.naturalHeight,getComputedStyle(e).objectFit]),[128,128,'contain']);}
    if(empty)assert.equal(await p.locator(kind==='saves'?'.save-card':'.server-card').count(),0,'empty fixture must render no library cards');
    assert.equal(await p.locator('main').evaluate(e=>e.scrollWidth<=e.clientWidth),true);
    await p.screenshot({path:path.join(shots,`${theme}-${kind}-${empty?'empty':'long'}-${width}.png`)});total++;
    if(kind==='servers'&&!empty){await p.locator('.server-description summary').click();await p.locator('.server-description').scrollIntoViewIfNeeded();assert.equal(await p.locator('.server-description').evaluate(e=>e.scrollWidth<=e.clientWidth),true);await p.screenshot({path:path.join(shots,`${theme}-servers-description-${width}.png`)});total++;}
   }
  }
  assert.deepEqual(errors,[]);
 }finally{await p.close();}}
 console.log(`VISUAL: ${total} screenshots; actual decoded square previews, long and empty libraries, home, both themes, 733/1366/1920`);
}finally{await b.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
