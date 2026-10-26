#!/usr/bin/env python3
"""Standalone recommendation administration; no deployment or game operations."""
import argparse
from contextlib import contextmanager
from datetime import datetime, timezone
import hashlib
import hmac
from http import cookies
from http.server import BaseHTTPRequestHandler, HTTPServer
try:
    from http.server import ThreadingHTTPServer
except ImportError:  # CentOS 7 / Python 3.6
    from socketserver import ThreadingMixIn
    class ThreadingHTTPServer(ThreadingMixIn, HTTPServer):
        daemon_threads = True
import ipaddress
import json
import os
from pathlib import Path
import re
import secrets
import sqlite3
import threading
import time
from urllib.parse import urlsplit

STATIC = Path(__file__).resolve().parent / "static"
MAX_BODY = 65536
MAX_DOCUMENT = 1048576
MAX_ROWS = 200
ITERATIONS = 310000
COOKIE = "ozl_recommendations_session"
FIELDS = {"id", "name", "host", "port", "queryPort", "version", "description", "cover", "tags", "order", "useSteamRelay", "published"}


class Rejected(Exception):
    def __init__(self, status, message):
        self.status, self.message = status, message


class PublishPending(Rejected):
    def __init__(self, document):
        Rejected.__init__(self, 503, "列表已保存，公开列表同步尚未完成；请重试公开同步。")
        self.document = document


def no_links(path):
    cursor = Path(os.path.abspath(str(path)))
    for part in [cursor] + list(cursor.parents):
        if part.is_symlink():
            raise OSError("Store/export paths must not contain symbolic links")


def digest(payload):
    return hashlib.sha256(payload).hexdigest()


def file_digest(path):
    no_links(path)
    if not path.exists():
        return "absent"
    if not path.is_file() or path.stat().st_size > MAX_DOCUMENT:
        raise OSError("Export target is not a bounded regular file")
    return digest(path.read_bytes())


