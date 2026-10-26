"""Prepared operator tools; fake SSH transports and temporary local files only."""
import ast
import builtins
import importlib.util
import json
from pathlib import Path
import socket
import tempfile
import threading
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]


def module(name):
    path = ROOT / "deploy/recommendations" / (name + ".py")
    spec = importlib.util.spec_from_file_location("recommendation_" + name, str(path))
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


class OperatorTests(unittest.TestCase):
    def test_windows_private_acl_replaces_the_whole_dacl_and_reparse_paths_refuse(self):
        helper = module("admin_remote")
        source = helper.windows_acl_script(Path("C:/fixture-private"), True)
        self.assertIn("DirectorySecurity]::new()", source)
        self.assertIn("SetAccessRuleProtection($true,$false)", source)
        self.assertIn("SetAccessControl", source)
        self.assertNotIn("Get-Acl", source)
        self.assertNotIn("icacls", source)
        value = {"username": "admin", "password": "fixture-secret-" + "a" * 40}
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / "linked"
            with mock.patch.object(helper, "is_reparse", return_value=True):
                with self.assertRaises(ValueError):
                    helper.save_credentials(value, destination)
            self.assertFalse(destination.exists())
    def test_python36_threaded_http_fallback_imports_without_binding_or_writes(self):
        original = builtins.__import__
        def compatible_import(name, globals=None, locals=None, fromlist=(), level=0):
            if name == "http.server" and "ThreadingHTTPServer" in fromlist:
                raise ImportError("Python 3.6 fixture")
            return original(name, globals, locals, fromlist, level)
        with mock.patch("builtins.__import__", side_effect=compatible_import):
            service = module("server")
        from socketserver import ThreadingMixIn
        self.assertTrue(issubclass(service.ThreadingHTTPServer, ThreadingMixIn))
    def installer_fixture(self):
        install = module("install")
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        def put(name, content):
            file = root / name.lstrip("/")
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_text(content, encoding="utf-8")
        put("/var/www/ozl/current/index.html", "existing page")
        put("/var/www/ozl/releases/v1.2.28/installer.exe", "existing release")
        put("/etc/caddy/ozl-static.caddy", "existing config")
        put("/etc/ozl-http-guard/nginx.conf", "user nginx; server { root /var/www/ozl/current; location / { try_files $uri $uri/ =404; } }")
        run = mock.Mock()
        return install, install.Installer(root, run), root, put, run

    def test_install_preflight_pins_current_and_all_protected_bytes(self):
        install, tool, root, put, run = self.installer_fixture()
        audit = tool.audit()
        tool.preflight(audit["current"], audit["protectedSha256"])
        with self.assertRaises(ValueError):
            tool.preflight(audit["current"] + "-other", audit["protectedSha256"])
        put("/etc/caddy/ozl-static.caddy", "changed by another task")
        with self.assertRaises(ValueError):
            tool.preflight(audit["current"], audit["protectedSha256"])
        run.assert_not_called()
        self.assertFalse((root / "opt/ozl-recommendations").exists())

    def test_install_refuses_existing_paths_and_disabled_symlink_policy(self):
        install, tool, root, put, run = self.installer_fixture()
        audit = tool.audit()
        put("/var/www/ozl/current/api/recommended-servers", "foreign existing route")
        with self.assertRaises(ValueError):
            tool.preflight(audit["current"], audit["protectedSha256"])
        self.assertEqual("foreign existing route", (root / "var/www/ozl/current/api/recommended-servers").read_text())
        (root / "var/www/ozl/current/api/recommended-servers").unlink()
        put("/etc/ozl-http-guard/nginx.conf", "user nginx; disable_symlinks on; server { root /var/www/ozl/current; location / { try_files $uri $uri/ =404; } }")
        with self.assertRaises(ValueError):
            tool.audit()
        run.assert_not_called()

    def test_install_refuses_api_interception_in_included_configuration(self):
        install, tool, root, put, run = self.installer_fixture()
        put("/etc/ozl-http-guard/nginx.conf", "user nginx; include /etc/ozl-http-guard/extra.conf; server { root /var/www/ozl/current; location / { try_files $uri $uri/ =404; } }")
        put("/etc/ozl-http-guard/extra.conf", "location /api/ { return 403; }")
        with self.assertRaises(ValueError):
            tool.audit()
        run.assert_not_called()

    def rollback_fixture(self):
        install, tool, root, put, run = self.installer_fixture()
        code = tool.path(install.CODE)
        code.mkdir(parents=True)
        source = code / "server.py"
        source.write_text("owned source", encoding="utf-8")
        unit = tool.path(install.UNIT)
        unit.parent.mkdir(parents=True)
        unit.write_text("owned unit", encoding="utf-8")
        private = tool.path(install.PRIVATE)
        private.mkdir(parents=True)
        (private / "recommendations.db").write_bytes(b"private recovery data")
        current = tool.path(install.CURRENT)
        state = {"version": 1, "phase": "prepared", "current": str(current), "currentIdentity": install.identity(current),
                 "directories": {install.CODE: install.identity(code)}, "files": {str(source): install.file_sha(source), str(unit): install.file_sha(unit)},
                 "route": str(current / "api/recommended-servers"), "apiCreated": False}
        tool.save_state(state)
        return install, tool, root, run, state

    def test_owned_rollback_preserves_private_data_foreign_files_and_web_configuration(self):
        install, tool, root, run, state = self.rollback_fixture()
        before = tool.audit()["protectedSha256"]
        foreign = tool.path(install.CODE) / "foreign-note.txt"
        foreign.write_text("keep", encoding="utf-8")
        result = tool.rollback()
        self.assertEqual("rolledBack", result["status"])
        self.assertEqual(before, tool.audit()["protectedSha256"])
        self.assertEqual(b"private recovery data", (tool.path(install.PRIVATE) / "recommendations.db").read_bytes())
        self.assertEqual("keep", foreign.read_text())
        self.assertFalse(tool.path(install.UNIT).exists())
        self.assertTrue((tool.path(install.CODE) / "install-state.json").exists())
        self.assertFalse(any("nginx" in str(call) or "caddy" in str(call) or "reload" in str(call) and "daemon-reload" not in str(call) for call in run.call_args_list))

    def test_rollback_refuses_changed_unit_or_forged_destination_before_stopping(self):
        install, tool, root, run, state = self.rollback_fixture()
        unit = tool.path(install.UNIT)
        unit.write_text("foreign new unit", encoding="utf-8")
        with self.assertRaises(ValueError):
            tool.rollback()
        run.assert_not_called()
        unit.write_text("owned unit", encoding="utf-8")
        state["files"][str(root / "foreign")] = "a" * 64
        tool.save_state(state)
        with self.assertRaises(ValueError):
            tool.rollback()
        run.assert_not_called()

    def test_tunnel_forwards_only_to_fixed_remote_loopback(self):
        helper = module("admin_remote")
        client, accepted = socket.socketpair()
        channel, remote = socket.socketpair()
        self.addCleanup(client.close)
        self.addCleanup(remote.close)
        transport = mock.Mock()
        transport.open_channel.return_value = channel
        worker = threading.Thread(target=helper.forward, args=(transport, accepted, ("127.0.0.1", 1234)))
        worker.start()
        client.sendall(b"request")
        remote.settimeout(2)
        self.assertEqual(b"request", remote.recv(20))
        remote.sendall(b"response")
        client.settimeout(2)
        self.assertEqual(b"response", client.recv(20))
        client.close()
        worker.join(3)
        self.assertFalse(worker.is_alive())
        self.assertEqual(("127.0.0.1", 18083), transport.open_channel.call_args.args[1])

    def test_credentials_are_exclusive_private_and_never_inside_repository(self):
        helper = module("admin_remote")
        value = {"username": "admin", "password": "fixture-secret-" + "a" * 40}
        with tempfile.TemporaryDirectory() as temporary:
            destination = Path(temporary) / "production"
            result = helper.save_credentials(value, destination)
            self.assertEqual(value, json.loads(result.read_text(encoding="utf-8")))
            self.assertEqual(result, helper.save_credentials(value, destination))
            with self.assertRaises(ValueError):
                helper.save_credentials(dict(value, password="different" * 8), destination)
        with self.assertRaises(ValueError):
            helper.save_credentials(value, ROOT / "artifacts/forbidden-credentials")

    def test_python36_grammar_and_fixed_service_boundary(self):
        for name in ("server", "install", "admin_remote", "remote"):
            source = (ROOT / "deploy/recommendations" / (name + ".py")).read_text(encoding="utf-8")
            ast.parse(source, feature_version=(3, 6))
        unit = (ROOT / "deploy/recommendations/ozl-recommendations.service").read_text(encoding="utf-8")
        self.assertIn("--export-feed /var/lib/ozl-recommendations-public/feed.json", unit)
        self.assertIn("User=ozl-recommendations", unit)
        self.assertNotIn("StateDirectory=", unit)
        for name in ("admin_remote", "remote"):
            source = (ROOT / "deploy/recommendations" / (name + ".py")).read_text(encoding="utf-8")
            self.assertIn("from ssh_connection import connect", source)
            self.assertNotIn("AutoAddPolicy", source)


if __name__ == "__main__":
    unittest.main()
