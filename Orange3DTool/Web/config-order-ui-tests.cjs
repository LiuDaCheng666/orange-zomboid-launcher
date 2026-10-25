/* Real Web UI with a synthetic native boundary; never touches the installed launcher or game. */
const {setup,route,playwright}=require('./full-ui-tests.cjs');
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const shots=path.resolve(__dirname,'../../artifacts/config-order-127');
fs.mkdirSync(shots,{recursive:true});
const tests=[];function test(name,fn){tests.push({name,fn});}
async function fixture(browser,options={}){
  const result=await setup(browser);
  await result.page.evaluate(options=>{
    const original=chrome.webview.postMessage;
    window.configUI={sorted:false,holdSort:false,heldSort:null,sortError:'',afterSortBlocked:false,running:false,...options};
    const response=(m,data,error)=>({data:{id:m.id,ok:!error,...(error?{error}:{data})}});
    chrome.webview.postMessage=m=>{
      let data,error='';
      if(m.action==='launch'||m.action==='profileLaunch'){
        const blocked=!configUI.sorted||configUI.afterSortBlocked;
        data={canLaunch:!blocked,checkedUtc:'2026-10-16T04:19:00Z',message:blocked?'模组依赖需要处理':'已核对本次启动',items:[{id:'mods',title:'模组依赖',status:blocked?'error':'ok',message:blocked?'当前清单不符合依赖约束':'顺序已核对',route:'mods',details:blocked?(configUI.sorted?['缺少依赖：Extra 需要 MissingMod']:['LaoYuTranslationJava 必须排在 ViewpointTrueWeathers 之后','literal <img src=x onerror=alert(1)> & <script>bad()</script>']):[],canAutoSort:blocked&&!configUI.sorted}]};
        if(configUI.extraAutoSort)data.items.push({id:'memory',title:'内存预算',status:'warning',message:'其他项目不会获得模组排序按钮',route:'memory',details:[],canAutoSort:true});
        if(blocked){sent.push(m);queueMicrotask(()=>reply({data:{id:m.id,ok:false,error:data.message,preflight:data}}));return;}data={message:'启动请求已发送',preflight:data};
      }else if(m.action==='modAutoSort'){
        data={message:'默认清单已自动排序',changed:true,mods:['base','extra']};error=configUI.sortError;
        configUI.sorted=!error;
      }else if(m.action==='modProfileSave'&&configUI.profileSaveError){error=configUI.profileSaveError;
      }else if(m.action==='status'&&configUI.running){data={...orange.state.status,running:true,task:{active:false}};
      }else if(m.action==='javaList'){data={supported:true,stamp:'java-1',jars:[{id:'test.jar',name:'Test Java',path:'C:\\Fixture\\test.jar',sha256:'f'.repeat(64),author:'Fixture author',workshopId:'123',decision:null}],warnings:[]};
      }else if(m.action==='javaSave'){data={supported:true,stamp:'java-2',jars:[{id:'test.jar',name:'Test Java',path:'C:\\Fixture\\test.jar',sha256:'f'.repeat(64),author:'Fixture author',workshopId:'123',decision:m.payload.decision}],warnings:[],message:'Java decision saved'};
      }else if(m.action==='playProfileApply'){data={status:'completed',message:'配置已应用',pendingRecovery:{pending:false,canRecover:false,locations:[],warnings:[]}};
      }else return original(m);
      sent.push(m);const replyValue=response(m,data,error);
      if(m.action==='modAutoSort'&&configUI.holdSort){configUI.heldSort=replyValue;return;}
      queueMicrotask(()=>reply(replyValue));
    };
  },options);
  return result;
}
async function blocked(page){await page.locator('#launch').click();await page.waitForSelector('.preflight-panel');await page.waitForFunction(()=>!orange.state.launchAttempt);}
async function doneSort(page){await page.waitForFunction(()=>sent.some(m=>m.action==='modAutoSort'));await page.waitForFunction(()=>!orange.state.preflightRepairing&&!orange.state.launchAttempt&&!orange.state.pendingMutations);}
async function launchActions(page){return page.evaluate(()=>sent.filter(m=>['launchPreflight','modAutoSort','launch','profileLaunch'].includes(m.action)).map(m=>({action:m.action,payload:m.payload})));}

