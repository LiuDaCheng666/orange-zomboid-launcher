"""Build/run an owned JVM boundary fixture against a production-captured snapshot."""
import pathlib,subprocess,sys,hashlib
repo=pathlib.Path(__file__).resolve().parents[1]
def run(args,cwd=None):
 p=subprocess.run([str(x) for x in args],cwd=cwd,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW)
 print(p.stdout.decode('utf-8',errors='replace'));assert p.returncode==0,p.stderr.decode('utf-8',errors='replace')
mode=sys.argv[1];root=pathlib.Path(sys.argv[2])
if mode=='prepare':
 fixtures=root/'fixtures';fixtures.mkdir(parents=True);sources=[p for p in sorted((repo/'tests/java/environment/fixtures').rglob('*.java')) if p.name not in ['ZomboidFileSystem.java','ActiveMods.java','ActiveModsFile.java']]+sorted((repo/'tests/java/live/fixtures').rglob('*.java'))
 original=next(p for p in sources if p.name=='ActiveModsFile.java');sources.remove(original)
 parser=root/'src/zombie/modding/ActiveModsFile.java';parser.parent.mkdir(parents=True)
 parser.write_text(original.read_text().replace('if(line.startsWith("mod="))value.getMods().add(line.substring(4));','{String row=line.trim();if(row.matches("mod\\\\s*=.*"))value.getMods().add(row.substring(row.indexOf(\'=\')+1).trim().replaceAll(",$", ""));}'))
 run(['javac','-encoding','UTF-8','--release','8','-d',fixtures,*sources,parser])
 run(['jar','--create','--file',root/'projectzomboid.jar','-C',fixtures,'.'])
 classes=root/'classes';classes.mkdir();run(['javac','-encoding','UTF-8','--release','8','-cp',fixtures,'-d',classes,repo/'tests/java/frozen-selection/FrozenCapturePersistenceProbe.java'])
else:
 snapshot=pathlib.Path(sys.argv[3]);user=pathlib.Path(sys.argv[4]);expected=sys.argv[5];game=snapshot/'game';manifest=game/'OrangeLauncherRuntime/environment/environment.properties';agent=game/'OrangeLauncherRuntime/environment/OrangeEnvironment.jar'
 run(['java','-Duser.home='+str(snapshot/'runtime/home'),'-Dozl.fixture.user='+str(user),'-Dozl.environment='+str(manifest),'-Dozl.environment.sha256='+hashlib.sha256(manifest.read_bytes()).hexdigest(),'-javaagent:'+str(agent),'-cp',str(root/'classes')+';'+str(game/'projectzomboid.jar'),'ozl.environment.tests.FrozenCapturePersistenceProbe',snapshot,user,expected]+sys.argv[6:],cwd=game)
