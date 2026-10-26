#!/usr/bin/env python3
"""Loopback-only, private feedback collector. Python 3.6+; standard library only."""
import argparse
import contextlib
from datetime import datetime, timezone
import hashlib
import hmac
from http.server import BaseHTTPRequestHandler, HTTPServer
import ipaddress
import json
import math
import os
from pathlib import Path
import re
import sqlite3
import threading
import time
import uuid

MAX_BODY = 262144
MAX_STORE = 256 * 1024 * 1024
# SQLite main DB and rollback journal are each bounded to 8 MiB. Also reserve
# 1 MiB for journal headers, key and directory entries. Report allocation is
# charged in disk blocks rather than just smaller logical JSON lengths.
STORAGE_RESERVE = 17 * 1024 * 1024
RETENTION = 30 * 86400
IDENTITY = re.compile(r'^[a-f0-9]{32}$')
FILE_CATEGORIES = {'console.txt': 'gameLog', 'launcher.log': 'launcherLog'}
SYSTEM_FIELDS = {'os', 'architecture', 'cpu', 'logicalProcessors',
                 'totalMemoryGiB', 'gpu', 'vramGiB', 'driver'}


class Rejected(Exception):
    def __init__(self, status, code='invalid_request', retry_after=None):
        super().__init__(code)
        self.status, self.code, self.retry_after = status, code, retry_after


def text(value, maximum, nonempty=False):
    if not isinstance(value, str) or len(value) > maximum or (nonempty and not value.strip()):
        raise Rejected(400)
    try:
        value.encode('utf-8')
    except UnicodeError:
        raise Rejected(400)


def exact(value, keys):
    if not isinstance(value, dict) or set(value) != set(keys):
        raise Rejected(400)


def pairs(items):
    result = {}
    for key, value in items:
        if key in result:
            raise Rejected(400)
        result[key] = value
    return result


def integer(value):
    if len(value) > 20:
        raise Rejected(400)
    return int(value)


def decode(raw):
    if not isinstance(raw, bytes) or len(raw) > MAX_BODY:
        raise Rejected(413, 'payload_too_large')
    try:
        value = json.loads(raw.decode('utf-8'), object_pairs_hook=pairs, parse_int=integer)
    except (ValueError, UnicodeError, RecursionError):
        raise Rejected(400)
    exact(value, {'schema', 'requestId', 'createdUtc', 'appVersion', 'description',
                  'categories', 'files', 'system'})
    if type(value['schema']) is not int or value['schema'] != 1:
        raise Rejected(400)
    if not isinstance(value['requestId'], str) or not IDENTITY.fullmatch(value['requestId']):
        raise Rejected(400)
    created = value['createdUtc']
    if not isinstance(created, str) or not re.fullmatch(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z', created):
        raise Rejected(400)
    try:
        datetime.strptime(created[:19], '%Y-%m-%dT%H:%M:%S')
    except ValueError:
        raise Rejected(400)
    text(value['appVersion'], 32, True)
    if any(ord(c) < 32 for c in value['appVersion']):
        raise Rejected(400)
    text(value['description'], 2000, True)
    categories = value['categories']
    if not isinstance(categories, list) or len(categories) > 3 or any(type(c) is not str for c in categories):
        raise Rejected(400)
    if len(set(categories)) != len(categories) or not set(categories) <= {'gameLog', 'launcherLog', 'system'}:
        raise Rejected(400)
    files = value['files']
    if not isinstance(files, list) or len(files) > 2:
        raise Rejected(400)
    selected = set()
    for file in files:
        exact(file, {'name', 'text'})
        name = file['name']
        if not isinstance(name, str) or name not in FILE_CATEGORIES or name in selected:
            raise Rejected(400)
        selected.add(name)
        text(file['text'], 65536)
    if {FILE_CATEGORIES[name] for name in selected} != set(categories) - {'system'}:
        raise Rejected(400)
    system = value['system']
    if 'system' not in categories:
        if system is not None:
            raise Rejected(400)
    else:
        exact(system, SYSTEM_FIELDS)
        for key in SYSTEM_FIELDS:
            item = system[key]
            if item is None:
                continue
            if key == 'logicalProcessors':
                if type(item) is not int or not 1 <= item <= 1024:
                    raise Rejected(400)
            elif key in ('totalMemoryGiB', 'vramGiB'):
                if type(item) not in (int, float) or not 0 <= item <= 65536 or not math.isfinite(item):
                    raise Rejected(400)
            else:
                text(item, 32 if key == 'architecture' else 128 if key == 'driver' else 256)
    return value


def safe(path):
    path = Path(path).absolute()
    for parent in [path] + list(path.parents):
        if parent.is_symlink():
            raise Rejected(503, 'storage_integrity')
    return path


def private_write(path, content):
    path = safe(path)
    temporary = path.with_name('.pending-' + uuid.uuid4().hex)
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, 'O_NOFOLLOW', 0)
    try:
        with os.fdopen(os.open(str(temporary), flags, 0o600), 'wb') as output:
            output.write(content)
            output.flush()
            os.fsync(output.fileno())
        os.replace(str(temporary), str(path))
        if os.name == 'posix':
            descriptor = os.open(str(path.parent), os.O_RDONLY | getattr(os, 'O_DIRECTORY', 0))
            try:
                os.fsync(descriptor)
            finally:
                os.close(descriptor)
    finally:
        if temporary.exists():
            temporary.unlink()


