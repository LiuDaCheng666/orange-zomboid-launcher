import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace

MODULE_PATH = Path(__file__).resolve().parents[1] / 'deploy/deploy_site.py'
if not MODULE_PATH.exists(): MODULE_PATH = Path(__file__).with_name('deploy_site.py')

class DeploymentTreeTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'site'; self.root.mkdir()
        if MODULE_PATH.exists():
            spec = importlib.util.spec_from_file_location('deploy_site', MODULE_PATH)
            self.module = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.module)
        else:
            self.module = None

    def validate(self):
        self.assertIsNotNone(self.module, 'A deployment validator must accept a valid static site before upload')
        return self.module.validate_site(self.root)

    def populate(self):
        for name in ['index.html', 'help.html', 'changes.html', 'site.css', 'site.js', 'release.json', 'assets/home.png', 'assets/orange-app.png', 'assets/orange-app.ico'] + [f'assets/launcher-{panel}-{theme}.png' for panel in ['home', 'components', 'mods', 'servers', 'maintenance'] for theme in ['dark', 'light']]:
            file = self.root / name; file.parent.mkdir(exist_ok=True, parents=True); file.write_bytes(b'public fixture')

    def test_valid_site_returns_only_public_relative_files(self):
        self.populate()
        result = self.validate()
        self.assertEqual(len(result), 19)
        self.assertTrue(all(not p.startswith('/') and '..' not in p for p in result))

    def test_secret_and_missing_index_are_rejected(self):
        self.populate(); (self.root / '.env').write_text('private')
        if self.module:
            with self.assertRaises(ValueError): self.validate()
            (self.root / '.env').unlink(); (self.root / 'index.html').unlink()
            with self.assertRaises(ValueError): self.validate()
        else:
            self.validate()

    def test_unexpected_executable_is_rejected(self):
        self.populate(); (self.root / 'unexpected.exe').write_bytes(b'not a site asset')
        if self.module:
            with self.assertRaises(ValueError): self.validate()
        else:
            self.validate()

    def test_unlisted_image_and_page_are_rejected(self):
        self.populate()
        for name in ['assets/unapproved.png', 'private.html']:
            extra = self.root / name; extra.write_bytes(b'unapproved')
            with self.assertRaises(ValueError): self.validate()
            extra.unlink()

    def test_symlink_to_private_file_is_rejected(self):
        self.populate()
        external = self.root.parent / 'private.txt'; external.write_text('private fixture')
        target = self.root / 'assets/home.png'; target.unlink()
        try: target.symlink_to(external)
        except OSError: self.skipTest('this host requires privilege to create symlinks')
        with self.assertRaises(ValueError): self.validate()

    def test_changed_host_key_stops_before_password_authentication(self):
        self.assertIsNotNone(self.module)
        handover = self.root.parent / 'handover.md'
        handover.write_text('SSH（主要工作方式）\n用户: root\n密码: fixture-not-real-password\n宝塔面板\n', encoding='utf-8')
        calls = []
        transport = SimpleNamespace(start_client=lambda **kw: None,
            get_remote_server_key=lambda: SimpleNamespace(get_name=lambda: 'ssh-ed25519', asbytes=lambda: b'wrong key fixture'),
            close=lambda: calls.append('close'), auth_password=lambda *args: calls.append('auth'))
        with patch.dict('sys.modules', {'paramiko': SimpleNamespace(Transport=lambda value: transport)}), patch('socket.create_connection', return_value=object()):
            with self.assertRaisesRegex(ValueError, 'fingerprint changed'):
                self.module.ssh_connect(handover)
        self.assertEqual(calls, ['close'])

if __name__ == '__main__': unittest.main()
