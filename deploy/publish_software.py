"""Publish an authenticated software release without deploying or reconfiguring the website."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shlex
import stat
import subprocess
import sys
import tempfile
import uuid
from urllib.request import Request, urlopen

from prepare_release import load_json, prepare_velopack_release

BASE = '/var/www/ozl'
FEED_NAMES = ('releases.win.json', 'velopack-manifest.json', 'velopack-manifest.sig')
PUBLIC_KEY = Path(__file__).resolve().parents[1] / 'release-public-key.xml'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def version_tuple(value):
    if not isinstance(value, str) or not re.fullmatch(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', value):
        raise ValueError('Invalid stable version')
    return tuple(map(int, value.split('.')))


def validate_current(path):
    if not re.fullmatch(re.escape(BASE) + r'/sites/[A-Za-z0-9][A-Za-z0-9_-]{0,120}', path):
        raise ValueError('Expected a fixed physical current site under /var/www/ozl/sites')
    return path


def command(connection, script):
    channel = connection.open_session(timeout=30)
    channel.settimeout(300)
    channel.exec_command('python3 -c ' + shlex.quote(script))
    channel.shutdown_write()
    out, err = bytearray(), bytearray()
    import select
    while True:
        if channel.recv_ready(): out.extend(channel.recv(65536))
        if channel.recv_stderr_ready(): err.extend(channel.recv_stderr(65536))
        if channel.exit_status_ready() and not channel.recv_ready() and not channel.recv_stderr_ready(): break
        select.select([channel], [], [], 1)
    code = channel.recv_exit_status()
    channel.close()
    if code: raise RuntimeError('Read-only remote verification failed: ' + err.decode(errors='replace')[-1500:])
    return load_json(bytes(out))


# These scripts only read. Their inputs are generated from fixed roots, a validated
# semantic version, and the already resolved physical site; no shell path expansion.
REMOTE_SNAPSHOT = '''
import os,stat,hashlib,json
excluded=EXCLUDED
result={}
def skip(p):
 return p.startswith('/var/www/ozl/test-runs/') or p=='/var/www/ozl/test-runs' or any(p==x or p.startswith(x+'/') for x in excluded)
def visit(p):
 if skip(p): return
 try: s=os.lstat(p)
 except FileNotFoundError:
  result[p]={'missing':True}; return
 if stat.S_ISLNK(s.st_mode):
  result[p]={'link':os.readlink(p),'mode':stat.S_IMODE(s.st_mode)}; return
 if stat.S_ISDIR(s.st_mode):
  result[p]={'directory':True,'mode':stat.S_IMODE(s.st_mode)}
  for name in sorted(os.listdir(p)): visit(p+'/'+name)
 elif stat.S_ISREG(s.st_mode):
  h=hashlib.sha256()
  with open(p,'rb') as f:
   for b in iter(lambda:f.read(1048576),b''): h.update(b)
  result[p]={'sha256':h.hexdigest(),'length':s.st_size,'mode':stat.S_IMODE(s.st_mode)}
 else: raise ValueError('Unsupported protected file: '+p)
for root in ['/var/www/ozl','/etc/caddy','/etc/ozl-http-guard','/etc/nginx','/etc/fail2ban/jail.d','/etc/fail2ban/filter.d/ozl-scan.conf','/etc/fail2ban/action.d/ozl-web.conf','/etc/ssh/sshd_config','/etc/ssh/sshd_config.d']:
 visit(root)
print(json.dumps(result,sort_keys=True))
'''


class SftpRemote:
    def __init__(self):
        import paramiko
        from ssh_connection import connect
        self.connection = connect()
        try: self.sftp = paramiko.SFTPClient.from_transport(self.connection)
        except BaseException:
            self.connection.close(); raise
        self.allowed_files, self.allowed_dirs, self.allowed_renames = set(), set(), set()

    def configure(self, files, directories, renames):
        self.allowed_files, self.allowed_dirs, self.allowed_renames = set(files), set(directories), set(renames)

    def regular(self, path, directory=False):
        # Resolve no symlinks during writes; current itself is never a write path.
        parts = path.strip('/').split('/')
        for i in range(1, len(parts)+1):
            candidate = '/' + '/'.join(parts[:i])
            try: mode = self.sftp.lstat(candidate).st_mode
            except FileNotFoundError:
                if i == len(parts): return
                raise
            expected_dir = i < len(parts) or directory
            if not (stat.S_ISDIR(mode) if expected_dir else stat.S_ISREG(mode)):
                raise ValueError('Unexpected remote type or symlink: ' + candidate)

    def resolve_current(self):
        return validate_current(self.sftp.normalize(BASE + '/current'))

    def exists(self, path):
        try: self.sftp.lstat(path); return True
        except FileNotFoundError: return False

    def read(self, path):
        self.regular(path)
        with self.sftp.open(path, 'rb') as f: return f.read()

    def names(self, path):
        self.regular(path, directory=True)
        items = self.sftp.listdir_attr(path)
        if any(not stat.S_ISREG(item.st_mode) for item in items):
            raise ValueError('Unexpected immutable release entry')
        return {item.filename for item in items}

    def mkdir(self, path):
        if path not in self.allowed_dirs: raise ValueError('Directory is outside publication whitelist')
        self.regular(path, directory=True)
        self.sftp.mkdir(path, 0o755)
        self.sftp.chmod(path, 0o755)

    def write(self, path, content):
        if path not in self.allowed_files: raise ValueError('File is outside publication whitelist')
        self.regular(path)
        with self.sftp.open(path, 'wb') as f: f.write(content)
        self.sftp.chmod(path, 0o644)

    def hashes(self, paths):
        for path in paths: self.regular(path)
        script = 'import hashlib,json\nresult={}\nfor p in ' + repr(list(paths)) + ':\n h=hashlib.sha256()\n with open(p,"rb") as f:\n  for b in iter(lambda:f.read(1048576),b""): h.update(b)\n result[p]=h.hexdigest()\nprint(json.dumps(result))'
        return command(self.connection, script)

    def rename(self, source, target, replace=False):
        if (source,target,replace) not in self.allowed_renames:
            raise ValueError('Rename is outside publication whitelist')
        is_directory = stat.S_ISDIR(self.sftp.lstat(source).st_mode)
        self.regular(source, directory=is_directory)
        self.regular(target, directory=is_directory)
        if replace: self.sftp.posix_rename(source, target)
        else: self.sftp.rename(source, target)

    def snapshot(self, excluded):
        return command(self.connection, REMOTE_SNAPSHOT.replace('EXCLUDED', repr(list(excluded))))

    def close(self):
        self.sftp.close(); self.connection.close()


def http_health(origin, mutable, display):
    for relative, expected in mutable.items():
        request = Request(origin.rstrip('/') + '/' + relative, headers={'Cache-Control':'no-cache', 'User-Agent':'OZL-release-verifier'})
        with urlopen(request, timeout=30) as response:
            if response.status != 200 or sha(response.read(2*1024*1024)) != sha(expected):
                raise RuntimeError('Public health verification failed: ' + relative)
    for name in ('setup', 'portable'):
        request = Request(origin.rstrip('/') + display[name]['url'], method='HEAD', headers={'User-Agent':'OZL-release-verifier'})
        with urlopen(request, timeout=30) as response:
            if response.status != 200 or int(response.headers.get('Content-Length', '-1')) != display[name]['length']:
                raise RuntimeError('Public artifact health verification failed: ' + name)


def publish(source, legacy, public_key, expected_current, evidence, *, remote_factory=SftpRemote, health_check=None,
            verified_artifacts=None):
    """Authenticate before connecting; replace exactly four existing mutable files."""
    expected_current = validate_current(expected_current)
    evidence = Path(evidence).resolve()
    if health_check is None: raise ValueError('A public health check is required')
    with tempfile.TemporaryDirectory(prefix='ozl-software-') as temporary:
        mirror = Path(temporary) / 'verified'
        display = prepare_velopack_release(source, mirror, public_key, legacy)
        version = display['version']; version_tuple(version)
        artifact_root = mirror / 'releases' / ('v' + version)
        artifacts = {p.name:p.read_bytes() for p in artifact_root.iterdir()}
        if len(artifacts) != 10: raise ValueError('Expected exactly ten immutable files')
        if verified_artifacts is not None:
            actual_hashes = {name: sha(content) for name, content in artifacts.items()}
            frozen_hashes = {name: value.lower() for name, value in verified_artifacts.items()}
            if actual_hashes != frozen_hashes:
                raise ValueError('Publication bytes differ from the Windows-verified release; verify the frozen candidate again')
        mutable = {'updates/velopack/latest/' + n:(mirror/'updates/velopack/latest'/n).read_bytes() for n in FEED_NAMES}
        final = BASE + '/releases/v' + version
        token = uuid.uuid4().hex
        staging = BASE + '/releases/.v' + version + '-staging-' + token
        run = BASE + '/test-runs/software-v' + version + '-' + token
        targets = {n:expected_current+'/'+n for n in [*mutable, 'release.json']}
        excluded = [*targets.values(), final, staging]
        remote = remote_factory()
        receipt = {'version':version, 'status':'failed', 'current':expected_current, 'run':run,
                   'started_at':datetime.now(timezone.utc).isoformat()}
        activated = []
        try:
            if remote.resolve_current() != expected_current: raise ValueError('current changed before publication')
            before = remote.snapshot(excluded); receipt['protected_before'] = before
            old = {name:remote.read(path) for name,path in targets.items()}
            old_display = load_json(old['release.json'])
            if not isinstance(old_display, dict): raise ValueError('Existing release.json must be an object')
            if version_tuple(old_display.get('version')) > version_tuple(version): raise ValueError('Refusing version downgrade')
            live_feed = load_json(remote.read(expected_current+'/updates/velopack/latest/velopack-manifest.json'))
            if not isinstance(live_feed,dict) or version_tuple(live_feed.get('version')) > version_tuple(version):
                raise ValueError('Refusing feed version downgrade')
            for name in ('update-manifest.json', 'update-manifest.sig'):
                if remote.read(expected_current+'/updates/latest/'+name) != (mirror/'updates/latest'/name).read_bytes():
                    raise ValueError('Preserved legacy feed differs from server')
            for kind in ('setup', 'portable'):
                previous = old_display.get(kind)
                if isinstance(previous, dict):
                    display[kind] = {**previous, **display[kind]}
            display = {**old_display, **display}
            mutable['release.json'] = json.dumps(display, ensure_ascii=False, indent=2).encode('utf-8')
            expected_hashes = {final+'/'+n:sha(b) for n,b in artifacts.items()}
            existing = remote.exists(final)
            if existing and (remote.names(final) != set(artifacts) or remote.hashes(expected_hashes) != expected_hashes):
                raise ValueError('Existing immutable release collision')
            staged = {name:run+'/new-'+str(i) for i,name in enumerate(targets)}
            backups = {name:run+'/old-'+str(i) for i,name in enumerate(targets)}
            rollback = {name:run+'/rollback-'+str(i) for i,name in enumerate(targets)}
            receipt['backups'] = {targets[n]:p for n,p in backups.items()}
            directories = [run] + ([] if existing else [staging])
            files = [*staged.values(), *backups.values(), *rollback.values()] + ([] if existing else [staging+'/'+n for n in artifacts])
            renames = [(staged[n],p,True) for n,p in targets.items()] + [(rollback[n],p,True) for n,p in targets.items()]
            if not existing: renames.append((staging, final, False))
            if hasattr(remote, 'configure'): remote.configure(files, directories, renames)
            remote.mkdir(run)
            for name in targets:
                remote.write(backups[name], old[name]); remote.write(staged[name], mutable[name])
            expected_staged = {**{staged[n]:sha(mutable[n]) for n in targets}, **{backups[n]:sha(old[n]) for n in targets}}
            if remote.hashes(expected_staged) != expected_staged: raise ValueError('Staged metadata hash mismatch')
            if not existing:
                remote.mkdir(staging)
                for name,content in artifacts.items(): remote.write(staging+'/'+name, content)
                staged_hashes = {staging+'/'+n:sha(b) for n,b in artifacts.items()}
                if remote.hashes(staged_hashes) != staged_hashes: raise ValueError('Staged immutable hash mismatch')
                remote.rename(staging, final)
            if remote.hashes(expected_hashes) != expected_hashes: raise ValueError('Immutable release hash mismatch')
            if remote.snapshot(excluded) != before or remote.resolve_current() != expected_current:
                raise ValueError('protected files or current changed before activation')
            if any(remote.read(p) != old[n] for n,p in targets.items()):
                raise ValueError('Mutable metadata changed concurrently; refusing activation')
            # Signature last among feed files, metadata last overall. Readers verify
            # signed pairs; rollback restores only files this run actually changed.
            for name,path in targets.items():
                if remote.resolve_current() != expected_current: raise ValueError('current changed during activation')
                # Uploading/replacing earlier feed files can take seconds. Recheck
                # this target now so a newer metadata edit is never knowingly lost.
                if remote.read(path) != old[name]:
                    raise ValueError('Mutable metadata changed concurrently during activation: ' + name)
                # Include an uncertain rename in rollback: SSH can disconnect after
                # the server commits a rename but before its response reaches us.
                activated.append(name)
                remote.rename(staged[name], path, replace=True)
            expected_mutable = {targets[n]:sha(b) for n,b in mutable.items()}
            if remote.hashes(expected_mutable) != expected_mutable: raise ValueError('Published mutable hash mismatch')
            health_check(mutable, display)
            if remote.hashes(expected_hashes) != expected_hashes: raise ValueError('Immutable release changed during health verification')
            after = remote.snapshot(excluded); receipt['protected_after'] = after
            if after != before or remote.resolve_current() != expected_current:
                raise ValueError('protected files or current changed after activation')
            receipt.update(status='published', immutable=expected_hashes, mutable=expected_mutable,
                           previous_mutable={targets[n]:sha(b) for n,b in old.items()})
            return receipt
        except BaseException as error:
            receipt['error'] = str(error)
            if activated:
                try:
                    for name in reversed(activated):
                        # Refuse to overwrite a newer publisher's intervening edit.
                        current_bytes = remote.read(targets[name])
                        if current_bytes == old[name]: continue
                        if current_bytes != mutable[name]:
                            raise RuntimeError('Concurrent metadata change; automatic rollback refused')
                        remote.write(rollback[name], old[name])
                        remote.rename(rollback[name], targets[name], replace=True)
                    receipt['status'] = 'rolled-back'
                    receipt['protected_after'] = remote.snapshot(excluded)
                    old_hashes = {targets[n]:sha(old[n]) for n in activated}
                    if remote.hashes(old_hashes) != old_hashes: raise RuntimeError('Rollback verification failed')
                except BaseException as rollback_error:
                    receipt['rollback_error'] = str(rollback_error)
                    receipt['status'] = 'rollback-failed'
            raise
        finally:
            receipt['finished_at'] = datetime.now(timezone.utc).isoformat()
            try:
                evidence.parent.mkdir(parents=True, exist_ok=True)
                evidence.write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
            finally:
                if hasattr(remote, 'close'): remote.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='Raw signed ten-file Velopack release')
    parser.add_argument('--preserved-legacy', type=Path, required=True)
    parser.add_argument('--public-key', type=Path, default=PUBLIC_KEY)
    parser.add_argument('--expected-current', required=True, help='Previously verified physical current site path')
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--health-origin', default='https://orangezl.com')
    parser.add_argument('--authenticode-config', type=Path, required=True,
                        help='Local exact Windows publisher identity; no certificate key is uploaded')
    args = parser.parse_args()
    try:
        # Verify the actual bytes before opening SSH. RSA update-feed signatures
        # do not establish a Windows publisher or prevent unsigned regressions.
        signing_receipt = args.evidence.resolve().with_suffix('.authenticode.json')
        signing_receipt.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File',
                        str(Path(__file__).resolve().parents[1] / 'build' / 'Verify-WindowsRelease.ps1'),
                        '-ReleaseDirectory', str(args.source.resolve()),
                        '-AuthenticodeConfig', str(args.authenticode_config.resolve()),
                        '-EvidencePath', str(signing_receipt)], check=True)
        windows_verified = load_json(signing_receipt.read_bytes())
        verified_hashes = {item['name']: item['sha256'] for item in windows_verified['artifacts']}
        result = publish(args.source, args.preserved_legacy, args.public_key.read_bytes(), args.expected_current,
                         args.evidence, health_check=lambda mutable,display:http_health(args.health_origin, mutable, display),
                         verified_artifacts=verified_hashes)
    except Exception as error:
        print('Software publication failed: ' + str(error), file=sys.stderr); return 1
    print(json.dumps({'status':result['status'], 'version':result['version'], 'evidence':str(args.evidence.resolve())}))
    return 0


if __name__ == '__main__': sys.exit(main())
