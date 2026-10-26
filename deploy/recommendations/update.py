#!/usr/bin/env python3
"""Update only the four owned recommendation service/UI files; never web config."""
import argparse
from contextlib import contextmanager
import json
import os
from pathlib import Path
import re
import stat
import uuid

from install import BUNDLE, CODE, CURRENT, FEED, SERVICE, UNIT, Installer, atomic, file_sha, identity, sha

UPDATE_FILES = ("server.py", "static/admin.html", "static/admin.css", "static/admin.js")


class Updater(Installer):
    def ordinary(self, path, directory=False):
        value = path.lstat()
        if stat.S_ISLNK(value.st_mode) or getattr(value, "st_file_attributes", 0) & 1024:
            raise ValueError("Owned update path became a link/reparse point")
        if (directory and not stat.S_ISDIR(value.st_mode)) or (not directory and not stat.S_ISREG(value.st_mode)):
            raise ValueError("Owned update path has an unexpected type")
        if os.name != "nt" and self.root == Path("/") and (value.st_uid != 0 or value.st_mode & 0o022):
            raise ValueError("Owned program/receipt must be root-owned and not writable by others")
        parent = path.parent
        while parent != parent.parent:
            entry = parent.lstat()
            if not stat.S_ISDIR(entry.st_mode) or stat.S_ISLNK(entry.st_mode) or getattr(entry, "st_file_attributes", 0) & 1024:
                raise ValueError("Owned path contains a linked/reparse ancestor")
            if parent == self.root:
                break
            parent = parent.parent

    def state(self):
        code = self.path(CODE)
        self.ordinary(code, True)
        self.ordinary(code / "static", True)
        manifest = code / "install-state.json"
        self.ordinary(manifest)
        payload = manifest.read_bytes()
        if len(payload) > 1048576:
            raise ValueError("Installation receipt exceeds update budget")
        value = json.loads(payload.decode("utf-8"))
        current = self.path(CURRENT).resolve(strict=True)
        expected = {str(self.path(UNIT))} | {str(code / name) for name in BUNDLE if name != "ozl-recommendations.service"}
        if value.get("version") != 1 or value.get("phase") != "installed" or set(value.get("files", {})) != expected:
            raise ValueError("Update requires a complete successful owned installation")
        if value.get("directories", {}).get(CODE) != identity(code) or value.get("current") != str(current) or value.get("currentIdentity") != identity(current):
            raise ValueError("Installed program/current physical identity changed")
        self.validate_route(value)
        return value, payload

    def validate_route(self, value):
        route = Path(value["current"]) / "api/recommended-servers"
        self.ordinary(route.parent, True)
        if value.get("route") != str(route) or not route.is_symlink() or os.readlink(str(route)) != FEED or identity(route.parent) != value.get("apiIdentity"):
            raise ValueError("Installed public route changed; update preserved it")

    def pending(self):
        folder = self.path(CODE + "/update-backups")
        if not folder.exists():
            return
        self.ordinary(folder, True)
        entries = list(folder.iterdir())
        if len(entries) > 256:
            raise ValueError("Update receipt inventory exceeds budget")
        for item in entries:
            if not re.fullmatch(r"[a-f0-9]{32}", item.name):
                raise ValueError("Unknown update backup requires inspection")
            record, _ = self.read_record(item.name)
            if record.get("phase") not in ("updated", "rolledBack"):
                raise ValueError("An interrupted update requires rollback-update --update-id " + item.name)

    def audit(self):
        self.pending()
        state, payload = self.state()
        for name, expected in state["files"].items():
            self.ordinary(Path(name))
            if not re.fullmatch(r"[a-f0-9]{64}", expected) or file_sha(Path(name)) != expected:
                raise ValueError("Installed source/unit differs from its ownership receipt")
        result = super().audit()
        result["installedSha256"] = sha(json.dumps({"files": state["files"], "receiptSha256": sha(payload)}, sort_keys=True).encode("utf-8"))
        result["installedFiles"] = dict(state["files"])
        return result

    def preflight_update(self, expected_current, expected_hash, expected_installed):
        result = self.audit()
        if result["current"] != expected_current or result["protectedSha256"] != expected_hash or result["installedSha256"] != expected_installed:
            raise ValueError("Current site/protected files/installed code changed since the explicit audit")
        return result

    @contextmanager
    def lock(self):
        path = self.path(CODE + "/update.lock")
        if path.exists() or path.is_symlink():
            self.ordinary(path)
        descriptor = os.open(str(path), os.O_RDWR | os.O_CREAT | getattr(os, "O_NOFOLLOW", 0), 0o600)
        with os.fdopen(descriptor, "r+b") as stream:
            try:
                if os.name == "nt":
                    import msvcrt
                    stream.write(b"0"); stream.flush(); stream.seek(0)
                    msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
                else:
                    import fcntl
                    fcntl.flock(stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
            except OSError:
                raise ValueError("Another recommendation operator owns the update lock")
            try:
                yield
            finally:
                if os.name == "nt":
                    stream.seek(0)
                    msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
                else:
                    fcntl.flock(stream.fileno(), fcntl.LOCK_UN)

    def save_record(self, folder, value):
        atomic(folder / "update-state.json", json.dumps(value, indent=2, sort_keys=True).encode("utf-8"), 0o600)
        if os.name != "nt":
            # Persist new backup/parent directory entries before any service stop.
            for parent in (folder.parent, folder.parent.parent):
                descriptor = os.open(str(parent), os.O_RDONLY | os.O_DIRECTORY)
                try:
                    os.fsync(descriptor)
                finally:
                    os.close(descriptor)

    def read_record(self, update_id):
        if not re.fullmatch(r"[a-f0-9]{32}", update_id):
            raise ValueError("Update ID must be the exact retained 32-character receipt")
        folder = self.path(CODE + "/update-backups/" + update_id)
        self.ordinary(folder.parent, True)
        self.ordinary(folder, True)
        self.ordinary(folder / "update-state.json")
        raw = (folder / "update-state.json").read_bytes()
        if len(raw) > 1048576:
            raise ValueError("Update receipt exceeds budget")
        value = json.loads(raw.decode("utf-8"))
        if value.get("version") != 1 or value.get("updateId") != update_id or value.get("backupIdentity") != identity(folder) or set(value.get("before", {})) != set(UPDATE_FILES) or set(value.get("after", {})) != set(UPDATE_FILES):
            raise ValueError("Update receipt is incomplete or has foreign paths")
        return value, folder

    def update(self, bundle, expected_current, expected_hash, expected_installed):
        with self.lock():
            self.preflight_update(expected_current, expected_hash, expected_installed)
            state, before_manifest = self.state()
            content = {}
            for name in UPDATE_FILES:
                source = Path(bundle) / name
                if source.is_symlink() or not source.is_file() or source.stat().st_size > 1048576:
                    raise ValueError("Reviewed update file is missing, linked or too large")
                content[name] = source.read_bytes()
            before = {name: state["files"][str(self.path(CODE + "/" + name))] for name in UPDATE_FILES}
            after = {name: sha(content[name]) for name in UPDATE_FILES}
            if before == after:
                return {"status": "unchanged", "protectedUnchanged": True}
            update_id = uuid.uuid4().hex
            parent = self.path(CODE + "/update-backups")
            if not parent.exists():
                parent.mkdir(mode=0o700)
            self.ordinary(parent, True)
            folder = parent / update_id
            folder.mkdir(mode=0o700)
            (folder / "static").mkdir(mode=0o700)
            for name in UPDATE_FILES:
                atomic(folder / name, self.path(CODE + "/" + name).read_bytes(), 0o600)
            atomic(folder / "install-state.before.json", before_manifest, 0o600)
            for name in UPDATE_FILES:
                state["files"][str(self.path(CODE + "/" + name))] = after[name]
            state["lastUpdateId"] = update_id
            after_manifest = json.dumps(state, indent=2, sort_keys=True).encode("utf-8")
            record = {"version": 1, "updateId": update_id, "phase": "prepared", "backupIdentity": identity(folder),
                      "before": before, "after": after, "beforeManifestSha256": sha(before_manifest),
                      "afterManifestSha256": sha(after_manifest), "current": expected_current,
                      "currentIdentity": state["currentIdentity"], "protectedSha256": expected_hash}
            self.save_record(folder, record)
            try:
                # No writes to any live source before a complete durable recovery receipt.
                state_now, raw_now = self.state()
                if sha(raw_now) != record["beforeManifestSha256"] or super().audit()["protectedSha256"] != expected_hash:
                    raise ValueError("Update boundary changed while preparing its receipt")
                for name in UPDATE_FILES:
                    if file_sha(self.path(CODE + "/" + name)) != before[name]:
                        raise ValueError("Owned source changed while preparing its receipt")
                self.run(["systemctl", "stop", SERVICE])
                stopped_state, stopped_raw = self.state()
                if sha(stopped_raw) != record["beforeManifestSha256"]:
                    raise ValueError("Ownership receipt changed during service stop")
                for name, expected in stopped_state["files"].items():
                    self.ordinary(Path(name))
                    if file_sha(Path(name)) != expected:
                        raise ValueError("Owned source changed during service stop; preserved it")
                record["phase"] = "applying"
                self.save_record(folder, record)
                for name in UPDATE_FILES:
                    atomic(self.path(CODE + "/" + name), content[name], 0o644)
                atomic(self.path(CODE + "/install-state.json"), after_manifest, 0o600)
                self.run(["systemctl", "start", SERVICE])
                self.wait_health()
                if super().audit()["protectedSha256"] != expected_hash:
                    raise ValueError("Protected site/configuration changed during update")
                record["phase"] = "updated"
                self.save_record(folder, record)
                return {"status": "updated", "updateId": update_id, "backup": CODE + "/update-backups/" + update_id,
                        "protectedUnchanged": True, "installedSha256": self.audit()["installedSha256"]}
            except BaseException as error:
                try:
                    self.restore(record, folder)
                except BaseException:
                    raise RuntimeError("Update interrupted; rollback requires inspection: " + update_id) from error
                raise

    def validate_restore(self, record, folder):
        # All validation precedes service stop; fixed filenames only, no DB/feed access.
        state, raw = self.state()
        if state["current"] != record["current"] or state["currentIdentity"] != record["currentIdentity"] or sha(raw) not in (record["beforeManifestSha256"], record["afterManifestSha256"]):
            raise ValueError("Later installation/site state differs; rollback preserved it")
        backup_manifest = folder / "install-state.before.json"
        self.ordinary(backup_manifest)
        before_manifest = backup_manifest.read_bytes()
        if sha(before_manifest) != record["beforeManifestSha256"]:
            raise ValueError("Saved ownership receipt changed")
        original = json.loads(before_manifest.decode("utf-8"))
        if set(original.get("files", {})) != set(state["files"]):
            raise ValueError("Saved ownership receipt contains foreign paths")
        for name in UPDATE_FILES:
            if original["files"][str(self.path(CODE + "/" + name))] != record["before"][name] or not re.fullmatch(r"[a-f0-9]{64}", record["after"][name]):
                raise ValueError("Saved source hashes disagree with the ownership receipt")
        for name, expected in original["files"].items():
            path = Path(name)
            relative = next((item for item in UPDATE_FILES if path == self.path(CODE + "/" + item)), None)
            self.ordinary(path)
            allowed = (record["before"][relative], record["after"][relative]) if relative else (expected,)
            if file_sha(path) not in allowed:
                raise ValueError("Foreign/newer source changed; rollback preserved it")
        for name in UPDATE_FILES:
            self.ordinary(folder / name)
            if file_sha(folder / name) != record["before"][name]:
                raise ValueError("Saved source backup changed")
        return before_manifest

    def restore(self, record, folder):
        self.validate_restore(record, folder)
        self.run(["systemctl", "stop", SERVICE])
        before_manifest = self.validate_restore(record, folder)
        for name in UPDATE_FILES:
            atomic(self.path(CODE + "/" + name), (folder / name).read_bytes(), 0o644)
        atomic(self.path(CODE + "/install-state.json"), before_manifest, 0o600)
        self.run(["systemctl", "start", SERVICE])
        self.wait_health()
        record["phase"] = "rolledBack"
        self.save_record(folder, record)
        return {"status": "rolledBack", "updateId": record["updateId"], "privateDataUntouched": True}

    def rollback_update(self, update_id):
        with self.lock():
            record, folder = self.read_record(update_id)
            return self.restore(record, folder)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--audit", action="store_true")
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--rollback-update")
    parser.add_argument("--expected-current")
    parser.add_argument("--expected-protected-sha256")
    parser.add_argument("--expected-installed-sha256")
    args = parser.parse_args()
    tool = Updater()
    if args.audit:
        result = tool.audit()
    elif args.rollback_update:
        result = tool.rollback_update(args.rollback_update)
    else:
        if not re.fullmatch(r"/var/www/ozl/sites/[a-zA-Z0-9_.-]+", args.expected_current or "") or any(not re.fullmatch(r"[a-f0-9]{64}", item or "") for item in (args.expected_protected_sha256, args.expected_installed_sha256)):
            parser.error("Update requires exact audited current, protected SHA256 and installed SHA256")
        if args.check:
            result = tool.preflight_update(args.expected_current, args.expected_protected_sha256, args.expected_installed_sha256)
        else:
            result = tool.update(Path(__file__).resolve().parent, args.expected_current, args.expected_protected_sha256, args.expected_installed_sha256)
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))


if __name__ == "__main__":
    main()
