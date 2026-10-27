"""Read-only source / owned-temp JVM probe, never launches a game main."""
import hashlib, json, os, pathlib, shutil, subprocess, tempfile

repo = pathlib.Path(__file__).resolve().parents[1]
previous = json.loads((repo / 'artifacts/owned-launch/probe-report.json').read_text(encoding='utf-8-sig'))
game = pathlib.Path(previous['game'])
owned = pathlib.Path(tempfile.mkdtemp(prefix='ozl-context-中文-'))
runtime = owned / '运行 时'
config = owned / '配置 目录'
runtime.mkdir(); config.mkdir()
jar = runtime / '伙伴.jar'
shutil.copyfile(pathlib.Path(previous['buddySource']) / 'libs/ZombieBuddy.jar', jar)
source = owned / 'OwnedProbe.java'
source.write_text('public class OwnedProbe { public static void main(String[] a) throws Exception { Class<?> ag=Class.forName("me.zed_0xff.zombie_buddy.Agent"); System.out.println("PROBE_CONFIG_DIR="+ag.getMethod("configDir").invoke(null)); Class<?> lo=Class.forName("me.zed_0xff.zombie_buddy.Loader"); var f=lo.getDeclaredField("g_config"); f.setAccessible(true); System.out.println("PROBE_CONFIG="+f.get(null)); }}', encoding='utf-8')
subprocess.run(['javac', '-encoding', 'UTF-8', '--release', '17', str(source)], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
(config / 'config.json').write_text(json.dumps(dict(trusted_authors=[], preload_mods={}, auto_fix_mod_order=False, fix_approval_dialog_cursor=False)), encoding='utf-8')
sources = [p for p in game.iterdir() if p.is_file()]
sources += list(pathlib.Path(previous['buddySource']).rglob('*'))
sources += [game / 'jre64/bin/java.exe']
sources = [p for p in sources if p.is_file()]
def hashes(): return {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in sources}
before = hashes()
args = [str(game / 'jre64/bin/java.exe'), '-Djava.awt.headless=true', '-Duser.home='+str(owned), '-Djava.library.path='+str(game), '-Dzb.config_dir='+str(config), '-Dfile.encoding=UTF-8', '-Dstdout.encoding=UTF-8', '-javaagent:'+str(jar)+'=prop_prefix=zb,policy=deny-new,frontend=console,verbosity=0', '-cp', str(game / 'projectzomboid.jar')+';'+str(owned), 'OwnedProbe']
env = {k:v for k,v in os.environ.items() if k.upper() not in {'JAVA_TOOL_OPTIONS','_JAVA_OPTIONS','JDK_JAVA_OPTIONS','ZB_VERBOSITY'}}
env['PATH'] = str(game / 'jre64/bin')+';'+env.get('PATH','')
p = subprocess.run(args, cwd=owned, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=60, creationflags=subprocess.CREATE_NO_WINDOW)
stdout = p.stdout.decode('utf-8',errors='replace'); stderr = p.stderr.decode('utf-8',errors='replace')
changed = [path for path,digest in hashes().items() if digest != before[path]]
result = dict(owned=str(owned), args=args, exitCode=p.returncode, stdout=stdout, stderr=stderr, sourcesBefore=before, changed=changed, scope='Background Buddy premain and tiny reflection main only; not game acceptance')
report = repo / 'artifacts/owned-launch/unicode-context-probe.json'
report.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
assert p.returncode == 0, stderr
assert 'auto_fix_mod_order=false' in stdout and 'fix_approval_dialog_cursor=false' in stdout, stdout
assert not changed, changed
print('PASS Chinese/spaced JAR + system-property config: actual Buddy premain; unchanged source files='+str(len(before)))
print('Evidence: '+str(report))
