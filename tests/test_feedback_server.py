"""Private feedback contract; isolated fixtures only, no live server mutations."""
import concurrent.futures
import copy
import hashlib
import http.client
import importlib.util
import json
import os
import socket
import subprocess
import sys
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1] / 'deploy' / 'feedback'


def load(name):
    path = ROOT / (name + '.py')
    if not path.is_file():
        raise AssertionError('Feedback implementation missing: ' + str(path))
    spec = importlib.util.spec_from_file_location('feedback_' + name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def body(identity='a' * 32):
    return dict(schema=1, requestId=identity, createdUtc='2026-10-26T08:00:00.1234567Z',
                appVersion='1.2.3', description='启动后提示配置错误',
                categories=[], files=[], system=None)


def raw(value):
    return json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf-8')


class FeedbackTests(unittest.TestCase):
    def setUp(self):
        self.module = load('server')
        self.temporary = tempfile.TemporaryDirectory(prefix='ozl-feedback-test-')
        self.addCleanup(self.temporary.cleanup)
        self.clock = [1800000000.0]
        self.store = self.module.Store(self.temporary.name, now=lambda: self.clock[0])

    def rejected(self, value, status=400):
        with self.assertRaises(self.module.Rejected) as caught:
            self.module.decode(raw(value))
        self.assertEqual(caught.exception.status, status)

    def test_exact_schema_selection_and_no_paths_or_extra_payload(self):
        self.assertEqual(self.module.decode(raw(body()))['description'], '启动后提示配置错误')
        report = body()
        report['categories'] = ['gameLog', 'launcherLog', 'system']
        report['files'] = [dict(name='console.txt', text='游戏日志'), dict(name='launcher.log', text='launcher')]
        report['system'] = dict(os='Windows', architecture='X64', cpu=None,
                                logicalProcessors=8, totalMemoryGiB=16.5, gpu=None,
                                vramGiB=None, driver=None)
        self.assertEqual(self.module.decode(raw(report))['system']['logicalProcessors'], 8)
        for mutate in (
                lambda r: r.update(unknown='secret'),
                lambda r: r.update(consent=True),
                lambda r: r.update(schema=True),
                lambda r: r.update(description='  '),
                lambda r: r.update(description='x' * 2001),
                lambda r: r.update(appVersion='x' * 33),
                lambda r: r.update(requestId='../' + 'a' * 29),
                lambda r: r.update(createdUtc='2026-02-30T00:00:00Z'),
                lambda r: r.update(createdUtc='2026-10-26T08:00:00+08:00'),
                lambda r: r.update(categories=['gameLog', 'gameLog']),
                lambda r: r.update(categories=['anything']),
                lambda r: r.update(categories=['gameLog']),
                lambda r: r.update(categories=['system']),
                lambda r: r.update(categories=['launcherLog'], files=[dict(name='console.txt', text='wrong selection')]),
                lambda r: r.update(categories=['gameLog'], files=[dict(name='console.txt', text='one'), dict(name='console.txt', text='two')]),
                lambda r: r.update(files=[dict(name='console.txt', text='unselected')]),
                lambda r: r.update(system={'os': 'unselected'}),
                lambda r: r.update(categories=['gameLog'], files=[dict(name='../console.txt', text='x')]),
                lambda r: r.update(categories=['gameLog'], files=[dict(name='console.txt', text='x', path='C:/x')]),
                lambda r: r.update(categories=['gameLog'], files=[dict(name='console.txt', text='x' * 65537)]),
                lambda r: r.update(categories=['gameLog'], files=[dict(name='console.txt', text=[])])
        ):
            with self.subTest(mutate=mutate):
                invalid = body()
                mutate(invalid)
                self.rejected(invalid)
        for key, value in (('logicalProcessors', True), ('logicalProcessors', 0),
                           ('logicalProcessors', 1025), ('totalMemoryGiB', -1),
                           ('totalMemoryGiB', float('nan')), ('vramGiB', 65537),
                           ('architecture', 'x' * 33), ('driver', []), ('extra', 'bad'),
                           ('totalMemoryGiB', 10 ** 1000)):
            invalid = copy.deepcopy(report)
            invalid['system'][key] = value
            self.rejected(invalid)

    def test_raw_size_duplicate_keys_invalid_utf8_and_scalar_json(self):
        for value in (b'[]', b'null', b'\xff', b'{"schema":1,"schema":1}',
                      b' ' * 262145, b'{"x":"\\ud800"}'):
            with self.subTest(value=value[:40]), self.assertRaises(self.module.Rejected):
                self.module.decode(value)

    def test_atomic_receipts_survive_restart_dedupe_does_not_consume_quota(self):
        report = raw(body())
        status, receipt = self.store.accept(report, '192.0.2.1')
        self.assertEqual(status, 201)
        self.assertEqual(set(receipt), {'id', 'receivedUtc', 'status'})
        other = self.module.Store(self.temporary.name, now=lambda: self.clock[0])
        self.assertEqual(other.accept(report, '192.0.2.1'), (200, receipt))
        self.assertEqual(other.read('a' * 32), report)
        for index in range(5):
            other.accept(raw(body('%032x' % index)), '192.0.2.1')
        self.assertEqual(other.accept(report, '192.0.2.1')[0], 200)
        with self.assertRaises(self.module.Rejected) as caught:
            other.accept(raw(body('f' * 32)), '192.0.2.1')
        self.assertEqual(caught.exception.status, 429)
        changed = body()
        changed['description'] = 'different body'
        with self.assertRaises(self.module.Rejected) as caught:
            other.accept(raw(changed), '192.0.2.2')
        self.assertEqual(caught.exception.status, 409)
        self.assertEqual(len(other.list()), 6)
        for path in Path(self.temporary.name).rglob('*'):
            if path.is_file():
                self.assertNotIn(b'192.0.2.1', path.read_bytes())
                if os.name == 'posix':
                    self.assertEqual(path.stat().st_mode & 0o777, 0o600)

    def test_concurrent_same_body_commits_once(self):
        report = raw(body())
        with concurrent.futures.ThreadPoolExecutor(max_workers=10) as executor:
            result = list(executor.map(lambda _: self.store.accept(report, '192.0.2.1'), range(10)))
        self.assertEqual(sum(status == 201 for status, _ in result), 1)
        self.assertEqual(sum(status == 200 for status, _ in result), 9)
        self.assertEqual(len(self.store.list()), 1)

    def test_startup_cleans_only_uncommitted_files_and_rejects_symlink_store(self):
        self.store.accept(raw(body()), '192.0.2.1')
        pending = Path(self.temporary.name, 'reports', '.pending-interrupted')
        orphan = Path(self.temporary.name, 'reports', 'b' * 32 + '.json')
        pending.write_bytes(b'partial')
        orphan.write_bytes(raw(body('b' * 32)))
        restarted = self.module.Store(self.temporary.name, now=lambda: self.clock[0])
        self.assertFalse(pending.exists())
        self.assertFalse(orphan.exists())
        self.assertEqual(restarted.read('a' * 32), raw(body()))
        if os.name == 'posix':
            link = Path(self.temporary.name, 'linked')
            link.symlink_to(Path(self.temporary.name, 'reports'), target_is_directory=True)
            with self.assertRaises(self.module.Rejected):
                self.module.Store(link)

    def test_tamper_fails_closed_without_overwrite(self):
        self.store.accept(raw(body()), '192.0.2.1')
        path = Path(self.temporary.name, 'reports', 'a' * 32 + '.json')
        path.write_bytes(b'corrupt')
        for action in (lambda: self.store.accept(raw(body()), '192.0.2.1'),
                       lambda: self.store.read('a' * 32)):
            with self.assertRaises(self.module.Rejected) as caught:
                action()
            self.assertEqual(caught.exception.status, 503)
        self.assertEqual(path.read_bytes(), b'corrupt')

    def test_global_daily_receipt_and_byte_caps_and_expiry(self):
        self.store.daily = 1
        self.store.accept(raw(body()), '192.0.2.1')
        with self.assertRaises(self.module.Rejected) as caught:
            self.store.accept(raw(body('b' * 32)), '192.0.2.2')
        self.assertEqual(caught.exception.status, 429)
        self.clock[0] += 86400
        self.store.max_receipts = 1
        with self.assertRaises(self.module.Rejected) as caught:
            self.store.accept(raw(body('b' * 32)), '192.0.2.2')
        self.assertEqual(caught.exception.status, 507)
        self.store.max_receipts = 5000
        # Reserve only one disk block, so two tiny receipts must still be
        # rejected instead of accounting only their smaller logical lengths.
        self.store.max_bytes = self.module.STORAGE_RESERVE + 4096
        with self.assertRaises(self.module.Rejected) as caught:
            self.store.accept(raw(body('b' * 32)), '192.0.2.2')
        self.assertEqual(caught.exception.status, 507)
        self.clock[0] += 30 * 86400
        self.store.prune()
        self.assertEqual(self.store.list(), [])
        self.assertFalse(Path(self.temporary.name, 'reports', 'a' * 32 + '.json').exists())
        self.assertEqual(self.store.accept(raw(body('b' * 32)), '192.0.2.2')[0], 201)

    def test_operator_confined_read_list_delete(self):
        self.store.accept(raw(body()), '192.0.2.1')
        self.assertEqual(self.store.list()[0]['id'], 'a' * 32)
        with self.assertRaises(self.module.Rejected):
            self.store.read('../ip-key')
        self.store.delete('a' * 32)
        self.assertEqual(self.store.list(), [])
        with self.assertRaises(self.module.Rejected):
            self.store.read('a' * 32)

    def test_actual_http_health_post_dedupe_conflict_and_method_paths(self):
        server = self.module.Server(('127.0.0.1', 0), self.store)
        thread = threading.Thread(target=server.serve_forever)
        thread.daemon = True
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 3)
        self.addCleanup(server.shutdown)

        def request(method, path, payload=None, headers=None):
            connection = http.client.HTTPConnection(*server.server_address, timeout=3)
            connection.request(method, path, body=payload, headers=headers or {})
            response = connection.getresponse()
            result = response.status, response.read()
            self.assertEqual(response.getheader('Cache-Control'), 'no-store')
            connection.close()
            return result
        self.assertEqual(request('GET', '/health')[0], 200)
        self.assertEqual(request('GET', '/api/feedback')[0], 405)
        self.assertEqual(request('GET', '/anything')[0], 404)
        self.assertEqual(request('BREW', '/anything')[0], 404)
        self.assertEqual(request('BREW', '/api/feedback')[0], 405)
        self.assertEqual(request('POST', '/health', b'{}')[0], 404)
        self.assertEqual(request('POST', '/api/feedback', raw(body()), {'Content-Type': 'application/json'})[0], 400)
        invalid_ip = {'Content-Type': 'application/json', 'X-OZL-Client-IP': 'not-an-ip'}
        self.assertEqual(json.loads(request('POST', '/api/feedback', raw(body()), invalid_ip)[1]), {'error': 'invalid_client_ip'})
        headers = {'Content-Type': 'application/json; charset=utf-8', 'X-OZL-Client-IP': '192.0.2.1'}
        self.assertEqual(request('POST', '/api/feedback', raw(body()), headers)[0], 201)
        self.assertEqual(request('POST', '/api/feedback', raw(body()), headers)[0], 200)
        empty_transfer = dict(headers, **{'Transfer-Encoding': '', 'Content-Length': str(len(raw(body('c' * 32))))})
        self.assertEqual(request('POST', '/api/feedback', raw(body('c' * 32)), empty_transfer)[0], 400)
        changed = body(); changed['description'] = 'changed'
        self.assertEqual(request('POST', '/api/feedback', raw(changed), headers)[0], 409)
        # Reject the oversized declaration before sending ignored bytes; a TCP
        # peer closing with unread payload may otherwise reset on Windows.
        oversized = dict(headers, **{'Content-Length': '262145'})
        self.assertEqual(request('POST', '/api/feedback', b'', oversized)[0], 413)
        self.assertEqual(request('POST', '/api/feedback', raw(body()), dict(headers, **{'Content-Encoding': 'gzip'}))[0], 415)

    def test_four_request_workers_and_strict_http_framing(self):
        server = self.module.Server(('127.0.0.1', 0), self.store)
        thread = threading.Thread(target=server.serve_forever)
        thread.daemon = True
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(thread.join, 3)
        self.addCleanup(server.shutdown)
        for framing in (b'Transfer-Encoding: chunked\r\nContent-Length: 0\r\n',
                        b'Content-Length: 0\r\nContent-Length: 0\r\n', b''):
            with socket.create_connection(server.server_address, timeout=3) as connection:
                connection.sendall(b'POST /api/feedback HTTP/1.0\r\nContent-Type: application/json\r\nX-OZL-Client-IP: 192.0.2.1\r\n' + framing + b'\r\n')
                self.assertIn(b'400 Bad Request', connection.recv(4096))
        with self.assertRaises(ValueError):
            self.module.Server(('0.0.0.0', 0), self.store)
        # Four real connections wait on incomplete headers. The fifth gets an
        # overload response before sending any bytes, without a fifth worker.
        # Not sending bytes on the refused connection also avoids TCP's unread
        # input reset obscuring an already-sent response on Windows.
        slow = []
        try:
            for _ in range(4):
                connection = socket.create_connection(server.server_address, timeout=3)
                slow.append(connection)
                connection.sendall(b'GET /health HTTP/1.0\r\nX-Wait: ')
            with socket.create_connection(server.server_address, timeout=3) as connection:
                self.assertIn(b'503 Service Unavailable', connection.recv(4096))
        finally:
            for connection in slow:
                connection.close()