test('preflight exposes escaped concrete dependency details and only the mods repair',async browser=>{
  const {page,errors}=await fixture(browser,{extraAutoSort:true});try{await blocked(page);
    assert.deepEqual(await page.locator('.preflight-details li').allTextContents(),['LaoYuTranslationJava 必须排在 ViewpointTrueWeathers 之后','literal <img src=x onerror=alert(1)> & <script>bad()</script>']);
    assert.equal(await page.locator('.preflight-panel img,.preflight-panel script').count(),0);
    assert.equal(await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).count(),1);
    await page.screenshot({path:path.join(shots,'preflight-details.png')});assert.deepEqual(errors,[]);
    await page.setViewportSize({width:733,height:507});assert.equal(await page.locator('main').evaluate(el=>el.scrollWidth<=el.clientWidth),true);await page.screenshot({path:path.join(shots,'preflight-details-733.png')});
  }finally{await page.close();}
});
test('explicit repair rechecks readiness and continues the selected save without claiming game success',async browser=>{
  const {page}=await fixture(browser);try{
    await page.locator('#launch-target-kind').selectOption('save');await page.locator('#launch-target-id').selectOption('Sandbox/one');await blocked(page);
    await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).click();await doneSort(page);
    assert.deepEqual(await launchActions(page),[{action:'profileLaunch',payload:{startupId:'0123456789abcdef0123456789abcdef',kind:'save',id:'Sandbox/one'}},{action:'modAutoSort',payload:{confirmed:true}},{action:'profileLaunch',payload:{startupId:'0123456789abcdef0123456789abcdef',kind:'save',id:'Sandbox/one'}}]);
    assert.doesNotMatch(await page.locator('[data-launch-feedback]').innerText(),/100%|已进入|加载成功|本次已提交目标/);assert.doesNotMatch(await page.locator('[data-launch-feedback]').innerText(),/已进入游戏|启动成功/);
    await page.screenshot({path:path.join(shots,'sort-continued.png')});
  }finally{await page.close();}
});
test('failed automatic sort stays visible and never sends a launch request',async browser=>{
  const {page}=await fixture(browser,{sortError:'依赖环：base → extra <svg onload=alert(1)>'});try{
    await blocked(page);await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).click();await doneSort(page);
    assert.match(await page.locator('[data-launch-feedback]').innerText(),/依赖环：base → extra <svg/);
    assert.deepEqual((await launchActions(page)).map(x=>x.action),['launch','modAutoSort']);
    assert.equal(await page.locator('[data-launch-feedback] svg').count(),0);await page.screenshot({path:path.join(shots,'sort-error.png')});
  }finally{await page.close();}
});
test('a remaining genuine dependency error after sorting still prevents launch',async browser=>{
  const {page}=await fixture(browser,{afterSortBlocked:true});try{
    await blocked(page);await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).click();await doneSort(page);
    assert.deepEqual((await launchActions(page)).map(x=>x.action),['launch','modAutoSort','launch']);
    assert.match(await page.locator('.preflight-details').innerText(),/MissingMod/);assert.equal(await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).count(),0);
  }finally{await page.close();}
});
test('a target changed during sorting is preserved and cannot be launched by the old repair',async browser=>{
  const {page}=await fixture(browser,{holdSort:true});try{
    await blocked(page);await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).click();await page.waitForFunction(()=>configUI.heldSort);
    assert.equal(await page.locator('[data-preflight-auto-sort]').isDisabled(),true,'the sort request retains its in-flight lock');
    await page.locator('#launch-target-kind').selectOption('server');await page.locator('#launch-target-id').selectOption('own');await page.evaluate(()=>reply(configUI.heldSort));await doneSort(page);
    assert.deepEqual((await launchActions(page)).map(x=>x.action),['launch','modAutoSort']);
    assert.equal(await page.locator('#launch-target-id').inputValue(),'own');assert.match(await page.locator('[data-launch-feedback]').innerText(),/启动选择已变化/);
  }finally{await page.close();}
});
test('an unapplied named mod draft opened while sorting survives automatic continuation',async browser=>{
  const {page}=await fixture(browser,{holdSort:true});try{
    await blocked(page);await page.getByRole('button',{name:'自动排序并继续启动',exact:true}).click();await page.waitForFunction(()=>configUI.heldSort);
    await route(page,'mods');await page.locator('#mod-workspace-select').selectOption('p1');await page.locator('#mod-profile-name').fill('New draft during repair');
    await page.evaluate(()=>reply(configUI.heldSort));await doneSort(page);
    assert.equal(await page.locator('#mod-profile-name').inputValue(),'New draft during repair');assert.equal(await page.evaluate(()=>orange.state.modProfileDirty),true);
    assert.deepEqual((await launchActions(page)).map(x=>x.action),['launch','modAutoSort','launch']);
  }finally{await page.close();}
});
test('failed named-profile save remains by the buttons and successful retry clears it with a receipt',async browser=>{
  const {page}=await fixture(browser);try{
    await route(page,'mods');await page.locator('#mod-workspace-select').selectOption('p1');await page.locator('#mod-profile-name').fill('Retry this draft');await page.locator('[data-profile-mod-up="base"]').click();
    await page.evaluate(()=>configUI.profileSaveError='游戏、服务器或 Java 仍在占用相关文件，无法保存配置。');await page.locator('[data-mod-profile-save]').click();await page.waitForFunction(()=>orange.state.notice.includes('仍在占用'));
    assert.match(await page.locator('[data-mod-manager] [data-mod-profile-save-feedback][role="alert"]').innerText(),/游戏、服务器或 Java 仍在占用相关文件/);
    assert.equal(await page.locator('#mod-profile-name').inputValue(),'Retry this draft');assert.deepEqual(await page.evaluate(()=>orange.state.modProfileMods),['base','extra']);assert.equal(await page.locator('[data-mod-profile-save]').isEnabled(),true);
    assert.equal(await page.locator('[data-mod-profile-save-feedback][role="alert"]').count(),1);await page.screenshot({path:path.join(shots,'profile-save-error.png')});
    await page.evaluate(()=>configUI.profileSaveError='');await page.locator('[data-mod-profile-save]').click();await page.waitForFunction(()=>!orange.state.modProfileDirty);
    assert.match(await page.locator('[data-mod-profile-save-feedback]').innerText(),/Profile saved|已保存/);assert.equal(await page.locator('[data-mod-profile-save-feedback][role="alert"]').count(),0);await page.screenshot({path:path.join(shots,'profile-save-success.png')});
  }finally{await page.close();}
});
test('running game leaves configuration activation, saving, Java decisions and recovery usable',async browser=>{
  const {page}=await fixture(browser,{running:true});try{
    await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:true,launch:{pending:true,state:'waiting'},components:[{id:'base',title:'Fixture component',item:'123',configured:true,downloaded:true}]}));
    await route(page,'mods');assert.equal(await page.locator('[data-mod-save-activate]').isDisabled(),true,'unchanged current list is already enabled');
    await page.locator('#mod-workspace-select').selectOption('p1');await page.locator('[data-mod-save-activate]').click();await page.waitForFunction(()=>sent.some(m=>m.action==='modProfileActivate'));
    await page.locator('#mod-workspace-select').selectOption('p1');await page.locator('#mod-profile-name').fill('Saved while running');await page.locator('[data-mod-profile-save]').click();await page.waitForFunction(()=>!orange.state.modProfileDirty);
    await route(page,'components');assert.equal(await page.locator('[data-component="base"]').first().isEnabled(),true);
    await route(page,'help');await page.locator('#java-help').evaluate(e=>e.open=true);await page.waitForSelector('[data-java-allow]');assert.equal(await page.locator('[data-java-allow]').isEnabled(),true);
    await page.locator('[data-java-allow]').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>sent.some(m=>m.action==='javaSave'));
    assert.equal(await page.evaluate(()=>sent.find(m=>m.action==='javaSave').payload.decision),true);
    await page.evaluate(()=>{orange.state.playProfiles.pendingRecovery={pending:true,canRecover:true,stamp:'fixture-recovery',locations:['Fixture owned config'],warnings:[]};});await route(page,'home');assert.equal(await page.locator('[data-play-recover]').isEnabled(),true);assert.equal(await page.locator('#launch').isDisabled(),true,'duplicate launch remains blocked');await page.screenshot({path:path.join(shots,'running-config-controls.png')});
  }finally{await page.close();}
});
test('running game still allows confirmed play-profile application',async browser=>{
  const {page}=await fixture(browser,{running:true});try{
    await page.evaluate(()=>{
      const profile={id:'play-fixture',name:'Running profile',targetKind:'normal',targetId:'',mods:['base'],heapGiB:8,shaderId:'',settings:{}};
      orange.state.playProfiles={...orange.state.playProfiles,selectedId:profile.id,selectedProfile:profile,profiles:[profile]};
      orange.state.playPreview={profile,canApply:true,previewStamp:'apply-1',changes:[{kind:'heap',key:'heapGiB',before:6,after:8}],errors:[],warnings:[]};
      orange.acceptStatus({...orange.state.status,running:true});
    });
    assert.equal(await page.locator('[data-play-apply]').isEnabled(),true);await page.locator('[data-play-apply]').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>sent.some(m=>m.action==='playProfileApply'));
    assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='playProfileApply').payload),{id:'play-fixture',previewStamp:'apply-1',confirmed:true});
  }finally{await page.close();}
});
for(const [running,pending] of [[true,false],[false,true],[true,true]])test(`component restore sends confirmed uninstall while running=${running} and launch pending=${pending}`,async browser=>{
  const {page}=await fixture(browser,{running});try{
    await page.evaluate(({running,pending})=>orange.acceptStatus({...orange.state.status,running,launch:{pending,stage:'submitted-server'},components:[{id:'base',title:'Fixture component',configured:true,downloaded:true}]}),{running,pending});
    await route(page,'components');assert.equal(await page.locator('[data-components-restore]').isEnabled(),true);
    await page.locator('[data-components-restore]').click();assert.doesNotMatch(await page.locator('#confirm-body').innerText(),/游戏运行中.*不能执行/);await page.locator('#confirm-yes').click();
    await page.waitForFunction(()=>sent.some(m=>m.action==='componentsAction'));assert.deepEqual(await page.evaluate(()=>sent.find(m=>m.action==='componentsAction').payload),{component:'all',operation:'uninstall',confirmed:true});
  }finally{await page.close();}
});
test('runtime and active session changes during component restore confirmation do not refuse the native action',async browser=>{
  const {page}=await fixture(browser);try{
    await route(page,'components');await page.locator('[data-components-restore]').click();await page.locator('#confirm[open]').waitFor();
    await page.evaluate(()=>orange.acceptStatus({...orange.state.status,running:true,launch:{pending:true,stage:'submitted-server'}}));await page.locator('#confirm-yes').click();await page.waitForFunction(()=>!orange.state.restoreConfirming&&!orange.state.busy);
    assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='componentsAction')),true);
  }finally{await page.close();}
});
test('component restore retains in-flight and pending failed-transaction locks',async browser=>{
  const {page}=await fixture(browser);try{
    await route(page,'components');await page.evaluate(()=>orange.acceptStatus({...orange.state.status,task:{active:true}}));assert.equal(await page.locator('[data-components-restore]').isDisabled(),true);
    await page.evaluate(()=>orange.acceptStatus({...orange.state.status,task:{active:false},buddyRecovery:{pending:true}}));assert.equal(await page.locator('[data-components-restore]').isDisabled(),true);
    await page.evaluate(()=>{orange.state.playProfiles.pendingRecovery={pending:true,canRecover:true,locations:[],warnings:[]};orange.acceptStatus({...orange.state.status,buddyRecovery:{pending:false}});});assert.equal(await page.locator('[data-components-restore]').isDisabled(),true);
    assert.equal(await page.evaluate(()=>sent.some(m=>m.action==='componentsAction')),false);
  }finally{await page.close();}
});
test('profile save keeps its real in-flight editor lock and re-enables the draft after failure',async browser=>{
  const {page}=await fixture(browser);try{
    await route(page,'mods');await page.locator('#mod-workspace-select').selectOption('p1');await page.locator('#mod-profile-name').fill('Pending save');await page.evaluate(()=>window.holdAction='modProfileSave');await page.locator('[data-mod-profile-save]').click();await page.waitForFunction(()=>held.some(x=>x.request.action==='modProfileSave'));
    assert.equal(await page.locator('#mod-profile-name').isDisabled(),true);assert.equal(await page.locator('[data-mod-profile-save]').isDisabled(),true);
    await page.evaluate(()=>{const h=held.find(x=>x.request.action==='modProfileSave');reply({data:{id:h.request.id,ok:false,error:'Actual fixture file lock <img src=x onerror=alert(1)>'}});});await page.waitForFunction(()=>!orange.state.busy);
    assert.equal(await page.locator('#mod-profile-name').isEnabled(),true);assert.equal(await page.locator('[data-mod-profile-save]').isEnabled(),true);assert.match(await page.locator('[data-mod-profile-save-feedback]').innerText(),/Actual fixture file lock <img/);assert.equal(await page.locator('[data-mod-profile-save-feedback] img').count(),0);
  }finally{await page.close();}
});

(async()=>{const browser=await playwright.chromium.launch({channel:process.env.ORANGE_BROWSER_CHANNEL||'msedge',headless:true});let failed=0;try{for(const {name,fn} of tests){try{await fn(browser);console.log('PASS '+name);}catch(e){failed++;console.error('FAIL '+name+'\n'+e.stack);}}}finally{await browser.close();}console.log(`${tests.length-failed}/${tests.length} configuration/order UI test groups passed`);if(failed)process.exitCode=1;})().catch(e=>{console.error(e);process.exitCode=1});
