#!/usr/bin/env python3
"""One-off recovery for the reviewed 2026-10-26 failed first install only.

Archives four exact owned paths. No deletion, reinstallation or nginx changes.
Run only after the root task approves this helper and the readiness repair.
"""
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import sys

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
from install import Installer, atomic, file_sha, sha

FAILED_STATE = '/var/backups/ozl-feedback/20261026T090323Z-68d89fb9/state.json'
ORIGINAL_SHA = '6681922b442acf02274b305e756ab7b6f5df4c323fda1a04df02f0eb137c5a3f'
PATCHED_SHA = '675a503e00845528250406cffaa46eb169415c73d6a68142c3394d07532f456a'
SOURCES = {
    '/opt/ozl-feedback': {
        'server.py': 'a254692f0ff619a9968823c20f2e113b8dce9b8e754fdc4075d28edb49cac017',
        'install.py': '806381c6dc8a824053054cb85070de3aec45689531acc85ecdf58ce3b830f873',
        'configuration.py': '797496e604764474400f6946841908030f39a1d35aa2871f6e71f736ef535f7c'},
    '/etc/ozl-feedback': {
        'nginx-http.conf': '0f79ffdc8873e6d9a264ae5e37908699894920b8a789844a0dd6982172cfca4b',
        'nginx-location.conf': 'e78086d48fea494ebbcff1affad71394f3bf49d5396e033d45bd195f1267a535'}}
UNIT_SHA = 'a809e3722090f461c06cc0f358110317ab47a6c026750f8e559721d96ed7a47d'


def no_links(path):
    for candidate in [path] + list(path.parents):
        if candidate.is_symlink():
            raise RuntimeError('Symlink in an owned recovery path: ' + str(candidate))


