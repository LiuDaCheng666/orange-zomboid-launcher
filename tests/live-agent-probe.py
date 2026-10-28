"""Owned files and JVM boundary fixtures only. Does not launch game or change real sources."""
import pathlib,tempfile,subprocess,shutil,hashlib,base64,os
repo=pathlib.Path(__file__).resolve().parents[1]
root=pathlib.Path(tempfile.mkdtemp(prefix='ozl-live-中文-'))
agent=pathlib.Path(__import__('sys').argv[1]).resolve()
def run(argv,cwd=None,expected=0):
 p=subprocess.run([str(x) for x in argv],cwd=cwd,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
 print(p.stdout.decode('utf-8',errors='replace'));assert p.returncode==expected,p.stderr.decode('utf-8',errors='replace')
 if expected==78:assert b'[OZL-LIVE] isolation failure:' in p.stderr
 return p
def write(path,s):path.parent.mkdir(parents=True,exist_ok=True);path.write_text(s,encoding='utf-8',newline='\n');return path
def record(k,*vs):return k+''.join('\t'+base64.b64encode(str(v).encode()).decode() for v in vs)+'\n'
fixtures=root/'fixtures';fixtures.mkdir();run(['javac','-encoding','UTF-8','--release','8','-Xlint:-options','-d',fixtures,*[p for p in sorted((repo/'tests/java/environment/fixtures').rglob('*.java')) if p.name not in ['ZomboidFileSystem.java','ActiveMods.java','ActiveModsFile.java']],*sorted((repo/'tests/java/live/fixtures').rglob('*.java'))])
classes=root/'classes';classes.mkdir();run(['javac','-encoding','UTF-8','--release','8','-Xlint:-options','-cp',fixtures,'-d',classes,repo/'tests/java/live/LiveAgentProbe.java'])
for vanilla,named in [(False,False),(False,True),(True,False)]:
 base=root/(str(vanilla)+'-'+str(named));game=base/'game';user=base/'user';context=base/'context';choices=context/'runtime/session';game.mkdir(parents=True)
 run(['jar','--create','--file',game/'projectzomboid.jar','-C',fixtures,'.'])
 selected=base/'source/selected';lua=base/'source/lua';write(selected/'42/mod.info','id=Viewpoint\n');write(lua/'mod.info','id=1299328280/ToadTraits\n');write(user/'mods/ConflictingMod/common/mod.info','id=Viewpoint\n')
 write(user/'mods/default.txt','original-default');write(choices/'mods/default.txt','mod=1299328280/ToadTraits\nmod=NamedB' if named else 'owned-default');write(choices/'mods/loaded.txt','owned-loaded')
 text='OZLLIVE1\n'+record('game',game)+record('user',user)+record('context',context)+record('choices',choices)+record('vanilla',str(vanilla).lower())+record('managed','Viewpoint')+record('managed','Disabled')
 if not vanilla:text+=record('selected','Viewpoint',selected)
 if named:text+=record('override','true')+record('ordinary','1299328280/ToadTraits')+record('ordinary','NamedB')
 manifest=write(choices/'live.manifest',text)
 run(['java','-Duser.home='+str(base),'-Dozl.fixture.user='+str(user),'-Dozl.fixture.workshop='+str(lua),'-Dozl.live.manifest='+str(manifest),'-Dozl.live.sha256='+hashlib.sha256(manifest.read_bytes()).hexdigest(),'-javaagent:'+str(agent),'-cp',str(classes)+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.LiveAgentProbe',selected,lua,choices,user,str(vanilla).lower(),str(named).lower()],cwd=game)
args=['java','-Dozl.live.manifest='+str(manifest),'-Dozl.live.sha256='+'0'*64,'-javaagent:'+str(agent),'-cp',str(classes)+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.LiveAgentProbe'];run(args,cwd=game,expected=78)
print('PASS live/vanilla owned native file probes + altered manifest rejected; artifacts='+str(root))
