"""Owned frozen JVM phase fixture. No actual game initialization or source writes."""
import pathlib,tempfile,subprocess,hashlib,base64,sys
repo=pathlib.Path(__file__).resolve().parents[1]
root=pathlib.Path(tempfile.mkdtemp(prefix='ozl-frozen-selection-'))
agent=pathlib.Path(sys.argv[1]).resolve()
def run(args,cwd=None):
 p=subprocess.run([str(x) for x in args],cwd=cwd,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
 print(p.stdout.decode('utf-8',errors='replace'));assert p.returncode==0,p.stderr.decode('utf-8',errors='replace')
def write(p,text):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8',newline='\n');return p
def b64(v):return base64.b64encode(str(v).encode()).decode()
fixtures=root/'fixtures';fixtures.mkdir()
run(['javac','-encoding','UTF-8','--release','8','-d',fixtures,*[p for p in sorted((repo/'tests/java/environment/fixtures').rglob('*.java')) if p.name not in ['ZomboidFileSystem.java','ActiveMods.java','ActiveModsFile.java']],*sorted((repo/'tests/java/live/fixtures').rglob('*.java'))])
classes=root/'classes';classes.mkdir();run(['javac','-encoding','UTF-8','--release','8','-cp',fixtures,'-d',classes,repo/'tests/java/frozen-selection/FrozenSelectionProbe.java'])
snapshot=root/'snapshot';game=snapshot/'game';game.mkdir(parents=True);run(['jar','--create','--file',game/'projectzomboid.jar','-C',fixtures,'.'])
user=root/'user';home=snapshot/'runtime/home';home.mkdir(parents=True);mods=snapshot/'runtime/mods'
write(mods/'default.txt','mod=OldMod\nmod=SkippedMod');write(mods/'loaded.txt','');write(user/'Saves/Sandbox/Original/mods.txt','mod=Unavailable\nmod=Disabled')
text='version=OZLENV1\nid=1234567890abcdef1234567890abcdef\n'+''.join(k+'='+b64(v)+'\n' for k,v in [('game',game),('user',user),('home',home),('mods',mods),('bridge',user/'mods/OrangeLauncherSession')])+'roots=\nchoices='+b64('OldMod')+':'+b64('NewMod')+','+b64('SkippedMod')+':\nactiveManaged='+b64('Component')+','+b64('Disabled')+'\nactiveComponents='+b64('Component')+'\nactiveOrder='+b64('Component')+','+b64('NewMod')+'\n'
manifest=write(snapshot/'environment.properties',text)
run(['java','-Duser.home='+str(home),'-Dozl.fixture.user='+str(user),'-Dozl.environment='+str(manifest),'-Dozl.environment.sha256='+hashlib.sha256(manifest.read_bytes()).hexdigest(),'-javaagent:'+str(agent),'-cp',str(classes)+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.FrozenSelectionProbe',user],cwd=game)
print('PASS frozen owned phase fixture; artifacts='+str(root))
