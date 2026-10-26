const assert=require('node:assert/strict');const fs=require('node:fs'),path=require('node:path');const out=path.resolve(__dirname,'../artifacts/library-ui');fs.mkdirSync(out,{recursive:true});
const {setup,route,chromium}=require('../Orange3DTool/Web/experience-ui-tests.cjs');
(async()=>{const browser=await chromium.launch({channel:'msedge',headless:true});try{
 const {page,errors}=await setup(browser);try{
  assert.equal(await page.locator('nav [data-page=versions]').count(),1,'version management is a direct navigation item');
  await page.evaluate(()=>{
   const original=chrome.webview.postMessage;window.libraryCalls=[];
   window.libraryRows=[{id:'steam',name:'当前 Steam 安装',version:'42.21',gamePath:'D:\\Games\\PZ',userPath:'C:\\Users\\Player\\Zomboid',installed:true,selected:true,managed:false},{id:'a'.repeat(32),name:'旧版生存',version:'41.78.16',gamePath:'E:\\PZ41',userPath:'E:\\OZL\\version-users\\41',installed:true,selected:false,managed:true}];
   const response=async(a,p={})=>{libraryCalls.push({a,p});if(a==='versionList')return{versions:libraryRows,selectedId:libraryRows.find(r=>r.selected).id};if(a==='versionSelect'){libraryRows.forEach(r=>r.selected=r.id===p.id);return{message:'已切换版本',version:libraryRows.find(r=>r.selected)};}if(a==='recommendedServers')return{schema:1,revision:5,servers:[{id:'test-server',name:'推荐测试服',host:'203.0.113.10',port:16261,queryPort:16262,version:'42.21',description:'一起生存',tags:['生存','PVE'],order:0}],cached:false};if(a==='backupList')return{saveId:p.id,archives:[{id:'b'.repeat(32),label:'出发前',createdUtc:'2026-10-26T11:00:00Z',bytes:1048576,fileCount:4}],warnings:[]};if(a==='backupSettings')return{directory:'D:\\OZLBackups',retention:10,afterExit:false};if(a==='versionInstallState')return{stage:'none'};if(a==='recommendedQuery')return{status:'online',players:7,maxPlayers:32,latencyMs:18};if(a==='backupCreate')return{status:'completed',id:'c'.repeat(32),message:'备份完成'};return{};};chrome.webview.postMessage=m=>{if(!['versionList','versionSelect','versionInstallState','recommendedServers','recommendedQuery','backupList','backupSettings','backupCreate'].includes(m.action))return original(m);sent.push(m);Promise.resolve(response(m.action,m.payload)).then(data=>emitNative({id:m.id,ok:true,data}));};
  });
  await page.locator('nav [data-page=versions]').click();await page.locator('[data-version-refresh]').click();await page.getByRole('heading',{name:'旧版生存',exact:true}).waitFor();
  await page.screenshot({path:path.join(out,'versions-dark.png'),fullPage:true});assert.equal(await page.locator('[data-version-select]').count(),1,'only inactive versions offer switch');
  await page.locator('[data-version-select]').click();await page.waitForFunction(()=>libraryCalls.some(x=>x.a==='versionSelect'));
  await route(page,'servers');await page.locator('[data-server-tab=recommended]').click();await page.getByRole('heading',{name:'推荐测试服'}).waitFor();
  await page.screenshot({path:path.join(out,'recommended-dark.png'),fullPage:true});assert.equal(await page.locator('[data-recommended-connect]').count(),1,'recommendation has a connection action');
  assert.equal(await page.locator('#page').innerText().then(t=>t.includes('203.0.113.10:16261')),true);
  await route(page,'saves');await page.locator('[data-save-backups]').first().waitFor();await page.locator('[data-save-backups]').first().click();
  await page.getByRole('heading',{name:'存档备份',exact:true}).waitFor();await page.getByText('出发前',{exact:true}).waitFor();
  await page.waitForFunction(()=>!orange.state.busy&&!orange.library.model.working);await page.locator('#save-backup-dialog').evaluate(e=>e.getAnimations({subtree:true}).forEach(a=>{if(Number.isFinite(a.effect.getComputedTiming().endTime))a.finish();}));await page.screenshot({path:path.join(out,'backups-dark.png'),fullPage:true});assert.equal(await page.locator('[data-backup-restore]').count(),1,'archive history can restore');
  await page.locator('[data-backup-create]').click();await page.waitForFunction(()=>libraryCalls.some(x=>x.a==='backupCreate'));
  assert.equal(await page.locator('[data-backup-message]').innerText(),'备份完成');
  assert.deepEqual(errors,[]);console.log('PASS version selection, recommended server cards and save backup actions');
 }finally{await page.close();}
}finally{await browser.close();}})().catch(e=>{console.error(e);process.exitCode=1;});
