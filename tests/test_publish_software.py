"""Release-only publication safety tests; no network access or executable payloads."""
import hashlib
import json
from pathlib import Path
import stat
import sys
from types import SimpleNamespace
import unittest

import test_velopack_release as fixtures

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'deploy'))
import publish_software as publisher


class MemoryRemote:
    def __init__(self, legacy):
        self.current = '/var/www/ozl/sites/existing'
        self.files = {self.current + '/index.html': b'optimized site',
                      self.current + '/release.json': b'{"version":"1.2.14","custom":"preserve"}',
                      '/var/www/ozl/releases/v1.2.14/old.zip': b'old release'}
        for name in publisher.FEED_NAMES:
            self.files[self.current + '/updates/velopack/latest/' + name] = b'old feed'
        self.files[self.current + '/updates/velopack/latest/velopack-manifest.json'] = b'{"version":"1.2.14"}'
        for name in ['update-manifest.json', 'update-manifest.sig']:
            self.files[self.current + '/updates/latest/' + name] = (legacy / name).read_bytes()
        self.directories = {'/var/www/ozl', '/var/www/ozl/releases', '/var/www/ozl/test-runs'}
        self.writes = []
        self.on_snapshot = None

    def resolve_current(self): return self.current
    def exists(self, path): return path in self.files or path in self.directories
    def read(self, path): return self.files[path]
    def names(self, path): return {p[len(path)+1:] for p in self.files if p.startswith(path + '/')}
    def mkdir(self, path): self.writes.append(('mkdir', path)); self.directories.add(path)
    def write(self, path, content): self.writes.append(('write', path)); self.files[path] = content
    def hashes(self, paths): return {p: hashlib.sha256(self.files[p]).hexdigest() for p in paths}
    def rename(self, source, target, replace=False):
        self.writes.append(('rename', target))
        if source in self.directories:
            self.directories.remove(source); self.directories.add(target)
            for path in list(self.files):
                if path.startswith(source + '/'): self.files[target + path[len(source):]] = self.files.pop(path)
        else: self.files[target] = self.files.pop(source)
    def snapshot(self, excluded):
        if self.on_snapshot: self.on_snapshot(self)
        result = {'current': self.current}
        result.update({p: hashlib.sha256(b).hexdigest() for p,b in self.files.items()
                       if not p.startswith('/var/www/ozl/test-runs/')
                       and not any(p == x or p.startswith(x + '/') for x in excluded)})
        return result