class Store:
    def __init__(self, root, now=time.time):
        self.root = safe(root)
        self.reports = self.root / 'reports'
        self.now, self.hourly, self.daily = now, 6, 1000
        self.max_bytes, self.max_receipts = MAX_STORE, 5000
        self.lock = threading.RLock()
        for folder in (self.root, self.reports):
            safe(folder).mkdir(mode=0o700, parents=True, exist_ok=True)
            os.chmod(str(folder), 0o700)
        key_path = safe(self.root / 'ip-key')
        try:
            descriptor = os.open(str(key_path), os.O_WRONLY | os.O_CREAT | os.O_EXCL | getattr(os, 'O_NOFOLLOW', 0), 0o600)
        except FileExistsError:
            pass
        else:
            with os.fdopen(descriptor, 'wb') as output:
                output.write(os.urandom(32)); output.flush(); os.fsync(output.fileno())
        self.key = key_path.read_bytes()
        if len(self.key) != 32:
            raise Rejected(503, 'storage_integrity')
        os.chmod(str(key_path), 0o600)
        self.database = safe(self.root / 'receipts.sqlite3')
        with self.transaction() as db:
            db.execute('CREATE TABLE IF NOT EXISTS receipts (id TEXT PRIMARY KEY, digest TEXT NOT NULL, received TEXT NOT NULL, accepted REAL NOT NULL, ip_tag TEXT NOT NULL, size INTEGER NOT NULL)')
            db.execute('CREATE INDEX IF NOT EXISTS accepted_index ON receipts(accepted)')
            db.execute('CREATE INDEX IF NOT EXISTS ip_index ON receipts(ip_tag, accepted)')
            self._prune(db)
            # A crash before SQLite commit can leave only an uncommitted file.
            # It has no accepted receipt/quota and may safely be discarded.
            known = {row[0] for row in db.execute('SELECT id FROM receipts')}
            for path in self.reports.iterdir():
                safe(path)
                if path.name.startswith('.pending-') or (re.fullmatch(r'[a-f0-9]{32}\.json', path.name) and path.stem not in known):
                    path.unlink()
                elif not re.fullmatch(r'[a-f0-9]{32}\.json', path.name):
                    raise Rejected(503, 'storage_integrity')

    @contextlib.contextmanager
    def transaction(self):
        with self.lock:
            safe(self.root); safe(self.database)
            for suffix in ('-journal', '-wal', '-shm'):
                safe(str(self.database) + suffix)
            db = sqlite3.connect(str(self.database), timeout=10, isolation_level=None)
            try:
                os.chmod(str(self.database), 0o600)
                db.row_factory = sqlite3.Row
                db.execute('PRAGMA journal_mode=DELETE')
                db.execute('PRAGMA synchronous=FULL')
                db.execute('PRAGMA secure_delete=ON')
                if db.execute('PRAGMA max_page_count=2048').fetchone()[0] > 2048:
                    raise Rejected(503, 'storage_integrity')
                if db.execute('PRAGMA page_size').fetchone()[0] != 4096:
                    raise Rejected(503, 'storage_integrity')
                db.execute('BEGIN IMMEDIATE')
                yield db
                db.execute('COMMIT')
            except sqlite3.Error:
                if db.in_transaction:
                    db.execute('ROLLBACK')
                raise Rejected(503, 'storage_unavailable')
            except BaseException:
                if db.in_transaction:
                    db.execute('ROLLBACK')
                raise
            finally:
                db.close()

    def path(self, identity):
        if not isinstance(identity, str) or not IDENTITY.fullmatch(identity):
            raise Rejected(400)
        return safe(self.reports / (identity + '.json'))

    def _prune(self, db):
        expired = list(db.execute('SELECT id FROM receipts WHERE accepted < ?', (self.now() - RETENTION,)))
        for row in expired:
            path = self.path(row['id'])
            if path.exists():
                path.unlink()
        db.execute('DELETE FROM receipts WHERE accepted < ?', (self.now() - RETENTION,))

    def prune(self):
        with self.transaction() as db:
            self._prune(db)

    def verified(self, row):
        try:
            with self.path(row['id']).open('rb') as source:
                content = source.read(MAX_BODY + 1)
        except OSError:
            raise Rejected(503, 'storage_integrity')
        if len(content) != row['size'] or hashlib.sha256(content).hexdigest() != row['digest']:
            raise Rejected(503, 'storage_integrity')
        return content

    def accept(self, raw, client_ip):
        report = decode(raw)
        try:
            client = str(ipaddress.ip_address(client_ip))
        except ValueError:
            raise Rejected(400)
        tag = hmac.new(self.key, client.encode('ascii'), hashlib.sha256).hexdigest()
        identity, digest, now = report['requestId'], hashlib.sha256(raw).hexdigest(), self.now()
        with self.transaction() as db:
            self._prune(db)
            previous = db.execute('SELECT * FROM receipts WHERE id=?', (identity,)).fetchone()
            if previous is not None:
                if previous['digest'] != digest:
                    raise Rejected(409, 'request_id_conflict')
                self.verified(previous)
                return 200, dict(id=identity, receivedUtc=previous['received'], status='received')
            hour = db.execute('SELECT COUNT(*) FROM receipts WHERE ip_tag=? AND accepted>?', (tag, now - 3600)).fetchone()[0]
            day_start = now - now % 86400
            daily = db.execute('SELECT COUNT(*) FROM receipts WHERE accepted>=?', (day_start,)).fetchone()[0]
            if hour >= self.hourly or daily >= self.daily:
                raise Rejected(429, 'quota_exceeded', 3600 if hour >= self.hourly else int(day_start + 86400 - now) + 1)
            count, size = db.execute('SELECT COUNT(*), COALESCE(SUM(size),0) FROM receipts').fetchone()
            stored = list(self.reports.iterdir())
            occupied = 0
            for stored_path in stored:
                stat = stored_path.stat()
                occupied += max((stat.st_size + 4095) // 4096 * 4096, getattr(stat, 'st_blocks', 0) * 512)
            allocation = (len(raw) + 4095) // 4096 * 4096
            if max(count, len(stored)) >= self.max_receipts or max(size, occupied) + allocation + STORAGE_RESERVE > self.max_bytes:
                raise Rejected(507, 'storage_capacity')
            received = datetime.fromtimestamp(now, timezone.utc).isoformat(timespec='milliseconds').replace('+00:00', 'Z')
            path = self.path(identity)
            private_write(path, raw)
            try:
                db.execute('INSERT INTO receipts VALUES (?,?,?,?,?,?)', (identity, digest, received, now, tag, len(raw)))
            except BaseException:
                path.unlink()
                raise
            return 201, dict(id=identity, receivedUtc=received, status='received')

    def list(self):
        with self.transaction() as db:
            self._prune(db)
            return [dict(id=row['id'], receivedUtc=row['received'], bytes=row['size']) for row in db.execute('SELECT id,received,size FROM receipts ORDER BY accepted DESC,id')]

    def read(self, identity):
        self.path(identity)
        with self.transaction() as db:
            self._prune(db)
            row = db.execute('SELECT * FROM receipts WHERE id=?', (identity,)).fetchone()
            if row is None:
                raise Rejected(404, 'not_found')
            return self.verified(row)

    def delete(self, identity):
        path = self.path(identity)
        with self.transaction() as db:
            if db.execute('SELECT id FROM receipts WHERE id=?', (identity,)).fetchone() is None:
                raise Rejected(404, 'not_found')
            if path.exists():
                path.unlink()
            db.execute('DELETE FROM receipts WHERE id=?', (identity,))


class Handler(BaseHTTPRequestHandler):
    server_version = 'OZLFeedback'
    sys_version = ''

    def log_message(self, *args):
        pass  # Neither peer addresses nor report contents enter process logs.

    def setup(self):
        self.request.settimeout(10)
        super().setup()

    def reply(self, status, value, retry_after=None):
        content = json.dumps(value, ensure_ascii=True, separators=(',', ':')).encode('ascii')
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(content)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('Connection', 'close')
        if status == 405:
            self.send_header('Allow', 'POST')
        if retry_after is not None:
            self.send_header('Retry-After', str(retry_after))
        self.end_headers()
        self.wfile.write(content)
        self.close_connection = True

    def send_error(self, code, message=None, explain=None):
        self.reply(code, dict(error='invalid_request'))

    def do_GET(self):
        if self.path == '/health':
            self.reply(200, dict(status='ok'))
        else:
            self.other_method()

    def other_method(self):
        self.reply(405 if self.path == '/api/feedback' else 404, dict(error='method_not_allowed' if self.path == '/api/feedback' else 'not_found'))

    do_HEAD = do_PUT = do_PATCH = do_DELETE = do_OPTIONS = other_method

    def __getattr__(self, name):
        if name.startswith('do_'):
            return self.other_method
        raise AttributeError(name)

    def do_POST(self):
        if self.path != '/api/feedback':
            self.reply(404, dict(error='not_found'))
            return
        try:
            lengths = self.headers.get_all('Content-Length', [])
            if len(lengths) != 1 or not re.fullmatch(r'[0-9]+', lengths[0]) or self.headers.get_all('Transfer-Encoding', []):
                raise Rejected(400)
            if len(lengths[0]) > 6:
                raise Rejected(413, 'payload_too_large')
            length = int(lengths[0])
            if length > MAX_BODY:
                raise Rejected(413, 'payload_too_large')
            if self.headers.get('Content-Encoding', 'identity').lower() != 'identity':
                raise Rejected(415, 'unsupported_encoding')
            content_type = self.headers.get('Content-Type', '').lower().replace(' ', '')
            if content_type not in ('application/json', 'application/json;charset=utf-8'):
                raise Rejected(415, 'unsupported_media_type')
            addresses = self.headers.get_all('X-OZL-Client-IP', [])
            if len(addresses) != 1:
                raise Rejected(400, 'invalid_client_ip')
            try:
                ipaddress.ip_address(addresses[0])
            except ValueError:
                raise Rejected(400, 'invalid_client_ip')
            try:
                data = self.rfile.read(length)
            except TimeoutError:
                raise Rejected(408, 'request_timeout')
            if len(data) != length:
                raise Rejected(400)
            status, receipt = self.server.store.accept(data, addresses[0])
            self.reply(status, receipt)
        except Rejected as error:
            self.reply(error.status, dict(error=error.code), error.retry_after)
        except Exception:
            self.reply(503, dict(error='service_unavailable'), 60)


class Server(HTTPServer):
    def __init__(self, address, store):
        if address[0] != '127.0.0.1':
            raise ValueError('Collector may bind only IPv4 loopback')
        self.store, self.slots = store, threading.BoundedSemaphore(4)
        self.last_prune = time.monotonic()
        super().__init__(address, Handler)

    def process_request(self, request, client_address):
        request.settimeout(10)
        if not self.slots.acquire(False):
            try:
                request.sendall(b'HTTP/1.0 503 Service Unavailable\r\nContent-Length: 0\r\nCache-Control: no-store\r\nRetry-After: 10\r\nConnection: close\r\n\r\n')
            except OSError:
                pass
            self.shutdown_request(request)
            return
        thread = threading.Thread(target=self.worker, args=(request, client_address))
        thread.daemon = True
        thread.start()

    def worker(self, request, address):
        try:
            self.finish_request(request, address)
        except Exception:
            pass
        finally:
            self.shutdown_request(request)
            self.slots.release()

    def service_actions(self):
        if time.monotonic() - self.last_prune >= 3600:
            self.store.prune()
            self.last_prune = time.monotonic()


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--store', default='/var/lib/ozl-feedback')
    parser.add_argument('command', choices=('serve', 'list', 'read', 'delete'))
    parser.add_argument('id', nargs='?')
    args = parser.parse_args()
    store = Store(args.store)
    if args.command == 'serve':
        Server(('127.0.0.1', 18082), store).serve_forever()
    elif args.command == 'list':
        print(json.dumps(store.list(), indent=2))
    elif args.command == 'read':
        print(store.read(args.id).decode('utf-8'))
    elif args.command == 'delete':
        store.delete(args.id)
        print(json.dumps(dict(deleted=args.id)))


if __name__ == '__main__':
    try:
        main()
    except Rejected as error:
        raise SystemExit(error.code)