class ArchiveFailed:
    def __init__(self, root='/', uid=None, gid=None):
        self.root = Path(root).resolve()
        self.uid, self.gid = uid, gid
        self.installer = Installer(self.root)

    def path(self, absolute):
        path = self.root / absolute.lstrip('/')
        no_links(path)
        if self.root not in path.parents:
            raise RuntimeError('Recovery path escapes its fixed root')
        return path

    def command(self, args, check=False):
        return subprocess.run(args, check=check, timeout=20, stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE, universal_newlines=True)

    def service_safe(self):
        active = self.command(['systemctl', 'is-active', 'ozl-feedback'])
        enabled = self.command(['systemctl', 'is-enabled', 'ozl-feedback'])
        if active.returncode != 3 or active.stdout.strip() != 'inactive' or enabled.returncode != 1 or enabled.stdout.strip() != 'disabled':
            raise RuntimeError('Feedback must be inactive and disabled before archival')

    def metadata(self, path, mode, uid=0, gid=0):
        no_links(path)
        stat = path.stat()
        if os.name == 'posix' and (stat.st_mode & 0o777 != mode or stat.st_uid != uid or stat.st_gid != gid):
            raise RuntimeError('Owned path permission/owner changed: ' + str(path))

    def inventory(self):
        paths = []
        for absolute, files in SOURCES.items():
            directory = self.path(absolute)
            self.metadata(directory, 0o755)
            if set(path.name for path in directory.iterdir()) != set(files):
                raise RuntimeError('Unexpected file in owned source directory: ' + absolute)
            for name, digest in files.items():
                path = directory / name
                self.metadata(path, 0o644)
                if not path.is_file() or file_sha(path) != digest:
                    raise RuntimeError('Reviewed source changed: ' + str(path))
            paths.append(directory)
        unit = self.path('/etc/systemd/system/ozl-feedback.service')
        self.metadata(unit, 0o644)
        if file_sha(unit) != UNIT_SHA:
            raise RuntimeError('Reviewed feedback unit changed')
        store = self.path('/var/lib/ozl-feedback')
        self.metadata(store, 0o700, self.uid, self.gid)
        if set(path.name for path in store.iterdir()) != {'reports', 'ip-key', 'receipts.sqlite3'}:
            raise RuntimeError('Unexpected file in owned feedback storage')
        reports = store / 'reports'
        self.metadata(reports, 0o700, self.uid, self.gid)
        if list(reports.iterdir()):
            raise RuntimeError('Report storage is not empty; never archive user submissions for a retry')
        key = store / 'ip-key'
        self.metadata(key, 0o600, self.uid, self.gid)
        if key.stat().st_size != 32:
            raise RuntimeError('Unexpected private key shape')
        database = store / 'receipts.sqlite3'
        self.metadata(database, 0o600, self.uid, self.gid)
        db = sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)
        try:
            if db.execute('PRAGMA quick_check').fetchone()[0] != 'ok' or db.execute('SELECT COUNT(*) FROM receipts').fetchone()[0] != 0:
                raise RuntimeError('Accepted feedback exists or database is not intact')
        finally:
            db.close()
        # Keep the unit path present until the last rename, so repeated
        # is-enabled checks can still verify its exact disabled state.
        paths.extend([store, unit])
        return paths

    def archive(self):
        state_path = self.path(FAILED_STATE)
        self.metadata(state_path, 0o600)
        state = json.loads(state_path.read_text(encoding='utf-8'))
        backup = state_path.parent
        self.metadata(backup, 0o700)
        if (set(state) != {'phase', 'original_sha256', 'patched_sha256', 'backup'} or state['phase'] != 'failed'
                or state['original_sha256'] != ORIGINAL_SHA or state['patched_sha256'] != PATCHED_SHA
                or state['backup'] != str(backup)):
            raise RuntimeError('Failed-install state does not match the one reviewed attempt')
        live = self.path('/etc/ozl-http-guard/nginx.conf')
        if sha(live.read_bytes()) != ORIGINAL_SHA:
            raise RuntimeError('Guard configuration is not the verified rolled-back original')
        self.service_safe()
        expected = json.loads((backup / 'protected-sha256.json').read_text(encoding='utf-8'))
        if self.installer.protected() != expected:
            raise RuntimeError('Protected content differs from the failed-install baseline')
        paths = self.inventory()
        destination = backup / 'failed-install'
        no_links(destination)
        if destination.exists():
            raise RuntimeError('Archival destination already exists; no overwrite allowed')
        if any(path.stat().st_dev != backup.stat().st_dev for path in paths):
            raise RuntimeError('Archival would cross a filesystem; refusing non-atomic copy')
        identities = {str(path): (path.stat().st_dev, path.stat().st_ino) for path in paths}
        destination.mkdir(mode=0o700)
        os.chmod(str(destination), 0o700)
        moved = []
        try:
            for path in paths:
                if sha(live.read_bytes()) != ORIGINAL_SHA:
                    raise RuntimeError('Guard configuration changed during archival')
                self.service_safe()
                no_links(path)
                if (path.stat().st_dev, path.stat().st_ino) != identities[str(path)]:
                    raise RuntimeError('Owned path identity changed during archival')
                relative = str(path.relative_to(self.root)).replace('\\', '/')
                target = destination / relative.replace('/', '__')
                if target.exists():
                    raise RuntimeError('Owned archival target unexpectedly exists')
                os.rename(str(path), str(target))
                moved.append((path, target))
            self.command(['systemctl', 'daemon-reload'], check=True)
            if self.installer.protected() != expected or sha(live.read_bytes()) != ORIGINAL_SHA:
                raise RuntimeError('Protected content changed during archival')
            receipt = dict(archived=True, state=str(state_path), paths=[dict(source=str(a), archive=str(b)) for a, b in moved])
            atomic(destination / 'archive-receipt.json', json.dumps(receipt, indent=2).encode('utf-8'), 0o600)
            return receipt
        except BaseException:
            for original, target in reversed(moved):
                if original.exists() or not target.exists():
                    raise RuntimeError('Concurrent owned-path change; manual archival recovery required')
                os.rename(str(target), str(original))
            self.command(['systemctl', 'daemon-reload'], check=True)
            raise


def main():
    if os.name != 'posix' or os.geteuid() != 0:
        raise RuntimeError('This one-off recovery must run as root on the reviewed Linux host')
    import pwd
    account = pwd.getpwnam('ozl-feedback')
    if account.pw_uid == 0:
        raise RuntimeError('Feedback account must remain unprivileged')
    os.umask(0o077)
    print(json.dumps(ArchiveFailed(uid=account.pw_uid, gid=account.pw_gid).archive(), indent=2))


if __name__ == '__main__':
    main()
