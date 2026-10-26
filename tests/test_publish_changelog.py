"""Changelog-only publication: preserve the live page and concurrent edits."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'deploy'))
import publish_changelog as publisher


PAGE = '''<!doctype html><head><title>Optimized</title><link href="site.css"></head>
<article><h1>更新记录</h1>
<section class="change-entry">
<span class="pill">当前稳定版</span><h2>1.2.16</h2><p>old text</p>
</section>
<section class="change-entry">
<span class="pill">历史版本</span><h2>1.2.15</h2><p>older text</p>
</section>
</article><footer>Keep this optimization</footer>'''.encode('utf-8')
NOTES = '''# 更新记录
## 1.2.31 — 2026-10-27
- 修复更新 & 保留 <资料>。
## 1.2.30 — 2026-10-27
- 修复旧会话。
## 1.2.16 — 2026-10-25
- Do not replace existing copy.
'''
RELEASES = [dict(tag_name='v1.2.31', published_at='2026-10-26T18:10:00Z', draft=False, prerelease=False),
            dict(tag_name='v1.2.30', published_at='2026-10-26T18:09:00Z', draft=False, prerelease=False)]


class Remote:
    current = '/var/www/ozl/sites/existing'

    def __init__(self):
        self.files = {self.current + '/changes.html': PAGE,
                      self.current + '/index.html': b'foreign home optimization',
                      self.current + '/site.css': b'foreign stylesheet',
                      self.current + '/release.json': b'{"version":"1.2.31"}'}
        self.allowed = set()
        self.renamed = []
        self.closed = False

    def resolve_current(self): return self.current
    def read(self, path): return self.files[path]
    def configure(self, files, directories, renames): self.allowed = set(renames)
    def mkdir(self, path): pass
    def write(self, path, content): self.files[path] = content
    def hashes(self, paths): return {path:publisher.sha(self.files[path]) for path in paths}
    def rename(self, source, target, replace=False):
        assert (source, target, replace) in self.allowed
        self.files[target] = self.files.pop(source)
        self.renamed.append(target)
    def replace_checked(self, source, target, expected_hash, current, version=None):
        if publisher.sha(self.files[target]) != expected_hash: raise RuntimeError('Concurrent edit')
        if version is not None and json.loads(self.files[current+'/release.json'])['version'] != version:
            raise RuntimeError('Version changed')
        self.rename(source, target, replace=True)
    def snapshot(self, excluded):
        return {path:publisher.sha(data) for path,data in self.files.items()
                if path not in excluded and not path.startswith('/var/www/ozl/test-runs/')}
    def close(self): self.closed = True


class ChangelogTests(unittest.TestCase):
    def test_missing_published_versions_inserted_without_replacing_live_markup(self):
        result = publisher.render(PAGE, NOTES, RELEASES, '1.2.31')
        self.assertTrue(result.startswith(PAGE[:PAGE.index(b'<section')]))
        old_cards = PAGE[PAGE.index(b'<section'):].replace('当前稳定版'.encode(), '历史版本'.encode())
        self.assertTrue(result.endswith(old_cards))
        self.assertEqual(result.count(b'<section class="change-entry">'), 4)
        self.assertEqual(result.count('当前稳定版'.encode()), 1)
        self.assertLess(result.index(b'<h2>1.2.31</h2>'), result.index(b'<h2>1.2.30</h2>'))
        self.assertIn(b'datetime="2026-10-27"', result)
        self.assertIn('&amp; 保留 &lt;资料&gt;'.encode(), result)
        self.assertNotIn(b'Do not replace', result)
        self.assertEqual(publisher.render(result, NOTES, RELEASES, '1.2.31'), result)

    def test_missing_notes_and_downgrade_fail_before_writing(self):
        with self.assertRaises(ValueError): publisher.render(PAGE, '', RELEASES, '1.2.31')
        with self.assertRaises(ValueError): publisher.render(PAGE, NOTES, RELEASES, '1.2.14')

    def publish(self, remote, health):
        with tempfile.TemporaryDirectory() as folder:
            evidence = Path(folder) / 'receipt.json'
            result = publisher.publish(NOTES, RELEASES, '1.2.31', remote.current, evidence,
                                       remote_factory=lambda:remote, health_check=health)
            self.assertEqual(json.loads(evidence.read_text(encoding='utf-8'))['status'], result['status'])
            return result

    def test_only_history_changes_and_a_backup_is_retained(self):
        remote = Remote(); before = dict(remote.files)
        result = self.publish(remote, lambda content:None)
        self.assertEqual(result['status'], 'published')
        self.assertEqual(remote.renamed, [remote.current + '/changes.html'])
        for path,data in before.items():
            if not path.endswith('/changes.html'): self.assertEqual(remote.files[path], data)
        self.assertEqual(remote.files[result['backup']], PAGE)
        self.assertEqual(result['protected_before'], result['protected_after'])
        self.assertTrue(remote.closed)

    def test_failed_public_check_restores_only_our_history(self):
        remote = Remote()
        def fail(_): raise RuntimeError('HTTP failed')
        with self.assertRaises(RuntimeError): self.publish(remote, fail)
        self.assertEqual(remote.files[remote.current + '/changes.html'], PAGE)

    def test_concurrent_history_edit_is_not_rolled_back(self):
        remote = Remote()
        def fail(_):
            remote.files[remote.current + '/changes.html'] = b'newer editor copy'
            raise RuntimeError('another editor changed history')
        with self.assertRaises(RuntimeError): self.publish(remote, fail)
        self.assertEqual(remote.files[remote.current + '/changes.html'], b'newer editor copy')

    def test_edit_arriving_at_activation_is_preserved(self):
        class Edited(Remote):
            def replace_checked(self, *args, **kwargs):
                self.files[self.current+'/changes.html'] = b'editor at activation'
                super().replace_checked(*args, **kwargs)
        remote = Edited()
        with self.assertRaises(RuntimeError): self.publish(remote, lambda _:None)
        self.assertEqual(remote.files[remote.current+'/changes.html'], b'editor at activation')

    def test_edit_arriving_during_rollback_upload_is_preserved(self):
        class Edited(Remote):
            def write(self, path, content):
                super().write(path, content)
                if path.endswith('/rollback.html'):
                    self.files[self.current+'/changes.html'] = b'editor during rollback'
        remote = Edited()
        def fail(_): raise RuntimeError('HTTP failed')
        with self.assertRaises(RuntimeError): self.publish(remote, fail)
        self.assertEqual(remote.files[remote.current+'/changes.html'], b'editor during rollback')

    def test_release_change_before_first_baseline_is_rejected(self):
        class Advanced(Remote):
            def snapshot(self, excluded):
                self.files[self.current+'/release.json'] = b'{"version":"1.2.32"}'
                return super().snapshot(excluded)
        remote = Advanced()
        with self.assertRaises(ValueError): self.publish(remote, lambda _:None)
        self.assertEqual(remote.files[remote.current+'/changes.html'], PAGE)
        self.assertEqual(remote.renamed, [])


if __name__ == '__main__': unittest.main()
