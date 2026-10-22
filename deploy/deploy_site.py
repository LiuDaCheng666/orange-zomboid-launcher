"""Deploy only the approved OZL static tree; SSH credentials stay in process memory."""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import posixpath
import re
import shlex
import socket
import sys
import uuid

HOST = '101.201.143.163'
HOST_KEY = 'SHA256:3Kj/0G9bg0xadE8/EjVY49yZEfuSF5iq3qDubXCgYIg'
BASE = '/var/www/ozl'
SITE_FILES = {'index.html', 'site.css', 'site.js', 'release.json', 'assets/home.png', 'assets/orange-app.png', 'assets/orange-app.ico'}

def validate_site(root):
    root = Path(root).resolve()
    files = {}
    for file in root.rglob('*'):
        if file.is_symlink():
            raise ValueError('Site symlinks are not published')
        if file.is_file():
            relative = file.relative_to(root).as_posix()
            if relative not in SITE_FILES or file.stat().st_size > 12 * 1024 * 1024:
                raise ValueError('Unexpected site file: ' + relative)
            files[relative] = file
    if set(files) != SITE_FILES:
        raise ValueError('Missing required site files')
    return files

def ssh_connect(handover):
    import paramiko
    text = Path(handover).read_text(encoding='utf-8-sig')
    section = text.split('SSH（主要工作方式）', 1)[1].split('宝塔面板', 1)[0]
    match = re.search(r'(?:密码|password)\s*[:：]\s*(\S+)', section, re.I)
    if not match:
        raise ValueError('SSH credential field was not recognized')
    connection = paramiko.Transport(socket.create_connection((HOST, 22), timeout=15))
    connection.start_client(timeout=15)
    key = connection.get_remote_server_key()
    actual = 'SHA256:' + base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip('=')
    if key.get_name() != 'ssh-ed25519' or actual != HOST_KEY:
        connection.close()
        raise ValueError('SSH fingerprint changed; no credentials sent')
    connection.auth_password('root', match.group(1).strip('`'))
    return connection

def command(connection, script, input_bytes=None):
    channel = connection.open_session(timeout=30)
    channel.exec_command(script)
    if input_bytes is not None:
        channel.sendall(input_bytes)
    channel.shutdown_write()
    # Drain both streams together, including verbose package-manager output.
    out, err = bytearray(), bytearray()
    while True:
        if channel.recv_ready(): out.extend(channel.recv(65536))
        if channel.recv_stderr_ready(): err.extend(channel.recv_stderr(65536))
        if channel.exit_status_ready() and not channel.recv_ready() and not channel.recv_stderr_ready(): break
        import select
        select.select([channel], [], [], 1)
    code = channel.recv_exit_status(); channel.close()
    if code:
        raise RuntimeError(f'Remote operation failed ({code}): ' + err.decode(errors='replace')[-2000:] + out.decode(errors='replace')[-2000:])
    return out.decode(errors='replace')

def mkdirs(sftp, directory):
    parts = directory.strip('/').split('/'); current = ''
    for part in parts:
        current += '/' + part
        try: sftp.stat(current)
        except FileNotFoundError: sftp.mkdir(current, 0o755)

def upload(sftp, file, target):
    mkdirs(sftp, posixpath.dirname(target))
    sftp.put(str(file), target)
    sftp.chmod(target, 0o644)

def verify_remote(connection, items):
    checker = "import sys,json,hashlib\nitems=json.load(sys.stdin)\nfor p,h in items.items():\n with open(p,'rb') as f: actual=hashlib.sha256(f.read()).hexdigest()\n if actual!=h: raise SystemExit('Remote hash mismatch: '+p)\nprint('Remote hashes verified: '+str(len(items)))"
    command(connection, 'python3 -c ' + shlex.quote(checker), json.dumps(items).encode())

def prepare_mirror(metadata, original, checked, public_key):
    # Only the regenerated verified tree may be uploaded, never extra input files.
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from prepare_release import prepare_release
    return prepare_release(metadata, Path(original) / 'releases' / metadata['tag_name'], checked, public_key)

def deploy(args):
    files = validate_site(args.site)
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from prepare_release import load_json
    import tempfile
    with tempfile.TemporaryDirectory(prefix='ozl-deploy-check-') as temp:
        metadata = load_json(args.release_metadata.read_bytes())
        checked = Path(temp) / 'checked'
        prepare_mirror(metadata, args.mirror.resolve(), checked, args.public_key.read_bytes())
        deploy_checked(args, files, checked, metadata['tag_name'])

