"""Actual Buddy2.3.4 + installed game byte API, tiny main only; no game entry/UI."""
import pathlib,tempfile,subprocess,shutil,hashlib,base64,os,sys,json
repo=pathlib.Path(__file__).resolve().parents[1];agent=pathlib.Path(sys.argv[1]).resolve();root=pathlib.Path(tempfile.mkdtemp(prefix='ozl-frozen-gate-中文-'));game=root/'game';game.mkdir();user=root/'user';user.mkdir();context=root;session=root/'runtime';session.mkdir(parents=True);home=session/'home';home.mkdir()
source_game=pathlib.Path('E:/Steam/steamapps/common/ProjectZomboid');source_buddy=pathlib.Path('E:/Steam/steamapps/workshop/content/108600/3619862853/mods/ZombieBuddy/libs/ZombieBuddy.jar')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
before={str(p):sha(p) for p in [source_game/'projectzomboid.jar',source_buddy,source_game/'jre64/bin/java.exe']}
shutil.copyfile(source_game/'projectzomboid.jar',game/'projectzomboid.jar');buddy=game/'ZombieBuddy.jar';shutil.copyfile(source_buddy,buddy)
def write(p,s):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(s,encoding='utf-8');return p
def record(k,*vs):return k+''.join('\t'+base64.b64encode(str(v).encode()).decode() for v in vs)+'\n'
def run(argv,cwd=None):
 env={k:v for k,v in os.environ.items() if k.upper() not in ['JAVA_TOOL_OPTIONS','_JAVA_OPTIONS','JDK_JAVA_OPTIONS','ZB_VERBOSITY']}
 p=subprocess.run([str(x) for x in argv],cwd=cwd,env=env,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=60);assert p.returncode==0,p.stdout.decode(errors='replace')+p.stderr.decode(errors='replace');return p
classes=root/'classes';classes.mkdir();run(['javac','-encoding','UTF-8','--release','8','-Xlint:-options','-d',classes,repo/'tests/java/live/BuddyGateProbe.java'])
payload_classes=root/'payload-classes';payload_classes.mkdir();marker=write(root/'payload-src/fixture/payload/Marker.java','package fixture.payload; public final class Marker {}');run(['javac','--release','8','-Xlint:-options','-d',payload_classes,marker])
selected=root/'mods/selected';write(selected/'mod.info','id=Selected\n');payload=selected/'payload.jar';run(['jar','--create','--file',payload,'-C',payload_classes,'.']);external=root/'external.jar';shutil.copyfile(payload,external);disabled=selected/'disabled.jar';shutil.copyfile(payload,disabled)
write(session/'mods/default.txt','owned');write(session/'mods/loaded.txt','owned');config=session/'buddy';write(config/'config.json',json.dumps(dict(trusted_authors=[],preload_mods={},auto_fix_mod_order=False,fix_approval_dialog_cursor=False)))
def b64(v):return base64.b64encode(str(v).encode()).decode()
text='version=OZLENV1\nid='+'a'*32+'\n'+''.join(k+'='+b64(v)+'\n' for k,v in dict(game=game,user=user,home=home,mods=session/'mods',bridge=user/'mods/OrangeLauncherSession').items())+'roots='+b64(selected)+'\nactiveManaged='+b64('Selected')+'\nactiveComponents='+b64('Selected')+'\nactiveOrder='+b64('Selected')+'\njavaRules='+':'.join(b64(v) for v in [payload,'fixture.payload',sha(payload),'true'])+','+':'.join(b64(v) for v in [disabled,'fixture.payload',sha(disabled),'false'])+'\n'
manifest=write(game/'OrangeLauncherRuntime/environment/environment.properties',text)
args=[source_game/'jre64/bin/java.exe','-Djava.awt.headless=true','-Duser.home='+str(home),'-Dzb.config_dir='+str(config),'-Dozl.environment='+str(manifest),'-Dozl.environment.sha256='+sha(manifest),'-javaagent:'+str(agent),'-javaagent:'+str(buddy)+'=prop_prefix=zb,policy=deny-new,frontend=console,verbosity=0','-cp',str(classes)+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.BuddyGateProbe',payload,external,sha(payload),disabled]

p=run(args,cwd=game);stdout=p.stdout.decode('utf-8',errors='replace');assert 'PASS actual Buddy' in stdout;assert all(sha(pathlib.Path(p))==h for p,h in before.items())
report=dict(scope='Actual Buddy premain and compiled gate + tiny main; not actual game acceptance',owned=str(root),args=[str(x) for x in args],stdout=stdout,stderr=p.stderr.decode('utf-8',errors='replace'),sourceHashes=before,changed=[])
out=repo/'artifacts/owned-launch/frozen-buddy-gate.json';out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print('PASS actual Buddy gate, 3 source hashes unchanged; report='+str(out))
