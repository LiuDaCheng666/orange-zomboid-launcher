#!/usr/bin/env python3
"""First-install/owned rollback only. Never replace a website or Caddy config."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import time
import urllib.error
import urllib.request
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parent))
from configuration import patch_nginx, unpatch_nginx


def sha(content):
    return hashlib.sha256(content).hexdigest()


def file_sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def atomic(path, content, mode=0o644):
    path = Path(path)
    if path.is_symlink():
        raise ValueError('Refusing a symlink deployment target')
    temporary = path.with_name('.feedback-' + uuid.uuid4().hex)
    try:
        with os.fdopen(os.open(str(temporary), os.O_WRONLY | os.O_CREAT | os.O_EXCL, mode), 'wb') as output:
            output.write(content); output.flush(); os.fsync(output.fileno())
        os.replace(str(temporary), str(path))
        # Creation modes are filtered by main()'s private umask. Final public
        # service sources/includes must still be readable by their consumers.
        os.chmod(str(path), mode)
    finally:
        if temporary.exists():
            temporary.unlink()


class Installer:
    """Fixed server layout; root prefix permits isolated filesystem validation."""
    def __init__(self, root='/', run=None):
        self.root = Path(root)
        self.run = run or (lambda args: subprocess.run(args, check=True, timeout=120))

    def path(self, absolute):
        return self.root / absolute.lstrip('/')

    def protected(self):
        current = self.path('/var/www/ozl/current')
        resolved = current.resolve(strict=True)
        result = dict(current=str(resolved), current_link=os.readlink(str(current)) if current.is_symlink() else None,
                      website={}, releases={}, caddy={}, root_files={})
        for label, directory in (('website', resolved), ('releases', self.path('/var/www/ozl/releases'))):
            for folder, dirs, files in os.walk(str(directory), followlinks=False):
                for name in sorted(dirs + files):
                    path = Path(folder) / name
                    relative = str(path.relative_to(directory)).replace('\\', '/')
                    if path.is_symlink():
                        result[label][relative] = 'symlink:' + os.readlink(str(path))
                    elif path.is_file():
                        result[label][relative] = file_sha(path)
        for path in self.path('/etc/caddy').rglob('*'):
            if path.is_file():
                result['caddy'][str(path.relative_to(self.path('/etc/caddy')))] = file_sha(path)
        for path in self.path('/var/www/ozl').iterdir():
            if path.is_file() and not path.is_symlink():
                result['root_files'][path.name] = file_sha(path)
        return result

    def account(self):
        import pwd
        try:
            account = pwd.getpwnam('ozl-feedback')
        except KeyError:
            self.run(['useradd', '--system', '--no-create-home', '--home-dir', '/var/lib/ozl-feedback', '--shell', '/sbin/nologin', 'ozl-feedback'])
            account = pwd.getpwnam('ozl-feedback')
        if account.pw_uid == 0:
            raise ValueError('Feedback account must be unprivileged')
        return account.pw_uid, account.pw_gid

    def health(self, port, path, expected, timeout=5):
        request = urllib.request.Request('http://127.0.0.1:' + str(port) + path,
                                         headers={'X-Real-IP': '192.0.2.254'})
        try:
            response = urllib.request.urlopen(request, timeout=timeout)
        except urllib.error.HTTPError as error:
            response = error
        try:
            if response.code != expected:
                raise RuntimeError('expected HTTP ' + str(expected) + ', received HTTP ' + str(response.code))
            response.read(4096)
        finally:
            response.close()

    def wait_health(self, port, path, expected, maximum=10):
        # systemctl's ExecReload only sends HUP. Old nginx workers may still
        # answer while the new generation initializes; probe actual readiness.
        deadline = time.monotonic() + maximum
        last, observed = None, None
        while time.monotonic() < deadline:
            try:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    break
                self.health(port, path, expected, min(1.0, remaining))
                return
            except (OSError, RuntimeError) as error:
                last = error
                if str(error) != observed:
                    observed = str(error)
                    print(json.dumps(dict(readiness=dict(port=port, expected=expected, observed=observed))), flush=True)
                remaining = deadline - time.monotonic()
                if remaining > 0:
                    time.sleep(min(0.25, remaining))
        raise RuntimeError('Local feedback readiness timed out: ' + str(last))

    def state(self, path, value):
        atomic(path, json.dumps(value, indent=2).encode('utf-8'), 0o600)

    def proxy_probe(self):
        # Invalid schema never reaches storage or consumes an accepted quota.
        # A forged identity must be overwritten by the trusted nginx route.
        request = urllib.request.Request('http://127.0.0.1:18081/api/feedback', data=b'{}',
            headers={'Content-Type': 'application/json', 'X-Real-IP': '192.0.2.254',
                     'X-OZL-Client-IP': 'not-an-ip'})
        try:
            response = urllib.request.urlopen(request, timeout=5)
        except urllib.error.HTTPError as error:
            response = error
        try:
            try:
                value = json.loads(response.read(4096).decode('utf-8'))
            except (ValueError, UnicodeError):
                raise RuntimeError('Feedback proxy did not return backend JSON')
            if (response.code != 400 or value != {'error': 'invalid_request'}
                    or response.headers.get('Cache-Control') != 'no-store'
                    or response.headers.get('Content-Type', '').split(';')[0].strip() != 'application/json'):
                raise RuntimeError('Feedback proxy/body/client-identity readiness check failed')
        finally:
            response.close()

    def install(self, bundle, expected):
        bundle = Path(bundle)
        live = self.path('/etc/ozl-http-guard/nginx.conf')
        original = live.read_bytes()
        patched = patch_nginx(original.decode('utf-8'), expected).encode('utf-8')
        for name in ('/opt/ozl-feedback', '/etc/ozl-feedback', '/etc/systemd/system/ozl-feedback.service', '/var/lib/ozl-feedback'):
            if self.path(name).exists():
                raise ValueError('Existing feedback installation; explicit migration required')
        baseline = self.protected()
        stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:8]
        backup = self.path('/var/backups/ozl-feedback') / stamp
        backup.mkdir(mode=0o700, parents=True)
        os.chmod(str(backup.parent), 0o700)
        atomic(backup / 'nginx.conf', original, 0o600)
        atomic(backup / 'protected-sha256.json', json.dumps(baseline, indent=2).encode('utf-8'), 0o600)
        with tarfile.open(str(backup / 'protected-site.tar.gz'), 'w:gz') as archive:
            archive.add(str(Path(baseline['current'])), arcname='site', recursive=True)
            archive.add(str(self.path('/etc/caddy')), arcname='caddy', recursive=True)
        os.chmod(str(backup / 'protected-site.tar.gz'), 0o600)
        state_path = backup / 'state.json'
        state = dict(phase='prepared', original_sha256=sha(original), patched_sha256=sha(patched), backup=str(backup))
        self.state(state_path, state)
        try:
            uid, gid = self.account()
            for name in ('/opt/ozl-feedback', '/etc/ozl-feedback'):
                self.path(name).mkdir(mode=0o755)
                os.chmod(str(self.path(name)), 0o755)
            store = self.path('/var/lib/ozl-feedback')
            store.mkdir(mode=0o700); os.chmod(str(store), 0o700); os.chown(str(store), uid, gid)
            for name in ('server.py', 'install.py', 'configuration.py'):
                atomic(self.path('/opt/ozl-feedback') / name, (bundle / name).read_bytes())
            for name in ('nginx-http.conf', 'nginx-location.conf'):
                atomic(self.path('/etc/ozl-feedback') / name, (bundle / name).read_bytes())
            atomic(self.path('/etc/systemd/system/ozl-feedback.service'), (bundle / 'ozl-feedback.service').read_bytes())
            self.run(['systemctl', 'daemon-reload'])
            self.run(['systemctl', 'enable', '--now', 'ozl-feedback'])
            self.wait_health(18082, '/health', 200)
            if sha(live.read_bytes()) != expected:
                raise ValueError('Concurrent nginx configuration change before publication')
            atomic(live, patched, live.stat().st_mode & 0o777)
            self.run(['/usr/sbin/nginx', '-t', '-c', '/etc/ozl-http-guard/nginx.conf'])
            self.run(['systemctl', 'reload', 'ozl-http-guard'])
            self.wait_health(18081, '/api/feedback', 405)
            self.proxy_probe()
            if self.protected() != baseline:
                raise RuntimeError('Protected site, release files, current or Caddy changed during installation')
            if live.read_bytes() != patched:
                raise ValueError('Concurrent nginx configuration change after publication')
            state['phase'] = 'verified'
            self.state(state_path, state)
            return str(state_path)
        except BaseException:
            try:
                self.rollback(str(state_path))
            finally:
                state['phase'] = 'failed'
                self.state(state_path, state)
            raise

    def rollback(self, state_path):
        state_path = Path(state_path)
        state = json.loads(state_path.read_text(encoding='utf-8'))
        live = self.path('/etc/ozl-http-guard/nginx.conf')
        before = self.protected()
        try:
            current = live.read_bytes()
            if sha(current) == state['patched_sha256']:
                restored = unpatch_nginx(current.decode('utf-8'), state['patched_sha256']).encode('utf-8')
                if sha(restored) != state['original_sha256']:
                    raise ValueError('Owned route removal did not restore the expected configuration')
                atomic(live, restored, live.stat().st_mode & 0o777)
                self.run(['/usr/sbin/nginx', '-t', '-c', '/etc/ozl-http-guard/nginx.conf'])
                self.run(['systemctl', 'reload', 'ozl-http-guard'])
            elif sha(current) != state['original_sha256']:
                raise ValueError('Concurrent nginx configuration change; refusing rollback replacement')
            if self.protected() != before:
                raise RuntimeError('Protected content changed during rollback')
            state['phase'] = 'rolled_back'
            self.state(state_path, state)
        finally:
            self.run(['systemctl', 'disable', '--now', 'ozl-feedback'])


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--expected-nginx-sha256')
    parser.add_argument('--rollback', metavar='STATE_JSON')
    args = parser.parse_args()
    installer = Installer()
    if args.rollback:
        installer.rollback(args.rollback)
        print(json.dumps(dict(rolled_back=True)))
    else:
        if not args.expected_nginx_sha256:
            parser.error('--expected-nginx-sha256 is required')
        state = installer.install(Path(__file__).resolve().parent, args.expected_nginx_sha256)
        print(json.dumps(dict(installed=True, state=state)))


if __name__ == '__main__':
    main()
