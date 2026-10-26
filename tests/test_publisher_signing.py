"""Real publisher CLI preflight; never connects to SSH or uploads a file."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


@unittest.skipUnless(os.name == 'nt', 'Windows WinTrust/SignTool preflight')
class PublisherSigningTests(unittest.TestCase):
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