def deploy_checked(args, files, mirror, version):
    import paramiko
    site_id = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:8]
    site_root, release_root = f'{BASE}/sites/{site_id}', f'{BASE}/releases/{version}'
    connection = ssh_connect(args.handover)
    try:
        if args.bootstrap:
            install = '''set -e
dnf -y --setopt=gpgcheck=1 install dnf-plugins-core
dnf -y copr enable @caddy/caddy epel-8-x86_64
dnf -y --setopt=gpgcheck=1 install caddy
caddy version
'''
            print(command(connection, install)[-2500:])
        else:
            print(command(connection, 'command -v caddy\ncaddy version').strip())
        command(connection, f'mkdir -p {BASE}/sites {BASE}/releases {BASE}/config-backups /var/log/caddy\nchown caddy:caddy /var/log/caddy')
        sftp = paramiko.SFTPClient.from_transport(connection)
        proof = {}
        try:
            for relative, file in files.items():
                if relative == 'release.json': file = mirror / 'release.json'
                target = site_root + '/' + relative
                upload(sftp, file, target); proof[target] = hashlib.sha256(file.read_bytes()).hexdigest()
            for name in ['update-manifest.json', 'update-manifest.sig']:
                file = mirror / 'updates/latest' / name; target = site_root + '/updates/latest/' + name
                upload(sftp, file, target); proof[target] = hashlib.sha256(file.read_bytes()).hexdigest()
            immutable = mirror / 'releases' / version
            for file in sorted(immutable.iterdir()):
                target = release_root + '/' + file.name
                expected = hashlib.sha256(file.read_bytes()).hexdigest()
                try:
                    sftp.stat(target)
                    verify_remote(connection, {target: expected})
                except FileNotFoundError:
                    upload(sftp, file, target)
                proof[target] = expected
            verify_remote(connection, proof)
            config_root = site_root + '/deployment-config'
            for name in ['Caddyfile.ip', 'Caddyfile.domain', 'ozl-static.caddy']:
                upload(sftp, Path(__file__).parent / name, config_root + '/' + name)
        finally:
            sftp.close()
        print(command(connection, 'bash -se', activation_script(site_id).encode()))
        print(json.dumps(dict(site_id=site_id, url=f'http://{HOST}/', verified_files=len(proof), version=version)))
    finally:
        connection.close()

def activation_script(site_id, base=BASE, config='/etc/caddy', logs='/var/log/caddy'):
    if not re.fullmatch(r'[0-9]{8}T[0-9]{6}Z-[a-f0-9]{8}', site_id):
        raise ValueError('Invalid site id')
    for path in [base, config, logs]:
        if not re.fullmatch(r'/[a-zA-Z0-9_./-]+', path) or '..' in path:
            raise ValueError('Invalid deployment directory')
    site = f'{base}/sites/{site_id}'
    backup = f'{base}/config-backups/{site_id}'
    return f'''set -Ee
mkdir -p {base}/config/{site_id} {backup}
mv {site}/deployment-config/* {base}/config/{site_id}/
rmdir {site}/deployment-config
old_target=''
if [ -L {base}/current ]; then old_target=$(readlink {base}/current); fi
case "$old_target" in ''|{base}/sites/*) ;; *) exit 1 ;; esac
was_active=0
if systemctl is-active --quiet caddy; then was_active=1; fi
was_enabled=0
if systemctl is-enabled --quiet caddy; then was_enabled=1; fi
if [ -f {config}/Caddyfile ]; then cp -p {config}/Caddyfile {backup}/Caddyfile; fi
if [ -f {config}/ozl-static.caddy ]; then cp -p {config}/ozl-static.caddy {backup}/ozl-static.caddy; fi
rollback() {{
 code=$?
 trap - ERR
 if [ -f {backup}/Caddyfile ]; then
  cp -p {backup}/Caddyfile {config}/Caddyfile
 elif cmp -s {config}/Caddyfile {base}/config/{site_id}/Caddyfile.ip; then unlink {config}/Caddyfile; fi
 if [ -f {backup}/ozl-static.caddy ]; then
  cp -p {backup}/ozl-static.caddy {config}/ozl-static.caddy
 elif cmp -s {config}/ozl-static.caddy {base}/config/{site_id}/ozl-static.caddy; then unlink {config}/ozl-static.caddy; fi
 if [ -n "$old_target" ]; then
  ln -s "$old_target" {base}/rollback-{site_id}
  mv -Tf {base}/rollback-{site_id} {base}/current
 elif [ -L {base}/current ] && [ "$(readlink {base}/current)" = '{site}' ]; then
  unlink {base}/current
 fi
 if [ "$was_active" = 1 ]; then systemctl reload caddy || true; else systemctl stop caddy || true; fi
 if [ "$was_enabled" = 0 ]; then systemctl disable caddy || true; fi
 printf '%s\\n' 'Activation failed; previous configuration and site restored.' >&2
 exit "$code"
}}
trap rollback ERR
cp {base}/config/{site_id}/ozl-static.caddy {config}/ozl-static.caddy
if [ -f {logs}/ozl-access.log ]; then chown caddy:caddy {logs}/ozl-access.log; fi
runuser -u caddy -- caddy validate --config {base}/config/{site_id}/Caddyfile.ip --adapter caddyfile
ln -s {site} {base}/current-{site_id}
mv -Tf {base}/current-{site_id} {base}/current
cp {base}/config/{site_id}/Caddyfile.ip {config}/Caddyfile
systemctl enable caddy
if [ "$was_active" = 1 ]; then systemctl reload caddy; else systemctl start caddy; fi
systemctl is-active caddy
curl --fail --silent --show-error -H 'Host: {HOST}' http://127.0.0.1/release.json
trap - ERR
'''

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--site', type=Path, required=True)
    parser.add_argument('--mirror', type=Path, required=True)
    parser.add_argument('--release-metadata', type=Path, required=True)
    parser.add_argument('--public-key', type=Path, default=Path(__file__).resolve().parents[1] / 'release-public-key.xml')
    parser.add_argument('--handover', type=Path, required=True)
    parser.add_argument('--bootstrap', action='store_true')
    args = parser.parse_args()
    try: deploy(args)
    except Exception as error:
        print('Deployment stopped: ' + str(error), file=sys.stderr); return 1
    return 0

if __name__ == '__main__': sys.exit(main())
