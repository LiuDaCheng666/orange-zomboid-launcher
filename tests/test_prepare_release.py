import base64
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from cryptography.hazmat.primitives.asymmetric import rsa, padding
from cryptography.hazmat.primitives import hashes

SCRIPT = Path(__file__).resolve().parents[1] / 'deploy' / 'prepare_release.py'
REPO = 'https://github.com/LiuDaCheng666/orange-zomboid-launcher'
OWNED = ['橙子启动器.exe', '橙子更新助手.exe', '卸载橙子启动器.exe', 'VERSION', 'CHANGELOG.md', '使用说明.txt', 'release-public-key.xml', 'Web/index.html', 'Web/app.js', 'Web/app.css', 'Web/home.png', 'Web/orange-app.ico', 'Web/WebView2-LICENSE.txt', 'Web/Microsoft.Web.WebView2.Core.dll', 'Web/Microsoft.Web.WebView2.WinForms.dll', 'Web/WebView2Loader.dll', 'Assets/OrangeShootPresentation.jar', 'Assets/OrangeLaunchBridge.lua']

class PrepareReleaseTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.key = rsa.generate_private_key(public_exponent=65537, key_size=2048)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'; self.source.mkdir()
        self.output = self.root / 'output'
        numbers = self.key.public_key().public_numbers()
        b64 = lambda n: base64.b64encode(n.to_bytes((n.bit_length()+7)//8, 'big')).decode()
        self.public = self.root / 'public.xml'
        self.public.write_text(f'<RSAKeyValue><Modulus>{b64(numbers.n)}</Modulus><Exponent>{b64(numbers.e)}</Exponent></RSAKeyValue>')
        self.manifest = dict(schema=1, version='1.2.13', platform='win-x64', minimumUpdaterVersion='1.0.0', notes='# 更新\n\n## 1.2.13\n\n- Current change\n\n## 1.2.12\n- Old change', files=[])
        self.release = dict(tag_name='v1.2.13', draft=False, prerelease=False, published_at='2026-10-20T11:48:52Z', assets=[])
        for name in OWNED:
            content = ('fixture ' + name).encode()
            digest = hashlib.sha256(content).hexdigest()
            asset = digest + '.bin'
            self.add_asset(asset, content)
            self.manifest['files'].append(dict(path=name, sha256=digest.upper(), length=len(content), url=f'{REPO}/releases/download/v1.2.13/{asset}'))
        self.add_asset('orange-launcher-setup-v1.2.13.exe', b'installer fixture, never executed')
        self.add_asset('orange-launcher-v1.2.13-win-x64.zip', b'portable fixture, never executed')
        self.sign()

    def add_asset(self, name, content):
        (self.source / name).write_bytes(content)
        item = dict(name=name, size=len(content), digest='sha256:' + hashlib.sha256(content).hexdigest(), browser_download_url=f'{REPO}/releases/download/v1.2.13/{name}')
        self.release['assets'] = [a for a in self.release['assets'] if a['name'] != name] + [item]

    def sign(self):
        raw = json.dumps(self.manifest, ensure_ascii=False, separators=(',', ':')).encode()
        self.add_asset('update-manifest.json', raw)
        self.add_asset('update-manifest.sig', self.key.sign(raw, padding.PKCS1v15(), hashes.SHA256()))

    def run_prepare(self):
        metadata = self.root / 'github.json'; metadata.write_text(json.dumps(self.release))
        return subprocess.run([sys.executable, str(SCRIPT), '--release-metadata', str(metadata), '--source', str(self.source), '--output', str(self.output), '--public-key', str(self.public)], text=True, capture_output=True)

    def test_signed_release_is_copied_byte_identically_and_latest_is_ready(self):
        result = self.run_prepare()
        self.assertEqual(result.returncode, 0, result.stderr)
        for item in self.release['assets']:
            self.assertEqual((self.output / 'releases/v1.2.13' / item['name']).read_bytes(), (self.source / item['name']).read_bytes())
        self.assertEqual((self.output / 'updates/latest/update-manifest.json').read_bytes(), (self.source / 'update-manifest.json').read_bytes())
        display = json.loads((self.output / 'release.json').read_text())
        self.assertEqual(display['setup']['url'], '/releases/v1.2.13/orange-launcher-setup-v1.2.13.exe')
        self.assertEqual(display['notes'], ['Current change'])
        second = self.run_prepare()
        self.assertNotEqual(second.returncode, 0, 'never overwrite an immutable output tree')

    def test_invalid_signature_does_not_publish_anything(self):
        self.add_asset('update-manifest.sig', b'X' * 256)
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_altered_content_and_missing_files_do_not_publish_anything(self):
        file = self.source / self.release['assets'][0]['name']
        original = file.read_bytes(); file.write_bytes(original + b'bad')
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())
        file.unlink()
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_signed_wrong_version_resource_url_is_rejected(self):
        self.manifest['files'][0]['url'] = self.manifest['files'][0]['url'].replace('/v1.2.13/', '/v1.2.12/')
        self.sign()
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_parent_path_or_foreign_download_is_rejected(self):
        name = self.release['assets'][0]['name']
        self.release['assets'][0]['name'] = '../secret'
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())
        self.release['assets'][0]['name'] = name
        self.release['assets'][0]['browser_download_url'] = 'https://evil.example/file'
        self.assertNotEqual(self.run_prepare().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_deployment_uses_regenerated_verified_tree_not_untrusted_mirror_extras(self):
        self.assertEqual(self.run_prepare().returncode, 0)
        (self.output / 'releases/v1.2.13/unlisted-private.txt').write_text('must never be published')
        (self.output / 'release.json').write_text('{"version":"1.2.13","setup":{"url":"https://evil.example"}}')
        (self.output / 'updates/latest/update-manifest.json').write_text('stale or altered latest')
        spec = importlib.util.spec_from_file_location('deploy_site', SCRIPT.parent / 'deploy_site.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        self.assertTrue(hasattr(module, 'prepare_mirror'), 'deployment must regenerate its actual upload tree')
        checked = self.root / 'checked'
        module.prepare_mirror(self.release, self.output, checked, self.public.read_bytes())
        self.assertFalse((checked / 'releases/v1.2.13/unlisted-private.txt').exists())
        self.assertEqual((checked / 'updates/latest/update-manifest.json').read_bytes(), (self.source / 'update-manifest.json').read_bytes())
        self.assertEqual(json.loads((checked / 'release.json').read_text())['setup']['url'], '/releases/v1.2.13/orange-launcher-setup-v1.2.13.exe')

if __name__ == '__main__':
    unittest.main()
