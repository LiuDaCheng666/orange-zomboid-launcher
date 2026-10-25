"""Project SSH connection: one explicit Ed25519 key, pinned host, no password fallback."""
import base64
import hashlib
import json
from pathlib import Path, PureWindowsPath
import re
import socket

CONFIG_PATH = Path(__file__).with_name('ssh-config.json')


def load_config():
    config = json.loads(CONFIG_PATH.read_text(encoding='utf-8-sig'))
    fields = {'host', 'port', 'username', 'authentication', 'private_key',
              'host_key_algorithm', 'host_key_sha256', 'timeout'}
    if not isinstance(config, dict) or set(config) != fields:
        raise ValueError('SSH config must contain exactly the documented connection fields')
    if config['authentication'] != 'publickey' or config['host_key_algorithm'] != 'ssh-ed25519':
        raise ValueError('Only pinned Ed25519 public-key authentication is allowed')
    for name in ('host', 'username', 'private_key'):
        if not isinstance(config[name], str) or not config[name].strip():
            raise ValueError('SSH config field is missing: ' + name)
    key_path = config['private_key']
    native_absolute = Path(key_path).is_absolute()
    if not (native_absolute or PureWindowsPath(key_path).is_absolute()):
        raise ValueError('Private key must be an absolute path outside the project')
    project = Path(__file__).resolve().parents[1]
    if native_absolute and Path(key_path).resolve().is_relative_to(project):
        raise ValueError('Private key must stay outside the project')
    if type(config['port']) is not int or not 1 <= config['port'] <= 65535:
        raise ValueError('Invalid SSH port')
    if type(config['timeout']) is not int or not 1 <= config['timeout'] <= 120:
        raise ValueError('Invalid SSH timeout')
    if not isinstance(config['host_key_sha256'], str) or not re.fullmatch(r'SHA256:[A-Za-z0-9+/]{43}', config['host_key_sha256']):
        raise ValueError('A pinned SHA256 SSH host fingerprint is required')
    return config


def connect():
    import paramiko
    config = load_config()
    sock = socket.create_connection((config['host'], config['port']), timeout=config['timeout'])
    connection = None
    try:
        connection = paramiko.Transport(sock)
        connection.start_client(timeout=config['timeout'])
        host_key = connection.get_remote_server_key()
        actual = 'SHA256:' + base64.b64encode(hashlib.sha256(host_key.asbytes()).digest()).decode().rstrip('=')
        if host_key.get_name() != config['host_key_algorithm'] or actual != config['host_key_sha256']:
            raise ValueError('SSH fingerprint changed; no credentials sent')
        # Transport does not consult ssh-agent, discover other identities, or try a password.
        signer = paramiko.Ed25519Key.from_private_key_file(config['private_key'])
        connection.auth_publickey(config['username'], signer)
        if not connection.is_authenticated():
            raise RuntimeError('Key authentication failed')
        return connection
    except BaseException:
        if connection is not None:
            connection.close()
        else:
            sock.close()
        raise