class PublishSoftwareTests(fixtures.VelopackReleaseTests):
    def setUp(self):
        super().setUp()
        self.remote = MemoryRemote(self.legacy)
        self.evidence = self.root / 'evidence.json'
        self.connections = 0

    def publish(self, health=None):
        def connect():
            self.connections += 1
            return self.remote
        return publisher.publish(self.source, self.legacy, self.public.read_bytes(),
                                 self.remote.current, self.evidence,
                                 remote_factory=connect,
                                 health_check=health or (lambda *_: None))

    def test_only_approved_mutable_paths_and_immutable_assets_are_published(self):
        before = dict(self.remote.files)
        result = self.publish()
        self.assertEqual(result['status'], 'published')
        self.assertTrue(self.evidence.exists())
        site = self.remote.current
        self.assertEqual(json.loads(self.remote.read(site + '/release.json'))['custom'], 'preserve')
        self.assertEqual(self.remote.read(site + '/index.html'), before[site + '/index.html'])
        changed = {p for p in before if before[p] != self.remote.files[p]}
        self.assertEqual(changed, {site + '/release.json'} | {site + '/updates/velopack/latest/' + n for n in publisher.FEED_NAMES})
        self.assertEqual(len(self.remote.names('/var/www/ozl/releases/v1.2.15')), 10)
        self.assertEqual(result['protected_before'], result['protected_after'])
        for operation,path in self.remote.writes:
            self.assertTrue(path.startswith('/var/www/ozl/test-runs/') or path.startswith('/var/www/ozl/releases/') or path in changed, (operation,path))

    def test_setup_and_portable_custom_nested_metadata_survives_release_merge(self):
        path=self.remote.current+'/release.json'
        old=json.loads(self.remote.read(path))
        old['setup']={'url':'/old-setup.exe','length':1,'buttonLabel':'网站定制安装按钮','tracking':{'source':'site'}}
        old['portable']={'sha256':'old','mirrorLabel':'网站定制便携按钮'}
        self.remote.files[path]=json.dumps(old,ensure_ascii=False).encode()
        self.publish()
        new=json.loads(self.remote.read(path))
        self.assertEqual(new['setup']['buttonLabel'],old['setup']['buttonLabel'])
        self.assertEqual(new['setup']['tracking'],old['setup']['tracking'])
        self.assertEqual(new['portable']['mirrorLabel'],old['portable']['mirrorLabel'])
        self.assertEqual(new['setup']['url'],'/releases/v1.2.15/OrangeZomboidLauncher-win-Setup.exe')
        self.assertEqual(new['setup']['length'],len(self.blobs['OrangeZomboidLauncher-win-Setup.exe']))
        self.assertEqual(new['portable']['sha256'],hashlib.sha256(self.blobs['OrangeZomboidLauncher-win-Portable.zip']).hexdigest().upper())

    def test_unsigned_extra_or_mismatched_candidate_never_connects(self):
        for mode in ['unsigned', 'extra', 'mismatch']:
            with self.subTest(mode=mode):
                self.write_inventory()
                if mode == 'unsigned': (self.source/'velopack-artifacts.sig').write_bytes(b'bad')
                elif mode == 'extra': (self.source/'private.xml').write_bytes(b'private')
                else: (self.source/self.package).write_bytes(b'changed')
                with self.assertRaises(ValueError): self.publish()
                self.assertEqual(self.connections, 0)
                self.assertEqual(self.remote.writes, [])
                (self.source/'private.xml').unlink(missing_ok=True)

    def test_existing_immutable_collision_never_changes_mutable_files(self):
        path='/var/www/ozl/releases/v1.2.15'
        self.remote.directories.add(path); self.remote.files[path+'/foreign.exe']=b'collision'
        before=dict(self.remote.files)
        with self.assertRaisesRegex(ValueError, 'immutable'): self.publish()
        self.assertEqual(before, self.remote.files)

    def test_identical_immutable_release_can_resume(self):
        self.publish()
        self.assertEqual(self.publish()['status'], 'published')

    def test_changed_current_stops_before_activation(self):
        count=[0]
        def change(remote):
            count[0]+=1
            if count[0]==2: remote.current='/var/www/ozl/sites/other'
        self.remote.on_snapshot=change
        old=self.remote.current
        with self.assertRaisesRegex(ValueError, 'protected|current'): self.publish()
        self.assertEqual(json.loads(self.remote.read(old+'/release.json'))['version'], '1.2.14')

    def test_health_failure_restores_only_previous_four_mutable_files(self):
        before=dict(self.remote.files)
        def failed(*_): raise RuntimeError('health failed')
        with self.assertRaisesRegex(RuntimeError, 'health failed'): self.publish(failed)
        self.assertEqual({p:self.remote.files[p] for p in before}, before)
        self.assertEqual(json.loads(self.evidence.read_text())['status'], 'rolled-back')

    def test_uncertain_rename_result_still_restores_previous_mutable_files(self):
        before=dict(self.remote.files)
        original=self.remote.rename
        failed=[False]
        def interrupted(source,target,replace=False):
            original(source,target,replace)
            if '/new-' in source and not failed[0]:
                failed[0]=True
                raise RuntimeError('connection interrupted after rename')
        self.remote.rename=interrupted
        with self.assertRaisesRegex(RuntimeError, 'connection interrupted'): self.publish()
        self.assertEqual({p:self.remote.files[p] for p in before}, before)

    def test_mid_activation_foreign_metadata_edit_is_preserved_and_own_changes_rollback(self):
        before=dict(self.remote.files)
        release_path=self.remote.current+'/release.json'
        foreign=json.loads(before[release_path])
        foreign['custom_survived']='concurrent website optimization'
        foreign_bytes=json.dumps(foreign).encode()
        original=self.remote.rename
        changed=[False]
        def concurrent_edit(source,target,replace=False):
            original(source,target,replace)
            if '/new-' in source and not changed[0]:
                changed[0]=True
                self.remote.files[release_path]=foreign_bytes
        self.remote.rename=concurrent_edit
        with self.assertRaisesRegex(ValueError, 'concurrently'): self.publish()
        self.assertTrue(changed[0])
        self.assertEqual(self.remote.read(release_path),foreign_bytes)
        self.assertEqual({p:self.remote.files[p] for p in before if p!=release_path},
                         {p:b for p,b in before.items() if p!=release_path})
        self.assertEqual(json.loads(self.evidence.read_text())['status'],'rolled-back')

    def test_protected_site_change_blocks_activation(self):
        count=[0]
        def change(remote):
            count[0]+=1
            if count[0]==2: remote.files[remote.current+'/index.html']=b'concurrent site optimization'
        self.remote.on_snapshot=change
        with self.assertRaisesRegex(ValueError, 'protected'): self.publish()
        self.assertEqual(json.loads(self.remote.read(self.remote.current+'/release.json'))['version'], '1.2.14')

    def test_feed_downgrade_is_rejected_even_if_display_version_is_older(self):
        self.remote.files[self.remote.current+'/updates/velopack/latest/velopack-manifest.json']=b'{"version":"1.2.99"}'
        with self.assertRaisesRegex(ValueError, 'downgrade'): self.publish()
        self.assertEqual(self.remote.writes, [])

    def test_downgrade_and_legacy_changes_are_rejected_before_writes(self):
        path=self.remote.current+'/release.json'
        self.remote.files[path]=b'{"version":"2.0.0"}'
        with self.assertRaisesRegex(ValueError, 'downgrade'): self.publish()
        self.assertEqual(self.remote.writes, [])
        self.remote.files[path]=b'{"version":"1.2.14"}'
        self.remote.files[self.remote.current+'/updates/latest/update-manifest.json']=b'changed'
        with self.assertRaisesRegex(ValueError, 'legacy'): self.publish()
        self.assertEqual(self.remote.writes, [])


