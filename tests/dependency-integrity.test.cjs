const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const crypto = require('node:crypto');
const { spawnSync } = require('node:child_process');
const root = path.resolve(__dirname, '..');
const paths = ['lib/net462/Microsoft.Web.WebView2.Core.dll', 'lib/net462/Microsoft.Web.WebView2.WinForms.dll', 'runtimes/win-x64/native/WebView2Loader.dll'];
const q = s => "'" + s.replaceAll("'", "''") + "'";
function fixture(fn) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'ozl-integrity-'));
  try {
    const lock = { schema: 1, package: { id: 'Microsoft.Web.WebView2', version: '1.0.4258.31', url: 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/1.0.4258.31/microsoft.web.webview2.1.0.4258.31.nupkg', catalog: 'https://api.nuget.org/v3/catalog0/data/2026.09.28.08.32.55/microsoft.web.webview2.1.0.4258.31.json', packageSha512: crypto.createHash('sha512').update('package').digest('base64') }, files: paths.map((p, i) => ({ path: p, sha256: crypto.createHash('sha256').update('ordinary bytes ' + i).digest('hex') })) };
    paths.forEach((p, i) => { fs.mkdirSync(path.dirname(path.join(dir, p)), { recursive: true }); fs.writeFileSync(path.join(dir, p), 'ordinary bytes ' + i); });
    fs.writeFileSync(path.join(dir, 'package'), 'package');
    fn(dir, lock);
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
}
function validate(dir, lock, kind = 'Dependencies') {
  const lockPath = path.join(dir, 'lock.json'); fs.writeFileSync(lockPath, JSON.stringify(lock));
  return spawnSync('pwsh.exe', ['-NoProfile', '-NonInteractive', '-Command', `$ErrorActionPreference='Stop'; . ${q(path.join(root, 'build/DependencyIntegrity.ps1'))}; Assert-OzlWebView2${kind} -${kind === 'Package' ? 'PackagePath' : 'SdkDirectory'} ${q(kind === 'Package' ? path.join(dir, 'package') : dir)} -LockPath ${q(lockPath)}`], { encoding: 'utf8' });
}
for (const altered of paths.slice(1)) test(`cached preparation rejects altered ${altered}`, () => {
  fixture((dir, lock) => {
    // Isolate only the existing validation statement; never execute preparation.
    const source = fs.readFileSync(path.join(root, 'Prepare-Dependencies.ps1'), 'utf8');
    if (source.includes('Assert-OzlWebView2Dependencies')) {
      fs.appendFileSync(path.join(dir, altered), '!');
      const lockPath = path.join(dir, 'lock.json'); fs.writeFileSync(lockPath, JSON.stringify(lock));
      const statement = source.split(/\r?\n/).find(l => l.startsWith('Assert-OzlWebView2Dependencies'));
      const result = spawnSync('pwsh.exe', ['-NoProfile', '-NonInteractive', '-Command', `$ErrorActionPreference='Stop'; . ${q(path.join(root, 'build/DependencyIntegrity.ps1'))}; $sdk=${q(dir)}; ${statement} -LockPath ${q(lockPath)}`], { encoding: 'utf8' });
      assert.notEqual(result.status, 0); assert.match(result.stderr, /dependency hash mismatch/);
    } else {
      const statement = source.split(/\r?\n/).find(l => l.startsWith('if((Get-FileHash -LiteralPath $core'));
      assert.ok(statement);
      for (const p of [altered]) {
        fs.appendFileSync(path.join(dir, p), '!');
        const isolated = statement.replace('D60E6E94245078CB56E44EBD5F360DC04BF18E460136BE93BFC4CFCA43B98505', lock.files[0].sha256);
        const result = spawnSync('pwsh.exe', ['-NoProfile', '-NonInteractive', '-Command', `$ErrorActionPreference='Stop'; $core=${q(path.join(dir, paths[0]))}; ${isolated}`], { encoding: 'utf8' });
        assert.notEqual(result.status, 0, `cached validation accepted altered ${p}`);
      }
    }
  });
});
test('accepts all three files and package silently', () => fixture((dir, lock) => { for (const kind of ['Dependencies', 'Package']) { const r = validate(dir, lock, kind); assert.equal(r.status, 0, r.stderr); assert.equal(r.stdout, ''); } }));
for (const p of paths) {
  test(`rejects missing ${p}`, () => fixture((dir, lock) => { fs.unlinkSync(path.join(dir, p)); assert.notEqual(validate(dir, lock).status, 0); }));
  test(`rejects changed ${p}`, () => fixture((dir, lock) => { fs.appendFileSync(path.join(dir, p), '!'); assert.notEqual(validate(dir, lock).status, 0); }));
}
for (const [name, change] of Object.entries({ duplicate: l => l.files[2] = l.files[0], unknown: l => l.files[2].path = 'unknown.dll', escaping: l => l.files[2].path = '../outside.dll', malformed: l => l.files[2].sha256 = 'oops', schema: l => l.schema = 2, packageDigest: l => l.package.packageSha512 = 'oops', missingEntry: l => l.files.pop() })) {
  test(`rejects ${name} lock`, () => fixture((dir, lock) => { change(lock); assert.notEqual(validate(dir, lock).status, 0); }));
}
test('rejects mismatched package', () => fixture((dir, lock) => { fs.appendFileSync(path.join(dir, 'package'), '!'); assert.notEqual(validate(dir, lock, 'Package').status, 0); }));