class Singleton:
    """Process lifetime lock; Linux flock, with a Windows fixture equivalent."""
    def __init__(self, path):
        no_links(path)
        self.stream = open(str(path), "a+b")
        try:
            if os.name == "nt":
                import msvcrt
                self.stream.write(b"0")
                self.stream.flush()
                self.stream.seek(0)
                msvcrt.locking(self.stream.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(self.stream.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BaseException:
            self.stream.close()
            raise RuntimeError("Another recommendation publisher owns this private store")

    def close(self):
        if not self.stream.closed:
            self.stream.close()


def utc_now():
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def exact(value, fields):
    if not isinstance(value, dict) or set(value) != set(fields):
        raise Rejected(400, "请求字段不完整，请刷新后重试。")


def text(value, maximum, required=False, multiline=False):
    if not isinstance(value, str) or len(value) > maximum or (required and not value.strip()):
        raise Rejected(400, "文字长度或内容无效。")
    if any((ord(c) < 32 or 127 <= ord(c) <= 159) and not (multiline and c in "\n\r\t") for c in value):
        raise Rejected(400, "文字包含不支持的控制字符。")
    try:
        value.encode("utf-8")
    except UnicodeError:
        raise Rejected(400, "文字编码无效。")
    return value.strip()


def integer(value, minimum, maximum):
    if type(value) is not int or not minimum <= value <= maximum:
        raise Rejected(400, "数字范围无效。")
    return value


def identity(value):
    if not isinstance(value, str) or not re.fullmatch(r"[a-f0-9]{32}", value):
        raise Rejected(400, "服务器标识无效。")
    return value


def record(value):
    exact(value, FIELDS)
    result = dict(value)
    result["id"] = "" if value["id"] == "" else identity(value["id"])
    result["name"] = text(value["name"], 120, True)
    result["description"] = text(value["description"], 2000, multiline=True)
    result["version"] = text(value["version"], 80)
    host = text(value["host"], 253, True)
    if "%" in host:
        raise Rejected(400, "请输入有效的 IP 地址或域名。")
    try:
        host = str(ipaddress.ip_address(host))
    except ValueError:
        if all(c in "0123456789." for c in host) or any(not re.fullmatch(r"[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?", part) for part in host.split(".")):
            raise Rejected(400, "请输入有效的 IP 地址或域名。")
        host = host.lower()
    result["host"] = host
    result["port"] = integer(value["port"], 1, 65535)
    result["queryPort"] = integer(value["queryPort"], 0, 65535)
    result["order"] = integer(value["order"], 0, 1000000)
    for key in ("published", "useSteamRelay"):
        if type(value[key]) is not bool:
            raise Rejected(400, "发布或中继选项无效。")
    cover = text(value["cover"], 2048)
    if cover:
        try:
            uri = urlsplit(cover)
            valid = uri.scheme == "https" and uri.hostname and not uri.username and not uri.password and (uri.port is None or 1 <= uri.port <= 65535)
        except ValueError:
            valid = False
        if not valid:
            raise Rejected(400, "封面请填写 HTTPS 图片地址，或留空。")
    result["cover"] = cover
    if not isinstance(value["tags"], list) or len(value["tags"]) > 12:
        raise Rejected(400, "最多添加 12 个标签。")
    result["tags"] = list(dict.fromkeys(text(tag, 32, True) for tag in value["tags"]))
    return result


def write_private(path, payload):
    descriptor = os.open(str(path), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8") as stream:
        json.dump(payload, stream, ensure_ascii=False, indent=2)
        stream.flush()
        os.fsync(stream.fileno())


def setup_private(private_dir):
    """Create once; return a private credential FILE path, never credential values."""
    root = Path(private_dir).resolve()
    root.mkdir(parents=True, exist_ok=True)
    if root == STATIC or STATIC in root.parents:
        raise ValueError("Private store must be outside public assets")
    try:
        root.chmod(0o700)
    except OSError:
        pass
    auth, credential = root / "auth.json", root / "initial-credentials.json"
    if auth.exists():
        return credential
    password, salt = secrets.token_urlsafe(24), secrets.token_bytes(32)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, ITERATIONS)
    write_private(auth, {"username": "admin", "algorithm": "pbkdf2-sha256", "iterations": ITERATIONS, "salt": salt.hex(), "hash": digest.hex()})
    write_private(credential, {"username": "admin", "password": password})
    return credential


class Application:
    def __init__(self, private_dir, public_origin=None, export_feed=None):
        no_links(private_dir)
        self.root = Path(private_dir).resolve()
        if self.root == STATIC or STATIC in self.root.parents:
            raise ValueError("Private store must be outside public assets")
        self.auth = json.loads((self.root / "auth.json").read_text(encoding="utf-8"))
        if self.auth.get("algorithm") != "pbkdf2-sha256" or self.auth.get("iterations", 0) < ITERATIONS:
            raise ValueError("Unsupported password hash")
        self.database = self.root / "recommendations.db"
        self.lock = threading.RLock()
        self.closed = False
        self.publisher_lock = None
        self.export_feed = None
        self.export_error = ""
        self.sessions = {}
        self.attempts = {}
        self.public_origin = public_origin
        if public_origin and (not public_origin.startswith("https://") or urlsplit(public_origin).path not in ("", "/")):
            raise ValueError("Public origin must be an HTTPS origin")
        with self.connect() as db:
            db.execute("CREATE TABLE IF NOT EXISTS document (id INTEGER PRIMARY KEY CHECK(id=1), body TEXT NOT NULL)")
            db.execute("INSERT OR IGNORE INTO document VALUES (1, ?)", (json.dumps({"schema": 1, "revision": 0, "updatedUtc": utc_now(), "servers": []}),))
            db.execute("CREATE TABLE IF NOT EXISTS publisher (id INTEGER PRIMARY KEY CHECK(id=1), body TEXT NOT NULL)")
            db.execute("CREATE TABLE IF NOT EXISTS outbox (id INTEGER PRIMARY KEY CHECK(id=1), body TEXT NOT NULL)")
            if export_feed is None and db.execute("SELECT 1 FROM publisher WHERE id=1").fetchone():
                raise ValueError("This store is bound to a publisher; its explicit export path is required")
        if export_feed is not None:
            try:
                if not Path(export_feed).is_absolute():
                    raise ValueError("Export feed must be an explicit absolute file path")
                no_links(export_feed)
                self.export_feed = Path(export_feed)
                if not self.export_feed.parent.is_dir():
                    raise ValueError("Export parent must already exist")
                self.publisher_lock = Singleton(self.root / "publisher.lock")
                parent = self.export_feed.parent.stat()
                binding = {"path": str(self.export_feed), "parent": [parent.st_dev, parent.st_ino]}
                with self.connect() as db:
                    db.execute("BEGIN IMMEDIATE")
                    saved = db.execute("SELECT body FROM publisher WHERE id=1").fetchone()
                    if saved:
                        owner = json.loads(saved[0])
                        if any(owner[key] != binding[key] for key in binding):
                            raise ValueError("Export ownership/path changed; existing files preserved")
                    else:
                        if file_digest(self.export_feed) != "absent":
                            raise ValueError("Existing export file has no ownership record")
                        binding.update({"revision": -1, "hash": "absent"})
                        db.execute("INSERT INTO publisher VALUES (1, ?)", (json.dumps(binding),))
                        document = json.loads(db.execute("SELECT body FROM document WHERE id=1").fetchone()[0])
                        self.queue(db, document)
                self.sync_export()
            except BaseException:
                self.close()
                raise

    def close(self):
        with self.lock:
            self.closed = True
            if self.publisher_lock is not None:
                self.publisher_lock.close()

    @contextmanager
    def connect(self):
        db = sqlite3.connect(str(self.database), timeout=5)
        try:
            db.execute("PRAGMA synchronous=FULL")
            with db:
                yield db
        finally:
            db.close()

    def document(self):
        with self.connect() as db:
            return json.loads(db.execute("SELECT body FROM document WHERE id=1").fetchone()[0])

    def public_document(self):
        if self.export_feed is not None:
            # Keep the loopback public endpoint consistent with nginx's last
            # complete static publication while a committed edit awaits export.
            if file_digest(self.export_feed) == "absent":
                raise OSError("Public snapshot is unavailable")
            return json.loads(self.export_feed.read_text(encoding="utf-8"))
        return self.project(self.document())

    @staticmethod
    def project(document):
        document = dict(document)
        document["servers"] = [{key: row[key] for key in FIELDS - {"published"}} for row in document["servers"] if row["published"]]
        return document

    def queue(self, db, document):
        payload = json.dumps(self.project(document), ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        db.execute("INSERT OR REPLACE INTO outbox VALUES (1, ?)", (json.dumps({"revision": document["revision"], "payload": payload, "hash": digest(payload.encode("utf-8"))}),))

    def acknowledge(self, owner, desired):
        owner.update({"revision": desired["revision"], "hash": desired["hash"]})
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            db.execute("UPDATE publisher SET body=? WHERE id=1", (json.dumps(owner),))
            db.execute("DELETE FROM outbox WHERE id=1")

    def sync_export(self):
        if self.export_feed is None:
            return
        with self.lock:
            with self.connect() as db:
                owner = json.loads(db.execute("SELECT body FROM publisher WHERE id=1").fetchone()[0])
                pending = db.execute("SELECT body FROM outbox WHERE id=1").fetchone()
            no_links(self.export_feed)
            parent = self.export_feed.parent.stat()
            if owner["parent"] != [parent.st_dev, parent.st_ino] or owner["path"] != str(self.export_feed):
                raise OSError("Export directory identity changed")
            current = file_digest(self.export_feed)
            desired = json.loads(pending[0]) if pending else None
            if current not in {owner["hash"], desired["hash"] if desired else owner["hash"]}:
                raise OSError("Export file changed outside publisher; preserved")
            if desired:
                payload = desired["payload"].encode("utf-8")
                if len(payload) > MAX_DOCUMENT or digest(payload) != desired["hash"]:
                    raise OSError("Invalid durable export snapshot")
                temporary = self.export_feed.parent / (".feed-" + secrets.token_hex(12) + ".tmp")
                try:
                    fd = os.open(str(temporary), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
                    with os.fdopen(fd, "wb") as stream:
                        stream.write(payload)
                        stream.flush()
                        os.chmod(str(temporary), 0o644)
                        os.fsync(stream.fileno())
                    # Both names are in the one owned public-only directory.
                    if file_digest(self.export_feed) != current:
                        raise OSError("Export target changed before replacement")
                    os.replace(str(temporary), str(self.export_feed))
                    if os.name != "nt":
                        directory = os.open(str(self.export_feed.parent), os.O_RDONLY | os.O_DIRECTORY)
                        try:
                            os.fsync(directory)
                        finally:
                            os.close(directory)
                    self.acknowledge(owner, desired)
                finally:
                    if temporary.exists():
                        temporary.unlink()
            self.export_error = ""

    def publication(self):
        revision = self.document()["revision"]
        if self.export_feed is None:
            return {"enabled": False, "pending": False, "revision": revision, "exportedRevision": revision, "error": ""}
        with self.connect() as db:
            owner = json.loads(db.execute("SELECT body FROM publisher WHERE id=1").fetchone()[0])
            pending = db.execute("SELECT 1 FROM outbox WHERE id=1").fetchone() is not None
        return {"enabled": True, "pending": pending, "revision": revision, "exportedRevision": owner["revision"], "error": self.export_error}

    def admin_document(self):
        document = self.document()
        document["publication"] = self.publication()
        return document

    def retry_export(self):
        with self.lock:
            if self.closed:
                raise Rejected(503, "管理服务已关闭，请重新连接。")
            try:
                self.sync_export()
            except Exception:
                self.export_error = "公开列表尚未同步，请重试；若持续失败，请检查服务存储。"
                raise PublishPending(self.admin_document())
            return self.admin_document()

    def mutate(self, revision, change):
        with self.lock:
            if self.closed:
                raise Rejected(503, "管理服务已关闭，请重新连接。")
            self.change(revision, change)
            return self.retry_export()

    def change(self, revision, change):
        integer(revision, 0, 2147483646)
        with self.connect() as db:
            db.execute("BEGIN IMMEDIATE")
            if self.export_feed is None and db.execute("SELECT 1 FROM publisher WHERE id=1").fetchone():
                raise Rejected(409, "此存储已绑定公开发布路径，请重启正确的管理服务。")
            document = json.loads(db.execute("SELECT body FROM document WHERE id=1").fetchone()[0])
            if revision != document["revision"]:
                raise Rejected(409, "推荐列表已被其他管理员修改，请刷新后重试。")
            change(document["servers"])
            document["servers"].sort(key=lambda row: (row["order"], row["id"]))
            for index, row in enumerate(document["servers"]):
                row["order"] = index
            document["revision"] += 1
            document["updatedUtc"] = utc_now()
            encoded = json.dumps(document, ensure_ascii=False)
            if len(encoded.encode("utf-8")) > MAX_DOCUMENT:
                raise Rejected(400, "推荐列表内容过多，请精简介绍或移除不再使用的服务器。")
            db.execute("UPDATE document SET body=? WHERE id=1", (encoded,))
            if self.export_feed is not None:
                self.queue(db, document)

    def login(self, body, ip):
        exact(body, {"username", "password"})
        username = text(body["username"], 128, True)
        password = text(body["password"], 1024, True)
        now = time.monotonic()
        with self.lock:
            self.attempts = {key: value for key, value in self.attempts.items() if now - value[0] < 60}
            # Bounded map, and loopback-only listener; forwarding headers are never trusted.
            if len(self.attempts) >= 256 and ip not in self.attempts:
                raise Rejected(429, "登录尝试过多，请稍后重试。")
            start, count = self.attempts.get(ip, (now, 0))
            if count >= 6:
                raise Rejected(429, "登录尝试过多，请一分钟后重试。")
            self.attempts[ip] = (start, count + 1)
            digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), bytes.fromhex(self.auth["salt"]), self.auth["iterations"])
            if not (hmac.compare_digest(username.encode("utf-8"), self.auth["username"].encode("utf-8")) and hmac.compare_digest(digest.hex(), self.auth["hash"])):
                raise Rejected(401, "账号或密码不正确。")
            self.attempts.pop(ip, None)
            self.sessions = {key: value for key, value in self.sessions.items() if value["expires"] > now}
            if len(self.sessions) >= 128:
                self.sessions.pop(next(iter(self.sessions)))
            token, csrf = secrets.token_urlsafe(32), secrets.token_urlsafe(32)
            self.sessions[token] = {"csrf": csrf, "expires": now + 8 * 3600}
            return token, csrf

    def session(self, cookie):
        jar = cookies.SimpleCookie()
        try:
            jar.load(cookie or "")
            token = jar[COOKIE].value if COOKIE in jar else ""
        except cookies.CookieError:
            token = ""
        with self.lock:
            session = self.sessions.get(token)
            if session and session["expires"] > time.monotonic():
                return token, session
            self.sessions.pop(token, None)
            raise Rejected(401, "请先登录管理员账号。")


class Handler(BaseHTTPRequestHandler):
    server_version = "OrangeRecommendations"
    sys_version = ""
    protocol_version = "HTTP/1.1"

    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def log_message(self, *_args):
        # Never log passwords, cookies, bodies or URLs supplied by a user.
        pass

    def respond(self, status, payload, media="application/json; charset=utf-8", extra=None):
        data = payload if isinstance(payload, bytes) else json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        for key, value in {"Content-Type": media, "Content-Length": str(len(data)), "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer", "X-Frame-Options": "DENY", "Content-Security-Policy": "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self' https:; connect-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'", **(extra or {})}.items():
            self.send_header(key, value)
        self.end_headers()
        self.wfile.write(data)

    def body(self):
        if self.headers.get("Transfer-Encoding"):
            raise Rejected(400, "不支持分块请求。")
        try:
            size = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            raise Rejected(400, "请求长度无效。")
        if size > MAX_BODY:
            self.close_connection = True
            raise Rejected(413, "请求内容过大。")
        if size <= 0 or self.headers.get("Content-Type", "").split(";", 1)[0] != "application/json":
            raise Rejected(400, "请发送 JSON 请求。")
        try:
            return json.loads(self.rfile.read(size).decode("utf-8"))
        except (ValueError, UnicodeError):
            raise Rejected(400, "请求内容无效。")

    def dispatch(self):
        app = self.server.app
        local_authorities = {"127.0.0.1:" + str(self.server.server_port), "localhost:" + str(self.server.server_port)}
        allowed_host = urlsplit(app.public_origin).netloc if app.public_origin else None
        if self.headers.get("Host") not in local_authorities | ({allowed_host} if allowed_host else set()):
            raise Rejected(403, "主机地址无效。")
        origin = self.headers.get("Origin")
        allowed_origins = {"http://" + host for host in local_authorities}
        if app.public_origin:
            allowed_origins = {app.public_origin.rstrip("/")}
        if origin and origin not in allowed_origins:
            raise Rejected(403, "请求来源无效。")
        path = urlsplit(self.path).path
        method = self.command
        if method == "GET" and path == "/api/recommended-servers":
            return self.respond(200, app.public_document())
        assets = {"/": ("admin.html", "text/html"), "/admin": ("admin.html", "text/html"), "/admin/": ("admin.html", "text/html"), "/admin/admin.css": ("admin.css", "text/css"), "/admin/admin.js": ("admin.js", "application/javascript")}
        if method == "GET" and path in assets:
            file, media = assets[path]
            return self.respond(200, (STATIC / file).read_bytes(), media + "; charset=utf-8")
        if path == "/api/admin/login" and method == "POST":
            token, csrf = app.login(self.body(), self.client_address[0])
            cookie = COOKIE + "=" + token + "; Path=/api/admin; HttpOnly; SameSite=Strict; Max-Age=28800" + ("; Secure" if app.public_origin else "")
            return self.respond(200, {"authenticated": True, "csrf": csrf}, extra={"Set-Cookie": cookie})
        if not (path in ("/api/admin/session", "/api/admin/servers", "/api/admin/logout", "/api/admin/order", "/api/admin/publish", "/api/admin/export") or re.fullmatch(r"/api/admin/servers/[a-f0-9]{32}", path)):
            raise Rejected(404, "页面不存在。")
        if method not in ("GET", "POST", "DELETE"):
            raise Rejected(405, "不支持的请求方式。")
        token, session = app.session(self.headers.get("Cookie"))
        if method == "GET":
            if path == "/api/admin/session":
                return self.respond(200, {"authenticated": True, "csrf": session["csrf"]})
            if path == "/api/admin/servers":
                return self.respond(200, app.admin_document())
            raise Rejected(405, "不支持的请求方式。")
        if not hmac.compare_digest(self.headers.get("X-CSRF-Token", ""), session["csrf"]):
            raise Rejected(403, "登录校验已失效，请刷新后重试。")
        body = self.body()
        if path == "/api/admin/export" and method == "POST":
            exact(body, set())
            return self.respond(200, app.retry_export())
        if path == "/api/admin/logout" and method == "POST":
            exact(body, set())
            with app.lock:
                app.sessions.pop(token, None)
            return self.respond(200, {"authenticated": False}, extra={"Set-Cookie": COOKIE + "=; Path=/api/admin; HttpOnly; SameSite=Strict; Max-Age=0" + ("; Secure" if app.public_origin else "")})
        if path == "/api/admin/servers" and method == "POST":
            exact(body, {"revision", "server"})
            row = record(body["server"])
            def upsert(rows):
                if row["id"]:
                    index = next((i for i, item in enumerate(rows) if item["id"] == row["id"]), None)
                    if index is None:
                        raise Rejected(404, "服务器不存在，请刷新。")
                    rows[index] = row
                else:
                    if len(rows) >= MAX_ROWS:
                        raise Rejected(400, "最多维护 200 个服务器。")
                    row["id"], row["order"] = secrets.token_hex(16), len(rows)
                    rows.append(row)
            return self.respond(200, app.mutate(body["revision"], upsert))
        if path == "/api/admin/order" and method == "POST":
            exact(body, {"revision", "ids"})
            if not isinstance(body["ids"], list) or len(body["ids"]) > MAX_ROWS:
                raise Rejected(400, "排序列表无效。")
            ids = [identity(value) for value in body["ids"]]
            def reorder(rows):
                if len(set(ids)) != len(ids) or set(ids) != {row["id"] for row in rows}:
                    raise Rejected(400, "排序列表不完整，请刷新。")
                for row in rows:
                    row["order"] = ids.index(row["id"])
            return self.respond(200, app.mutate(body["revision"], reorder))
        if path == "/api/admin/publish" and method == "POST":
            exact(body, {"revision", "id", "published"})
            identity(body["id"])
            if type(body["published"]) is not bool:
                raise Rejected(400, "发布选项无效。")
            def publish(rows):
                row = next((row for row in rows if row["id"] == body["id"]), None)
                if row is None:
                    raise Rejected(404, "服务器不存在，请刷新。")
                row["published"] = body["published"]
                record(row)
            return self.respond(200, app.mutate(body["revision"], publish))
        if method == "DELETE" and path.startswith("/api/admin/servers/"):
            exact(body, {"revision"})
            def delete(rows):
                row = next((row for row in rows if row["id"] == path.rsplit("/", 1)[1]), None)
                if row is None:
                    raise Rejected(404, "服务器不存在，请刷新。")
                rows.remove(row)
            return self.respond(200, app.mutate(body["revision"], delete))
        raise Rejected(405, "不支持的请求方式。")

    def run_request(self):
        try:
            self.dispatch()
        except PublishPending as error:
            self.close_connection = True
            self.respond(503, {"error": error.message, "saved": True, "document": error.document})
        except Rejected as error:
            self.close_connection = True
            self.respond(error.status, {"error": error.message}, extra={"Retry-After": "60"} if error.status == 429 else None)
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception:
            self.close_connection = True
            self.respond(500, {"error": "服务暂时无法处理请求，请稍后重试。"})

    do_GET = run_request
    do_POST = run_request
    do_DELETE = run_request
    do_PUT = run_request
    do_OPTIONS = run_request


class Server(ThreadingHTTPServer):
    daemon_threads = True
    def server_close(self):
        try:
            ThreadingHTTPServer.server_close(self)
        finally:
            if hasattr(self, "app"):
                self.app.close()


def make_server(private_dir, port=18083, public_origin=None, export_feed=None):
    app = Application(private_dir, public_origin, export_feed)
    try:
        server = Server(("127.0.0.1", port), Handler)
        server.app = app
        return server
    except BaseException:
        app.close()
        raise


def default_private_dir():
    return Path(os.environ.get("LOCALAPPDATA", str(Path.home() / ".local/share"))) / "OrangeLauncher" / "recommendations-local"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--private-dir", type=Path, default=default_private_dir())
    parser.add_argument("--port", type=int, default=18083)
    parser.add_argument("--public-origin", help="HTTPS reverse-proxy origin; enables Secure cookies")
    parser.add_argument("--export-feed", type=Path, help="Explicit absolute public-only JSON export path; parent must exist")
    parser.add_argument("--setup-only", action="store_true")
    args = parser.parse_args()
    path = setup_private(args.private_dir)
    print("Private credentials file: " + str(path))
    if args.setup_only:
        return
    server = make_server(args.private_dir, args.port, args.public_origin, args.export_feed)
    print("Local admin: http://127.0.0.1:" + str(server.server_port) + "/admin")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
