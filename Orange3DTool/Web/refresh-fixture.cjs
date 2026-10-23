/* Synthetic native boundary. Never opens Steam, the game, or user files. */
const path=require('node:path');
const fs=require('node:fs');
const {chromium}=require('./playwright-runtime.cjs');
const routes=['home','components','mods','workshop','saves','servers','settings','shaders','memory','maintenance','help'];
async function setup(browser,options={}){
 const coverFolder=process.env.OZL_TEST_COVERS_DIR||path.join(__dirname,'author-covers');
 const covers=Object.fromEntries((fs.existsSync(coverFolder)?fs.readdirSync(coverFolder):[]).filter(n=>n.endsWith('.jpg')).map(n=>[n.slice(0,-4),'data:image/jpeg;base64,'+fs.readFileSync(path.join(coverFolder,n)).toString('base64')]));
 if(options.capture&&process.env.OZL_CAPTURE_BUDDY_ICON&&fs.existsSync(process.env.OZL_CAPTURE_BUDDY_ICON))covers['3619862853']='data:image/png;base64,'+fs.readFileSync(process.env.OZL_CAPTURE_BUDDY_ICON).toString('base64');
 const page=await browser.newPage({viewport:{width:1440,height:900}});page.setDefaultTimeout(4000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 if(options.clock)await page.clock.install();
 await page.addInitScript(options=>{
  window.sent=[];window.held=[];window.pollCallbacks=[];if(!options.clock)window.setInterval=f=>pollCallbacks.push(f);
  window.core={protocol:1,revision:1,state:'Idle',currentVersion:options.capture?'1.2.14':'1.2.0',latestVersion:'',taskId:'',checkedUtc:'2026-10-23T08:00:00Z',notes:'界面与稳定性改进',receivedBytes:0,totalBytes:0,speedBytes:0,currentFile:'',message:'',history:[],...options.core};
  const components=[['buddy','ZombieBuddy','3619862853','Java 模组加载支持，保留原生启动方式。'],['viewpoint','Project Viewpoint','3809306528','在第一人称与第三人称之间自由切换。'],['chinese','老余汉化 · JAVA 版','3810637496','独立中文支持，也适用于原版游戏。'],['models','6244 3D Models','3810302175','为物件带来细致的立体模型。'],['shoot','Shoot to Thrill','3813032735','作者枪械与多人联机支持。'],['surface','Surface Fix','3814398576','修复 Viewpoint 表面显示。']].map(([id,title,item,description],i)=>({id,title,item,description,optional:i>2,downloaded:options.missing?false:i<3,configured:options.missing?false:i<3,version:i===0?'未声明':i===1?'0.6.3':'2026.10',runtimeState:'下次启动生效',sourceState:'same',sourceFingerprint:'已核对本地来源',compatibility:i===2?'可独立使用':'Build 42',warning:''}));
  for(const c of components){if(c.id==='buddy')c.version='2.3.4';if(c.id==='viewpoint')c.version='0.1.6.1';if(c.id==='chinese')c.version='1.19.0';}
  components.find(c=>c.id==='chinese').description='为 Viewpoint 等 Java 模组提供简体中文，可独立于 3D 安装。';
  window.demoStatus={validGame:options.validGame!==false,running:false,theme:options.theme||'dark',gamePath:'示例游戏目录',gameVersion:{value:'42.21.0',warning:''},configStamp:'demo',components,task:{active:false},launch:{pending:false},buddyRecovery:{pending:false}};
  const mods=[['survival','生存工具箱','日常制作与整理工具'],['map','河畔探索地图','一段新的探索路线'],['furniture','温暖的家','室内家具与装饰']].map(([id,title,description])=>({id,title,name:title,description,author:'工坊原作者',version:'Build 42',source:'workshop',item:'',workshopId:'',cover:'',require:[],requires:[],loadAfter:[],loadBefore:[],conflicts:[],componentManaged:false,conflict:false,map:id==='map'}));
  const saves=[{id:'Sandbox/demo',name:'河畔的第 28 天',mode:'Sandbox',modifiedUtc:'2026-10-22T12:00:00Z',modCount:3,editable:true},{id:'Sandbox/cabin',name:'林间小屋',mode:'Sandbox',modifiedUtc:'2026-10-20T12:00:00Z',modCount:2,editable:true}];
  const servers=[{id:'demo',name:'橘子镇 · 合作生存',host:'play.example.invalid',port:16261,queryPort:16262,username:'',hasUserPassword:false,hasServerPassword:false,useSteamRelay:false,description:'慢节奏探索，共同建造安全屋。示例连接，不会发起网络请求。'}];
  window.chrome={webview:{addEventListener:(e,f)=>window.reply=f,postMessage:m=>{
   sent.push(m);if(options.hold===m.action){held.push(m);return;}
   let data={message:'操作完成 · 合成示例'};
   switch(m.action){
    case'status':data=demoStatus;break;
    case'coreUpdateStatus':data=core;break;
    case'coreUpdateCheck':
     data={...core,revision:2,state:'Checking'};
     window.completeCoreCheck=(overrides={})=>reply({data:{event:'coreUpdateProgress',data:{...core,revision:3,state:options.update||'Latest',latestVersion:options.update==='Available'?(options.capture?'1.2.15':'1.2.1'):'',...overrides}}});
     if(options.coreProgressBeforeReply)queueMicrotask(()=>completeCoreCheck());
     else if(!options.holdCoreProgress)setTimeout(()=>completeCoreCheck(),20);
     break;
    case'coreUpdateCancel':data={...core,revision:3,state:'Cancelled'};break;
    case'coreUpdateDownload':data={...core,revision:5,state:'Downloading',latestVersion:'1.2.1',taskId:'a'.repeat(32)};break;
    case'componentsAction':{const c=components.find(x=>x.id===m.payload.component);if(c){c.configured=m.payload.operation!=='uninstall';c.downloaded=true;}break;}
    case'modCatalog':data={mods:[...mods,...components.filter(c=>options.covers[c.item]).map(c=>({id:c.id,title:c.title,name:c.title,author:'工坊原作者',description:c.description,version:c.version,item:c.item,workshopId:c.item,cover:options.covers[c.item],requires:[],require:[],conflicts:[],componentManaged:true,conflict:false,map:false}))],warnings:[]};break;
    case'modProfiles':data={stamp:'mods',profiles:[{id:'relax',name:'轻量探索',modCount:3,updated:'2026-10-22'}],activeMods:['survival','map']};break;
    case'modProfileGet':data={id:'relax',name:'轻量探索',stamp:'mods',mods:['survival','map']};break;
    case'savesList':data={saves,warnings:[]};break;
    case'serverList':data={stamp:'servers',servers};break;
    case'serverQuery':data={id:'demo',status:'online',players:8,maxPlayers:24,latencyMs:42,checkedUtc:'2026-10-23T08:00:00Z'};break;
    case'saveThumb':case'serverThumb':data={dataUrl:'',message:'尚无本地缩略图'};break;
    case'memoryGet':data={currentGiB:8,totalGiB:32,availableGiB:21,maximumGiB:24,recommendedGiB:8,stamp:'memory'};break;
    case'settingsGet':data={stamp:'settings',rows:[{key:'view|firstPerson',name:'第一人称切换键',group:'视角与操作',description:'游戏中切换第一人称视角。',kind:'text',value:'O',editable:true},{key:'view|thirdPerson',name:'第三人称视角',group:'视角与操作',description:'使用第三人称视角观察周围环境。',kind:'bool',value:'true',editable:true},{key:'view|fov',name:'视野范围',group:'画面表现',description:'按你的屏幕与习惯设置视野。',kind:'text',value:'90',editable:true}],warnings:[]};break;
    case'shadersGet':data={stamp:'shaders',selected:'original',packs:[{id:'original',name:'原版光影',description:'清晰、自然的游戏原始表现。',valid:true,file:'原版'},{id:'eclipse',name:'Eclipse',description:'为 Viewpoint 适配的光影效果。',valid:true,file:'Eclipse.pack'}]};break;
    case'javaList':data={supported:true,stamp:'java',jars:[],warnings:[]};break;
    case'playProfiles':data={stamp:'play',selectedId:'',selectedProfile:null,profiles:[{id:'explore',name:'周末探索',targetKind:'normal',modCount:3}],pendingRecovery:{pending:false},warnings:[]};break;
    case'playtimeGet':data={todaySeconds:3900,totalSeconds:39240,running:false,sessionSeconds:0,reminderMinutes:60,reminderDue:false,tracking:'stopped'};break;
    case'help':data={message:'核对来源后，仅允许你信任的 Java 文件。'};break;
    case'steamConnection':data={available:true,accountName:'示例会话',message:'由 Steam 管理登录状态'};break;
    case'steamSubscriptions':data={items:[],message:'没有已读取的订阅'};break;
   }
   queueMicrotask(()=>reply({data:{id:m.id,ok:true,data:structuredClone(data)}}));
  }}};
 },{...options,covers});
 await page.goto('file:///'+path.resolve(__dirname,'index.html').replaceAll('\\','/')+(options.hash||''));
 await page.waitForFunction(()=>orange.state.status);return {page,errors};
}
async function route(page,name){await page.evaluate(n=>location.hash=n,name);await page.waitForFunction(n=>orange.state.route===n,name);await page.waitForTimeout(60);}
module.exports={setup,route,routes,chromium};
