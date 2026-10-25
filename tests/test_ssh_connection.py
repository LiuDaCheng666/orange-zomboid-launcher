import base64
import hashlib
import importlib.util
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
MODULE = ROOT / 'deploy/ssh_connection.py'


class KeyConnectionTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(MODULE.is_file(), 'key-only project connection helper is missing')
        spec = importlib.util.spec_from_file_location('ozl_ssh_test', MODULE)
        self.module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.module)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.config = dict(host='101.201.143.163', port=22, username='root',
                           authentication='publickey', private_key=str(Path(self.temp.name) / 'fixture-key'),
                           host_key_algorithm='ssh-ed25519',
                           host_key_sha256='SHA256:' + base64.b64encode(hashlib.sha256(b'fixture-host').digest()).decode().rstrip('='),
                           timeout=15)
        self.path = Path(self.temp.name) / 'ssh.json'
        self.write_config()
        self.module.CONFIG_PATH = self.path
        self.calls = []

    def write_config(self):
        self.path.write_text(json.dumps(self.config), encoding='utf-8')

    def transport(self, key=b'fixture-host', authenticated=True):
        return SimpleNamespace(
            start_client=lambda **kw: self.calls.append(('handshake', kw)),
            get_remote_server_key=lambda: SimpleNamespace(get_name=lambda: 'ssh-ed25519', asbytes=lambda: key),
            auth_publickey=lambda user, key: self.calls.append(('publickey', user, key)),
            auth_password=lambda *a: self.fail('password authentication must never be attempted'),
            is_authenticated=lambda: authenticated,
            close=lambda: self.calls.append(('close',)))

    def fake_paramiko(self, transport, failure=None):
        def load(path):
            self.calls.append(('key-load', path))
            if failure:
                raise failure
            return 'fixture-signer'
        return SimpleNamespace(Transport=lambda sock: transport,
                               Ed25519Key=SimpleNamespace(from_private_key_file=load))

    def test_connection_uses_configured_endpoint_and_only_explicit_key(self):
        transport = self.transport()
        with patch.dict('sys.modules', {'paramiko': self.fake_paramiko(transport)}), patch('socket.create_connection') as socket:
            self.assertIs(self.module.connect(), transport)
        socket.assert_called_once_with(('101.201.143.163', 22), timeout=15)
        self.assertEqual([x[0] for x in self.calls], ['handshake', 'key-load', 'publickey'])
        self.assertEqual(self.calls[-1], ('publickey', 'root', 'fixture-signer'))

    def test_changed_host_fails_before_loading_private_key_or_authenticating(self):
        transport = self.transport(key=b'foreign-host')
        with patch.dict('sys.modules', {'paramiko': self.fake_paramiko(transport)}), patch('socket.create_connection'):
            with self.assertRaisesRegex(ValueError, 'fingerprint changed'):
                self.module.connect()
        self.assertEqual([x[0] for x in self.calls], ['handshake', 'close'])

    def test_failed_key_login_closes_connection_without_password_fallback(self):
        transport = self.transport(authenticated=False)
        with patch.dict('sys.modules', {'paramiko': self.fake_paramiko(transport)}), patch('socket.create_connection'):
            with self.assertRaisesRegex(RuntimeError, 'Key authentication failed'):
                self.module.connect()
        self.assertEqual(self.calls[-1], ('close',))

    def test_missing_or_encrypted_key_never_falls_back_to_other_credentials(self):
        for error in [FileNotFoundError('fixture key missing'), ValueError('fixture key needs passphrase')]:
            with self.subTest(error=type(error).__name__):
                self.calls.clear()
                with patch.dict('sys.modules', {'paramiko': self.fake_paramiko(self.transport(), error)}), patch('socket.create_connection'):
                    with self.assertRaises(type(error)):
                        self.module.connect()
                self.assertEqual([x[0] for x in self.calls], ['handshake', 'key-load', 'close'])

    def test_password_or_unpinned_configuration_is_rejected(self):
        for field, value in [('authentication', 'password'), ('host_key_sha256', ''), ('host_key_algorithm', 'ssh-rsa'), ('port', 0)]:
            with self.subTest(field=field):
                config = dict(self.config, **{field: value})
                self.path.write_text(json.dumps(config), encoding='utf-8')
                with patch('socket.create_connection') as socket:
                    with self.assertRaises(ValueError):
                        self.module.connect()
                socket.assert_not_called()

    def test_checked_in_config_matches_approved_key_login_without_secrets(self):
        config = json.loads((ROOT / 'deploy/ssh-config.json').read_text(encoding='utf-8'))
        self.assertEqual((config['host'], config['port'], config['username']), ('101.201.143.163', 22, 'root'))
        self.assertEqual(config['authentication'], 'publickey')
        self.assertEqual(config['private_key'], r'C:\Users\MC233\.ssh\ozl_panel')
        self.assertEqual(config['host_key_sha256'], 'SHA256:3Kj/0G9bg0xadE8/EjVY49yZEfuSF5iq3qDubXCgYIg')
        self.assertFalse(any(word in key.lower() for key in config for word in ['password', 'passphrase']))

    def test_deployer_delegates_without_reading_old_handover(self):
        spec = importlib.util.spec_from_file_location('ozl_deploy_test', ROOT / 'deploy/deploy_site.py')
        deployer = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(deployer)
        transport = object()
        helper = SimpleNamespace(connect=lambda: transport)
        with patch.dict('sys.modules', {'ssh_connection': helper}), patch.object(Path, 'read_text', side_effect=AssertionError('old credential document must not be read')):
            self.assertIs(deployer.ssh_connect('nonexistent-obsolete-password-handover'), transport)


if __name__ == '__main__':
    unittest.main()
