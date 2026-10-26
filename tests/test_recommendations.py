"""Recommendation administration contract; isolated stores and loopback only."""
import http.client
import importlib.util
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest import mock

MODULE = Path(__file__).resolve().parents[1] / "deploy/recommendations/server.py"


class RecommendationTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(MODULE.exists(), "recommendation service must exist")
        spec = importlib.util.spec_from_file_location("recommendation_server", MODULE)
        self.service = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.service)
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.private = Path(self.temp.name)
        self.credentials_path = self.service.setup_private(self.private)
        self.credentials = json.loads(self.credentials_path.read_text(encoding="utf-8"))
        self.server = self.service.make_server(self.private, port=0)
        thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(self.stop_server)
        self.cookie = self.csrf = ""

    def stop_server(self):
        self.server.shutdown()
        self.server.server_close()

    def request(self, method, path, body=None, authenticated=True, csrf=True, origin=None):
        connection = http.client.HTTPConnection("127.0.0.1", self.server.server_port, timeout=4)
        headers = {"Content-Type": "application/json"}
        if authenticated and self.cookie:
            headers["Cookie"] = self.cookie
        if csrf and self.csrf:
            headers["X-CSRF-Token"] = self.csrf
        if origin:
            headers["Origin"] = origin
        connection.request(method, path, None if body is None else json.dumps(body), headers)
        response = connection.getresponse()
        payload = response.read()
        result = (response.status, dict(response.getheaders()), payload)
        connection.close()
        return result

    def read_json(self, method, path, body=None, **kwargs):
        status, headers, payload = self.request(method, path, body, **kwargs)
        return status, headers, json.loads(payload)

    def login(self):
        status, headers, payload = self.read_json("POST", "/api/admin/login", self.credentials)
        self.assertEqual(200, status)
        self.cookie = headers["Set-Cookie"].split(";", 1)[0]
        self.csrf = payload["csrf"]
        return headers

    def document(self):
        status, _, payload = self.read_json("GET", "/api/admin/servers")
        self.assertEqual(200, status)
        return payload

    def row(self, name="橙子生存服", published=False):
        return {"id": "", "name": name, "host": "127.0.0.1", "port": 16261,
                "queryPort": 16262, "version": "42.13", "description": "友好合作\n欢迎新玩家",
                "cover": "", "tags": ["合作", "新手友好"], "order": 0,
                "useSteamRelay": False, "published": published}

    def save(self, row, revision=None, **kwargs):
        if revision is None:
            revision = self.document()["revision"]
        return self.read_json("POST", "/api/admin/servers", {"revision": revision, "server": row}, **kwargs)

    def test_anonymous_cannot_read_private_or_write(self):
        self.assertEqual(401, self.request("GET", "/api/admin/servers")[0])
        self.assertEqual(401, self.request("POST", "/api/admin/servers", {"revision": 0, "server": self.row()})[0])
        status, _, public = self.read_json("GET", "/api/recommended-servers")
        self.assertEqual(200, status)
        self.assertEqual([], public["servers"])
        self.assertEqual({"schema", "revision", "updatedUtc", "servers"}, set(public))

    def test_login_requires_password_and_csrf_cookie_protects_write(self):
        bad = dict(self.credentials, password="wrong")
        self.assertEqual(401, self.request("POST", "/api/admin/login", bad)[0])
        headers = self.login()
        self.assertIn("HttpOnly", headers["Set-Cookie"])
        self.assertIn("SameSite=Strict", headers["Set-Cookie"])
        self.assertNotIn("Secure", headers["Set-Cookie"])
        self.assertEqual(403, self.save(self.row(), csrf=False)[0])
        self.assertEqual(403, self.save(self.row(), origin="https://evil.invalid")[0])
        self.assertEqual(200, self.save(self.row())[0])
        self.assertEqual(200, self.request("POST", "/api/admin/logout", {})[0])
        self.assertEqual(401, self.request("GET", "/api/admin/servers")[0])

    def test_add_edit_order_publish_unpublish_delete_and_restart(self):
        self.login()
        _, _, first = self.save(self.row())
        row = first["servers"][0]
        first_id = row["id"]
        self.assertEqual([], self.read_json("GET", "/api/recommended-servers")[2]["servers"])
        row["name"] = "<img src=x onerror=alert(1)>"
        row["published"] = True
        self.assertEqual(200, self.save(row)[0])
        _, _, second = self.save(self.row("第二个服", published=True))
        second_id = next(r["id"] for r in second["servers"] if r["id"] != first_id)
        status, _, reordered = self.read_json("POST", "/api/admin/order", {"revision": second["revision"], "ids": [second_id, first_id]})
        self.assertEqual(200, status)
        public = self.read_json("GET", "/api/recommended-servers")[2]
        self.assertEqual([second_id, first_id], [r["id"] for r in public["servers"]])
        self.assertEqual("<img src=x onerror=alert(1)>", public["servers"][1]["name"])
        self.assertNotIn("published", public["servers"][0])
        self.assertNotIn(self.credentials["password"], json.dumps(public))
        status, _, unpublished = self.read_json("POST", "/api/admin/publish", {"revision": reordered["revision"], "id": first_id, "published": False})
        self.assertEqual(200, status)
        self.assertEqual([second_id], [r["id"] for r in self.read_json("GET", "/api/recommended-servers")[2]["servers"]])
        self.assertEqual(200, self.request("DELETE", "/api/admin/servers/" + first_id, {"revision": unpublished["revision"]})[0])
        reopened = self.service.Application(self.private)
        self.assertEqual([second_id], [r["id"] for r in reopened.public_document()["servers"]])

    def test_stale_revision_cannot_overwrite_other_edit(self):
        self.login()
        revision = self.document()["revision"]
        self.assertEqual(200, self.save(self.row(), revision)[0])
        self.assertEqual(409, self.save(self.row("过期编辑"), revision)[0])
        self.assertEqual(["橙子生存服"], [r["name"] for r in self.document()["servers"]])

    def test_optional_extended_text_and_screenshots_preserve_old_records(self):
        self.login()
        row = self.row(published=True)
        row.update(extendedDescription="长介绍\n<b>普通文字</b>", screenshots=["https://orangezl.com/screens/example.png", "https://images.steamusercontent.com/example.jpg"])
        status, _, saved = self.save(row)
        self.assertEqual(200, status)
        public = self.read_json("GET", "/api/recommended-servers")[2]["servers"][0]
        self.assertEqual(row["screenshots"], public["screenshots"])
        self.assertEqual(row["extendedDescription"], public["extendedDescription"])
        self.assertEqual(200, self.save(self.row("legacy", published=True))[0])
        row["screenshots"] = ["javascript:alert(1)"]
        self.assertEqual(400, self.save(row)[0])
        row["screenshots"] = ["https://foreign.example.invalid/image.png"]
        self.assertEqual(400, self.save(row)[0])

    def test_export_failure_returns_saved_revision_and_retry_requires_authentication(self):
        self.stop_server()
        public = self.private / "public-fixture"
        public.mkdir()
        feed = public / "feed.json"
        self.server = self.service.make_server(self.private, port=0, export_feed=feed)
        threading.Thread(target=self.server.serve_forever, daemon=True).start()
        self.login()
        with mock.patch.object(self.service.os, "replace", side_effect=OSError("fixture unavailable")):
            status, _, value = self.save(self.row(published=True))
        self.assertEqual(503, status)
        self.assertTrue(value["saved"])
        self.assertEqual(1, value["document"]["revision"])
        self.assertTrue(value["document"]["publication"]["pending"])
        self.assertNotIn(self.credentials["password"], json.dumps(value))
        self.assertEqual(0, json.loads(feed.read_text(encoding="utf-8"))["revision"])
        self.assertEqual(0, self.read_json("GET", "/api/recommended-servers")[2]["revision"])
        self.assertEqual(401, self.request("POST", "/api/admin/export", {}, authenticated=False)[0])
        self.assertEqual(403, self.request("POST", "/api/admin/export", {}, csrf=False)[0])
        status, _, value = self.read_json("POST", "/api/admin/export", {})
        self.assertEqual(200, status)
        self.assertEqual(1, value["revision"])
        self.assertFalse(value["publication"]["pending"])
        self.assertEqual(1, self.read_json("GET", "/api/recommended-servers")[2]["revision"])

    def test_invalid_values_never_publish_and_private_files_are_unservable(self):
        self.login()
        for field, value in [("host", "https://host/path"), ("port", 0), ("port", True),
                             ("queryPort", 65536), ("cover", "javascript:alert(1)"),
                             ("tags", ["x"] * 13), ("published", "yes"), ("id", False),
                             ("name", "server\x7f"), ("cover", "https://[bad/")]:
            row = self.row(published=True)
            row[field] = value
            self.assertEqual(400, self.save(row)[0], field)
        self.assertEqual([], self.document()["servers"])
        for path in ("/credentials.json", "/../credentials.json", "/admin/../credentials.json", "/recommendations.db"):
            self.assertEqual(404, self.request("GET", path)[0])
        config = (self.private / "auth.json").read_text(encoding="utf-8")
        self.assertNotIn(self.credentials["password"], config)
        self.assertIn("pbkdf2", config)

    def test_unicode_login_is_rejected_without_internal_error(self):
        self.assertEqual(401, self.request("POST", "/api/admin/login", {"username": "管理员", "password": "wrong"})[0])

    def test_https_mode_uses_secure_cookie_and_rejects_foreign_origin(self):
        self.server.app.public_origin = "https://preview.example.invalid"
        headers = self.login()
        self.assertIn("; Secure", headers["Set-Cookie"])
        self.assertEqual(403, self.save(self.row(), origin="http://127.0.0.1:" + str(self.server.server_port))[0])
        self.assertEqual(200, self.save(self.row(), origin="https://preview.example.invalid")[0])

    def test_login_rate_limit_and_request_body_limit(self):
        for _ in range(8):
            status = self.request("POST", "/api/admin/login", dict(self.credentials, password="bad"))[0]
        self.assertEqual(429, status)
        self.assertEqual(413, self.request("POST", "/api/admin/login", {"padding": "x" * 70000})[0])

    def test_oversize_document_is_refused_without_replacing_previous_list(self):
        rows = []
        for index in range(200):
            row = self.row()
            row["id"] = format(index, "032x")
            row["description"] = "橙" * 2000
            rows.append(row)
        app = self.server.app
        with self.assertRaises(self.service.Rejected):
            app.mutate(0, lambda saved: saved.extend(rows))
        self.assertEqual([], app.document()["servers"])
        self.assertEqual(0, app.document()["revision"])

    def test_admin_static_security_headers_and_asset_routes(self):
        for path, media in [("/admin", "text/html"), ("/admin/admin.css", "text/css"), ("/admin/admin.js", "javascript")]:
            status, headers, _ = self.request("GET", path)
            self.assertEqual(200, status)
            self.assertIn(media, headers["Content-Type"])
            self.assertEqual("nosniff", headers["X-Content-Type-Options"])
            self.assertIn("frame-ancestors 'none'", headers["Content-Security-Policy"])
        self.assertEqual(404, self.request("GET", "/admin/auth.json")[0])


if __name__ == "__main__":
    unittest.main()
