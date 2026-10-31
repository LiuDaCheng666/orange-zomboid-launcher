"""Transformed isolated JVM surfaces only; never starts the game or edits real sources."""
import pathlib,tempfile,subprocess,hashlib,base64,sys
repo=pathlib.Path(__file__).resolve().parents[1]
root=pathlib.Path(tempfile.mkdtemp(prefix='ozl-forced-agent-'))
agent=pathlib.Path(sys.argv[1]).resolve()
def run(args,cwd=None):
 p=subprocess.run([str(x) for x in args],cwd=cwd,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
 print(p.stdout.decode('utf-8',errors='replace'));assert p.returncode==0,p.stderr.decode('utf-8',errors='replace')
def put(path,text):
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text,encoding='utf-8',newline='\n');return path
def record(key,*values):return key+''.join('\t'+base64.b64encode(str(value).encode()).decode() for value in values)+'\n'
fixtures=root/'fixtures';fixtures.mkdir();sources=[p for p in sorted((repo/'tests/java/environment/fixtures').rglob('*.java')) if p.name not in ('ZomboidFileSystem.java','ActiveMods.java','ActiveModsFile.java','ChooseGameInfo.java')]+[p for p in sorted((repo/'tests/java/live/fixtures').rglob('*.java')) if p.name!='ZomboidFileSystem.java']
fs=(repo/'tests/java/live/fixtures/zombie/ZomboidFileSystem.java').read_text(encoding='utf-8').replace('public int liveScans;', 'private final java.util.Map<String,Object> modIdToDir=new java.util.HashMap<String,Object>();public int liveScans;')
info=(repo/'tests/java/environment/fixtures/zombie/gameStates/ChooseGameInfo.java').read_text(encoding='utf-8').replace('public final class ChooseGameInfo {','public final class ChooseGameInfo {private static final java.util.HashMap<String,Object> Mods=new java.util.HashMap<String,Object>();private static final java.util.HashSet<String> MissingMods=new java.util.HashSet<String>();')
sources += [put(root/'src/zombie/ZomboidFileSystem.java',fs),put(root/'src/zombie/gameStates/ChooseGameInfo.java',info)]
run(['javac','-encoding','UTF-8','--release','8','-Xlint:-options','-d',fixtures,*sources])
classes=root/'classes';classes.mkdir();run(['javac','-encoding','UTF-8','--release','8','-Xlint:-options','-cp',fixtures,'-d',classes,repo/'tests/java/live/ForcedChineseAgentProbe.java'])
for mode in ('normal','follow-server','force-local'):
 base=root/mode;game=base/'game';game.mkdir(parents=True);user=base/'user';context=base/'context';choices=context/'session';local=user/'mods/ConflictingMod';server=base/'workshop/server'
 run(['jar','--create','--file',game/'projectzomboid.jar','-C',fixtures,'.'])
 put(local/'42/mod.info','id=B42Trans_CN\n');put(server/'42/mod.info','id=B42Trans_CN\n');put(user/'mods/default.txt','original');put(choices/'mods/default.txt','mod=Ordinary');put(choices/'mods/loaded.txt','')
 body='OZLLIVE1\n'+record('game',game)+record('user',user)+record('context',context)+record('choices',choices)+record('vanilla','false')+record('managed','B42Trans_CN')+record('selected','B42Trans_CN',local)+record('component','B42Trans_CN')+record('order','B42Trans_CN')+record('force-chinese','true')
 if mode!='normal':body+=record('server-components',mode)
 manifest=put(choices/'live.manifest',body)
 run(['java','-Duser.home='+str(base),'-Dozl.fixture.user='+str(user),'-Dozl.fixture.workshop='+str(server),'-Dozl.live.manifest='+str(manifest),'-Dozl.live.sha256='+hashlib.sha256(manifest.read_bytes()).hexdigest(),'-javaagent:'+str(agent),'-cp',str(classes)+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.ForcedChineseAgentProbe',local,server,mode],cwd=game)
 assert (user/'mods/default.txt').read_text()=='original','original default changed'
 print('PASS '+mode+' original default preserved')
print('Artifacts: '+str(root))
