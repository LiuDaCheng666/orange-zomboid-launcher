"""Pinned-key SSH operator for feedback only. `audit` performs no writes."""
import argparse
import json
from pathlib import Path
import re
import select
import shlex
import sys
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from ssh_connection import connect

AUDIT = '''import hashlib,json,os,sys
from pathlib import Path
p=Path('/etc/ozl-http-guard/nginx.conf')
data=p.read_bytes()
print(json.dumps(dict(read_only=True,python=sys.version.split()[0],nginx_sha256=hashlib.sha256(data).hexdigest(),
 current=os.readlink('/var/www/ozl/current'),caddy_sha256=hashlib.sha256(Path('/etc/caddy/ozl-static.caddy').read_bytes()).hexdigest(),
 listener18080=data.count(b'listen 127.0.0.1:18080;'),listener18081=data.count(b'listen 127.0.0.1:18081;'),
 feedback_installed=Path('/etc/systemd/system/ozl-feedback.service').exists())))
'''
BUNDLE = ('server.py', 'configuration.py', 'install.py', 'nginx-http.conf',
          'nginx-location.conf', 'ozl-feedback.service')


def readonly_check(connection, expected):
    """Compile and nginx-test anonymous memory only; no upload/reload/temp file."""
    import paramiko
    from configuration import patch_nginx
    with paramiko.SFTPClient.from_transport(connection) as sftp:
        with sftp.open('/etc/ozl-http-guard/nginx.conf', 'rb') as source:
            original = source.read(262145)
    if len(original) > 262144:
        raise ValueError('Live nginx config is unexpectedly large')
    patched = patch_nginx(original.decode('utf-8'), expected)
    for name in ('nginx-http.conf', 'nginx-location.conf'):
        patched = patched.replace('include /etc/ozl-feedback/' + name + ';', (Path(__file__).parent / name).read_text(encoding='utf-8'))
    patched = re.sub(r'(?m)^pid .*?;', 'pid /dev/null;', patched)
    patched = re.sub(r'(?m)^\s*error_log .*?;', '\nerror_log /dev/stderr warn;', patched)
    patched = re.sub(r'(?m)^\s*access_log .*?;', '\naccess_log /dev/null;', patched)
    sources = {name: (Path(__file__).parent / name).read_text(encoding='utf-8') for name in ('server.py', 'configuration.py', 'install.py')}
    return ('import ctypes,json,os,subprocess,sys\n'
            'sources=json.loads(' + repr(json.dumps(sources)) + ')\n'
            'for name,source in sources.items(): compile(source,name,"exec")\n'
            'print(json.dumps(dict(read_only=True,python=sys.version.split()[0],compiled=sorted(sources))),flush=True)\n'
            'lib=ctypes.CDLL(None,use_errno=True)\n'
            'lib.memfd_create.argtypes=[ctypes.c_char_p,ctypes.c_uint]\n'
            'fd=lib.memfd_create(b"ozl-feedback-readonly",0)\n'
            'assert fd>=0\n'
            'os.write(fd,' + repr(patched.encode('utf-8')) + ')\n'
            'os.lseek(fd,0,0)\n'
            'r=subprocess.run(["/usr/sbin/nginx","-t","-c","/proc/self/fd/"+str(fd),"-e","/dev/stderr"],pass_fds=(fd,),stdout=subprocess.PIPE,stderr=subprocess.PIPE)\n'
            'print(r.stdout.decode(),r.stderr.decode());os.close(fd);sys.exit(r.returncode)\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('audit', 'check', 'install', 'rollback', 'list', 'read', 'delete'))
    parser.add_argument('--expected-nginx-sha256')
    parser.add_argument('--state')
    parser.add_argument('--id')
    args = parser.parse_args()
    if args.command in ('check', 'install') and not re.fullmatch(r'[a-f0-9]{64}', args.expected_nginx_sha256 or ''):
        parser.error('check/install requires an explicitly audited --expected-nginx-sha256')
    if args.command == 'rollback' and not re.fullmatch(r'/var/backups/ozl-feedback/[0-9TZ-]+[a-f0-9]{8}/state\.json', args.state or ''):
        parser.error('rollback requires the exact installed /var/backups/ozl-feedback/.../state.json')
    if args.command in ('read', 'delete') and not re.fullmatch(r'[a-f0-9]{32}', args.id or ''):
        parser.error('read/delete require one exact 32-lowercase-hex --id')
    connection = connect()
    try:
        source = None
        if args.command == 'install':
            import paramiko
            staging = '/root/ozl-feedback-' + uuid.uuid4().hex
            with paramiko.SFTPClient.from_transport(connection) as sftp:
                sftp.mkdir(staging, mode=0o700)
                for name in BUNDLE:
                    target = staging + '/' + name
                    sftp.put(str(Path(__file__).parent / name), target)
                    sftp.chmod(target, 0o600)
            command = ['/usr/bin/python3', staging + '/install.py', '--expected-nginx-sha256', args.expected_nginx_sha256]
        elif args.command == 'rollback':
            command = ['/usr/bin/python3', '/opt/ozl-feedback/install.py', '--rollback', args.state]
        elif args.command in ('audit', 'check'):
            command = ['/usr/bin/python3', '-u', '-']
            source = AUDIT if args.command == 'audit' else readonly_check(connection, args.expected_nginx_sha256)
        else:
            command = ['runuser', '-u', 'ozl-feedback', '--', '/usr/bin/python3', '/opt/ozl-feedback/server.py', args.command]
            if args.id:
                command.append(args.id)
        channel = connection.open_session(timeout=15)
        try:
            channel.settimeout(300)
            channel.exec_command(' '.join(shlex.quote(value) for value in command))
            if source is not None:
                channel.sendall(source.encode('utf-8'))
            channel.shutdown_write()
            while True:
                if channel.recv_ready():
                    sys.stdout.buffer.write(channel.recv(65536)); sys.stdout.buffer.flush()
                if channel.recv_stderr_ready():
                    sys.stderr.buffer.write(channel.recv_stderr(65536)); sys.stderr.buffer.flush()
                if channel.exit_status_ready() and not channel.recv_ready() and not channel.recv_stderr_ready():
                    return channel.recv_exit_status()
                select.select([channel], [], [], 0.25)
        finally:
            channel.close()
    finally:
        connection.close()


if __name__ == '__main__':
    raise SystemExit(main())