class SftpSafetyTests(unittest.TestCase):
    def test_writes_and_renames_outside_exact_whitelist_are_rejected(self):
        remote=object.__new__(publisher.SftpRemote)
        remote.configure([],[],[])
        for action in [lambda:remote.write('/etc/caddy/Caddyfile',b'bad'),
                       lambda:remote.mkdir('/var/www/ozl/sites/new'),
                       lambda:remote.rename('/tmp/stage','/var/www/ozl/current',True)]:
            with self.assertRaisesRegex(ValueError,'whitelist'): action()

    def test_symlink_parent_is_rejected_before_open(self):
        remote=object.__new__(publisher.SftpRemote)
        path='/var/www/ozl/test-runs/test/new-0'
        remote.configure([path],[],[])
        class SymlinkSftp:
            def lstat(self,path):
                return SimpleNamespace(st_mode=(stat.S_IFLNK if path.endswith('/test-runs') else stat.S_IFDIR)|0o755)
        remote.sftp=SymlinkSftp()
        with self.assertRaisesRegex(ValueError,'symlink'): remote.write(path,b'no')

    def test_snapshot_remote_program_compiles_and_never_reads_ssh_keys(self):
        script=publisher.REMOTE_SNAPSHOT.replace('EXCLUDED',repr([]))
        compile(script,'remote-snapshot','exec')
        self.assertNotIn("'/etc/ssh'",script)
        self.assertIn("'/etc/ssh/sshd_config'",script)


# Reuse signed fixture setup without rerunning its independent preparation tests.
for name in list(fixtures.VelopackReleaseTests.__dict__):
    if name.startswith('test_'): setattr(PublishSoftwareTests, name, None)
# Restore this class's own tests after hiding inherited ones (names above differ).

if __name__ == '__main__': unittest.main()
