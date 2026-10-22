const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('../Orange3DTool/Web/playwright-runtime.cjs');
const root = path.resolve(__dirname, '../website');
let server, browser, base;
before(async () => {
  server = http.createServer(async (req, res) => {
    try {
      const name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
      const file = path.join(root, name === '/' ? 'index.html' : name);
      if (!file.startsWith(root + path.sep)) throw Error('path');
      const bytes = await fs.readFile(file);
      const mime = { '.html': 'text/html', '.css': 'text/css', '.js': 'text/javascript', '.json': 'application/json', '.png': 'image/png', '.ico': 'image/x-icon' }[path.extname(file)] || 'application/octet-stream';
      res.writeHead(200, { 'Content-Type': mime }); res.end(bytes);
    } catch { res.writeHead(404); res.end('Not found'); }
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  base = process.env.OZL_SITE_URL?.replace(/\/$/, '') || 'http://127.0.0.1:' + server.address().port;
  browser = await chromium.launch({ headless: true });
});
after(async () => { await browser?.close(); await new Promise(resolve => server?.close(resolve)); });

test('desktop and mobile expose real downloads without horizontal overflow', async () => {
  for (const width of [1440, 1280, 768, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const response = await page.goto(base);
    assert.equal(response.status(), 200, 'website must serve an actual page');
    await page.locator('[data-release-status]').filter({ hasText: '1.2.13' }).waitFor();
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.ok(await page.locator('[data-download="setup"]').first().isVisible());
    assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), '/releases/v1.2.13/orange-launcher-setup-v1.2.13.exe');
    assert.equal(await page.locator('[data-download="portable"]').getAttribute('href'), '/releases/v1.2.13/orange-launcher-v1.2.13-win-x64.zip');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    assert.equal(errors.length, 0);
    if (width === 1440 || width === 390) {
      await fs.mkdir(path.resolve(__dirname, '../artifacts/website-shots'), { recursive: true });
      await page.screenshot({ path: path.resolve(__dirname, `../artifacts/website-shots/${width}.png`), fullPage: true });
      if (width === 1440) await page.screenshot({ path: path.resolve(__dirname, '../artifacts/website-shots/home-viewport.png') });
    }
    await page.close();
  }
});

test('source, registration and keyboard-operable help have explicit destinations', async () => {
  const page = await browser.newPage(); await page.goto(base);
  assert.ok(await page.locator('a[href="https://github.com/LiuDaCheng666/orange-zomboid-launcher"]').count() >= 2);
  assert.equal(await page.getByRole('link', { name: '辽ICP备2026024866号-1' }).getAttribute('href'), 'https://beian.miit.gov.cn/');
  const summary = page.locator('details summary').first();
  await summary.focus(); await page.keyboard.press('Enter');
  assert.equal(await summary.locator('..').getAttribute('open'), '');
  await page.close();
});

test('failed metadata retains a usable official GitHub fallback', async () => {
  const page = await browser.newPage();
  await page.route('**/release.json', route => route.abort());
  await page.goto(base); await page.locator('[data-release-status]').filter({ hasText: 'GitHub' }).waitFor();
  assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), 'https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest');
  assert.ok(await page.getByRole('link', { name: /GitHub 备用/ }).count() > 0);
  await page.close();
});

test('untrusted metadata cannot inject markup or send downloads to a foreign host', async () => {
  const page = await browser.newPage();
  const hostile = { version: '<img src=x onerror="window.injected=1">', setup: { url: 'https://evil.example/a.exe' } };
  await page.route('**/release.json', route => route.fulfill({ json: hostile }));
  await page.goto(base); await page.locator('[data-release-status]').filter({ hasText: 'GitHub' }).waitFor();
  assert.equal(await page.evaluate(() => window.injected), undefined);
  assert.equal(await page.locator('a[href^="https://evil.example"]').count(), 0);
  assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), 'https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest');
  await page.close();
});

test('deployed metadata cannot be cached while versioned downloads are immutable', { skip: !process.env.OZL_SITE_URL }, async () => {
  const metadata = await fetch(base + '/updates/latest/update-manifest.json', { method: 'HEAD' });
  assert.equal(metadata.status, 200);
  assert.ok(metadata.headers.get('cache-control').includes('no-store'));
  const download = await fetch(base + '/releases/v1.2.13/orange-launcher-setup-v1.2.13.exe', { method: 'HEAD' });
  assert.equal(download.headers.get('cache-control'), 'public, max-age=31536000, immutable');
  assert.equal(download.headers.get('content-disposition'), 'attachment');
  assert.equal(download.headers.get('content-length'), '15315968');
});
