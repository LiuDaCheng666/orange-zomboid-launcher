"""Run with Python on Linux; all service commands are local temporary doubles."""
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

@unittest.skipUnless(os.name == 'posix', 'activation runs on the Linux server')
class ActivationTests(unittest.TestCase):
    def setUp(self):
        module_path = Path(__file__).resolve().parents[1] / 'deploy/deploy_site.py'
        if not module_path.exists(): module_path = Path(__file__).with_name('deploy_site.py')
        spec = importlib.util.spec_from_file_location('deploy_site', module_path)
        self.module = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.module)
        self.temp = tempfile.TemporaryDirectory(prefix='ozl-activation-test-'); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.base, self.config, self.logs, self.bin = [self.root / name for name in ['base', 'etc', 'logs', 'bin']]
        self.identifier = '20261023T000000Z-aabbccdd'
        self.site = self.base / 'sites' / self.identifier
        for directory in [self.site / 'deployment-config', self.config, self.logs, self.bin, self.base / 'sites/old']:
            directory.mkdir(parents=True)
        (self.base / 'current').symlink_to(self.base / 'sites/old')
        (self.config / 'Caddyfile').write_text('old config')
        (self.config / 'ozl-static.caddy').write_text('old include')
        (self.site / 'deployment-config/Caddyfile.ip').write_text('new config')
        (self.site / 'deployment-config/Caddyfile.domain').write_text('new TLS config')
        (self.site / 'deployment-config/ozl-static.caddy').write_text('new include')
        fake = '''#!/bin/sh
name=$(basename "$0")
printf '%s\\n' "$name $*" >> "$OZL_TEST_LOG"
case "$name" in
 runuser) shift 3; exec "$@" ;;
 caddy) if [ "$FAIL_MODE" = validate ]; then exit 40; fi ;;
 systemctl)
  if [ "$PRIOR_ACTIVE" = 0 ] && [ "$1" = is-enabled ]; then exit 1; fi
  if [ "$PRIOR_ACTIVE" = 0 ] && [ "$1" = is-active ] && [ ! -f "$OZL_TEST_ACTIVE" ]; then exit 1; fi
  if [ "$1" = start ]; then touch "$OZL_TEST_ACTIVE"; fi
  if [ "$1" = stop ] && [ -f "$OZL_TEST_ACTIVE" ]; then unlink "$OZL_TEST_ACTIVE"; fi
  if [ "$1" = reload ] && [ "$FAIL_MODE" = reload ] && [ ! -f "$OZL_TEST_MARKER" ]; then touch "$OZL_TEST_MARKER"; exit 41; fi ;;
 curl) if [ "$FAIL_MODE" = health ]; then exit 42; fi ;;
esac
exit 0
'''
        for name in ['runuser', 'caddy', 'systemctl', 'curl', 'chown']:
            file = self.bin / name; file.write_text(fake); file.chmod(0o755)

    def execute(self, mode, prior_active=True, domain=False):
        env = dict(os.environ, PATH=str(self.bin) + ':' + os.environ['PATH'], FAIL_MODE=mode, PRIOR_ACTIVE='1' if prior_active else '0', OZL_TEST_ACTIVE=str(self.root / 'active'), OZL_TEST_LOG=str(self.root / 'commands'), OZL_TEST_MARKER=str(self.root / 'failed-once'))
        args = [self.identifier, str(self.base), str(self.config), str(self.logs)]
        try: script = self.module.activation_script(*args, **({'domain': True} if domain else {}))
        except TypeError: self.fail('Domain activation must select HTTPS configuration, not downgrade to IP HTTP')
        return subprocess.run(['bash', '-se'], input=script, stdout=subprocess.PIPE, stderr=subprocess.PIPE, universal_newlines=True, env=env)

    def test_success_switches_to_new_site_and_validates_as_service_user(self):
        result = self.execute('')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.base / 'current').resolve(), self.site)
        self.assertEqual((self.config / 'Caddyfile').read_text(), 'new config')
        commands = (self.root / 'commands').read_text()
        self.assertIn('runuser -u caddy -- caddy validate', commands)

    def test_domain_deploy_activates_https_and_probes_valid_tls(self):
        result = self.execute('', domain=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.config / 'Caddyfile').read_text(), 'new TLS config')
        commands = (self.root / 'commands').read_text()
        self.assertIn('https://orangezl.com/release.json', commands)
        self.assertIn('--resolve orangezl.com:443:127.0.0.1', commands)
        self.assertNotIn('--insecure', commands)

    def test_domain_health_failure_restores_previous_tls_config(self):
        (self.config / 'Caddyfile').write_text('previous TLS config')
        result = self.execute('health', domain=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.config / 'Caddyfile').read_text(), 'previous TLS config')
        self.assertEqual((self.base / 'current').resolve(), self.base / 'sites/old')

    def check_failure(self, mode):
        result = self.execute(mode)
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.base / 'current').resolve(), self.base / 'sites/old')
        self.assertEqual((self.config / 'Caddyfile').read_text(), 'old config')
        self.assertEqual((self.config / 'ozl-static.caddy').read_text(), 'old include')
        self.assertIn('previous configuration and site restored', result.stderr)

    def test_validation_failure_restores_config(self): self.check_failure('validate')
    def test_service_reload_failure_restores_config_and_site(self): self.check_failure('reload')
    def test_health_failure_restores_config_and_site(self): self.check_failure('health')

    def test_bootstrap_failure_restores_absent_config_site_and_service(self):
        (self.base / 'current').unlink()
        (self.config / 'Caddyfile').unlink()
        (self.config / 'ozl-static.caddy').unlink()
        result = self.execute('health', prior_active=False)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.base / 'current').is_symlink())
        self.assertFalse((self.config / 'Caddyfile').exists())
        self.assertFalse((self.config / 'ozl-static.caddy').exists())
        commands = (self.root / 'commands').read_text()
        self.assertIn('systemctl stop caddy', commands)
        self.assertIn('systemctl disable caddy', commands)

if __name__ == '__main__': unittest.main()
