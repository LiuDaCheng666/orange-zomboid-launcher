"""Owned-code updates only; temporary trees and fake systemd, never SSH."""
import ast
import importlib.util
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / "deploy/recommendations"


def load(name):
    spec = importlib.util.spec_from_file_location(name, str(FOLDER / (name + ".py")))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


class UpdateTests(unittest.TestCase):
    def fixture(self):
        install = load("install")
        with mock.patch.dict(sys.modules, {"install": install}):
            update = load("update")
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        def put(absolute, data):
            path = root / absolute.lstrip("/")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
            return path
        put("/var/www/ozl/current/index.html", b"foreign page")
        put("/etc/caddy/ozl-static.caddy", b"foreign config")
        put("/etc/ozl-http-guard/nginx.conf", b"user nginx; server { root /var/www/ozl/current; location / { try_files $uri $uri/ =404; } }")
        run = mock.Mock()
        tool = update.Updater(root, run)
        files = {}
        for name in install.BUNDLE:
            absolute = install.UNIT if name == "ozl-recommendations.service" else install.CODE + "/" + name
            path = put(absolute, ("old:" + name).encode("ascii"))
            files[str(path)] = install.file_sha(path)
        private = put(install.PRIVATE + "/recommendations.db", b"existing private DB")
        feed = put(install.FEED, b'{"schema":1,"revision":7,"servers":[{"name":"real existing record"}]}')
        current = tool.path(install.CURRENT)
        state = {"version": 1, "phase": "installed", "current": str(current), "currentIdentity": install.identity(current),
                 "directories": {install.CODE: install.identity(tool.path(install.CODE))}, "files": files,
                 "route": str(current / "api/recommended-servers"), "apiCreated": False}
        tool.save_state(state)
        # Windows fixtures cannot create production symlinks without privileges.
        # Route ownership itself is independently tested by the installer.
        tool.validate_route = mock.Mock()
        tool.wait_health = mock.Mock()
        bundle = root / "reviewed-bundle"
        for name in update.UPDATE_FILES:
            path = bundle / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(("new:" + name).encode("ascii"))
        return install, update, tool, root, bundle, run, private, feed

    def apply(self, tool, bundle):
        audit = tool.audit()
        return tool.update(bundle, audit["current"], audit["protectedSha256"], audit["installedSha256"])

    def test_update_and_explicit_rollback_change_only_owned_four_files(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        before = tool.audit()
        saved = {name: tool.path(install.CODE + "/" + name).read_bytes() for name in update.UPDATE_FILES}
        manifest = tool.path(install.CODE + "/install-state.json").read_bytes()
        result = self.apply(tool, bundle)
        self.assertEqual("updated", result["status"])
        self.assertEqual(before["protectedSha256"], tool.audit()["protectedSha256"])
        self.assertNotEqual(before["installedSha256"], tool.audit()["installedSha256"])
        self.assertEqual(b"existing private DB", private.read_bytes())
        self.assertIn(b'"revision":7', feed.read_bytes())
        self.assertEqual(b"old:install.py", tool.path(install.CODE + "/install.py").read_bytes())
        self.assertEqual(b"old:ozl-recommendations.service", tool.path(install.UNIT).read_bytes())
        self.assertTrue(all(call.args[0][:2] in (["systemctl", "stop"], ["systemctl", "start"]) for call in run.call_args_list))
        result = tool.rollback_update(result["updateId"])
        self.assertEqual("rolledBack", result["status"])
        self.assertEqual(manifest, tool.path(install.CODE + "/install-state.json").read_bytes())
        for name, content in saved.items():
            self.assertEqual(content, tool.path(install.CODE + "/" + name).read_bytes())
        self.assertEqual(b"existing private DB", private.read_bytes())

    def test_stale_hash_or_foreign_source_refuses_before_service_stop(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        audit = tool.audit()
        with self.assertRaises(ValueError):
            tool.update(bundle, audit["current"], audit["protectedSha256"], "a" * 64)
        tool.path(install.CODE + "/server.py").write_bytes(b"foreign edit")
        with self.assertRaises(ValueError):
            tool.update(bundle, audit["current"], audit["protectedSha256"], audit["installedSha256"])
        run.assert_not_called()

    def test_failed_readiness_restores_known_before_files_and_preserves_data(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        before = tool.audit()["installedSha256"]
        tool.wait_health.side_effect = [RuntimeError("fixture new service failed"), None]
        with self.assertRaisesRegex(RuntimeError, "fixture new service failed"):
            self.apply(tool, bundle)
        self.assertEqual(before, tool.audit()["installedSha256"])
        self.assertEqual(b"existing private DB", private.read_bytes())
        self.assertIn(b'"revision":7', feed.read_bytes())
        self.assertEqual(2, tool.wait_health.call_count)

    def test_rollback_refuses_foreign_later_edit_before_stop(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        result = self.apply(tool, bundle)
        tool.path(install.CODE + "/static/admin.js").write_bytes(b"foreign changed javascript")
        run.reset_mock()
        with self.assertRaises(ValueError):
            tool.rollback_update(result["updateId"])
        run.assert_not_called()
        self.assertEqual(b"foreign changed javascript", tool.path(install.CODE + "/static/admin.js").read_bytes())

    def test_pending_update_is_recoverable_but_cannot_start_another_update(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        result = self.apply(tool, bundle)
        record = tool.path(install.CODE + "/update-backups/" + result["updateId"] + "/update-state.json")
        state = json.loads(record.read_text(encoding="utf-8"))
        state["phase"] = "applying"
        record.write_text(json.dumps(state), encoding="utf-8")
        run.reset_mock()
        with self.assertRaises(ValueError):
            tool.audit()
        self.assertEqual("rolledBack", tool.rollback_update(result["updateId"])["status"])

    def test_half_written_update_restores_from_exact_receipt(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        result = self.apply(tool, bundle)
        folder = tool.path(install.CODE + "/update-backups/" + result["updateId"])
        for name in update.UPDATE_FILES[1:]:
            tool.path(install.CODE + "/" + name).write_bytes((folder / name).read_bytes())
        tool.path(install.CODE + "/install-state.json").write_bytes((folder / "install-state.before.json").read_bytes())
        record = json.loads((folder / "update-state.json").read_text(encoding="utf-8"))
        record["phase"] = "applying"
        (folder / "update-state.json").write_text(json.dumps(record), encoding="utf-8")
        self.assertEqual("rolledBack", tool.rollback_update(result["updateId"])["status"])
        for name in update.UPDATE_FILES:
            self.assertEqual(("old:" + name).encode("ascii"), tool.path(install.CODE + "/" + name).read_bytes())

    def test_old_receipt_cannot_roll_back_a_later_update(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        first = self.apply(tool, bundle)
        (bundle / "server.py").write_bytes(b"newer server")
        self.apply(tool, bundle)
        run.reset_mock()
        with self.assertRaises(ValueError):
            tool.rollback_update(first["updateId"])
        run.assert_not_called()
        self.assertEqual(b"newer server", tool.path(install.CODE + "/server.py").read_bytes())

    def test_final_stop_guard_cannot_overwrite_a_foreign_late_edit(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        def stop(args):
            if args[:2] == ["systemctl", "stop"]:
                tool.path(install.CODE + "/server.py").write_bytes(b"late foreign edit")
        run.side_effect = stop
        with self.assertRaises((RuntimeError, ValueError)):
            self.apply(tool, bundle)
        self.assertEqual(b"late foreign edit", tool.path(install.CODE + "/server.py").read_bytes())

    def test_rollback_stop_guard_preserves_a_foreign_late_edit(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        result = self.apply(tool, bundle)
        def stop(args):
            if args[:2] == ["systemctl", "stop"]:
                tool.path(install.CODE + "/server.py").write_bytes(b"foreign late rollback edit")
        run.side_effect = stop
        with self.assertRaises(ValueError):
            tool.rollback_update(result["updateId"])
        self.assertEqual(b"foreign late rollback edit", tool.path(install.CODE + "/server.py").read_bytes())

    def test_operator_lock_refuses_a_second_writer(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        audit = tool.audit()
        with tool.lock():
            with self.assertRaises((OSError, ValueError)):
                tool.update(bundle, audit["current"], audit["protectedSha256"], audit["installedSha256"])
        run.assert_not_called()

    def test_anonymous_audit_executes_reviewed_sources_without_writes(self):
        install, update, tool, root, bundle, run, private, feed = self.fixture()
        remote = load("remote")
        source = remote.update_source("audit")
        source = source.replace("tool=namespace['Updater']()", "namespace['Updater'].validate_route=lambda *args:None\ntool=namespace['Updater'](" + repr(str(root)) + ")")
        before = {str(path): path.read_bytes() for path in root.rglob("*") if path.is_file()}
        output = io.StringIO()
        with mock.patch.dict(sys.modules), redirect_stdout(output):
            exec(compile(source, "fixture-reviewed-anonymous", "exec"), {})
        result = json.loads(output.getvalue())
        self.assertTrue(result["readOnly"] and result["runtimeImportVerified"])
        self.assertEqual(tool.audit()["installedSha256"], result["installedSha256"])
        self.assertEqual(before, {str(path): path.read_bytes() for path in root.rglob("*") if path.is_file()})

    def test_update_runtime_is_python36_and_remote_payload_is_anonymous(self):
        source = (FOLDER / "update.py").read_text(encoding="utf-8")
        ast.parse(source, feature_version=(3, 6))
        remote = load("remote")
        source = remote.update_source("audit")
        compile(source, "fixture-anonymous-update", "exec")
        self.assertIn("installedSha256", (FOLDER / "update.py").read_text(encoding="utf-8"))
        self.assertIn("types.ModuleType('install')", source)
        self.assertNotIn("importlib", source)


if __name__ == "__main__":
    unittest.main()
