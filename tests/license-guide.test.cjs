const assert = require('node:assert/strict');
const { test } = require('node:test');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');

const repo = path.resolve(__dirname, '..');
const script = path.join(repo, 'Build-Guide.ps1');
const inputs = ['使用说明.md', 'docs/THIRD-PARTY.md', 'docs/LICENSING.md', 'LICENSE'];
const fixtureContents = [
  '# 使用说明\n中文与 emoji：🍊\n',
  '# Third party\nOriginal vendor notice must remain.\n',
  '# OZL\nSPDX-License-Identifier: GPL-3.0-only\nhttps://github.com/LiuDaCheng666/orange-zomboid-launcher\n',
  'GNU GENERAL PUBLIC LICENSE\nVersion 3, 29 June 2007\n\nAll fixture license paragraphs.\n',
];

function createFixture(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'ozl-license-guide-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.mkdirSync(path.join(root, 'docs'));
  inputs.forEach((input, i) => fs.writeFileSync(path.join(root, input), fixtureContents[i], 'utf8'));
  return root;
}

function generate(root, output) {
  return spawnSync('powershell.exe', ['-NoProfile', '-NonInteractive', '-File', script,
    '-SourceRoot', root, '-OutputPath', output], { windowsHide: true, encoding: 'utf8' });
}

test('guide includes the complete original text of all four inputs in order, as UTF-8 without BOM', t => {
  const root = createFixture(t);
  const output = path.join(root, '使用说明.txt');
  const result = generate(root, output);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  const bytes = fs.readFileSync(output);
  assert.notEqual(bytes.subarray(0, 3).toString('hex'), 'efbbbf');
  assert.equal(bytes.toString('utf8'), fixtureContents.join('\r\n\r\n'));
  assert.deepEqual(fs.readdirSync(root).sort(), ['LICENSE', 'docs', '使用说明.md', '使用说明.txt'].sort());
});

for (const missing of inputs) {
  test(`missing ${missing} fails without replacing an existing guide`, t => {
    const root = createFixture(t);
    fs.unlinkSync(path.join(root, missing));
    const output = path.join(root, '使用说明.txt');
    fs.writeFileSync(output, 'existing guide', 'utf8');
    const result = generate(root, output);
    assert.notEqual(result.status, 0, 'Missing legal input must fail generation');
    assert.match(result.stderr, /Required guide input is missing/);
    assert.equal(fs.readFileSync(output, 'utf8'), 'existing guide');
  });
}

test('actual release guide contains the complete tracked GPL and third-party notices', t => {
  const root = createFixture(t);
  const output = path.join(root, 'actual-guide.txt');
  const result = generate(repo, output);
  assert.equal(result.status, 0, result.stderr || result.error?.message);
  const expected = inputs.map(input => fs.readFileSync(path.join(repo, input), 'utf8'));
  assert.equal(fs.readFileSync(output, 'utf8'), expected.join('\r\n\r\n'));
  assert.match(expected[2], /GPL-3\.0-only/);
  assert.match(expected[2], /github\.com\/LiuDaCheng666\/orange-zomboid-launcher/);
  assert.match(expected[3], /END OF TERMS AND CONDITIONS/);
});
