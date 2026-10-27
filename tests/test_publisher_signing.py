"""Real publisher CLI preflight; never connects to SSH or uploads a file."""
import json
import base64
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'deploy'))
import publish_software as publisher


@unittest.skipUnless(os.name == 'nt', 'Windows WinTrust/SignTool preflight')
class PublisherSigningTests(unittest.TestCase):
    def test_windows_verifier_uses_windows_modules_in_an_inherited_pwsh_environment(self):
        self.assertTrue(hasattr(publisher, 'windows_verification_environment'),
                        'Windows verifier must isolate inherited PowerShell 7 modules')
        with tempfile.TemporaryDirectory(prefix='ozl-windows-module-') as name:
            path = Path(name) / 'bytes.txt'
            path.write_bytes(b'Windows verifier file hashing')
            script = Path(name) / 'hash.ps1'
            script.write_text('param([string]$p)\n(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash')
            result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File', str(script), str(path)],
                env=publisher.windows_verification_environment(), capture_output=True, text=True, errors='replace')
            import hashlib
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertIn(hashlib.sha256(path.read_bytes()).hexdigest().upper(), result.stdout)

    def test_standalone_unsigned_build_requires_rsa_without_a_windows_certificate(self):
        repo = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory(prefix='ozl-build-entry-') as name:
            root = Path(name)
            result = subprocess.run(['powershell.exe', '-NoProfile', '-File',
                str(repo / 'Build-VelopackRelease.ps1'), '-AllowUnsignedRelease',
                '-SigningKey', str(root / 'missing-update-key.xml'), '-OutputDirectory', str(root / 'output')],
                capture_output=True, text=True, encoding='utf-8', errors='replace')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('missing-update-key.xml', result.stdout + result.stderr)
            self.assertNotIn('Join-Path', result.stdout + result.stderr)
            self.assertFalse((root / 'output').exists())

    def test_explicit_unsigned_publication_still_authenticates_rsa_before_ssh(self):
        repo = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory(prefix='ozl-unsigned-publication-') as name:
            root = Path(name)
            source = root / 'release'
            source.mkdir()
            (source / 'velopack-artifacts.json').write_bytes(b'{}')
            (source / 'velopack-artifacts.sig').write_bytes(b'not-an-rsa-signature')
            (source / 'velopack-manifest.json').write_text(json.dumps({'version': '1.2.34'}))
            modulus = ET.fromstring((repo / 'release-public-key.xml').read_text()).findtext('Modulus')
            with zipfile.ZipFile(source / 'OrangeZomboidLauncher-1.2.34-full.nupkg', 'w') as archive:
                archive.writestr('lib/app/velopack-bootstrap.json', b'{}')
                archive.writestr('lib/app/velopack-bootstrap.sig', bytes(len(base64.b64decode(modulus))))
            evidence = root / 'publication.json'
            result = subprocess.run([
                sys.executable, str(repo / 'deploy' / 'publish_software.py'),
                '--source', str(source), '--preserved-legacy', str(root / 'unused'),
                '--expected-current', '/var/www/ozl/sites/test-never-connect', '--evidence', str(evidence),
                '--allow-unsigned-release'], capture_output=True, text=True, encoding='utf-8', errors='replace')
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn('Packaged bootstrap signature failed', result.stdout + result.stderr)
            self.assertNotIn('unrecognized arguments', result.stdout + result.stderr)
            self.assertFalse(evidence.exists())
            self.assertFalse(evidence.with_suffix('.authenticode.json').exists())

    def test_missing_publisher_identity_fails_before_publication(self):
        repo = Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory(prefix='ozl-publish-signing-') as name:
            root = Path(name)
            source = root / 'release'
            source.mkdir()
            (source / 'velopack-manifest.json').write_text(json.dumps({'version': '1.2.34'}))
            evidence = root / 'publication.json'
            result = subprocess.run([
                sys.executable, str(repo / 'deploy' / 'publish_software.py'),
                '--source', str(source), '--preserved-legacy', str(root / 'unused'),
                '--expected-current', 'invalid-current-never-connect', '--evidence', str(evidence),
                '--authenticode-config', str(root / 'missing-signing.json')],
                capture_output=True, text=True, encoding='utf-8', errors='replace')
            self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
            self.assertIn('Windows code-signing configuration is missing', result.stdout + result.stderr)
            self.assertFalse(evidence.exists(), 'No SSH publication receipt is created')
            self.assertFalse(evidence.with_suffix('.authenticode.json').exists())


if __name__ == '__main__':
    unittest.main()