class ConfigurationTests(unittest.TestCase):
    def test_additive_patch_and_owned_rollback_preserve_both_servers_and_unknowns(self):
        module = load('configuration')
        original = ('events {}\nhttp {\n # custom administrator setting\n server_tokens off;\n'
                    ' server {\n  listen 127.0.0.1:18080;\n  location / { return 404; }\n }\n'
                    ' server {\n  listen 127.0.0.1:18081;\n  location / { return 404; }\n }\n}\n')
        digest = hashlib.sha256(original.encode()).hexdigest()
        patched = module.patch_nginx(original, digest)
        self.assertEqual(patched.count('nginx-location.conf'), 2)
        self.assertEqual(patched.count('nginx-http.conf'), 1)
        self.assertEqual(module.unpatch_nginx(patched, hashlib.sha256(patched.encode()).hexdigest()), original)
        for malformed in (original.replace(':18081', ':18083'), original + 'server {}\n'):
            with self.assertRaises(ValueError):
                module.patch_nginx(malformed, digest)
        with self.assertRaises(ValueError):
            module.unpatch_nginx(patched + '# external change\n', hashlib.sha256(patched.encode()).hexdigest())

    def fixture(self):
        temporary = tempfile.TemporaryDirectory(prefix='ozl-feedback-deploy-')
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        for name, data in {
                'etc/ozl-http-guard/nginx.conf': 'events {}\nhttp {\n server {\n listen 127.0.0.1:18080;\n }\n server {\n listen 127.0.0.1:18081;\n }\n}\n',
                'etc/caddy/Caddyfile': 'existing caddy\n',
                'etc/caddy/ozl-static.caddy': 'existing real-ip guard\n',
                'var/www/ozl/current/index.html': 'someone else website\n',
                'var/www/ozl/releases/keep.zip': 'release payload\n'} .items():
            path = root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data.encode('utf-8'))
        for directory in ('opt', 'var/lib', 'etc/systemd/system'):
            (root / directory).mkdir(parents=True, exist_ok=True)
        return root

    def test_installer_validates_then_reloads_and_rollback_preserves_protected_files(self):
        module = load('install')
        root = self.fixture()
        calls = []
        installer = module.Installer(root, run=lambda args: calls.append(args))
        installer.account = lambda: (0, 0)
        installer.health = lambda *args: None
        probes = []
        installer.proxy_probe = lambda: probes.append('POST')
        before = installer.protected()
        original = (root / 'etc/ozl-http-guard/nginx.conf').read_bytes()
        with patch.object(os, 'chown', create=True):
            state_path = installer.install(ROOT, hashlib.sha256(original).hexdigest())
        state = json.loads(Path(state_path).read_text())
        self.assertEqual(state['phase'], 'verified')
        self.assertEqual(installer.protected(), before)
        validation = calls.index(['/usr/sbin/nginx', '-t', '-c', '/etc/ozl-http-guard/nginx.conf'])
        reload = calls.index(['systemctl', 'reload', 'ozl-http-guard'])
        self.assertLess(validation, reload)
        self.assertEqual(probes, ['POST'])
        installer.rollback(state_path)
        self.assertEqual((root / 'etc/ozl-http-guard/nginx.conf').read_bytes(), original)
        self.assertEqual(installer.protected(), before)
        self.assertIn(['systemctl', 'disable', '--now', 'ozl-feedback'], calls)

    def test_failed_nginx_validation_rolls_back_only_owned_route_and_disables_service(self):
        module = load('install')
        root = self.fixture()
        calls = []
        failed = [False]
        def command(args):
            calls.append(args)
            if args[0] == '/usr/sbin/nginx' and not failed[0]:
                failed[0] = True
                raise RuntimeError('isolated validation rejection')
        installer = module.Installer(root, run=command)
        installer.account = lambda: (0, 0)
        installer.health = lambda *args: None
        installer.proxy_probe = lambda: None
        original = (root / 'etc/ozl-http-guard/nginx.conf').read_bytes()
        before = installer.protected()
        with patch.object(os, 'chown', create=True), self.assertRaises(RuntimeError):
            installer.install(ROOT, hashlib.sha256(original).hexdigest())
        self.assertEqual((root / 'etc/ozl-http-guard/nginx.conf').read_bytes(), original)
        self.assertEqual(installer.protected(), before)
        self.assertIn(['systemctl', 'disable', '--now', 'ozl-feedback'], calls)

    def test_installer_and_rollback_refuse_concurrent_configuration_changes(self):
        module = load('install')
        root = self.fixture()
        calls = []
        installer = module.Installer(root, run=lambda args: calls.append(args))
        with self.assertRaises(ValueError):
            installer.install(ROOT, '0' * 64)
        self.assertEqual(calls, [])
        installer.account = lambda: (0, 0)
        installer.health = lambda *args: None
        installer.proxy_probe = lambda: None
        config = root / 'etc/ozl-http-guard/nginx.conf'
        with patch.object(os, 'chown', create=True):
            state_path = installer.install(ROOT, hashlib.sha256(config.read_bytes()).hexdigest())
        config.write_text(config.read_text() + '# later administrator change\n')
        changed = config.read_bytes()
        with self.assertRaises(ValueError):
            installer.rollback(state_path)
        self.assertEqual(config.read_bytes(), changed)

    def test_final_modes_are_explicit_even_with_private_umask(self):
        module = load('install')
        root = self.fixture()
        installer = module.Installer(root, run=lambda args: None)
        installer.account = lambda: (0, 0)
        installer.health = lambda *args: None
        installer.proxy_probe = lambda: None
        modes = []
        chmod = os.chmod
        def set_mode(path, mode):
            modes.append((str(path), mode))
            chmod(path, mode)
        original = (root / 'etc/ozl-http-guard/nginx.conf').read_bytes()
        previous = os.umask(0o077)
        try:
            with patch.object(os, 'chmod', side_effect=set_mode), patch.object(os, 'chown', create=True):
                state_path = installer.install(ROOT, hashlib.sha256(original).hexdigest())
        finally:
            os.umask(previous)
        required = {
            root / 'opt/ozl-feedback': 0o755, root / 'etc/ozl-feedback': 0o755,
            root / 'var/lib/ozl-feedback': 0o700,
            root / 'opt/ozl-feedback/server.py': 0o644,
            root / 'opt/ozl-feedback/install.py': 0o644,
            root / 'opt/ozl-feedback/configuration.py': 0o644,
            root / 'etc/ozl-feedback/nginx-http.conf': 0o644,
            root / 'etc/ozl-feedback/nginx-location.conf': 0o644,
            root / 'etc/systemd/system/ozl-feedback.service': 0o644,
            Path(state_path): 0o600,
            Path(state_path).parent / 'nginx.conf': 0o600,
        }
        for path, mode in required.items():
            self.assertIn((str(path), mode), modes)
            if os.name == 'posix':
                self.assertEqual(path.stat().st_mode & 0o777, mode, str(path))
        if os.name == 'posix':
            print('POSIX_MODE_REPORT=' + json.dumps({str(path.relative_to(root)): {
                'mode': oct(path.stat().st_mode & 0o777), 'inode': path.stat().st_ino,
                'uid': path.stat().st_uid, 'gid': path.stat().st_gid,
                'sha256': hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else None
            } for path in required}, sort_keys=True))
        if os.name == 'posix' and os.geteuid() == 0:
            import pwd
            nobody = pwd.getpwnam('nobody')
            for name in ('', 'opt', 'etc'):
                os.chmod(str(root / name), 0o755)
            # Enter the isolated tree before lowering credentials. Its outer
            # temporary parent remains private and need not be made public.
            def unprivileged():
                os.setgroups([]); os.setgid(nobody.pw_gid); os.setuid(nobody.pw_uid)
            result = subprocess.run([sys.executable, '-c',
                'from pathlib import Path; assert Path("opt/ozl-feedback/server.py").read_bytes(); '
                'assert Path("etc/ozl-feedback/nginx-location.conf").read_bytes(); print("unprivileged-readable")'],
                cwd=str(root), preexec_fn=unprivileged, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            self.assertEqual(result.returncode, 0, result.stderr.decode('utf-8'))

    def test_proxy_probe_requires_backend_json_and_no_store(self):
        module = load('install')
        installer = module.Installer(self.fixture(), run=lambda args: None)
        with patch.object(module.urllib.request, 'urlopen') as request:
            response = request.return_value
            response.code = 400
            response.read.return_value = b'{"error":"invalid_request"}'
            response.headers = {'Cache-Control': 'no-store', 'Content-Type': 'application/json; charset=utf-8'}
            installer.proxy_probe()
            sent = request.call_args[0][0]
            self.assertEqual(sent.get_method(), 'POST')
            self.assertEqual(sent.data, b'{}')
            self.assertEqual(sent.get_header('X-ozl-client-ip'), 'not-an-ip')
            response.read.return_value = b'<html>gateway error</html>'
            with self.assertRaises(RuntimeError):
                installer.proxy_probe()

    def test_reload_readiness_waits_for_transient_old_route(self):
        module = load('install')
        installer = module.Installer(self.fixture(), run=lambda args: None)
        self.assertTrue(hasattr(installer, 'wait_health'), 'reload completion must wait for actual route readiness')
        clock = [0.0]
        calls = []
        def health(*args):
            calls.append(args)
            if len(calls) == 1:
                raise RuntimeError('expected HTTP 405, received HTTP 404')
        installer.health = health
        with patch.object(module.time, 'monotonic', side_effect=lambda: clock[0]), \
                patch.object(module.time, 'sleep', side_effect=lambda duration: clock.__setitem__(0, clock[0] + duration)):
            installer.wait_health(18081, '/api/feedback', 405)
        self.assertEqual(len(calls), 2)
        self.assertGreater(clock[0], 0)

    def test_route_readiness_timeout_preserves_original_and_never_posts(self):
        module = load('install')
        root = self.fixture()
        calls = []
        installer = module.Installer(root, run=lambda args: calls.append(args))
        self.assertTrue(hasattr(installer, 'wait_health'), 'reload completion must wait for actual route readiness')
        installer.account = lambda: (0, 0)
        def health(port, *args):
            if port == 18081:
                raise RuntimeError('expected HTTP 405, received HTTP 404')
        installer.health = health
        installer.proxy_probe = lambda: self.fail('must not post until the reloaded route is ready')
        original = (root / 'etc/ozl-http-guard/nginx.conf').read_bytes()
        protected = installer.protected()
        clock = [0.0]
        with patch.object(module.time, 'monotonic', side_effect=lambda: clock[0]), \
                patch.object(module.time, 'sleep', side_effect=lambda duration: clock.__setitem__(0, clock[0] + duration)), \
                patch.object(os, 'chown', create=True), self.assertRaisesRegex(RuntimeError, 'received HTTP 404'):
            installer.install(ROOT, hashlib.sha256(original).hexdigest())
        self.assertLessEqual(clock[0], 10)
        self.assertEqual((root / 'etc/ozl-http-guard/nginx.conf').read_bytes(), original)
        self.assertEqual(installer.protected(), protected)
        self.assertIn(['systemctl', 'disable', '--now', 'ozl-feedback'], calls)

    def test_health_mismatch_diagnostic_includes_only_statuses(self):
        module = load('install')
        installer = module.Installer(self.fixture(), run=lambda args: None)
        with patch.object(module.urllib.request, 'urlopen') as request:
            response = request.return_value
            response.code = 404
            response.read.return_value = b'private response must not enter diagnostics'
            with self.assertRaisesRegex(RuntimeError, 'expected HTTP 405, received HTTP 404'):
                installer.health(18081, '/api/feedback', 405)

    def failed_archiver(self):
        import contextlib
        import sqlite3
        module = load('archive_failed')
        root = self.fixture()
        for absolute, names in module.SOURCES.items():
            folder = root / absolute.lstrip('/')
            folder.mkdir(mode=0o755)
            for name in names:
                path = folder / name
                path.write_bytes(('reviewed source: ' + name).encode('ascii'))
                path.chmod(0o644)
        unit = root / 'etc/systemd/system/ozl-feedback.service'
        unit.write_bytes(b'reviewed unit')
        unit.chmod(0o644)
        store = root / 'var/lib/ozl-feedback'
        store.mkdir(mode=0o700)
        (store / 'reports').mkdir(mode=0o700)
        (store / 'ip-key').write_bytes(b'k' * 32)
        (store / 'ip-key').chmod(0o600)
        db = sqlite3.connect(str(store / 'receipts.sqlite3'))
        db.execute('CREATE TABLE receipts(id TEXT)'); db.commit(); db.close()
        (store / 'receipts.sqlite3').chmod(0o600)
        backup = root / module.FAILED_STATE.lstrip('/')
        backup.parent.mkdir(mode=0o700, parents=True)
        original = (root / 'etc/ozl-http-guard/nginx.conf').read_bytes()
        expected = {absolute: {name: hashlib.sha256((root / absolute.lstrip('/') / name).read_bytes()).hexdigest()
                    for name in names} for absolute, names in module.SOURCES.items()}
        stack = contextlib.ExitStack()
        self.addCleanup(stack.close)
        stack.enter_context(patch.object(module, 'SOURCES', expected))
        stack.enter_context(patch.object(module, 'UNIT_SHA', hashlib.sha256(unit.read_bytes()).hexdigest()))
        stack.enter_context(patch.object(module, 'ORIGINAL_SHA', hashlib.sha256(original).hexdigest()))
        state = dict(phase='failed', original_sha256=module.ORIGINAL_SHA,
                     patched_sha256=module.PATCHED_SHA, backup=str(backup.parent))
        backup.write_text(json.dumps(state), encoding='utf-8'); backup.chmod(0o600)
        baseline = module.Installer(root).protected()
        (backup.parent / 'protected-sha256.json').write_text(json.dumps(baseline), encoding='utf-8')
        archiver = module.ArchiveFailed(root, uid=0, gid=0)
        calls = []
        def command(args, check=False):
            calls.append(args)
            if 'is-active' in args:
                return subprocess.CompletedProcess(args, 3, 'inactive\n', '')
            if 'is-enabled' in args:
                return subprocess.CompletedProcess(args, 1, 'disabled\n', '')
            return subprocess.CompletedProcess(args, 0, '', '')
        archiver.command = command
        return module, archiver, root, calls, baseline

    @unittest.skipIf(os.name == 'posix' and os.geteuid() != 0, 'owned deployment archive validates real root ownership')
    def test_failed_install_archive_preserves_exact_four_paths_without_delete(self):
        module, archiver, root, calls, baseline = self.failed_archiver()
        inodes = {str(path): path.stat().st_ino for path in archiver.inventory()}
        receipt = archiver.archive()
        self.assertTrue(receipt['archived'])
        self.assertEqual(len(receipt['paths']), 4)
        for entry in receipt['paths']:
            self.assertFalse(Path(entry['source']).exists())
            self.assertTrue(Path(entry['archive']).exists())
            self.assertEqual(Path(entry['archive']).stat().st_ino, inodes[entry['source']])
        self.assertEqual(archiver.installer.protected(), baseline)
        self.assertIn(['systemctl', 'daemon-reload'], calls)
        self.assertEqual(json.loads((root / module.FAILED_STATE.lstrip('/')).read_text())['phase'], 'failed')

    @unittest.skipIf(os.name == 'posix' and os.geteuid() != 0, 'owned deployment archive validates real root ownership')
    def test_failed_install_archive_rejects_changed_source_or_any_receipt(self):
        module, archiver, root, calls, baseline = self.failed_archiver()
        path = root / 'opt/ozl-feedback/server.py'
        original = path.read_bytes()
        path.write_bytes(b'external change')
        with self.assertRaisesRegex(RuntimeError, 'Reviewed source changed'):
            archiver.archive()
        self.assertTrue(path.exists())
        path.write_bytes(original)
        unexpected = path.parent / 'unknown-admin-file'
        unexpected.write_bytes(b'preserve me')
        with self.assertRaisesRegex(RuntimeError, 'Unexpected file'):
            archiver.archive()
        self.assertEqual(unexpected.read_bytes(), b'preserve me')
        unexpected.unlink()
        import sqlite3
        db = sqlite3.connect(str(root / 'var/lib/ozl-feedback/receipts.sqlite3'))
        db.execute('INSERT INTO receipts VALUES (?)', ('a' * 32,)); db.commit(); db.close()
        with self.assertRaisesRegex(RuntimeError, 'Accepted feedback exists'):
            archiver.archive()
        self.assertTrue(path.exists())
        self.assertNotIn(['systemctl', 'daemon-reload'], calls)
        self.assertEqual(archiver.installer.protected(), baseline)


if __name__ == '__main__':
    unittest.main()
