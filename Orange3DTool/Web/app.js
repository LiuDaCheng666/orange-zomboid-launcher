(()=>{
 'use strict';
 const model={description:'',gameLog:false,launcherLog:false,system:false,preview:null,previewRevision:-1,consent:false,revision:0,view:0,pending:null,status:null,statusRequest:null,error:'',statusError:'',receipt:null};
 const esc=value=>String(value??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const exactText=value=>esc(value).replace(/\r/g,'&#13;');
 const active=()=>window.orange?.state?.route==='feedback';
 const validDescription=()=>model.description.trim().length>0&&model.description.length<=2000;
 const validPreview=()=>!!model.preview&&model.previewRevision===model.revision;
 const date=value=>{const d=new Date(value);return Number.isNaN(d.getTime())?String(value||''):d.toLocaleString('zh-CN',{hour12:false});};
 const categoryNames={description:'问题描述',gameLog:'游戏日志',launcherLog:'启动器日志',system:'电脑信息'};

 function checkbox(key,label){const known=key==='system'||model.status?.available?.[key]===true;return `<label class="filter-label"><input type="checkbox" data-feedback-category="${key}" ${model[key]?'checked':''} ${known?'':'disabled'}> ${label}${known?'':` <span class="small">${model.status?'暂无日志':'正在核对'}</span>`}</label>`;}
 function previewPanel(){
  if(!validPreview())return '';
  const p=model.preview;
  return `<section class="card section" data-feedback-preview-content><div class="row"><h2>预览</h2><span class="small">${esc(p.id)} · ${esc(p.bytes)} 字节</span></div><p class="small">${esc((p.categories||[]).map(key=>categoryNames[key]||key).join(' · '))}</p><details><summary>问题描述</summary><pre data-feedback-preview-description><code>${exactText(p.description)}</code></pre></details>${p.files.map(file=>`<details data-feedback-file><summary>${esc(file.name)} · ${esc(file.bytes)} 字节</summary><pre><code>${exactText(file.text)}</code></pre></details>`).join('')}${p.system===null?'':`<details><summary>电脑信息</summary><pre data-feedback-system><code>${exactText(JSON.stringify(p.system,null,2))}</code></pre></details>`}<div class="actions section"><label class="filter-label"><input type="checkbox" data-feedback-consent ${model.consent?'checked':''} ${model.pending?'disabled':''}> 我同意发送以上内容</label><button class="primary" data-feedback-send ${model.pending||!model.consent?'disabled':''}>${model.pending?.kind==='send'?'正在发送…':'发送反馈'}</button></div></section>`;
 }
 function receiptPanel(){const r=model.receipt;return r?`<section class="card section" data-feedback-receipt><h2>反馈已收到</h2><p>${esc(r.message||'反馈已收到')}</p><p class="small">编号 ${esc(r.id)} · ${esc(date(r.receivedUtc))}</p></section>`:'';}
 function render(){return `<div class="diagnostics log-analysis-page" data-feedback-root><div class="heading"><div><div class="eyebrow">OZL / FEEDBACK</div><h1>反馈</h1></div></div><section class="card"><h2>问题描述</h2><label for="feedback-description" class="small">遇到了什么问题？</label><textarea id="feedback-description" data-feedback-description maxlength="2000" rows="5" required aria-describedby="feedback-description-count">${esc(model.description)}</textarea><p id="feedback-description-count" class="small">${model.description.length} / 2000</p><h3 class="section">勾选资料</h3><div class="actions section">${checkbox('gameLog','游戏日志')}${checkbox('launcherLog','启动器日志')}${checkbox('system','电脑信息')}</div><div class="actions section"><button data-feedback-preview ${model.pending||!validDescription()?'disabled':''}>${model.pending?.kind==='preview'?'正在准备预览…':'预览'}</button></div></section>${model.error?`<p class="status-negative section" data-feedback-error role="status">${esc(model.error)}</p>`:''}${previewPanel()}${receiptPanel()}<section class="card section" data-feedback-history><div class="row"><h2>发送记录</h2><button data-feedback-reload ${model.statusRequest||model.pending?'disabled':''}>${model.statusRequest?'正在读取…':'刷新记录'}</button></div>${model.statusError?`<p class="status-negative" role="status">${esc(model.statusError)}</p>`:''}${(model.status?.history||[]).length?`<ul>${model.status.history.map(item=>`<li><strong>${esc(item.id)}</strong> · ${esc(date(item.receivedUtc))} · ${esc(item.status==='received'?'已收到':item.status)}</li>`).join('')}</ul>`:'<p class="small">暂无发送记录。</p>'}</section></div>`;}

 function renderMounted(){
  if(!active())return;
  const page=document.querySelector('#page');if(!page)return;
  const focused=document.activeElement;let selector='',start=null,end=null;
  if(focused?.matches('[data-feedback-description]')){selector='[data-feedback-description]';start=focused.selectionStart;end=focused.selectionEnd;}
  else if(focused?.hasAttribute('data-feedback-category'))selector=`[data-feedback-category="${focused.dataset.feedbackCategory}"]`;
  else if(focused?.hasAttribute('data-feedback-consent'))selector='[data-feedback-consent]';
  const main=page.closest('main'),scroll=main?.scrollTop;
  const shared=[document.querySelector('#notice'),document.querySelector('#task')].filter(Boolean);
  page.innerHTML=render();
  const heading=page.querySelector('.heading');if(heading)heading.after(...shared);else page.prepend(...shared);
  const notice=document.querySelector('#notice');if(notice)notice.hidden=!window.orange.state.notice||window.orange.state.noticeRoute!=='feedback';
  const next=selector&&page.querySelector(selector);if(next){next.focus({preventScroll:true});if(start!==null)next.setSelectionRange(start,end);}
  if(main&&scroll!==undefined)main.scrollTop=scroll;
 }
 function edited(){model.revision++;model.preview=null;model.previewRevision=-1;model.consent=false;model.error='';renderMounted();}
 function current(operation){return model.pending===operation&&operation.revision===model.revision&&operation.view===model.view&&active();}
 function readPreview(value){
  if(!value||typeof value.token!=='string'||!value.token||typeof value.id!=='string'||typeof value.description!=='string'||!Array.isArray(value.categories)||value.categories.some(x=>typeof x!=='string')||!Array.isArray(value.files)||value.files.some(f=>!f||typeof f.name!=='string'||typeof f.text!=='string'||typeof f.bytes!=='number')||value.system!==null&&(typeof value.system!=='object'||Array.isArray(value.system))||typeof value.bytes!=='number')throw new Error('反馈预览不可用，请重新预览。');
  return value;
 }
 async function prepare(){
  if(!active()||model.pending||!validDescription())return;
  const operation={kind:'preview',revision:model.revision,view:model.view};model.pending=operation;model.preview=null;model.previewRevision=-1;model.consent=false;model.error='';
  const payload={description:model.description,gameLog:model.gameLog,launcherLog:model.launcherLog,system:model.system};renderMounted();
  try{const value=readPreview(await window.orange.request('feedbackPreview',payload));if(current(operation)){model.preview=value;model.previewRevision=operation.revision;}}
  catch(error){if(current(operation))model.error=error.message||'反馈预览失败，请重试。';}
  finally{if(model.pending===operation)model.pending=null;renderMounted();}
 }
 async function send(){
  if(!active()||model.pending||!validPreview()||!model.consent)return;
  const operation={kind:'send',revision:model.revision,view:model.view,token:model.preview.token};model.pending=operation;model.error='';renderMounted();
  try{
   const receipt=await window.orange.request('feedbackSend',{token:operation.token,confirmed:true});
   if(!receipt||receipt.status!=='received'||typeof receipt.id!=='string'||!receipt.id||typeof receipt.receivedUtc!=='string')throw new Error('反馈回执不可用，请重试。');
   // A completed send is still this user's receipt after navigation or a new
   // draft. It never replaces their edited description or selected categories.
   model.receipt=receipt;if(model.preview?.token===operation.token){model.preview=null;model.previewRevision=-1;model.consent=false;}
   if(current(operation))model.error='';if(active()&&operation.view===model.view)await readStatus(false);
  }catch(error){if(current(operation))model.error=error.message||'反馈发送失败，请重试。';}
  finally{if(model.pending===operation)model.pending=null;renderMounted();}
 }
 async function readStatus(entering){
  if(!active())return;
  if(entering)model.view++;
  else if(model.statusRequest)return model.statusRequest.promise;
  const ticket={view:model.view,revision:model.revision};model.statusRequest=ticket;model.statusError='';renderMounted();
  ticket.promise=(async()=>{try{
   const value=await window.orange.request('feedbackStatus',{});
   if(model.statusRequest!==ticket||ticket.view!==model.view||!active())return;
   if(!value||!Array.isArray(value.history)||!value.available||typeof value.available.gameLog!=='boolean'||typeof value.available.launcherLog!=='boolean')throw new Error('反馈记录不可用，请重试。');
   model.status=value;let changed=false;for(const key of ['gameLog','launcherLog'])if(!value.available[key]&&model[key]){model[key]=false;changed=true;}
   if(changed){model.revision++;model.preview=null;model.previewRevision=-1;model.consent=false;}
  }catch(error){if(model.statusRequest===ticket&&ticket.view===model.view&&active())model.statusError=error.message||'反馈记录读取失败。';}
  finally{if(model.statusRequest===ticket)model.statusRequest=null;renderMounted();}})();return ticket.promise;
 }
 function load(){return readStatus(true);}

 document.addEventListener('input',event=>{if(active()&&event.target.matches('[data-feedback-description]')){model.description=event.target.value;edited();}});
 document.addEventListener('change',event=>{
  if(!active())return;const input=event.target;
  if(input.matches('[data-feedback-category]')){const key=input.dataset.feedbackCategory;if(['gameLog','launcherLog','system'].includes(key)&&!input.disabled){model[key]=input.checked;edited();}}
  else if(input.matches('[data-feedback-consent]')&&validPreview()&&!model.pending){model.consent=input.checked;renderMounted();}
 });
 document.addEventListener('click',event=>{
  if(!active())return;const button=event.target.closest('button');if(!button||button.disabled)return;
  if(button.hasAttribute('data-feedback-preview')){event.preventDefault();void prepare();}
  else if(button.hasAttribute('data-feedback-send')){event.preventDefault();void send();}
  else if(button.hasAttribute('data-feedback-reload')){event.preventDefault();void readStatus(false);}
 });
 window.ozlFeedback={render,load};
})();

(()=>{'use strict';
const $=s=>document.querySelector(s),esc=v=>String(v??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const preview=new URLSearchParams(location.search).get('preview')==='1',host=window.chrome?.webview,pending=new Map();let seq=0;
const desktopTools=matchMedia('(min-width:1101px)');$('.header-menu').open=desktopTools.matches;desktopTools.addEventListener('change',e=>{$('.header-menu').open=e.matches;});
if(preview)document.documentElement.classList.toggle('light',new URLSearchParams(location.search).get('theme')==='light');
const state={route:'home',status:null,busy:false,notice:'',memory:null,memoryDirty:false,memoryEdit:null,memoryConflict:false,settings:null,settingsDirty:false,settingEdits:{},settingsConflict:false,shaders:null,shootHelp:false,logs:null,help:null,errors:{},loading:{},saves:null,modCatalog:null,saveDetail:null,saveMods:[],saveDirty:false,saveSearch:'',servers:null,serverQueries:{},saveReadSequence:0,java:null};
Object.assign(state,{modProfiles:null,modProfile:null,modProfileName:'',modProfileMods:[],modProfileDirty:false,modProfileReadSequence:0,profileSearch:'',modSelection:{save:'',profile:''},sortPreview:null,serverTab:'personal',serverFavorites:null,serverHall:null,hallQuery:'',hallRequestId:'',hallSort:'players',hallHideEmpty:false,hallHidePassword:false,hallMinPlayers:0,hallMaxPing:'',javaPending:new Set(),steamConnection:null,steamSubscriptions:null,steamSearch:null,steamDetails:null,workshopTab:'search',workshopQuery:'',workshopB42:false,workshopPage:1,workshopReadSequence:0,tasks:null,playtime:null,reminderDismissed:false});
const items={base:'3809306528',models:'3810302175',shoot:'3813032735',chinese:'3810637496',shelves:'3815931973',surface:'3814398576',door:'3812151347',roofs:'3814178814',foraging:'3814994364',fishing:'3814560827',weather:'3813908957',trees:'3813172420'};
const titles={saves:'存档与模组',servers:'服务器',home:'让生存，拥有新视角。',components:'组件管理',shaders:'光影选择',settings:'画面设置',memory:'游戏内存',maintenance:'修复',help:'使用帮助'};
titles.diagnostics='日志分析';titles.mods='模组配置';titles.workshop='Steam 工坊';titles.feedback='反馈';
function fixture(action,payload){const components=Object.entries(items).map(([id,item])=>({id,item,title:{base:'Project Viewpoint + Buddy',models:'6244 3D Models',shoot:'Shoot to Thrill',chinese:'老余汉化 · JAVA 版'}[id]||{shelves:'KK 动态货架',surface:'Surface Fix · 表面修复',door:'Door Fix · 门修复',roofs:'Complete Roofs · 完整屋顶',foraging:'Viewpoint 搜寻与追踪',fishing:'Viewpoint 钓鱼',weather:'True Weathers · 天气与光照',trees:'KK 动态季节树木'}[id],optional:id!=='base'&&id!=='chinese',description:{base:'第一人称与第三人称视角，配合原生 Buddy 启动。',models:'让物件拥有立体模型，丰富探索时的细节。',shoot:'作者多人枪械模组；完整功能需要服务器与客户端模组匹配。',chinese:'独立汉化组件，可单独安装和维护。'}[id]||'按需安装的 Viewpoint 附加内容。',downloaded:id==='base',configured:id==='base',version:'预览示例'}));switch(action){case'status':return {gamePath:'预览 · Project Zomboid',validGame:true,running:false,configured:true,buddyFiles:true,theme:document.documentElement.classList.contains('light')?'light':'dark',task:{active:false,value:-1,text:''},components,configStamp:'preview-stamp'};case'loadingOptions':return{enabled:true,installed:false,removable:false,warning:''};case'javaList':return {supported:false,stamp:'preview',jars:[],warnings:['预览不读取或修改 Java 文件授权。']};case'savesList':return {saves:[{id:'Sandbox/preview',name:'预览存档',mode:'Sandbox',modifiedUtc:'预览示例',modCount:1,editable:true}],warnings:['界面预览；没有读取真实存档。']};case'modCatalog':return {mods:[{id:'preview-mod',name:'预览模组',author:'未提供',description:'仅用于界面预览。实际目录显示作者原始资料。',version:'未提供',source:'预览示例',cover:'',requires:[],conflict:false,map:false}],warnings:[]};case'saveGet':return {id:payload.id,name:'预览存档',mode:'Sandbox',stamp:'preview',enabled:['preview-mod'],maps:[],warnings:[]};case'serverList':return {stamp:'preview',servers:[{id:'preview-server',name:'预览服务器',host:'example.invalid',port:16261,queryPort:0,username:'',hasUserPassword:false,hasServerPassword:false,useSteamRelay:false,description:'仅为界面示例；没有读取或连接服务器。'}]};case'serverQuery':return {id:payload.id,status:'unknown',error:'预览不执行网络查询',checkedUtc:'预览示例'};case'memoryGet':return{currentGiB:6,totalGiB:32,availableGiB:22,maximumGiB:24,recommendedGiB:8,stamp:'preview-stamp'};case'settingsGet':return {stamp:'preview-stamp',rows:[{key:'viewpoint-live.properties|keys.firstPerson',name:'第一人称切换键 / First person',group:'视角与快捷键',description:'在游戏中切换第一人称视角，注意避免与其他模组冲突。',kind:'text',choices:[],value:'O',editable:true},{key:'unknown|custom',name:'未识别的原始参数',group:'其他参数',description:'保留原始内容，未知参数只读。',kind:'text',choices:[],value:'原值',editable:false}],warnings:[]};case'shadersGet':return {selected:'original',stamp:'preview-stamp',packs:[{id:'original',name:'原版光影',description:'使用原版渲染表现。',valid:true,file:'原版'},{id:'eclipse',name:'Eclipse',description:'Viewpoint 专用适配；实际效果取决于当前安装版本。',valid:true,file:'Eclipse.pack'}]};case'logs':return {text:'[预览] 界面测试模式，没有启动游戏或修改真实文件。'};case'help':return {message:'安装组件后，需要在游戏中授权可信 Java 模组。请核对当前快捷键和本次启动日志。'};default:return{message:'预览：'+action+' '+(payload.component||'')+' · 未执行真实操作'};}}
const mutationActions=new Set(['profileRecover','javaSave','saveSave','profileLaunch','serverUpsert','serverDelete','componentsAction','maintenance','memorySave','settingsSave','shaderSelect','dlss','importShader','chooseGame','launch','theme']);
['loadingSetEnabled','loadingUninstall','forceCloseGame','modProfileSave','modProfileDelete','modProfileActivate','modAutoSort','steamDownload','favoriteImport','playtimeSettings','buddyTakeover','buddyRecover'].forEach(a=>mutationActions.add(a));
Object.assign(state,{buddyPreview:null,buddyResult:null,buddyStage:'',buddyActive:false,buddyScanning:false,buddyEpoch:0,buddyInstall:null});
function request(action,payload={}){if(preview)return Promise.resolve(logAnalysisFixture(action)??experienceFixture(action,payload)??richFixture(action,payload)??fixture(action,payload));if(!host)return Promise.reject(new Error('请从 OZL EXE 打开启动器。'));const id='ui-'+Date.now()+'-'+(++seq);return new Promise((resolve,reject)=>{const mutation=mutationActions.has(action),timer=['logAnalysisImport','logAnalysisFolder','logAnalysisExport','environmentPreview','environmentStorageChoose'].includes(action)?null:setTimeout(()=>{if(mutation){notify('任务仍在处理，请查看任务进度；也可取消后重试。');return;}pending.delete(id);reject(new Error('请求等待超时，请查看日志后重试。'));},mutation?7200000:['houseTransferPreview','worldEditRecovery','javaComponentsList','javaComponentsPreview'].includes(action)?600000:60000);pending.set(id,{resolve,reject,timer});host.postMessage({id,action,payload,protocol:1});});}
if(host)host.addEventListener('message',event=>{let message=event.data;if(typeof message==='string'){try{message=JSON.parse(message);}catch{return;}}if(!message||typeof message!=='object')return;if(['configurationProgress','frozenConfigurationProgress'].includes(message.event)){document.dispatchEvent(new CustomEvent('ozl-configuration-progress',{detail:message.data}));return;}if(message.event==='environmentProgress'){document.dispatchEvent(new CustomEvent('ozl-environment-progress',{detail:message.data}));return;}if(message.event==='serverWorkshopProgress'){acceptServerWorkshop(message.data);return;}if(message.event==='coreUpdateProgress'){acceptCoreUpdate(message.data);return;}if(message.event==='progress'){task(message.data);return;}if(message.event==='hallBatch'){mergeHallBatch(message.data);return;}if(message.event==='networkProgress'){if(message.data?.requestId===state.hallRequestId&&state.loading.serverHall){state.hallProgress=message.data.text||'';renderHallResults();}return;}const waiter=pending.get(message.id);if(!waiter)return;clearTimeout(waiter.timer);pending.delete(message.id);message.ok?waiter.resolve(message.data||{}):waiter.reject(Object.assign(new Error(message.error||'操作失败'),{preflight:message.preflight}));});
function notify(text){state.notice=text;$('#notice').hidden=!text;$('#notice').textContent=text;}
function task(data={}){if(data.active!==undefined)state.nativeBusy=!!data.active;state.busy=!!state.nativeBusy||state.pendingMutations>0;$('#task').hidden=!state.busy;$('#cancel').disabled=!state.busy;const staleIdle=data.active===false&&state.pendingMutations>0;if(!staleIdle){$('#task-text').textContent=data.text||'正在处理，请稍候…';if(state.busy&&data.value>=0)$('#progress').value=Math.min(100,data.value);else if(state.busy)$('#progress').removeAttribute('value');else $('#progress').value=0;}if(state.buddyActive&&!staleIdle&&data.text){state.buddyStage=data.text;const stage=$('[data-buddy-stage]');if(stage)stage.textContent=data.text;}disable();if($('#task-center')?.open)scheduleTasks();}
const unsafeControls='[data-loading-toggle],[data-loading-remove],[data-buddy-scan],[data-buddy-takeover],[data-buddy-recover],[data-buddy-continue],[data-component],[data-core-install],[data-addons-install],[data-save-memory],[data-save-settings],[data-shader],[data-maintenance],[data-cache-all],[data-save-write],[data-profile-launch],[data-server-write],[data-server-delete],[data-mod-profile-save],[data-mod-profile-activate],[data-mod-profile-delete],[data-preflight-auto-sort],[data-workshop-download],[data-favorite-import],[data-reminder-save],[data-reminder-dismiss],[data-game-stop],[data-profile-recover],[data-mod-save-activate],[data-mod-membership],#theme';
const editorSaves=new Set(),editorLocks={modProfileSave:{controls:'#mod-workspace-select,[data-profile-sort],[data-mod-profile-reread],[data-mod-profile-delete],[data-profiles-refresh="modProfiles"],[data-mod-membership],[data-mod-save-activate],#mod-profile-name,[data-profile-mod-up],[data-profile-mod-down],[data-profile-mod-remove],[data-profile-mod-add],[data-mod-profile-new],[data-mod-profile-open],[data-mod-profile-copy],[data-mod-profile-reread],[data-sort-apply="profile"]',save:'[data-mod-profile-save],[data-mod-save-activate]'},saveSave:{controls:'[data-mod-up],[data-mod-down],[data-mod-remove],[data-mod-add],[data-save-open],[data-save-reread],#save-profile-select,[data-save-profile-load],[data-sort-apply="save"]',save:'[data-save-write]'},memorySave:{controls:'#memory-number,#memory-range,[data-memory],[data-reload="memory"]',save:'[data-save-memory]'},settingsSave:{controls:'[data-setting],[data-reload="settings"]',save:'[data-save-settings]'},serverUpsert:{controls:'#server-form input,#server-form select,[data-server-close]',save:'[data-server-write]'},playtimeSettings:{controls:'#reminder-minutes',save:'[data-reminder-save]'}};
function disable(){document.querySelectorAll('button,input,select').forEach(el=>{if(el.matches('[data-page],[data-safe],#confirm-no,#confirm-yes'))return;const modLock=(state.loading.modProfile||state.modSorting)&&el.matches(editorLocks.modProfileSave.controls+','+editorLocks.modProfileSave.save)&&!(state.loading.modProfile&&el.id==='mod-workspace-select'),unsafe=el.matches(unsafeControls)||mutationActions.has(el.dataset.action),saving=Object.entries(editorLocks).some(([action,lock])=>editorSaves.has(action)&&el.matches(lock.controls+','+lock.save));if(modLock||(state.busy||state.coreApplying)&&unsafe||saving||state.coreApplying&&el.matches("input,select")){if(!el.disabled){el.dataset.busyDisabled='1';el.disabled=true;}}else if(el.dataset.busyDisabled){delete el.dataset.busyDisabled;el.disabled=false;}const ownSave=Object.entries(editorLocks).some(([action,lock])=>editorSaves.has(action)&&el.matches(lock.save));if(ownSave){if(!el.dataset.saveLabel)el.dataset.saveLabel=el.textContent;el.textContent='保存中…';}else if(el.dataset.saveLabel){el.textContent=el.dataset.saveLabel;delete el.dataset.saveLabel;}if(state.status?.gameVersion?.value?.startsWith('41.')&&el.matches('[data-component]:not([data-operation=uninstall]),[data-core-install],[data-addons-install]')){el.disabled=true;el.title='当前内置组件需要 B42';}if(el.matches('#launch,[data-profile-launch]'))el.disabled=el.dataset.targetLaunchable==='false'||state.busy||!!state.coreApplying||!state.status?.validGame||!!state.status?.running||launchPending();if(el.matches('[data-profile-recover]'))el.disabled=state.busy||!!state.coreApplying;if(el.matches('[data-java-allow],[data-java-deny]'))el.disabled=state.busy||!!state.coreApplying||!state.java?.supported||state.javaPending.has(el.dataset.javaAllow||el.dataset.javaDeny);});}
async function persistEditor(action,payload,complete,options={}){if(editorSaves.has(action)||state.busy)return null;editorSaves.add(action);disable();try{const result=await run(action,payload,options);if(result&&complete)await complete(result);return result;}finally{editorSaves.delete(action);disable();}}
document.addEventListener('cancel',e=>{if(e.target.id==='server-editor'&&editorSaves.has('serverUpsert'))e.preventDefault();},true);
function acceptStatus(data){if(data.components&&independentCore(data.components))data={...data,components:data.components.filter(c=>c.id!=='base')};const previous=state.status;state.status=data;if(previous&&(previous.gamePath!==data.gamePath||previous.configStamp!==data.configStamp)){state.buddyPreview=null;state.buddyEpoch++;if(previous.gamePath!==data.gamePath){state.buddyResult=null;state.buddyInstall=null;}renderBuddy();}if(JSON.stringify(previous?.buddyRecovery)!==JSON.stringify(data.buddyRecovery))renderBuddy();$('#game-state').textContent=data.running?'游戏正在运行':data.validGame?'已找到游戏':'未选择游戏';$('#connection').textContent=preview?'界面测试预览':'本机服务已连接';document.documentElement.classList.toggle('light',data.theme==='light');$('#theme').textContent=data.theme==='light'?'黑金主题':'浅蓝主题';if(previous&&previous.configStamp!==data.configStamp){if(state.saveDirty&&state.route==='saves')notify('外部配置状态已变化，未保存的存档模组顺序已保留。保存时将校验原始快照；若存档发生变化，请重新读取。');for(const name of ['memory','settings']){if(state[name+'Dirty']){state[name+'Conflict']=true;if(state.route===name){notify('外部配置已变化，未保存编辑已保留。请重新读取后再保存。');const save=$('[data-save-'+name+']');if(save)save.disabled=true;}}else {state[name]=null;if(state.route===name)load(name);}}if(state.route==='shaders'){state.shaders=null;load('shaders');}}task(data.task||{active:false});syncLaunchRecovery();if(state.route==='help'&&previous?.running!==data.running)paintLoadingOptions();if(['javaMods','transfer'].includes(state.route)&&previous?.running!==data.running)render();if(!previous||['home','components'].includes(state.route)&&JSON.stringify({validGame:previous.validGame,running:previous.running,components:previous.components,launch:state.route==='home'?previous.launch:null,hardware:state.route==='home'?previous.hardware:null})!==JSON.stringify({validGame:data.validGame,running:data.running,components:data.components,launch:state.route==='home'?data.launch:null,hardware:state.route==='home'?data.hardware:null}))render();}
async function refresh(){try{acceptStatus(await request('status'));void checkStartupUpdate();}catch(e){notify(e.message);if(!state.status)render();}}
async function load(name,force=false){if(name==='steamSearch'){return searchWorkshop(state.workshopPage);}if(name==='steamDetails'){return workshopDetails(state.workshopDetailId);}if(name==='serverHall'){return loadHall();}if(state.loading[name])return;if(name==='modCatalog')invalidateCovers();const saveListTicket=name==='saves'?{epoch:state.experienceEpoch||0}:null;if(saveListTicket)state.savesReadTicket=saveListTicket;const origin=state.route,javaRevision=state.javaRevision||0,visible=()=>state.route===name||name==='saves'&&state.route==='transfer'||name==='modCatalog'&&['saves','mods','workshop','components','transfer'].includes(state.route)||name==='modProfiles'&&['saves','mods'].includes(state.route)||['steamConnection','steamSubscriptions','steamSearch','steamDetails'].includes(name)&&state.route==='workshop'||['serverFavorites','serverHall'].includes(name)&&state.route==='servers'||name==='playtime'&&state.route==='home'||name==='java'&&state.route==='help'||name==='logs'&&['maintenance','help'].includes(state.route);state.loading[name]=true;delete state.errors[name];if(visible())render();try{const data=await request({memory:'memoryGet',settings:'settingsGet',shaders:'shadersGet',logs:'logs',help:'help',saves:'savesList',modCatalog:'modCatalog',servers:'serverList',java:'javaList',modProfiles:'modProfiles',serverFavorites:'serverFavorites',serverHall:'serverHall',steamConnection:'steamConnection',steamSubscriptions:'steamSubscriptions',playtime:'playtimeGet'}[name],name==='modCatalog'&&force?{refresh:true}:{});if(name==='java'&&javaRevision!==(state.javaRevision||0))return;if(saveListTicket&&(state.savesReadTicket!==saveListTicket||saveListTicket.epoch!==(state.experienceEpoch||0)))return;state[name]=data;if(name==='memory'&&!state.memoryDirty)state.memoryEdit=data.currentGiB;if(name==='settings'&&!state.settingsDirty)state.settingEdits={};}catch(e){if(saveListTicket&&(state.savesReadTicket!==saveListTicket||saveListTicket.epoch!==(state.experienceEpoch||0)))return;if(name!=='java'||javaRevision===(state.javaRevision||0)){state.errors[name]=e.message;notify(e.message,origin);}}finally{if(saveListTicket&&(state.savesReadTicket!==saveListTicket||saveListTicket.epoch!==(state.experienceEpoch||0))){if(state.savesReadTicket===saveListTicket)state.loading[name]=false;return;}state.loading[name]=false;if(visible())render();if(['java','help'].includes(name)&&state.javaOpen&&state.route==='help'&&state.java&&!state.loading.java)focusJavaPending();if(name==='playtime')maybeShowReminder();}}
function route(name){if(!titles[name])name='home';const changed=state.route!==name;if(changed)thumbGeneration++;state.route=name;location.hash=name;render();if(name==='diagnostics')loadLogAnalysisStatus();if(name==='feedback')window.ozlFeedback?.load();if(name==='help')loadLoadingOptions();if(name==='workshop'&&state.workshopTab==='search'&&!state.steamSearch&&!state.loading.steamSearch&&!state.errors.steamSearch)searchWorkshop(1);if(changed)document.querySelector('main').scrollTop=0;if(name==='help'&&!state.java)load('java');if(['saves','mods'].includes(name)){if(name==='saves'&&!state.saves)load('saves');if(!state.modCatalog)load('modCatalog');if(!state.modProfiles)load('modProfiles');}if(['workshop','components'].includes(name)&&!state.modCatalog)load('modCatalog');if(name==='home'&&!state.playtime)load('playtime');if(name==='servers'&&!state.servers)load('servers');if(['memory','settings','shaders','help'].includes(name)&&!state[name])load(name);}
function heading(actions=''){return`<div class="heading"><div><div class="eyebrow">OZL / ${state.route.toUpperCase()}</div><h1>${titles[state.route]}</h1></div><div class="actions">${actions}</div></div>`+readPending()+(state.route==='help'?'<div class="contact-row" data-contact><span>官方 QQ 群 <strong>1124858561</strong></span><div class="actions"><button data-action="openContact">加入 QQ 群 ↗</button><button data-contact-copy>复制群号</button></div><span data-contact-feedback role="status" aria-live="polite"></span></div>':'');}
document.addEventListener('click',async e=>{if(!e.target.closest('[data-contact-copy]'))return;const feedback=$('[data-contact-feedback]');try{await navigator.clipboard.writeText('1124858561');if(feedback)feedback.textContent='群号已复制';}catch{if(feedback)feedback.textContent='复制失败，请手动复制群号 1124858561';}});
function placeholder(name){return`<div class="empty"><h3>${state.errors[name]?'读取失败':'正在读取当前配置…'}</h3><p>${esc(state.errors[name]||'请稍候。')}</p>${state.errors[name]?`<button data-reload="${name}" class="secondary">重试读取</button>`:''}</div>`;}
const button=(label,action,extra='',cls='')=>`<button class="${cls}" data-action="${action}" ${extra}>${esc(label)}</button>`;
const addonSelection=new Set();
function buddyUntouched(value){return value?.untouchedFiles?.length?'<details class="small section" data-buddy-untouched><summary>保留的备用启动脚本 · '+value.untouchedFiles.length+' 个</summary><ul>'+value.untouchedFiles.map(name=>'<li>'+esc(name)+(value.untouchedReasons?.[name]?' · '+esc(value.untouchedReasons[name]):'')+'</li>').join('')+'</ul></details>':'';}
function buddyResultMessage(result){return result?.cleanupCompleted&&result.steamVerified===false?'游戏文件参数已清理；Steam 启动项尚未处理。':result?.message;}
async function installComponentsBatch(batch){
 for(let index=0;index<batch.length;index++){
  const entry=batch[index];if(entry.warning&&!entry.warningConfirmed){if(!await confirm('安装前说明',entry.warning))continue;entry.warningConfirmed=true;}
  const result=await run('componentsAction',entry.payload);
  if(!result&&state.buddyInstall){state.buddyInstall={batch:batch.slice(index)};renderBuddy();return;}
 }
 await load('java');if(state.java?.jars?.some(j=>j.decision==null))openJavaReview();
}
function memoryLowAvailable(){const m=state.memory;return !!m&&m.availableGiB<Math.max(m.currentGiB,m.recommendedGiB,Number(state.memoryEdit)||0)+2;}
function memoryAdvice(){return '<div data-memory-advice class="notice" '+(memoryLowAvailable()?'':'hidden')+'>当前其他程序占用较高；下次启动前可关闭不需要的程序。当前可用内存不会降低设置上限。</div>';}
function buddyRecoveryState(){return state.status?.buddyRecovery?.pending?(state.buddyPreview?.recovery||state.status.buddyRecovery):state.status?.buddyRecovery;}
function buddyPanel(){
 const p=state.buddyPreview,r=state.buddyResult,recovery=buddyRecoveryState(),labels={completed:'旧参数已接管',partial:'旧参数已接管 · Steam 重新打开未完成',cancelled:'接管已取消',failed:'接管未完成',confirmationRequired:'需要单独确认退出 Steam',recoveryRequired:'需要恢复或人工处理',recovered:'接管前原值已恢复',unchanged:'未发现可接管参数'};
 const unverifiedCleanup=r?.cleanupCompleted&&r.steamVerified===false;
 return `<article class="card buddy-panel section" data-buddy-panel><div class="row"><div><h2>启动参数</h2></div><button data-buddy-scan ${state.buddyScanning?'disabled':''}>${state.buddyScanning?'正在扫描…':'扫描启动参数'}</button></div>${p&&!p.recovery?`<section data-buddy-summary class="section"><p>${esc(p.message)}</p>${p.locations?.length?'<p class="key">'+p.locations.map(esc).join(' · ')+'</p>':''}${buddyUntouched(p)}${p.supported&&p.hasConflicts&&!recovery?.pending?'<button data-buddy-takeover class="primary section">接管旧启动参数</button>':''}</section>`:''}${state.buddyError?'<p class="error-text">'+esc(state.buddyError)+'</p>':''}${state.buddyActive?'<p data-buddy-stage role="status" aria-live="polite">'+esc(state.buddyStage||'正在准备接管')+'</p>':''}${r?`<section data-buddy-result data-status="${esc(r.status)}" class="section"><span class="badge ${['completed','recovered'].includes(r.status)?'status-positive':['failed','recoveryRequired','partial'].includes(r.status)?'status-warning':'status-neutral'}">${esc(unverifiedCleanup?'游戏文件参数已清理 · Steam 待处理':labels[r.status]||'请核对任务结果')}</span><p>${esc(buddyResultMessage(r))}</p>${buddyUntouched(r)}${r.cleanupCompleted?'<p class="small">旧参数已清理，可继续安装所需组件。</p>':''}${r.steamExitRequested?'<p class="small">'+(r.steamReopenRequested?'已发送 Steam 重启请求，请完成登录。':'Steam 重新打开请求未完成；请手动打开同一客户端，刷新后重试原安装。')+'</p>':''}${r.rollbackCompleted?'<p class="small">本轮自有改动已恢复；请重新扫描后决定是否接管。</p>':''}${r.recoveryPending?'<p class="status-warning">迁移仍有待恢复记录；请核对下面的恢复说明。</p>':''}${r.cleanupCompleted&&!r.recoveryPending&&state.buddyInstall?'<button data-buddy-continue class="secondary section">继续原组件安装</button>':''}</section>`:''}${recovery?.pending?`<section data-buddy-recovery class="notice section"><h3>有待恢复的接管记录</h3><p>${esc(recovery.message)}</p><p class="key">${(recovery.locations||[]).map(esc).join(' · ')}</p>${recovery.supported?'<button data-buddy-recover class="secondary section">恢复本轮自有改动</button>':''}<button data-refresh>刷新恢复状态</button></section>`:''}</article>`;
}
function renderBuddy(){const panel=$('[data-buddy-panel]');if(panel)panel.outerHTML=buddyPanel();else if(['home','components'].includes(state.route)&&state.status?.buddyRecovery?.pending&&!state.busy)render();disable();}
async function scanBuddy(){if(state.busy||state.buddyScanning)return;const epoch=++state.buddyEpoch,game=state.status?.gamePath;state.buddyScanning=true;state.buddyPreview=null;state.buddyError='';renderBuddy();try{const p=await request('buddyPreview');if(epoch!==state.buddyEpoch||game!==state.status?.gamePath)return;state.buddyPreview=p;if(p.recovery)state.status={...state.status,buddyRecovery:{...p.recovery,supported:false,stamp:''}};}catch(e){if(epoch===state.buddyEpoch){state.buddyError=e.message;}}finally{state.buddyScanning=false;renderBuddy();}}
async function applyBuddy(recover=false){
 if(state.busy||state.buddyActive)return;const p=recover?buddyRecoveryState():state.buddyPreview,game=state.status?.gamePath,epoch=state.buddyEpoch;
 const current=()=>game===state.status?.gamePath&&epoch===state.buddyEpoch&&p?.stamp===(recover?buddyRecoveryState():state.buddyPreview)?.stamp;
 if(!p?.supported||!p.stamp)return;
 if(!await confirm(recover?'恢复本轮接管改动？':'接管已识别的旧 Buddy 参数？',(recover?'仅恢复本轮事务仍可安全恢复的自有改动。':'将清理以下位置中已识别的旧 Buddy 参数，保留其他启动参数并保存私有恢复资料。')+'\n\n'+(p.locations||[]).join('\n')+'\n\n组件安装、Java 文件授权和游戏启动须分别决定。'))return;
 if(!current()){notify('所选游戏或预览已变化，请重新扫描。');return;}
 let exit=false;if(p.requiresSteamExit){if(!await confirm('正常退出并重新打开 Steam？','这将一次性请求当前已核实的 Steam 客户端正常退出，影响正在使用此客户端的功能，最多等待 30 秒。文件处理后请求重新打开同一客户端，登录与就绪状态须在 Steam 中确认。'))return;exit=true;}
 if(!current()){notify('所选游戏或预览已变化，请重新扫描。');return;}
 state.buddyActive=true;state.buddyResult=null;state.buddyStage=recover?'正在准备恢复':'正在准备接管';renderBuddy();
 try{state.buddyResult=await run(recover?'buddyRecover':'buddyTakeover',{stamp:p.stamp,confirmed:true,steamExitConfirmed:exit});}finally{state.buddyActive=false;state.buddyPreview=null;state.buddyEpoch++;await refresh();renderBuddy();}
}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;if(b.hasAttribute('data-buddy-scan')){await scanBuddy();return;}if(b.hasAttribute('data-buddy-takeover')){await applyBuddy();return;}if(b.hasAttribute('data-buddy-recover')){await applyBuddy(true);return;}if(b.hasAttribute('data-buddy-continue')&&state.buddyInstall&&state.buddyResult?.cleanupCompleted&&!state.buddyResult?.recoveryPending){const install=state.buddyInstall;state.buddyInstall=null;await installComponentsBatch(install.batch||[{payload:install,warning:'',warningConfirmed:true}]);renderBuddy();}});
function localDate(value){const date=new Date(value);return Number.isFinite(date.getTime())?date.toLocaleString('zh-CN',{hour12:false}):String(value||'未查询');}
function applyRichDescription(root){root.querySelectorAll('[data-rich-size]').forEach(el=>{const n=Number(el.dataset.richSize);if(n>=0.75&&n<=1.8)el.style.fontSize='max(12px, '+n+'em)';if(/^rgb\(\d{1,3},\d{1,3},\d{1,3}\)$/.test(el.dataset.richColor||''))el.style.color=el.dataset.richColor;});}
function richDescription(value){let color='',size='1',result='';const pieces=String(value||'').slice(0,65536).split(/(<[^>]{0,160}>)/g);for(const p of pieces){let m;if(/^<(?:LINE|BR)>$/i.test(p)){result+='<br>';continue;}if((m=p.match(/^<RGB:\s*([0-9.]+)\s*,\s*([0-9.]+)\s*,\s*([0-9.]+)\s*>$/i))){const values=m.slice(1).map(Number);if(values.every(v=>Number.isFinite(v)&&v>=0&&v<=1))color='rgb('+values.map(v=>Math.round(v*255)).join(',')+')';continue;}if((m=p.match(/^<SIZE:\s*([0-9.]+)\s*>$/i))){const v=Number(m[1]);if(Number.isFinite(v)&&v>=0.75&&v<=1.8)size=String(v);continue;}if(/^<TEXT>$/i.test(p)){color='';size='1';continue;}if(p)result+='<span data-rich-color="'+color+'" data-rich-size="'+size+'">'+esc(p).replace(/\r?\n/g,'<br>')+'</span>';}return result;}
function componentUpdate(c){const rows=state.updates?.items?.filter(r=>(c.manifests||[{item:c.item}]).some(m=>m.item===r.item))||[];if(!rows.length)return '尚未在线检查';return rows.some(r=>r.state==='available')?'工坊有更新':rows.every(r=>r.state==='latest')?'工坊版本一致':rows.some(r=>r.state==='missing')?'本地待下载':'在线版本未知';}
const coreComponentIds=['buddy','viewpoint','chinese'];
function independentCore(list){return list.some(c=>c.id==='buddy'||c.id==='viewpoint');}
function componentCard(c){
 const update=componentUpdate(c),needsReapply=c.needsReapply||c.bridgeRetirementNeeded,hasUpdate=update==='工坊有更新'||needsReapply;
 const action=c.configured?'uninstall':'install',label=c.configured?'关闭':c.downloaded?'启用':'安装并启用';
 return `<article class="card component-card" data-card="${esc(c.id)}">${workshopCover({id:c.item||items[c.id]})}<div class="component-body"><div class="component-title"><h3>${esc(c.title)}</h3><span class="badge ${c.configured?'status-positive':'status-neutral'}">${c.configured?'已启用':c.downloaded?'未启用':'尚未安装'}</span></div><p class="component-description">${esc(c.description)}</p><p class="component-version small">版本 ${esc(c.version||'未声明')} <span class="${hasUpdate?'status-warning':'status-neutral'}">· ${needsReapply?'需要重新应用':esc(update)}</span></p>${c.id==='shoot'&&c.bridgeRetirementNeeded?'<p class="notice small status-warning">旧本地瞄准补丁待清理，请点击“重新应用”；保留作者枪械与个人校准。</p>':''}<div class="actions component-actions"><button class="${c.configured?'secondary':'primary'}" aria-label="${label} ${esc(c.title)}" data-component="${esc(c.id)}" data-operation="${action}">${label}</button>${c.optional?'<button class="addon-toggle" data-addon-select="'+esc(c.id)+'" aria-pressed="'+addonSelection.has(c.id)+'">'+(addonSelection.has(c.id)?'已选择':'选择安装')+'</button>':''}${needsReapply?'<button data-component="'+esc(c.id)+'" data-operation="install">重新应用</button>':''}${c.id==='shoot'?'<button data-shoot-usage aria-expanded="'+state.shootHelp+'">使用说明</button>':''}</div><details><summary>来源、更新与维护</summary><p class="small">${c.downloaded?'安装源已找到':c.downloaded===false?'安装源未找到':'安装源待读取'} · ${esc(c.runtimeState||(c.configured?'下次启动启用':'未启用'))}</p><p class="small">${c.sourceState==='unrecorded'?'尚无安装记录':c.sourceState==='same'?'安装来源未变':'来源状态：'+esc(c.sourceState||'待核对')}</p><p class="small">组件更新后，如出现 Java 授权提示，请允许所需文件。</p><p class="small">兼容性：${esc(c.compatibility||'请核对作者说明')}</p>${c.warning?'<p class="notice small status-warning">'+esc(c.warning)+'</p>':''}<p class="key">来源指纹：${esc(c.sourceFingerprint||'未记录')}</p><p class="small">${esc(c.retirement||'可在此独立安装、更新或卸载。')}</p><div class="actions"><button data-component="${esc(c.id)}" data-operation="update">下载更新并应用</button>${button('原作者工坊 ↗','openOfficial',`data-item="${esc(c.item)}"`)}</div></details></div>${c.id==='shoot'&&state.shootHelp?shootUsage():''}</article>`;
}
function components(){
 const list=state.status?.components;if(!list)return heading()+placeholder('status');
 const modern=independentCore(list),coreIds=modern?coreComponentIds:['base','chinese'],core=coreIds.map(id=>list.find(c=>c.id===id)).filter(Boolean),addons=list.filter(c=>!coreIds.includes(c.id)&&c.id!=='base');
 return heading((modern?'':'<button data-core-install class="primary">安装核心视角＋汉化</button>')+'<button data-component-discover class="primary">下载并识别 / 安装组件</button><button data-components-manage>管理已下载的 Java 组件</button><button data-check-updates>检查更新（不下载）</button><button data-refresh>刷新状态</button>')+(state.buddyInstall||state.status?.buddyRecovery?.pending?buddyPanel():'')+`<section class="component-intro"><div><span class="eyebrow">按你的玩法选择</span><h2>核心组件</h2></div><span class="small">${core.filter(c=>c.configured).length} / ${core.length} 已配置</span></section>${state.updates?'<p class="small section">上次检查：'+esc(localDate(state.updates.checkedUtc))+' · '+esc(state.updates.message||'')+'</p>':''}<div class="grid core-components" data-core-components>${core.map(componentCard).join('')}</div><details class="optional-components" data-optional-components data-disclosure="optional-components"><summary><span><strong>可选附加内容</strong><span class="small">模型、枪械与兼容修复 · ${addons.length} 项</span></span><span class="small">按需展开</span></summary><div class="row section"><h3>选择需要的扩展</h3><button data-addons-install class="primary" ${addonSelection.size===0?'disabled':''}>安装所选（${addonSelection.size}）</button></div><div class="grid">${addons.map(componentCard).join('')}</div></details>`;
}
function shootUsage(){return`<section class="drawer shoot-help" data-shoot-help><h3>作者枪械使用说明</h3><p>默认 O 开关第一人称，右键瞄准；Ctrl+V 切换腰射 / 精瞄。实际按键以游戏内绑定为准。</p><p>联机使用时，服务器需启用 Shoot to Thrill，客户端安装枪械组件并完成 Java 授权。</p><p class="small">旧本地瞄准补丁已退役。主动安装或更新枪械组件时清理旧补丁，保留作者枪械、Viewpoint、汉化和个人校准；游戏中已加载的旧 Java 会在你下次完全重启游戏后退出。</p></section>`;}
function memory(){
 const title=heading('<button data-reload="memory">重新读取</button>');if(!state.memory)return title+placeholder('memory');
 const m=state.memory,valid=Number.isInteger(state.memoryEdit)&&state.memoryEdit>=1&&state.memoryEdit<=m.maximumGiB;
 const notices=(state.memoryConflict?'<div class="notice">外部配置已变化。未保存的编辑已保留；请重新读取后再保存。</div>':'')+(m.currentGiB>m.maximumGiB?'<div class="notice">现有配置超过设置上限，按实际值显示；请选择上限内的值后保存。</div>':'');
 const metrics=[['当前 Java 堆',m.currentGiB],['系统可见总内存',m.totalGiB],['当前系统可用',m.availableGiB],['基础推荐',m.recommendedGiB],['设置上限（按总量）',m.maximumGiB]];
 return title+notices+`<section class="card memory-allocation"><div class="row"><div><h3>分配给游戏的内存</h3><p>推荐 ${esc(m.recommendedGiB??'—')} GiB · 起始建议，重度模组可手动调整</p></div><div class="actions"><input id="memory-number" type="number" min="1" max="${esc(m.maximumGiB)}" step="1" value="${esc(state.memoryEdit)}" style="width:92px" aria-label="内存 GiB"><span>GiB</span></div></div><input id="memory-range" type="range" min="1" max="${esc(m.maximumGiB)}" step="1" value="${esc(state.memoryEdit)}" aria-label="内存滑块"><div class="presets">${[8,12,16].map(v=>`<button data-memory="${v}" ${v>m.maximumGiB?'disabled':''}>${v} GiB</button>`).join('')}</div><div class="row save-row"><p class="small">${state.memoryDirty?'有未保存修改 · 下次启动生效':'仅调整 Java 堆，不修改分页或显存。'}</p><button data-save-memory class="primary" ${!state.memoryDirty||state.memoryConflict||!valid?'disabled':''}>保存内存设置</button></div></section><section class="metrics memory-metrics" aria-label="系统与分配指标">${metrics.map(([label,value])=>`<div class="metric"><p>${label}</p><div class="value">${esc(value??'—')} <small>GiB</small></div></div>`).join('')}</section>`+memoryAdvice();
}
function settings(){if(!state.settings)return heading()+placeholder('settings');const s=state.settings,groups=[...new Set(s.rows.map(r=>r.group||'其他参数'))];return heading('<button data-reload="settings">重新读取</button>')+(state.settingsConflict?'<div class="notice">外部配置已变化，未保存修改已保留。请重新读取后再保存。</div>':'')+(s.warnings||[]).map(w=>`<div class="notice">${esc(w)}</div>`).join('')+(s.rows.length?groups.map(group=>`<div class="card section"><h2>${esc(group)}</h2>${s.rows.filter(r=>(r.group||'其他参数')===group).map(r=>{const value=state.settingEdits[r.key]??r.value,choices=Array.isArray(r.choices)?r.choices:[];const options=choices.length?choices:r.kind==='bool'||r.kind==='boolean'?['true','false']:[];const control=options.length?`<select data-setting="${esc(r.key)}" ${!r.editable?'disabled':''}>${options.map(v=>{let val=typeof v==='object'?v.value:v;return`<option value="${esc(val)}" ${String(val)===String(value)?'selected':''}>${esc(typeof v==='object'?v.name||v.label||val:val==='true'?'启用':val==='false'?'关闭':val)}</option>`;}).join('')}</select>`:`<input data-setting="${esc(r.key)}" value="${esc(value)}" ${!r.editable?'disabled':''} aria-label="${esc(r.name)}">`;return`<div class="setting"><label><h3>${esc(r.name||r.key)}</h3><p class="small">${esc(r.description)}</p><div class="key">${esc(r.key)}${!r.editable?' · 只读':''}</div></label>${control}</div>`;}).join('')}</div>`).join(''):`<div class="empty">当前目录没有可编辑的 Viewpoint 配置，请先检查组件。</div>`)+`<div class="row save-row"><p>${state.settingsDirty?'有未保存修改':'未知参数保留原值，只读显示。'}</p><button data-save-settings class="primary" ${!state.settingsDirty||state.settingsConflict?'disabled':''}>保存更改</button></div><details class="card section"><summary>DLSS 实验功能</summary><p>实验功能需要有效的适配文件。出现异常时可关闭并恢复原始光影。</p><div class="actions section">${button('关闭实验 DLSS','dlss','data-operation="disable"')}${button('启用实验 DLSS','dlss','data-operation="enable"')}</div></details>`;}
function shaders(){if(!state.shaders)return heading()+placeholder('shaders');const s=state.shaders;return heading(button('导入光影','importShader','', 'primary')+button('光影目录','open','data-target="shaders"'))+`<div class="notice">当前选择：${esc(s.packs.find(p=>p.id===s.selected)?.name||s.selected||'未配置')}</div><div class="grid">${s.packs.map(p=>`<div class="card"><span class="badge">${p.id===s.selected?'当前使用':p.valid?'文件就绪':'文件不可用'}</span><h2 class="section">${esc(p.name)}</h2><p class="description">${esc(p.description)}</p><p class="key section">${esc(p.file)}</p><button class="${p.id===s.selected?'secondary':'primary'} section" data-shader="${esc(p.id)}" ${!p.valid||p.id===s.selected?'disabled':''}>${p.id===s.selected?'已选择':'使用此光影'}</button></div>`).join('')}</div><div class="card section"><h3>Eclipse 适配说明</h3><p>使用专用适配文件。修改后下次启动生效，导入和选择不会自动启动游戏。</p></div>`;}
function maintenance(){return heading()+buddyPanel()+`<div class="card maintenance"><div><span class="badge">多人本地数据</span><h2 class="section">清除多人缓存</h2><p>默认仅清除地图区块，保留角色、车辆和地图标记。不生成清理备份。</p><label><input type="checkbox" data-cache-all ${state.cacheAll?'checked':''}> 彻底清除，包括角色缓存、车辆、地图标记等全部多人本地数据</label><p class="small">彻底清除会删除本机 Saves/Multiplayer 的全部内容。服务器端存档保留；重新连接时同步服务器数据，本地地图标记等内容可能无法恢复。</p></div><button data-maintenance="cache" class="secondary">清除多人缓存</button></div><div class="card maintenance"><div><span class="badge">需要重新下载</span><h2 class="section">修复游戏工坊文件</h2><p>清除本游戏的工坊模组文件，并通过 Steam 重新下载已订阅模组。</p></div><button data-maintenance="workshop" class="secondary">工坊重下载</button></div><div class="card"><h2>人工恢复与诊断</h2><p>修复失败时，先查看任务错误及备份，再决定后续处理。</p><div class="actions section">${button('查看日志','logs')}${button('打开备份目录','open','data-target="backups"')}${button('打开游戏目录','open','data-target="game"')}</div>${logPanel()}</div>`;}
document.addEventListener('change',e=>{if(e.target.matches('[data-cache-all]'))state.cacheAll=e.target.checked;});
function logPanel(){return state.logs?`<div class="section"><div class="actions"><input id="log-filter" placeholder="按时间 / 阶段 / 错误筛选" aria-label="日志筛选">${button('日志目录','open','data-target="logs"')}</div><pre class="log section" id="log-output">${esc(logText())}</pre></div>`:'';}
function logText(){const logs=state.logs;return typeof logs==='string'?logs:logs?.text||logs?.content||logs?.message||(Array.isArray(logs?.entries)?logs.entries.join('\n')||'当前工具会话尚无任务记录。':JSON.stringify(logs,null,2));}
function help(){return heading()+loadingOptionsPanel()+`<div class="help-list"><p>OZL · Orange zombiod launcher</p><details><summary>存档与本机多人缓存</summary><p>选择存档后可继续游戏、调整模组或备份。删除会永久移除所选存档；Multiplayer 列表显示本机多人存档。</p></details><details><summary>启动与封面场景</summary><p>在首页选择“继续单人存档”或“连接服务器”，再选择目标并启动。首页原封面排第一，另外三个场景每 8 秒轮换；可上一张、下一张、选择或暂停。悬停、焦点操作、后台及减少动态效果时暂停自动轮换。</p></details><details open><summary>第一次使用与 Java 模组授权</summary><p>选择游戏目录 → 安装所需组件 → 完成 Java 授权 → 启动游戏。新增 Java 文件可在游戏的授权窗口处理。</p><button data-java-open>检查 Java 文件授权</button><p>${esc(state.help?.message||'授权只应给予你信任的模组。请根据当前游戏版本的授权提示完成。')}</p></details><details><summary>如何开启 Viewpoint 视角？</summary><p>安装后启动游戏，按 O 开关第一人称，Shift+O 切换第一／第三人称。画面设置页可查看和修改快捷键。</p><div class="actions section"><button data-page="settings">查看当前快捷键</button>${button('读取日志','logs')}</div></details><details><summary>Buddy、枪械与原始瞄准</summary><p>基础组件包含 Viewpoint 与 Buddy 的依赖关系。Buddy 保留原生启动方式；枪械卡片的“使用说明”可查看作者操作与快捷键。</p></details><details><summary>错误记录与文件恢复</summary><p>查看实际错误阶段。备份与恢复文件保持在工具指定目录；工坊修复失败后应先处理失败原因，再启动游戏。</p><div class="actions section">${button('读取日志','logs')}${button('备份目录','open','data-target="backups"')}</div>${logPanel()}</details><details><summary>版权、组件作者与原始来源</summary><p>免费第三方辅助工具，非游戏官方产品。工坊封面均来自原作者，完整比例展示，未经重绘。组件与图像版权属于各自作者；私人工具展示不代表获得公开再分发许可。</p><div class="actions section">${Object.entries(items).map(([id,item])=>button(state.status?.components?.find(c=>c.id===id)?.title||id+' ↗','openOfficial',`data-item="${item}"`)).join('')}${button('完整声明','open','data-target="notices"')}</div></details></div>`;}
function syncLaunchRecovery(){const previous=$('[data-launch-recovery]');if(state.route!=='maintenance'||!state.status?.launch?.pending||state.status.running){previous?.remove();return;}if(previous){previous.querySelector('button').disabled=state.busy||!!state.coreApplying;return;}const block=document.createElement('div');block.className='notice section launch-status';block.setAttribute('data-launch-recovery','');block.textContent='上次启动清理未完成';const recover=document.createElement('button');recover.className='secondary section';recover.setAttribute('data-profile-recover','');recover.textContent='修复启动状态';recover.disabled=state.busy||!!state.coreApplying;block.append(document.createElement('br'),recover);const heading=$('#page > .heading');if(heading)heading.after(block);else $('#page').prepend(block);}
function render(){const page=$('#page');page.dataset.route=state.route;page.innerHTML=({home,saves,servers,mods,workshop,components,shaders,settings,memory,maintenance,help,diagnostics,feedback,versions,transfer,javaMods}[state.route])();if(state.route==='help'){page.insertAdjacentHTML('afterbegin',coreUpdatePanel());if(!state.coreUpdate&&!state.coreStatusRequested){state.coreStatusRequested=true;coreRequest('coreUpdateStatus');}}if(state.route==='home'&&state.status?.hardware){const hardware=state.status.hardware,summary=document.createElement('div');summary.className='hardware-summary';summary.setAttribute('aria-label','本机硬件');for(const [label,value] of [['GPU',hardware.gpu||'未知'],['专用显存',hardware.vramGiB==null?'未知':(Math.round(Number(hardware.vramGiB)*10)/10)+' GiB'],['驱动',hardware.driver||'未知']]){const entry=document.createElement('span'),name=document.createElement('small');name.textContent=label+'　';entry.append(name,document.createTextNode(String(value)));summary.append(entry);}if(hardware.warning){const warning=document.createElement('small');warning.className='hardware-warning';warning.textContent=hardware.warning;summary.append(warning);}page.append(summary);}if(state.route==='help'&&state.help){const h=state.help;for(const [label,text] of [['当前安装与授权说明',h.body],['完整版权与第三方声明',h.rights],['人工文件恢复步骤',h.recovery],['实验 DLSS 来源说明',h.dlss]]){if(text){const block=document.createElement('details');block.className='card section';const summary=document.createElement('summary');summary.textContent=label;const content=document.createElement('p');content.className='full-text';content.textContent=text;block.append(summary,content);page.append(block);}}}if(['home','components'].includes(state.route)){const entry=document.createElement('div');entry.className='card section java-entry';entry.innerHTML='<div class="row"><div><h3>游戏启动前 · Java 文件授权</h3><p class="small">查看工具管理的 Java 文件、来源与完整 SHA-256，再逐个决定。</p></div><button data-java-open>检查 Java 授权</button></div>';page.append(entry);}if(state.route==='help'){const section=document.createElement('details');section.id='java-help';section.className='card section';section.open=state.javaOpen===true;section.innerHTML='<summary>游戏启动前 · Java 文件授权</summary>'+javaPanel();page.append(section);}syncLaunchRecovery();applyRichDescription(document);if(state.route==='home'&&state.status?.buddyRecovery?.pending)page.insertAdjacentHTML('afterbegin',buddyPanel());observeCovers();observeThumbnails();document.querySelectorAll('nav [data-page]').forEach(b=>{b.classList.toggle('active',b.dataset.page===state.route);if(b.dataset.page===state.route)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});disable();}
function confirm(title,body){return new Promise(resolve=>{const dialog=$('#confirm');$('#confirm-title').textContent=title;$('#confirm-body').textContent=body;dialog.showModal();let finish=v=>{dialog.close();$('#confirm-no').onclick=null;$('#confirm-yes').onclick=null;dialog.oncancel=null;resolve(v);};$('#confirm-no').onclick=()=>finish(false);$('#confirm-yes').onclick=()=>finish(true);dialog.oncancel=e=>{e.preventDefault();finish(false);};});}
// A failed write is settled: a follow-up status read must not keep its editor locked.
function actionNotice(action,payload,data){const core={buddy:'ZombieBuddy',viewpoint:'3D 视角',base:'基础组件'};return action==='componentsAction'&&payload.operation==='install'&&data.status==='completed'&&core[payload.component]?core[payload.component]+' 已启用，下次启动生效。':data.message;}
async function run(action,payload={},options={}){if(!launchReplyCurrent(options.launchToken))return null;const origin=state.route,epoch=state.experienceEpoch||0,mutating=mutationActions.has(action);let failed=false;if(state.busy&&mutating&&action!=='cancel')return;if(mutating){state.pendingMutations=(state.pendingMutations||0)+1;task({text:'正在执行，请稍候…',value:-1});}try{const data=await request(action,payload);if(!launchReplyCurrent(options.launchToken)){cancelChangedLaunch();return null;}if((action==='launch'||action==='profileLaunch')&&data.preflight)acceptLaunchPreflight(data.preflight);if(epoch===(state.experienceEpoch||0))actualWritten(action,data);if(!options.quiet&&!(options.restoreResultOwner&&action==='componentsAction'&&payload.component==='all'&&payload.operation==='uninstall'))notify(action==='profileLaunch'?'启动请求已发送。':((action==='buddyTakeover'||action==='buddyRecover')?buddyResultMessage(data):actionNotice(action,payload,data))||'操作已完成',origin);return data;}catch(e){failed=true;if(!launchReplyCurrent(options.launchToken)){cancelChangedLaunch();return null;}options.error=e.message;if((action==='launch'||action==='profileLaunch')&&e.preflight){acceptLaunchPreflight(e.preflight);launchMessage(e.message);}else if(!options.quietErrors)notify(e.message,origin);if(action==='componentsAction'&&/Buddy|接管旧启动参数/.test(e.message)){state.buddyInstall={...payload};state.buddyPreview=null;state.buddyResult=null;render();}return null;}finally{if(mutating){state.pendingMutations--;task({active:false,text:''});if(failed)void refresh();else await refresh();}}}
function memoryEdit(value){cancelLaunchAttempt();const n=Number(value);state.memoryEdit=n;state.memoryDirty=true;$('#memory-number').value=String(value);$('#memory-range').value=String(value);const save=$('[data-save-memory]');save.disabled=state.memoryConflict||!Number.isInteger(n)||n<1||n>state.memory.maximumGiB||state.busy;const advice=$('[data-memory-advice]');if(advice)advice.hidden=!memoryLowAvailable();}
document.addEventListener('input',e=>{if(e.target.matches('#memory-number,#memory-range'))memoryEdit(e.target.value);if(e.target.matches('[data-setting]')){state.settingEdits[e.target.dataset.setting]=e.target.value;state.settingsDirty=true;$('[data-save-settings]').disabled=state.settingsConflict||state.busy;}if(e.target.id==='mod-search'){state.saveSearch=e.target.value;renderCatalog();}if(e.target.id==='log-filter'){const filter=e.target.value.toLowerCase();$('#log-output').textContent=logText().split('\n').filter(line=>line.toLowerCase().includes(filter)).join('\n');}});
document.addEventListener('change',e=>{if(e.target.matches('[data-setting]')){state.settingEdits[e.target.dataset.setting]=e.target.value;state.settingsDirty=true;$('[data-save-settings]').disabled=state.settingsConflict||state.busy;}});
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;
if(b.dataset.page){route(b.dataset.page);if(b.dataset.toolFocus){const target=document.querySelector('[data-maintenance="'+b.dataset.toolFocus+'"]');if(target){target.scrollIntoView({block:'center'});target.focus({preventScroll:true});}}return;}
if(b.id==='theme'){const value=document.documentElement.classList.contains('light')?'dark':'light';const result=await run('theme',{value});if(result){document.documentElement.classList.toggle('light',value==='light');if(state.status)state.status.theme=value;b.textContent=value==='light'?'黑金主题':'浅蓝主题';}return;}
if(b.id==='cancel'){await run('cancel');return;}
if(b.hasAttribute('data-refresh')){refresh();return;}
if(b.dataset.reload){const name=b.dataset.reload;if(state[name+'Dirty']&&!await confirm('重新读取配置？','这会放弃当前页面未保存的修改，并读取外部最新配置。'))return;state[name+'Dirty']=false;state[name+'Conflict']=false;state[name]=null;load(name,name==='modCatalog');return;}
if(b.hasAttribute('data-addon-select')){const key=b.dataset.addonSelect;addonSelection.has(key)?addonSelection.delete(key):addonSelection.add(key);render();return;}
if(b.hasAttribute('data-check-updates')){try{notify('正在查询工坊版本；不下载、不占用游戏身份…');state.updates=await request('updatesGet');notify('版本检查结束，结果已显示在组件卡片。');render();}catch(e){notify(e.message);}return;}
if(b.hasAttribute('data-core-install')||b.hasAttribute('data-addons-install')){const keys=b.hasAttribute('data-core-install')?['base','chinese']:[...addonSelection],batch=keys.map(component=>({payload:{component,operation:'install',confirmed:true},warning:state.status?.components.find(c=>c.id===component)?.warning||'',warningConfirmed:false}));state.buddyInstall=null;await installComponentsBatch(batch);return;}
if(b.dataset.component){const component=b.dataset.component,operation=b.dataset.operation;const card=state.status?.components.find(c=>c.id===component);const roofWarning=component==='base'&&operation==='uninstall'?state.status?.components.find(c=>c.id==='roofs'&&c.configured)?.warning||'':'';if(card?.warning&&!await confirm(operation==='uninstall'?'卸载前说明':'扩展兼容说明',card.warning))return;if(operation==='uninstall'&&component==='base'&&!await confirm('卸载基础视角组件？','基础视角、Buddy及依赖它的物件、枪械、汉化和扩展一并关闭；下载文件及个人配置保留。'+(roofWarning?'\n\n完整屋顶移除要求：'+roofWarning:'')))return;const payload={component,operation,confirmed:true};if(operation==='uninstall'&&(component==='roofs'&&card?.warning||component==='base'&&roofWarning))payload.roofRemovalAcknowledged=true;const result=await run('componentsAction',payload);if(result&&['install','update'].includes(operation)){await load('java');if(state.java?.jars?.some(j=>j.decision==null))openJavaReview();}return;}
if(b.hasAttribute('data-shoot-usage')){state.shootHelp=!state.shootHelp;render();return;}
if(b.dataset.memory){memoryEdit(b.dataset.memory);return;}
if(b.hasAttribute('data-save-memory')){if(!state.memoryDirty||state.memoryConflict||!Number.isInteger(state.memoryEdit)||state.memoryEdit<1||state.memoryEdit>state.memory.maximumGiB)return;await persistEditor('memorySave',{value:state.memoryEdit,stamp:state.memory.stamp},async()=>{state.memoryDirty=false;state.memoryConflict=false;await load('memory');});return;}
if(b.hasAttribute('data-save-settings')){await persistEditor('settingsSave',{values:{...state.settingEdits},stamp:state.settings.stamp},async()=>{state.settingsDirty=false;state.settingsConflict=false;await load('settings');});return;}
if(b.dataset.shader){const result=await run('shaderSelect',{id:b.dataset.shader,stamp:state.shaders.stamp});if(result)load('shaders');return;}
if(b.dataset.maintenance){const kind=b.dataset.maintenance==='cache'&&state.cacheAll?'cacheAll':b.dataset.maintenance;const title=kind==='cacheAll'?'彻底清除全部多人本地数据？':kind==='cache'?'清理多人地图缓存？':'清理本游戏工坊并重新下载？';const body=kind==='cacheAll'?'将永久删除本机 Saves/Multiplayer 中所有服务器的本地数据，包括角色缓存、车辆、地图标记和世界元数据。不生成备份，单人存档和本地模组保留。服务器端存档不受影响；重新连接时同步服务器数据，本地地图标记等内容可能无法恢复。':kind==='cache'?'只处理引擎验证过的多人地图区块缓存。角色及受保护文件保留，缓存清理不生成备份。':'将安全清理本游戏工坊文件并重新下载，可能耗时较长。开始前会再次验证路径、订阅、空间与游戏状态。';if(await confirm(title,body))await run('maintenance',{kind,confirmed:true});return;}
if(b.dataset.action){const action=b.dataset.action;if(action==='status'){refresh();return;}if(action==='logs'){await load('logs');return;}if(action==='dlss'){if(b.dataset.operation==='enable'&&!await confirm('启用实验 DLSS？','实验功能需要有效适配，出现画面异常时可关闭恢复。'))return;await run('dlss',{operation:b.dataset.operation,confirmed:true});return;}await run(action,action==='open'?{target:b.dataset.target}:action==='openOfficial'?{item:b.dataset.item}:{});if(action==='importShader')load('shaders');}});
function launchText(value){const stages={'submitted-server':'连接请求已发送','submitted-save':'存档加载请求已发送',submitted:'启动请求已发送',preparing:'正在准备本次启动',starting:'正在启动游戏进程',launched:'游戏进程已启动',waiting:'等待游戏菜单就绪',failed:'启动请求未完成',timeout:'等待游戏响应超时',recovered:'本次临时配置已恢复',conflict:'检测到外部配置变化，请查看日志'};return (value.pending?'当前启动状态：':'上次启动状态：')+(stages[value.stage]||(/[\\u3400-\\u9fff]/.test(value.stage||'')&&!/已进入/.test(value.stage)?value.stage:'等待启动'))+(value.warning?' · '+value.warning:'');}
function javaRow(j){const busy=state.javaPending.has(j.sha256),allowed=j.decision===true,rejected=j.decision===false,disabled=!state.java.supported||state.busy||state.coreApplying||busy;return `<article class="java-file" data-java-state="${allowed?'allowed':rejected?'rejected':'pending'}"><div class="row"><h3>${esc(j.name||j.id)}</h3><span class="badge ${allowed?'status-positive':rejected?'status-negative':'status-neutral'}">${busy?'正在保存此文件…':allowed?'已允许':rejected?'已拒绝':'等待授权'}</span></div><p class="small">作者（工坊资料）：${esc(j.author||'作者未提供')} · 工坊 ${esc(j.workshopId||'未提供')}</p>${j.warning?`<p class="small">${esc(j.warning)}</p>`:''}<details class="java-evidence" ${!allowed?'open':''}><summary>完整路径与 SHA-256</summary><p class="key">完整路径：${esc(j.path)}</p><p class="key java-hash">SHA-256：${esc(j.sha256)}</p></details><div class="actions">${!allowed?`<button data-java-allow="${esc(j.sha256)}" ${disabled?'disabled':''}>允许此文件</button>`:''}${!rejected?`<button data-java-deny="${esc(j.sha256)}" ${disabled?'disabled':''}>拒绝此文件</button>`:''}</div></article>`;}
function javaPanel(){const data=state.java,jars=data?.jars||[],pendingFiles=jars.filter(j=>j.decision==null),rejected=jars.filter(j=>j.decision===false),allowed=jars.filter(j=>j.decision===true);return `<div class="java-approval"><p class="java-risk">Java 文件可访问你的计算机及个人文件。请选择可信文件授权；文件更新后需重新授权。</p><div class="row"><span class="small">未决定 ${pendingFiles.length} · 已允许 ${allowed.length} · 已拒绝 ${rejected.length}</span><button data-java-refresh>重新读取 Java 文件</button></div>${data?warnings(data)+(data.supported?'':`<div class="notice">当前游戏版本或授权接口不可用，请使用游戏内授权入口。</div>`)+`<section class="java-pending"><h3 class="section">待核对文件 · ${pendingFiles.length}</h3>${pendingFiles.map(javaRow).join('')||'<p class="small">当前没有待授权文件。</p>'}</section>${rejected.length?`<details class="java-rejected"><summary>已拒绝 · ${rejected.length}</summary>${rejected.map(javaRow).join('')}</details>`:''}${allowed.length?`<details class="java-allowed"><summary class="status-positive">已允许 · ${allowed.length}</summary>${allowed.map(javaRow).join('')}</details>`:''}`:placeholder('java')}</div>`;}
let javaDecisionQueue=Promise.resolve();
function queueJavaDecision(hash,decision){if(state.javaPending.has(hash))return;state.javaPending.add(hash);render();updateJavaReview();javaDecisionQueue=javaDecisionQueue.then(async()=>{const current=state.java?.jars.find(j=>j.sha256===hash);if(!current||current.decision===decision)return;if(!state.java?.supported)throw new Error('Java 授权接口不可用，请重新读取文件后再决定授权。');const result=await request('javaSave',{stamp:state.java.stamp,hash,decision});state.javaRevision=(state.javaRevision||0)+1;state.java=result;notify(result.message||'此文件授权已保存。');render();updateJavaReview();}).catch(e=>notify(e.message)).finally(()=>{state.javaPending.delete(hash);render();updateJavaReview();refresh();});}
function javaReviewComplete(){return !!(state.java?.supported&&state.java.jars?.length&&!state.java.jars.some(j=>j.decision==null)&&state.javaPending.size===0);}
function updateJavaReview(){const dialog=$('#java-review');if(dialog){if(javaReviewComplete()){dialog.close();return;}dialog.querySelector('.java-approval').outerHTML=javaPanel();disable();}}
function focusJavaPending(container=$('#java-help')){if(!container)return;if(container.id==='java-help')scrollMainTo('#java-help');const first=container.querySelector('.java-pending [data-java-allow]:not(:disabled)');if(first){first.focus({preventScroll:true});first.scrollIntoView({block:'nearest'});}}
function openJavaReview(){if($('#java-review')||javaReviewComplete())return;const d=document.createElement('dialog');d.id='java-review';d.innerHTML='<div class="row"><h2>Java 模组授权</h2><button data-java-close>关闭</button></div>'+javaPanel();document.body.append(d);d.addEventListener('close',()=>d.remove());d.showModal();disable();focusJavaPending(d);}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;if(b.hasAttribute('data-java-open')){state.javaOpen=true;route('help');if(!state.java&&!state.loading.java)await load('java');scrollMainTo('#java-help');return;}if(b.hasAttribute('data-java-close')){$('#java-review')?.close();return;}if(b.hasAttribute('data-java-refresh')){await load('java');updateJavaReview();return;}if(b.dataset.javaAllow||b.dataset.javaDeny){if(state.busy||state.coreApplying)return;const hash=b.dataset.javaAllow||b.dataset.javaDeny,decision=!!b.dataset.javaAllow;const j=state.java?.jars.find(j=>j.sha256===hash);if(!j||j.decision===decision||state.javaPending.has(hash))return;if(!await confirm(decision?'允许这个 Java 文件？':'拒绝这个 Java 文件？',`${j.name}\n完整路径：${j.path}\nSHA-256：${hash}\nJava 文件可访问你的计算机及个人文件。本次授权对应上方 SHA-256 文件。`))return;queueJavaDecision(hash,decision);}});
const coverCache=new Map(),coverInflight=new Set(),coverQueue=new Set();let coverObserver,coverRevision=0;
function invalidateCovers(){coverRevision++;coverCache.clear();coverQueue.clear();}
setInterval(()=>{invalidateCovers();observeCovers();},30000);
function coverMarkup(m){const cached=coverCache.get(m.coverToken),waiting=!m.cover&&m.coverToken&&!coverCache.has(m.coverToken);return `<div class="cover" ${!m.cover&&m.coverToken?`data-cover-token="${esc(m.coverToken)}"`:''}>${waiting?'<span class="muted small">封面待读取</span>':cached===null?missingCover('本地封面不可用'):localCover(m.cover||cached)}</div>`;}
function touchCover(token,cover){coverCache.delete(token);coverCache.set(token,cover);while(coverCache.size>30)coverCache.delete(coverCache.keys().next().value);}
function applyCover(token,cover){if(!['saves','mods','workshop','components'].includes(state.route))return;for(const el of document.querySelectorAll('[data-cover-token]'))if(el.dataset.coverToken===token){el.innerHTML=cover===null?missingCover('本地封面不可用'):localCover(cover);coverObserver?.unobserve(el);}}
async function pumpCovers(){if(!['saves','mods','workshop','components'].includes(state.route)){coverQueue.clear();return;}while(coverInflight.size<2&&coverQueue.size){const token=coverQueue.values().next().value;coverQueue.delete(token);if(coverInflight.has(token))continue;coverInflight.add(token);const revision=coverRevision;request('modCover',{token}).then(data=>{if(revision!==coverRevision)return;const cover=data.token===token&&typeof data.cover==='string'&&data.cover.length<710000&&localCover(data.cover).startsWith('<img')?data.cover:null;touchCover(token,cover);applyCover(token,cover);}).catch(()=>{if(revision!==coverRevision)return;touchCover(token,null);applyCover(token,null);}).finally(()=>{coverInflight.delete(token);if(revision!==coverRevision)observeCovers();pumpCovers();});}}
function observeCovers(){coverObserver?.disconnect();coverQueue.clear();if(!['saves','mods','workshop','components'].includes(state.route))return;coverObserver=new IntersectionObserver(entries=>{for(const entry of entries){const token=entry.target.dataset.coverToken;if(!entry.isIntersecting){coverQueue.delete(token);continue;}if(coverCache.has(token)){const cover=coverCache.get(token);touchCover(token,cover);applyCover(token,cover);}else if(!coverInflight.has(token))coverQueue.add(token);}pumpCovers();},{root:document.querySelector('main'),rootMargin:'0px',threshold:0.01});document.querySelectorAll('[data-cover-token]').forEach(el=>coverObserver.observe(el));}
function scrollMainTo(selector){window.scrollTo(0,0);const main=document.querySelector('main'),target=document.querySelector(selector);if(main&&target){for(let el=target;el&&el!==main;el=el.parentElement)if(el.matches('details'))el.open=true;main.scrollTo({top:main.scrollTop+target.getBoundingClientRect().top-main.getBoundingClientRect().top-12,left:0,behavior:'auto'});}window.scrollTo(0,0);}
function shortDate(value){const date=new Date(typeof value==='number'&&Number.isFinite(value)&&Math.abs(value)<1e12?value*1000:value);return value&&!Number.isNaN(date.valueOf())?new Intl.DateTimeFormat('zh-CN',{year:'numeric',month:'2-digit',day:'2-digit',hour:'2-digit',minute:'2-digit',hour12:false}).format(date):value||'修改时间未知';}
const warnings=data=>(data?.warnings||[]).map(w=>`<div class="notice">${esc(w)}</div>`).join('');
function missingCover(label='暂无封面'){return `<span class="cover-missing" role="img" aria-label="${esc(label)}"><svg viewBox="0 0 32 32" aria-hidden="true" focusable="false"><rect x="5" y="6" width="22" height="20" rx="3"/><circle cx="12" cy="12" r="2"/><path d="m7 23 7-7 4 4 3-3 5 6"/></svg><small>${esc(label)}</small></span>`;}
function localCover(value){return typeof value==='string'&&value.length<1500000&&/^data:image\/(png|jpeg|jpg|gif|webp);base64,[a-z0-9+/=\s]+$/i.test(value)?`<img src="${esc(value)}" alt="原作者本地封面">`:missingCover();}
function catalogCards(){return richCatalog('save');}
function renderCatalog(){const el=$('#mod-catalog');if(el){el.innerHTML=catalogCards();observeCovers();disable();}}
function saves(){return richSaves();}
function saveEditor(){return richSaveEditor();}
async function readSave(id){const ticket=++state.saveReadSequence;state.loading.saveDetail=true;render();try{const d=await request('saveGet',{id});if(ticket!==state.saveReadSequence)return;const row=state.saves?.saves.find(s=>s.id===id);state.saveDetail={...d,editable:row?.editable!==false};state.saveMods=[...(d.enabled||[])];state.saveDirty=false;cancelChangedLaunch();delete state.errors.saveDetail;}catch(e){state.errors.saveDetail=e.message;notify(e.message);}finally{if(ticket!==state.saveReadSequence)return;state.loading.saveDetail=false;if(state.route==='saves'){render();if(ticket===state.saveReadSequence&&state.saveDetail?.id===id)scrollMainTo('.save-editor');}}}
function servers(){return serverPage();}
function serverDialog(id,seed={}){if(!state.servers){notify('请先读取服务器列表，再编辑连接。');return;}const snapshotStamp=state.servers.stamp,existingIds=new Set(state.servers.servers.map(row=>row.id));const s=state.servers?.servers.find(s=>s.id===id)||{id:'',port:16261,queryPort:0,useSteamRelay:false,...seed};const dialog=document.createElement('dialog');dialog.id='server-editor';dialog.innerHTML=`<h2>${s.id?'编辑':'添加'}服务器</h2><form id="server-form"><div class="server-fields">${[['name','显示名称','text'],['host','IP / 域名','text'],['port','游戏端口','number'],['serverPassword','服务器密码（可选）','password'],['username','角色账号','text'],['userPassword','角色密码','password'],['queryPort','查询端口（0 自动）','number'],['description','私人备注','text']].map(([key,label,type])=>`<label>${label}<input id="server-${key}" name="${key}" type="${type}" value="${type==='password'?'':esc(s[key]??'')}" ${type==='password'?'autocomplete="new-password" placeholder="'+(s['has'+key[0].toUpperCase()+key.slice(1)]?'已保存；留空保留':'未设置')+'"':key==='port'?'min="1" max="65535" required':key==='queryPort'?'min="0" max="65535"':key==='name'||key==='host'?'required maxlength="255"':'maxlength="4096"'}></label>`).join('')}</div><label class="relay"><input id="server-relay" type="checkbox" ${s.useSteamRelay?'checked':''}> 使用 Steam 中继</label><p class="small">密码默认隐藏。编辑时留空保留；需要清除已保存密码可勾选对应项。</p><div class="actions"><label><input id="clear-userPassword" type="checkbox">清除角色密码</label><label><input id="clear-serverPassword" type="checkbox">清除服务器密码</label></div><div class="actions section"><button type="button" data-server-close>返回</button><button type="submit" class="primary" data-server-write>保存服务器</button></div></form>`;document.body.append(dialog);cancelChangedLaunch();dialog.showModal();disable();dialog.addEventListener('close',()=>dialog.remove());$('#server-form').addEventListener('submit',async e=>{e.preventDefault();if(state.busy)return;const payload={id:s.id,stamp:snapshotStamp,useSteamRelay:$('#server-relay').checked};for(const key of ['name','host','port','queryPort','username','description'])payload[key]=['port','queryPort'].includes(key)?Number($('#server-'+key).value):$('#server-'+key).value;for(const key of ['userPassword','serverPassword'])if($('#clear-'+key).checked)payload[key]='';else if($('#server-'+key).value)payload[key]=$('#server-'+key).value;const connecting=dialog.dataset.recommendedConnection==='true';const result=await persistEditor('serverUpsert',payload,async()=>{dialog.close();dialog.remove();await load('servers');});if(result&&connecting){const written=(result.servers||[]).filter(row=>payload.id?row.id===payload.id:!existingIds.has(row.id));if(written.length===1&&state.servers?.servers.some(row=>row.id===written[0].id))await launchExperience('server',written[0].id);else notify('服务器已保存，请在个人列表选择连接。');}});}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;if(b.dataset.profilesRefresh){await load(b.dataset.profilesRefresh);if(b.dataset.profilesRefresh==='modProfiles')await load('modCatalog',true);return;}if(b.dataset.saveOpen){if(state.saveDirty&&!await confirm('放弃未保存修改？','切换存档会放弃当前编辑。'))return;await readSave(b.dataset.saveOpen);return;}if(b.hasAttribute('data-save-reread')){if(state.saveDirty&&!await confirm('重新读取此存档？','放弃未保存修改并读取最新字节快照。'))return;readSave(state.saveDetail.id);return;}for(const [key,delta] of [['modUp',-1],['modDown',1],['modRemove',0],['modAdd',2]])if(b.dataset[key]){const id=b.dataset[key],i=state.saveMods.indexOf(id);if(delta===2){if(i<0)state.saveMods.push(id);}else if(delta===0){state.saveMods.splice(i,1);}else if(i+delta>=0&&i+delta<state.saveMods.length){[state.saveMods[i],state.saveMods[i+delta]]=[state.saveMods[i+delta],state.saveMods[i]];}state.saveDirty=true;render();return;}if(b.hasAttribute('data-save-write')){const removed=state.saveDetail.enabled.filter(id=>!state.saveMods.includes(id));const needsConfirmation=!!state.saveDetail.requiresConfirmation||removed.length>0;if(needsConfirmation&&!await confirm('确认保存此存档模组？',(state.saveDetail.requiresConfirmation?'为此存档创建独立模组清单，不修改全局清单。\n':'')+(removed.length?'移除地图、物品等模组可能损坏存档。将移除：'+removed.join('、'):'请核对当前继承的模组及顺序。')))return;const payload={id:state.saveDetail.id,stamp:state.saveDetail.stamp,mods:[...state.saveMods],confirmed:needsConfirmation};await persistEditor('saveSave',payload,async()=>{await readSave(payload.id);await load('saves');});return;}if(b.dataset.profileLaunch){if(b.dataset.profileLaunch==='save'&&state.saveDirty){notify('请先保存或重新读取未保存的存档编辑。');return;}await run('profileLaunch',{kind:b.dataset.profileLaunch,id:b.dataset.id});return;}if(b.hasAttribute('data-server-new')){if(!state.servers)await load('servers');serverDialog('');return;}if(b.dataset.serverEdit){serverDialog(b.dataset.serverEdit);return;}if(b.hasAttribute('data-server-close')){$('#server-editor').close();return;}if(b.dataset.serverQuery){queueVisibleServerMetrics(b.dataset.serverQuery);return;}if(b.dataset.serverDelete){if(!await confirm('删除保存的服务器？','只删除本工具保存的连接配置。'))return;const result=await run('serverDelete',{id:b.dataset.serverDelete,stamp:state.servers.stamp});if(result)await load('servers');}});
document.addEventListener('click',async e=>{const b=e.target.closest('[data-profile-recover]');if(!b||b.disabled||state.busy||state.coreApplying)return;await run('profileRecover',{});});
// Local display data stays separate from editable drafts and native secrets.
function asList(value){return Array.isArray(value)?value:typeof value==='string'?value.split(/[;,]/).map(s=>s.trim()).filter(Boolean):[];}
function modInfo(id){return state.modCatalog?.mods?.find(m=>m.id===id)||{id,title:id,description:'本地目录没有找到此模组',requires:[]};}
function modTitle(m){return m.title||m.name||m.id;}
function readPending(){const names={saves:'存档列表',saveDetail:'存档模组',modCatalog:'本地模组目录',modProfiles:'命名配置',modProfile:'模组配置',servers:'个人服务器',serverFavorites:'游戏收藏',serverHall:'公开大厅',steamConnection:'Steam 连接',steamSubscriptions:'Steam 订阅',steamSearch:'工坊搜索',steamDetails:'工坊详情',playtime:'游玩计时',memory:'内存设置',settings:'画面设置',shaders:'光影设置',help:'帮助',java:'Java 文件',logs:'日志'},visible={home:['playtime'],mods:['modProfiles','modProfile','modCatalog'],saves:['saves','saveDetail','modProfiles','modCatalog'],servers:['servers','serverFavorites','serverHall'],workshop:['steamConnection','steamSubscriptions','steamSearch','steamDetails','modCatalog'],help:['help','java','logs'],maintenance:['logs'],components:[]};const active=(visible[state.route]||[state.route]).filter(n=>state.loading[n]);return active.length?`<div class="read-pending" data-read-pending role="status" aria-live="polite"><span>正在读取${active.map(n=>names[n]||'数据').join('、')}…</span><progress max="100" aria-label="读取进度"></progress></div>`:'';}
function modMeta(m){return `<div class="key">${esc(m.id)} · ${esc(m.author||'作者未提供')} · 版本 ${esc(m.version||'未提供')}</div><p class="small">依赖 ${asList(m.requires??m.require).map(esc).join('、')||'无'}${asList(m.loadAfter).length?' · 排在 '+asList(m.loadAfter).map(esc).join('、')+' 之后':''}${asList(m.loadBefore).length?' · 排在 '+asList(m.loadBefore).map(esc).join('、')+' 之前':''}${m.conflict?' · 重复来源冲突':''}${m.map?' · 地图模组':''}</p>`;}
function modSourceConflict(id){const entries=(state.modCatalog?.mods||[]).filter(mod=>mod.id===id);return entries.length>1||entries.some(mod=>mod.conflict);}
function modDragHandle(id,kind,disabled=false){return `<button type="button" class="mod-drag-handle" data-mod-drag="${esc(id)}" data-mod-drag-kind="${kind}" aria-label="拖动排序 ${esc(id)}" title="拖动调整顺序，也可使用上下移按钮" ${disabled?'disabled':''}><span aria-hidden="true">⠿</span></button>`;}
function modWorkshopLink(mod){const id=String(mod.workshopId||mod.item||'');return !mod.componentManaged&&['workshop','snapshot'].includes(mod.source)&&/^[1-9][0-9]{0,19}$/.test(id)&&BigInt(id)<=18446744073709551615n?`<button type="button" class="mod-workshop-link" data-mod-workshop="${esc(id)}">打开工坊原页 ↗</button><p class="small mod-workshop-feedback" data-mod-workshop-feedback role="status"></p>`:'';}
function orderedRows(ids,kind,readonly=false){return ids.map((id,i)=>{const m=modInfo(id),prefix=kind==='save'?'mod':'profile-mod';return `<li ${kind==='save'?'data-enabled-id':'data-profile-mod-id'}="${esc(id)}">${modDragHandle(id,kind,readonly||m.componentManaged||modSourceConflict(id))}<span class="order-number">${i+1}.</span><div class="ordered-info">${coverMarkup(m)}<div><h3>${esc(modTitle(m))}</h3>${modMeta(m)}${modWorkshopLink(m)}</div></div><div class="actions"><button aria-label="上移 ${esc(id)}" data-${prefix}-up="${esc(id)}" ${readonly||i===0?'disabled':''}>↑</button><button aria-label="下移 ${esc(id)}" data-${prefix}-down="${esc(id)}" ${readonly||i===ids.length-1?'disabled':''}>↓</button><button data-${prefix}-remove="${esc(id)}" ${readonly?'disabled':''}>移除</button></div></li>`;}).join('');}
function richCatalog(kind){if(!state.modCatalog)return placeholder('modCatalog');const search=(kind==='profile'?state.profileSearch:state.saveSearch).toLowerCase(),ids=kind==='profile'?state.modProfileMods:state.saveMods,mods=(state.modCatalog.mods||[]).filter(m=>[m.id,modTitle(m),m.author,m.description].join(' ').toLowerCase().includes(search)),selected=mods.find(m=>m.id===state.modSelection[kind])||mods[0],editable=kind==='profile'?!!state.modProfile:!!state.saveDetail;return warnings(state.modCatalog)+`<div class="mod-workspace"><div class="mod-list" role="list" aria-label="本地模组">${mods.map(m=>`<article class="catalog-card catalog-row ${selected?.id===m.id?'selected':''}" role="listitem">${coverMarkup(m)}<button class="mod-select" data-mod-select="${esc(m.id)}" data-mod-kind="${kind}" aria-pressed="${selected?.id===m.id}"><h3>${esc(modTitle(m))}</h3><span class="key">${esc(m.author||'作者未提供')} · ${esc(m.id)} · 版本 ${esc(m.version||'未提供')}</span><span class="small">依赖 ${asList(m.requires??m.require).map(esc).join('、')||'无'}${m.map?' · 地图模组':''}</span></button><div class="mod-row-state"><span class="badge ${modSourceConflict(m.id)?'status-negative':ids.includes(m.id)?'status-positive':'status-neutral'}">${modSourceConflict(m.id)?'来源冲突':ids.includes(m.id)?'已在清单':'未选择'}</span>${editable?`<button data-${kind==='profile'?'profile-mod':'mod'}-add="${esc(m.id)}" ${ids.includes(m.id)||modSourceConflict(m.id)||kind==='save'&&state.saveDetail.editable===false?'disabled':''}>${ids.includes(m.id)?'已添加':'添加'}</button>`:''}</div></article>`).join('')||'<div class="empty">没有匹配的本地模组。</div>'}</div><aside class="mod-detail" aria-label="所选模组详情">${selected?`<div class="mod-detail-title">${coverMarkup(selected)}<h3>${esc(modTitle(selected))}</h3></div>${modMeta(selected)}${modWorkshopLink(selected)}<p class="full-text">${esc(selected.description||'作者未提供介绍')}</p><details open><summary>来源与依赖</summary><p class="key">来源 ${esc(selected.source||'未提供')}${selected.item||selected.workshopId?' · 工坊 '+esc(selected.item||selected.workshopId):''}</p><p class="key">模组 ID：${esc(selected.id)}</p><p class="small">依赖：${asList(selected.requires??selected.require).map(esc).join('、')||'无'}</p></details>`:'<p>选择模组查看原作者介绍与来源。</p>'}</aside></div>`;}
function sortFindings(result){if(!result)return '';const section=(title,items,cls='')=>items.length?`<section class="mod-sort-finding-group ${cls}"><h4>${title}</h4><ul>${items.map(item=>`<li>${esc(item)}</li>`).join('')}</ul></section>`:'';const cycles=(Array.isArray(result.cycles)?result.cycles:[]).filter(Array.isArray).map(group=>group.map(id=>modTitle(modInfo(id))+' ('+id+')').join('、'));return `<div class="mod-sort-findings" data-mod-sort-findings role="status">${section('需要处理',asList(result.errors),'status-negative')}${section('原顺序需要调整',asList(result.violations))}${section('依赖循环',cycles,'status-negative')}${section('受循环影响的模组（不是循环成员）',asList(result.blockedByCycles).map(id=>modTitle(modInfo(id))+' ('+id+')'))}${section('提示',asList(result.warnings))}</div>`;}
function workspaceSortFindings(){const p=state.modSortReport;return p&&p.owner===state.modProfile&&p.catalog===state.modCatalog&&p.context===modWorkspaceKey()&&p.draft===JSON.stringify(state.modProfileMods)?sortFindings(p):'';}
function sortPanel(kind){const p=state.sortPreview;if(!p||p.kind!==kind)return '';const blocked=asList(p.errors).length||(p.cycles||[]).length;return `<div class="sort-preview section"><h3>依赖排序预览</h3>${sortFindings(p)}<ol>${asList(p.mods).map(id=>`<li>${esc(modTitle(modInfo(id)))} <small>${esc(id)}</small></li>`).join('')}</ol><p class="small">${blocked?'请先处理以上依赖问题':p.changed?'顺序将发生变化':'当前顺序符合依赖约束'} · 只更新当前草稿。</p><button data-sort-apply="${kind}" ${blocked?'disabled':''}>应用到草稿</button><button data-sort-close>关闭预览</button></div>`;}
document.addEventListener('change',async e=>{const el=e.target;if(el.id==='mod-workspace-select'){if(state.modProfileDirty&&!await confirm('放弃当前修改？','切换配置')){el.value=state.modProfile?.id||'';return;}if(el.value)await readModProfile(el.value);else{state.modProfileReadSequence++;state.modProfile=null;state.modProfileDirty=false;state.modProfileSaveFeedback='';state.modProfiles=await request('modProfiles');render();}return;}if(el.id==='mod-list-filter'){state.modListFilter=el.value;render();return;}if(el.matches('[data-mod-membership]')){ensureModDraft();const id=el.dataset.modMembership;if(el.checked&&modSourceConflict(id)){el.checked=false;return;}state.modProfileMods=el.checked?[...new Set([...state.modProfileMods,id])]:state.modProfileMods.filter(x=>x!==id);state.modProfileDirty=true;state.sortPreview=null;render();}});
document.addEventListener('click',async e=>{const b=e.target.closest('[data-mod-save-activate]');if(!b||b.disabled)return;ensureModDraft();await saveModWorkspace(true);});
function gameProcessPanel(){return `<section class="game-stop-section"><button class="danger" data-game-stop ${state.busy||state.coreApplying||state.gameStopActive?'disabled':''}>强制关闭游戏</button><span data-game-stop-feedback role="status">${esc(state.gameStopFeedback||'')}</span></section>`;}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;if(b.hasAttribute('data-workshop-clear')){state.workshopQuery='';state.workshopB42=false;await searchWorkshop(1);return;}if(!b.hasAttribute('data-game-stop')||state.gameStopActive)return;state.gameStopActive=true;b.disabled=true;disable();try{const snapshot=await request('gameProcesses');if(!snapshot.clients?.length||!snapshot.stamp){state.gameStopFeedback='未检测到游戏进程';await refresh();return;}if(!await confirm('强制关闭游戏？',snapshot.clients.map(x=>`${x.executable} · PID ${x.pid}`).join('\n')+'\n未保存的游戏进度可能丢失。'))return;const options={quiet:true},result=await run('forceCloseGame',{confirmed:true,stamp:snapshot.stamp},options);state.gameStopFeedback=result?.message||options.error||'游戏关闭未完成';await refresh();}catch(error){state.gameStopFeedback=error.message;}finally{state.gameStopActive=false;if(state.route==='home')render();disable();}});
function modWorkspaceKey(){return JSON.stringify([state.status?.gamePath,state.experienceEpoch||0]);}
async function sortModWorkspace(){if(state.busy||state.modSorting||state.loading.modProfile||editorSaves.has('modProfileSave'))return;ensureModDraft();const profile=state.modProfile,key=modWorkspaceKey(),catalog=state.modCatalog,snapshot=JSON.stringify(state.modProfileMods);state.modSorting=true;disable();try{const result=await request('modSort',{mods:[...state.modProfileMods]});if(profile!==state.modProfile||key!==modWorkspaceKey()||snapshot!==JSON.stringify(state.modProfileMods))return;const blocked=asList(result.errors).length||(result.cycles||[]).length;if(!blocked){const sorted=ordinaryModIds(result.mods);state.modProfileDirty=state.modProfileDirty||JSON.stringify(sorted)!==snapshot;state.modProfileMods=sorted;}state.modSortReport={...result,owner:profile,catalog,context:key,draft:JSON.stringify(state.modProfileMods)};state.modProfileSaveError=!!blocked;state.modProfileSaveFeedback=blocked?'排序未应用，请核对依赖问题。':JSON.stringify(state.modProfileMods)===snapshot?'当前顺序已正确':'已按依赖调整顺序';}catch(error){if(profile===state.modProfile&&key===modWorkspaceKey()){state.modProfileSaveError=true;state.modProfileSaveFeedback=error.message;}}finally{state.modSorting=false;if(state.route==='mods')render();disable();}}
function ordinaryModIds(ids){return asList(ids).filter(id=>!modInfo(id).componentManaged);}
function ensureModDraft(){if(!state.modProfile){state.modProfile={id:'',stamp:state.modProfiles?.stamp,baseline:null,workspaceKey:modWorkspaceKey()};state.modProfileName='我的配置';state.modProfileMods=ordinaryModIds(state.modProfiles?.activeMods);state.modProfileSaveFeedback='';state.modProfileSaveError=false;}return state.modProfile;}
async function newModWorkspace(){if(state.loading.modProfile||state.modSorting||editorSaves.has('modProfileSave'))return;if(state.modProfileDirty&&!await confirm('放弃当前配置修改？','新建配置会放弃尚未保存的修改。'))return;const key=modWorkspaceKey(),ticket=++state.modProfileReadSequence;state.loading.modProfile=true;render();try{const list=await request('modProfiles');if(key!==modWorkspaceKey()||ticket!==state.modProfileReadSequence)return;state.modProfiles=list;state.modProfile={id:'',stamp:list.stamp,baseline:null,workspaceKey:modWorkspaceKey()};state.modProfileName='新配置';state.modProfileMods=ordinaryModIds(list.activeMods);state.modProfileDirty=true;state.modProfileSaveFeedback='';state.modProfileSaveError=false;state.sortPreview=null;}catch(e){state.errors.modProfiles=e.message;notify(e.message);}finally{if(ticket===state.modProfileReadSequence){state.loading.modProfile=false;if(state.route==='mods')render();}}}
async function saveModWorkspace(activate=false){if(state.busy||state.modSorting||state.loading.modProfile||editorSaves.has('modProfileSave'))return;const p=ensureModDraft(),key=modWorkspaceKey(),name=state.modProfileName.trim(),mods=ordinaryModIds(state.modProfileMods),draft=JSON.stringify([name,mods]);if(!name)return;if(p.workspaceKey&&p.workspaceKey!==key){state.modProfileSaveError=true;state.modProfileSaveFeedback='请重新选择当前游戏的配置';render();return;}editorSaves.add('modProfileSave');disable();state.modProfileSaveError=false;state.modProfileSaveFeedback='';try{
 const fresh=p.id?await request('modProfileGet',{id:p.id}):await request('modProfiles');if(key!==modWorkspaceKey()||p!==state.modProfile||draft!==JSON.stringify([state.modProfileName.trim(),ordinaryModIds(state.modProfileMods)]))return;
 if(p.id&&p.baseline&&JSON.stringify([fresh.name,ordinaryModIds(fresh.mods)])!==JSON.stringify([p.baseline.name,p.baseline.mods]))throw Error('这个配置已在别处更改，请重新读取。');
 const result=await run('modProfileSave',{id:p.id||'',name,mods,stamp:fresh.stamp},{quiet:true});if(!result)throw Error(state.notice||'配置未保存');if(key!==modWorkspaceKey()||p!==state.modProfile)return;
 state.modProfile={...result,workspaceKey:key,baseline:{name:result.name||name,mods:ordinaryModIds(result.mods??mods)}};state.modProfileName=result.name||name;state.modProfileMods=ordinaryModIds(result.mods??mods);state.modProfileDirty=false;
 if(activate){const list=await request('modProfiles');if(key!==modWorkspaceKey())return;state.modProfiles=list;const options={quiet:true},enabled=await run('modProfileActivate',{id:state.modProfile.id,stamp:list.stamp,confirmed:true},options);if(!enabled)throw Error('配置已保存，启用失败：'+(options.error||'请重试'));state.modProfileSaveFeedback='已保存并启用';}else state.modProfileSaveFeedback='配置已保存';
 if(key!==modWorkspaceKey())return;state.modProfiles=await request('modProfiles');const latest=await request('modProfileGet',{id:state.modProfile.id});if(key!==modWorkspaceKey())return;state.modProfile={...latest,workspaceKey:key,baseline:{name:latest.name,mods:ordinaryModIds(latest.mods)}};await freshActual();
 }catch(e){state.modProfileSaveError=true;state.modProfileSaveFeedback=e.message;}finally{editorSaves.delete('modProfileSave');if(state.route==='mods')render();disable();}}
function modWorkspaceRows(){const ids=state.modProfile?state.modProfileMods:ordinaryModIds(state.modProfiles?.activeMods),search=(state.profileSearch||'').toLowerCase(),filter=state.modListFilter||'all',all=(state.modCatalog?.mods||[]).filter(m=>!m.componentManaged),known=new Set(all.map(m=>m.id));for(const id of ids)if(!known.has(id))all.push(modInfo(id));const rows=all.filter(m=>[m.id,modTitle(m),m.author].join(' ').toLowerCase().includes(search)&&(!filter||filter==='all'||(filter==='enabled')===ids.includes(m.id))).sort((a,b)=>{const ai=ids.indexOf(a.id),bi=ids.indexOf(b.id);return ai>=0&&bi>=0?ai-bi:ai>=0?-1:bi>=0?1:modTitle(a).localeCompare(modTitle(b),'zh-CN');});return rows.map(m=>{const index=ids.indexOf(m.id),on=index>=0;return `<li class="mod-workspace-row" data-mod-row="${esc(m.id)}">${modDragHandle(m.id,'profile',!on||modSourceConflict(m.id))}<label class="mod-membership"><input type="checkbox" data-mod-membership="${esc(m.id)}" ${on?'checked':''} ${modSourceConflict(m.id)&&!on?'disabled':''} aria-label="启用 ${esc(modTitle(m))}"></label>${coverMarkup(m)}<div class="mod-row-title"><strong>${esc(modTitle(m))}</strong><span>${esc(m.id)}${m.author?' · '+esc(m.author):''}</span>${modSourceConflict(m.id)?'<span class="status-negative">来源冲突</span>':''}</div><span class="mod-order">${on?index+1:'—'}</span><div class="actions"><button data-profile-mod-up="${esc(m.id)}" aria-label="上移 ${esc(m.id)}" ${!on||index===0?'disabled':''}>↑</button><button data-profile-mod-down="${esc(m.id)}" aria-label="下移 ${esc(m.id)}" ${!on||index===ids.length-1?'disabled':''}>↓</button></div><details class="mod-row-details"><summary>详情</summary><div><h3>${esc(modTitle(m))}</h3>${modMeta(m)}<p>${esc(String(m.description||'').slice(0,1500))}</p>${modWorkshopLink(m)}</div></details></li>`;}).join('')||'<li class="mod-list-empty">没有符合条件的模组</li>';}
document.addEventListener('toggle',e=>{const d=e.target;if(!d.matches?.('.mod-row-details')||!d.open)return;const list=d.closest('.mod-workspace-list'),body=d.querySelector('div');if(!list||!body)return;const a=list.getBoundingClientRect(),b=body.getBoundingClientRect();if(b.bottom>a.bottom)list.scrollTop+=b.bottom-a.bottom+8;else if(b.top<a.top)list.scrollTop-=a.top-b.top+8;},true);
function mods(){const list=state.modProfiles,p=state.modProfile,ids=p?state.modProfileMods:ordinaryModIds(list?.activeMods);return heading()+`<section class="card mod-manager" data-mod-manager><div class="mod-manager-toolbar"><label>模组配置<select id="mod-workspace-select" ${!list?'disabled':''}><option value="">当前启用清单</option>${(list?.profiles||[]).map(x=>`<option value="${esc(x.id)}" ${p?.id===x.id?'selected':''}>${esc(x.name)}</option>`).join('')}</select></label><button data-mod-profile-new>新建配置</button>${p?.id?`<button class="danger" data-mod-profile-delete="${esc(p.id)}">删除配置</button>`:''}<button data-profiles-refresh="modProfiles" aria-label="刷新配置列表">刷新</button></div>${p?`<label class="mod-name">配置名称<input id="mod-profile-name" maxlength="100" value="${esc(state.modProfileName)}"></label>`:''}<div class="mod-list-toolbar"><input id="profile-mod-search" type="search" aria-label="搜索本地模组" placeholder="搜索模组名称、ID、作者" value="${esc(state.profileSearch)}"><select id="mod-list-filter" aria-label="筛选模组">${[['all','全部模组'],['enabled','已启用'],['disabled','未启用']].map(([id,label])=>`<option value="${id}" ${id===(state.modListFilter||'all')?'selected':''}>${label}</option>`).join('')}</select><span>已选择 ${ids.length} 个</span><button data-profile-sort>自动排序</button></div>${state.loading.modProfile?'<p class="read-pending">正在读取配置…</p>':''}${state.errors.modCatalog?`<p class="error-text">${esc(state.errors.modCatalog)} <button data-reload="modCatalog">重试</button></p>`:''}<ul class="mod-workspace-list" id="profile-mod-catalog">${modWorkspaceRows()}</ul><div class="mod-manager-footer"><span class="small">${state.modProfileDirty?'有未保存修改':p?'已保存':'当前已启用'}</span><div class="actions"><button data-mod-profile-reread ${!p?'disabled':''}>放弃修改</button><button data-mod-profile-save ${!p||!state.modProfileName.trim()||!state.modProfileDirty?'disabled':''}>仅保存配置</button><button class="primary" data-mod-save-activate ${!list||!p||state.loading.modProfile?'disabled':''}>${state.modProfileDirty?'保存并启用':p?'启用此配置':'已启用'}</button></div></div><p data-mod-profile-save-feedback class="local-feedback ${state.modProfileSaveError?'status-negative':''}" role="${state.modProfileSaveError?'alert':'status'}">${esc(state.modProfileSaveFeedback||'')}</p>${workspaceSortFindings()}${sortPanel('profile')}</section>`;}

async function readModProfile(id,copy=false){const key=modWorkspaceKey(),ticket=++state.modProfileReadSequence;state.loading.modProfile=true;delete state.errors.modProfile;render();try{const p=await request('modProfileGet',{id});if(ticket!==state.modProfileReadSequence||key!==modWorkspaceKey())return;state.modProfile={...p,id:copy?'':p.id,stamp:p.stamp,baseline:copy?null:{name:p.name,mods:ordinaryModIds(p.mods)},workspaceKey:key};state.modProfileName=p.name+(copy?' · 副本':'');state.modProfileMods=ordinaryModIds(p.mods);state.modProfileDirty=copy;state.modProfileSaveFeedback='';state.modProfileSaveError=false;state.sortPreview=null;}catch(e){state.errors.modProfile=e.message;notify(e.message);}finally{if(ticket===state.modProfileReadSequence){state.loading.modProfile=false;if(state.route==='mods'){render();scrollMainTo('.mod-profile-editor');}}}}
async function previewSort(kind){const key=kind==='save'?'saveMods':'modProfileMods',snapshot=[...state[key]],context=modWorkspaceKey(),owner=kind==='save'?state.saveDetail:state.modProfile;state.loading.sort=true;notify('正在计算依赖排序，当前草稿保留…');try{const result=await request('modSort',{mods:snapshot});if(context!==modWorkspaceKey()||owner!==(kind==='save'?state.saveDetail:state.modProfile))return;if(JSON.stringify(state[key])!==JSON.stringify(snapshot)){notify('草稿已变化，请重新预览排序。');return;}state.sortPreview={...result,kind,snapshot};render();}catch(e){if(context===modWorkspaceKey())notify(e.message);}finally{state.loading.sort=false;}}
function richSaveEditor(){const d=state.saveDetail,readonly=d.editable===false;return `<div class="card section save-editor"><div class="row"><h2>${esc(d.name)} · 加载顺序</h2><button data-save-reread>重新读取此存档</button></div>${warnings(d)}<p class="small">${readonly?'多人存档只读':state.saveDirty?'有未保存修改；状态轮询不会覆盖编辑。':'仅修改此存档 mods.txt，地图及其他段保留。'}</p><p class="small">地图：${asList(d.maps).map(esc).join('、')||'未指定'}</p>${!readonly?`<div class="profile-transfer section"><label>读取命名配置<select id="save-profile-select" ${!state.modProfiles?'disabled':''}><option value="">选择配置</option>${(state.modProfiles?.profiles||[]).map(p=>`<option value="${esc(p.id)}">${esc(p.name)} · ${esc(p.modCount)} 个模组</option>`).join('')}</select></label><button data-save-profile-load ${!state.modProfiles?'disabled':''}>载入到此草稿</button></div>`:''}<ol class="enabled-mods">${orderedRows(state.saveMods,'save',readonly)}</ol><div class="actions"><button class="primary" data-save-write ${readonly||!state.saveDirty&&!d.requiresConfirmation?'disabled':''}>保存此存档模组</button><button data-save-sort ${readonly?'disabled':''}>预览依赖排序</button></div>${sortPanel('save')}</div>`;}
const thumbCache=new Map(),thumbMissing=new Map(),thumbInflight=new Set(),thumbQueue=new Map();let thumbObserver,thumbGeneration=0;
function thumbnail(kind,id){const key=kind+':'+id,data=thumbCache.get(key)||thumbMissing.get(key);return `<div class="original-thumb" data-${kind==='save'?'save':'server'}-thumb="${esc(id)}" data-thumb-kind="${kind}" data-thumb-id="${esc(id)}">${data?.dataUrl?localCover(data.dataUrl):`<span class="thumb-symbol" aria-hidden="true">${kind==='save'?'▣':'◎'}</span><span class="small">${esc(data?.message||'原版缩略图')}</span>${data?.error?'<button data-thumb-retry="'+esc(key)+'" data-safe>重试</button>':''}`}</div>`;}
function applyThumb(kind,id){const wrapper=document.createElement('div');wrapper.innerHTML=thumbnail(kind,id);for(const node of document.querySelectorAll('[data-thumb-id]'))if(node.dataset.thumbKind===kind&&node.dataset.thumbId===id)node.innerHTML=wrapper.firstElementChild.innerHTML;}
function pumpThumbs(){while(thumbInflight.size<2&&thumbQueue.size){const [key,entry]=thumbQueue.entries().next().value;thumbQueue.delete(key);if(thumbInflight.has(key))continue;const {kind,id,generation}=entry;thumbInflight.add(key);request(kind==='save'?'saveThumb':'serverThumb',{id}).then(d=>{if(generation!==thumbGeneration)return;const safe=localCover(d.dataUrl).startsWith('<img'),data={dataUrl:safe?d.dataUrl:null,message:d.message||'没有精确匹配的原版缩略图'};(safe?thumbCache:thumbMissing).set(key,data);}).catch(e=>{if(generation===thumbGeneration)thumbMissing.set(key,{error:true,message:e.message});}).finally(()=>{thumbInflight.delete(key);while(thumbCache.size>48)thumbCache.delete(thumbCache.keys().next().value);while(thumbMissing.size>512)thumbMissing.delete(thumbMissing.keys().next().value);if(generation===thumbGeneration&&(thumbCache.has(key)||thumbMissing.has(key)))applyThumb(kind,id);pumpThumbs();});}}
function observeThumbnails(){thumbObserver?.disconnect();thumbQueue.clear();if(!['saves','servers'].includes(state.route))return;const generation=thumbGeneration;thumbObserver=new IntersectionObserver(entries=>{for(const e of entries){const kind=e.target.dataset.thumbKind,id=e.target.dataset.thumbId,key=kind+':'+id;if(!e.isIntersecting){thumbQueue.delete(key);continue;}if(thumbCache.has(key)||thumbMissing.has(key))applyThumb(kind,id);else if(!thumbInflight.has(key))thumbQueue.set(key,{kind,id,generation});}pumpThumbs();},{root:document.querySelector('main'),rootMargin:'0px',threshold:0.01});document.querySelectorAll('[data-thumb-id]').forEach(el=>thumbObserver.observe(el));}
function richSaves(){return heading('<button data-profiles-refresh="saves">重新读取列表</button>')+warnings(state.saves)+(state.saves?`<div class="library-grid">${state.saves.saves.map(s=>`<article class="card save-card">${thumbnail('save',s.id)}<div class="library-body"><span class="badge">${esc(s.mode)}${s.editable===false?' · 模组只读':''}</span><h3>${esc(s.name)}</h3><p><time data-save-date title="${esc(s.modifiedUtc||'')}" datetime="${esc(s.modifiedUtc||'')}">${esc(shortDate(s.modifiedUtc))}</time> · ${esc(s.modCount)} 个模组</p>${s.mode==='Multiplayer'?'<p class="small">多人本地缓存；删除不影响服务器存档。</p>':''}<div class="actions"><button data-profile-launch="save" data-target-launchable="${s.editable!==false}" data-id="${esc(s.id)}" class="primary" ${s.editable===false||state.saveDirty?'disabled':''}>继续（实验） ↗</button><button data-save-open="${esc(s.id)}">查看模组</button><button data-save-delete="${esc(s.id)}" class="danger">${s.mode==='Multiplayer'?'删除本地缓存':'删除'}</button></div></div></article>`).join('')||'<div class="empty">没有找到本机存档。</div>'}</div>`:placeholder('saves'))+(state.loading.saveDetail?placeholder('saveDetail'):state.saveDetail?richSaveEditor():'')+`<div class="section"><h2>本地模组目录</h2><input id="mod-search" type="search" value="${esc(state.saveSearch)}" placeholder="搜索名称、ID、作者、介绍" aria-label="搜索本地模组"><div id="mod-catalog" class="grid section">${richCatalog('save')}</div></div>`;}
function metricBadges(q={}){return `<div class="server-metrics"><div><span>在线人数</span><strong>${esc(q.players??'—')}</strong></div><div><span>容量</span><strong>${esc(q.maxPlayers??'—')}</strong></div><div><span>响应延迟</span><strong>${esc(q.latencyMs??q.ping??'—')} <small>ms</small></strong></div></div>`;}
function serverPage(){const tab=state.serverTab;let content;const tabs=`<div class="tabs" role="tablist" aria-label="服务器来源">${[['recommended','推荐服务器'],['personal','个人列表'],['favorites','游戏收藏'],['hall','公开大厅']].map(([id,label])=>`<button role="tab" aria-selected="${tab===id}" data-server-tab="${id}" class="${tab===id?'secondary':''}">${label}</button>`).join('')}</div>`;
if(tab==='recommended')content=libraryUi.recommendedPage();
else if(tab==='personal')content=state.servers?`<div class="library-grid section">${state.servers.servers.map(s=>{const q=state.serverQueries[s.id];return `<article class="card server-card" data-server-card="${esc(s.id)}">${thumbnail('server',s.id)}<div class="library-body"><span class="badge">${q?.status==='online'?'公开查询有响应':'状态未知'}</span><h3>${esc(s.name)}</h3><p>${esc(s.host)}:${esc(s.port)}</p>${metricBadges(q)}<details class="server-description small" data-disclosure="server-${esc(s.id)}"><summary>${q?.descriptionSource==='server'?'服务器公开介绍':'私人备注／介绍未读取'}</summary><div>${richDescription(q?.description||s.description||'暂无介绍或备注')}</div>${q?.descriptionSource==='server'&&s.description?'<p>私人备注：'+esc(s.description)+'</p>':''}${q?.descriptionWarning?'<p>'+esc(q.descriptionWarning)+'</p>':''}</details><p class="small server-query-meta" ${q?'':'hidden'}>${q?`${esc(q.version||'')} · <time datetime="${esc(q.checkedUtc||'')}" title="${esc(q.checkedUtc||'')}">${esc(localDate(q.checkedUtc))}</time> ${esc(q.error||'')}`:''}</p><div class="actions"><button data-profile-launch="server" data-id="${esc(s.id)}" class="primary">连接 ↗</button><button data-server-query="${esc(s.id)}">查询</button><button data-server-edit="${esc(s.id)}">编辑</button><button data-server-delete="${esc(s.id)}" class="danger">删除</button></div></div></article>`;}).join('')||'<div class="empty">还没有保存的服务器。添加你的连接信息。</div>'}</div>`:placeholder('servers');
else if(tab==='favorites')content=state.serverFavorites?`<p class="small section">${esc(state.serverFavorites.message||'只读游戏收藏；导入后可在个人列表编辑和连接。')}</p><div class="grid section">${(state.serverFavorites.favorites||[]).map(s=>`<article class="card"><span class="badge">游戏收藏</span><h3 class="section">${esc(s.name)}</h3><p>${esc(s.host)}:${esc(s.port)}</p><p class="small">角色 ${esc(s.username||'未填写')} · ${s.hasUserPassword?'已保存角色密码':'无角色密码'} · ${s.hasServerPassword?'已保存服务器密码':'无服务器密码'}</p><button class="primary section" data-favorite-import="${esc(s.id)}">导入个人列表</button></article>`).join('')||'<div class="empty">游戏收藏为空。</div>'}</div>`:placeholder('serverFavorites');
else content=`<form id="hall-search-form" class="search-bar section"><input id="hall-query" type="search" value="${esc(state.hallQuery)}" placeholder="即时筛选已收到的服务器名称" aria-label="大厅搜索"><button data-hall-search>重新查询大厅</button>${state.loading.serverHall?'<button type="button" data-hall-cancel>取消查询</button>':''}</form><div class="hall-filters"><label>排序<select id="hall-sort">${[['players','在线人数 ↓'],['ping','响应延迟 ↑'],['name','名称 ↑']].map(([id,label])=>`<option value="${id}" ${state.hallSort===id?'selected':''}>${label}</option>`).join('')}</select></label><label class="filter-label"><input type="checkbox" id="hall-hide-empty" ${state.hallHideEmpty?'checked':''}> 隐藏空服</label><label class="filter-label"><input type="checkbox" id="hall-hide-password" ${state.hallHidePassword?'checked':''}> 隐藏有密码</label><label>最少人数<input id="hall-min-players" type="number" min="0" value="${esc(state.hallMinPlayers)}"></label><label>最大延迟 ms<input id="hall-max-ping" type="number" min="0" value="${esc(state.hallMaxPing)}" placeholder="不限"></label></div><div id="hall-results">${hallResults()}</div>`;
return heading('<button data-server-new class="primary">添加服务器</button><button data-server-source-refresh>刷新当前列表</button>')+tabs+content;}
function workshopText(value){return String(value||'').slice(0,100000).replace(/\[img\][\s\S]*?\[\/img\]/gi,'').replace(/\[\*\]/g,'\n• ').replace(/\[\/?(?:h[1-6]|list|olist|quote|table|tr|td|th)(?:=[^\]]*)?\]|\[hr\]/gi,'\n').replace(/\[\/?(?:b|i|u|s|strike|spoiler|noparse|code|url|color|size)(?:=[^\]]*)?\]/gi,'').replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g,'').replace(/\n{3,}/g,'\n\n').trim();}
function workshopImage(value){try{const u=new URL(value);return u.protocol==='https:'&&!u.username&&!u.password&&(!u.port||u.port==='443')&&['images.steamusercontent.com','steamuserimages-a.akamaihd.net'].includes(u.hostname)?u.href:'';}catch{return '';}}
function workshopCover(item){const url=workshopImage(item.previewUrl);if(url)return `<div class="cover workshop-preview"><img src="${esc(url)}" alt="${esc(item.title||'模组封面')}" loading="lazy" referrerpolicy="no-referrer"></div>`;const m=state.modCatalog?.mods?.find(m=>String(m.item||m.workshopId)===String(item.id));return m?coverMarkup(m):'<div class="cover workshop-neutral">'+missingCover(state.errors.modCatalog?'封面读取失败':state.loading.modCatalog?'封面待读取':'暂无封面')+'</div>';}
function workshopState(item){return `<div class="workshop-states"><span class="badge">${item.subscribed?'已订阅':'未订阅'}</span>${item.downloaded?'<span class="badge">已下载</span>':''}${item.needsUpdate?'<span class="badge">有更新</span>':''}</div>`;}
function workshopCards(items){return(items||[]).map(item=>`<article class="card workshop-card">${workshopCover(item)}<h3>${esc(item.title||item.id)}</h3>${workshopState(item)}<p class="workshop-excerpt">${esc(workshopText(item.description).slice(0,300))}</p><p class="small">${esc(shortDate(item.updated))}${item.sizeBytes?' · '+esc(Math.round(item.sizeBytes/1024))+' KiB':''}</p><div class="actions"><button data-workshop-details="${esc(item.id)}">查看详情</button><button class="primary" data-workshop-download="${esc(item.id)}">${item.subscribed?'更新下载':'订阅下载'}</button>${item.downloaded?`<button data-java-workshop-inspect="${esc(item.id)}">检查 Java 模组</button>`:''}</div></article>`).join('')||'<div class="empty">没有找到模组</div>';}
function workshop(){const tab=state.workshopTab,tabs=`<div class="tabs" role="tablist" aria-label="工坊页面">${[['search','浏览工坊'],['subscribed','已订阅'],['connection','Steam 连接']].map(([id,label])=>`<button role="tab" aria-selected="${tab===id}" data-workshop-tab="${id}" class="${tab===id?'secondary':''}">${label}</button>`).join('')}</div>`;let content;
if(tab==='connection')content=`<div class="card section"><h2>Steam 连接</h2><button data-steam-connect ${state.loading.steamConnection?'disabled':''}>检查连接</button>${state.errors.steamConnection?`<p class="error-text">${esc(state.errors.steamConnection)}</p>`:''}${state.steamConnection?`<p>${esc(state.steamConnection.available?'已连接':'未连接')}${state.steamConnection.accountName?' · '+esc(state.steamConnection.accountName):''}</p>`:''}</div>`;
else if(tab==='subscribed')content=`<div class="actions section"><button data-workshop-refresh>刷新订阅</button></div>`+(state.loading.steamSubscriptions?placeholder('steamSubscriptions'):state.steamSubscriptions?`<div class="grid section">${workshopCards(state.steamSubscriptions.items)}</div>`:placeholder('steamSubscriptions'));
else content=`<form id="workshop-search-form" class="search-bar section"><input id="workshop-query" type="search" value="${esc(state.workshopQuery)}" placeholder="搜索模组" aria-label="工坊搜索"><label class="filter-label"><input id="workshop-b42" type="checkbox" ${state.workshopB42?'checked':''}> B42</label><button class="primary" data-workshop-search>搜索</button>${state.workshopQuery||state.workshopB42?'<button type="button" data-workshop-clear>全部模组</button>':''}</form>`+(state.loading.steamSearch?placeholder('steamSearch'):state.errors.steamSearch?placeholder('steamSearch'):state.steamSearch?`<p class="small">${state.workshopAppliedQuery?'搜索结果':'热门模组'} · ${logNumber(state.steamSearch.total)} 个</p><div class="grid section">${workshopCards(state.steamSearch.items)}</div><div class="row section"><button data-workshop-prev ${state.workshopPage<=1?'disabled':''}>上一页</button><span>第 ${logNumber(state.workshopPage)} 页</span><button data-workshop-next ${(state.steamSearch.hasMore===false||state.steamSearch.hasMore==null&&state.workshopPage>=Number(state.steamSearch.totalPages||1))?'disabled':''}>下一页</button></div>`:placeholder('steamSearch'));
const detail=state.steamDetails?.item;return heading()+tabs+content+(state.loading.steamDetails?placeholder('steamDetails'):detail?`<article class="card section workshop-detail"><div class="row"><h2>${esc(detail.title||detail.id)}</h2><button data-workshop-detail-close>关闭详情</button></div>${workshopCover(detail)}${workshopState(detail)}<p class="full-text">${esc(workshopText(detail.description))}</p><details><summary>依赖</summary><ul>${asList(detail.dependencies).map(d=>`<li>${esc(typeof d==='object'?d.title||d.id:d)}</li>`).join('')||'<li>无</li>'}</ul></details><button class="primary section" data-workshop-download="${esc(detail.id)}">订阅并下载</button></article>`:state.errors.steamDetails?placeholder('steamDetails'):'');}
async function searchWorkshop(page=1){const query=(state.workshopQuery.trim()+(state.workshopB42?' B42':'')).trim();if(page===1||query!==state.workshopAppliedQuery){page=1;state.workshopCursors=['*'];state.workshopAppliedQuery=query;}const cursors=state.workshopCursors||['*'],cursor=cursors[page-1],ticket=++state.workshopReadSequence;state.workshopPage=page;state.loading.steamSearch=true;delete state.errors.steamSearch;render();try{const payload={query,page};if(cursor)payload.cursor=cursor;const d=await request('steamSearch',payload);if(ticket!==state.workshopReadSequence)return;state.steamSearch=d;if(d.nextCursor&&d.hasMore!==false)cursors[page]=d.nextCursor;state.workshopCursors=cursors;}catch(e){if(ticket===state.workshopReadSequence)state.errors.steamSearch=e.message;}finally{if(ticket===state.workshopReadSequence){state.loading.steamSearch=false;if(state.route==='workshop')render();}}}

async function workshopDetails(id){state.workshopDetailId=id;const ticket=state.workshopDetailSequence=(state.workshopDetailSequence||0)+1;state.loading.steamDetails=true;delete state.errors.steamDetails;state.steamDetails=null;render();try{const d=await request('steamDetails',{id});if(ticket===state.workshopDetailSequence)state.steamDetails=d;}catch(e){if(ticket===state.workshopDetailSequence){state.errors.steamDetails=e.message;notify(e.message);}}finally{if(ticket===state.workshopDetailSequence){state.loading.steamDetails=false;if(state.route==='workshop'){render();scrollMainTo('.workshop-detail');}}}}
function taskStatus(status){return {running:'进行中',completed:'已完成',partial:'部分完成',failed:'失败',cancelled:'已取消',interrupted:'已中断'}[status]||'状态未知';}
function taskProgress(value,status){return typeof value==='number'&&Number.isFinite(value)&&value>=0&&value<=100&&!(['partial','failed','cancelled','interrupted'].includes(status)&&value===100)?value:null;}
function taskEntry(t){const pct=taskProgress(t.progress,t.status),known=pct!==null;return `<article class="task-entry section" data-task-id="${esc(t.id)}"><div class="row"><h3>${esc(t.title||t.action||'任务')}</h3><span class="badge task-status-${esc(t.status)}">${taskStatus(t.status)}</span></div><p class="small">开始 ${esc(localDate(t.started))}${t.finished?' · 结束 '+esc(localDate(t.finished)):''}</p><progress max="100" ${known?'value="'+pct+'"':''} aria-label="${esc(t.title)}进度"></progress><p>${known?esc(pct)+'% · ':'进度未知 · '}${esc(t.message||'')}</p><details open><summary>阶段与事件（${(t.events||[]).length}）</summary><ol class="task-events">${(t.events||[]).map(e=>`<li><time>${esc(localDate(e.at))}</time><span>${taskProgress(e.progress,t.status)!==null?esc(e.progress)+'%':'进度未知'}</span><p>${esc(e.message||'')}</p></li>`).join('')||'<li>尚无阶段记录</li>'}</ol></details></article>`;}
let taskRefreshTimer=null,taskRefreshPending=false;
const taskRowSnapshots=new WeakMap();
function patchTaskNode(current,next){if(current.nodeType!==next.nodeType||current.nodeName!==next.nodeName){current.replaceWith(next);return;}if(current.nodeType===Node.TEXT_NODE){if(current.nodeValue!==next.nodeValue)current.nodeValue=next.nodeValue;return;}if(current.nodeType!==Node.ELEMENT_NODE)return;for(const attr of [...current.attributes])if(!(current.tagName==='DETAILS'&&attr.name==='open')&&!next.hasAttribute(attr.name))current.removeAttribute(attr.name);for(const attr of next.attributes)if(!(current.tagName==='DETAILS'&&attr.name==='open')&&current.getAttribute(attr.name)!==attr.value)current.setAttribute(attr.name,attr.value);const old=[...current.childNodes],fresh=[...next.childNodes];for(let i=0;i<fresh.length;i++){if(old[i])patchTaskNode(old[i],fresh[i]);else current.append(fresh[i]);}for(let i=fresh.length;i<old.length;i++)old[i].remove();}
function renderTasks(){const content=$('#task-center-content'),dialog=$('#task-center'),scroll=dialog.scrollTop;if(!content.querySelector('[data-task-feedback]'))content.innerHTML='<div data-task-feedback role="status"></div><div data-task-active></div><h3 class="section" data-task-history-title>历史记录</h3><div data-task-history></div><p class="key section" data-task-log></p><button data-task-log-open data-action="open" data-target="logs" data-safe>打开日志目录</button>';const feedback=content.querySelector('[data-task-feedback]'),message=state.errors.tasks||(state.loading.tasks&&!state.tasks?'正在读取持久任务记录…':'');feedback.hidden=!message;let text=feedback.querySelector('p');if(!text){text=document.createElement('p');feedback.append(text);}text.textContent=message;let retry=feedback.querySelector('button');if(state.errors.tasks){if(!retry){retry=document.createElement('button');retry.dataset.taskRefresh='';retry.dataset.safe='';retry.textContent='重试读取';feedback.append(retry);}retry.disabled=!!state.loading.tasks;}else retry?.remove();if(state.tasks){const nodes=new Map([...content.querySelectorAll('[data-task-id]')].map(n=>[n.dataset.taskId,n])),seen=new Set(),active=content.querySelector('[data-task-active]'),history=content.querySelector('[data-task-history]');const place=(parent,rows,empty)=>{let previous=null;for(const row of rows){seen.add(row.id);const signature=JSON.stringify(row);let node=nodes.get(String(row.id));if(!node||taskRowSnapshots.get(node)!==signature){const template=document.createElement('template');template.innerHTML=taskEntry(row);const next=template.content.firstElementChild;if(node)patchTaskNode(node,next);else node=next;taskRowSnapshots.set(node,signature);}const wanted=previous?previous.nextSibling:parent.firstChild;if(wanted!==node)parent.insertBefore(node,wanted);previous=node;}let placeholder=parent.querySelector(':scope > [data-task-empty]');if(rows.length)placeholder?.remove();else if(!placeholder){placeholder=document.createElement('p');placeholder.dataset.taskEmpty='';placeholder.className='section';placeholder.textContent=empty;parent.append(placeholder);}};place(active,state.tasks.active?[state.tasks.active]:[],'当前没有执行中的任务。');place(history,(state.tasks.history||[]).filter(t=>t.id!==state.tasks.active?.id),'还没有历史任务。');for(const [id,node]of nodes)if(!seen.has(id))node.remove();content.querySelector('[data-task-log]').textContent='脱敏日志：'+(state.tasks.logPath||'尚未生成');}for(const node of content.querySelectorAll('[data-task-history-title],[data-task-log],[data-task-log-open]'))node.hidden=!state.tasks;dialog.scrollTop=scroll;}
function scheduleTasks(){if(state.loading.tasks){taskRefreshPending=true;return;}if(taskRefreshTimer!==null)return;taskRefreshTimer=setTimeout(()=>{taskRefreshTimer=null;if($('#task-center')?.open)void loadTasks();},120);}
async function loadTasks(){if(taskRefreshTimer!==null){clearTimeout(taskRefreshTimer);taskRefreshTimer=null;}if(state.loading.tasks){taskRefreshPending=true;return;}taskRefreshPending=false;state.loading.tasks=true;delete state.errors.tasks;renderTasks();try{state.tasks=await request('taskGet');}catch(e){state.errors.tasks=e.message;}finally{state.loading.tasks=false;renderTasks();if(taskRefreshPending&&$('#task-center')?.open){taskRefreshPending=false;scheduleTasks();}}}
function duration(seconds){const n=Math.max(0,Math.floor(Number(seconds)||0)),hours=Math.floor(n/3600),minutes=Math.floor(n%3600/60);return hours?`${hours} 小时 ${minutes} 分钟`:`${minutes} 分钟`;}
function reminderKey(p){const d=new Date();return [d.getFullYear(),d.getMonth()+1,d.getDate(),p.reminderMinutes,Math.floor(Math.max(0,Number(p.todaySeconds)||0)/(60*Math.max(1,Number(p.reminderMinutes)||1)))].join(':');}
function maybeShowReminder(){const p=state.playtime;if(!p?.reminderDue||!p.reminderMinutes)return;const key=reminderKey(p);if(state.reminderDismissed===key||state.reminderShownKey===key||document.querySelector('dialog[open]'))return;state.reminderShownKey=key;$('#reminder-dialog-time').textContent='今天已游玩 '+duration(p.todaySeconds)+'，可以舒展一下。';$('#playtime-reminder').showModal();}
async function dismissGlobalReminder(settings=false){state.reminderDismissed=reminderKey(state.playtime);$('#playtime-reminder').close();if(settings){route('home');scrollMainTo('.playtime-panel');}else if(state.route==='home')render();try{const d=await request('playtimeSettings',{dismissReminder:true});notify(d.message||'已记录本次提醒。');}catch(e){notify('提醒已在本次界面关闭，保存提醒状态失败：'+e.message);}}
$('#playtime-reminder').addEventListener('cancel',e=>{e.preventDefault();dismissGlobalReminder();});
document.addEventListener('close',()=>queueMicrotask(maybeShowReminder),true);
function playtimePanel(){const p=state.playtime;if(!p)return `<div class="section">${placeholder('playtime')}</div>`;return readPending()+`<div class="card section playtime-panel"><div class="row"><div><h3>今天的游玩时间</h3><div class="value" data-playtime-today>${duration(p.todaySeconds)}</div><p class="small">累计 ${duration(p.totalSeconds)} · ${p.running?'正在游戏 · 本次 '+duration(p.sessionSeconds):p.tracking==='waiting'?'正在等待游戏进程':p.tracking==='lost'?'计时中断，离线时间未计入':'游戏已结束 / 尚未开始'}</p></div><span class="badge">按实际游戏进程计时</span></div><p class="small">${esc(p.message||'')}</p><div class="actions section"><label>休息提醒<select id="reminder-minutes">${[[0,'关闭'],[15,'15 分钟'],[30,'30 分钟'],[60,'1 小时'],[90,'90 分钟'],[120,'2 小时'],[180,'3 小时'],[240,'4 小时'],[480,'8 小时']].map(([n,l])=>`<option value="${n}" ${Number(state.reminderMinutesDraft??p.reminderMinutes)===n?'selected':''}>${l}</option>`).join('')}</select></label><button data-reminder-save>保存提醒设置</button><button data-profiles-refresh="playtime">刷新计时</button></div>${p.reminderDue&&state.reminderDismissed!==reminderKey(p)?'<div class="notice section reminder"><h3>可以休息一下了</h3><p>本次提醒来自实际记录的游玩时间。随时继续你的游戏。</p><button data-reminder-dismiss>知道了，稍后提醒</button></div>':''}</div>`;}
async function pollPlaytime(){if(state.loading.playtime)return;try{const next=await request('playtimeGet');if(!next.reminderDue)state.reminderDismissed=false;state.playtime=next;const el=$('.playtime-panel');if(el&&state.route==='home'){el.outerHTML=playtimePanel();disable();}maybeShowReminder();}catch(e){state.errors.playtime=e.message;}}
function richFixture(action,payload){const mod={id:'preview-mod',title:'界面示例模组',author:'预览作者',version:'B42 示例',description:'仅用于布局预览。',requires:[],source:'预览',cover:''};const item={id:'preview-item',title:'工坊布局示例',description:'仅用于预览；实际浏览需 Steam 接口。',author:'预览作者',subscribed:false,downloaded:false,needsUpdate:false};switch(action){case'modProfiles':return {stamp:'preview',profiles:[{id:'preview-profile',name:'预览 · 轻量探索',modCount:1,updated:'2026-10-13'}],activeMods:[]};case'modProfileGet':return {id:payload.id,name:'预览 · 轻量探索',stamp:'preview',mods:[mod.id]};case'modSort':return {mods:payload.mods,warnings:['预览不扫描真实依赖'],errors:[],changed:false};case'saveThumb':case'serverThumb':return {dataUrl:null,message:'预览不读取真实原版缩略图'};case'serverFavorites':return {favorites:[],message:'预览不读取游戏收藏'};case'serverHall':return {servers:[],message:'预览不执行公开大厅查询'};case'steamConnection':return {available:false,message:'预览不会连接 Steam'};case'steamSubscriptions':return {items:[item],message:'界面示例，非真实订阅'};case'steamSearch':return {items:[item],page:1,totalPages:1,total:1,message:'界面示例，非真实工坊结果'};case'steamDetails':return {item:{...item,id:payload.id,dependencies:[]}};case'taskGet':return {active:null,history:[],logPath:'预览不读取真实日志'};case'playtimeGet':return {todaySeconds:0,totalSeconds:0,running:false,sessionSeconds:0,reminderMinutes:60,reminderDue:false,tracking:'stopped',message:'预览不记录真实游玩时间'};default:return null;}}
document.addEventListener('input',e=>{const el=e.target;if(el.id==='mod-profile-name'){state.modProfileName=el.value;state.modProfileDirty=true;const b=$('[data-mod-profile-save]');if(b)b.disabled=!el.value.trim()||state.busy;}if(el.id==='profile-mod-search'){state.profileSearch=el.value;$('#profile-mod-catalog').innerHTML=modWorkspaceRows();observeCovers();disable();}if(el.id==='workshop-query')state.workshopQuery=el.value;if(el.id==='hall-query'){state.hallQuery=el.value;renderHallResults();}if(el.id==='hall-min-players'){state.hallMinPlayers=Math.max(0,Number(el.value)||0);renderHallResults();}if(el.id==='hall-max-ping'){state.hallMaxPing=el.value;renderHallResults();}});
document.addEventListener('change',e=>{if(e.target.id==='workshop-b42')state.workshopB42=e.target.checked;if(e.target.id==='reminder-minutes')state.reminderMinutesDraft=Number(e.target.value);if(e.target.id==='hall-sort'){state.hallSort=e.target.value;renderHallResults();}if(e.target.id==='hall-hide-empty'){state.hallHideEmpty=e.target.checked;renderHallResults();}if(e.target.id==='hall-hide-password'){state.hallHidePassword=e.target.checked;renderHallResults();}});
document.addEventListener('submit',e=>{if(['workshop-search-form','hall-search-form'].includes(e.target.id)){e.preventDefault();if(e.target.id==='workshop-search-form')searchWorkshop(1);else loadHall();}});
function hallPing(s){const n=s.latencyMs??s.ping;return typeof n==='number'&&Number.isFinite(n)&&n>=0?n:null;}
function hallRows(){const query=state.hallQuery.trim().toLowerCase(),max=state.hallMaxPing===''?null:Math.max(0,Number(state.hallMaxPing)||0);return (state.serverHall?.servers||[]).filter(s=>String(s.name||'').toLowerCase().includes(query)&&(!state.hallHideEmpty||Number(s.players)>0)&&(!state.hallHidePassword||!s.passwordRequired)&&Number(s.players??0)>=state.hallMinPlayers&&(max===null||hallPing(s)!==null&&hallPing(s)<=max)).sort((a,b)=>{const names=String(a.name||'').localeCompare(String(b.name||''),'zh-CN');if(state.hallSort==='name')return names;if(state.hallSort==='ping'){const ap=hallPing(a),bp=hallPing(b);return ap===null?(bp===null?names:1):bp===null?-1:ap-bp||names;}return Number(b.players||0)-Number(a.players||0)||names;});}
function hallResults(){const all=state.serverHall?.servers||[],rows=hallRows();return `<p class="small hall-summary" role="status" aria-live="polite">${state.loading.serverHall?'正在查询 · ':''}已发现 ${all.length} 个 · 显示 ${rows.length} 个${state.hallProgress?' · '+esc(state.hallProgress):''}</p>${state.errors.serverHall?`<p class="status-negative">查询未完成：${esc(state.errors.serverHall)}</p>`:''}<p class="small">${esc(state.serverHall?.message||'服务器将陆续显示，可按人数或延迟排序。')}</p><div class="grid hall-grid section">${rows.map(s=>`<article class="card hall-card" data-hall-id="${esc(s.id)}"><div class="row"><h3>${esc(s.name)}</h3><span class="badge status-neutral">公开大厅${s.passwordRequired?' · 需要密码':''}</span></div><p class="key">${esc(s.host||'地址未提供')}${s.port?':'+esc(s.port):''}</p>${metricBadges({...s,ping:hallPing(s)})}<details data-disclosure="hall-${esc(s.id)}"><summary>服务器介绍</summary><div class="server-description small">${richDescription(s.description||'未提供介绍')}</div></details><button class="primary" data-hall-add="${esc(s.id)}">添加 / 填写角色后连接</button></article>`).join('')||`<div class="empty">${state.loading.serverHall?'等待有效服务器响应…':state.errors.serverHall?'查询失败，尚未收到匹配服务器。':all.length?'没有匹配当前筛选的服务器。':'没有找到公开服务器。'}</div>`}</div>`;}
function patchServerNode(current,next){
 if(current.nodeType!==next.nodeType||current.nodeType===1&&current.tagName!==next.tagName){current.replaceWith(next.cloneNode(true));return;}
 if(current.nodeType!==1){if(current.nodeValue!==next.nodeValue)current.nodeValue=next.nodeValue;return;}
 for(const attr of [...current.attributes])if(!(current.tagName==='DETAILS'&&attr.name==='open')&&!next.hasAttribute(attr.name))current.removeAttribute(attr.name);
 for(const attr of next.attributes)if(!(current.tagName==='DETAILS'&&attr.name==='open')&&current.getAttribute(attr.name)!==attr.value)current.setAttribute(attr.name,attr.value);
 if(current.matches('.hall-grid,.recommended-grid')){
  const key=el=>el.getAttribute('data-hall-id')||el.getAttribute('data-recommended-card'),old=new Map([...current.children].filter(el=>key(el)).map(el=>[key(el),el])),kept=new Set();
  for(const [index,child] of [...next.children].entries()){const id=key(child);let existing=id?old.get(id):[...current.children].find(el=>!key(el)&&!kept.has(el));if(existing){patchServerNode(existing,child);}else existing=child.cloneNode(true);kept.add(existing);if(current.children[index]!==existing)current.insertBefore(existing,current.children[index]||null);}
  for(const child of [...current.children])if(!kept.has(child))child.remove();return;
 }
 const previous=[...current.childNodes],incoming=[...next.childNodes];for(let i=0;i<incoming.length;i++){if(previous[i])patchServerNode(previous[i],incoming[i]);else current.append(incoming[i].cloneNode(true));}for(let i=incoming.length;i<previous.length;i++)previous[i].remove();
}
function patchServerContent(current,next){const focused=document.activeElement;patchServerNode(current,next);applyRichDescription(current);if(focused?.isConnected&&document.activeElement!==focused)focused.focus({preventScroll:true});}
function paintPersonalServer(id){if(state.route!=='servers'||state.serverTab!=='personal')return;const current=document.querySelector(`[data-server-card="${CSS.escape(id)}"]`);if(!current)return;const template=document.createElement('template');template.innerHTML=serverPage();const next=template.content.querySelector(`[data-server-card="${CSS.escape(id)}"]`);if(next){patchServerContent(current,next);disable();}}
const serverLive={key:'',generation:0,queue:[],pending:new Set(),active:0,activeIds:new Set()};
function serverMetricView(){if(document.hidden||state.route!=='servers'||!['personal','recommended'].includes(state.serverTab)||state.busy||state.coreApplying)return '';return [state.serverTab,state.experienceEpoch||0,state.serverTab==='personal'?state.servers?.stamp:libraryUi.model.recommended?.revision].join('|');}
function serverMetricRows(){return state.serverTab==='personal'?(state.servers?.servers||[]):(libraryUi.model.recommended?.servers||[]);}
function invalidateServerMetrics(){serverLive.generation++;serverLive.queue=[];for(const key of [...serverLive.pending])if(!key.endsWith('|active'))serverLive.pending.delete(key);}
function syncServerMetrics(){const key=serverMetricView();if(key!==serverLive.key){invalidateServerMetrics();serverLive.key=key;if(key)queueVisibleServerMetrics();}}
function queueVisibleServerMetrics(id){const view=serverMetricView();if(!view)return;if(view!==serverLive.key){invalidateServerMetrics();serverLive.key=view;}const generation=serverLive.generation,mode=state.serverTab;for(const row of serverMetricRows()){if(id&&row.id!==id)continue;const action=mode==='personal'?'serverQuery':'recommendedQuery',key=[generation,action,row.id].join('|');if(serverLive.pending.has(key)||serverLive.pending.has(key+'|active'))continue;serverLive.pending.add(key);serverLive.queue.push({key,view,generation,action,id:row.id,mode});}pumpServerMetrics();}
function pumpServerMetrics(){while(serverLive.active<4&&serverLive.queue.length){const view=serverMetricView();if(!view)return;serverLive.queue=serverLive.queue.filter(job=>{const valid=job.view===view&&job.generation===serverLive.generation;if(!valid)serverLive.pending.delete(job.key);return valid;});const index=serverLive.queue.findIndex(job=>!serverLive.activeIds.has(job.action+'|'+job.id));if(index<0)return;const job=serverLive.queue.splice(index,1)[0],activeId=job.action+'|'+job.id;serverLive.pending.delete(job.key);serverLive.pending.add(job.key+'|active');serverLive.active++;serverLive.activeIds.add(activeId);void request(job.action,{id:job.id}).then(result=>{if(job.generation!==serverLive.generation||job.view!==serverMetricView())return;if(job.mode==='personal'){state.serverQueries[job.id]=result;paintPersonalServer(job.id);}else{libraryUi.model.queries[job.id]=result;libraryUi.paintRecommendedCards();}}).catch(error=>{if(job.generation!==serverLive.generation||job.view!==serverMetricView())return;const result={id:job.id,status:'unknown',checkedUtc:new Date().toISOString(),error:error.message};if(job.mode==='personal'){state.serverQueries[job.id]=result;paintPersonalServer(job.id);}else{libraryUi.model.queries[job.id]=result;libraryUi.paintRecommendedCards();}}).finally(()=>{serverLive.pending.delete(job.key+'|active');serverLive.active--;serverLive.activeIds.delete(activeId);pumpServerMetrics();});}}
function refreshVisibleServerMetrics(){syncServerMetrics();queueVisibleServerMetrics();}
setInterval(refreshVisibleServerMetrics,30000);
document.addEventListener('visibilitychange',syncServerMetrics);
function renderHallResults(){const el=$('#hall-results');if(el&&state.route==='servers'&&state.serverTab==='hall'){const next=document.createElement('div');next.id=el.id;next.innerHTML=hallResults();patchServerContent(el,next);if(!state.loading.serverHall)$('[data-hall-cancel]')?.remove();disable();}}
function mergeHallBatch(data){if(!data||data.requestId!==state.hallRequestId||!state.loading.serverHall||!Array.isArray(data.servers))return;const byId=new Map((state.serverHall?.servers||[]).map(s=>[s.id,s]));for(const s of data.servers)if(s&&typeof s.id==='string'&&s.id&&typeof s.name==='string')byId.set(s.id,s);state.serverHall={...(state.serverHall||{}),servers:[...byId.values()]};renderHallResults();}
async function loadHall(){const ticket=state.hallReadSequence=(state.hallReadSequence||0)+1,requestId='hall-'+Date.now()+'-'+ticket;state.hallRequestId=requestId;state.hallProgress='';state.serverHall={servers:[],message:''};state.loading.serverHall=true;delete state.errors.serverHall;render();try{const result=await request('serverHall',{query:state.hallQuery.trim(),requestId});if(ticket===state.hallReadSequence)state.serverHall={...result,servers:[...new Map((result.servers||[]).filter(s=>s&&typeof s.id==='string'&&typeof s.name==='string').map(s=>[s.id,s])).values()]};}catch(e){if(ticket===state.hallReadSequence){state.errors.serverHall=e.message;notify(e.message);}}finally{if(ticket===state.hallReadSequence){state.loading.serverHall=false;state.hallProgress='';renderHallResults();}}}
async function cancelHall(){if(!state.loading.serverHall)return;const requestId=state.hallRequestId;state.hallReadSequence++;state.hallRequestId='';state.loading.serverHall=false;state.hallProgress='';state.serverHall={...(state.serverHall||{servers:[]}),message:'查询已取消'};renderHallResults();try{await request('cancel',{scope:'hall',requestId});}catch(e){notify(e.message);}}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;
if(b.dataset.modSelect){const kind=b.dataset.modKind;if(!['save','profile'].includes(kind))return;state.modSelection[kind]=b.dataset.modSelect;const el=$(kind==='profile'?'#profile-mod-catalog':'#mod-catalog'),scroll=el?.querySelector('.mod-list')?.scrollTop||0;if(el){el.innerHTML=richCatalog(kind);el.querySelector('.mod-list').scrollTop=scroll;observeCovers();disable();}return;}if(b.hasAttribute('data-hall-cancel')){cancelHall();return;}
if(b.hasAttribute('data-reminder-modal-dismiss')||b.hasAttribute('data-reminder-modal-settings')){dismissGlobalReminder(b.hasAttribute('data-reminder-modal-settings'));return;}if(b.id==='task-center-open'){$('#task-center').showModal();loadTasks();return;}if(b.hasAttribute('data-task-close')){$('#task-center').close();return;}if(b.hasAttribute('data-task-refresh')){loadTasks();return;}
if(b.hasAttribute('data-mod-profile-new')){await newModWorkspace();return;}
if(b.dataset.modProfileOpen||b.dataset.modProfileCopy){if(state.modProfileDirty&&!await confirm('放弃当前配置草稿？','读取另一配置会放弃尚未保存的修改。'))return;readModProfile(b.dataset.modProfileOpen||b.dataset.modProfileCopy,!!b.dataset.modProfileCopy);return;}
if(b.hasAttribute('data-mod-profile-reread')){if(state.modProfileDirty&&!await confirm('重新读取配置？','将放弃未保存的配置草稿。'))return;if(state.modProfile?.id)readModProfile(state.modProfile.id);else {state.modProfile=null;state.modProfileDirty=false;render();}return;}
for(const [key,delta] of [['profileModUp',-1],['profileModDown',1],['profileModRemove',0],['profileModAdd',2]])if(b.dataset[key]){ensureModDraft();const id=b.dataset[key],ids=state.modProfileMods,i=ids.indexOf(id);if(delta===2&&modSourceConflict(id))return;if(delta===2){if(i<0)ids.push(id);}else if(delta===0){if(i>=0)ids.splice(i,1);}else if(i>=0&&i+delta>=0&&i+delta<ids.length)[ids[i],ids[i+delta]]=[ids[i+delta],ids[i]];state.modProfileDirty=true;state.sortPreview=null;render();return;}
if(b.hasAttribute('data-mod-profile-save')){await saveModWorkspace(false);return;}
if(b.dataset.modProfileActivate){const result=await run('modProfileActivate',{id:b.dataset.modProfileActivate,stamp:state.modProfiles.stamp,confirmed:true},{quiet:true});if(result){state.modProfiles=await request('modProfiles');if(state.modProfile?.id&&!state.modProfileDirty)await readModProfile(state.modProfile.id);render();}return;}
if(b.dataset.modProfileDelete){if(!await confirm('删除命名配置？','只删除此命名配置，当前默认清单与存档清单保留。'))return;const id=b.dataset.modProfileDelete;const result=await run('modProfileDelete',{id,stamp:state.modProfiles.stamp});if(result){if(state.modProfile?.id===id){state.modProfile=null;state.modProfileDirty=false;}await load('modProfiles');}return;}
if(b.hasAttribute('data-profile-sort')||b.hasAttribute('data-save-sort')){if(b.hasAttribute('data-profile-sort')){await sortModWorkspace();return;}previewSort('save');return;}if(b.dataset.sortApply){const p=state.sortPreview,key=b.dataset.sortApply==='save'?'saveMods':'modProfileMods';if(!p||(p.errors||[]).length||(p.cycles||[]).length)return;if(JSON.stringify(state[key])!==JSON.stringify(p.snapshot)){notify('草稿已变化，请重新预览排序。');return;}state[key]=[...asList(p.mods)];state[b.dataset.sortApply==='save'?'saveDirty':'modProfileDirty']=true;state.sortPreview=null;render();return;}if(b.hasAttribute('data-sort-close')){state.sortPreview=null;render();return;}
if(b.hasAttribute('data-save-profile-load')){const id=$('#save-profile-select').value;if(!id)return;const saveId=state.saveDetail.id,ticket=state.saveReadSequence,snapshot=JSON.stringify(state.saveMods);try{notify('正在读取命名配置…');const p=await request('modProfileGet',{id});if(ticket!==state.saveReadSequence||state.saveDetail.id!==saveId)return;if(JSON.stringify(state.saveMods)!==snapshot){notify('草稿已变化，已保留新的手动编辑。请重新载入配置。');return;}state.saveMods=[...asList(p.mods)];state.saveDirty=true;state.sortPreview=null;render();notify('配置已载入存档草稿；尚未写入存档。');}catch(e){notify(e.message);}return;}
if(b.dataset.serverTab){state.serverTab=b.dataset.serverTab;render();if(state.serverTab==='favorites'&&!state.serverFavorites)load('serverFavorites');if(state.serverTab==='hall'&&!state.serverHall)loadHall();return;}if(b.hasAttribute('data-server-source-refresh')){const key={personal:'servers',favorites:'serverFavorites',hall:'serverHall'}[state.serverTab];key==='serverHall'?loadHall():load(key);return;}
if(b.dataset.favoriteImport){if(!await confirm('导入游戏收藏？','将此收藏复制到个人列表。已保存密码只由原生程序处理，不在界面显示。'))return;const result=await run('favoriteImport',{id:b.dataset.favoriteImport,confirmed:true});if(result)await load('servers');return;}
if(b.dataset.hallAdd){const s=state.serverHall?.servers.find(s=>s.id===b.dataset.hallAdd);if(s){await load('servers');serverDialog('',{name:s.name,host:s.host,port:s.port,queryPort:s.queryPort||0,useSteamRelay:!!s.steam});}return;}if(b.hasAttribute('data-hall-search'))return;
if(b.hasAttribute('data-steam-connect')){load('steamConnection');return;}if(b.dataset.workshopTab){state.workshopTab=b.dataset.workshopTab;render();if(state.workshopTab==='subscribed'&&!state.steamSubscriptions)load('steamSubscriptions');if(state.workshopTab==='search'&&!state.steamSearch&&!state.loading.steamSearch)searchWorkshop(1);return;}if(b.hasAttribute('data-workshop-refresh')){load('steamSubscriptions');return;}if(b.hasAttribute('data-workshop-search'))return;if(b.hasAttribute('data-workshop-next')||b.hasAttribute('data-workshop-prev')){searchWorkshop(state.workshopPage+(b.hasAttribute('data-workshop-next')?1:-1));return;}if(b.dataset.workshopDetails){workshopDetails(b.dataset.workshopDetails);return;}if(b.hasAttribute('data-workshop-detail-close')){state.workshopDetailSequence++;state.steamDetails=null;state.errors.steamDetails=null;render();return;}
if(b.dataset.workshopDownload){if(!await confirm('确认订阅并下载？','工坊条目 '+b.dataset.workshopDownload+' 将通过 Steam 订阅和下载。依赖信息请先核对；下载后不会自动启用。'))return;const result=await run('steamDownload',{id:b.dataset.workshopDownload,confirmed:true});if(result&&!result.cancelled){await load('steamSubscriptions');await load('modCatalog');await libraryUi.inspectWorkshopComponent(b.dataset.workshopDownload);}return;}
if(b.hasAttribute('data-reminder-save')){const minutes=Number($('#reminder-minutes').value);await persistEditor('playtimeSettings',{reminderMinutes:minutes},async()=>{state.reminderMinutesDraft=null;state.reminderDismissed=false;await load('playtime');});return;}if(b.hasAttribute('data-reminder-dismiss')){const result=await run('playtimeSettings',{dismissReminder:true});if(result){state.reminderDismissed=reminderKey(state.playtime);render();}return;}
if(b.dataset.thumbRetry){thumbCache.delete(b.dataset.thumbRetry);thumbMissing.delete(b.dataset.thumbRetry);observeThumbnails();}
});
window.addEventListener('hashchange',()=>{const name=location.hash.slice(1);if(name!==state.route)route(name);});

function hasUnsavedDrafts(options={}){return !!(state.memoryDirty||state.settingsDirty||state.saveDirty||options.includeModProfiles!==false&&state.modProfileDirty||$('#server-editor')||state.reminderMinutesDraft!=null||editorSaves.size||(state.pendingMutations||0)>(options.pendingAllowance||0)||$('#server-workshop-import')?.value.trim());}
function validCoreStatus(value){return !!value&&value.protocol===1&&Number.isSafeInteger(value.revision)&&value.revision>=0&&typeof value.state==='string'&&typeof value.latestVersion==='string'&&typeof value.taskId==='string';}
function validCoreReady(value){return validCoreStatus(value)&&value.state==='Ready'&&/^[a-f0-9]{32}$/.test(value.taskId)&&!!value.latestVersion;}
function acceptCoreUpdate(value){
 // Checking results cannot roll a download/verified package back into discovery,
 // even when a late native progress message carries a newer global revision.
 if(['Downloading','Ready','Restarting'].includes(state.coreUpdate?.state)&&['Idle','Checking','Available','Latest','Unavailable'].includes(value?.state))return;
 if(startupUpdate.pending&&startupUpdate.generation!==coreRequestGeneration&&value?.state==='Error'&&!value.taskId)return;
 if(!validCoreStatus(value)){coreDownloadIntent=null;coreStatusVerified=false;state.coreUpdateError='更新状态读取失败，请重新检查或安装完整版本。';renderCoreUpdate();return;}if(value.revision<=(state.coreUpdate?.revision??-1))return;state.coreUpdate=value;coreStatusVerified=true;state.coreUpdateError='';observeCoreDownload(value);observeStartupCompletion(value);renderCoreUpdate();renderStartupNotice();offerCoreReady();
}
function bytes(n){return Number(n||0)<1024?Number(n||0)+' B':Number(n)<1048576?(Number(n)/1024).toFixed(1)+' KiB':(Number(n)/1048576).toFixed(1)+' MiB';}
function coreUpdatePanel(){const s=state.coreUpdate||{},phase=s.state||'Idle',working=['Checking','Downloading','Restarting'].includes(phase),labels={Idle:'尚未检查',Checking:'正在检查',Available:'可更新至 '+s.latestVersion,Latest:'已验证当前正式版本',Downloading:'正在下载并核验',Ready:'下载完成，等待重启安装',Cancelled:'已取消',Unavailable:'更新渠道暂不可用',Error:'更新未完成',Restarting:'正在重启',Succeeded:'更新成功',RolledBack:'已回退',Conflicted:'恢复受阻',PendingHealth:'正在验证新版界面'},tone=['Error','Unavailable','RolledBack','Conflicted'].includes(phase)?'status-warning':phase==='Ready'||phase==='Latest'||phase==='Succeeded'?'status-positive':'status-neutral';return `<article id="core-update-panel" class="card core-update-panel"><div class="row"><h2>启动器更新</h2><span class="badge ${tone}" role="status">${esc(labels[phase]||phase)}</span></div><p class="small">当前版本 <span data-core-current>${esc(s.currentVersion||'待读取原生版本')}</span> · 最新版本 ${esc(s.latestVersion||'未检查')}</p><div class="actions section"><button data-core-check data-safe ${working||phase==='Ready'?'disabled':''}>检查启动器更新</button>${['Available','Cancelled','Error'].includes(phase)&&s.latestVersion?'<button data-core-download data-safe class="primary">下载启动器更新</button>':''}${['Checking','Downloading'].includes(phase)?'<button data-core-cancel data-safe>取消启动器更新</button>':''}${phase==='Ready'?'<button data-core-apply data-safe class="primary">重启并更新</button>':''}</div><p class="small">检查时间：${s.checkedUtc?esc(localDate(s.checkedUtc)):'尚未检查'} · 启动时检查一次，不会自动下载</p>${state.coreUpdateError?'<p class="status-negative">'+esc(state.coreUpdateError)+'</p>':'<p>'+esc(s.message||'仅更新启动器自身，游戏与组件更新仍各自管理。')+'</p>'}${s.notes?'<p class="core-release-notes">'+esc(s.notes)+'</p>':''}${phase==='Downloading'||phase==='Ready'?'<progress max="'+Math.max(1,s.totalBytes||0)+'" value="'+(s.receivedBytes||0)+'" aria-label="启动器下载进度"></progress><div class="row small"><span>'+esc(bytes(s.receivedBytes))+' / '+esc(bytes(s.totalBytes))+'</span><span>'+esc(bytes(s.speedBytes))+'/s</span></div><p class="key">'+esc(s.currentFile||'文件已验证')+'</p>':''}${s.history?.length?'<section data-core-history><h3>启动器更新记录</h3>'+s.history.map(item=>'<p class="small"><strong>'+esc(({Succeeded:'更新成功',RolledBack:'已回退',Conflicted:'恢复受阻',PendingHealth:'正在验证新版界面'})[item.State]||item.State)+'</strong> · '+esc(item.Version)+' · '+esc(localDate(item.UpdatedUtc))+'<br>'+esc(item.Message||(item.State==='Succeeded'?'健康验证已完成':'等待完成验证或恢复'))+'</p>').join('')+'</section>':''}<p class="small">只重启启动器，游戏可以继续运行。</p></article>`;}
function renderCoreUpdate(){const panel=$('#core-update-panel');if(panel)panel.outerHTML=coreUpdatePanel();}
let coreRequestGeneration=0,coreDownloadIntent=null,corePrompting=false,coreStatusVerified=false;
// Automatic discovery owns neither the manual request generation nor download intent.
const startupUpdate={started:false,pending:false,awaitingCompletion:false,baselineRevision:-1,generation:0,version:'',dismissed:new Set()};
function observeStartupCompletion(value){
 if(!startupUpdate.pending||!startupUpdate.awaitingCompletion||value.revision<=startupUpdate.baselineRevision)return;
 if(startupUpdate.generation!==coreRequestGeneration){startupUpdate.pending=false;startupUpdate.awaitingCompletion=false;return;}
 if(value.state==='Checking')return;
 if(value.state==='Available')startupUpdate.version=value.latestVersion;
 startupUpdate.pending=false;startupUpdate.awaitingCompletion=false;
}
async function checkStartupUpdate(){
 if(startupUpdate.started||!state.status||!host||preview)return;
 startupUpdate.started=true;
 if(coreRequestGeneration||['Checking','Downloading','Ready','Restarting','PendingHealth'].includes(state.coreUpdate?.state))return;
 startupUpdate.pending=true;startupUpdate.generation=coreRequestGeneration;
 const current=()=>startupUpdate.generation===coreRequestGeneration;
 try{
  const initial=await request('coreUpdateStatus',{protocol:1});
  if(!current()||!validCoreStatus(initial))return;
  if(['Available','Latest'].includes(initial.state)&&initial.checkedUtc){if(initial.state==='Available')startupUpdate.version=initial.latestVersion;acceptCoreUpdate(initial);renderStartupNotice();return;}
  if(['Checking','Downloading','Ready','Restarting','PendingHealth'].includes(initial.state)){acceptCoreUpdate(initial);return;}
  // Check acknowledges with Checking; its terminal result arrives as progress.
  // Establish ownership before dispatch so an event racing the reply is covered.
  startupUpdate.baselineRevision=Math.max(initial.revision,state.coreUpdate?.revision??-1);
  startupUpdate.awaitingCompletion=true;
  const result=await request('coreUpdateCheck',{protocol:1});
  if(!current()||!validCoreStatus(result)){startupUpdate.awaitingCompletion=false;return;}
  acceptCoreUpdate(result);
 }catch{startupUpdate.awaitingCompletion=false;/* Startup failures never become global errors. */}
 finally{if(!startupUpdate.awaitingCompletion)startupUpdate.pending=false;}
}
function renderStartupNotice(){
 const el=$('#startup-update-notice');if(!el)return;
 const version=startupUpdate.version,visible=version&&!startupUpdate.dismissed.has(version)&&state.coreUpdate?.state==='Available'&&state.coreUpdate.latestVersion===version;
 el.hidden=!visible;
 if(visible){el.querySelector('[data-startup-version]').textContent='OZL '+version+' 可以更新';}
}
function observeCoreDownload(value){const intent=coreDownloadIntent;if(!intent||intent.consumed||intent.generation!==coreRequestGeneration)return;if(!validCoreStatus(value)){coreDownloadIntent=null;return;}if(value.revision<=intent.baselineRevision)return;if(!['Downloading','Ready'].includes(value.state)||value.latestVersion!==intent.version){coreDownloadIntent=null;return;}if(value.taskId&&(value.taskId===intent.baselineTask||intent.taskId&&value.taskId!==intent.taskId)){coreDownloadIntent=null;return;}if(value.state==='Downloading'){if(value.taskId)intent.taskId=value.taskId;return;}if(!validCoreReady(value)){coreDownloadIntent=null;return;}intent.taskId=value.taskId;intent.readyRevision=value.revision;}
function coreRestartBlock(target,generation){if(!coreStatusVerified||generation!==coreRequestGeneration||!validCoreReady(state.coreUpdate)||state.coreUpdate.taskId!==target.taskId||state.coreUpdate.latestVersion!==target.latestVersion)return '更新任务已变化，请重新检查更新。';if(!state.status)return '正在读取启动器状态，请稍后重试。';if(hasUnsavedDrafts())return '请先保存或放弃未保存的编辑，再重启更新。';if(state.busy||state.nativeBusy||state.pendingMutations||state.playApplying||state.playSaving||state.launchAttempt||state.buddyActive||state.restoreConfirming)return '另一个本机任务正在执行，请等待完成后手动重启更新。';if(document.querySelector('dialog[open]'))return '请先处理当前弹窗，再手动重启更新。';return '';}
function coreRestartMessage(message){state.coreUpdateError=message;notify(message);renderCoreUpdate();}
function offerCoreReady(){const intent=coreDownloadIntent,s=state.coreUpdate;if(!intent||intent.consumed||!intent.requestSucceeded||!intent.readyRevision||intent.generation!==coreRequestGeneration||!validCoreReady(s)||s.taskId!==intent.taskId||s.latestVersion!==intent.version)return;intent.consumed=true;void promptCoreRestart({taskId:s.taskId,latestVersion:s.latestVersion},intent.generation);}
async function promptCoreRestart(target={taskId:state.coreUpdate?.taskId,latestVersion:state.coreUpdate?.latestVersion},generation=coreRequestGeneration){if(corePrompting||state.coreApplying)return;corePrompting=true;try{let blocked=coreRestartBlock(target,generation);if(blocked){coreRestartMessage(blocked);return;}const readiness=await request('coreUpdatePreflight',{protocol:1});if(!readiness||readiness.protocol!==1||typeof readiness.allowed!=='boolean'||typeof readiness.message!=='string'){coreRestartMessage('未能核实原生重启条件，请稍后手动重启更新。');return;}if(!readiness.allowed){coreRestartMessage(readiness.message||'原生重启条件尚未满足，请处理后手动继续。');return;}blocked=coreRestartBlock(target,generation);if(blocked){coreRestartMessage(blocked);return;}if(!await confirm('重启并更新启动器？','下载完成，等待重启安装。只重启启动器，已验证的更新应用后自动重新打开；游戏可以继续。'))return;blocked=coreRestartBlock(target,generation);if(blocked){coreRestartMessage(blocked);return;}state.coreApplying=true;disable();acceptCoreUpdate(await request('coreUpdateApply',{protocol:1,confirmed:true,drafts:false}));}catch(error){coreRestartMessage(error.message);}finally{corePrompting=false;state.coreApplying=false;disable();renderCoreUpdate();}}
async function coreRequest(action){const passive=action==='coreUpdateStatus',generation=passive?coreRequestGeneration:++coreRequestGeneration,revision=state.coreUpdate?.revision??-1;if(!passive)coreDownloadIntent=action==='coreUpdateDownload'?{generation,version:state.coreUpdate?.latestVersion,baselineRevision:revision,baselineTask:state.coreUpdate?.taskId,taskId:'',readyRevision:0,requestSucceeded:false,consumed:false}:null;const intent=passive?null:coreDownloadIntent;try{const data=preview?{protocol:1,revision:1,state:'Unavailable',currentVersion:'预览模式',latestVersion:'',taskId:'',message:'预览不会联网检查或下载。'}:await request(action,{protocol:1});if(generation!==coreRequestGeneration)return;if(intent&&coreDownloadIntent===intent){observeCoreDownload(data);if(coreDownloadIntent===intent)intent.requestSucceeded=validCoreStatus(data)&&data.revision>revision&&['Downloading','Ready'].includes(data.state)&&data.latestVersion===intent.version;}acceptCoreUpdate(data);offerCoreReady();}catch(e){if(generation===coreRequestGeneration){if(!passive)coreDownloadIntent=null;if((state.coreUpdate?.revision??-1)<=revision){state.coreUpdateError=e.message;renderCoreUpdate();}}}}
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;
 if(b.hasAttribute('data-startup-dismiss')){startupUpdate.dismissed.add(startupUpdate.version);renderStartupNotice();return;}
 if(b.hasAttribute('data-core-open')){route('help');scrollMainTo('#core-update-panel');return;}
 if(b.hasAttribute('data-core-check')){await coreRequest('coreUpdateCheck');return;}
 if(b.hasAttribute('data-core-download')){await coreRequest('coreUpdateDownload');return;}
 if(b.hasAttribute('data-core-cancel')){await coreRequest('coreUpdateCancel');return;}
 if(b.hasAttribute('data-core-apply')){await promptCoreRestart();}
});

// Unified experience uses the existing authenticated resource and native envelope.
const experienceGroups={home:['home'],components:['components','mods','workshop'],saves:['saves'],servers:['servers'],memory:['memory'],settings:['settings','shaders'],help:['help','maintenance'],diagnostics:['diagnostics'],feedback:['feedback']};
Object.assign(state,{playProfiles:null,playDirty:false,playImportDraft:'',experienceReads:{},experienceErrors:{},actual:{},launchKind:'normal',launchId:'',launchAttempt:null,launchStage:'',launchFeedback:'',preflight:null,playPreview:null,diagnostics:null,modFilters:{profile:'all',save:'all'},bulkMods:{profile:new Set(),save:new Set()}});
['playProfileCapture','playProfileSave','playProfileDelete','playProfileSelect','playProfileApply','playProfileImport','playProfileRecover'].forEach(a=>mutationActions.add(a));
const previousDraftCheck=hasUnsavedDrafts;
hasUnsavedDrafts=options=>previousDraftCheck(options)||state.playDirty||!!state.playImportDraft;
const previousNotify=notify;
notify=function(text,origin=state.route){state.noticeRoute=origin;previousNotify(text);$('#notice').hidden=!text||origin!==state.route;};
const baseRender=render;let renderedRoute='';
const disclosureStates=new Map();
function disclosureKey(el){return el.dataset.disclosure||[el.closest('[data-card]')?.dataset.card||'',el.id,el.querySelector('summary')?.textContent].join('|');}
function rememberDisclosures(){if(!renderedRoute)return;disclosureStates.set(renderedRoute,[...document.querySelectorAll('#page details')].map(el=>({key:disclosureKey(el),open:el.open})));}
function restoreDisclosures(){const saved=disclosureStates.get(state.route)||[];for(const el of document.querySelectorAll('#page details')){const entry=saved.find(s=>s.key===disclosureKey(el));if(entry&&el.open!==entry.open){restoredDisclosures.add(el);el.open=entry.open;}}}
const sceneNames={original:'原封面',crossroads:'小镇路口',riverside:'雨后河畔',safehouse:'林间安全屋'},sceneSources={original:'home.png'},sceneLoaded=new Set(['original']),sceneFailed=new Set();
try{const data=JSON.parse($('#home-scene-data')?.textContent||'{}');for(const id of ['crossroads','riverside','safehouse'])if(typeof data[id]==='string'&&/^data:image\/png;base64,[A-Za-z0-9+/=]+$/.test(data[id]))sceneSources[id]=data[id];}catch{}
const sceneIds=Object.keys(sceneSources),sceneMotion=matchMedia('(prefers-reduced-motion: reduce)');
// The persistent page node owns one finite navigation presentation. Refreshes
// replace its contents without taking ownership or restarting the entrance.
let pageEntrance=null,navEntrance=null,lastPresentedRoute=null;
const presentationEffects=new Set(),targetPresentations=new WeakMap(),restoredDisclosures=new WeakSet();
function cancelTargetPresentation(element){const effect=targetPresentations.get(element);if(effect){effect.cancel();presentationEffects.delete(effect);targetPresentations.delete(element);}}
function ownPresentation(element,keyframes,options){
 cancelTargetPresentation(element);if(sceneMotion.matches||document.hidden||!element.isConnected)return null;
 const effect=element.animate(keyframes,options);presentationEffects.add(effect);targetPresentations.set(element,effect);
 const release=()=>{presentationEffects.delete(effect);if(targetPresentations.get(element)===effect)targetPresentations.delete(element);};
 effect.finished.then(release,release);return effect;
}
function cancelPresentation(){cancelPageEntrance();for(const effect of presentationEffects)effect.cancel();presentationEffects.clear();navEntrance=null;alignNavigation(false);}
function cancelPageEntrance(){if(pageEntrance){pageEntrance.cancel();pageEntrance=null;}}
function alignNavigation(animate=true){
 const nav=$('nav[aria-label="主导航"]'),target=nav?.querySelector('button.active');if(!target)return;
 let indicator=nav.querySelector('.nav-glass');
 if(!indicator){indicator=document.createElement('i');indicator.className='nav-glass';indicator.setAttribute('aria-hidden','true');nav.prepend(indicator);animate=false;}
 const from=indicator.getBoundingClientRect(),to=target.getBoundingClientRect(),parent=nav.getBoundingClientRect();
 const unchanged=Math.abs(parseFloat(indicator.style.top)-(to.top-parent.top))<.5&&Math.abs(parseFloat(indicator.style.left)-(to.left-parent.left))<.5&&Math.abs(parseFloat(indicator.style.width)-to.width)<.5&&Math.abs(parseFloat(indicator.style.height)-to.height)<.5;
 if(unchanged)return;
 navEntrance?.cancel();
 Object.assign(indicator.style,{top:to.top-parent.top+'px',left:to.left-parent.left+'px',width:to.width+'px',height:to.height+'px'});
 if(animate)navEntrance=ownPresentation(indicator,[{transform:`translate(${from.left-to.left}px,${from.top-to.top}px) scale(${from.width/to.width},${from.height/to.height})`},{transform:'translate(0,0) scale(1)'}],{duration:380,easing:'cubic-bezier(.22,.72,.18,1)'});
}
window.addEventListener('resize',()=>alignNavigation(false));
function presentPage(){
 cancelPageEntrance();const order=Object.keys(titles),direction=order.indexOf(state.route)>=order.indexOf(lastPresentedRoute)?1:-1;lastPresentedRoute=state.route;
 if(sceneMotion.matches||document.hidden)return;
 // Relative displacement stays inside the inset created by scaling at every width.
 const entrance=ownPresentation($('#page'),[{opacity:.28,transform:`translate(${direction}%,10px) scale(.972)`},{opacity:1,transform:'translate(0,0) scale(1)'}],{duration:380,easing:'cubic-bezier(.22,.72,.18,1)'});
 pageEntrance=entrance;
 entrance.finished.then(()=>{if(pageEntrance===entrance)pageEntrance=null;},()=>{});
}
sceneMotion.addEventListener('change',e=>{if(e.matches)cancelPresentation();});
document.addEventListener('visibilitychange',()=>{if(document.hidden)cancelPresentation();});
// Dialogs and disclosure panels share the same cancellable finite presentation.
new MutationObserver(records=>{
 for(const effect of presentationEffects)if(!effect.effect?.target?.isConnected){effect.cancel();presentationEffects.delete(effect);}
 // Coalesce rapid toggles; only the final state of each panel owns presentation.
 const panels=new Set(records.filter(r=>r.type==='attributes'&&r.target.matches('dialog,details')).map(r=>r.target));
 for(const panel of panels){const target=panel.matches('details')?panel.querySelector(':scope > :not(summary)'):panel;
  if(target)cancelTargetPresentation(target);
  const restored=restoredDisclosures.delete(panel);
  if(target&&panel.open&&!restored)ownPresentation(target,[{opacity:.35,transform:'translateY(12px) scale(.975)'},{opacity:1,transform:'translateY(0) scale(1)'}],{duration:220,easing:'cubic-bezier(.2,.75,.2,1)'});
 }
}).observe(document.body,{subtree:true,childList:true,attributes:true,attributeFilter:['open']});
$('#notice').setAttribute('role','status');$('#notice').setAttribute('aria-live','polite');
$('#task').setAttribute('role','status');$('#task').setAttribute('aria-live','polite');
let sceneId='original',scenePaused=sceneMotion.matches,sceneTimer=null,sceneNode=null,sceneLoadTicket=0,sceneAutoEpoch=0;
try{const pref=JSON.parse(localStorage.getItem('orange.homeScene'));if(pref&&sceneIds.includes(pref.id))sceneId=pref.id;if(typeof pref?.paused==='boolean')scenePaused=pref.paused;}catch{}
function saveScene(){try{localStorage.setItem('orange.homeScene',JSON.stringify({id:sceneId,paused:scenePaused}));}catch{}}
function paintScene(){
 const cover=$('[data-home-cover]');if(!cover)return;
 const paintedId=sceneLoaded.has(sceneId)?sceneId:'original';
 cover.dataset.sceneId=paintedId;
 cover.style.backgroundImage=`linear-gradient(0deg,rgba(5,13,14,.84),transparent 70%),url("${sceneSources[paintedId]}")`;
 const label=cover.querySelector('[data-scene-label]'),pause=cover.querySelector('[data-scene-pause]');
 if(label)label.textContent=sceneNames[paintedId];if(pause){pause.textContent=scenePaused?'继续轮播':'暂停轮播';pause.setAttribute('aria-pressed',String(scenePaused));}
}
function sceneEligible(cover){return cover&&!document.hidden&&!scenePaused&&!cover.matches(':hover')&&!cover.contains(document.activeElement)&&sceneIds.some(id=>id!=='original'&&!sceneFailed.has(id));}
async function selectScene(id,direction=1,automatic=false){
 const ticket=++sceneLoadTicket,cover=$('[data-home-cover]'),autoEpoch=sceneAutoEpoch;
 const current=()=>ticket===sceneLoadTicket&&cover===$('[data-home-cover]')&&state.route==='home'&&(!automatic||autoEpoch===sceneAutoEpoch&&sceneEligible(cover));
 for(let n=0;n<sceneIds.length;n++){
  if(!sceneFailed.has(id)){
   try{if(!sceneLoaded.has(id)){const image=new Image();image.src=sceneSources[id];await image.decode();sceneLoaded.add(id);}if(!current())return;sceneId=id;paintScene();saveScene();return;}
   catch{sceneFailed.add(id);}
  }
  if(!current())return;
  id=sceneIds[(sceneIds.indexOf(id)+direction+sceneIds.length)%sceneIds.length];
 }
 sceneId='original';paintScene();saveScene();
}
function syncHomeScene(){
 const cover=state.route==='home'?$('[data-home-cover]'):null;
 if(cover!==sceneNode){sceneNode=cover;sceneLoadTicket++;if(cover){
  cover.insertAdjacentHTML('beforeend',`<div class="scene-controls" data-scene-controls role="group" aria-label="首页封面"><button data-scene-prev data-safe aria-label="上一张封面">‹</button><span data-scene-label></span><button data-scene-next data-safe aria-label="下一张封面">›</button><button data-scene-pause data-safe aria-pressed="${scenePaused}"></button></div>`);
  paintScene();if(!sceneLoaded.has(sceneId))selectScene(sceneId);
 }}
 const eligible=sceneEligible(cover);
 // A suspension retires pending autoplay even if eligibility returns before decode finishes.
 if(!eligible){sceneAutoEpoch++;if(sceneTimer!==null){clearTimeout(sceneTimer);sceneTimer=null;}}
 if(eligible&&sceneTimer===null)sceneTimer=setTimeout(()=>{sceneTimer=null;selectScene(sceneIds[(sceneIds.indexOf(sceneId)+1)%sceneIds.length],1,true).finally(syncHomeScene);},8000);
}
document.addEventListener('click',e=>{const b=e.target.closest('[data-scene-prev],[data-scene-next],[data-scene-pause]');if(!b)return;if(b.hasAttribute('data-scene-pause')){scenePaused=!scenePaused;saveScene();paintScene();}else{const direction=b.hasAttribute('data-scene-prev')?-1:1;selectScene(sceneIds[(sceneIds.indexOf(sceneId)+direction+sceneIds.length)%sceneIds.length],direction);}syncHomeScene();});
for(const event of ['pointerover','pointerout','focusin','focusout'])document.addEventListener(event,e=>{if(e.target.closest?.('[data-home-cover]'))queueMicrotask(syncHomeScene);});
document.addEventListener('visibilitychange',syncHomeScene);
sceneMotion.addEventListener('change',e=>{if(e.matches){scenePaused=true;saveScene();paintScene();syncHomeScene();}});
function actualCover(){
 const version=state.status?.gameVersion,raw=state.status?.components,components=raw?.filter(c=>c.id!=='base'||!independentCore(raw)),configured=(components||[]).filter(c=>c.configured),heap=state.actual.memory?.currentGiB;
 return `<aside class="scene-status" data-home-actual aria-label="当前实际配置摘要"><h3>当前实际配置</h3><dl><dt>游戏版本</dt><dd>${esc(version?.value||(version?'未知':'待读取'))}</dd><dt>启动内存</dt><dd>${heap!=null?esc(heap)+' GiB':'待读取'}</dd><dt>下次启动启用组件</dt><dd>${components?configured.length+' 项':'待读取'}</dd></dl><p class="configured-names">${components?configured.map(c=>esc(c.title||c.id)).join(' · ')||'没有已配置组件':'组件状态待读取'}</p></aside>`;
}
function launcherTools(){return `<section class="launcher-tools" data-launcher-tools aria-label="常用工具"><h3>常用工具</h3><div class="launcher-tool-links"><button data-page="memory">修改内存</button><button data-page="maintenance" data-tool-focus="cache">清除缓存</button><button data-page="maintenance" data-tool-focus="workshop">重下工坊模组</button><button data-page="mods">更换模组</button></div></section>`;}
function arrangeHome(){
 const page=$('#page'),launch=$('#page .play-launch'),cover=$('[data-home-cover]');if(!launch||!cover)return;
 const s=state.status,core=(s?.components||[]).filter(c=>['buddy','viewpoint','chinese','base'].includes(c.id));
 if(s&&!s.validGame){const step=document.createElement('div');step.className='next-step';step.dataset.nextStep='';step.innerHTML=!s.validGame?'<strong>从游戏目录开始</strong><p>选择 Project Zomboid 的安装目录。</p><button data-action="chooseGame">选择游戏目录</button>':'<strong>选择你的游戏体验</strong><p>核心组件尚未配置，可按需要逐项安装。</p><button data-page="components">查看核心组件</button>';launch.querySelector('.launch-config').after(step);}
 const layout=document.createElement('div');layout.className='home-layout';cover.before(layout);layout.append(cover,launch);
 const copy=document.createElement('div');copy.className='scene-copy';while(cover.firstChild)copy.append(cover.firstChild);cover.append(copy);cover.insertAdjacentHTML('beforeend',actualCover());
 const config=$('#page .current-config'),actions=launch.querySelector('.launch-config .actions'),manage=document.createElement('details');manage.className='home-manage';manage.dataset.homeManage='';manage.dataset.disclosure='home-manage';manage.innerHTML='<summary id="home-manage-summary">管理配置、Java 授权与详情</summary>';if(!actions.querySelector('[data-configuration-create]'))manage.append(actions);config.querySelector('h2').textContent='当前配置摘要';launch.append(config,manage);
 const evidence=config.querySelector('details');if(evidence)manage.append(evidence);
 const java=$('#page .java-entry');if(java){java.classList.add('compact-java');manage.append(java);}
 const details=document.createElement('details');details.className='home-details';details.dataset.homeDetails='';details.dataset.disclosure='home-details';details.innerHTML='<summary id="home-details-summary">游玩计时、最近存档、服务器与硬件详情</summary>';layout.after(details);
 for(const selector of ['.home-targets','.playtime-panel','.hardware-summary']){const el=page.querySelector(selector);if(el)details.append(el);}
 const warning=details.querySelector('.hardware-warning');if(warning){warning.classList.add('notice');details.after(warning);}
 // Action results must remain visible when the scene occupies the first viewport.
 const preflightNeedsAction=state.preflight&&!state.preflight.canLaunch;
 const launchNeedsAction=state.status?.launch&&(state.status.launch.pending||state.status.launch.warning||['failed','conflict','timeout'].includes(state.status.launch.stage));
 for(const el of [...page.children])if(el.matches('.local-feedback,.status-warning,.hardware-warning')||preflightNeedsAction&&el.matches('.preflight-panel')||launchNeedsAction&&el.matches('.launch-status')||el.querySelector('.change-preview'))layout.before(el);
 const pendingJava=(state.java?.jars||[]).filter(j=>j.decision==null);if(pendingJava.length)layout.insertAdjacentHTML('beforebegin',`<div class="notice row" data-home-java-pending><span>${pendingJava.length} 个 Java 文件未决定 · 请逐个核对来源与完整哈希</span><button data-java-open>查看 Java 授权</button></div>`);
}
function restoreBlocked(){return state.busy||state.coreApplying||state.restoreConfirming?'另一个任务正在执行，请等待完成。':state.status?.buddyRecovery?.pending||state.playProfiles?.pendingRecovery?.pending?'存在待恢复操作，请先到修复核对。':'';}
function restoreResult(){const r=state.restoreResult;if(!r)return '';const completed=(r.message.match(/^\[完成\]/gm)||[]).length,failed=(r.message.match(/^\[失败\]/gm)||[]).length;return `<section class="notice" data-restore-result role="status"><div class="row"><strong>${r.failed?'恢复未完成，请核对各项结果':'恢复完成'}${completed||failed?' · 完成 '+completed+' · 失败 '+failed:''}</strong><button data-restore-dismiss aria-label="关闭恢复结果">关闭</button></div><details id="restore-report-${r.revision}"><summary>查看各项结果</summary><pre>${esc(r.message)}</pre></details><button data-page="maintenance">查看恢复与日志</button></section>`;}
const baseDisableLayout=disable;
disable=function(){baseDisableLayout();const restore=$('[data-components-restore]');if(restore){restore.disabled=!!restoreBlocked()||!!state.saveDeleting;restore.title=restoreBlocked();}for(const b of document.querySelectorAll('[data-save-delete]'))b.disabled=!!state.saveDeleting||!!saveDeleteBlocked();if(state.saveDeleting&&state.saveDetail?.id===state.saveDeleting.id)for(const el of document.querySelectorAll(editorLocks.saveSave.controls+','+editorLocks.saveSave.save))if(!el.disabled){el.dataset.busyDisabled='1';el.disabled=true;}};
mutationActions.add('saveDelete');
function saveDeleteBlocked(id){return state.busy||state.coreApplying||state.restoreConfirming||state.launchAttempt||state.playApplying||$('#confirm')?.open?'当前操作正在处理…':'';}
function saveDeleteIdentity(id){return JSON.stringify({epoch:state.experienceEpoch||0,game:state.status?.gamePath,config:state.status?.configStamp,route:state.route,save:state.saves?.saves.find(s=>s.id===id)});}
document.addEventListener('click',async e=>{
 const b=e.target.closest('[data-save-delete],[data-restore-dismiss]');if(!b||b.disabled)return;if(b.hasAttribute('data-restore-dismiss')){state.restoreResult=null;render();return;}if(state.saveDeleting)return;
 const id=b.dataset.saveDelete,blocked=saveDeleteBlocked(id);if(blocked){notify(blocked);return;}if(!state.saves?.saves.some(s=>s.id===id))return;
 const token={id,epoch:state.experienceEpoch||0},identity=saveDeleteIdentity(id),origin=state.route;state.saveDeleting=token;disable();
 try{
  const p=await request('saveDeletePreview',{id});if(identity!==saveDeleteIdentity(id))throw new Error('存档列表或游戏目录已变化，请重新读取后删除。');
  if(p.id!==id||typeof p.name!=='string'||typeof p.mode!=='string'||typeof p.path!=='string'||!/^saveDelete:[a-f0-9]{64}$/.test(p.stamp))throw new Error('删除预览无效，请重新读取后重试。');
  if(saveDeleteBlocked(id))throw new Error(saveDeleteBlocked(id));
  if(!await confirm(p.mode==='Multiplayer'?'删除本地多人存档？':'删除存档？','「'+p.name+'」的存档文件将被永久删除，无法撤销。'))return;
  if(identity!==saveDeleteIdentity(id))throw new Error('确认期间存档列表或游戏目录已变化，请重新读取后删除。');const conflict=saveDeleteBlocked(id);if(conflict)throw new Error(conflict);
  const result=await run('saveDelete',{id,stamp:p.stamp,confirmed:true});if(!result)return;if(token.epoch!==(state.experienceEpoch||0))return;if(result.status!=='completed'||result.id!==id)throw new Error('原生宿主未确认删除完成，请重新读取列表核对。');
  // Retire pending reads before refreshing, so an older reply cannot resurrect this save.
  state.saveReadSequence++;state.loading.saveDetail=false;if(state.saveDetail?.id===id){state.saveDetail=null;state.saveMods=[];state.saveDirty=false;state.sortPreview=null;}
  if(state.launchKind==='save'&&state.launchId===id){state.launchId='';state.manualTarget=true;state.playPreview=null;state.preflight=null;if(state.playProfiles?.selectedId||state.playSelecting)await selectPlay('');}
  if(token.epoch!==(state.experienceEpoch||0))return;
  if(state.saves)state.saves={...state.saves,saves:state.saves.saves.filter(s=>s.id!==id)};
  if(state.actual.saves)state.actual.saves={...state.actual.saves,saves:state.actual.saves.saves.filter(s=>s.id!==id)};
  state.savesReadTicket=null;state.loading.saves=false;delete state.experienceReads.saves;await Promise.all([load('saves'),experienceRead('saves','savesList')]);
 }catch(x){if(token.epoch===(state.experienceEpoch||0))notify(x.message,origin);}finally{if(state.saveDeleting===token){state.saveDeleting=null;render();disable();}}
});
document.addEventListener('click',async e=>{
 const b=e.target.closest('[data-components-restore]');if(!b)return;
 const blocked=restoreBlocked();if(blocked){notify(blocked);disable();return;}
 const identity=JSON.stringify({game:state.status?.gamePath,stamp:state.status?.configStamp,components:state.status?.components}),roof=state.status?.components?.find(c=>c.id==='roofs'&&c.configured),roofText=roof?(roof.warning||'完整屋顶可能留下已保存标记，请先按作者说明清理存档中的屋顶标记。'):'';
 state.restoreConfirming=true;disable();
 const agreed=await confirm('恢复全部组件？','将停用启动器管理的全部视角、Buddy、模型、射击、汉化和可选扩展，并撤销可识别的安装改动。\n保留已下载工坊文件、订阅、光影、个人设置和存档；不重置 Java 授权，不自动重新安装或下载。'+(roofText?'\n\n完整屋顶移除要求：'+roofText+'\n确认继续即表示已阅读并完成作者要求的清理。':''));
 state.restoreConfirming=false;disable();if(!agreed)return;
 const conflict=restoreBlocked();if(conflict){notify(conflict);return;}
 if(identity!==JSON.stringify({game:state.status?.gamePath,stamp:state.status?.configStamp,components:state.status?.components})){notify('实际组件或游戏目录已变化，请重新核对后恢复。');return;}
 const payload={component:'all',operation:'uninstall',confirmed:true};if(roofText)payload.roofRemovalAcknowledged=true;
 const epoch=state.experienceEpoch||0,options={restoreResultOwner:true};const result=await run('componentsAction',payload,options);if(epoch!==(state.experienceEpoch||0))return;invalidateActual(actualGlobals);
 const message=result?.message||options.error||'原生宿主没有返回恢复结果，请查看日志。';state.restoreResult={message,failed:!result||result.status==='partial'||result.status==='failed'||message.includes('[失败]'),revision:(state.restoreRevision=(state.restoreRevision||0)+1)};render();
});
render=function(){
 cancelChangedLaunch();
 rememberDisclosures();
 const same=renderedRoute===state.route,active=document.activeElement,focus=same&&active?.closest('#page')?{id:active.id,attrs:[...active.attributes].filter(a=>a.name.startsWith('data-')).map(a=>[a.name,a.value]),start:active.selectionStart,end:active.selectionEnd}:null;
 const scroll=same?[...document.querySelectorAll('main,.mod-list,.mod-detail,.enabled-mods,.play-launch')].map(el=>({key:el.matches('main')?'main':el.classList.contains('mod-list')?'.mod-list':el.classList.contains('mod-detail')?'.mod-detail':el.classList.contains('play-launch')?'.play-launch':'.enabled-mods',top:el.scrollTop})):[];
 const noticeNode=$('#notice'),taskNode=$('#task');$('#page').before(noticeNode,taskNode);
 baseRender();renderedRoute=state.route;const headingNode=$('#page .heading');if(headingNode)headingNode.after(noticeNode,taskNode);else $('#page').prepend(noticeNode,taskNode);noticeNode.hidden=!state.notice||state.noticeRoute!==state.route;
 if(!same)cancelPageEntrance();
 if(state.route==='settings')$('#page .heading')?.insertAdjacentHTML('afterend',launcherTools());
 const group=Object.keys(experienceGroups).find(k=>experienceGroups[k].includes(state.route))||'home',tabs=experienceGroups[group];
 $('#secondary-navigation').innerHTML=group==='home'?launcherTools():tabs.length>1?`<div class="secondary-tabs">${tabs.map(n=>`<button data-page="${n}" ${n===state.route?'aria-current="page"':''}>${esc(titles[n])}</button>`).join('')}</div>`:'';
 document.querySelectorAll('nav[aria-label="主导航"]>button').forEach(b=>{b.classList.toggle('active',b.dataset.page===group);if(b.dataset.page===group)b.setAttribute('aria-current','page');else b.removeAttribute('aria-current');});
 alignNavigation(!same);
 if(state.route==='maintenance')$('#page .heading')?.insertAdjacentHTML('afterend',logAnalysisEntry());
 if(state.route==='help'){$('#page').insertAdjacentHTML('beforeend',logAnalysisEntry());const heading=$('#page .heading');if(heading)$('#core-update-panel')?.before(heading);}
 if(state.route==='home')arrangeHome();
 if(state.route==='components'){
  $('#page .heading .actions')?.insertAdjacentHTML('beforeend',`<button data-components-restore class="danger" ${restoreBlocked()?'disabled':''}>恢复全部组件</button>`);
  const heading=$('#page .heading');if(heading)heading.insertAdjacentHTML('afterend',restoreResult());
 }
 restoreDisclosures();
 if(focus){let el=focus.id?document.getElementById(focus.id):focus.attrs.length?document.querySelector(focus.attrs.map(([k,v])=>`[${k}="${CSS.escape(v)}"]`).join('')):null;if(el&&!el.disabled){el.focus({preventScroll:true});if(focus.start!=null&&el.setSelectionRange)try{el.setSelectionRange(focus.start,focus.end);}catch{}}}
 for(const s of scroll){const el=$(s.key);if(el)el.scrollTop=s.top;}
 if(state.route==='home'&&state.status)ensureExperience();
 syncHomeScene();
 if(!same)presentPage();
};
const baseTask=task;
task=function(data){baseTask(data);if(state.launchAttempt||state.playApplying){if(data?.text)state.launchStage=data.text;const text=$('[data-launch-stage]');if(text)text.textContent=state.launchStage;$('#task').hidden=true;}};
const baseAcceptStatus=acceptStatus;
const actualGlobals=['memory','shaders','settings','mods'];
function invalidateActual(keys){for(const key of keys){delete state.actual[key];delete state.experienceErrors[key];delete state.experienceReads[key];}}
function actualWritten(action,data){const keys=({memorySave:['memory'],settingsSave:['settings'],shaderSelect:['shaders','settings'],importShader:['shaders','settings'],modProfileActivate:['mods'],modAutoSort:['mods'],saveSave:['saves'],serverUpsert:['servers'],serverDelete:['servers'],favoriteImport:['servers'],dlss:['settings'],profileRecover:['mods','saves'],profileLaunch:['mods','saves'],componentsAction:actualGlobals,maintenance:actualGlobals,buddyTakeover:actualGlobals,buddyRecover:actualGlobals,forceTakeoverApply:actualGlobals,forceTakeoverRecover:actualGlobals,playProfileApply:[...actualGlobals,'saves'],playProfileRecover:[...actualGlobals,'saves']})[action];if(!keys||data.cancelled||data.status&&!['completed','unchanged','recovered'].includes(data.status))return;invalidateActual(keys);if(state.route==='home')render();}
acceptStatus=function(data){const changed=state.status&&state.status.gamePath!==data.gamePath,stampChanged=state.status&&state.status.configStamp!==data.configStamp,versionChanged=JSON.stringify(state.status?.gameVersion)!==JSON.stringify(data.gameVersion);if(changed){cancelLaunchAttempt();if(state.launchAttempt?.submitted)state.launchAttempt=null;state.playProfiles=null;state.actual={};state.playPreview=null;state.preflight=null;state.diagnostics=null;resetLogAnalysis();state.restoreResult=null;state.launchFeedback='游戏目录已变化，请重新核对配置。';state.experienceEpoch=(state.experienceEpoch||0)+1;state.experienceReads={};state.experienceErrors={};}else if(stampChanged)invalidateActual(actualGlobals);const pendingBefore=JSON.stringify(state.status?.startup),attemptBefore=state.launchAttempt;baseAcceptStatus(data);reconcileLaunchPending(data.startup);if(pendingBefore!==JSON.stringify(data.startup)||attemptBefore!==state.launchAttempt){if(state.route==='home')render();else disable();}if((changed||stampChanged||versionChanged)&&state.route==='home'||changed&&state.route==='diagnostics')render();};
function reconcilePlayProfiles(value){
 if(!value||!Array.isArray(value.profiles)||typeof value.stamp!=='string'||!value.stamp||typeof value.selectedId!=='string'||value.profiles.some(p=>!p||typeof p.id!=='string'))throw new Error('游玩配置格式不可用，请重新读取。');
 const p=value.selectedProfile,rows=value.profiles.filter(p=>p.id===value.selectedId);
 if(value.selectedId?(!p||p.id!==value.selectedId||rows.length!==1||!['normal','save','server'].includes(p.targetKind)||typeof p.targetId!=='string'||(p.targetKind==='normal'?p.targetId!=='':!p.targetId)||rows[0].targetKind!==p.targetKind||rows[0].targetId!==p.targetId):p!==null)throw new Error('游玩配置选择关联不可用，请重新读取并选择。');
 if(p&&state.manualTarget&&(p.targetKind!==state.launchKind||p.targetId!==state.launchId))throw new Error('所选配置与启动目标不一致，请重新选择配置或清除选择。');
 state.playProfiles=value;if(p&&!state.manualTarget){state.launchKind=p.targetKind;state.launchId=p.targetId;}
 cancelChangedLaunch();
 const previousError=state.playSelectionError;state.playSelectionError='';if(previousError&&state.launchFeedback===previousError)launchMessage('');
}
function experienceRead(key,action){if(state.experienceReads[key])return state.experienceReads[key].promise;const token={epoch:state.experienceEpoch||0,selectionRevision:state.playSelectionRevision||0,selecting:!!state.playSelecting};state.experienceReads[key]=token;delete state.experienceErrors[key];const current=()=>state.experienceReads[key]===token&&token.epoch===(state.experienceEpoch||0),currentSelection=()=>!token.selecting&&!state.playSelecting&&token.selectionRevision===(state.playSelectionRevision||0);token.promise=(async()=>{try{const value=await request(action);if(!current())return;if(key==='profiles'){if(!currentSelection())return;reconcilePlayProfiles(value);}else state.actual[key]=value;}catch(e){if(current()&&(key!=='profiles'||currentSelection()))state.experienceErrors[key]=e.message;}finally{if(current()){delete state.experienceReads[key];if(state.route==='home')render();}}})();return token.promise;}
function ensureExperience(){for(const[key,action]of[['profiles','playProfiles'],['memory','memoryGet'],['shaders','shadersGet'],['settings','settingsGet'],['mods','modProfiles'],['saves','savesList'],['servers','serverList']])if(!(key==='profiles'?state.playProfiles:state.actual[key])&&!state.experienceReads[key]&&!state.experienceErrors[key])experienceRead(key,action);}
async function freshProfiles(){delete state.experienceErrors.profiles;await experienceRead('profiles','playProfiles');}
async function freshActual(){await Promise.all(['memory','shaders','settings','mods'].map(async key=>{delete state.experienceErrors[key];await experienceRead(key,{memory:'memoryGet',shaders:'shadersGet',settings:'settingsGet',mods:'modProfiles'}[key]);}));}
function targetOptions(kind,id){const rows=kind==='save'?(state.actual.saves?.saves||state.saves?.saves||[]).filter(x=>x.editable!==false):(state.actual.servers?.servers||state.servers?.servers||[]);return '<option value="">选择本机目标</option>'+rows.map(x=>`<option value="${esc(x.id)}" ${x.id===id?'selected':''}>${esc(x.name||x.id)}</option>`).join('')+(id&&!rows.some(x=>x.id===id)?`<option value="${esc(id)}" selected>目标不可用 · ${esc(id)}</option>`:'');}
function recoveryPanel(){const r=state.playProfiles?.pendingRecovery;if(!r?.pending)return '';return `<div class="card recovery-panel"><span class="badge status-negative">游玩配置应用尚未恢复</span><p>${r.locations.map(esc).join('、')||'恢复所属目录待核对'}</p>${r.warnings.map(w=>`<p class="small">${esc(w)}</p>`).join('')}${r.canRecover?'<button data-play-recover>核对并恢复原配置</button>':'<p>请核对上方恢复提示与文件状态。</p>'}<button data-action="chooseGame">选择恢复所属游戏目录</button></div>`;}
function preflightPanel(){const p=state.preflight;if(!p)return '';const warnings=(p.items||[]).filter(i=>i.status==='warning').length;return `<section class="card preflight-panel ${p.canLaunch?'is-clear':''}" aria-label="启动前体检"><details data-disclosure="preflight-${state.preflightRevision||0}" ${p.canLaunch?'':'open'}><summary class="preflight-summary"><span class="badge ${p.canLaunch?'status-positive':'status-negative'}">${p.canLaunch?'检查通过':'需要处理'}</span><strong>${p.canLaunch?'启动检查已通过':'启动前体检'}</strong><span class="small">${p.canLaunch?(warnings?`${warnings} 条提示，不影响启动 · `:'')+'查看详情':'查看阻断原因'}</span></summary><p class="small">${esc(localDate(p.checkedUtc))}</p>${(p.items||[]).map(i=>`<div class="preflight-item"><span class="badge ${i.status==='error'?'status-negative':i.status==='warning'?'status-warning':'status-positive'}">${i.status==='error'?'阻断':i.status==='warning'?'提醒':'已核对'}</span><div><strong>${esc(i.title)}</strong><p>${esc(i.message)}</p>${Array.isArray(i.details)&&i.details.length?`<ul class="preflight-details">${i.details.map(detail=>`<li>${esc(detail)}</li>`).join('')}</ul>`:''}${i.id==='mods'&&i.canAutoSort===true?`<button data-preflight-auto-sort class="primary" ${state.preflightRepairing||state.launchAttempt?'disabled':''}>${state.preflightRepairing?'正在自动排序…':'自动排序并继续启动'}</button>`:''}</div>${i.status!=='ok'?`<button data-preflight-repair="${esc(i.route)}">查看处理入口</button>`:''}</div>`).join('')}</details></section>`;}
function changeValue(v){return v===null?'原来不存在':Array.isArray(v)?v.join('、')||'空清单':String(v??'未指定');}
function changesText(p){return (p.changes||[]).map(c=>`${c.kind==='mods'?(c.key==='saveMods'?'存档 '+p.profile.targetId+' 的模组':'默认模组'):c.key}：${changeValue(c.before)} → ${changeValue(c.after)}`).join('\n');}
function previewPanel(){const p=state.playPreview;if(!p)return '';return `<section class="card"><h2>应用预览 · ${esc(p.profile?.name||'游玩配置')}</h2><p>内存、光影、Viewpoint 参数是全局设置；模组清单按普通 / 所选存档目标应用。联机不套用普通模组。</p>${(p.errors||[]).map(x=>`<p class="status-negative">${esc(x)}</p>`).join('')}${(p.warnings||[]).map(x=>`<p class="status-warning">${esc(x)}</p>`).join('')}<pre class="change-preview">${esc(changesText(p)||'当前实际配置没有需要应用的变化。')}</pre><p class="small">受影响文件：${[...new Set((p.changes||[]).map(c=>c.kind==='heap'?'启动 JSON':c.kind==='mods'?c.key==='saveMods'?'所选存档模组清单':'默认模组清单':'Viewpoint 实际设置'))].map(esc).join('、')||'无'}</p><button data-play-apply ${!p.canApply||!(p.changes||[]).length?'disabled':''}>${state.playApplying?'正在应用配置':'确认应用配置'}</button></section>`;}
function launchIntent(){const kind=state.launchKind,rows=kind==='save'?(state.actual.saves?.saves||state.saves?.saves||[]):(state.actual.servers?.servers||state.servers?.servers||[]),target=rows.find(s=>s.id===state.launchId&&(kind!=='save'||s.editable!==false));return kind==='normal'?{label:'启动到主菜单',help:'进入游戏主菜单，再选择存档或多人游戏。'}:target?{label:(kind==='save'?'继续：':'连接：')+(target.name||target.id),help:kind==='save'?'继续所选单人存档。':'使用已保存的连接信息进入服务器。'}:{label:kind==='save'?'选择存档后继续':'选择服务器后连接',help:kind==='save'?'请先选择可用的本机单人存档。':'请先选择可用的已保存服务器。'};}
function home(){const list=state.playProfiles,selected=list?.selectedProfile,actual=state.actual,m=actual.memory,shader=actual.shaders?.packs?.find(x=>x.id===actual.shaders.selected),targets=state.launchKind!=='normal';return `<section class="home-cover" data-home-cover aria-labelledby="home-cover-title"><div class="eyebrow">OZL / 下一次游玩</div><h1 id="home-cover-title">准备好你的生存之旅</h1></section>${recoveryPanel()}<section class="card play-launch"><div class="launch-config"><label for="play-profile-select">游玩配置</label><select id="play-profile-select" ${!list?'disabled':''}><option value="">当前实际配置</option>${(list?.profiles||[]).map(p=>`<option value="${esc(p.id)}" ${p.id===list.selectedId?'selected':''}>${esc(p.name)}</option>`).join('')}</select><p data-selected-profile>${selected?'已选择 '+esc(selected.name)+' · 待应用':'使用当前实际配置'}</p><div class="actions"><button data-play-capture ${!list?'disabled':''}>捕获当前配置</button><button data-play-edit ${!selected?'disabled':''}>编辑所选配置</button><button data-play-preview ${!selected?'disabled':''}>查看应用预览</button><button data-play-share ${!list?'disabled':''}>导入 / 分享</button><button data-play-refresh>重新读取</button></div></div><div class="launch-target"><label>启动目标<select id="launch-target-kind"><option value="normal" ${state.launchKind==='normal'?'selected':''}>进入主菜单</option><option value="save" ${state.launchKind==='save'?'selected':''}>继续单人存档</option><option value="server" ${state.launchKind==='server'?'selected':''}>连接服务器</option></select></label>${targets?`<label>本机目标<select id="launch-target-id">${targetOptions(state.launchKind,state.launchId)}</select></label>`:''}<button id="launch" data-action="launch" class="primary" ${!state.status?.validGame||state.status.running||launchPending()?'disabled':''}>${esc(launchPending()?'正在启动游戏…':launchIntent().label)}</button><p class="small" data-launch-help>${esc(state.status?.running?'游戏正在运行':launchIntent().help)}</p></div></section><section class="card current-config"><h2>当前实际配置</h2><p data-current-config>${m?.currentGiB!=null?esc(m.currentGiB)+' GiB Java 堆':'内存待读取'} · ${esc(shader?.name||actual.shaders?.selected||'光影待读取')} · ${actual.mods?'默认模组 '+asList(actual.mods.activeMods).length+' 个':'默认模组待读取'}</p><details><summary>配置详情</summary><p>Viewpoint 可编辑参数 ${actual.settings?(actual.settings.rows||[]).filter(x=>x.editable).length+' 项':'待读取'} · 已配置组件 ${(state.status?.components||[]).filter(c=>c.configured).length} / ${(state.status?.components||[]).length} 项。</p><button data-page="settings">查看实际参数</button><button data-page="components">管理组件</button></details></section><div class="local-feedback" data-launch-feedback role="status"><span data-launch-stage>${esc(state.launchAttempt||state.playApplying?state.launchStage:state.launchFeedback)}</span>${state.launchAttempt||state.playApplying?'<progress data-experience-progress aria-label="启动请求阶段"></progress>':''}</div>${Object.entries(state.experienceErrors).map(([k,v])=>`<p class="status-warning">${esc(v)}</p>`).join('')}${Object.keys(state.experienceReads).length?'<p class="small" role="status">正在读取本机配置…</p>':''}${previewPanel()}${preflightPanel()}<div class="home-targets"><section class="card"><h2>最近修改存档</h2>${(actual.saves?.saves||[]).slice().sort((a,b)=>String(b.modifiedUtc).localeCompare(String(a.modifiedUtc))).slice(0,3).map(s=>`<button data-recent-target="save" data-target-id="${esc(s.id)}">${esc(s.name)} · ${esc(shortDate(s.modifiedUtc))}</button>`).join('')||'<p class="small">没有已读取的可用存档。</p>'}</section><section class="card"><h2>已保存服务器</h2>${(actual.servers?.servers||[]).slice(0,3).map(s=>`<button data-recent-target="server" data-target-id="${esc(s.id)}">${esc(s.name)}</button>`).join('')||'<p class="small">没有已读取的个人服务器。</p>'}</section></div>`+playtimePanel()+gameProcessPanel();};
function attemptIdentity(){return JSON.stringify({game:state.status?.gamePath,config:state.status?.configStamp,profile:state.playProfiles?.selectedId||'',stamp:state.playProfiles?.stamp,kind:state.launchKind,id:state.launchId});}
function launchIntentIdentity(){return JSON.stringify({game:state.status?.gamePath,epoch:state.experienceEpoch||0,profileId:state.playProfiles?.selectedId||'',profile:state.playProfiles?.selectedProfile||null,kind:state.launchKind,id:state.launchId});}
function launchDraftIdentity(){return JSON.stringify({revision:state.launchDraftRevision||0,save:state.saveDetail?state.saveDetail.id:'none',play:$('#play-editor')?{id:state.playDraft?.id||'',epoch:state.playEditorEpoch}:null,server:!!$('#server-editor')});}
function assertLaunchDrafts(expected){if(hasUnsavedDrafts({includeModProfiles:false})||expected!==launchDraftIdentity()){const message='编辑状态已变化或存在未保存编辑，请先保存或重新读取，再明确启动。';notify(message,state.route);throw new Error(message);}}
const launchDraftControls='#memory-number,#memory-range,[data-setting],#server-form input,#server-form select,#reminder-minutes,#play-editor input,#play-editor select,#play-editor textarea,#play-import-json,#server-workshop-import';
for(const name of ['input','change'])document.addEventListener(name,e=>{if(e.target.matches(launchDraftControls))invalidateLaunchDrafts();},true);
document.addEventListener('click',e=>{const b=e.target.closest('button');if(b&&!b.disabled&&b.matches('[data-mod-up],[data-mod-down],[data-mod-remove],[data-mod-add],[data-sort-apply="save"],[data-save-profile-load],[data-bulk-add],[data-bulk-remove],[data-save-open],[data-play-edit],[data-play-capture],[data-play-close]')&&b.dataset.bulkKind!=='profile')invalidateLaunchDrafts();},true);
function invalidateLaunchDrafts(){state.launchDraftRevision=(state.launchDraftRevision||0)+1;cancelLaunchAttempt();}
function launchReplyCurrent(token){
 if(!token)return true;if(state.launchAttempt!==token||token.cancelled)return false;if(token.submitted)return true;
 const context=token.context;
 // Allow the one native launch request itself; required drafts and other mutations still block.
 return !context||context.epoch===(state.experienceEpoch||0)&&context.intent===launchIntentIdentity()&&context.drafts===launchDraftIdentity()&&!hasUnsavedDrafts({includeModProfiles:false,pendingAllowance:1});
}
function cancelChangedLaunch(){if(state.launchAttempt?.context&&!launchReplyCurrent(state.launchAttempt))cancelLaunchAttempt();}
function nativeLaunchPending(){return state.status?.startup?.pending===true;}
function launchPending(){return !!state.launchAttempt||nativeLaunchPending();}
function reconcileLaunchPending(value){const token=state.launchAttempt;if(!value||!/^[a-f0-9]{32}$/.test(value.startupId||''))return;if(token&&token.startupId===value.startupId&&!token.cancelled){if(value.pending&&value.phase==='submitted'){token.submitted=true;state.launchStage=value.message||'启动请求已提交，正在等待本次游戏进程…';}else if(token.submitted&&!value.pending){state.launchAttempt=null;state.launchFeedback=value.message||'';}}else if(!token&&value.pending)state.launchFeedback=value.message||'启动请求已提交，正在等待本次游戏进程…';}
async function pollLaunchPending(){if(state.startupReading||!state.status||!nativeLaunchPending()&&!state.launchAttempt?.submitted)return;const ticket={gamePath:state.status.gamePath,epoch:state.experienceEpoch||0,startupId:state.launchAttempt?.startupId||state.status.startup.startupId};state.startupReading=ticket;try{const value=await request('startupStatus'),currentId=state.launchAttempt?.startupId||state.status?.startup?.startupId;if(state.status?.gamePath===ticket.gamePath&&(state.experienceEpoch||0)===ticket.epoch&&currentId===ticket.startupId&&value.startup?.startupId===ticket.startupId)acceptStatus({...state.status,startup:value.startup,running:!!value.running});}catch{}finally{if(state.startupReading===ticket)state.startupReading=null;}}
setInterval(pollLaunchPending,1000);
function cancelLaunchAttempt(){const token=state.launchAttempt;if(!token||token.cancelled||token.submitted)return;token.cancelled=true;state.preflight=null;state.launchFeedback='';if(token.startupId)void request('startupCancel',{startupId:token.startupId}).catch(()=>{});}
function acceptLaunchPreflight(report){state.preflight=report;state.preflightRevision=(state.preflightRevision||0)+1;render();}
function launchMessage(text){state.launchFeedback=text;const el=$('[data-launch-stage]');if(el)el.textContent=text;}
function assertProfileTarget(profile=state.playProfiles?.selectedProfile){if(state.playSelecting||state.playSelectionError)throw new Error(state.playSelectionError||'正在核对配置选择，请等待完成后重新操作。');const selected=state.playProfiles?.selectedId;if(!selected&&!profile)return;if(!selected||!profile||profile.id!==selected||profile.targetKind!==state.launchKind||profile.targetId!==state.launchId)throw new Error('所选配置与启动目标不一致，请重新选择配置或清除选择。');}
async function selectPlay(id){if(!state.playProfiles)return false;state.playSelectionRevision=(state.playSelectionRevision||0)+1;const token={epoch:state.experienceEpoch||0},current=()=>state.playSelecting===token&&token.epoch===(state.experienceEpoch||0);state.playSelecting=token;state.playSelectionError='';state.playPreview=null;state.preflight=null;try{const result=await request('playProfileSelect',{stamp:state.playProfiles.stamp,id});if(!current())return false;if(typeof result.stamp!=='string'||result.selectedId!==id||(id?result.profile?.id!==id:result.profile!==null))throw new Error('配置选择回复已失效，请重新读取并选择。');state.playProfiles={...state.playProfiles,stamp:result.stamp,selectedId:result.selectedId,selectedProfile:result.profile};if(result.profile){state.manualTarget=false;state.launchKind=result.profile.targetKind;state.launchId=result.profile.targetId;}render();return true;}catch(e){if(current()){state.playSelectionError=e.message;launchMessage(e.message);}return false;}finally{if(state.playSelecting===token)state.playSelecting=null;}}
async function previewPlay(id=state.playProfiles?.selectedId){assertProfileTarget();if(!id||!state.playProfiles)return null;const identity=attemptIdentity(),result=await request('playProfilePreview',{stamp:state.playProfiles.stamp,id});if(identity!==attemptIdentity())throw new Error('所选配置或游戏目录已变化，请重新核对。');assertProfileTarget(result.profile);state.playPreview=result;render();return result;}
async function applyPlay(p,continuing=false,drafts=launchDraftIdentity()){if(!p?.canApply||!p.previewStamp)return false;assertProfileTarget(p.profile);if(hasUnsavedDrafts({includeModProfiles:false})){launchMessage('请先保存或放弃未保存的编辑，再应用游戏配置。');return false;}const identity=attemptIdentity();if(state.launchAttempt?.startupId)await request('startupYield',{startupId:state.launchAttempt.startupId});if(!await confirm(continuing?'应用并继续启动？':'应用所选配置？',changesText(p)+'\n\n内存、光影、Viewpoint 参数是全局游戏设置。受影响文件按预览列出；不会修改地图、角色或车辆。'))return false;if(state.launchAttempt?.startupId)await request('startupResume',{startupId:state.launchAttempt.startupId});if(identity!==attemptIdentity())throw new Error('确认期间配置已变化，请重新预览。');assertProfileTarget(p.profile);assertLaunchDrafts(drafts);state.playApplying=true;state.launchStage='正在应用已确认的配置…';render();disable();try{const result=await request('playProfileApply',{id:p.profile.id,previewStamp:p.previewStamp,confirmed:true});launchMessage(result.message);state.playPreview=null;if(!['completed','unchanged'].includes(result.status)){if(result.pendingRecovery&&state.playProfiles)state.playProfiles.pendingRecovery=result.pendingRecovery;return false;}actualWritten('playProfileApply',result);await refresh();await freshActual();await freshProfiles();return true;}catch(e){state.playPreview=null;throw e;}finally{state.playApplying=false;render();disable();}}
async function launchExperience(kind,id){if(launchPending()||state.status?.running)return;if(hasUnsavedDrafts({includeModProfiles:false})){launchMessage('请先保存或重新读取未保存的编辑，再启动。');return;}const clearSelection=!!kind&&!!(state.playProfiles?.selectedId||state.playSelecting);if(kind){state.launchKind=kind;state.launchId=id;state.manualTarget=true;}if(state.launchKind!=='normal'&&!state.launchId){launchMessage('请先选择有效的本机存档或服务器目标。');return;}const token={};state.launchAttempt=token;state.launchStage='正在启动游戏…';render();try{
 const target={kind:state.launchKind,id:state.launchId},drafts=launchDraftIdentity();const begin=await request('startupBegin',{targetKind:target.kind,targetId:target.kind==='normal'?'':target.id});if(!/^[a-f0-9]{32}$/.test(begin?.startupId||''))throw new Error('启动准备回复无效，请重试。');token.startupId=begin.startupId;if(token.cancelled)throw new Error('本次启动准备已取消，请重新点击启动。');assertLaunchDrafts(drafts);if(clearSelection&&!await selectPlay(''))return;if(target.kind!==state.launchKind||target.id!==state.launchId)throw new Error('启动选择已变化，请重新核对。');assertProfileTarget();let identity=attemptIdentity();const intent=launchIntentIdentity();if(state.playProfiles?.selectedId){const p=await previewPlay();assertLaunchDrafts(drafts);if(identity!==attemptIdentity())throw new Error('启动选择已变化，请重新核对。');if(!p.canApply){launchMessage((p.errors||[]).join('；')||'配置暂不可应用');return;}if(p.changes.length){if(!await applyPlay(p,true,drafts))return;assertLaunchDrafts(drafts);if(intent!==launchIntentIdentity())throw new Error('启动选择已变化，请重新核对。');identity=JSON.stringify({...JSON.parse(identity),config:state.status?.configStamp,stamp:state.playProfiles?.stamp});}}

 assertLaunchDrafts(drafts);if(identity!==attemptIdentity()||intent!==launchIntentIdentity())throw new Error('启动选择已变化，请重新核对。');state.launchStage='正在启动游戏…';token.context={epoch:state.experienceEpoch||0,intent:launchIntentIdentity(),drafts:launchDraftIdentity()};render();const result=await run(target.kind==='normal'?'launch':'profileLaunch',{startupId:token.startupId,...(target.kind==='normal'?{}:{kind:target.kind,id:target.id})},{quiet:true,launchToken:token});if(result&&launchReplyCurrent(token)){reconcileLaunchPending(result.startup);if(!token.submitted)launchMessage(result.warning||'');}
 }catch(e){launchMessage(e.message);}finally{if(!token.submitted&&token.startupId)try{await request('startupCancel',{startupId:token.startupId});}catch{}if(state.launchAttempt===token&&!token.submitted)state.launchAttempt=null;render();}}
async function autoSortAndLaunch(){if(state.preflightRepairing||state.launchAttempt||state.busy||state.coreApplying)return;if(!(state.preflight?.items||[]).some(i=>i.id==='mods'&&i.canAutoSort===true))return;assertProfileTarget();const intent=launchIntentIdentity(),drafts=launchDraftIdentity();assertLaunchDrafts(drafts);state.preflightRepairing=true;launchMessage('正在自动排序默认模组清单…');render();try{const options={},result=await run('modAutoSort',{confirmed:true},options);if(!result){launchMessage(options.error||'自动排序未完成，请核对后重试。');return;}assertLaunchDrafts(drafts);if(intent!==launchIntentIdentity())throw new Error('启动选择已变化，请重新核对。');await launchExperience();}finally{state.preflightRepairing=false;render();}}
function diagnosticsPanel(){const d=state.diagnostics;return `<section class="card diagnostics" data-diagnostics><div class="row"><h2>故障诊断</h2><button data-diagnostics-read ${state.diagnosticsReading?'disabled':''}>${state.diagnosticsReading?'正在读取日志…':'读取日志报告'}</button></div>${state.diagnosticsError?`<p class="status-warning">${esc(state.diagnosticsError)}</p>`:''}${d?`<p class="small">核对时间：${esc(localDate(d.checkedUtc))}</p>${d.warnings.map(w=>`<p class="status-warning">${esc(w.message)}</p>`).join('')}${d.findings.length?d.findings.map(f=>{const s=d.sources.find(x=>x.id===f.source),age=s?.freshness==='unknown'||d.warnings.some(w=>w.id==='source-age-unknown'&&w.source===f.source)?'时间未知':f.historical?'历史记录':'近期日志';return `<article data-diagnostic-finding><div class="row"><h3>${esc(f.title)}</h3><span class="badge ${f.severity==='error'?'status-negative':f.severity==='warning'?'status-warning':'status-neutral'}">${f.confidence==='confirmed'?'日志报错':'相关线索'} · ${age}</span></div><p>${esc(f.message)}</p><p class="small">来源：${esc(s?.label||f.source)} · ${s?.kind==='launcher-log'?'启动器日志':s?.kind==='jvm-crash'?'虚拟机崩溃日志':'游戏日志'}</p><details><summary>日志详情（脱敏）</summary><pre>${esc(f.evidence.join('\n'))}</pre></details><p>下一步：${esc(f.nextStep)}</p></article>`;}).join(''):'<p>未发现常见错误记录。</p>'}<details><summary>日志来源</summary>${d.sources.map(s=>`<p class="small">${esc(s.label)} · ${esc(({read:'已读取',missing:'文件不存在',unreadable:'读取失败',rejected:'拒绝读取',skipped:'未读取'})[s.status]||s.status)} · ${s.freshness==='unknown'?'时间未知':s.freshness==='historical'?'历史记录':'近期记录'}${s.truncated?' · 已截取有界内容':''}</p>`).join('')}</details><button data-diagnostics-copy>复制脱敏报告</button><textarea id="diagnostics-report" readonly aria-label="脱敏诊断报告">${esc(d.reportText)}</textarea><p data-copy-feedback role="status"></p>`:''}</section>`;}
async function copyText(value,field,feedback){try{await navigator.clipboard.writeText(value);feedback.textContent='已复制脱敏 / 便携文本。';}catch{field.focus();field.select();feedback.textContent='剪贴板不可用，请手动复制选中的文本。';}}
function closePlayDialog(id){const d=$(id);if(d){d.close();d.remove();cancelChangedLaunch();}}
function lockPlayEditor(locked){const d=$('#play-editor');if(!d)return;d.querySelectorAll('input,select,textarea,button').forEach(el=>{if(locked&&!el.disabled){el.disabled=true;el.dataset.playSaveLock='1';}else if(!locked&&el.dataset.playSaveLock){delete el.dataset.playSaveLock;el.disabled=false;}});if(!locked&&$('#play-edit-kind'))updatePlayEditorTarget();}
async function closeEditor(){if(state.playSaving)return;if(state.playDirty&&!await confirm('放弃未保存的游玩配置？','草稿尚未保存；游戏配置没有改变。'))return;state.playDirty=false;closePlayDialog('#play-editor');}
async function closeShare(){if(state.playImportDraft&&!await confirm('放弃尚未导入的文本？','文本尚未保存为游玩配置。'))return;state.playImportDraft='';closePlayDialog('#play-share');}
const previousDisable=disable;
disable=function(){previousDisable();document.querySelectorAll(unsafeControls+',#launch,#play-profile-select,[data-play-apply],[data-play-recover]').forEach(el=>{if(state.playApplying&&!el.disabled){el.disabled=true;el.dataset.playApplyLock='1';}else if(!state.playApplying&&el.dataset.playApplyLock){delete el.dataset.playApplyLock;el.disabled=false;}});if(state.playSaving)lockPlayEditor(true);};
const originalCatalog=richCatalog;
function modUpdateEvidence(m){const id=m.workshopId||m.item;return id?(state.updates?.items||[]).find(x=>String(x.item)===String(id))?.state||'unknown':'unknown';}
richCatalog=function(kind){if(!state.modCatalog)return originalCatalog(kind);const original=state.modCatalog,filter=state.modFilters[kind],draft=kind==='profile'?!!state.modProfile:!!state.saveDetail,ids=kind==='profile'?(draft?state.modProfileMods:asList(state.modProfiles?.activeMods)):state.saveMods,selected=state.bulkMods[kind],readonly=kind==='save'&&state.saveDetail?.editable===false;
 const mods=original.mods.filter(m=>filter==='enabled'?ids.includes(m.id):filter==='disabled'?!ids.includes(m.id):filter==='updates'?modUpdateEvidence(m)==='available':filter==='dependencies'?asList(m.requires??m.require).some(id=>!ids.includes(id)):true);
 state.modCatalog={...original,mods};const originalIds=state.modProfileMods;if(kind==='profile'&&!draft)state.modProfileMods=ids;let html;try{html=originalCatalog(kind);}finally{state.modCatalog=original;state.modProfileMods=originalIds;}
 let row=0;html=html.replace(/<article[^>]*role="listitem">/g,match=>{const m=mods.filter(m=>[m.id,modTitle(m),m.author,m.description].join(' ').toLowerCase().includes((kind==='profile'?state.profileSearch:state.saveSearch).toLowerCase()))[row++];return match+(m?`<input type="checkbox" data-bulk-mod="${esc(m.id)}" data-bulk-kind="${kind}" aria-label="多选 ${esc(modTitle(m))}" ${selected.has(m.id)?'checked':''} ${!draft||readonly||m.conflict||m.componentManaged!==false?'disabled':''}>`:'');});
 const unknown=original.mods.filter(m=>modUpdateEvidence(m)==='unknown').length;const unavailable=original.mods.filter(m=>m.componentManaged!==false).length;
 return `<div class="mod-filter-bar"><label>${draft?'当前草稿':'当前默认清单'}<select data-mod-filter data-mod-kind="${kind}">${[['all','全部'],['enabled',draft?'当前草稿已包含':'当前默认已包含'],['disabled',draft?'当前草稿未包含':'当前默认未包含'],['updates','有更新'],['dependencies','缺少清单依赖']].map(([id,label])=>`<option value="${id}" ${filter===id?'selected':''}>${label}</option>`).join('')}</select></label><button data-check-updates>核对工坊更新（不下载）</button><span class="small">更新状态未知：${unknown} 个。${unavailable?' 组件页管理或未分类：'+unavailable+' 个，不参与批量。':''}</span></div>${selected.size?`<div class="bulk-actions" role="toolbar" aria-label="当前草稿批量操作"><span>已选 ${selected.size} 个 · 仅修改当前草稿</span><button data-bulk-add data-bulk-kind="${kind}" ${!draft||readonly?'disabled':''}>批量添加到草稿</button><button data-bulk-remove data-bulk-kind="${kind}" ${!draft||readonly?'disabled':''}>批量移出草稿</button><button data-bulk-clear data-bulk-kind="${kind}">清除多选</button></div>`:''}${state.bulkFeedback?`<p class="small" role="status">${esc(state.bulkFeedback)}</p>`:''}${html}`;
};
function refreshModCatalog(kind){const el=$(kind==='profile'?'#profile-mod-catalog':'#mod-catalog');if(!el)return;const scroll=el.querySelector('.mod-list')?.scrollTop||0;el.innerHTML=richCatalog(kind);const list=el.querySelector('.mod-list');if(list)list.scrollTop=scroll;observeCovers();disable();}
document.addEventListener('change',e=>{const el=e.target;if(el.hasAttribute('data-mod-filter')){state.modFilters[el.dataset.modKind]=el.value;refreshModCatalog(el.dataset.modKind);}if(el.dataset.bulkMod){const set=state.bulkMods[el.dataset.bulkKind];el.checked?set.add(el.dataset.bulkMod):set.delete(el.dataset.bulkMod);refreshModCatalog(el.dataset.bulkKind);}});
document.addEventListener('click',e=>{const b=e.target.closest('[data-bulk-add],[data-bulk-remove],[data-bulk-clear]');if(!b||b.disabled)return;const kind=b.dataset.bulkKind,set=state.bulkMods[kind],key=kind==='profile'?'modProfileMods':'saveMods';if(b.hasAttribute('data-bulk-clear')){set.clear();refreshModCatalog(kind);return;}if(kind==='profile'&&!state.modProfile||kind==='save'&&(!state.saveDetail||state.saveDetail.editable===false))return;const allowed=(state.modCatalog?.mods||[]).filter(m=>set.has(m.id)&&!m.conflict&&m.componentManaged===false).map(m=>m.id),skipped=set.size-allowed.length;if(b.hasAttribute('data-bulk-add'))state[key]=[...state[key],...allowed.filter(id=>!state[key].includes(id))];else state[key]=state[key].filter(id=>!allowed.includes(id));state[kind==='profile'?'modProfileDirty':'saveDirty']=true;state.sortPreview=null;set.clear();state.bulkFeedback='当前草稿已更新，尚未启用。'+(skipped?' 跳过 '+skipped+' 个冲突或受管理条目。':'');render();});
function showPlayEditor(p,capture=false){state.playDraft=JSON.parse(JSON.stringify(p));state.playEditorEpoch=state.experienceEpoch||0;state.playDirty=false;const actual=state.actual,rows=actual.settings?.rows||[],d=document.createElement('dialog');d.id='play-editor';d.innerHTML=`<div class="row"><h2>${capture?'捕获当前实际配置':'编辑完整游玩配置'}</h2><button data-play-close>返回</button></div><label>配置名称<input id="play-profile-name" maxlength="100" value="${esc(p.name)}"></label>${capture?'':`<p>内存、光影和 Viewpoint 参数会影响全局游戏配置。模组仅修改普通 / 所选存档清单；服务器不套用普通模组。</p><label>目标类型<select id="play-edit-kind">${['normal','save','server'].map(k=>`<option value="${k}" ${p.targetKind===k?'selected':''}>${k==='normal'?'普通':k==='save'?'存档':'服务器'}</option>`).join('')}</select></label><label data-play-target-row>本机目标<select id="play-edit-target">${targetOptions(p.targetKind,p.targetId)}</select></label><label data-play-mods-row>普通模组 ID 与加载顺序（每行一个）<textarea id="play-edit-mods">${esc(p.mods.join('\n'))}</textarea></label><label>全局堆内存 GiB（留空保留当前）<input id="play-edit-heap" type="number" min="1" max="${esc(actual.memory?.maximumGiB||1)}" value="${esc(p.heapGiB??'')}"></label><label>全局光影<select id="play-edit-shader"><option value="">保留当前</option>${(actual.shaders?.packs||[]).map(x=>`<option value="${esc(x.id)}" ${x.id===p.shaderId?'selected':''}>${esc(x.name)}${x.valid?'':' · 不可用'}</option>`).join('')}${p.shaderId&&!(actual.shaders?.packs||[]).some(x=>x.id===p.shaderId)?`<option selected value="${esc(p.shaderId)}">本地未找到 · ${esc(p.shaderId)}</option>`:''}</select></label><details><summary>全局 Viewpoint 已有参数</summary>${rows.filter(r=>r.editable).map(r=>`<label class="play-setting"><input type="checkbox" data-play-setting-include="${esc(r.key)}" ${Object.hasOwn(p.settings,r.key)?'checked':''}>${esc(r.name)}<input data-play-setting="${esc(r.key)}" value="${esc(p.settings[r.key]??r.value)}"></label>`).join('')}${Object.keys(p.settings).filter(k=>!rows.some(r=>r.key===k)).map(k=>`<p class="status-warning">本机未找到该参数：${esc(k)}</p>`).join('')}</details>`}<p data-play-editor-feedback role="status"></p><div class="actions"><button class="primary" ${capture?'data-play-capture-confirm':'data-play-save'}>${capture?'保存捕获配置':'保存游玩配置'}</button>${!capture?'<button data-play-delete>删除这个配置</button>':''}</div>`;document.body.append(d);d.addEventListener('close',()=>d.remove());d.addEventListener('cancel',e=>{e.preventDefault();closeEditor();});d.showModal();if(!capture)updatePlayEditorTarget();}
function updatePlayEditorTarget(){const p=state.playDraft;if(!p)return;const server=p.targetKind==='server',normal=p.targetKind==='normal';$('[data-play-target-row]').hidden=normal;$('[data-play-mods-row]').hidden=server;$('#play-edit-mods').disabled=server;}
function showShare(){const d=document.createElement('dialog');d.id='play-share';d.innerHTML='<div class="row"><h2>便携配置分享</h2><button data-share-close>返回</button></div><p>只分享模组 ID、顺序和已核实设置；不含路径、账号、密码、授权或本机目标。导入只创建新配置，不应用、不下载。</p><button data-play-export>读取所选配置的便携 JSON</button><textarea id="play-share-json" readonly aria-label="便携分享 JSON"></textarea><button data-play-copy>复制便携 JSON</button><label>粘贴版本化 JSON<textarea id="play-import-json" maxlength="131072"></textarea></label><button data-play-import>核对并导入新配置</button><p data-share-feedback role="status"></p>';document.body.append(d);d.addEventListener('close',()=>d.remove());d.addEventListener('cancel',e=>{e.preventDefault();closeShare();});d.showModal();}
document.addEventListener('input',e=>{const el=e.target;if(el.closest('#play-editor')){state.playDirty=true;if(el.id==='play-profile-name')state.playDraft.name=el.value;if(el.id==='play-edit-mods')state.playDraft.mods=el.value.split(/\r?\n/).map(x=>x.trim()).filter(Boolean);if(el.id==='play-edit-heap')state.playDraft.heapGiB=el.value===''?null:Number(el.value);if(el.dataset.playSetting&&$(`[data-play-setting-include="${CSS.escape(el.dataset.playSetting)}"]`)?.checked)state.playDraft.settings[el.dataset.playSetting]=el.value;}if(el.id==='play-import-json')state.playImportDraft=el.value;});
document.addEventListener('change',async e=>{const el=e.target;if(el.id==='play-profile-select'){cancelLaunchAttempt();await selectPlay(el.value);return;}if(el.id==='launch-target-kind'||el.id==='launch-target-id'){cancelLaunchAttempt();if(el.id==='launch-target-kind'){state.launchKind=el.value;state.launchId='';}else state.launchId=el.value;state.manualTarget=true;state.playPreview=null;state.preflight=null;render();if(state.playProfiles?.selectedId||state.playSelecting)await selectPlay('');}if(el.id==='play-edit-kind'){state.playDraft.targetKind=el.value;state.playDraft.targetId='';if(el.value==='server')state.playDraft.mods=[];$('#play-edit-mods').value=state.playDraft.mods.join('\n');$('#play-edit-target').innerHTML=targetOptions(el.value,'');state.playDirty=true;updatePlayEditorTarget();}if(el.id==='play-edit-target'){state.playDraft.targetId=el.value;state.playDirty=true;}if(el.id==='play-edit-shader'){state.playDraft.shaderId=el.value;state.playDirty=true;}if(el.dataset.playSettingInclude){const key=el.dataset.playSettingInclude;if(el.checked)state.playDraft.settings[key]=$(`[data-play-setting="${CSS.escape(key)}"]`).value;else delete state.playDraft.settings[key];state.playDirty=true;}});
document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;const relevant=b.id==='launch'||b.hasAttribute('data-profile-launch')||[...b.attributes].some(a=>a.name.startsWith('data-play-')||a.name.startsWith('data-preflight-')||a.name.startsWith('data-diagnostics-')||a.name.startsWith('data-recent-target')||a.name==='data-share-close');if(!relevant)return;e.stopImmediatePropagation();try{
 if(b.id==='launch'){await launchExperience();return;}if(b.dataset.profileLaunch){if(b.dataset.profileLaunch==='save'&&state.saveDirty){notify('请先保存或重新读取未保存的存档编辑。');return;}await launchExperience(b.dataset.profileLaunch,b.dataset.id);return;}
 if(b.hasAttribute('data-preflight-auto-sort')){await autoSortAndLaunch();return;}if(b.dataset.preflightRepair){if(b.dataset.preflightRepair==='java'){await load('java');openJavaReview();}else route(b.dataset.preflightRepair);return;}
 if(b.dataset.recentTarget){cancelLaunchAttempt();state.launchKind=b.dataset.recentTarget;state.launchId=b.dataset.targetId;state.manualTarget=true;state.playPreview=null;state.preflight=null;render();if(state.playProfiles?.selectedId||state.playSelecting)await selectPlay('');return;}
 if(b.hasAttribute('data-play-refresh')){state.experienceErrors={};await freshProfiles();await freshActual();return;}if(b.hasAttribute('data-play-preview')){await previewPlay();return;}if(b.hasAttribute('data-play-apply')){await applyPlay(state.playPreview);render();return;}
 if(b.hasAttribute('data-play-capture')){showPlayEditor({name:'新游玩配置',targetKind:state.launchKind,targetId:state.launchId},true);return;}if(b.hasAttribute('data-play-edit')){const p=await previewPlay();if(p?.profile)showPlayEditor(p.profile);return;}
 if(b.hasAttribute('data-play-close')){await closeEditor();return;}
 if(b.hasAttribute('data-play-save')||b.hasAttribute('data-play-capture-confirm')){if((state.experienceEpoch||0)!==state.playEditorEpoch){$('[data-play-editor-feedback]').textContent='游戏目录已变化，请关闭草稿后重新读取所属配置。';return;}if(state.playSaving)return;state.playSaving=true;lockPlayEditor(true);const p=state.playDraft;try{const result=await request(b.hasAttribute('data-play-save')?'playProfileSave':'playProfileCapture',b.hasAttribute('data-play-save')?{stamp:state.playProfiles.stamp,id:p.id||'',name:p.name.trim(),targetKind:p.targetKind,targetId:p.targetKind==='normal'?'':p.targetId,mods:p.targetKind==='server'?[]:[...p.mods],heapGiB:p.heapGiB,shaderId:p.shaderId,settings:{...p.settings}}:{stamp:state.playProfiles.stamp,name:p.name.trim(),targetKind:p.targetKind,targetId:p.targetKind==='normal'?'':p.targetId});state.playDirty=false;closePlayDialog('#play-editor');await freshProfiles();launchMessage((result.warnings||[]).join('；')||'游玩配置已保存；尚未应用到游戏。');}catch(x){$('[data-play-editor-feedback]').textContent=x.message;}finally{state.playSaving=false;lockPlayEditor(false);}return;}
 if(b.hasAttribute('data-play-delete')){const id=state.playDraft?.id;if(!id||!await confirm('删除这个游玩配置？','只删除启动器记录；实际游戏配置保持原样。'))return;await request('playProfileDelete',{stamp:state.playProfiles.stamp,id});state.playDirty=false;closePlayDialog('#play-editor');await freshProfiles();return;}
 if(b.hasAttribute('data-play-recover')){await freshProfiles();const r=state.playProfiles?.pendingRecovery;if(!r?.canRecover)return;if(!await confirm('恢复原游戏配置？',r.locations.join('、')+'\n仅恢复仍由这次事务拥有的文件；外部变化不会被覆盖。'))return;const result=await request('playProfileRecover',{stamp:r.stamp,confirmed:true});state.playProfiles.pendingRecovery=result.pendingRecovery;actualWritten('playProfileRecover',result);launchMessage(result.message);await refresh();await freshProfiles();render();return;}
 if(b.hasAttribute('data-play-share')){showShare();return;}if(b.hasAttribute('data-share-close')){await closeShare();return;}
 if(b.hasAttribute('data-play-export')){if(!state.playProfiles?.selectedId){$('[data-share-feedback]').textContent='请先选择要分享的游玩配置。';return;}const value=await request('playProfileExport',{stamp:state.playProfiles.stamp,id:state.playProfiles.selectedId});$('#play-share-json').value=value.json;$('[data-share-feedback]').textContent=value.warnings.join('；');return;}if(b.hasAttribute('data-play-copy')){await copyText($('#play-share-json').value,$('#play-share-json'),$('[data-share-feedback]'));return;}
 if(b.hasAttribute('data-play-import')){const json=$('#play-import-json').value;if(new TextEncoder().encode(json).length>131072){$('[data-share-feedback]').textContent='JSON 超过 128 KiB 限制。';return;}if(!await confirm('导入新的游玩配置？','只创建新配置，清除本机目标关联，不下载或应用。'))return;try{const result=await request('playProfileImport',{stamp:state.playProfiles.stamp,json,confirmed:true});state.playImportDraft='';await freshProfiles();$('[data-share-feedback]').textContent=result.warnings.join('；')||'已导入新配置；尚未应用。';}catch(x){$('[data-share-feedback]').textContent=x.message;}return;}
 if(b.hasAttribute('data-diagnostics-read')){const epoch=state.experienceEpoch||0;state.diagnosticsReading=true;state.diagnosticsError='';render();try{const result=await request('gameDiagnostics',{});if(epoch!==(state.experienceEpoch||0)){state.diagnosticsError='游戏目录已变化，请重新读取诊断。';}else state.diagnostics=result;}catch(x){state.diagnosticsError=x.message;}finally{state.diagnosticsReading=false;render();}return;}if(b.hasAttribute('data-diagnostics-copy')){await copyText(state.diagnostics.reportText,$('#diagnostics-report'),$('[data-copy-feedback]'));return;}
 }catch(x){launchMessage(x.message);if($('[data-share-feedback]'))$('[data-share-feedback]').textContent=x.message;}},true);
let demoProfileStamp=1,demoSelected='',demoHeap=6;
const demoProfile={id:'d'.repeat(32),name:'预览 · 轻量探索',targetKind:'normal',targetId:'',mods:['preview-mod'],heapGiB:8,shaderId:'original',settings:{}};
let demoProfiles=[demoProfile];
const demoRecovery={pending:false,state:'none',stamp:'',canRecover:false,locations:[],warnings:[]};
function experienceFixture(action,p={}){if(!preview)return null;const summary=x=>({id:x.id,name:x.name,targetKind:x.targetKind,targetId:x.targetId,modCount:x.mods.length,updated:'2026-10-15T00:00:00Z'});switch(action){
 case'playProfiles':return{stamp:String(demoProfileStamp),selectedId:demoSelected,profiles:demoProfiles.map(summary),selectedProfile:demoProfiles.find(x=>x.id===demoSelected)||null,pendingRecovery:demoRecovery,warnings:['仅为合成预览，不读取真实游戏或用户配置。']};
 case'playProfileSelect':demoSelected=p.id;demoProfileStamp++;return{stamp:String(demoProfileStamp),selectedId:p.id,profile:demoProfiles.find(x=>x.id===p.id)||null,message:'预览选择；没有真实写入'};
 case'playProfilePreview':{const profile=demoProfiles.find(x=>x.id===p.id);return{stamp:String(demoProfileStamp),previewStamp:'demo-preview-'+demoProfileStamp,profile,canApply:true,changes:profile.heapGiB&&profile.heapGiB!==demoHeap?[{kind:'heap',key:'heapGiB',before:demoHeap,after:profile.heapGiB}]:[],warnings:['合成界面预览，不会实际应用。'],errors:[],pendingRecovery:demoRecovery};}
 case'playProfileApply':demoHeap=demoProfiles.find(x=>x.id===p.id)?.heapGiB||demoHeap;return{status:'completed',message:'预览示例：实际文件未改变。',profileId:p.id,recoveryPending:false,pendingRecovery:demoRecovery};
 case'playProfileCapture':case'playProfileSave':{const profile=action==='playProfileCapture'?{...demoProfile,name:p.name,targetKind:p.targetKind,targetId:p.targetId,heapGiB:demoHeap}:{...p};delete profile.stamp;profile.id=profile.id||crypto.randomUUID().replaceAll('-','');demoProfiles=demoProfiles.filter(x=>x.id!==profile.id).concat(profile);demoProfileStamp++;return{stamp:String(demoProfileStamp),profile,summary:summary(profile),warnings:['合成预览；没有真实持久化。']};}
 case'playProfileDelete':demoProfiles=demoProfiles.filter(x=>x.id!==p.id);if(demoSelected===p.id)demoSelected='';demoProfileStamp++;return{stamp:String(demoProfileStamp),selectedId:demoSelected,message:'预览删除；真实文件未改变'};
 case'playProfileExport':{const x=demoProfiles.find(x=>x.id===p.id);return{json:JSON.stringify({format:'OrangePlayProfile',version:1,name:x.name,mods:x.mods,heapGiB:x.heapGiB,shaderId:x.shaderId,settings:x.settings},null,2),warnings:['仅为界面示例']};}
 case'playProfileImport':{const x=JSON.parse(p.json);if(x.format!=='OrangePlayProfile'||x.version!==1||Object.keys(x).some(k=>!['format','version','name','mods','heapGiB','shaderId','settings'].includes(k)))throw new Error('预览：不支持的分享格式。');const profile={id:crypto.randomUUID().replaceAll('-',''),name:x.name,targetKind:'normal',targetId:'',mods:x.mods,heapGiB:x.heapGiB,shaderId:x.shaderId,settings:x.settings};demoProfiles.push(profile);demoProfileStamp++;return{stamp:String(demoProfileStamp),profile,summary:summary(profile),warnings:['预览导入；依赖未实际核实，不会应用或下载。']};}
 case'startupBegin':return{startupId:'0123456789abcdef0123456789abcdef'};
 case'startupCancel':case'startupYield':case'startupResume':return{};
 case'launchPreflight':return{canLaunch:true,checkedUtc:new Date().toISOString(),items:['activity','recovery','workshop','launch-config','launch-guard','mods','java-approvals','memory'].map((id,i)=>({id,status:i===6?'warning':'ok',title:['任务状态','配置恢复','工坊状态','游戏启动配置','启动参数保护','模组依赖','Java 授权','内存预算'][i],message:i===6?'预览示例：Java 文件等待授权。':'合成预览：没有实际检查游戏文件。',route:['home','maintenance','maintenance','components','maintenance','mods','java','memory'][i]})),message:'合成预览，不会启动游戏'};
 case'memoryGet':return{currentGiB:demoHeap,totalGiB:32,availableGiB:22,maximumGiB:24,recommendedGiB:8,stamp:'preview-stamp'};
 case'modCatalog':{const x=fixture(action,p);return{...x,mods:x.mods.map(m=>({...m,componentManaged:false}))};}
 case'gameDiagnostics':return{checkedUtc:new Date().toISOString(),findings:[{id:'java-heap-exhausted',severity:'error',confidence:'confirmed',title:'Java 堆内存不足 · 合成示例',message:'示例日志记录了 Java 堆内存不足。',evidence:['[合成示例] java.lang.OutOfMemoryError: Java heap space'],source:'console',historical:true,nextStep:'核对实际堆内存、模组负载和总物理内存预算。'}],warnings:[{id:'source-age-unknown',source:'console',message:'合成示例 · 时间未知。'}],sources:[{id:'console',kind:'game-log',label:'游戏控制台日志 · 合成示例',modifiedUtc:'',newestEvidenceUtc:'',freshness:'unknown',status:'read',encoding:'utf-8',bytesRead:72,historical:true,truncated:false}],reportText:'[合成预览，不读取真实日志]\njava.lang.OutOfMemoryError: Java heap space'};
 default:return null;
}}
// A non-modal manifest panel keeps navigation and unrelated drafts available.
let serverWorkshopTicket=0;
const workshopRenderBase=render;
render=function(){workshopRenderBase();for(const card of document.querySelectorAll('[data-server-card]')){const id=card.dataset.serverCard,actions=card.querySelector('.actions');if(actions&&!actions.querySelector('[data-server-workshop]'))actions.insertAdjacentHTML('beforeend',`<button data-server-workshop="${esc(id)}">模组清单</button>`);}if(state.route==='home'&&state.launchKind==='server'&&state.launchId){const target=$('.launch-target');if(target)target.insertAdjacentHTML('beforeend',`<button data-server-workshop="${esc(state.launchId)}">模组清单</button>`);}};
let serverWorkshopStart=null;
function acceptServerWorkshop(value,allowNew=false){
 if(!value||value.protocol!==1||!value.jobId||!Number.isInteger(value.revision))return;
 // A later job can finish before its start acknowledgement. Retain only the
 // newest matching event, then bind it to the job ID returned by that start.
 const starting=serverWorkshopStart;
 if(starting&&value.serverId===starting.manifest.id&&value.endpoint===starting.manifest.endpoint&&value.manifestStamp===starting.manifest.stamp){
  const buffered=starting.events.get(value.jobId);
  if((buffered||starting.events.size<4)&&(!buffered||value.revision>buffered.revision))starting.events.set(value.jobId,value);
 }
 const old=state.serverWorkshopJob;
 if(old&&old.jobId===value.jobId&&value.revision<=old.revision)return;
 if(old&&old.jobId!==value.jobId&&!allowNew)return;
 state.serverWorkshopJob=value;
 paintServerWorkshopProgress();
}
async function prepareServerWorkshop(manifest){
 if(serverWorkshopStart)return;
 const starting={manifest,events:new Map()};
 serverWorkshopStart=starting;
 try{
  const response=await request('serverWorkshopPrepare',{id:manifest.id,stamp:manifest.stamp,confirmed:true});
  const buffered=starting.events.get(response.jobId);
  acceptServerWorkshop(buffered&&buffered.revision>response.revision?buffered:response,true);
 }finally{
  if(serverWorkshopStart===starting)serverWorkshopStart=null;
 }
}
function paintServerWorkshopProgress(){const el=$('[data-server-workshop-job]'),m=state.serverWorkshopManifest,j=state.serverWorkshopJob;if(!el)return;if(!j||!m||j.serverId!==m.id||j.endpoint!==m.endpoint){el.innerHTML='';return;}const names={running:'正在准备',completed:'已完成',partial:'部分完成',failed:'失败',cancelled:'已取消',interrupted:'已中断'};el.innerHTML=`<section data-server-workshop-progress role="status"><h3>${esc(names[j.state]||'状态未知')} · ${esc(j.completedItems)} / ${esc(j.totalItems)}</h3>${j.manifestStamp!==m.stamp?'<p class="small">此任务使用此前确认的清单。</p>':''}<p>当前项目：${esc(j.currentItem||'—')}</p><progress max="100" ${j.progress>=0?`value="${Math.min(100,j.progress)}"`:''}></progress><p>${esc(j.message)}</p>${j.state==='running'?'<button data-server-workshop-cancel>取消准备</button>':''}${(j.items||[]).filter(i=>i.state==='failed').map(i=>`<p class="status-warning">${esc(i.id)} · ${esc(i.message)}</p>`).join('')}</section>`;}
function paintServerWorkshop(){const el=$('[data-server-workshop-content]'),m=state.serverWorkshopManifest;if(!el||!m)return;el.innerHTML=`<p class="small">${esc(m.endpoint)} · ${m.source==='connection'?'连接记录':m.source==='imported'?'导入清单':'尚无完整清单'}${m.capturedUtc?' · '+esc(localDate(m.capturedUtc)):''}</p><p>${m.items?.length?`${m.items.length} 个工坊项目 · 本地已有 ${m.items.filter(i=>i.localPresent).length} 个`:'尚无完整清单，可导入工坊 ID。'}</p><ul class="server-workshop-items">${(m.items||[]).map(i=>`<li data-server-workshop-item="${esc(i.id)}"><span>${esc(i.title||i.id)}${i.title?' · '+esc(i.id):''}</span><small>${i.localPresent?'本地文件存在':'未发现本地文件'}</small></li>`).join('')}</ul><button data-server-workshop-prepare ${m.items?.length?'':'disabled'}>订阅并下载</button>`;paintServerWorkshopProgress();}
async function openServerWorkshop(id){const ticket=++serverWorkshopTicket;$('#server-workshop-panel')?.remove();state.serverWorkshopManifest=null;const panel=document.createElement('aside');panel.id='server-workshop-panel';panel.setAttribute('aria-label','服务器模组清单');panel.innerHTML='<div class="row"><h2>模组清单</h2><button data-server-workshop-close aria-label="关闭模组清单">关闭</button></div><div data-server-workshop-content>正在读取…</div><details><summary>导入清单</summary><label for="server-workshop-import">WorkshopItems 或工坊 ID 列表</label><textarea id="server-workshop-import" maxlength="65536" placeholder="WorkshopItems=123;456"></textarea><button data-server-workshop-import>导入清单</button></details><p data-server-workshop-error role="status"></p><div data-server-workshop-job></div>';document.body.append(panel);try{const m=await request('serverWorkshopGet',{id});if(ticket!==serverWorkshopTicket)return;state.serverWorkshopManifest=m;paintServerWorkshop();const priorJob=state.serverWorkshopJob,status=await request('serverWorkshopStatus',{});if(ticket===serverWorkshopTicket&&state.serverWorkshopJob===priorJob)acceptServerWorkshop(status,true);}catch(e){if(ticket===serverWorkshopTicket)$('[data-server-workshop-error]').textContent=e.message;}}
let serverWorkshopEditRevision=0;
document.addEventListener('input',e=>{if(e.target.id==='server-workshop-import')serverWorkshopEditRevision++;});
async function importServerWorkshop(manifest,ticket,button){
 const input=$('#server-workshop-import'),revision=serverWorkshopEditRevision;
 button.disabled=true;
 try{
  const result=await request('serverWorkshopImport',{id:manifest.id,stamp:manifest.stamp,text:input.value,confirmed:true});
  if(ticket!==serverWorkshopTicket)return;
  state.serverWorkshopManifest=result;
  if(revision===serverWorkshopEditRevision)input.value='';
  paintServerWorkshop();
 }finally{button.disabled=false;}
}
document.addEventListener('click',async e=>{
 const b=e.target.closest('button');
 if(!b||b.disabled)return;
 if(b.dataset.serverWorkshop){openServerWorkshop(b.dataset.serverWorkshop);return;}
 if(b.hasAttribute('data-server-workshop-close')){serverWorkshopTicket++;$('#server-workshop-panel')?.remove();return;}
 if(!b.matches('[data-server-workshop-import],[data-server-workshop-prepare],[data-server-workshop-cancel]'))return;
 const ticket=serverWorkshopTicket,m=state.serverWorkshopManifest;
 if(!m)return;
 try{
  if(b.hasAttribute('data-server-workshop-import')){
   await importServerWorkshop(m,ticket,b);
  }else if(b.hasAttribute('data-server-workshop-prepare')){
   if(!await confirm('订阅并下载这份清单？',`${m.items.length} 个工坊项目。由 Steam 核实订阅与安装；完成后不会自动启动游戏。`))return;
   if(ticket!==serverWorkshopTicket||state.serverWorkshopManifest!==m)return;
   await prepareServerWorkshop(m);
  }else{
   acceptServerWorkshop(await request('serverWorkshopCancel',{jobId:state.serverWorkshopJob.jobId}));
  }
 }catch(x){if(ticket===serverWorkshopTicket)$('[data-server-workshop-error]').textContent=x.message;}
});
// Log analysis has its own read-only job lane; it never takes the game mutation lock.
Object.assign(state,{logAnalysis:null,logAnalysisPending:false,logAnalysisError:'',logAnalysisFeedback:'',logAnalysisRevision:0,logAnalysisPolling:false});
function logAnalysisEntry(){return '<section class="card section log-analysis-entry"><div><h2>游戏日志分析</h2></div><button data-page="diagnostics" data-log-analysis-entry>打开日志分析 →</button></section>';}
const logOutcomes={'entered-with-issues':'已进入游戏，仍有运行问题',entered:'日志显示已进入游戏',failed:'日志显示本次尝试失败',disconnected:'日志显示连接已断开',unknown:'无法确认是否进入游戏'};
const logStages={runtime:'游戏运行中','world-loading':'加载世界',connection:'连接服务器',disconnection:'连接断开',playing:'游戏运行中',ingame:'游戏运行中',loading:'加载世界',connecting:'连接服务器',initializing:'初始化',startup:'启动阶段',workshop:'工坊准备',unknown:'阶段未确认',menu:'主菜单',disconnected:'连接断开'};
function logSourceName(id){const source=state.logAnalysis?.report?.sources?.find(s=>s.id===id);return source?`${source.name||'日志'} (${source.id})`:id||'来源未提供';}
function logStage(value){return logStages[value]||value||'阶段未确认';}
function logNumber(value){return Math.max(0,Number(value)||0).toLocaleString('zh-CN');}
function logBytes(value){const n=Math.max(0,Number(value)||0);return n>=1048576?(n/1048576).toFixed(1)+' MiB':n>=1024?(n/1024).toFixed(1)+' KiB':logNumber(n)+' B';}
function logOrigin(report){return report.origin==='local'?'本机最近日志':report.origin==='preview'?'合成示例数据':'导入的日志 · 仅代表所选资料';}
function logAttention(issue){
 if(['primary','review','background'].includes(issue.attention))return issue.attention;
 if(['asset-bone','asset-diagnostic'].includes(issue.type))return 'review';
 if(['connection-failure','connection-lost'].includes(issue.type))return 'primary';
 if(issue.severity==='error'&&(['blocking','launch','connection'].includes(issue.impact)||issue.impact==='runtime'&&issue.type!=='unknown'&&(issue.frames||[]).length))return 'primary';
 return 'review';
}
function logAttemptName(id){const index=(state.logAnalysis?.report?.attempts||[]).findIndex(a=>a.id===id);return index>=0?'第 '+(index+1)+' 次尝试':'关联尝试待核对';}
function logIssue(issue,index){
 const frames=Array.isArray(issue.frames)?issue.frames:[],evidence=Array.isArray(issue.evidence)?issue.evidence:[],mods=Array.isArray(issue.mods)?issue.mods:[];
 const severity=logAttention(issue)==='primary'?'status-negative':'status-neutral';
 const impact={runtime:'可能影响游戏内功能',launch:'可能影响启动',blocking:'可能阻止启动或连接',connection:'可能影响连接',unknown:'影响尚待核实'}[issue.impact]||'需结合实际表现核实';
 return `<article class="card log-issue" data-log-issue><div class="row"><h3>${esc(issue.title||evidence[0]?.text||'含义待核对的日志记录')}</h3><span class="badge ${severity}">${issue.severity==='error'?'错误':issue.severity==='warning'?'警告':'待核实'} · ${logNumber(issue.count||1)} 次</span></div><p class="small log-issue-context">${esc(logStage(issue.phase))} · ${esc(impact)}${issue.attemptId?' · '+esc(logAttemptName(issue.attemptId)):''}</p>${mods.length||frames.length?`<p class="log-location"><strong>关联 MOD：${mods.length?mods.map(esc).join("、"):"未确认"}</strong>${frames[0]?` · <code>${esc(frames[0].file||"文件未知")}${frames[0].line?":"+logNumber(frames[0].line):""}</code>`:""}</p>`:""}<div class="log-explanation"><p><strong>这意味着什么</strong>${esc(issue.meaning||'含义待核对。')}</p><p><strong>建议下一步</strong>${esc(issue.advice||'暂无具体建议。')}</p></div>${mods.length?`<div class="log-mods"><span class="small">堆栈关联模组</span>${mods.map(m=>`<span class="badge">${esc(m)}</span>`).join('')}</div>`:''}<details id="log-evidence-${index}" data-log-evidence><summary>查看原始证据与调用位置（脱敏）</summary><p class="small">问题编号 ${esc(issue.id||"未提供")} · 尝试编号 ${esc(issue.attemptId||"未提供")}</p>${evidence.length?evidence.map(e=>`<div class="log-evidence"><p class="small">${esc(logSourceName(e.source))} · 第 ${logNumber(e.line)} 行</p><pre>${esc(e.text)}</pre></div>`).join(''):'<p class="small">未保留原始行。</p>'}${frames.length?`<h4>调用位置</h4><ol class="log-frames">${frames.map(f=>`<li>${f.mod?`<strong>${esc(f.mod)}</strong> · `:''}<code>${esc(f.file||'文件未知')}${f.line?':'+logNumber(f.line):''}</code>${f.function?' · '+esc(f.function):''}</li>`).join('')}</ol>`:''}</details></article>`;
}
function logReport(report){
 const issues=Array.isArray(report.issues)?report.issues:[],sources=Array.isArray(report.sources)?report.sources:[],attempts=Array.isArray(report.attempts)?report.attempts:[];
 const indexed=issues.map((issue,index)=>({issue,index})),primary=indexed.filter(x=>logAttention(x.issue)==='primary').sort((a,b)=>(a.issue.impact==='blocking'?0:1)-(b.issue.impact==='blocking'?0:1)),review=indexed.filter(x=>logAttention(x.issue)==='review'),background=indexed.filter(x=>logAttention(x.issue)==='background');
 const mods=[...new Set(primary.flatMap(x=>x.issue.mods||[]))];
 const consoleSource=sources.find(x=>/console\.txt/i.test(x.name||''));
 const folded=(kind,label,members)=>members.length?`<details class="card log-folded" data-log-attention="${kind}"><summary>${label} · ${logNumber(members.length)} 类 / ${logNumber(members.reduce((n,x)=>n+(Number(x.issue.count)||1),0))} 次</summary><div class="log-folded-list">${members.map(x=>logIssue(x.issue,x.index)).join('')}</div></details>`:'';
 const running=state.logAnalysis?.state==='running'||state.logAnalysisPending;
 return `<section class="log-report" data-log-report>${running?'<p class="small log-previous">上一份报告</p>':''}<div class="card log-outcome" data-log-analysis-summary><div class="row"><span class="badge" data-log-analysis-origin>${esc(logOrigin(report))}</span><span class="small">分析时间 ${esc(localDate(report.checkedUtc)||'未提供')}</span></div><h2>${esc(logOutcomes[report.state]||logOutcomes.unknown)}</h2>${primary.length?`<p class="log-primary-titles"><strong>优先核对：${primary.slice(0,3).map(x=>esc(x.issue.title||"证据中的功能故障")+(x.issue.frames?.[0]?" · "+esc(x.issue.frames[0].file||"脚本未知")+(x.issue.frames[0].line?":"+logNumber(x.issue.frames[0].line):""):"")).join("；")}</strong></p>`:""}<p class="log-primary-summary">核心故障 ${logNumber(primary.length)} 类 · 待核对 ${logNumber(review.length)} 类 · 背景 ${logNumber(background.length)} 类${mods.length?' · 关联 MOD：'+mods.map(esc).join('、'):''}</p>${consoleSource?`<p class="log-console-source"><strong>主要日志：${esc(consoleSource.name)}</strong> · 修改时间 ${esc(localDate(consoleSource.modifiedUtc)||'未知')}</p>`:''}<p class="small">${esc(logStage(report.stage))}</p><div class="log-metrics"><div><strong>${logNumber(report.fileCount)}</strong><span>读取文件</span></div><div><strong>${logNumber(report.errorCount)}</strong><span>原始 ERROR 行</span></div><div><strong>${logNumber(report.warningCount)}</strong><span>原始 WARN 行</span></div><div><strong>${esc(logBytes(report.bytesRead))}</strong><span>已读取</span></div></div></div>${report.incomplete?'<p class="notice log-gap">分析信息不完整</p>':''}${(report.warnings||[]).map(w=>`<p class="status-warning log-warning">${esc(w)}</p>`).join('')}<div class="log-issue-groups">${primary.length?`<section class="log-issue-group"><h2>优先处理的核心故障 <span class="small">${logNumber(primary.length)} 类</span></h2>${primary.slice(0,5).map(x=>logIssue(x.issue,x.index)).join('')}${primary.length>5?`<details class="card log-folded" data-log-more-primary><summary>其余核心故障 · ${logNumber(primary.length-5)} 类</summary>${primary.slice(5).map(x=>logIssue(x.issue,x.index)).join('')}</details>`:''}</section>`:'<div class="card log-empty" data-log-analysis-empty><h3>当前资料未发现明确核心故障</h3></div>'}${folded('review','需要核对的记录',review)}${folded('background','资源与背景记录',background)}</div><div class="card log-report-details"><details id="log-attempts" data-log-attempts><summary>启动与连接尝试 · ${attempts.length} 次</summary><ol class="log-attempts">${attempts.map((a,attemptIndex)=>`<li><div class="row"><strong>第 ${attemptIndex+1} 次尝试 · ${esc(logOutcomes[a.state]||logOutcomes.unknown)}</strong><span class="small">${esc(logStage(a.stage))}</span></div><p>${esc(a.summary)}</p><p class="small">编号 ${esc(a.id)} · ${esc(a.sessionKey)} · 第 ${logNumber(a.firstLine)}–${logNumber(a.lastLine)} 行</p></li>`).join('')||'<li>没有足够证据区分启动或连接尝试。</li>'}</ol></details><details id="log-sources" data-log-sources><summary>日志来源与读取范围 · ${sources.length} 个来源</summary>${sources.map(s=>`<article class="log-source"><strong>${esc(s.name)}</strong><p class="small">${esc(({read:'已读取',missing:'文件不存在',unreadable:'无法读取',rejected:'拒绝读取',skipped:'未读取'})[s.status]||s.status||'状态未知')} · ${esc(s.encoding||'编码未确认')} · ${esc(logBytes(s.bytesRead))}${s.truncated?' · 已截取':''}</p><p class="small">修改时间 ${esc(localDate(s.modifiedUtc)||'未知')} · ${esc(s.kind||'日志')}</p></article>`).join('')||'<p>未提供日志来源。</p>'}</details><details id="log-report-text"><summary>查看完整脱敏文本报告</summary><textarea id="log-analysis-report" readonly aria-label="日志分析脱敏报告">${esc(report.reportText||'')}</textarea></details><div class="actions section"><button data-log-analysis-copy ${!report.reportText?'disabled':''}>复制脱敏报告</button><button data-log-analysis-export ${running||!report.reportText?'disabled':''}>导出脱敏报告…</button><span class="small" data-log-analysis-feedback role="status">${esc(state.logAnalysisFeedback)}</span></div></div></section>`;
}
function diagnostics(){
 const job=state.logAnalysis,running=job?.state==='running',busy=running||state.logAnalysisPending,progress=Number(job?.progress);
 const status=state.logAnalysisPending?'正在打开并准备读取…':({running:'正在分析日志…',cancelled:'分析已取消，可重新选择日志。',failed:'日志分析未完成，请重试或换一份日志。',completed:'分析完成',idle:''}[job?.state]||'');
 return heading()+`<div class="log-analysis-page">${preview?'<p class="notice log-preview">界面预览</p>':''}<section class="card log-intake${job?.report?' has-report':''}"><div><h2>日志文件</h2><p>本机主要日志：console.txt · 支持 TXT / LOG / ZIP</p></div><div class="actions"><button class="primary" data-log-analysis-local ${busy?'disabled':''}>分析本机最近日志</button><button data-log-analysis-import ${busy?'disabled':''}>导入文件 / ZIP…</button><button data-log-analysis-folder ${busy?'disabled':''}>选择日志文件夹…</button></div></section><div data-log-analysis-status role="status" aria-live="polite" class="log-job ${busy?'is-running':''}"><span>${esc(status)}</span>${running?`<div class="log-progress"><progress data-log-analysis-progress aria-label="日志分析进度" max="100" ${Number.isFinite(progress)&&progress>=0?`value="${Math.min(100,progress)}"`:''}></progress><button data-log-analysis-cancel ${state.logAnalysisPending?'disabled':''}>取消分析</button></div>`:''}</div>${state.logAnalysisError?`<p class="notice" data-log-analysis-error role="alert">${esc(state.logAnalysisError)} <button data-log-analysis-retry>重新读取状态</button></p>`:''}${job?.report?logReport(job.report):`<section class="log-welcome"><div class="log-welcome-mark" aria-hidden="true">≡</div><h2>${busy?'正在分析日志':'暂无分析结果'}</h2></section>`}</div>`;
}
function paintLogAnalysis(){if(state.route==='diagnostics')render();}
function resetLogAnalysis(){state.logAnalysisRevision++;state.logAnalysis=null;state.logAnalysisPending=false;state.logAnalysisError='';state.logAnalysisFeedback='';}
function logAnalysisFixture(action){
 if(!preview||!action.startsWith('logAnalysis'))return null;
 if(action==='logAnalysisExport')return {cancelled:true};
 if(action==='logAnalysisCancel')return {jobId:'preview-only',state:'cancelled',message:'',progress:0,report:null};
 if(action==='logAnalysisStatus'&&!state.logAnalysisDemo&&new URLSearchParams(location.search).get('analysisSample')!=='1')return {jobId:'',state:'idle',message:'',progress:0,report:null};
 state.logAnalysisDemo=true;
 return {jobId:'preview-only',state:'completed',message:'合成界面示例',progress:100,report:{origin:'preview',summary:'合成示例：第二次连接已进入世界，随后记录了脚本运行错误。',state:'entered-with-issues',stage:'playing',incomplete:false,fileCount:3,errorCount:12,warningCount:2,bytesRead:428032,checkedUtc:'2026-10-25T08:00:00Z',warnings:[],reportText:'OZL 日志分析 · 合成示例数据\n日志显示已进入游戏，仍有运行问题。\n示例模组的角色更新脚本读取了空对象。\n这些内容不是实际玩家的日志。',issues:[{id:'sample-lua',type:'lua-null',severity:'error',impact:'runtime',title:'模组脚本访问了空对象',meaning:'角色更新脚本读取了尚未准备好的数据，相关功能可能反复中断。日志已显示进入世界，因此不能把这条错误直接解释为进服失败。',advice:'先核对该模组与当前游戏版本是否匹配；如仍重现，可在备份存档后逐个排查相关模组组合。',phase:'playing',attemptId:'2',count:12,mods:['示例 · Survivor Toolkit'],frames:[{mod:'示例 · Survivor Toolkit',file:'SurvivorStatus.lua',function:'updateStatus',line:84}],evidence:[{source:'console.txt（合成示例）',line:183,text:'ERROR: attempted index: health of non-table: null\nLua((MOD:示例 · Survivor Toolkit)).updateStatus(SurvivorStatus.lua:84)'}]},{id:'sample-warning',type:'warning',severity:'warning',impact:'unknown',title:'发现未能加载的资源记录',meaning:'日志报告某个资源未找到。它可能只影响显示，也可能属于可选资源，需要结合实际画面判断。',advice:'如游戏内出现缺图，核对相关模组的文件完整性和所需依赖。',phase:'loading',attemptId:'2',count:2,mods:[],frames:[],evidence:[{source:'DebugLog.txt（合成示例）',line:61,text:'WARN: optional texture not found: example_status_icon'}]}],attempts:[{id:'1',sessionKey:'示例会话',state:'failed',stage:'connecting',summary:'第一次连接没有完成。',firstLine:1,lastLine:92},{id:'2',sessionKey:'示例会话',state:'entered-with-issues',stage:'playing',summary:'重试后进入世界，并记录运行错误。',firstLine:93,lastLine:226}],sources:[{id:'console',name:'console.txt（合成示例）',kind:'game-log',encoding:'UTF-8',status:'read',modifiedUtc:'2026-10-25T07:58:00Z',bytesRead:204800,truncated:false},{id:'debug',name:'DebugLog.txt（合成示例）',kind:'game-log',encoding:'UTF-8',status:'read',modifiedUtc:'2026-10-25T07:58:00Z',bytesRead:221184,truncated:false},{id:'connections',name:'connections.txt（合成示例）',kind:'connection-log',encoding:'UTF-8',status:'read',modifiedUtc:'2026-10-25T07:58:00Z',bytesRead:2048,truncated:false}]}};
}
function acceptLogAnalysis(value){if(!value||!['idle','running','completed','cancelled','failed'].includes(value.state))throw new Error('Invalid analysis status');state.logAnalysis=value;state.logAnalysisError=value.state==='failed'?'未能完成日志分析。请重新选择文件，或检查文件是否可读取。':'';}
async function loadLogAnalysisStatus(){
 if(state.logAnalysisPolling||state.logAnalysisPending)return;
 const revision=state.logAnalysisRevision;state.logAnalysisPolling=true;
 try{const value=await request('logAnalysisStatus',{});if(revision!==state.logAnalysisRevision)return;const changed=!!state.logAnalysisError||JSON.stringify(value)!==JSON.stringify(state.logAnalysis);acceptLogAnalysis(value);if(changed)paintLogAnalysis();}
 catch{if(revision===state.logAnalysisRevision){state.logAnalysisError='暂时无法读取分析状态。请重试；正在运行的分析可能仍在后台继续。';paintLogAnalysis();}}
 finally{state.logAnalysisPolling=false;}
}
async function startLogAnalysis(action){
 if(state.logAnalysisPending||state.logAnalysis?.state==='running')return;
 const revision=++state.logAnalysisRevision;state.logAnalysisPending=true;state.logAnalysisError='';state.logAnalysisFeedback='';paintLogAnalysis();
 try{const result=await request(action,{});if(revision===state.logAnalysisRevision&&!result.cancelled)acceptLogAnalysis(result);}
 catch{if(revision===state.logAnalysisRevision)state.logAnalysisError='无法开始分析。请确认文件可读取后重试，或重新选择日志文件。';}
 finally{if(revision===state.logAnalysisRevision){state.logAnalysisPending=false;paintLogAnalysis();}}
}
document.addEventListener('click',async e=>{
 const b=e.target.closest('button');if(!b||b.disabled)return;
 for(const [attr,action]of [['local','logAnalysisLocal'],['import','logAnalysisImport'],['folder','logAnalysisFolder']])if(b.hasAttribute('data-log-analysis-'+attr)){await startLogAnalysis(action);return;}
 if(b.hasAttribute('data-log-analysis-retry')){state.logAnalysisError='';await loadLogAnalysisStatus();paintLogAnalysis();return;}
 if(b.hasAttribute('data-log-analysis-cancel')){state.logAnalysisRevision++;state.logAnalysisPending=true;paintLogAnalysis();try{acceptLogAnalysis(await request('logAnalysisCancel',{}));}catch{state.logAnalysisError='取消请求未完成，请重新读取状态后重试。';}finally{state.logAnalysisPending=false;paintLogAnalysis();}return;}
 if(b.hasAttribute('data-log-analysis-copy')){const field=$('#log-analysis-report'),feedback=$('[data-log-analysis-feedback]');if(!field||!feedback)return;await copyText(state.logAnalysis.report.reportText,field,feedback);state.logAnalysisFeedback=feedback.textContent;if(feedback.textContent.includes('手动复制')){$('#log-report-text').open=true;field.focus();field.select();}return;}
 if(b.hasAttribute('data-log-analysis-export')){b.disabled=true;try{const result=await request('logAnalysisExport',{});state.logAnalysisFeedback=result.cancelled?'已取消导出。':'脱敏报告已导出。';}catch{state.logAnalysisFeedback='导出未完成，请重试或复制脱敏报告。';}finally{paintLogAnalysis();}return;}
});
setInterval(()=>{if(state.logAnalysis?.state==='running')loadLogAnalysisStatus();},1000);
function feedback(){return window.ozlFeedback?.render()||'<p>正在读取反馈页面…</p>';}
function loadingOptionsPanel(){const o=state.loadingOptions,pending=state.loadingOptionsPending,blocked=state.busy||state.coreApplying||state.status?.running;return `<section class="card section" data-loading-options><div class="row"><div><h2>游戏加载画面</h2><p>${pending?'正在处理…':o?(o.enabled?'已开启':'已关闭'):'正在读取…'}</p></div><div class="actions"><button data-loading-toggle ${!o||pending||blocked?'disabled':''}>${o?.enabled?'关闭加载画面':'开启加载画面'}</button><button class="secondary" data-loading-remove ${!o?.removable||pending||blocked?'disabled':''}>卸载</button></div></div>${state.loadingOptionsMessage?`<p role="status">${esc(state.loadingOptionsMessage)}</p>`:''}${o?.warning?`<p class="error-text">${esc(o.warning)}</p>`:''}${state.loadingOptionsError?`<p class="error-text">${esc(state.loadingOptionsError)}</p>`:''}</section>`;}
function paintLoadingOptions(){const block=$('[data-loading-options]');if(block&&state.route==='help'){block.outerHTML=loadingOptionsPanel();disable();}}
async function loadLoadingOptions(){if(state.loadingOptionsPending)return;const epoch=state.experienceEpoch||0;try{const result=await request('loadingOptions');if(epoch!==(state.experienceEpoch||0))return;state.loadingOptions=typeof result.enabled==='boolean'?result:null;state.loadingOptionsError='';}catch(e){state.loadingOptionsError=e.message;}paintLoadingOptions();}
document.addEventListener('click',async e=>{const button=e.target.closest('[data-loading-toggle],[data-loading-remove]');if(!button||button.disabled||state.loadingOptionsPending||state.busy||state.coreApplying)return;const remove=button.hasAttribute('data-loading-remove');state.loadingOptionsPending=true;state.loadingOptionsMessage='';state.loadingOptionsError='';paintLoadingOptions();try{const options={quiet:true};const result=await run(remove?'loadingUninstall':'loadingSetEnabled',{confirmed:true,...(remove?{}:{enabled:!state.loadingOptions.enabled})},options);if(!result)throw new Error(options.error||'操作未完成，请重试。');state.loadingOptions=result.state;state.loadingOptionsMessage=result.message||'';}catch(e){state.loadingOptionsError=e.message;}finally{state.loadingOptionsPending=false;paintLoadingOptions();}});
// Library workflows share the same native queue and current game identity as launch.
function versions(){return snapshotUi.page();}
function transfer(){return libraryUi.transferPage();}
function javaMods(){return libraryUi.javaPage();}
const libraryUi=(()=>{
 const model={versions:null,reading:false,error:'',message:'',install:null,recommended:null,recommending:false,recommendError:'',queries:{},querying:new Set(),backups:null,backupId:'',backupName:'',backupSettings:null,backupMessage:'',backupError:'',working:false,epoch:0};
 titles.versions='版本管理';experienceGroups.versions=['versions'];
  titles.transfer='房屋迁移';experienceGroups.saves=['saves','transfer'];
   model.house={sourceId:'',targetId:'',minX:'',minY:'',maxX:'',maxY:'',marginChunks:0,preview:null,result:null,error:'',working:false,catalog:null,catalogLoading:false,catalogRevision:0,previewRevision:0,rangeMode:'auto',houseId:''};
  titles.javaMods='Java 模组';experienceGroups.components=['components','javaMods','mods','workshop'];
  Object.assign(model,{java:null,javaLoading:false,javaRequested:false,javaError:'',javaMessage:'',javaSearch:'',javaWorkflow:null,javaStage:'',javaProgress:-1,javaActive:false,javaRecovery:null,forceRecovery:null,forceResult:null,forceError:''});
  ['javaComponentsInstall','javaComponentsEnable','javaComponentsReconcile'].forEach(a=>mutationActions.add(a));mutationActions.add('javaComponentsApply');mutationActions.add('javaComponentsRecover');mutationActions.add('worldEditRecover');
  mutationActions.add('houseTransferApply');mutationActions.add('forceTakeoverApply');mutationActions.add('forceTakeoverRecover');
 for(const action of ['versionSelect','versionChooseImport','versionChoosePreserve','versionChooseInstall','versionInstallComplete','versionInstallCancel','backupCreate','backupRestore','backupDelete','backupSettingsSave','backupChooseDirectory'])mutationActions.add(action);
 const bytes=n=>n==null?'—':n>=1073741824?(n/1073741824).toFixed(1)+' GiB':n>=1048576?(n/1048576).toFixed(1)+' MiB':n.toLocaleString()+' B';
 const current=()=>model.versions?.versions?.find(v=>v.selected)||null;
 const blocked=()=>state.busy||state.coreApplying||model.working||!!state.launchAttempt;
 async function write(action,payload){const options={quiet:true,quietErrors:true},result=await run(action,payload,options);if(!result)throw new Error(options.error||'操作未完成，请重试。');return result;}
  async function inspectWorkshopComponent(id){if(model.javaLoading)return;model.java=null;model.javaRequested=true;model.javaMessage='正在识别已下载条目的标准组件元数据…';state.route='javaMods';location.hash='javaMods';render();await loadJavaComponents(String(id));if(!model.javaError){model.javaMessage=model.java?.rows?.length?'已识别 '+model.java.rows.length+' 个声明的 Java 组件。请核对来源后再启用；下载不会自动授权。':'没有找到声明标准 Java 元数据的组件。普通 Lua／模型资源可在本地模组清单核对，未自动判断为 3D 组件。';render();}}
  function componentDownloadDialog(){const dialog=document.createElement('dialog');dialog.id='workshop-component-dialog';dialog.className='library-dialog component-download-dialog';dialog.innerHTML='<h2>安装工坊组件</h2><p>填写作者的 Steam 工坊链接或条目 ID。将下载、识别并启用组件与所需依赖；含多个 Java 子模组时由你逐项选择。</p><form><label for="component-workshop-id">工坊链接 / 条目 ID</label><input id="component-workshop-id" required maxlength="2048" placeholder="https://steamcommunity.com/sharedfiles/filedetails/?id=…"><p class="error-text" role="alert"></p><div class="actions"><button type="button" data-component-download-cancel>返回</button><button class="primary" data-component-download type="submit">下载并安装</button></div></form>';dialog.querySelector('[data-component-download-cancel]').onclick=()=>dialog.close();dialog.addEventListener('close',()=>dialog.remove());dialog.querySelector('form').onsubmit=async event=>{event.preventDefault();if(blocked()){dialog.querySelector('[role=alert]').textContent='另一项组件操作正在进行，请等待完成或取消后，再提交此链接。';return;}const value=dialog.querySelector('input').value.trim();let id=value;try{if(!/^[0-9]{1,20}$/.test(id)){const url=new URL(value);if(url.protocol!=='https:'||url.hostname!=='steamcommunity.com'||url.username||url.password||url.port||!url.pathname.includes('/filedetails/'))throw new Error();id=url.searchParams.get('id')||'';}if(!/^[0-9]{1,20}$/.test(id)||!/[1-9]/.test(id))throw new Error();}catch{dialog.querySelector('[role=alert]').textContent='请输入有效的 Steam 工坊链接或数字条目 ID。';return;}dialog.close();dialog.remove();model.javaRequested=true;model.javaSearch='';state.route='javaMods';location.hash='javaMods';await javaWorkflow('javaComponentsInstall',{id,confirmed:true});};document.body.append(dialog);dialog.showModal();}
  async function loadJavaComponents(workshopId){if(model.javaLoading)return;const epoch=model.epoch;model.javaLoading=true;model.javaRequested=true;model.javaError='';if(state.route==='javaMods')render();try{const result=await request(workshopId?'javaComponentsInspectDownloaded':'javaComponentsList',workshopId?{id:String(workshopId)}:{});if(epoch!==model.epoch)return;model.java=result;const javaRecovery=await request('javaComponentsRecovery');if(epoch!==model.epoch)return;model.javaRecovery=javaRecovery;const forceRecovery=state.status?.configurationId==='steam'?await request('forceTakeoverRecovery'):{pending:false};if(epoch!==model.epoch)return;model.forceRecovery=forceRecovery;}catch(error){if(epoch===model.epoch)model.javaError=error.message;}finally{model.javaLoading=false;if(state.route==='javaMods')render();}}
  function javaPage(){
   const search=model.javaSearch.trim().toLowerCase(),all=model.java?.rows||[],locked=blocked()||state.status?.running,rows=all.filter(row=>[row.name,row.modId,row.packageName].join(' ').toLowerCase().includes(search)),source=row=>row.workshopId?'Steam 工坊':row.sourceKind==='user'?'手动安装':row.sourceKind==='game'?'游戏目录':'手动组件',card=row=>{const managed=!!row.managedKey,component=state.status?.components?.find(c=>c.id===row.managedKey),on=managed?(component?.configured??row.enabled):row.enabled,restorable=row.canRestore&&row.receiptId,removed=row.jarState==='quarantined',limitations=(row.limitations||[]).filter(text=>text!=='请使用现有组件安装与移除流程。'&&text!=='启停作用于本地默认清单和 Buddy 预加载；独立存档或服务器清单可能再次选用此模组。');return `<article class="java-component-row" data-java-component="${esc(row.token)}"><div class="java-component-symbol" aria-hidden="true">J</div><div class="java-component-main"><div class="java-component-name"><h3>${esc(row.name||row.modId)}</h3>${managed?'<span class="badge">启动器组件</span>':''}<span class="badge ${on?'status-positive':''}">${removed?'已卸载':row.javaBlocked?'已屏蔽':row.jarState==='unverified'?'未核实':on?'已启用':'未启用'}</span></div><p class="small">${esc(source(row))}${row.version?' · '+esc(row.version):''}${row.modId?' · '+esc(row.modId):''}</p>${!managed&&!row.canEnable&&!on&&!removed&&limitations.length?`<p class="status-warning small">${limitations.map(esc).join('；')}</p>`:''}${row.conflicts?.length?`<p class="status-warning small">${row.conflicts.map(esc).join('；')}</p>`:''}${row.workshopId?`<button class="link-button" data-mod-workshop="${esc(row.workshopId)}">工坊原页</button>`:''}<details><summary>来源与加载信息</summary>${limitations.length?`<p class="small java-component-limitation">${limitations.map(esc).join('；')}</p>`:''}<dl><dt>Java 包</dt><dd>${esc(row.packageName||'未声明')}</dd><dt>预加载</dt><dd>${esc(({enabled:'已启用',disabled:'未启用',none:'无',unknown:'未读取'})[row.preloadState]||row.preloadState||'无')}</dd></dl>${row.requires?.length?`<p class="small">依赖：${row.requires.map(esc).join('、')}</p>`:''}</details></div><div class="actions java-component-actions">${managed?(component?`<button ${on?'':'class="primary"'} data-component="${esc(row.managedKey)}" data-operation="${on?'uninstall':'install'}" ${locked?'disabled':''}>${on?'关闭':'启用'}</button>`:''):`${row.canEnable&&!on?`<button class="primary" data-java-component-action="enable" data-token="${esc(row.token)}" ${locked?'disabled':''}>启用</button>`:''}${row.canDisable?`<button data-java-component-action="disable" data-token="${esc(row.token)}" ${locked?'disabled':''}>屏蔽 Java</button>`:''}${row.canBlock&&!row.canDisable?`<button data-java-component-action="block" data-token="${esc(row.token)}" ${locked?'disabled':''}>屏蔽 Java</button>`:''}${row.canUninstall?`<button class="danger" data-java-component-action="uninstall" data-token="${esc(row.token)}" ${locked?'disabled':''}>卸载</button>`:''}${restorable?`<button data-java-component-action="restore" data-token="${esc(row.token)}" ${locked?'disabled':''}>恢复</button>`:''}`}</div></article>`;};
   return heading('<button class="primary" data-component-discover>安装工坊组件</button><button data-page="workshop">浏览创意工坊</button><button data-java-components-refresh>刷新</button>'+(state.status?.configurationId==='steam'?'<button data-force-takeover '+(locked?'disabled':'')+'>强制接管</button>':''))+forcePanel()+`<section class="card java-manager"><div class="java-manager-toolbar"><div><h2>已安装的 Java 模组</h2><p class="small">${all.length} 项 · ${all.filter(r=>r.enabled).length} 项启用</p></div><input type="search" id="java-components-search" aria-label="搜索 Java 模组" placeholder="搜索名称、模组 ID" value="${esc(model.javaSearch)}"></div>${javaWorkflowPanel()}${model.javaMessage?`<p class="library-feedback" role="status">${esc(model.javaMessage)}</p>`:''}${model.javaError?`<p class="error-text" role="alert">${esc(model.javaError)}</p>`:''}${model.java?.actionRequired?`<p class="library-feedback" role="status">${esc(model.java.actionRequired)}</p>`:''}${model.javaRecovery?.pending?`<div class="java-recovery"><p>上一次 Java 模组操作尚未完成。</p><button data-java-components-recover ${!model.javaRecovery.supported||blocked()?'disabled':''}>恢复上次操作</button></div>`:''}${model.javaLoading&&!model.java?'<p class="read-pending">正在读取 Java 模组…</p>':''}<div data-java-component-rows>${rows.map(card).join('')||(!model.javaLoading?'<div class="empty">没有找到 Java 模组。</div>':'')}</div>${model.java?.warnings?.some(w=>w!==model.java.actionRequired)?`<details class="java-scan-warnings"><summary>查看未能读取的来源</summary><ul>${model.java.warnings.filter(w=>w!==model.java.actionRequired).map(w=>`<li>${esc(w)}</li>`).join('')}</ul></details>`:''}</section>`;
  }
  function forcePanel(){const result=model.forceResult,recovery=model.forceRecovery;return `${model.forceError?`<p class="error-text" role="alert">${esc(model.forceError)}</p>`:''}${result?`<section class="card section" data-force-result><h2>${result.status==='completed'?'强制接管完成':result.status==='recovered'?'已恢复本次改动':result.status==='partial'?'接管部分完成':'接管未完成'}</h2><p>${esc(result.message||'')}</p>${result.unresolved?.length?`<ul>${result.unresolved.map(v=>`<li>${esc(v)}</li>`).join('')}</ul>`:''}</section>`:''}${recovery?.pending?`<section class="card section"><h2>上次强制接管未完成</h2><p>${esc(recovery.message||'')}</p><button data-force-recover ${!recovery.supported||blocked()?'disabled':''}>恢复本次改动</button></section>`:''}`;}
  async function forceAction(recover=false){if(blocked()||state.status?.running)return;const epoch=model.epoch;model.working=true;model.forceError='';render();try{const plan=await request(recover?'forceTakeoverRecovery':'forceTakeoverPreview');if(epoch!==model.epoch)return;if(!plan.supported)throw new Error(plan.message||'暂时无法接管，请重新读取。');const dialog=document.createElement('dialog');dialog.id='force-takeover-dialog';dialog.className='library-dialog';dialog.innerHTML=`<h2>${recover?'恢复本次强制接管？':'强制接管启动参数与 Java 组件？'}</h2><p>${recover?'恢复这次接管前的配置。':'先备份，清空当前账号此游戏的全部 Steam 启动参数，并停用已识别的本地 Java／3D 加载入口。之后从组件中心安装或启用。'}</p><p>本次操作会正常退出并重新打开 Steam。</p>${plan.locations?.length?`<details><summary>查看处理范围</summary><ul>${plan.locations.map(v=>`<li>${esc(v)}</li>`).join('')}</ul></details>`:''}${plan.unresolved?.length?`<h3>仍需检查</h3><ul>${plan.unresolved.map(v=>`<li>${esc(v)}</li>`).join('')}</ul>`:''}<div class="actions section"><button data-force-cancel>取消</button><button class="primary" data-force-confirm>${recover?'确认恢复':'备份并强制接管'}</button></div>`;document.body.append(dialog);dialog.showModal();const confirmed=await new Promise(resolve=>{dialog.querySelector('[data-force-cancel]').onclick=()=>resolve(false);dialog.querySelector('[data-force-confirm]').onclick=()=>resolve(true);dialog.oncancel=e=>{e.preventDefault();resolve(false);};});dialog.close();dialog.remove();if(!confirmed||epoch!==model.epoch)return;const result=await write(recover?'forceTakeoverRecover':'forceTakeoverApply',{stamp:plan.stamp,confirmed:true});if(epoch!==model.epoch)return;model.forceResult=result;model.javaRequested=false;await loadJavaComponents();await refresh();}catch(error){if(epoch===model.epoch)model.forceError=error.message;}finally{model.working=false;if(state.route==='javaMods')render();}}
  async function javaComponentAction(token,operation){
   if(operation==='enable'){await javaWorkflow('javaComponentsEnable',{token,confirmed:true});return;}
   if(blocked())return;const row=model.java?.rows.find(row=>row.token===token);if(!row)return;model.working=true;model.javaWorkflow=null;model.javaError='';model.javaMessage='';const epoch=model.epoch;let changed=false;render();try{const preview=await request('javaComponentsPreview',{token,operation,stamp:model.java.stamp,receiptId:row.receiptId||''});if(epoch!==model.epoch||state.route!=='javaMods')return;if(!preview.supported)throw new Error(preview.message||'这项组件暂时无法自动处理。');const labels={enable:'启用',disable:'屏蔽 Java',block:'屏蔽 Java',uninstall:'卸载',restore:'恢复'},label=labels[operation];const dialog=document.createElement('dialog');dialog.className='library-dialog java-component-confirm';dialog.innerHTML=`<h2>${label} ${esc(row.name||row.modId)}</h2><p>${esc(preview.message||'确认后执行此项操作。')}</p>${preview.warnings?.length?`<ul>${preview.warnings.map(w=>`<li>${esc(w)}</li>`).join('')}</ul>`:''}<details><summary>查看具体变更</summary><ul>${(preview.changes||[]).map(change=>`<li>${esc(change)}</li>`).join('')}</ul></details><div class="actions section"><button data-java-plan-cancel>取消</button><button class="primary" data-java-plan-confirm>确认${label}</button></div>`;document.body.append(dialog);dialog.showModal();const confirmed=await new Promise(resolve=>{dialog.querySelector('[data-java-plan-cancel]').onclick=()=>resolve(false);dialog.querySelector('[data-java-plan-confirm]').onclick=()=>resolve(true);dialog.oncancel=e=>{e.preventDefault();resolve(false);};});dialog.close();dialog.remove();if(!confirmed||epoch!==model.epoch)return;changed=true;const result=await write('javaComponentsApply',{previewStamp:preview.previewStamp,confirmed:true});if(!['completed','recovered','unchanged'].includes(result.status))throw new Error(result.message||'操作未完成。');model.javaMessage=result.message||'操作完成';}catch(error){model.javaError=error.message;}finally{model.working=false;const error=model.javaError;if(changed)await loadJavaComponents();model.javaError=error;if(state.route==='javaMods')render();}
  }
  function javaWorkflowPanel(){const result=model.javaWorkflow;if(model.javaActive)return `<div class="java-workflow-result" data-java-workflow-result role="status"><p data-java-workflow-stage>${esc(model.javaStage||'正在准备组件操作…')}</p><progress max="100" ${model.javaProgress>=0?'value="'+model.javaProgress+'"':''} data-java-workflow-progress aria-label="组件操作进度"></progress><button data-java-workflow-cancel data-safe>取消操作</button></div>`;if(!result)return '';return `<div class="java-workflow-result" data-java-workflow-result role="${['completed','unchanged','selectionRequired','cancelled'].includes(result.status)?'status':'alert'}"><p>${esc(result.message||'操作未完成，请重试。')}</p>${result.unresolved?.length?`<ul>${result.unresolved.map(r=>`<li>${esc(r.name||r.modId)}${r.name&&r.modId?'（'+esc(r.modId)+'）':''}：${esc(r.reason)}</li>`).join('')}</ul>`:''}${result.enabled?.length?`<p class="small">本次已启用：${result.enabled.map(esc).join('、')}</p>`:''}${result.downloaded?.length?`<p class="small">本次已下载：${result.downloaded.map(esc).join('、')}</p>`:''}${result.recoveryPending?'<p>部分修改仍需恢复，请使用下方恢复操作。</p>':''}</div>`;}
  async function javaWorkflow(action,payload){if(blocked())return;model.working=true;model.javaActive=true;model.javaStage=action==='javaComponentsInstall'?'正在准备下载与识别…':'正在核对组件与依赖…';model.javaWorkflow=null;model.javaProgress=-1;model.javaError='';model.javaMessage='';render();try{const result=await write(action,payload);model.javaWorkflow=result;if(result.status==='selectionRequired')model.java={...(model.java||{}),rows:result.rows||[]};else await loadJavaComponents();}catch(error){model.javaError=error.message;}finally{model.working=false;model.javaActive=false;if(state.route==='javaMods')render();}}
  async function refreshJavaComponents(){if(model.javaActive||model.javaLoading)return;await javaWorkflow('javaComponentsReconcile',{confirmed:true});}
  function syncJavaTaskPanel(){ $('#task')?.classList.toggle('java-inline-active',state.route==='javaMods'&&model.javaActive);document.querySelectorAll('[data-component-discover]').forEach(button=>button.disabled=model.working||state.pendingMutations>0||state.coreApplying||!!state.launchAttempt);}
  const previousJavaTask=task;task=function(data={}){previousJavaTask(data);syncJavaTaskPanel();if(!model.javaActive)return;if(data.text)model.javaStage=data.text;model.javaProgress=data.value>=0?Math.min(100,data.value):-1;const text=$('[data-java-workflow-stage]'),progress=$('[data-java-workflow-progress]');if(text)text.textContent=model.javaStage;if(progress){if(data.value>=0)progress.value=Math.min(100,data.value);else progress.removeAttribute('value');}};
  document.addEventListener('click',event=>{if(event.target.closest('[data-java-workflow-cancel]'))void request('cancel');});
  async function recoverJavaComponents(){if(blocked()||!model.javaRecovery?.pending)return;if(!await confirm('恢复上次 Java 模组操作？',model.javaRecovery.message||'恢复上一次未完成的修改。'))return;model.working=true;try{const result=await write('javaComponentsRecover',{stamp:model.javaRecovery.stamp,confirmed:true});if(!['completed','recovered','unchanged'].includes(result.status))throw new Error(result.message);model.javaMessage=result.message;await loadJavaComponents();}catch(error){model.javaError=error.message;}finally{model.working=false;render();}}
   function invalidateHouse(){const house=model.house;house.preview=null;house.result=null;house.error='';house.previewRevision++;}
   function selectedHouse(){const house=model.house;return house.catalog?.houses?.find(row=>row.id===house.houseId)||null;}
   function resetHouseSource(sourceId){const house=model.house;invalidateHouse();Object.assign(house,{sourceId,minX:'',minY:'',maxX:'',maxY:'',catalog:null,catalogLoading:false,rangeMode:'auto',houseId:''});house.catalogRevision++;}
   async function loadHouseCatalog(){const house=model.house,sourceId=house.sourceId,epoch=model.epoch,revision=++house.catalogRevision;if(!sourceId)return;house.catalogLoading=true;house.catalog=null;house.houseId='';render();const current=()=>epoch===model.epoch&&revision===house.catalogRevision&&sourceId===house.sourceId;try{const result=await request('houseCatalogList',{sourceId});if(!current())return;if(result.sourceId!==sourceId)throw new Error('安全屋登记与当前来源不一致，请重新读取。');house.catalog=result;const rows=Array.isArray(result.houses)?result.houses:[];if(result.detected&&rows.length){house.houseId=rows[0].id;}else house.rangeMode='manual';}catch(error){if(!current())return;house.catalog={sourceId,detected:false,houses:[],state:'unreadable',message:'未能读取安全屋登记，可手动选择房屋范围。'};house.rangeMode='manual';}finally{if(current()){house.catalogLoading=false;if(state.route==='transfer')render();}}}
   function houseRange(locked){const house=model.house,chosen=selectedHouse(),catalog=house.catalog;if(!house.sourceId)return '<p class="small">先选择来源存档，自动读取其中登记的安全屋。</p>';const manual=house.rangeMode==='manual',switcher=manual?(catalog?.detected?`<button data-house-auto ${locked?'disabled':''}>返回登记安全屋</button>`:''):`<button data-house-manual ${locked?'disabled':''}>手动选择范围</button>`;
    if(!manual){if(house.catalogLoading||!catalog)return `<div class="row"><div><h2>正在识别安全屋</h2><p class="small" role="status">正在读取来源存档的安全屋登记…</p></div>${switcher}</div>`;if(chosen){const bounds=chosen.bounds,members=chosen.members||[];return `<div class="row"><div><h2>选择登记安全屋</h2><p class="small">使用存档记录的实际范围，导入到目标世界的相同位置。</p></div>${switcher}</div><label class="house-claim-label">安全屋<select data-house-id ${locked?'disabled':''}>${catalog.houses.map(row=>`<option value="${esc(row.id)}" ${row.id===house.houseId?'selected':''}>${esc(row.name||row.title||'安全屋')}${row.owner?' · '+esc(row.owner):''}</option>`).join('')}</select></label><div class="house-detected"><h3>${esc(chosen.name||chosen.title||'安全屋')}</h3><p>${chosen.owner?'屋主：'+esc(chosen.owner):'未记录屋主'}${chosen.location?' · '+esc(chosen.location):''}</p>${members.length?`<p class="small">成员：${members.slice(0,12).map(esc).join('、')}${members.length>12?' 等 '+members.length+' 人':''}</p>`:''}<p class="house-native-bounds">实际范围 · X ${esc(bounds.minX)}–${esc(bounds.maxX)} · Y ${esc(bounds.minY)}–${esc(bounds.maxY)}</p></div>`;}}
    return `<div class="row"><div><h2>手动选择房屋范围</h2><p class="small">${esc(catalog?.message||'填写房屋两角的地图坐标，导入到目标存档的相同位置。')}</p></div>${switcher}</div><div class="house-coordinate-pair">${[['minX','左上角 X'],['minY','左上角 Y'],['maxX','右下角 X'],['maxY','右下角 Y']].map(([field,label])=>`<label>${label}<input type="number" step="1" data-house-field="${field}" value="${esc(house[field])}" ${locked?'disabled':''}></label>`).join('')}</div>`;
   }
   function transferPage(){
   const house=model.house,rows=(state.saves?.saves||[]).filter(s=>String(s.mode).toLowerCase()!=='multiplayer'),options=(selected,other)=>'<option value="">选择存档</option>'+rows.filter(s=>s.id!==other).map(s=>`<option value="${esc(s.id)}" ${s.id===selected?'selected':''}>${esc(s.name)} · ${esc(s.mode)}</option>`).join(''),preview=house.preview,result=house.result,locked=house.working||state.busy||state.status?.running;
    const rangeReady=house.rangeMode==='manual'||(!house.catalogLoading&&selectedHouse()&&house.catalog?.stamp);
    return heading('<button data-page="saves">返回存档</button>')+`<section class="card house-selection"><div class="house-worlds"><label>来源存档<select data-house-field="sourceId" ${locked?'disabled':''}>${options(house.sourceId,house.targetId)}</select></label><span aria-hidden="true">→</span><label>目标存档<select data-house-field="targetId" ${locked?'disabled':''}>${options(house.targetId,house.sourceId)}</select></label></div><div class="house-range">${houseRange(locked)}</div><div class="row house-preview-action"><label class="checkbox"><input type="checkbox" data-house-margin ${house.marginChunks?'checked':''} ${locked?'disabled':''}> 带上周围一圈区域</label><button class="primary" data-house-preview ${locked||!house.sourceId||!house.targetId||!rangeReady?'disabled':''}>${house.working?'正在处理…':'检查迁移内容'}</button></div>${state.status?.running?'<p class="small">退出游戏后可以迁移房屋。</p>':''}${house.error?`<p class="error-text" role="alert">${esc(house.error)}</p>`:''}</section>`+(preview?housePreview(preview):'')+(result?`<section class="card house-result" role="status"><span class="badge status-positive">迁移完成</span><h2>${esc(result.chunkCount)} 个区块已转移</h2><div class="house-metrics"><div><strong>${result.itemCountKnown===false?'未统计':esc(result.itemsTransferred)}</strong><span>${result.itemCountKnown===false?'转移物品数量':'件物品已转移'}</span></div><div><strong>${esc(result.itemsRemoved)}</strong><span>件缺失物品已跳过</span></div></div><div class="actions"><button data-save-backups="${esc(result.targetId)}">查看目标存档备份</button><button data-page="saves">返回存档</button></div></section>`:'');
  }
  function housePreview(preview){
   const bounds=preview.actualBounds,mods=(rows,missing)=>`<ol>${(rows||[]).map(id=>`<li class="${missing.includes(id)?'status-warning':''}">${esc(modTitle(modInfo(id))||id)}</li>`).join('')||'<li>无模组</li>'}</ol>`,dropped=preview.droppedTypes?.length?preview.droppedTypes:preview.missingTypes||[];
   return `<section class="card house-review"><div class="row"><div><span class="eyebrow">迁移预览</span><h2>${preview.canTransfer?'可以迁移':'需要处理以下问题'}</h2></div>${bounds?`<span class="small">X ${esc(bounds.minX)}–${esc(bounds.maxX)} · Y ${esc(bounds.minY)}–${esc(bounds.maxY)}</span>`:''}</div><div class="house-metrics"><div><strong>${esc(preview.chunkCount??0)}</strong><span>个区块</span></div><div><strong>${preview.itemCountKnown===false?'未统计':esc(preview.itemsTransferred??0)}</strong><span>${preview.itemCountKnown===false?'物品数量':'件可转移物品'}</span></div><div><strong>${esc(preview.overwrittenChunks??0)}</strong><span>个目标区块会替换</span></div><div><strong>${esc(preview.itemsRemoved??0)}</strong><span>件缺失物品会跳过</span></div></div>${preview.errors?.length?`<ul class="error-text">${preview.errors.map(error=>`<li>${esc(error)}</li>`).join('')}</ul>`:''}${preview.warnings?.length?`<ul class="house-warnings">${preview.warnings.map(w=>`<li>${esc(w)}</li>`).join('')}</ul>`:''}<div class="house-mod-comparison"><div><h3>来源模组 · ${preview.sourceMods?.length||0}</h3>${mods(preview.sourceMods,preview.sourceOnlyMods||[])}</div><div><h3>目标模组 · ${preview.targetMods?.length||0}</h3>${mods(preview.targetMods,[])}</div></div>${preview.sourceOnlyMods?.length?`<p class="status-warning">目标存档缺少 ${preview.sourceOnlyMods.length} 个来源模组。</p>`:''}${preview.orderChanges?.length?`<p class="small">${preview.orderChanges.length} 个模组的加载顺序不同。</p>`:''}${dropped.length?`<details class="house-missing"><summary>查看会跳过的物品 · ${preview.itemsRemoved} 件</summary><ul>${dropped.map(type=>`<li>${esc(type)}</li>`).join('')}</ul></details>`:''}<div class="row section"><p class="small">迁移前自动完整备份目标存档。</p><button class="primary" data-house-apply ${!preview.canTransfer||!preview.previewStamp||model.house.working||state.status?.running?'disabled':''}>备份并迁移</button></div></section>`;
  }
  async function previewHouse(){const house=model.house;if(house.working||state.busy)return;const epoch=model.epoch,payload={sourceId:house.sourceId,targetId:house.targetId};if(house.rangeMode==='auto'){if(house.catalogLoading||!selectedHouse()||!house.catalog?.stamp)return;payload.houseId=house.houseId;payload.catalogStamp=house.catalog.stamp;}else for(const field of ['minX','minY','maxX','maxY']){if(house[field]===''||!Number.isInteger(Number(house[field]))){house.error='请填写房屋两角的整数坐标。';render();return;}payload[field]=Number(house[field]);}payload.marginChunks=house.marginChunks;const revision=++house.previewRevision,current=()=>epoch===model.epoch&&revision===house.previewRevision&&payload.sourceId===house.sourceId&&payload.targetId===house.targetId;house.working=true;house.error='';house.result=null;house.preview=null;render();try{const result=await request('houseTransferPreview',payload);if(current())house.preview=result;}catch(error){if(current())house.error=error.message;}finally{if(current()){house.working=false;render();}}}
  async function applyHouse(){const house=model.house,preview=house.preview;if(house.working||!preview?.canTransfer)return;if(!await confirm('迁移房屋到目标存档？',`将替换目标存档中的 ${preview.overwrittenChunks} 个区块${preview.itemCountKnown===false?'，物品将随区块原样转移':'，转移 '+preview.itemsTransferred+' 件物品'}，跳过 ${preview.itemsRemoved} 件缺失物品。目标存档会先完整备份。`))return;if(house.preview!==preview)return;house.working=true;house.error='';render();try{house.result=await write('houseTransferApply',{previewStamp:preview.previewStamp,confirmed:true});house.preview=null;state.saves=null;await load('saves');}catch(error){house.error=error.message;house.preview=null;}finally{house.working=false;await loadWorldRecovery();render();}}
 function error(text){model.error=text;render();}
 async function loadVersions(){if(model.reading)return;model.reading=true;model.error='';try{const result=await request('versionList');model.versions={versions:[],...result};model.install=await request('versionInstallState');}catch(e){model.error=e.message;}finally{model.reading=false;render();}}
 function selectedChanged(){model.epoch++;state.experienceEpoch=(state.experienceEpoch||0)+1;state.actual={};state.experienceErrors={};state.experienceReads={};for(const key of ['saves','modCatalog','modProfiles','saveDetail','memory','settings','shaders','java','playProfiles','loadingOptions'])state[key]=null;state.saveDirty=false;state.modProfileDirty=false;state.playSelectionError='';model.backups=null;model.backupSettings=null;model.settingsRead=false;model.java=null;model.javaRequested=false;model.javaRecovery=null;model.forceRecovery=null;model.forceResult=null;model.forceError='';model.worldRecovery=null;model.worldRequested=false;model.backupId='';resetHouseSource('');model.house.targetId='';model.house.working=false;$('#save-backup-dialog')?.close();}
 async function select(id){if(blocked())return;if(hasUnsavedDrafts()&&!await confirm('切换游戏版本？','当前未保存的编辑将放弃。')){render();return;}model.working=true;model.message='';try{const result=await write('versionSelect',{id});selectedChanged();model.message=result.message||'已切换版本';model.versions=null;await loadVersions();await refresh();if(state.route==='saves')await load('saves');}catch(e){model.error=e.message;}finally{model.working=false;render();}}
 function installPanel(){const job=model.install;if(!job?.stage||job.stage==='none'||job.stage==='cancelled')return '';const steps={'awaiting-steam':'在 Steam 中选择版本','downloading':'Steam 正在下载','ready-to-preserve':'下载完成，可以保存独立版本','saved':'独立版本已保存','source-changed':'游戏文件已变化，请重新核对'};const ratio=job.bytesTotal>0?Math.max(0,Math.min(100,job.bytesDone*100/job.bytesTotal)):null;return `<section class="card library-install"><div class="row"><div><span class="eyebrow">安装进度</span><h2>${esc(steps[job.stage]||job.message||'正在核对安装')}</h2></div><span class="badge">B${esc(job.major||'')}</span></div>${job.stage==='awaiting-steam'?`<p>在 Steam 的“游戏版本与测试版”中选择 ${job.major===41?'legacy41':'目标 B42 分支'}，等待下载完成。</p>`:''}${ratio!==null?`<progress max="100" value="${ratio}"></progress><p class="small">${bytes(job.bytesDone)} / ${bytes(job.bytesTotal)}</p>`:''}<div class="actions"><button data-version-steam>打开 Steam</button>${job.stage==='ready-to-preserve'?'<button class="primary" data-version-complete>保存为独立版本</button>':''}${job.stage!=='saved'?'<button data-version-install-cancel>取消本次安装</button>':''}</div></section>`;}
 function versionPage(){const rows=model.versions?.versions||[],selected=current();return heading('<button data-version-import>添加已有版本</button><button class="primary" data-version-install>安装其他版本</button><button data-version-refresh>刷新</button>')+`<section class="library-overview"><div><span class="eyebrow">本地游戏库</span><h2>${selected?esc(selected.name):'选择你的游戏版本'}</h2><p>${selected?'Build '+esc(selected.version)+' · '+esc(selected.gamePath):'添加已有游戏，或通过 Steam 安装并保留独立版本。'}</p></div><div class="library-count"><strong>${rows.filter(v=>v.installed).length}</strong><span>已安装版本</span></div></section>${model.message?`<p class="library-feedback" role="status">${esc(model.message)}</p>`:''}${model.error?`<p class="error-text" role="alert">${esc(model.error)}</p>`:''}${installPanel()}<div class="library-version-grid">${rows.map(v=>`<article class="card version-card ${v.selected?'is-selected':''}"><div class="row"><span class="version-mark">${esc(v.version?.split('.')[0]||'PZ')}</span><span class="badge ${v.selected?'status-positive':''}">${v.selected?'当前版本':v.installed?'已安装':'文件已移动'}</span></div><h2>${esc(v.name)}</h2><p class="version-number">Build ${esc(v.version)}</p><dl><dt>游戏位置</dt><dd>${esc(v.gamePath)}</dd><dt>存档与设置</dt><dd>${esc(v.userPath)}</dd></dl><div class="actions">${!v.selected?`<button class="primary" data-version-select="${esc(v.id)}" ${!v.installed||blocked()?'disabled':''}>使用此版本</button>`:`<button class="primary" data-page="home">返回启动</button><button data-version-preserve>保存独立副本</button>`}</div></article>`).join('')||`<div class="empty">${model.reading?'正在读取版本库…':'还没有可显示的版本。'}</div>`}</div>`;}
 async function recommendations(refresh=false){if(model.recommending)return;model.recommending=true;model.recommendError='';if(refresh){model.queries={};model.querying.clear();}render();try{model.recommended=await request('recommendedServers',{refresh});}catch(e){model.recommendError=e.message;}finally{model.recommending=false;render();queryRecommendations();}}
 async function queryRecommendations(){if(state.route==='servers'&&state.serverTab==='recommended')queueVisibleServerMetrics();}
 function recommendationImage(value){try{const uri=new URL(value);return uri.protocol==='https:'&&!uri.username&&!uri.password&&!uri.port&&['orangezl.com','images.steamusercontent.com','steamuserimages-a.akamaihd.net'].includes(uri.hostname)?uri.href:'';}catch{return '';}}
 function recommendedPage(){const rows=model.recommended?.servers||[];return `${model.versions?.localPreview?'<div class="actions recommendation-management"><button data-recommended-admin>管理推荐名单 ↗</button></div>':''}${model.recommendError?`<p class="error-text">${esc(model.recommendError)}</p>`:''}${model.recommended?.warning?`<p class="small" role="status">${esc(model.recommended.warning)}</p>`:''}<div class="recommended-grid">${rows.map((s,index)=>{const q=model.queries[s.id],cover=recommendationImage(s.cover),images=(s.screenshots||[]).map(recommendationImage).filter(Boolean).slice(0,8),actualName=q?.status==='online'&&q.name?q.name:s.name;return `<article class="card recommended-card" data-recommended-card="${esc(s.id)}"><div class="recommended-art art-${index%4}">${cover?`<img src="${esc(cover)}" alt="" loading="lazy" referrerpolicy="no-referrer">`:''}<span class="recommended-rank">${String(index+1).padStart(2,'0')}</span><span class="badge">${esc(q?.version||s.version?'Build '+(q?.version||s.version):'推荐服务器')}</span></div><div class="recommended-body"><h2>${esc(actualName)}</h2><p class="small recommended-alias" ${actualName!==s.name?'':'hidden'}>推荐名称：${esc(s.name)}</p><p class="server-endpoint">${esc(s.host)}:${esc(s.port)}</p><div class="recommended-tags">${(s.tags||[]).map(t=>`<span>${esc(t)}</span>`).join('')}</div><p class="recommended-description">${esc(s.description||'')}</p><details class="recommended-introduction" data-disclosure="recommended-${esc(s.id)}"><summary>详细介绍与服务器公开信息</summary><p class="recommendation-extended" ${s.extendedDescription?'':'hidden'}>${esc(s.extendedDescription||'')}</p><h3>服务器公开介绍</h3><div class="server-description small">${richDescription(q?.descriptionSource==='server'?q.description:'服务器尚未返回公开介绍。')}</div><p class="small">${esc(q?.descriptionWarning||q?.error||'')}</p></details><div class="recommended-gallery" data-recommended-gallery ${images.length?'':'hidden'}>${images.map((image,i)=>`<button data-recommended-image="${esc(s.id)}" data-image-index="${i}" aria-label="查看服务器截图 ${i+1}"><img src="${esc(image)}" alt="服务器截图 ${i+1}" loading="lazy" referrerpolicy="no-referrer"></button>`).join('')}</div><div class="recommended-metrics"><span>在线 <strong>${q?.players==null?'—':esc(q.players)+' / '+esc(q.maxPlayers??'—')}</strong></span><span>延迟 <strong>${q?.latencyMs==null?'—':esc(q.latencyMs)+' ms'}</strong></span></div><div class="actions"><button class="primary" data-recommended-connect="${esc(s.id)}" ${blocked()?'disabled':''}>连接服务器</button><button data-recommended-import="${esc(s.id)}" ${blocked()?'disabled':''}>导入个人列表</button></div></div></article>`;}).join('')||`<div class="empty"><h3>${model.recommending?'正在读取推荐服务器…':'推荐名单暂时为空'}</h3>${!model.recommending?'<button data-recommended-refresh>刷新列表</button>':''}</div>`}</div>`;}
 function paintRecommendedCards(){const current=$('#page .recommended-grid');if(!current)return;const template=document.createElement('template');template.innerHTML=recommendedPage();const next=template.content.querySelector('.recommended-grid');if(next){patchServerContent(current,next);disable();}}
 async function importRecommendation(id){const row=model.recommended?.servers.find(value=>value.id===id);if(!row||blocked())return;if(!state.servers)await load('servers');const saved=state.servers?.servers.find(value=>value.host.toLowerCase()===row.host.toLowerCase()&&value.port===row.port);serverDialog(saved?.id||'',{name:model.queries[id]?.name||row.name,host:row.host,port:row.port,queryPort:row.queryPort||0,useSteamRelay:!!row.useSteamRelay,description:row.description||''});const dialog=$('#server-editor');if(dialog){dialog.querySelector('h2').textContent='导入个人列表';dialog.querySelector('[data-server-write]').textContent='保存到个人列表';}}
 async function connectRecommendation(id){const row=model.recommended?.servers.find(value=>value.id===id);if(!row||blocked())return;if(!state.servers)await load('servers');const saved=state.servers?.servers.find(value=>value.host.toLowerCase()===row.host.toLowerCase()&&value.port===row.port);if(saved?.username){await launchExperience('server',saved.id);return;}serverDialog(saved?.id||'',{name:model.queries[id]?.name||row.name,host:row.host,port:row.port,queryPort:row.queryPort||0,useSteamRelay:!!row.useSteamRelay,description:row.description||''});const dialog=$('#server-editor');if(dialog){dialog.dataset.recommendedConnection='true';dialog.querySelector('h2').textContent='连接服务器';dialog.querySelector('[data-server-write]').textContent='保存并连接';}}
 function showRecommendationImage(id,index){const row=model.recommended?.servers.find(value=>value.id===id),images=(row?.screenshots||[]).map(recommendationImage).filter(Boolean).slice(0,8),image=images[index];if(!image)return;const dialog=document.createElement('dialog');dialog.className='recommendation-image-dialog';dialog.innerHTML=`<div class="row"><h2>${esc(row.name)} · 截图 ${index+1}</h2><button type="button">关闭图片</button></div><img src="${esc(image)}" alt="服务器截图 ${index+1}" referrerpolicy="no-referrer">`;dialog.querySelector('button').onclick=()=>dialog.close();dialog.addEventListener('close',()=>dialog.remove());document.body.append(dialog);dialog.showModal();}
 function backupSettingsPanel(){const settings=model.backupSettings;if(!settings)return '';return `<section class="card section backup-settings"><div class="row"><h2>自动备份</h2><span class="small">${settings.afterExit?'游戏退出后备份':'手动备份'}</span></div><div class="backup-setting-fields"><label class="checkbox"><input data-backup-auto type="checkbox" ${settings.afterExit?'checked':''}> 游戏退出后自动备份</label><label>自动备份保留数量<input data-backup-retention type="number" min="0" max="10000" value="${esc(settings.retention)}"></label></div><div class="row"><p class="backup-directory">${esc(settings.directory)}</p><div class="actions"><button data-backup-directory>更换位置</button><button data-backup-settings-save>保存设置</button></div></div>${settings.lastMessage?`<p class="small">${esc(settings.lastMessage)}</p>`:''}</section>`;}
 async function loadBackupSettings(){const epoch=model.epoch;try{const result=await request('backupSettings');if(epoch===model.epoch){model.backupSettings=result;if(state.route==='saves')render();}}catch(e){model.backupError=e.message;}}
  async function loadWorldRecovery(){const epoch=model.epoch;try{const result=await request('worldEditRecovery');if(epoch===model.epoch){model.worldRecovery=result;model.worldRecoveryError='';if(state.route==='saves')render();}}catch(error){if(epoch===model.epoch){model.worldRecoveryError=error.message;if(state.route==='saves')render();}}}
  function worldRecoveryPanel(){return `<section class="card section world-recovery"><h2>有存档操作尚未完成</h2>${(model.worldRecovery?.entries||[]).map(entry=>`<div class="row section"><div><h3>${esc(entry.name)}</h3><p class="small">${esc(entry.message)}</p></div><button data-world-recover="${esc(entry.id)}" ${!entry.supported||blocked()||state.status?.running?'disabled':''}>恢复存档状态</button></div>`).join('')}${model.worldRecoveryError?`<p class="error-text">${esc(model.worldRecoveryError)}</p>`:''}</section>`;}
  async function recoverWorld(id){const entry=model.worldRecovery?.entries?.find(entry=>entry.id===id);if(!entry?.supported||blocked())return;if(!await confirm('恢复存档状态？',entry.message))return;model.working=true;model.worldRecoveryError='';try{const result=await write('worldEditRecover',{id,stamp:entry.stamp,confirmed:true});model.message=result.message;await loadWorldRecovery();await load('saves');}catch(error){model.worldRecoveryError=error.message;}finally{model.working=false;await loadWorldRecovery();render();}}
 function backupContents(){const exists=state.saves?.saves.some(s=>s.id===model.backupId);return `<div class="row"><div><span class="eyebrow">${esc(model.backupName)}</span><h2 id="save-backup-title">存档备份</h2></div><button data-backup-close aria-label="关闭备份窗口" ${model.working?'disabled':''}>关闭</button></div>${exists?`<div class="row section"><input data-backup-label placeholder="备份名称（可选）" aria-label="备份名称"><button class="primary" data-backup-create ${blocked()||state.status?.running?'disabled':''}>立即备份</button></div>`:''}<p data-backup-message role="status">${esc(model.backupMessage)}</p>${model.backupError?`<p class="error-text" role="alert">${esc(model.backupError)}</p>`:''}<div class="backup-history">${model.backups===null?'<div class="empty">正在读取备份…</div>':model.backups.archives?.length?model.backups.archives.map(a=>`<article class="backup-history-row"><div><strong>${esc(a.label||'存档备份')}</strong><p class="small">${esc(localDate(a.createdUtc))} · ${bytes(a.bytes)} · ${esc(a.fileCount)} 个文件${a.recovery?' · 恢复前保留':a.automatic?' · 自动':''}</p></div><div class="actions"><button class="primary" data-backup-restore="${esc(a.id)}" ${blocked()||state.status?.running?'disabled':''}>恢复</button><button class="danger" data-backup-delete="${esc(a.id)}" ${blocked()?'disabled':''}>删除</button></div></article>`).join(''):'<div class="empty">还没有备份。点击“立即备份”保存当前世界。</div>'}</div>`;}
 function paintBackup(){const dialog=$('#save-backup-dialog');if(dialog?.open)dialog.innerHTML=backupContents();}
 async function inventory(){let dialog=$('#backup-inventory-dialog');if(!dialog){dialog=document.createElement('dialog');dialog.id='backup-inventory-dialog';dialog.className='library-dialog';document.body.append(dialog);}dialog.innerHTML='<div class="row"><h2>备份记录</h2><button data-backup-inventory-close>关闭</button></div><p>正在读取备份…</p>';dialog.showModal();try{const result=await request('backupInventory');dialog.innerHTML='<div class="row"><h2>备份记录</h2><button data-backup-inventory-close>关闭</button></div><div class="backup-history">'+(result.saves||[]).map(s=>`<article class="backup-history-row"><div><strong>${esc(s.name)}</strong><p class="small">${esc(s.mode)} · ${s.count} 份备份 · ${esc(localDate(s.latestUtc))}</p></div><button data-inventory-save="${esc(s.id)}">查看备份</button></article>`).join('')+(!(result.saves||[]).length?'<div class="empty">还没有备份记录。</div>':'')+'</div>';}catch(e){dialog.innerHTML='<div class="row"><h2>备份记录</h2><button data-backup-inventory-close>关闭</button></div><p class="error-text">'+esc(e.message)+'</p>';}}
 async function history(){const epoch=model.epoch,id=model.backupId;try{const result=await request('backupList',{id});if(epoch===model.epoch&&id===model.backupId)model.backups=result;}catch(e){if(epoch===model.epoch)model.backupError=e.message;}paintBackup();}
 async function openBackups(id){model.backupId=id;model.backupName=state.saves?.saves.find(s=>s.id===id)?.name||id;model.backups=null;model.backupError='';model.backupMessage='';let dialog=$('#save-backup-dialog');if(!dialog){dialog=document.createElement('dialog');dialog.id='save-backup-dialog';dialog.className='library-dialog';dialog.setAttribute('aria-labelledby','save-backup-title');dialog.addEventListener('cancel',e=>{if(model.working)e.preventDefault();});document.body.append(dialog);}dialog.innerHTML=backupContents();dialog.showModal();await history();}
 async function backupOperation(action,backupId){if(blocked())return;const id=model.backupId,label=$('[data-backup-label]')?.value||'';if(action==='backupRestore'&&!await confirm('恢复这份存档？','当前存档会先保留一份恢复前备份。'))return;if(action==='backupDelete'&&!await confirm('删除这份备份？','确认后移除所选备份。'))return;model.working=true;model.backupError='';model.backupMessage='';paintBackup();try{const result=await write(action,{id,...(action==='backupCreate'?{label}:{backupId,confirmed:true})});model.backupMessage=result.message||'操作完成';await history();if(action==='backupRestore')await load('saves');}catch(e){model.backupError=e.message;}finally{model.working=false;if(action==='backupRestore')await loadWorldRecovery();paintBackup();}}
 function settingValues(){return{directory:model.backupSettings.directory,retention:Number($('[data-backup-retention]')?.value??model.backupSettings.retention),afterExit:!!$('[data-backup-auto]')?.checked};}
 function versionDialog(kind){const dialog=document.createElement('dialog');dialog.className='library-dialog version-dialog';dialog.innerHTML=`<h2>${kind==='install'?'安装游戏版本':kind==='import'?'添加已有版本':'保存独立版本'}</h2><form><label>版本名称<input name="name" value="${kind==='preserve'?esc(current()?.name+' · 独立副本'):'我的游戏版本'}" maxlength="80" required></label>${kind==='install'?'<label>目标版本<select name="major"><option value="42">Build 42</option><option value="41">Build 41</option></select></label><p>先保留当前 Steam 版本，再打开 Steam 下载所选版本。</p>':''}<div class="actions"><button type="button" data-version-dialog-cancel>返回</button><button type="submit" class="primary">${kind==='import'?'选择游戏文件夹':kind==='install'?'选择位置并打开 Steam':'选择保存位置'}</button></div><p class="error-text" role="alert"></p></form>`;document.body.append(dialog);dialog.querySelector('[data-version-dialog-cancel]').onclick=()=>{dialog.close();dialog.remove();};dialog.addEventListener('cancel',()=>dialog.remove());dialog.querySelector('form').onsubmit=async e=>{e.preventDefault();if(blocked())return;const name=dialog.querySelector('[name=name]').value.trim(),major=Number(dialog.querySelector('[name=major]')?.value||42);model.working=true;dialog.querySelectorAll('button').forEach(b=>b.disabled=true);try{const result=await write({import:'versionChooseImport',preserve:'versionChoosePreserve',install:'versionChooseInstall'}[kind],{name,...(kind==='install'?{major}:{})});if(result.cancelled){dialog.querySelectorAll('button').forEach(b=>b.disabled=false);return;}model.message=result.message||'版本任务已完成';dialog.close();dialog.remove();await loadVersions();}catch(error){dialog.querySelector('[role=alert]').textContent=error.message;dialog.querySelectorAll('button').forEach(b=>b.disabled=false);}finally{model.working=false;render();}};dialog.showModal();}
 const baseRenderLibrary=render;render=function(){baseRenderLibrary();syncJavaTaskPanel();const selected=current();if(state.route==='javaMods'&&!model.javaRequested&&!model.javaLoading)queueMicrotask(()=>loadJavaComponents());if(state.route==='home'&&!$('#launch-configuration-select')&&!$('#home-version-select')){const launch=$('#page .home-launch')||$('#page .launch-config')||$('#page .home-scene');if(launch){const bar=document.createElement('div');bar.className='home-version-picker';bar.innerHTML=`<label for="home-version-select">游戏版本</label><select id="home-version-select" ${blocked()||state.status?.running?'disabled':''}>${(model.versions?.versions||[]).filter(v=>v.installed).map(v=>`<option value="${esc(v.id)}" ${v.selected?'selected':''}>${esc(v.name)} · ${esc(v.version)}</option>`).join('')||'<option>正在读取版本…</option>'}</select><button data-page="versions">管理版本</button>`;launch.before(bar);}}if(state.route==='saves'){if(!model.worldRequested){model.worldRequested=true;void loadWorldRecovery();}if(model.worldRecoveryError&&!model.worldRecovery?.pending&&!$('.world-read-error'))$('#page .heading')?.insertAdjacentHTML('afterend',`<div class="card section world-read-error"><p>${esc(model.worldRecoveryError)}</p><button data-world-retry>重新读取存档状态</button></div>`);if(model.worldRecovery?.pending&&!$('.world-recovery'))$('#page .heading')?.insertAdjacentHTML('afterend',worldRecoveryPanel());if(!$('[data-backup-inventory]'))$('#page .heading .actions')?.insertAdjacentHTML('afterbegin','<button data-backup-inventory>备份记录</button>');for(const card of document.querySelectorAll('.save-card')){const id=card.querySelector('[data-save-open]')?.dataset.saveOpen;if(id&&!card.querySelector('[data-save-backups]'))card.querySelector('.actions')?.insertAdjacentHTML('beforeend',`<button data-save-backups="${esc(id)}">备份与恢复</button>`);}if(model.backupSettings&&!$('.backup-settings'))$('#page .heading')?.insertAdjacentHTML('afterend',backupSettingsPanel());}if(state.route==='servers'&&state.serverTab==='recommended'&&!model.recommended&&!model.recommending)queueMicrotask(()=>recommendations());if(state.route==='saves'&&!model.backupSettings&&!model.settingsRead){model.settingsRead=true;void loadBackupSettings();}syncServerMetrics();};
 document.addEventListener('change',e=>{if(e.target.id==='home-version-select')void select(e.target.value);});
  document.addEventListener('input',e=>{const field=e.target.dataset.houseField;if(field&&e.target.matches('input[data-house-field]')){model.house[field]=e.target.value;invalidateHouse();$('.house-review')?.remove();$('.house-result')?.remove();}});
  document.addEventListener('input',e=>{if(e.target.id==='java-components-search'){model.javaSearch=e.target.value;render();}});
  document.addEventListener('change',e=>{if(e.target.matches('select[data-house-field]')){const field=e.target.dataset.houseField;if(field==='sourceId'){resetHouseSource(e.target.value);if(model.house.sourceId)void loadHouseCatalog();else render();}else{model.house[field]=e.target.value;invalidateHouse();render();}}if(e.target.matches('[data-house-id]')){model.house.houseId=e.target.value;invalidateHouse();render();}if(e.target.matches('[data-house-margin]')){model.house.marginChunks=e.target.checked?1:0;invalidateHouse();render();}});
 document.addEventListener('click',async e=>{const b=e.target.closest('button');if(!b||b.disabled)return;let handled=true;
  if(b.dataset.serverTab==='recommended'){state.serverTab='recommended';render();if(!model.recommended)void recommendations();else void queryRecommendations();}
  else if(b.hasAttribute('data-server-source-refresh')&&state.serverTab==='recommended'||b.hasAttribute('data-recommended-refresh'))void recommendations(true);
  else if(b.hasAttribute('data-version-refresh'))void loadVersions();
  else if(b.dataset.versionSelect)void select(b.dataset.versionSelect);
  else if(b.hasAttribute('data-version-import'))versionDialog('import');else if(b.hasAttribute('data-version-preserve'))versionDialog('preserve');else if(b.hasAttribute('data-version-install'))versionDialog('install');
  else if(b.hasAttribute('data-version-steam'))void request('versionOpenSteam').catch(e=>error(e.message));
  else if(b.hasAttribute('data-version-complete')){model.working=true;try{const result=await write('versionInstallComplete',{});model.message=result.message;await loadVersions();}catch(error){model.error=error.message;}finally{model.working=false;render();}}
  else if(b.hasAttribute('data-version-install-cancel')){try{await write('versionInstallCancel',{});await loadVersions();}catch(error){model.error=error.message;render();}}
  else if(b.dataset.recommendedConnect)void connectRecommendation(b.dataset.recommendedConnect);else if(b.dataset.recommendedImport)void importRecommendation(b.dataset.recommendedImport);else if(b.dataset.recommendedImage)showRecommendationImage(b.dataset.recommendedImage,Number(b.dataset.imageIndex));
  else if(b.hasAttribute('data-recommended-admin'))void request('recommendedAdmin').catch(e=>notify(e.message));
  else if(b.hasAttribute('data-backup-inventory'))void inventory();else if(b.hasAttribute('data-backup-inventory-close'))$('#backup-inventory-dialog')?.close();else if(b.dataset.inventorySave){$('#backup-inventory-dialog')?.close();void openBackups(b.dataset.inventorySave);}else if(b.dataset.saveBackups)void openBackups(b.dataset.saveBackups);
  else if(b.hasAttribute('data-backup-close'))$('#save-backup-dialog')?.close();else if(b.hasAttribute('data-backup-create'))void backupOperation('backupCreate');else if(b.dataset.backupRestore)void backupOperation('backupRestore',b.dataset.backupRestore);else if(b.dataset.backupDelete)void backupOperation('backupDelete',b.dataset.backupDelete);
  else if(b.hasAttribute('data-house-preview'))void previewHouse();else if(b.hasAttribute('data-house-apply'))void applyHouse();else if(b.hasAttribute('data-house-manual')||b.hasAttribute('data-house-auto')){model.house.rangeMode=b.hasAttribute('data-house-manual')?'manual':'auto';invalidateHouse();render();}
  else if(b.hasAttribute('data-force-takeover'))void forceAction();else if(b.hasAttribute('data-force-recover'))void forceAction(true);
  else if(b.hasAttribute('data-component-discover'))componentDownloadDialog();else if(b.hasAttribute('data-components-manage'))route('javaMods');else if(b.hasAttribute('data-java-components-refresh'))void refreshJavaComponents();else if(b.dataset.javaComponentAction)void javaComponentAction(b.dataset.token,b.dataset.javaComponentAction);else if(b.hasAttribute('data-java-components-recover'))void recoverJavaComponents();else if(b.dataset.javaWorkshopInspect)void inspectWorkshopComponent(b.dataset.javaWorkshopInspect);
  else if(b.dataset.worldRecover)void recoverWorld(b.dataset.worldRecover);else if(b.hasAttribute('data-world-retry'))void loadWorldRecovery();
  else if(b.hasAttribute('data-backup-settings-save')||b.hasAttribute('data-backup-directory')){if(!model.backupSettings)return;try{const values=settingValues(),choose=b.hasAttribute('data-backup-directory');delete values.directory;const result=await write(choose?'backupChooseDirectory':'backupSettingsSave',values);if(!result.cancelled){model.backupSettings=result;model.message='备份设置已保存';render();}}catch(error){notify(error.message);}}
  else handled=false;if(handled){e.preventDefault();e.stopImmediatePropagation();}
 },true);
  const baseRouteLibrary=route;route=function(name){baseRouteLibrary(name);if(name==='versions'&&!model.versions&&!model.reading)void loadVersions();if(name==='javaMods'&&!model.javaRequested)void loadJavaComponents();if(name==='transfer'){if(!state.saves)void load('saves');if(!state.modCatalog)void load('modCatalog');}if(name==='servers'&&state.serverTab==='recommended'&&model.recommended)void queryRecommendations();};
 setInterval(async()=>{if(state.route==='versions'&&model.install?.stage&&!['none','saved','cancelled'].includes(model.install.stage)&&!model.working){try{model.install=await request('versionInstallState');render();}catch{}}},3000);
  const previousActualWritten=actualWritten;actualWritten=function(action,data){previousActualWritten(action,data);if(action==='chooseGame'&&!data.cancelled){selectedChanged();model.versions=null;void loadVersions();}if(['componentsAction','steamDownload'].includes(action)&&!data.cancelled){model.javaRequested=false;if(state.route==='javaMods')void loadJavaComponents();}};
  return {versionPage,transferPage,javaPage,recommendedPage,paintRecommendedCards,inspectWorkshopComponent,loadVersions,model};
})();
// Source mirrored inline in app.js; no separate runtime resource is required.
const launchConfigurationUi = (() => {
  const model = {
    list: null,
    versions: null,
    selected: null,
    selecting: "",
    cover: null,
    epoch: 0,
    reading: null,
    error: "",
    editor: null,
    jobs: new Map(),
  };
  const raw = request,
    selectedId = () => model.selecting || model.list?.selectedId || "launcher";
  const owned = new Set([
    "savesList",
    "serverList",
    "status",
    "modCatalog",
    "modProfiles",
    "memoryGet",
    "memorySave",
    "javaList",
    "javaSave",
    "javaComponentsList",
    "javaComponentsInstall",
    "javaComponentsEnable",
    "javaComponentsPreview",
    "javaComponentsApply",
    "javaComponentsRecovery",
    "javaComponentsRecover",
    "javaComponentsReconcile",
    "javaComponentsInspectDownloaded",
    "componentsAction",
    "modSort",
    "steamDownload",
    "forceTakeoverPreview",
    "forceTakeoverApply",
    "buddyPreview",
    "buddyTakeover",
  ]);
  for (const action of [
    "configurationRequest",
    "configurationCreate",
    "configurationRename",
    "configurationVersion",
    "configurationDelete",
    "configurationSaveOrdinary",
    "frozenConfigurationSave",
    "frozenConfigurationApprove",
    "frozenConfigurationUninstall",
    "frozenConfigurationRecover",
  ])
    mutationActions.add(action);
  const pureReads = new Set([
    "status",
    "modCatalog",
    "modProfiles",
    "memoryGet",
    "javaList",
    "javaComponentsList",
    "javaComponentsRecovery",
    "modSort",
    "savesList",
    "serverList",
  ]);
  const readId = () => crypto.randomUUID().replaceAll("-", "");
  async function ownerRequest(id, action, payload = {}, editor = null) {
    const token = pureReads.has(action) ? readId() : "";
    if (editor && token) editor.readRequests.add(token);
    let result;
    try {
      result = await raw("configurationRequest", {
        id,
        action,
        payload,
        ...(token ? { readId: token } : {}),
      });
    } finally {
      if (editor && token) editor.readRequests.delete(token);
    }
    if (result.configurationId !== id)
      throw Error("配置回复不匹配，请重新读取。");
    return result.data;
  }
  request = async function (action, payload = {}) {
    if (!owned.has(action)) return raw(action, payload);
    const id = selectedId(),
      epoch = model.epoch;
    const data = await ownerRequest(id, action, payload);
    if (
      epoch !== model.epoch &&
      [
        "status",
        "memoryGet",
        "modCatalog",
        "modProfiles",
        "javaList",
        "javaComponentsList",
      ].includes(action)
    )
      throw Error("配置选择已变化，已忽略之前的读取。");
    return data;
  };
  const kind = (row) =>
    row?.kind === "frozen"
      ? "快照配置"
      : row?.kind === "live"
        ? "普通配置"
        : "内置配置";
  const description = (row) =>
    row?.id === "steam"
      ? "使用 Steam 与原游戏的启动设置，可按需接管。"
      : row?.id === "vanilla"
        ? "使用原版游戏，不加载普通模组和可选 Java 组件。"
        : row?.kind === "frozen"
          ? "固定游戏与模组副本；工坊更新不会改变这份快照。"
          : "使用所选游戏版本和当前模组文件。";
  const versionOptions = (id) =>
    (model.versions?.versions || [])
      .filter((v) => v.installed)
      .map(
        (v) =>
          `<option value="${esc(v.id)}" ${v.id === id ? "selected" : ""}>${esc(v.name)} · ${esc(v.version)}</option>`,
      )
      .join("");
  async function readCover(id, epoch) {
    const row = model.list?.configurations.find((r) => r.id === id);
    model.selected = row;
    model.cover = null;
    if (!row?.available) return;
    const [status, memory] = await Promise.all([
      ownerRequest(id, "status"),
      ownerRequest(id, "memoryGet"),
    ]);
    if (epoch !== model.epoch || id !== selectedId()) return;
    if (status.components && independentCore(status.components))
      status.components = status.components.filter((c) => c.id !== "base");
    model.cover = { status, memory };
    state.actual.memory = memory;
    acceptStatus(status);
  }
  async function reload() {
    if (model.reading) return model.reading;
    const epoch = model.epoch;
    model.error = "";
    model.reading = (async () => {
      try {
        const [list, versions] = await Promise.all([
          raw("launchConfigurations"),
          raw("versionList"),
        ]);
        if (epoch !== model.epoch) return;
        model.list = list;
        model.versions = versions;
        await readCover(list.selectedId, epoch);
      } catch (error) {
        if (epoch === model.epoch) model.error = error.message;
      } finally {
        model.reading = null;
        if (state.route === "home" || state.route === "components") render();
      }
    })();
    return model.reading;
  }
  async function select(id) {
    const epoch = ++model.epoch;
    model.selecting = id;
    model.selected = model.list?.configurations.find((row) => row.id === id);
    model.cover = null;
    render();
    model.error = "";
    cancelLaunchAttempt();
    state.playProfiles = null;
    state.playPreview = null;
    state.preflight = null;
    state.actual = {};
    state.java = null;
    state.memory = null;
    state.modCatalog = null;
    libraryUi.model.javaRequested = false;
    libraryUi.model.epoch++;
    try {
      const list = await raw("launchConfigurationSelect", { id });
      if (epoch !== model.epoch) return;
      model.list = list;
      model.selected = list.configurations.find((r) => r.id === id);
      render();
      await readCover(id, epoch);
    } catch (error) {
      if (epoch === model.epoch) model.error = error.message;
    } finally {
      if (epoch === model.epoch) {
        model.selecting = "";
        render();
      }
    }
  }
  const previousEnsure = ensureExperience;
  ensureExperience = function () {
    if (!model.list && !model.reading && !model.error) void reload();
    for (const [key, action] of [
      ["memory", "memoryGet"],
      ["saves", "savesList"],
      ["servers", "serverList"],
    ])
      if (
        !state.actual[key] &&
        !state.experienceReads[key] &&
        !state.experienceErrors[key]
      )
        experienceRead(key, action);
  };
  const previousHome = home;
  home = function () {
    const container = document.createElement("div");
    container.innerHTML = previousHome();
    const row =
      model.selected ||
      model.list?.configurations.find((r) => r.id === selectedId());
    container.querySelector(".launch-config").innerHTML =
      `<label for="launch-configuration-select">启动配置</label><select id="launch-configuration-select" ${!model.list ? "disabled" : ""}>${(model.list?.configurations || [{ id: "launcher", name: "启动器配置", kind: "builtin" }]).map((r) => `<option value="${esc(r.id)}" ${r.id === selectedId() ? "selected" : ""}>${esc(r.name)} · ${kind(r)}${r.version ? " · " + esc(r.version) : ""}${r.available === false ? " · 版本不可用" : ""}</option>`).join("")}</select><p data-configuration-description>${esc(description(row))}</p>${row?.available === false ? '<p class="status-warning">' + esc(row.message) + "</p>" : ""}${model.error ? '<p class="status-warning">' + esc(model.error) + "</p>" : ""}<div class="actions"><button data-configuration-create>新建配置</button><button data-configuration-edit ${!row ? "disabled" : ""}>编辑配置</button><button data-configuration-reload>重新读取</button>${row?.id === "steam" ? "<button data-configuration-steam>接管 Steam 配置</button><button data-force-takeover>强制接管</button>" : ""}</div>`;
    const summary = container.querySelector("[data-current-config]"),
      cover = model.cover,
      currentStatus =
        state.status?.configurationId === selectedId()
          ? state.status
          : cover?.status,
      memory = state.actual.memory,
      components = currentStatus?.components?.filter((c) => c.configured) || [];
    summary.textContent = cover
      ? `${memory ? memory.currentGiB + " GiB Java 堆" : "内存待读取"} · ${components.length} 个组件 · ${row?.version || cover.status.gameVersion?.value || "版本未读取"}`
      : "正在读取所选配置…";
    container.querySelector(".current-config h2").textContent = "下次启动配置";
    const detail = container.querySelector(".current-config details");
    if (detail)
      detail.innerHTML =
        "<summary>配置详情</summary><p>" +
        esc(components.map((c) => c.title).join(" · ") || "未启用组件") +
        "</p>";
    const launch = container.querySelector("#launch");
    if (!row?.available || !cover || model.selecting) launch.disabled = true;
    container.insertAdjacentHTML(
      "beforeend",
      '<div class="configuration-jobs" data-configuration-jobs>' +
        jobsMarkup() +
        "</div>",
    );
    if (
      cover?.status.runtimeConfigurationName &&
      (cover.status.running || cover.status.startup?.pending) &&
      cover.status.runtimeConfigurationId !== row?.id
    )
      container.insertAdjacentHTML(
        "beforeend",
        '<p class="small" data-configuration-runtime>正在运行：' +
          esc(cover.status.runtimeConfigurationName) +
          " · 下次启动：" +
          esc(row?.name) +
          "</p>",
      );
    if (
      row?.id === "steam" &&
      (libraryUi.model.forceResult || libraryUi.model.forceError)
    )
      container.insertAdjacentHTML(
        "beforeend",
        '<section class="card section" data-configuration-steam-result><h3>Steam 配置接管结果</h3><p>' +
          esc(
            libraryUi.model.forceResult?.message || libraryUi.model.forceError,
          ) +
          "</p></section>",
      );
    return container.innerHTML;
  };
  function jobsMarkup() {
    return [...model.jobs.values()]
      .map(
        (job) =>
          `<section class="card section" data-configuration-job-owner="${esc(job.id)}"><h3>${esc(job.name)} · ${job.active ? "正在处理" : "操作结果"}</h3><p role="status">${esc(job.message)}</p>${job.active ? '<progress aria-label="' + esc(job.name) + '操作进度"></progress><button data-configuration-job-cancel="' + esc(job.id) + '">取消此配置任务</button>' : '<button data-configuration-job-dismiss="' + esc(job.id) + '">关闭结果</button>'}</section>`,
      )
      .join("");
  }
  function paintJobs() {
    const root = document.querySelector("[data-configuration-jobs]");
    if (root) root.innerHTML = jobsMarkup();
  }
  actualCover = function () {
    const row = model.selected,
      cover = model.cover,
      currentStatus =
        state.status?.configurationId === selectedId()
          ? state.status
          : cover?.status,
      memory = state.actual.memory,
      components = currentStatus?.components?.filter((c) => c.configured) || [];
    return `<aside class="scene-status" data-home-actual aria-label="所选启动配置摘要"><h3>${esc(row?.name || "启动配置")}</h3><dl><dt>游戏版本</dt><dd>${esc(row?.version || cover?.status.gameVersion?.value || "待读取")}</dd><dt>启动内存</dt><dd>${cover ? esc(cover.memory.currentGiB) + " GiB" : "待读取"}</dd><dt>下次启动启用组件</dt><dd>${cover ? components.length + " 项" : "待读取"}</dd></dl><p class="configured-names">${esc(components.map((c) => c.title).join(" · ") || (cover ? "未启用组件" : "正在读取"))}</p></aside>`;
  };
  function editorStatus(editor, text, busy = false) {
    if (model.editor !== editor) return;
    editor.message = text;
    const root = editor.element,
      feedback = root.querySelector("[data-configuration-editor-status]");
    feedback.textContent = text;
    root.querySelector("[data-configuration-progress]").hidden = !busy;
    root.querySelector("[data-configuration-cancel]").hidden = !busy;
    root
      .querySelectorAll(
        "[data-configuration-write],[data-configuration-up],[data-configuration-down],[data-configuration-remove],[data-configuration-add]",
      )
      .forEach((b) => {
        if (busy) {
          if (!b.disabled) {
            b.dataset.configurationLock = "1";
            b.disabled = true;
          }
        } else if (b.dataset.configurationLock) {
          delete b.dataset.configurationLock;
          b.disabled = false;
        }
      });
  }
  const frozenPayload = (editor) => ({
    snapshotId: editor.id.slice(9),
    manifestHash: editor.view.frozen.manifestHash,
  });
  function meta(editor, id) {
    return (
      [...(editor.catalog || []), ...(editor.sources || [])].find(
        (r) => r.id === id,
      ) || { id, name: id }
    );
  }
  function paintMods() {
    const editor = model.editor;
    if (!editor) return;
    const root = editor.element,
      query = editor.search.toLowerCase();
    root.querySelector("[data-configuration-mods]").innerHTML =
      editor.ids
        .filter((id) =>
          `${meta(editor, id).name} ${id}`.toLowerCase().includes(query),
        )
        .map((id) => {
          const row = meta(editor, id),
            index = editor.ids.indexOf(id);
          return `<li data-configuration-mod="${esc(id)}"><button class="mod-drag-handle" data-mod-drag="${esc(id)}" data-mod-drag-kind="configuration" aria-label="拖动 ${esc(row.name || id)} 调整顺序" ${editor.saving ? "disabled" : ""}>⠿</button><span class="order-number">${index + 1}.</span><div class="ordered-info"><div><h3>${esc(row.name || id)}</h3><span class="small">${esc(id)}</span>${row.workshopId ? `<button data-mod-workshop="${esc(row.workshopId)}">工坊原页 ↗</button>` : ""}</div></div><div class="actions"><button data-configuration-up="${esc(id)}" ${index === 0 || editor.saving ? "disabled" : ""} aria-label="上移 ${esc(row.name || id)}">↑</button><button data-configuration-down="${esc(id)}" ${index === editor.ids.length - 1 || editor.saving ? "disabled" : ""} aria-label="下移 ${esc(row.name || id)}">↓</button><button data-configuration-remove="${esc(id)}" ${editor.saving ? "disabled" : ""}>移除</button></div></li>`;
        })
        .join("") || "<li>没有符合搜索条件的模组。</li>";
    root.querySelector("[data-configuration-available]").innerHTML =
      (editor.sources || editor.catalog || [])
        .filter(
          (r) =>
            !editor.ids.includes(r.id) &&
            !editor.view.frozen?.managedIds.includes(r.id) &&
            !r.componentManaged &&
            !r.component,
        )
        .filter((r) => `${r.name} ${r.id}`.toLowerCase().includes(query))
        .map(
          (r, index) =>
            `<div class="configuration-source"><span>${esc(r.name || r.id)} <small>${esc({ user: "本机模组", workshop: "工坊", local: "本机模组" }[r.sourceKind || r.source] || "当前来源")}${r.workshopId ? " · 工坊 " + esc(r.workshopId) : ""}</small></span><button data-configuration-add="${index}" data-source-key="${esc(r.token || r.id)}" ${editor.saving ? "disabled" : ""}>${editor.frozen ? "复制到快照" : "添加"}</button></div>`,
        )
        .join("") || '<p class="small">没有可添加的匹配项。</p>';
  }
  async function readComponents(editor, epoch = editor.epoch) {
    const [inventory, approvals, status] = await Promise.all([
      ownerRequest(editor.id, "javaComponentsList", {}, editor),
      ownerRequest(editor.id, "javaList", {}, editor),
      ownerRequest(editor.id, "status", {}, editor),
    ]);
    if (model.editor !== editor || epoch !== editor.epoch) return;
    editor.inventory = inventory;
    editor.approvals = approvals;
    editor.status = status;
    paintComponents(editor);
  }
  function paintComponents(editor) {
    if (model.editor !== editor) return;
    const rows = editor.inventory?.rows || [],
      approvals = editor.approvals?.jars || [],
      root = editor.element.querySelector("[data-configuration-components]");
    const disclosure = root.querySelector(
        "[data-known-component-sources]",
      )?.open,
      workshop = root.querySelector("[data-configuration-workshop]")?.value;
    root.innerHTML = `<p class="small">${editor.frozen ? "停用保留副本；移除会删除最后一个未被引用的副本。" : "组件更改立即保存到此配置；从此配置移除会保留原工坊文件。"}模组草稿单独保存。</p><div class="configuration-component-grid">${
      rows
        .map((r, index) => {
          const approval = r.enabled
            ? approvals.find((j) => j.id === r.modId)
            : null;
          return `<article class="card" data-configuration-component="${esc(r.modId)}" data-configuration-source-token="${esc(r.token)}"><h3>${esc((editor.frozen ? meta(editor, r.modId).name : r.name) || r.name || r.modId)}</h3><p class="small">${esc(r.workshopId ? "工坊 " + r.workshopId : r.sourceLabel || (editor.frozen ? "快照副本" : "本地来源"))}</p><p>${r.enabled ? "已启用" : "未启用"}${r.jarState === "missing" ? " · 文件缺失" : ""}${r.jarState === "ready" ? " · Java " + (approval?.decision === true ? "已允许" : approval?.decision === false ? "已拒绝" : "待授权") : ""}</p><div class="actions"><button data-configuration-component-toggle="${index}" data-configuration-write>${r.enabled ? "停用" : "启用"}</button><button data-configuration-component-remove="${index}" data-configuration-write>${editor.frozen ? "移除副本" : "从此配置移除"}</button>${approval ? `<button data-configuration-approve="${esc(approval.approvalToken || approval.sha256)}" data-configuration-decision="${approval.decision !== true}" data-configuration-write>${approval.decision === true ? "拒绝 Java 文件" : "允许此 Java 文件"}</button>` : ""}</div>${approval ? `<details data-configuration-java-source><summary>来源与 Java 文件</summary><p>${editor.frozen ? "此快照中的固定副本" : "此配置所选的当前文件"}</p><p class="small">Java 包 ${esc(r.packageName || "未声明")}</p><p class="key">SHA-256 ${esc(approval.sha256)}</p></details>` : ""}${r.workshopId ? `<button data-mod-workshop="${esc(r.workshopId)}">工坊原页 ↗</button>` : ""}${(r.limitations || []).length ? '<p class="small">' + r.limitations.map(esc).join(" · ") + "</p>" : ""}</article>`;
        })
        .join("") || "<p>没有已读取的组件。</p>"
    }</div><details data-known-component-sources><summary>已知组件与下载</summary><div class="actions">${(
      editor.status?.components || []
    )
      .filter(
        (r) =>
          r.id !== "base" &&
          ["buddy", "viewpoint", ...Object.keys(items)].includes(r.id),
      )
      .map(
        (r) =>
          `<button data-configuration-known="${esc(r.id)}" data-configuration-write>${r.configured ? "重新核对" : "安装 / 启用"} ${esc(r.title)}</button>`,
      )
      .join(
        "",
      )}</div><label>工坊项目 ID<input inputmode="numeric" data-configuration-workshop placeholder="输入工坊项目 ID"></label><button data-configuration-download data-configuration-write>下载并选择组件</button><p class="small">新 Java 文件需要单独授权；下载不会自动允许新文件。</p></details>`;
    if (disclosure !== undefined)
      root.querySelector("[data-known-component-sources]").open = disclosure;
    if (workshop !== undefined)
      root.querySelector("[data-configuration-workshop]").value = workshop;
    if (editor.saving) editorStatus(editor, editor.message, true);
  }
  async function openEditor(id = selectedId(), tab = "mods") {
    if (model.editor) return;
    const element = document.createElement("dialog");
    element.id = "configuration-editor";
    element.className = "configuration-editor";
    const editor = {
      id,
      element,
      epoch: 0,
      reading: true,
      readRequests: new Set(),
      ids: [],
      catalog: [],
      sources: null,
      tokens: new Map(),
      search: "",
      dirty: false,
      saving: false,
      view: null,
    };
    model.editor = editor;
    element.innerHTML =
      '<div class="row"><h2>正在读取配置…</h2><div class="actions"><button data-configuration-editor-reload>重新读取</button><button data-configuration-close>关闭</button></div></div><p data-configuration-editor-status role="status">正在读取此配置…</p><progress data-configuration-progress aria-label="配置操作进度"></progress><button data-configuration-cancel>取消此操作</button><div data-configuration-editor-body></div>';
    document.body.append(element);
    element.addEventListener("cancel", (event) => {
      event.preventDefault();
      void closeEditor();
    });
    element.showModal();
    try {
      const view = await raw("configurationRead", { id });
      if (model.editor !== editor) return;
      editor.view = view;
      editor.frozen = view.kind === "frozen";
      editor.ids = [...view.ordinaryIds];
      const named = view.kind === "live";
      element.querySelector("h2").textContent = view.name + " · " + kind(view);
      element.querySelector("[data-configuration-editor-body]").innerHTML =
        `${named ? `<div class="configuration-fields"><label>配置名称<input data-configuration-name maxlength="100" value="${esc(view.name)}"></label><button data-configuration-rename data-configuration-write>保存名称</button><label>游戏版本<select data-configuration-version>${versionOptions(view.versionId)}</select></label><button data-configuration-set-version data-configuration-write>保存版本</button></div>` : `<p>游戏版本：${esc(view.frozen?.gameVersion || model.selected?.version || view.versionId)}${editor.frozen ? " · 副本版本已固定" : ""}</p>`}${!view.available ? `<p class="status-warning">${esc(view.message)}</p>` : ""}<div class="configuration-tabs"><button data-configuration-tab="mods" aria-pressed="${tab === "mods"}">模组</button><button data-configuration-tab="components" aria-pressed="${tab === "components"}">组件</button></div><section data-configuration-area="mods" ${tab !== "mods" ? "hidden" : ""}><p>${editor.frozen ? "新增模组会保存当前文件；工坊更新不会改变这份快照。" : "使用所选版本和当前模组文件。普通模组清单保存后生效。"}</p><label>搜索模组<input type="search" data-configuration-search placeholder="搜索已选和可添加的模组"></label><ol class="enabled-mods configuration-mods" data-configuration-mods></ol><div class="actions"><button class="primary" data-configuration-save data-configuration-write ${!view.available || id === "steam" || id === "vanilla" ? "disabled" : ""}>保存模组清单</button><button data-configuration-sort data-configuration-write ${!view.available ? "disabled" : ""}>自动排序</button></div><details data-configuration-catalog open><summary>添加当前模组</summary><div data-configuration-available></div></details></section><section data-configuration-area="components" ${tab !== "components" ? "hidden" : ""}><div data-configuration-components>正在读取组件…</div></section>${named ? '<button class="danger" data-configuration-delete data-configuration-write>删除此配置</button>' : ""}`;
      const saveActions = element
        .querySelector("[data-configuration-save]")
        ?.closest(".actions");
      if (saveActions) {
        saveActions.classList.add("configuration-editor-actions");
        saveActions.hidden = tab !== "mods";
        element.append(saveActions);
      }
      if (!view.available) {
        editorStatus(editor, "此版本不可用；仍可修改名称或选择已安装版本。");
        return;
      }
      if (editor.frozen) {
        editor.catalog = view.frozen.copied;
        const sources = await raw("frozenConfigurationSources", {
          ...frozenPayload(editor),
          readId: (editor.sourceReadId = readId()),
        });
        if (model.editor !== editor) return;
        editor.sources = sources.sources;
      } else {
        const catalog = await ownerRequest(id, "modCatalog", {}, editor);
        if (model.editor !== editor) return;
        editor.catalog = catalog.mods || [];
      }
      paintMods();
      if (tab === "components") await readComponents(editor);
      editorStatus(editor, "模组清单尚未修改。组件操作会单独保存。");
    } catch (error) {
      editorStatus(editor, error.message);
    } finally {
      if (model.editor === editor) {
        editor.reading = false;
        element.querySelector("[data-configuration-progress]").hidden = true;
        element.querySelector("[data-configuration-cancel]").hidden = true;
      }
    }
  }
  async function reloadEditor(editor) {
    if (editor.reading || editor.saving) return;
    const epoch = ++editor.epoch;
    editor.reading = true;
    editorStatus(editor, "正在读取此配置，模组草稿会保留…");
    try {
      const view = await raw("configurationRead", { id: editor.id });
      let catalog, sources;
      if (editor.frozen) {
        const result = await raw("frozenConfigurationSources", {
          snapshotId: editor.id.slice(9),
          manifestHash: view.frozen.manifestHash,
          readId: (editor.sourceReadId = readId()),
        });
        catalog = view.frozen.copied;
        sources = result.sources;
      } else {
        catalog = (
          await ownerRequest(editor.id, "modCatalog", { refresh: true }, editor)
        ).mods;
      }
      if (model.editor !== editor || epoch !== editor.epoch) return;
      editor.view = view;
      editor.catalog = catalog;
      editor.sources = sources;
      if (!editor.dirty) editor.ids = [...view.ordinaryIds];
      await readComponents(editor, epoch);
      if (model.editor === editor && epoch === editor.epoch) {
        paintMods();
        editorStatus(editor, "已重新读取，未保存的模组草稿保留。");
      }
    } catch (error) {
      if (model.editor === editor && epoch === editor.epoch)
        editorStatus(editor, error.message + " 模组草稿已保留。");
    } finally {
      if (model.editor === editor && epoch === editor.epoch)
        editor.reading = false;
    }
  }
  async function closeEditor() {
    const editor = model.editor;
    if (!editor) return;
    if (
      !editor.saving &&
      editor.dirty &&
      !(await confirm(
        "放弃未保存的模组清单？",
        "仅放弃模组草稿。已完成的组件更改仍保留。",
      ))
    )
      return;
    model.editor = null;
    for (const token of editor.readRequests)
      void raw("configurationReadCancel", {
        id: editor.id,
        readId: token,
      }).catch(() => {});
    if (editor.reading && editor.sourceReadId)
      void raw("frozenConfigurationReadCancel", {
        snapshotId: editor.id.slice(9),
        readId: editor.sourceReadId,
      }).catch(() => {});
    editor.element.close();
    editor.element.remove();
    if (editor.saving) {
      paintJobs();
      notify(editor.view.name + "的操作继续进行，可在首页查看或取消。");
    }
  }
  async function write(editor, operation, message, options = {}) {
    if (editor.saving) return;
    editor.saving = true;
    editor.epoch++;
    const job = {
      id: editor.id,
      name: editor.view.name,
      active: true,
      message,
      hash: editor.view.frozen?.manifestHash,
    };
    model.jobs.set(editor.id, job);
    paintJobs();
    editorStatus(editor, message, true);
    let completedMessage = "";
    try {
      const result = await operation();
      editor.message = result?.message || "此配置的操作已完成。";
      completedMessage = editor.message;
      if (result?.configuration && editor.frozen)
        editor.view.frozen = result.configuration;
      if (model.editor === editor && options.components !== false) {
        if (editor.frozen) {
          const current = await raw("frozenConfigurationRead", {
            snapshotId: editor.id.slice(9),
          });
          editor.view.frozen = current;
        }
        await readComponents(editor);
      }
      if (editor.id === selectedId() && options.cover !== false)
        await readCover(editor.id, model.epoch);
    } catch (error) {
      editor.message = completedMessage
        ? completedMessage + " 状态读取未完成，请重新读取。"
        : (/^无法原子保存快照元数据/.test(error.message)
            ? "快照配置文件未能写入，文件可能被占用。"
            : error.message) + " 模组草稿已保留，可重试。";
    } finally {
      editor.saving = false;
      job.active = false;
      job.message = editor.message;
      paintJobs();
      if (model.editor === editor) {
        editorStatus(editor, editor.message);
        paintMods();
      }
    }
  }
  async function save(editor) {
    await write(
      editor,
      async () => {
        let result;
        if (editor.frozen) {
          result = await raw("frozenConfigurationSave", {
            ...frozenPayload(editor),
            ordinaryIds: editor.ids,
            componentIds: editor.view.frozen.componentIds,
            sourceTokens: [...editor.tokens.values()],
          });
          editor.view.frozen = result.configuration;
          editor.catalog = result.configuration.copied;
        } else
          result = await raw("configurationSaveOrdinary", {
            id: editor.id,
            ordinaryIds: editor.ids,
          });
        editor.dirty = false;
        editor.tokens.clear();
        return { ...result, message: "模组清单已保存；组件选择独立保留。" };
      },
      editor.frozen
        ? "正在核对并复制所选当前文件…"
        : "正在保存此配置的模组清单…",
      { components: editor.frozen, cover: editor.frozen },
    );
  }
  async function componentAction(editor, row, operation) {
    await write(
      editor,
      async () => {
        if (editor.frozen) {
          let result;
          if (operation === "uninstall")
            result = await raw("frozenConfigurationUninstall", {
              ...frozenPayload(editor),
              componentIds: [row.modId],
            });
          else
            result = await raw("frozenConfigurationSave", {
              ...frozenPayload(editor),
              ordinaryIds: editor.view.frozen.ordinaryIds.filter(
                (id) => operation !== "enable" || id !== row.modId,
              ),
              componentIds:
                operation === "enable"
                  ? [
                      ...new Set([
                        ...editor.view.frozen.componentIds,
                        row.modId,
                      ]),
                    ]
                  : editor.view.frozen.componentIds.filter(
                      (id) => id !== row.modId,
                    ),
              sourceTokens: [],
            });
          editor.view.frozen = result.configuration;
          if (operation === "enable")
            editor.ids = editor.ids.filter((id) => id !== row.modId);
          return {
            ...result,
            message:
              operation === "disable"
                ? "组件已停用，副本保留。"
                : result.message,
          };
        }
        const plan = await ownerRequest(editor.id, "javaComponentsPreview", {
          token: row.token,
          operation,
          stamp: editor.inventory.stamp,
          receiptId: "",
        });
        return ownerRequest(editor.id, "javaComponentsApply", {
          previewStamp: plan.previewStamp,
          confirmed: true,
        });
      },
      "正在保存此配置的组件更改…",
    );
  }
  async function create() {
    if (document.querySelector("#configuration-create")) return;
    const dialog = document.createElement("dialog");
    dialog.id = "configuration-create";
    dialog.className = "configuration-editor";
    dialog.innerHTML = `<h2>新建普通启动配置</h2><label>配置名称<input data-configuration-new-name maxlength="100" autofocus></label><label>游戏版本<select data-configuration-new-version>${versionOptions("steam")}</select></label><p>引用当前游戏与模组文件，不复制完整游戏。</p><p data-create-status role="status"></p><div class="actions"><button data-create-cancel>取消</button><button data-create-confirm class="primary">创建并编辑</button></div>`;
    document.body.append(dialog);
    dialog.addEventListener("close", () => dialog.remove());
    dialog.querySelector("[data-create-cancel]").onclick = () => dialog.close();
    dialog.querySelector("[data-create-confirm]").onclick = async () => {
      const button = dialog.querySelector("[data-create-confirm]");
      button.disabled = true;
      try {
        const row = await raw("configurationCreate", {
          name: dialog.querySelector("input").value,
          versionId: dialog.querySelector("select").value,
        });
        dialog.close();
        await select(row.id);
        model.list = null;
        await reload();
        await openEditor(row.id);
      } catch (error) {
        dialog.querySelector("[data-create-status]").textContent =
          error.message;
      } finally {
        button.disabled = false;
      }
    };
    dialog.showModal();
  }
  document.addEventListener("input", (event) => {
    const editor = model.editor;
    if (!editor || !event.target.closest("#configuration-editor")) return;
    if (event.target.matches("[data-configuration-search]")) {
      editor.search = event.target.value;
      paintMods();
    }
  });
  document.addEventListener("change", (event) => {
    if (event.target.id === "launch-configuration-select")
      void select(event.target.value);
  });
  document.addEventListener("click", async (event) => {
    const button = event.target.closest("button");
    if (!button || button.disabled) return;
    const editor = model.editor;
    try {
      if (button.hasAttribute("data-configuration-create"))
        return void create();
      if (button.hasAttribute("data-configuration-edit"))
        return void openEditor();
      if (button.hasAttribute("data-configuration-components-open"))
        return void openEditor(selectedId(), "components");
      if (button.hasAttribute("data-configuration-reload"))
        return void reload();
      if (button.dataset.configurationJobDismiss) {
        model.jobs.delete(button.dataset.configurationJobDismiss);
        paintJobs();
        return;
      }
      if (button.dataset.configurationJobCancel) {
        const id = button.dataset.configurationJobCancel;
        await raw(
          id.startsWith("snapshot:")
            ? "frozenConfigurationCancel"
            : "configurationCancel",
          id.startsWith("snapshot:") ? { snapshotId: id.slice(9) } : { id },
        );
        return;
      }
      if (button.hasAttribute("data-configuration-steam")) {
        route("components");
        await scanBuddy();
        return;
      }
      if (!editor || !button.closest("#configuration-editor")) return;
      if (button.hasAttribute("data-configuration-editor-reload"))
        return void reloadEditor(editor);
      if (button.hasAttribute("data-configuration-close"))
        return void closeEditor();
      if (button.hasAttribute("data-configuration-cancel")) {
        await raw(
          editor.frozen ? "frozenConfigurationCancel" : "configurationCancel",
          editor.frozen
            ? { snapshotId: editor.id.slice(9) }
            : { id: editor.id },
        );
        return;
      }
      if (button.dataset.configurationTab) {
        editor.element.querySelector(".configuration-editor-actions").hidden =
          button.dataset.configurationTab !== "mods";
        for (const area of editor.element.querySelectorAll(
          "[data-configuration-area]",
        ))
          area.hidden =
            area.dataset.configurationArea !== button.dataset.configurationTab;
        for (const tab of editor.element.querySelectorAll(
          "[data-configuration-tab]",
        ))
          tab.setAttribute("aria-pressed", String(tab === button));
        if (
          button.dataset.configurationTab === "components" &&
          !editor.inventory &&
          !editor.reading
        ) {
          editor.reading = true;
          editorStatus(editor, "正在核对组件来源…");
          try {
            await readComponents(editor);
            if (model.editor === editor)
              editorStatus(editor, "组件更改会立即保存到此配置。");
          } catch (error) {
            if (model.editor === editor) editorStatus(editor, error.message);
          } finally {
            if (model.editor === editor) editor.reading = false;
          }
        }
        return;
      }
      if (button.hasAttribute("data-configuration-save"))
        return void save(editor);
      if (button.hasAttribute("data-configuration-rename"))
        return void write(
          editor,
          async () => {
            const view = await raw("configurationRename", {
              id: editor.id,
              name: editor.element.querySelector("[data-configuration-name]")
                .value,
            });
            editor.view.name = view.name;
            editor.element.querySelector("h2").textContent =
              view.name + " · " + kind(view);
            model.list = null;
            await reload();
            return { message: "配置名称已保存。" };
          },
          "正在保存名称…",
          { components: false, cover: false },
        );
      if (button.hasAttribute("data-configuration-set-version"))
        return void write(
          editor,
          async () => {
            const view = await raw("configurationVersion", {
              id: editor.id,
              versionId: editor.element.querySelector(
                "[data-configuration-version]",
              ).value,
            });
            editor.view = view;
            if (!editor.dirty) editor.ids = [...view.ordinaryIds];
            const catalog = await ownerRequest(
              editor.id,
              "modCatalog",
              {},
              editor,
            );
            editor.catalog = catalog.mods || [];
            editor.element.querySelector("[data-configuration-save]").disabled =
              false;
            editor.element.querySelector("[data-configuration-sort]").disabled =
              false;
            editor.element.querySelector(".status-warning")?.remove();
            model.list = null;
            await reload();
            return {
              message: "游戏版本已保存，已读取该版本的当前模组。",
            };
          },
          "正在保存版本…",
          { cover: false },
        );
      if (button.hasAttribute("data-configuration-delete")) {
        if (
          !(await confirm(
            "删除此启动配置？",
            "仅删除此普通配置的登记；原游戏、工坊和存档保留。",
          ))
        )
          return;
        await raw("configurationDelete", { id: editor.id });
        editor.dirty = false;
        await closeEditor();
        model.list = null;
        await reload();
        return;
      }
      if (button.hasAttribute("data-configuration-sort"))
        return void write(
          editor,
          async () => {
            const sorted = await ownerRequest(editor.id, "modSort", {
              mods: editor.ids,
            });
            const valid =
              !(sorted.errors || []).length &&
              Array.isArray(sorted.mods) &&
              sorted.mods.length === editor.ids.length &&
              editor.ids.every((id) => sorted.mods.includes(id));
            if (
              valid &&
              sorted.mods.some((id, index) => id !== editor.ids[index])
            ) {
              editor.ids = [...sorted.mods];
              editor.dirty = true;
            }
            return {
              message:
                (valid ? "排序建议已载入草稿；" : "保留当前模组顺序；") +
                [
                  ...(sorted.warnings || []),
                  ...(sorted.errors || []),
                  ...(sorted.violations || []),
                ].join("；") +
                " 可调整后保存。",
            };
          },
          "正在读取作者排序建议…",
          { components: false, cover: false },
        );
      if (
        button.hasAttribute("data-configuration-component-toggle") ||
        button.hasAttribute("data-configuration-component-remove")
      ) {
        const row =
            editor.inventory.rows[
              Number(
                button.dataset.configurationComponentToggle ??
                  button.dataset.configurationComponentRemove,
              )
            ],
          operation = button.hasAttribute("data-configuration-component-remove")
            ? "uninstall"
            : row.enabled
              ? "disable"
              : "enable";
        if (
          operation === "uninstall" &&
          !(await confirm(
            editor.frozen ? "移除这个组件副本？" : "从此配置移除组件？",
            editor.frozen
              ? "最后一个未被引用的副本将直接删除；被其他模组依赖的文件仍保留。"
              : "原工坊文件保留，其他配置不受影响。",
          ))
        )
          return;
        return void componentAction(editor, row, operation);
      }
      if (button.dataset.configurationApprove)
        return void write(
          editor,
          async () => {
            const decision = button.dataset.configurationDecision === "true";
            if (editor.frozen) {
              const result = await raw("frozenConfigurationApprove", {
                ...frozenPayload(editor),
                approvalToken: button.dataset.configurationApprove,
                decision,
              });
              editor.view.frozen = result.configuration;
              return { ...result, message: "此副本的 Java 决定已保存。" };
            }
            await ownerRequest(editor.id, "javaSave", {
              stamp: editor.approvals.stamp,
              hash: button.dataset.configurationApprove,
              decision,
            });
            return { message: "此来源的当前 Java 文件决定已保存。" };
          },
          "正在保存 Java 文件授权…",
        );
      if (button.hasAttribute("data-configuration-known"))
        return void write(
          editor,
          () =>
            ownerRequest(editor.id, "componentsAction", {
              component: button.dataset.configurationKnown,
              operation: "install",
              confirmed: true,
            }),
          "正在核对、下载并选择组件…",
        );
      if (button.hasAttribute("data-configuration-download"))
        return void write(
          editor,
          () =>
            ownerRequest(editor.id, "javaComponentsInstall", {
              id: editor.element.querySelector("[data-configuration-workshop]")
                .value,
              confirmed: true,
            }),
          "正在下载工坊组件…",
        );
      if (editor.saving) return;
      const up = button.dataset.configurationUp,
        down = button.dataset.configurationDown,
        remove = button.dataset.configurationRemove;
      if (up || down) {
        const index = editor.ids.indexOf(up || down),
          other = index + (up ? -1 : 1);
        [editor.ids[index], editor.ids[other]] = [
          editor.ids[other],
          editor.ids[index],
        ];
        editor.dirty = true;
        paintMods();
      } else if (remove) {
        editor.ids = editor.ids.filter((id) => id !== remove);
        editor.tokens.delete(remove);
        editor.dirty = true;
        paintMods();
      } else if (button.hasAttribute("data-configuration-add")) {
        const key = button.dataset.sourceKey,
          row = (editor.sources || editor.catalog).find(
            (r) => (r.token || r.id) === key,
          );
        if (!row) return;
        editor.ids.push(row.id);
        if (
          row.token &&
          !editor.view.frozen?.copied.some((c) => c.id === row.id)
        )
          editor.tokens.set(row.id, row.token);
        editor.dirty = true;
        paintMods();
      }
    } catch (error) {
      if (editor) editorStatus(editor, error.message);
      else notify(error.message);
    }
  });
  function progressLabel(progress) {
    const count =
      progress.completedFiles > 0 || progress.totalFiles > 0
        ? "文件 " +
          progress.completedFiles +
          (progress.totalFiles > 0 ? " / " + progress.totalFiles : "")
        : "";
    const size =
      progress.completedBytes > 0 || progress.totalBytes > 0
        ? formatBytes(progress.completedBytes) +
          (progress.totalBytes > 0
            ? " / " + formatBytes(progress.totalBytes)
            : "")
        : "";
    return [progress.text, count, size].filter(Boolean).join(" · ");
  }
  function formatBytes(value) {
    return value >= 1048576
      ? (value / 1048576).toFixed(1) + " MiB"
      : value >= 1024
        ? (value / 1024).toFixed(1) + " KiB"
        : value + " B";
  }
  document.addEventListener("ozl-configuration-progress", (event) => {
    const editor = model.editor,
      data = event.detail;
    const progress = data.progress || data;
    if (data.readId) {
      if (
        editor?.reading &&
        (editor.readRequests.has(data.readId) ||
          editor.sourceReadId === data.readId)
      )
        editorStatus(editor, progressLabel(progress), true);
      return;
    }
    const ownedJob = model.jobs.get(data.configurationId);
    if (
      ownedJob?.hash &&
      data.manifestHash &&
      ownedJob.hash !== data.manifestHash
    )
      return;
    const job = model.jobs.get(data.configurationId);
    if (job?.active) {
      job.message = progressLabel(progress) || job.message;
      paintJobs();
    }
    if (!editor || data.configurationId !== editor.id || !editor.saving) return;
    editorStatus(editor, progressLabel(progress) || "正在处理此配置…", true);
    const bar = editor.element.querySelector("[data-configuration-progress]");
    if (progress.value >= 0) {
      bar.max = 100;
      bar.value = progress.value;
    } else bar.removeAttribute("value");
  });
  const previousDrafts = hasUnsavedDrafts;
  hasUnsavedDrafts = (options) =>
    previousDrafts(options) ||
    !!model.editor?.dirty ||
    [...model.jobs.values()].some(
      (job) => job.active && job.id === selectedId(),
    );
  const previousComponents = components;
  components = function () {
    return (
      heading(
        '<button data-configuration-components-open class="primary">编辑此配置的组件</button>',
      ) +
      `<section class="card"><h2>${esc(model.selected?.name || "启动配置")}</h2><p>${esc(description(model.selected))}</p><p>组件启用与 Java 授权属于此启动配置。普通模组清单在同一个编辑器中单独保存。</p><button data-configuration-components-open>管理组件与授权</button>${selectedId() === "steam" ? "<button data-buddy-scan>扫描 Steam 配置</button><button data-force-takeover>强制接管</button>" + buddyPanel() : ""}</section>`
    );
  };
  return {
    model,
    reload,
    select,
    openEditor,
    ownerRequest,
    paintMods,
    editor: () => model.editor,
    dragIds: () => model.editor?.ids || [],
    dragCommit(ids) {
      if (!model.editor) return;
      model.editor.ids = ids;
      model.editor.dirty = true;
    },
  };
})();

const modOrderingUi=(()=>{
 let drag=null;
 const reduced=()=>matchMedia('(prefers-reduced-motion: reduce)').matches;
 const locked=()=>state.busy||state.coreApplying||state.modSorting||state.loading.modProfile||editorSaves.has('modProfileSave')||editorSaves.has('saveSave');
 const ids=kind=>kind==='configuration'?launchConfigurationUi.dragIds():kind==='profile'?(state.modProfile?state.modProfileMods:ordinaryModIds(state.modProfiles?.activeMods)):state.saveMods;
 const owner=kind=>kind==='configuration'?launchConfigurationUi.editor():kind==='profile'?state.modProfile:state.saveDetail;
 const rowId=row=>row.dataset.configurationMod||row.dataset.modRow||row.dataset.enabledId||row.dataset.profileModId;
 const selector=kind=>kind==='configuration'?'[data-configuration-mods]':kind==='profile'?'.mod-workspace-list':'.save-editor .enabled-mods';
 const focusSelector=t=>`[data-mod-drag-kind="${t.kind}"][data-mod-drag="${CSS.escape(t.id)}"]`;
 function valid(t){if(t.kind==='configuration')return !launchConfigurationUi.editor()?.saving&&t.owner===owner(t.kind)&&JSON.stringify(ids(t.kind))===t.initial;return !locked()&&t.context===modWorkspaceKey()&&t.route===state.route&&t.owner===owner(t.kind)&&t.readSequence===(t.kind==='profile'?state.modProfileReadSequence:state.saveReadSequence)&&JSON.stringify(ids(t.kind))===t.initial&&(t.kind!=='save'||state.saveDetail?.editable!==false);}
 function merged(t){const visible=new Set(t.visible),order=[...t.order];return t.ids.map(id=>visible.has(id)?order.shift():id);}
 function clean(t){if(drag!==t)return;drag=null;cancelAnimationFrame(t.frame);for(const animation of t.animations)animation.cancel();t.row.classList.remove('is-drag-placeholder');t.list.classList.remove('is-mod-dragging');document.body.classList.remove('mod-ordering-active');t.handle.removeAttribute('aria-pressed');if(t.handle.hasPointerCapture(t.pointerId))t.handle.releasePointerCapture(t.pointerId);t.ghost?.remove();}
 function finish(t,paint=true){if(drag!==t)return;const scroll=t.list.scrollTop;clean(t);if(paint){if(t.kind==='configuration')launchConfigurationUi.paintMods();else render();const list=document.querySelector(selector(t.kind));if(list)list.scrollTop=scroll;document.querySelector(focusSelector(t))?.focus({preventScroll:true});}}
 function cancel(paint=true){if(drag)finish(drag,paint);}
 function start(t){t.started=true;const rect=t.row.getBoundingClientRect(),ghost=t.row.cloneNode(true);t.originTop=rect.top;t.originLeft=rect.left;t.startY=t.y;t.ghost=ghost;for(const element of [ghost,...ghost.querySelectorAll('*')]){element.removeAttribute('id');for(const attribute of [...element.attributes])if(attribute.name.startsWith('data-'))element.removeAttribute(attribute.name);if(element.matches('button,input,select')){element.disabled=true;element.tabIndex=-1;}}ghost.classList.add('mod-drag-ghost');ghost.setAttribute('aria-hidden','true');Object.assign(ghost.style,{top:rect.top+'px',left:rect.left+'px',width:rect.width+'px'});document.body.append(ghost);t.row.classList.add('is-drag-placeholder');t.list.classList.add('is-mod-dragging');document.body.classList.add('mod-ordering-active');t.handle.setAttribute('aria-pressed','true');t.frame=requestAnimationFrame(()=>scrollFrame(t));}
 function place(t){if(drag!==t||!t.started||t.released)return;const others=t.order.filter(id=>id!==t.id),position=others.filter(id=>{const rect=t.rows.get(id).getBoundingClientRect();return t.y>rect.top+rect.height/2;}).length,next=[...others];next.splice(position,0,t.id);if(next.join('\u0000')===t.order.join('\u0000'))return;const children=[...t.list.children],before=new Map(children.map(row=>[row,row.getBoundingClientRect().top]));for(const animation of t.animations)animation.cancel();t.animations=[];t.order=next;let index=0;const reordered=children.map(row=>t.rows.has(rowId(row))?t.rows.get(next[index++]):row);t.list.append(...reordered);if(!t.released)t.handle.setPointerCapture(t.pointerId);const draft=merged(t),ghostNumber=t.ghost.querySelector('.mod-order,.order-number');if(ghostNumber)ghostNumber.textContent=(draft.indexOf(t.id)+1)+(ghostNumber.classList.contains('order-number')?'.':'');for(const row of reordered){const number=row.querySelector('.mod-order,.order-number');if(number&&draft.includes(rowId(row)))number.textContent=(draft.indexOf(rowId(row))+1)+(number.classList.contains('order-number')?'.':'');if(row===t.row||reduced())continue;const offset=before.get(row)-row.getBoundingClientRect().top;if(Math.abs(offset)>.5)t.animations.push(row.animate([{transform:`translateY(${offset}px)`},{transform:'translateY(0)'}],{duration:170,easing:'cubic-bezier(.2,.75,.25,1)'}));}}
 function move(t,x,y){if(!valid(t)){cancel();return;}t.x=x;t.y=y;if(!t.started&&Math.hypot(x-t.downX,y-t.downY)<5)return;if(!t.started)start(t);t.ghost.style.transform=`translateY(${y-t.startY}px)`;place(t);}
 function scrollFrame(t){if(drag!==t||t.released||!t.started)return;const rect=t.list.getBoundingClientRect(),top=Math.max(0,rect.top),bottom=Math.min(innerHeight,rect.bottom),edge=Math.min(44,(bottom-top)/3),speed=t.y<top+edge?-Math.min(15,(top+edge-t.y)*.38):t.y>bottom-edge?Math.min(15,(t.y-bottom+edge)*.38):0;if(speed){const old=t.list.scrollTop;t.list.scrollTop+=speed;if(old!==t.list.scrollTop)place(t);}t.frame=requestAnimationFrame(()=>scrollFrame(t));}
 function drop(t){if(drag!==t)return;if(!valid(t)){cancel();return;}t.released=true;cancelAnimationFrame(t.frame);if(t.handle.hasPointerCapture(t.pointerId))t.handle.releasePointerCapture(t.pointerId);if(!t.started){finish(t,false);return;}const next=merged(t);if(JSON.stringify(next)!==t.initial){if(t.kind==='configuration'){launchConfigurationUi.dragCommit(next);}else if(t.kind==='profile'){ensureModDraft();state.modProfileMods=next;state.modProfileDirty=true;state.modSortReport=null;}else{state.saveMods=next;state.saveDirty=true;}state.sortPreview=null;}t.committed=true;if(reduced()){finish(t);return;}const target=t.row.getBoundingClientRect().top-t.originTop,animation=t.ghost.animate([{transform:t.ghost.style.transform,opacity:1},{transform:`translateY(${target}px)`,opacity:.55}],{duration:180,easing:'cubic-bezier(.2,.75,.25,1)',fill:'forwards'});t.animations.push(animation);animation.finished.then(()=>finish(t)).catch(()=>{});}
 function controls(){for(const handle of document.querySelectorAll('[data-mod-drag]')){const kind=handle.dataset.modDragKind,id=handle.dataset.modDrag;handle.disabled=kind==='configuration'?!!launchConfigurationUi.editor()?.saving||!ids(kind).includes(id):!!locked()||!ids(kind).includes(id)||!!modInfo(id).componentManaged||modSourceConflict(id)||kind==='save'&&state.saveDetail?.editable===false;}if(drag&&!drag.committed&&!valid(drag))cancel();}
 document.addEventListener('pointerdown',event=>{const handle=event.target.closest('[data-mod-drag]');if(!handle||handle.disabled||event.button!==0||locked()||drag)return;const kind=handle.dataset.modDragKind,list=handle.closest(selector(kind));if(!list)return;const selected=[...list.children].filter(row=>{const h=row.querySelector('[data-mod-drag]');return h&&!h.disabled;});const currentIds=[...ids(kind)],visible=selected.map(rowId);if(!visible.includes(handle.dataset.modDrag))return;event.preventDefault();handle.focus({preventScroll:true});drag={kind,id:handle.dataset.modDrag,list,row:handle.closest('li'),handle,route:state.route,owner:owner(kind),context:modWorkspaceKey(),readSequence:kind==='profile'?state.modProfileReadSequence:state.saveReadSequence,ids:currentIds,initial:JSON.stringify(currentIds),visible,order:[...visible],rows:new Map(selected.map(row=>[rowId(row),row])),pointerId:event.pointerId,downX:event.clientX,downY:event.clientY,x:event.clientX,y:event.clientY,started:false,released:false,animations:[]};handle.setPointerCapture(event.pointerId);},true);
 document.addEventListener('pointermove',event=>{if(!drag||drag.pointerId!==event.pointerId||drag.released)return;event.preventDefault();move(drag,event.clientX,event.clientY);},true);
 document.addEventListener('pointerup',event=>{if(drag?.pointerId===event.pointerId&&!drag.released){event.preventDefault();drop(drag);}},true);
 document.addEventListener('pointercancel',event=>{if(drag?.pointerId===event.pointerId)cancel();},true);
 document.addEventListener('lostpointercapture',event=>{if(drag?.pointerId===event.pointerId&&!drag.released&&!drag.handle.hasPointerCapture(event.pointerId))cancel();},true);
 document.addEventListener('keydown',event=>{if(event.key==='Escape'&&drag){event.preventDefault();event.stopImmediatePropagation();cancel();}},true);
 for(const name of ['input','change'])document.addEventListener(name,event=>{if(drag&&event.target.closest('#page'))cancel(false);},true);
 window.addEventListener('blur',()=>cancel());
 const previousRender=render;render=function(){if(drag?.kind!=='configuration')cancel(false);previousRender();controls();};
 const previousDisable=disable;disable=function(){previousDisable();controls();};
 const previousRoute=route;route=function(name){if(name!==state.route)cancel(false);previousRoute(name);};
 const links=new Set();document.addEventListener('click',async event=>{const button=event.target.closest('[data-mod-workshop]');if(!button||button.disabled)return;event.preventDefault();event.stopImmediatePropagation();const id=button.dataset.modWorkshop;if(links.has(id))return;links.add(id);button.disabled=true;const feedback=button.parentElement.querySelector('[data-mod-workshop-feedback]'),context=modWorkspaceKey();try{await request('modOpenWorkshop',{workshopId:id});if(button.isConnected&&context===modWorkspaceKey()&&feedback)feedback.textContent='已打开工坊原页。';}catch(error){if(button.isConnected&&context===modWorkspaceKey()&&feedback)feedback.textContent=error.message;}finally{links.delete(id);if(button.isConnected)button.disabled=false;}},true);
 return {};
})();
const snapshotUi=(()=>{
 const model={tab:'versions',list:null,selected:'',revision:0,identity:'',reading:null,operation:null,dialog:null,checks:{},checking:new Map(),error:'',message:'',needsRead:true};
 titles.versions='版本管理 / 存档快照';
 for(const action of ['environmentCreateDefault','environmentBind','environmentUnbind'])mutationActions.add(action);
 const identity=()=>JSON.stringify([state.status?.gamePath||'',state.experienceEpoch||0]);
 const active=()=>state.route==='versions'&&model.tab==='snapshots';
 const bytes=value=>{const n=Number(value);return !Number.isFinite(n)||n<0?'—':n>=1073741824?(n/1073741824).toFixed(1)+' GiB':n>=1048576?(n/1048576).toFixed(1)+' MiB':Math.round(n).toLocaleString()+' B';};
 const locked=()=>!!model.operation||state.busy||state.coreApplying||state.status?.running||launchPending();
 const current=token=>token.identity===identity()&&token.revision===model.revision&&token.saveId===model.selected;
 const saveName=()=>model.list?.saves?.find(save=>save.id===model.selected)?.name||model.list?.selectedSaveName||'此存档';
 function closeDialog(cancelTask=false){
  const ticket=model.dialog;if(!ticket)return;model.dialog=null;clearInterval(ticket.timer);
  if(ticket.reading){ticket.reading=false;void request('environmentPreviewCancel',{jobId:ticket.jobId}).catch(()=>{});}
  if(cancelTask&&model.operation?.dialog===ticket)void cancelOperation();if(ticket.element.open)ticket.element.close();ticket.element.remove();
 }
 function sync(){const value=identity();if(value===model.identity)return false;closeDialog(true);model.identity=value;model.revision++;model.list=null;model.selected='';model.reading=null;model.checks={};model.checking.clear();model.error='';model.message='';model.needsRead=true;return true;}
 function tabs(){return `<div class="environment-tabs" role="tablist" aria-label="版本与存档快照"><button role="tab" data-environment-tab="versions" aria-selected="${model.tab==='versions'}" ${model.tab==='versions'?'class="is-selected"':''}>游戏版本</button><button role="tab" data-environment-tab="snapshots" aria-selected="${model.tab==='snapshots'}" ${model.tab==='snapshots'?'class="is-selected"':''}>存档快照</button></div>`;}
 function row(snapshot){const checked=model.checks[snapshot.id],using=snapshot.id===model.list?.activeSnapshotId;return `<article class="card environment-card ${using?'is-active':''}" data-environment-card="${esc(snapshot.id)}"><div class="environment-card-main"><div class="row"><h3>${esc(snapshot.name||'游戏与模组快照')}</h3>${using?'<span class="badge status-positive">当前使用</span>':'<span class="badge">历史快照</span>'}</div><dl class="environment-facts"><div><dt>游戏版本</dt><dd>Build ${esc(snapshot.gameVersion||'未读取')}</dd></div><div><dt>启用模组</dt><dd>${esc(snapshot.modCount??'—')} 个</dd></div><div><dt>副本大小</dt><dd>${esc(bytes(snapshot.bytes))}</dd></div><div><dt>创建时间</dt><dd><time datetime="${esc(snapshot.createdUtc||'')}">${esc(localDate(snapshot.createdUtc))}</time></dd></div></dl><details class="environment-location"><summary>快照位置</summary><p>${esc(snapshot.rootPath||'未读取')}</p></details>${checked?`<div class="environment-check-result" role="status"><p>${esc(checked.message|| (checked.changed?'当前原文件已有变化。':'当前原文件没有变化。'))}</p>${checked.gameChanged||checked.modsChanged?`<p class="small">${[checked.gameChanged?'游戏原文件已更新':'',checked.modsChanged?'模组原文件已更新':''].filter(Boolean).join(' · ')}</p>`:''}${checked.changed?`<button data-environment-update="${esc(snapshot.id)}" ${locked()?'disabled':''}>创建更新快照</button>`:''}</div>`:''}</div><div class="actions environment-card-actions">${using?`<button class="primary" data-environment-continue ${locked()?'disabled':''}>继续此存档</button>`:`<button data-environment-bind="${esc(snapshot.id)}" ${locked()?'disabled':''}>使用这个快照</button>`}<button data-environment-check="${esc(snapshot.id)}" ${model.checking.has(snapshot.id)||state.busy?'disabled':''}>${model.checking.has(snapshot.id)?'正在检查…':'检查原文件更新'}</button></div></article>`;}
 function workspace(){const list=model.list,saves=list?.saves||[],matching=list?.selectedSaveId===model.selected,rows=matching?(list.snapshots||[]).filter(snapshot=>snapshot.saveId===model.selected):[],using=matching?rows.find(snapshot=>snapshot.id===list.activeSnapshotId):null;return `<div class="environment-workspace" data-environment-root><section class="card environment-intro"><div><span class="eyebrow">游戏与模组快照</span><h2>为这次生存，留住原来的版本</h2><p>保存此存档使用的游戏版本和模组原文件，以后从这套副本继续游戏。</p><p class="small">存档进度仍写回原存档；备份与恢复单独管理。</p></div><div class="environment-save-choice"><label for="environment-save-select">选择存档</label><select id="environment-save-select" data-environment-save ${!saves.length||model.operation?'disabled':''}>${saves.length?saves.map(save=>`<option value="${esc(save.id)}" ${save.id===model.selected?'selected':''}>${esc(save.name)}</option>`).join(''):'<option value="">'+(model.reading?'正在读取存档…':'暂无单人存档')+'</option>'}</select><button class="primary" data-environment-create ${!model.selected||locked()?'disabled':''}>创建快照</button></div></section>${model.error?`<p class="error-text environment-message" role="alert">${esc(model.error)}</p>`:''}${model.message?`<p class="library-feedback environment-message" role="status">${esc(model.message)}</p>`:''}${(list?.warnings||[]).map(warning=>`<p class="small environment-message">${esc(warning)}</p>`).join('')}<section class="environment-history"><div class="row environment-history-heading"><div><h2>${esc(model.selected?saveName():'此存档')}的快照</h2><p class="small">${using?'当前使用：'+esc(using.name)+' · Build '+esc(using.gameVersion):'当前使用所选游戏版本和现有模组。'}${rows.length?' · '+rows.length+' 个历史副本':''}</p></div><div class="actions">${using?`<button data-environment-unbind ${locked()?'disabled':''}>使用当前环境</button>`:''}<button data-environment-refresh ${model.operation?'disabled':''}>${model.reading?'正在读取…':'刷新'}</button></div></div>${model.reading&&!matching?'<p class="read-pending" role="status">正在读取此存档的快照…</p>':rows.length?`<div class="environment-cards">${rows.map(row).join('')}</div>`:`<div class="card environment-empty"><h3>${model.reading?'正在读取快照…':'还没有游戏与模组快照'}</h3><p>${model.selected?'创建后，可以在这里选择用于继续此存档的副本。':'先选择一个单人存档。'}</p></div>`}</section></div>`;}
 function page(){sync();if(model.tab==='snapshots')return heading()+tabs()+workspace();const template=document.createElement('template');template.innerHTML=libraryUi.versionPage();template.content.querySelector('.heading')?.insertAdjacentHTML('afterend',tabs());return template.innerHTML;}
 function focusRecord(root){const element=document.activeElement;if(!root?.contains(element))return null;const key=element.id?'#'+CSS.escape(element.id):[...element.attributes].find(attribute=>attribute.name.startsWith('data-environment-'));return key?{selector:typeof key==='string'?key:'['+key.name+'="'+CSS.escape(key.value)+'"]'}:null;}
 function paint(){if(!active())return;const root=$('[data-environment-root]');if(!root)return;const focus=focusRecord(root),main=$('main'),scroll=main.scrollTop,template=document.createElement('template');template.innerHTML=workspace();root.replaceWith(template.content.firstElementChild);if(focus)$('#page')?.querySelector(focus.selector)?.focus({preventScroll:true});main.scrollTop=scroll;lockControls();}
 function readListValue(value,saveId){if(!value||typeof value.stamp!=='string'||!Array.isArray(value.saves)||!Array.isArray(value.snapshots)||value.saves.some(save=>typeof save.id!=='string'||typeof save.name!=='string')||value.snapshots.some(snapshot=>typeof snapshot.id!=='string'||typeof snapshot.saveId!=='string')||saveId&&value.selectedSaveId!==saveId)throw new Error('存档快照列表已变化，请刷新后重试。');return value;}
 // Cancellation may arrive after a native commit. Retire older reads and reconcile the saved binding.
 function invalidateListReads(){model.reading=null;model.needsRead=false;}
 function reconcileList(token){if(!current(token))return;invalidateListReads();void readList(token.saveId,true);}
 async function readList(saveId=model.selected,preserveError=false){sync();const token={identity:identity(),revision:model.revision,saveId};model.reading=token;if(!preserveError)model.error='';model.needsRead=false;paint();try{const value=readListValue(await request('environmentList',saveId?{saveId}:{}),saveId);if(model.reading!==token||token.identity!==identity()||token.revision!==model.revision)return;model.list=value;model.selected=value.selectedSaveId||'';if(model.selected&&!value.saves.some(save=>save.id===model.selected))throw new Error('所选存档已不可用，请重新读取列表。');}catch(error){if(model.reading===token&&token.identity===identity()&&token.revision===model.revision)model.error=error.message;}finally{if(model.reading===token){model.reading=null;paint();}}}
 function chooseSave(saveId){if(model.operation||saveId===model.selected||!model.list?.saves.some(save=>save.id===saveId))return;closeDialog();model.revision++;model.selected=saveId;model.error='';model.message='';void readList(saveId);}
 function storageValue(value){if(typeof value?.directory!=='string'||!value.directory.trim())throw new Error('未能读取快照保存位置，请点击“更改”重新选择。');return value.directory;}
 function paintStorage(ticket){if(model.dialog!==ticket)return;const dialog=ticket.element,path=dialog.querySelector('[data-environment-directory]'),change=dialog.querySelector('[data-environment-storage-change]');path.textContent=ticket.directory|| (ticket.storageReading?'正在读取保存位置…':'尚未选择保存位置');path.title=ticket.directory||'';change.textContent=ticket.storageChoosing?'正在选择…':'更改';dialog.querySelector('[data-environment-storage-error]').textContent=ticket.storageError||'';lockControls();}
 async function readStorage(ticket){const revision=++ticket.storageRevision;ticket.storageReading=true;paintStorage(ticket);try{const value=await request('environmentStorageGet');if(model.dialog!==ticket||!current(ticket)||ticket.storageRevision!==revision)return;ticket.directory=storageValue(value);ticket.storageError='';}catch(error){if(model.dialog===ticket&&ticket.storageRevision===revision)ticket.storageError=error.message;}finally{if(model.dialog===ticket&&ticket.storageRevision===revision){ticket.storageReading=false;paintStorage(ticket);}}}
 async function changeStorage(){const ticket=model.dialog;if(!ticket||model.operation||ticket.storageChoosing)return;const revision=++ticket.storageRevision;ticket.storageChoosing=true;ticket.storageReading=false;ticket.storageError='';paintStorage(ticket);try{const value=await request('environmentStorageChoose');if(model.dialog!==ticket||!current(ticket)||ticket.storageRevision!==revision)return;if(!value?.cancelled)ticket.directory=storageValue(value);}catch(error){if(model.dialog===ticket&&ticket.storageRevision===revision)ticket.storageError=error.message;}finally{if(model.dialog===ticket&&ticket.storageRevision===revision){ticket.storageChoosing=false;paintStorage(ticket);if(!ticket.directory&&!ticket.storageError)void readStorage(ticket);}}}
 function workshopId(value){value=value.trim();if(!/^\d+$/.test(value)){try{const url=new URL(/^steamcommunity\.com\//i.test(value)?'https://'+value:value);if(!['http:','https:'].includes(url.protocol)||!['steamcommunity.com','www.steamcommunity.com'].includes(url.hostname)||!/^\/(sharedfiles|workshop)\/filedetails\/?$/i.test(url.pathname)||url.searchParams.getAll('id').length!==1)return null;value=url.searchParams.get('id');}catch{return null;}}value=value.replace(/^0+(?=\d)/,'');return /^[1-9]\d{0,19}$/.test(value)&&BigInt(value)<=18446744073709551615n?value:null;}
 function modChoiceState(ticket){const result={choices:Object.create(null),replaced:0,skipped:0,automatic:0,blocked:0,ready:false},preview=ticket?.preview;if(!preview)return result;const selected=Object.fromEntries([...ticket.element.querySelectorAll('[data-environment-mod-choice]')].map(select=>[select.dataset.environmentModChoice,select.value]));for(const mod of preview.missing||[]){const value=selected[mod.modId];if(value==='skip'){result.choices[mod.modId]='';result.skipped++;}else if(mod.replacementId&&mod.replacementId!==mod.modId&&value==='replace:'+mod.replacementId){result.choices[mod.modId]=mod.replacementId;result.replaced++;}else if(mod.canDownload||mod.canSearch)result.automatic++;else result.blocked++;}result.ready=!!(preview.ready||preview.canPrepare||(preview.missing?.length&&result.replaced+result.skipped>0&&result.blocked===0));return result;}
 function modChoiceSummary(preview,choice){if(preview.inventoryAvailable===false)return '未能完成核对，请检查下方文件提示后重试。';if(choice.replaced+choice.skipped){const actions=[];if(choice.replaced)actions.push('使用新版 '+choice.replaced+' 项');if(choice.skipped)actions.push('跳过 '+choice.skipped+' 项');if(choice.automatic)actions.push('自动补齐 '+choice.automatic+' 项');return '核对完成，'+actions.join('，')+'。'+(choice.blocked?'还有 '+choice.blocked+' 项需要选择处理方式。':'可以创建快照。');}return preview.ready?'核对完成，可以创建快照。':preview.canPrepare?'核对完成，创建时将自动补齐 '+(preview.missing?.length||0)+' 项模组。':choice.blocked?'核对完成，请为 '+choice.blocked+' 项选择处理方式。':'核对完成，请先处理下方提示。';}
 function previewMarkup(value){
  const measured=value.inventoryAvailable!==false,missing=value.missing||[],warnings=(value.warnings||[]).filter(warning=>!missing.some(mod=>warning.includes(mod.modId)));
  return `<p class="environment-preview-summary" data-environment-preview-summary><span data-environment-preview-summary-text>核对完成。</span>${!value.ready?'<button type="button" data-environment-preview-retry>重新核对</button>':''}</p><dl class="environment-review-facts"><div><dt>游戏版本</dt><dd>Build ${esc(value.gameVersion||'未读取')}</dd></div><div><dt data-environment-mod-count-label>启用模组</dt><dd>${measured?esc(value.modCount)+' 个':'未统计'}</dd></div><div><dt>游戏文件</dt><dd>${measured?esc(bytes(value.gameBytes)):'未统计'}</dd></div><div><dt>模组文件</dt><dd>${measured?esc(bytes(value.modBytes)):'未统计'}</dd></div><div class="environment-space-total"><dt>${missing.length?'当前文件大小':'预计占用'}</dt><dd>${measured?esc(bytes(Number(value.gameBytes)+Number(value.modBytes))):'未统计'}</dd></div></dl>${missing.length?`<section class="environment-missing"><h3>缺失模组的处理方式</h3><p class="small">这些选择只用于本次快照，原存档的模组清单不变。</p><ul>${missing.map(mod=>`<li><strong>${esc(mod.name||mod.modId)}</strong><span>${esc(mod.canSearch?'保持原模组时，创建时搜索 Steam 工坊并补齐':mod.canDownload?'保持原模组时，自动从 Steam 工坊下载':mod.reason||'未找到对应的工坊项目')}</span><label class="environment-mod-choice">处理方式<select data-environment-mod-choice="${esc(mod.modId)}" aria-label="${esc(mod.name||mod.modId)}的处理方式"><option value="keep">${mod.canDownload||mod.canSearch?'保持原模组，自动补齐':'保持原模组'}</option>${mod.replacementId&&mod.replacementId!==mod.modId?`<option value="replace:${esc(mod.replacementId)}">使用新版：${esc(mod.replacementName||mod.replacementId)} (${esc(mod.replacementId)})</option>`:''}<option value="skip">跳过这项继续</option></select></label></li>`).join('')}</ul></section>`:''}${missing.some(mod=>mod.canSearch)?`<details class="environment-workshop-sources" data-environment-workshop-sources><summary>指定工坊来源（选填）</summary><p class="small">通常会自动查找。若未找到或有多个结果，可填写对应模组的 Steam 工坊链接或数字项目 ID 后重试。</p>${missing.filter(mod=>mod.canSearch).map(mod=>`<label>${esc(mod.name||mod.modId)}${mod.name&&mod.name!==mod.modId?` <span class="small">${esc(mod.modId)}</span>`:''}<input type="text" data-environment-workshop-source="${esc(mod.modId)}" placeholder="Steam 工坊链接或数字项目 ID" maxlength="2048" autocomplete="off" spellcheck="false"></label>`).join('')}</details>`:''}${warnings.map(warning=>`<p class="small">${esc(warning)}</p>`).join('')}`;
 }
 async function openCreate(snapshotName='',workshopDraft={},choiceDraft={}){
  sync();if(locked()||!model.selected||model.dialog)return;
  const token={identity:identity(),revision:model.revision,saveId:model.selected,preview:null,reading:true,jobId:crypto.randomUUID().replaceAll('-',''),startedAt:Date.now(),lastProgress:{},directory:'',storageRevision:0,storageReading:false,storageChoosing:false,storageError:''},dialog=document.createElement('dialog');token.element=dialog;model.dialog=token;
  dialog.id='environment-create-dialog';dialog.className='environment-dialog';dialog.setAttribute('aria-labelledby','environment-create-title');
  dialog.innerHTML=`<div class="row"><div><span class="eyebrow">${esc(saveName())}</span><h2 id="environment-create-title">创建游戏与模组快照</h2></div><button type="button" data-environment-dialog-close data-safe aria-label="关闭快照预览">返回</button></div><p>保存当前游戏版本和此存档使用的模组文件。</p><label class="environment-name-label" for="environment-snapshot-name">快照名称<input id="environment-snapshot-name" maxlength="100" value="${esc(snapshotName||saveName()+' · 游戏与模组')}"></label><section class="environment-storage" aria-label="快照保存位置"><div><span>保存位置</span><p data-environment-directory>正在读取保存位置…</p></div><button type="button" data-environment-storage-change data-safe>更改</button><p data-environment-storage-error class="error-text" role="alert"></p></section><div data-environment-progress class="environment-copy-progress"><div class="environment-progress-header"><strong data-environment-progress-text role="status">正在读取存档模组…</strong><span data-environment-elapsed>0 秒</span></div><div data-environment-progress-mode class="environment-progress-mode">读取存档清单 · 总量尚未确定</div><div class="environment-progress-stats"><div><span>本阶段已处理文件</span><strong data-environment-file-count>0</strong></div><div><span>本阶段已处理大小</span><strong data-environment-byte-count>0 B</strong></div></div><div data-environment-activity class="environment-activity">正在读取本机文件，数量和大小会随扫描更新。</div><p data-environment-current-item class="environment-current-item" hidden></p><progress max="100" aria-label="当前阶段的实际完成量" hidden></progress><span data-environment-progress-detail>正在统计文件，完成后显示所需空间。</span><div class="environment-preview-steps" data-environment-preview-steps aria-label="核对步骤"><span data-environment-preview-stage="context">存档</span><span data-environment-preview-stage="mod-catalog">模组来源</span><span data-environment-preview-stage="mod-inventory">模组文件</span><span data-environment-preview-stage="game-inventory">游戏文件</span><span data-environment-preview-stage="metadata-validation">复核</span></div><div class="environment-stage-track" aria-label="创建步骤"><span data-environment-stage="0" class="is-current">核对文件</span><span data-environment-stage="1">补齐模组</span><span data-environment-stage="2">复制快照</span></div><button type="button" data-environment-cancel-task data-safe>取消核对</button></div><div data-environment-review class="environment-review"></div><p data-environment-dialog-error class="error-text" role="alert"></p><div class="actions environment-dialog-actions" hidden><button type="button" class="primary" data-environment-choose disabled>创建快照</button></div>`;
  document.body.append(dialog);dialog.addEventListener('cancel',event=>{event.preventDefault();closeDialog(true);});dialog.addEventListener('close',()=>{if(model.dialog===token)closeDialog(true);dialog.remove();});dialog.showModal();
  token.timer=setInterval(()=>{if(model.dialog===token&&(token.reading||model.operation?.dialog===token))updateProgress({});},1000);
  void readStorage(token);
  try{
   const value=await request('environmentPreview',{saveId:token.saveId,jobId:token.jobId});if(!current(token)||model.dialog!==token||!dialog.open)return;
   if(value?.saveId!==token.saveId||typeof value.stamp!=='string'||typeof value.gameVersion!=='string'||!Number.isFinite(value.modCount)||!Number.isFinite(value.gameBytes)||!Number.isFinite(value.modBytes))throw new Error('未能读取完整快照信息，请重新核对。');
   token.preview=value;token.reading=false;dialog.querySelector('[data-environment-progress]').hidden=true;dialog.querySelector('.environment-dialog-actions').hidden=false;dialog.querySelector('[data-environment-review]').innerHTML=previewMarkup(value);for(const input of dialog.querySelectorAll('[data-environment-workshop-source]'))input.value=workshopDraft[input.dataset.environmentWorkshopSource]||'';for(const select of dialog.querySelectorAll('[data-environment-mod-choice]'))if([...select.options].some(option=>option.value===choiceDraft[select.dataset.environmentModChoice]))select.value=choiceDraft[select.dataset.environmentModChoice];dialog.querySelector('[data-environment-choose]').textContent=!value.ready&&value.canPrepare?'补齐模组并创建':'创建快照';lockControls();
  }catch(error){if(current(token)&&model.dialog===token){token.reading=false;dialog.querySelector('[data-environment-progress]').hidden=true;dialog.querySelector('[data-environment-review]').innerHTML='';dialog.querySelector('[data-environment-dialog-error]').textContent=error.message;lockControls();}}
 }
 async function cancelOperation(){
  if(model.dialog?.reading&&!model.operation){closeDialog();return;}
  const token=model.operation;if(!token||token.cancelRequested||token.dialog?.lastProgress?.stage==='snapshot-cleanup')return;token.cancelRequested=true;updateProgress({text:'正在取消…'});lockControls();try{await request('cancel');}catch(error){if(current(token))model.error=error.message;}
 }
 function updateProgress(data={}){
  const ticket=model.dialog,dialog=ticket?.element;if(!dialog||(!model.operation&&!ticket.reading))return;
  const panel=dialog.querySelector('[data-environment-progress]');panel.hidden=false;
  if(!data.stage&&typeof data.text==='string'){if(data.text.startsWith('正在保存快照文件清单'))data={...data,stage:'manifest-save'};else if(data.text.startsWith('正在清理未完成的快照'))data={...data,stage:'snapshot-cleanup'};else if(data.text.startsWith('正在核对快照文件清单容量')||data.text.startsWith('文件清单容量核对完成'))data={...data,stage:'manifest-capacity'};}
  if(data.stage&&data.stage!==ticket.lastProgress.stage)ticket.lastProgress={stage:data.stage};
  ticket.lastProgress={...ticket.lastProgress,...data};const detail=ticket.lastProgress,savingManifest=detail.stage==='manifest-save',cleaningSnapshot=detail.stage==='snapshot-cleanup',checkingCapacity=detail.stage==='manifest-capacity',unknownAmount=savingManifest||cleaningSnapshot||checkingCapacity;
  if(detail.text)panel.querySelector('[data-environment-progress-text]').textContent=detail.text;
  const progress=panel.querySelector('progress'),activity=panel.querySelector('[data-environment-activity]'),mode=panel.querySelector('[data-environment-progress-mode]');
  let maximum=0,done=0,label='';
  if(!unknownAmount&&detail.totalItems>0){maximum=detail.totalItems;done=Math.min(maximum,Math.max(0,Number(detail.completedItems)||0));label=`已统计 ${done.toLocaleString()} / ${maximum.toLocaleString()} 个模组目录`;}
  else if(!unknownAmount&&detail.totalBytes>0){maximum=detail.totalBytes;done=Math.min(maximum,Math.max(0,Number(detail.completedBytes)||0));label=`当前阶段：${bytes(done)} / ${bytes(maximum)}`;}
  else if(!unknownAmount&&detail.totalFiles>0){maximum=detail.totalFiles;done=Math.min(maximum,Math.max(0,Number(detail.completedFiles)||0));label=`当前阶段：${done.toLocaleString()} / ${maximum.toLocaleString()} 个文件`;}
  else if(!unknownAmount&&typeof detail.value==='number'&&Number.isFinite(detail.value)&&detail.value>=0){maximum=100;done=Math.min(100,detail.value);label='当前阶段正在处理';}
  progress.hidden=!maximum;activity.hidden=!!maximum;
  if(maximum){progress.max=maximum;progress.value=done;mode.textContent=label;}else{progress.removeAttribute('value');mode.textContent=savingManifest?'保存中':cleaningSnapshot?'清理中':checkingCapacity?'核对中':'扫描中 · 总量尚未确定';}
  activity.textContent=savingManifest?'正在保存文件清单，完成后会自动继续。':cleaningSnapshot?'正在清理未完成的快照，清理结束后可重试。':checkingCapacity?'正在核对快照文件清单容量，完成后开始复制。':detail.stage==='metadata-validation'?'正在复核刚才读取的文件清单。':'正在读取本机文件，已发现的数量和大小会持续更新。';
  const stats=panel.querySelector('.environment-progress-stats');stats.hidden=savingManifest||cleaningSnapshot;stats.querySelector('span').textContent=checkingCapacity?'清单条目':'本阶段已处理文件';stats.children[1].hidden=checkingCapacity;
  panel.querySelector('[data-environment-file-count]').textContent=Math.max(0,Number(detail.completedFiles)||0).toLocaleString();
  panel.querySelector('[data-environment-byte-count]').textContent=bytes(Math.max(0,Number(detail.completedBytes)||0));
  const item=panel.querySelector('[data-environment-current-item]');item.hidden=unknownAmount||!detail.currentItem;item.textContent=detail.currentItem||'';item.title=detail.currentItem||'';
  const facts=[];if(detail.completedFiles>0)facts.push(detail.totalFiles>0?`${detail.completedFiles.toLocaleString()} / ${detail.totalFiles.toLocaleString()} 个文件`:`已统计 ${detail.completedFiles.toLocaleString()} 个文件`);if(detail.completedBytes>0)facts.push(detail.totalBytes>0?`${bytes(detail.completedBytes)} / ${bytes(detail.totalBytes)}`:bytes(detail.completedBytes));
  const progressDetail=panel.querySelector('[data-environment-progress-detail]');progressDetail.hidden=unknownAmount;progressDetail.textContent=unknownAmount?'':facts.join(' · ')||'正在读取文件…';panel.querySelector('[data-environment-elapsed]').textContent=Math.max(0,Math.floor((Date.now()-ticket.startedAt)/1000))+' 秒';
  const previewSteps=panel.querySelector('[data-environment-preview-steps]'),createSteps=panel.querySelector('.environment-stage-track');previewSteps.hidden=!ticket.reading;createSteps.hidden=ticket.reading;
  const phases=['context','mod-catalog','mod-inventory','game-inventory','metadata-validation'],phase=detail.stage==='preview-complete'?phases.length:phases.indexOf(detail.stage);
  for(const step of previewSteps.children){const index=phases.indexOf(step.dataset.environmentPreviewStage);step.classList.toggle('is-current',index===phase);step.classList.toggle('is-done',index<phase);}
  const stage=checkingCapacity?0:/download|dependency|补齐|下载/.test(detail.stage||detail.text||'')?1:/copy|复制|verify-copy|manifest-save|snapshot-cleanup|complete|完成/.test(detail.stage||detail.text||'')?2:0;for(const step of createSteps.children){step.classList.toggle('is-current',Number(step.dataset.environmentStage)===stage);step.classList.toggle('is-done',Number(step.dataset.environmentStage)<stage);}const cancel=panel.querySelector('[data-environment-cancel-task]');cancel.textContent=ticket.reading?'取消核对':'取消任务';cancel.disabled=cleaningSnapshot||!(model.operation||ticket.reading)||!!model.operation?.cancelRequested;
 }
 async function create(){
  const dialogToken=model.dialog,choice=modChoiceState(dialogToken);if(!choice.ready||!current(dialogToken)||locked()||!dialogToken.directory||dialogToken.storageChoosing)return;
  const element=dialogToken.element,name=element.querySelector('#environment-snapshot-name').value.trim(),errorElement=element.querySelector('[data-environment-dialog-error]');
  if(!name){errorElement.textContent='请填写快照名称。';element.querySelector('#environment-snapshot-name').focus();return;}
  const workshopSources=Object.create(null);for(const input of element.querySelectorAll('[data-environment-workshop-source]')){if(Object.prototype.hasOwnProperty.call(choice.choices,input.dataset.environmentWorkshopSource)||!input.value.trim())continue;const id=workshopId(input.value);if(!id){errorElement.textContent=input.dataset.environmentWorkshopSource+'：请输入 Steam 工坊链接或数字项目 ID。';element.querySelector('[data-environment-workshop-sources]').open=true;input.focus();return;}workshopSources[input.dataset.environmentWorkshopSource]=id;}
  const token={identity:identity(),revision:model.revision,saveId:model.selected,dialog:dialogToken,cancelRequested:false};invalidateListReads();model.operation=token;model.error='';errorElement.textContent='';lockControls();
  try{
   const payload={saveId:token.saveId,name,stamp:dialogToken.preview.stamp};if(Object.keys(workshopSources).length)payload.workshopSources=JSON.stringify(workshopSources);if(Object.keys(choice.choices).length)payload.modChoices=JSON.stringify(choice.choices);
   const options={quiet:true,quietErrors:true},promise=run('environmentCreateDefault',payload,options);dialogToken.startedAt=Date.now();dialogToken.lastProgress={};element.querySelector('[data-environment-review]').hidden=true;element.querySelector('.environment-dialog-actions').hidden=true;updateProgress({text:'正在准备创建快照…',value:-1});
   const result=await promise;if(!current(token))return;if(!result)throw new Error(options.error||'快照未创建，请重试。');if(result.cancelled){if(model.dialog===dialogToken)element.querySelector('[data-environment-progress]').hidden=true;return;}model.list=readListValue(result.list,token.saveId);model.message=result.message||'游戏与模组快照已创建';if(model.dialog===dialogToken)closeDialog();paint();
  }catch(error){if(current(token)&&model.dialog===dialogToken){errorElement.textContent=error.message;const sources=element.querySelector('[data-environment-workshop-sources]');if(sources)sources.open=true;}}
  finally{if(model.operation===token)model.operation=null;if(model.dialog===dialogToken){element.querySelector('[data-environment-progress]').hidden=true;element.querySelector('[data-environment-review]').hidden=false;element.querySelector('.environment-dialog-actions').hidden=false;}lockControls();paint();reconcileList(token);}
 }
 async function binding(snapshotId){if(locked()||!model.selected||!model.list)return;const token={identity:identity(),revision:model.revision,saveId:model.selected},action=snapshotId?'environmentBind':'environmentUnbind';if(snapshotId&&!model.list.snapshots.some(snapshot=>snapshot.id===snapshotId&&snapshot.saveId===token.saveId))return;invalidateListReads();model.operation=token;model.error='';paint();try{const options={quiet:true,quietErrors:true},result=await run(action,{saveId:token.saveId,...(snapshotId?{snapshotId}:{}),stamp:model.list.stamp},options);if(!current(token))return;if(!result)throw new Error(options.error||'没有完成选择，请重试。');model.list=readListValue(result.list,token.saveId);model.message=result.message||'已更新此存档使用的环境';}catch(error){if(current(token))model.error=error.message;}finally{if(model.operation===token)model.operation=null;paint();reconcileList(token);}}
 async function check(snapshotId){if(model.checking.has(snapshotId)||state.busy)return;const token={identity:identity(),revision:model.revision,saveId:model.selected};model.checking.set(snapshotId,token);paint();try{const result=await request('environmentCheck',{snapshotId});if(!current(token))return;if(result?.snapshotId!==snapshotId||typeof result.changed!=='boolean'||typeof result.available!=='boolean')throw new Error('快照检查回复已失效，请重试。');model.checks[snapshotId]=result;}catch(error){if(current(token))model.checks[snapshotId]={message:error.message,available:true,changed:false};}finally{if(model.checking.get(snapshotId)===token)model.checking.delete(snapshotId);paint();}}
 function lockControls(){const busy=locked();for(const button of document.querySelectorAll('[data-environment-create],[data-environment-update],[data-environment-bind],[data-environment-unbind],[data-environment-continue]')){button.disabled=busy||!model.selected;}const select=$('[data-environment-save]');if(select)select.disabled=!!model.operation||!model.list?.saves?.length;const ticket=model.dialog,dialog=ticket?.element;if(dialog){const choice=modChoiceState(ticket);dialog.querySelector('[data-environment-choose]').disabled=busy||ticket.reading||!ticket.directory||ticket.storageChoosing||!choice.ready;if(ticket.preview){dialog.querySelector('[data-environment-preview-summary-text]').textContent=modChoiceSummary(ticket.preview,choice);dialog.querySelector('[data-environment-mod-count-label]').textContent=choice.replaced+choice.skipped?'原清单模组':'启用模组';dialog.querySelector('[data-environment-choose]').textContent=choice.replaced+choice.skipped?'按选择创建快照':!ticket.preview.ready&&ticket.preview.canPrepare?'补齐模组并创建':'创建快照';}dialog.querySelector('[data-environment-storage-change]').disabled=!!model.operation||ticket.storageChoosing;for(const input of dialog.querySelectorAll('#environment-snapshot-name,[data-environment-workshop-source],[data-environment-mod-choice]')){input.disabled=!!model.operation||input.hasAttribute('data-environment-workshop-source')&&Object.prototype.hasOwnProperty.call(choice.choices,input.dataset.environmentWorkshopSource);if(input.hasAttribute('data-environment-mod-choice'))input.title=input.selectedOptions[0]?.textContent||'';}dialog.querySelector('[data-environment-cancel-task]').disabled=ticket.lastProgress?.stage==='snapshot-cleanup'||!(model.operation||ticket.reading)||!!model.operation?.cancelRequested;}}
 function decorate(){sync();for(const card of document.querySelectorAll('.save-card')){const id=card.querySelector('[data-save-open]')?.dataset.saveOpen,save=state.saves?.saves.find(save=>save.id===id);if(id&&save?.mode!=='Multiplayer'&&!card.querySelector('[data-environment-open]'))card.querySelector('.actions')?.insertAdjacentHTML('beforeend',`<button data-environment-open="${esc(id)}">游戏与模组快照</button>`);}if(active()&&model.needsRead&&!model.reading)queueMicrotask(()=>{if(active()&&model.needsRead&&!model.reading)void readList();});lockControls();}
 const previousRender=render;render=function(){const focused=focusRecord($('[data-environment-root]'));previousRender();decorate();if(focused&&active())$('#page')?.querySelector(focused.selector)?.focus({preventScroll:true});};
 const previousDisable=disable;disable=function(){previousDisable();lockControls();};
 const previousTask=task;task=function(data){previousTask(data);if(model.operation)updateProgress(data);};
 const previousStatus=acceptStatus;acceptStatus=function(data){previousStatus(data);if(sync()&&active()){paint();void readList();}lockControls();};
 const previousRoute=route;route=function(name){if(name!==state.route&&state.route==='versions'){closeDialog(true);model.revision++;model.reading=null;model.needsRead=true;}previousRoute(name);};
 document.addEventListener('ozl-environment-progress',event=>{const value=event.detail,ticket=model.dialog;if(!ticket||!value||value.saveId!==ticket.saveId)return;if(ticket.reading&&value.jobId===ticket.jobId||model.operation?.dialog===ticket&&value.jobId==null)updateProgress(value);});
 document.addEventListener('change',event=>{if(event.target.matches('[data-environment-save]'))chooseSave(event.target.value);else if(event.target.matches('[data-environment-mod-choice]')){if(model.dialog)model.dialog.element.querySelector('[data-environment-dialog-error]').textContent='';lockControls();}});
 document.addEventListener('click',event=>{const button=event.target.closest('button');if(!button||button.disabled)return;let handled=true;if(button.dataset.environmentTab){if(model.tab!==button.dataset.environmentTab){closeDialog(true);model.revision++;model.reading=null;model.tab=button.dataset.environmentTab;if(active())model.needsRead=true;render();}}else if(button.dataset.environmentOpen){model.tab='snapshots';model.selected=button.dataset.environmentOpen;model.revision++;model.needsRead=true;route('versions');}else if(button.hasAttribute('data-environment-refresh'))void readList();else if(button.hasAttribute('data-environment-create')||button.dataset.environmentUpdate)void openCreate();else if(button.dataset.environmentBind)void binding(button.dataset.environmentBind);else if(button.hasAttribute('data-environment-unbind'))void binding('');else if(button.dataset.environmentCheck)void check(button.dataset.environmentCheck);else if(button.hasAttribute('data-environment-continue'))void launchExperience('save',model.selected);else if(button.hasAttribute('data-environment-storage-change'))void changeStorage();else if(button.hasAttribute('data-environment-preview-retry')){const element=model.dialog?.element,name=element?.querySelector('#environment-snapshot-name')?.value||'',sources=Object.fromEntries([...element.querySelectorAll('[data-environment-workshop-source]')].map(input=>[input.dataset.environmentWorkshopSource,input.value])),choices=Object.fromEntries([...element.querySelectorAll('[data-environment-mod-choice]')].map(select=>[select.dataset.environmentModChoice,select.value]));closeDialog();void openCreate(name,sources,choices);}else if(button.hasAttribute('data-environment-choose'))void create();else if(button.hasAttribute('data-environment-dialog-close'))closeDialog(true);else if(button.hasAttribute('data-environment-cancel-task'))void cancelOperation();else handled=false;if(handled){event.preventDefault();event.stopImmediatePropagation();}},true);
 return {page};
})();
/* Inline in app.js. Mount once after window.orange exists; ready waits for initial status/version reads. */
(()=>{
 'use strict';
 const chapters=[
  {page:'home',title:'启动',intro:'从这里继续你的生存，或和朋友进入同一台服务器。',steps:['先在首页“启动配置”选择启动器、Steam、原版，或自己的普通 / 快照配置；尚未找到游戏时，点击顶部“选择游戏目录”。','在启动目标中选择“继续单人存档”或“连接服务器”，再选具体目标。','点击“编辑配置”调整版本、组件和普通模组顺序，再从首页启动。普通配置使用当前文件；快照使用固定副本。']},
  {page:'components',title:'模组与组件',intro:'组件与普通模组清单分别保存，属于首页选中的启动配置。',steps:['在“组件管理”按需安装 Viewpoint、汉化或其他组件；需要 Java 授权时，按提示核对文件。','在首页“编辑配置 → 模组”整理清单和顺序，明确保存后生效；组件在同一编辑器的“组件”中单独管理。','自动排序会检查已知依赖与作者声明的冲突；普通模组可打开工坊原页，查看作者说明。']},
  {page:'saves',title:'存档',intro:'查看单人生存进度，管理各存档使用的模组。',steps:['找到存档，查看名称、预览与已启用模组，再选择继续游戏。','需要改模组时先读取该存档的清单，调整后保存。','需要保留存档进度时，打开“备份与恢复”手动备份，也可单独设置自动备份。']},
  {page:'servers',title:'服务器',intro:'找服、收藏地址，再用对应的游戏版本连接。',steps:['从“推荐服务器”了解服务器，也可在“个人列表”添加朋友的地址。','查看服务器状态和人数，核对版本与工坊清单。','选中目标后连接；常用地址可保存到个人列表，也能从“游戏收藏”导入。']},
  {page:'versions',title:'版本管理',intro:'给长期游玩的存档留下完整游戏与已启用模组的固定副本。',steps:['进入“版本管理 → 存档快照”，选择单人存档，点击“创建快照”。','核对游戏版本、模组数量和磁盘占用，确认记住的保存位置，也可更改。快照复制游戏与模组原文件，保留加载顺序。','以后继续该存档会自动使用绑定快照；首页也能选择同一快照进行编辑。新增模组复制当时的当前文件，工坊更新不会改变已保存的副本。'],note:'原存档持续记录你的游玩进度。存档备份单独在“存档 → 备份与恢复”选择。'},
  {page:'memory',title:'内存',intro:'为游戏分配合适的内存，给系统和其他程序留下余量。',steps:['先查看当前分配、可用内存和推荐值。','根据模组数量和电脑内存调整数值，点击保存。','下次启动游戏时使用新设置；出现内存不足时，可结合日志再调整。']},
  {page:'settings',title:'设置',intro:'调整 Viewpoint 的画面、视角快捷键和光影。',steps:['在“画面设置”找到要修改的参数或快捷键，改好后保存。','切到“光影选择”，查看已安装的方案，再选择要使用的光影。','调整完成后从启动页进入游戏，查看实际效果。']},
  {page:'diagnostics',title:'日志分析',intro:'启动失败、进服异常或游玩报错时，先从日志了解发生了什么。',steps:['点击“分析本机最近日志”，也可导入日志文件、ZIP 或选择日志文件夹。','先看分析摘要、发生阶段和相关模组，再展开具体证据。','按建议排查；需要求助时，可以复制或导出分析结果。']},
  {page:'feedback',title:'反馈',intro:'把发生经过和有用的记录一起交给我们。',steps:['描述你做了什么、出现什么问题，以及希望看到的结果。','按需要选择游戏日志、启动器日志或电脑信息。','先预览将要发送的内容，核对后再提交。']},
  {page:'help',title:'帮助',intro:'忘记操作时随时回来，这份引导也可以重新打开。',steps:['点击“重新查看使用引导”，按页面回顾常用操作。','在帮助中查看 Java 授权、Viewpoint 操作、加载画面和常见问题说明。','需要修复时先查看日志和任务结果，再到“修复”选择对应操作。']}
 ];
 const escape=value=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 let instance;
 function mount(options){
  if(instance)return instance;
  if(!options||typeof options.request!=='function')throw Error('Guide requires native request');
  const model={step:0,read:false,dismissed:false,considered:false,disposed:false,focus:null};let dialog,timer,observer;
  const ready=typeof options.ready==='function'?options.ready:()=>!!window.orange?.state?.status&&!window.orange?.state?.busy;
  function paint(){const chapter=chapters[model.step];dialog.querySelector('[data-guide-progress]').textContent=`${model.step+1} / ${chapters.length}`;dialog.querySelector('[data-guide-content]').innerHTML=`<p class="ozl-guide-eyebrow">认识你的启动器</p><h2 id="ozl-guide-step-title">${escape(chapter.title)}</h2><p class="ozl-guide-intro">${escape(chapter.intro)}</p><ol>${chapter.steps.map(step=>`<li>${escape(step)}</li>`).join('')}</ol>${chapter.note?`<p class="ozl-guide-note">${escape(chapter.note)}</p>`:''}`;dialog.querySelectorAll('[data-guide-step]').forEach((button,index)=>{if(index===model.step)button.setAttribute('aria-current','step');else button.removeAttribute('aria-current');});dialog.querySelector('[data-guide-prev]').disabled=model.step===0;dialog.querySelector('[data-guide-next]').hidden=model.step===chapters.length-1;dialog.querySelector('[data-guide-enter]').classList.toggle('primary',model.step===chapters.length-1);}
  function ensureDialog(){if(dialog)return;dialog=document.createElement('dialog');dialog.id='ozl-guide-dialog';dialog.className='ozl-guide-dialog';dialog.setAttribute('aria-labelledby','ozl-guide-title');dialog.innerHTML=`<div class="ozl-guide-header"><div><span class="ozl-guide-eyebrow">OZL · 欢迎使用</span><h1 id="ozl-guide-title">从这里开始</h1></div><button type="button" data-guide-close aria-label="关闭使用引导">关闭</button></div><div class="ozl-guide-layout"><nav class="ozl-guide-nav" aria-label="使用引导章节">${chapters.map((chapter,index)=>`<button type="button" data-guide-step="${index}" data-guide-page="${chapter.page}" data-guide-number="${String(index+1).padStart(2,'0')}">${escape(chapter.title)}</button>`).join('')}</nav><section class="ozl-guide-content" data-guide-content tabindex="-1" aria-labelledby="ozl-guide-step-title"></section></div><div class="ozl-guide-footer"><span class="ozl-guide-progress" data-guide-progress aria-live="polite"></span><div class="ozl-guide-controls"><button type="button" data-guide-prev>上一步</button><button type="button" data-guide-enter>进入启动器</button><button type="button" data-guide-next class="primary" autofocus>下一步</button></div></div>`;document.body.append(dialog);dialog.addEventListener('click',event=>{const button=event.target.closest('button');if(!button||button.disabled)return;if(button.hasAttribute('data-guide-close')||button.hasAttribute('data-guide-enter')){dialog.close();return;}if(button.hasAttribute('data-guide-step'))model.step=Number(button.dataset.guideStep);else if(button.hasAttribute('data-guide-prev'))model.step--;else if(button.hasAttribute('data-guide-next'))model.step++;else return;paint();dialog.querySelector('[data-guide-content]').scrollTop=0;dialog.querySelector('[data-guide-content]').focus({preventScroll:true});});dialog.addEventListener('cancel',event=>{event.preventDefault();dialog.close();});dialog.addEventListener('close',()=>{if(model.disposed)return;model.dismissed=true;model.considered=true;if(model.focus?.isConnected)model.focus.focus({preventScroll:true});Promise.resolve().then(()=>options.request('guideDismiss',{})).catch(()=>options.notify?.('使用引导已关闭，显示偏好暂未保存；下次打开可能再次显示。'));});}
  function open(){if(model.disposed)return;ensureDialog();if(dialog.open)return;model.considered=true;model.focus=document.activeElement;model.step=0;paint();dialog.showModal();}
  function helpEntry(){if(window.orange?.state?.route!=='help')return;const page=document.querySelector('#page');if(!page||page.querySelector('[data-guide-open]'))return;const block=document.createElement('div');block.className='ozl-guide-help';block.innerHTML='<div><h2>第一次使用 OZL？</h2><p>按页面认识常用功能，随时继续游戏。</p></div><button type="button" data-guide-open data-safe>重新查看使用引导</button>';block.querySelector('button').addEventListener('click',open);const heading=page.querySelector('.heading');if(heading)heading.after(block);else page.prepend(block);}
  function consider(){if(model.disposed)return;helpEntry();if(!model.read||model.considered||model.dismissed)return;let available=false;try{available=ready()===true;}catch{}if(available&&!document.querySelector('dialog[open]')){open();return;}if(!timer)timer=setTimeout(()=>{timer=null;consider();},120);}
  observer=new MutationObserver(consider);observer.observe(document.querySelector('#page')||document.body,{childList:true,subtree:true});helpEntry();
  instance={open,dispose(){model.disposed=true;clearTimeout(timer);observer.disconnect();if(dialog){if(dialog.open)dialog.close();dialog.remove();}document.querySelectorAll('.ozl-guide-help').forEach(element=>element.remove());instance=null;}};
  Promise.resolve().then(()=>options.request('guideGet',{})).then(value=>{if(!value||value.schema!==1||typeof value.dismissed!=='boolean')throw Error('Invalid guide preference');model.dismissed=value.dismissed;model.read=true;consider();}).catch(()=>{model.read=true;model.considered=true;helpEntry();});
  return instance;
 }
 window.ozlGuide={mount,open(){instance?.open();}};
})();
window.orange={state,request,run,acceptStatus,hasUnsavedDrafts,library:libraryUi,launchConfigurationUi,render:()=>render()};
window.ozlGuide.mount({request,ready:()=>!!state.status&&!state.busy&&!state.coreApplying&&!state.status?.task?.active&&!libraryUi.model.reading&&!!(libraryUi.model.versions||libraryUi.model.error),notify});
route(location.hash.slice(1)||new URLSearchParams(location.search).get('page')||'home');refresh();void libraryUi.loadVersions();setInterval(()=>{if(!state.busy)refresh();pollPlaytime();if($('#task-center').open)loadTasks();},15000);
})();
