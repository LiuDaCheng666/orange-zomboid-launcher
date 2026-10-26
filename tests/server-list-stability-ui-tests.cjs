const assert=require('node:assert/strict');
const {setup,route,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const {page,errors}=await setup(browser);try{
  await route(page,'servers');await page.evaluate(()=>fx.hold='serverHall');await page.locator('[data-server-tab=hall]').click();
  await page.waitForFunction(()=>held.some(m=>m.action==='serverHall'));
  await page.evaluate(()=>{window.hallRequest=held.find(m=>m.action==='serverHall');window.hallFixtures=[{id:'one',name:'一号生存服',host:'203.0.113.10',port:16261,players:5,maxPlayers:32,description:'介绍一'},{id:'two',name:'二号生存服',host:'203.0.113.11',port:16261,players:2,maxPlayers:24,description:'介绍二'}];emitNative({event:'hallBatch',data:{requestId:hallRequest.payload.requestId,servers:hallFixtures}});});
  await page.locator('[data-hall-id=one] details summary').click();await page.locator('[data-hall-add=one]').focus();
  await page.evaluate(()=>{window.hallCard=document.querySelector('[data-hall-id=one]');window.hallButton=hallCard.querySelector('button');window.hallDetails=hallCard.querySelector('details');emitNative({event:'networkProgress',data:{requestId:hallRequest.payload.requestId,text:'继续查询'}});});
  assert.equal(await page.evaluate(()=>hallButton===document.querySelector('[data-hall-add=one]')),true,'query progress preserves existing button DOM');
  assert.equal(await page.locator('[data-hall-id=one] details').evaluate(e=>e.open),true,'progress preserves expanded server introduction');
  await page.evaluate(()=>{hallFixtures[0].players=7;hallFixtures.push({id:'three',name:'三号生存服',host:'203.0.113.12',port:16261,players:1,maxPlayers:16});emitNative({event:'hallBatch',data:{requestId:hallRequest.payload.requestId,servers:hallFixtures}});});
  assert.equal(await page.locator('[data-hall-id]').count(),3);assert.equal(await page.evaluate(()=>hallCard===document.querySelector('[data-hall-id=one]')&&hallDetails===document.querySelector('[data-hall-id=one] details')&&document.activeElement===hallButton),true,'batch updates preserve card, disclosure and focused button');
  assert.match(await page.locator('[data-hall-id=one] .server-metrics').innerText(),/7/);
  await page.evaluate(()=>emitNative({id:hallRequest.id,ok:true,data:{servers:hallFixtures,message:'查询完成'}}));await page.waitForFunction(()=>!orange.state.loading.serverHall);
  assert.equal(await page.evaluate(()=>hallButton===document.querySelector('[data-hall-add=one]')&&hallDetails.open),true,'completion also preserves open introduction and button');
  await page.locator('[data-server-tab=personal]').click();await page.locator('[data-server-card=s1] details summary').click();await page.evaluate(()=>{fx.hold='serverQuery';window.personalCard=document.querySelector('[data-server-card=s1]');window.personalQuery=personalCard.querySelector('[data-server-query]');window.personalDetails=personalCard.querySelector('details');});await page.locator('[data-server-query=s1]').click();await page.waitForFunction(()=>held.some(m=>m.action==='serverQuery'));
  await page.evaluate(()=>{const m=held.find(m=>m.action==='serverQuery');emitNative({id:m.id,ok:true,data:{id:'s1',status:'online',players:8,maxPlayers:32,latencyMs:12,descriptionSource:'server',description:'公开介绍已更新',version:'42.21',checkedUtc:new Date().toISOString()}});});
  await page.getByText('公开介绍已更新',{exact:true}).waitFor();assert.equal(await page.evaluate(()=>personalQuery===document.querySelector('[data-server-query=s1]')&&personalDetails===document.querySelector('[data-server-card=s1] details')&&personalDetails.open),true,'personal server query preserves button and expanded introduction');
  assert.deepEqual(errors,[]);console.log('PASS streaming server progress, batches, completion and personal queries preserve card/button identity, focus and introductions');
 }finally{await page.close();}
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
