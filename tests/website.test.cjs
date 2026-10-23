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
    await page.locator('[data-version]').filter({ hasText: '1.2.13' }).waitFor();
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.ok(await page.locator('[data-download="setup"]').first().isVisible());
    assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), '/releases/v1.2.13/orange-launcher-setup-v1.2.13.exe');
    assert.equal(await page.locator('[data-download="portable"]').getAttribute('href'), '/releases/v1.2.13/orange-launcher-v1.2.13-win-x64.zip');
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    if (width === 1440) assert.ok(await page.evaluate(() => document.documentElement.scrollHeight <= 2700), 'homepage stays compact');
    assert.equal(errors.length, 0);
    if ([1440, 768, 390].includes(width)) {
      await fs.mkdir(path.resolve(__dirname, '../artifacts/website-shots'), { recursive: true });
      await page.screenshot({ path: path.resolve(__dirname, `../artifacts/website-shots/${width}.png`), fullPage: true, animations: 'disabled' });
      if (width === 1440) await page.screenshot({ path: path.resolve(__dirname, '../artifacts/website-shots/home-viewport.png'), animations: 'disabled' });
    }
    await page.close();
  }
});

test('source, registration and keyboard-operable help have explicit destinations', async () => {
  const page = await browser.newPage(); await page.goto(base);
  assert.ok(await page.locator('a[href="https://github.com/LiuDaCheng666/orange-zomboid-launcher"]').count() >= 2);
  assert.equal(await page.getByRole('link', { name: '辽ICP备2026024866号-1' }).getAttribute('href'), 'https://beian.miit.gov.cn/');
  await page.getByRole('link', { name: '使用帮助', exact: true }).first().click();
  assert.ok(page.url().endsWith('/help.html'));
  const summary = page.locator('details summary').first();
  await summary.focus(); await page.keyboard.press('Enter');
  assert.equal(await summary.locator('..').getAttribute('open'), '');
  await page.close();
});

test('feature tabs support keyboard navigation, readable enlargement and reduced motion', async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' });
  await page.goto(base);
  assert.equal(await page.getByRole('tab').count(), 5);
  await page.getByRole('tab', { name: '模组配置', exact: true }).click();
  const panel = page.getByRole('tabpanel');
  assert.ok(await panel.filter({ hasText: '模组清单' }).isVisible());
  assert.match(await panel.locator('img').getAttribute('src'), /launcher-mods-dark/);
  await page.keyboard.press('ArrowRight');
  assert.equal(await page.getByRole('tab', { name: '服务器连接', exact: true }).getAttribute('aria-selected'), 'true');
  await page.keyboard.press('Home');
  assert.equal(await page.getByRole('tab', { name: '启动与存档', exact: true }).getAttribute('aria-selected'), 'true');
  await page.getByRole('button', { name: '查看大图 ↗' }).click();
  assert.ok(await page.getByRole('dialog').isVisible());
  await page.keyboard.press('Escape');
  assert.equal(await page.getByRole('dialog').count(), 0);
  assert.equal(await page.getByRole('button', { name: '查看大图 ↗' }).evaluate(el => el === document.activeElement), true);
  assert.equal(await panel.evaluate(el => el.getAnimations().length), 0);
  await page.close();
});

test('feature navigation explains every category before selection and shows concrete selected capabilities', async () => {
  for (const width of [1440, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    await page.goto(base + '/index.html#features');
    const tabs = page.getByRole('tab');
    assert.equal(await tabs.count(), 5);
    for (const tab of await tabs.all()) {
      const summary = tab.locator('.feature-summary');
      assert.equal(await summary.count(), 1, 'each function needs a visible explanation, not just a name');
      assert.ok(await summary.isVisible());
      assert.ok((await summary.innerText()).trim().length >= 14);
      const visibleName = await tab.locator('.feature-name').innerText();
      assert.equal(await page.getByRole('tab', { name: visibleName, exact: true }).count(), 1, 'the visible label must also work for voice control and screen readers');
    }
    const before = await page.locator('[data-feature-points]').innerText();
    await page.getByRole('tab', { name: '组件安装', exact: true }).click();
    assert.equal(await page.locator('[data-feature-points] li').count(), 3);
    assert.notEqual(await page.locator('[data-feature-points]').innerText(), before);
    assert.ok(await page.getByRole('tabpanel').isVisible());
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    await page.close();
  }
});

test('selecting a feature plays a short transition and reduced-motion users get an immediate change', async () => {
  for (const reducedMotion of ['no-preference', 'reduce']) {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion });
    await page.goto(base + '/index.html#features');
    await page.evaluate(() => Promise.all(document.getAnimations().map(animation => animation.finished.catch(() => {}))));
    await page.getByRole('tab', { name: '组件安装', exact: true }).click();
    const durations = await page.getByRole('tabpanel').evaluate(panel => panel.getAnimations().filter(animation => animation.playState === 'running').map(animation => animation.effect.getTiming().duration));
    if (reducedMotion === 'reduce') assert.deepEqual(durations, []);
    else assert.ok(durations.some(duration => duration >= 180 && duration <= 460), 'a user selection should visibly transition, not jump');
    await page.getByRole('tab', { name: '服务器连接', exact: true }).click();
    await page.getByRole('tab', { name: '模组配置', exact: true }).click();
    assert.equal(await page.getByRole('tab', { name: '模组配置', exact: true }).getAttribute('aria-selected'), 'true');
    assert.match(await page.locator('[data-feature-image]').getAttribute('src'), /launcher-mods-dark/);
    await page.close();
  }
});

test('motion lifecycle cancels obsolete effects on close, hidden document and preference change', async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  await page.goto(base);
  await page.evaluate(() => Promise.all(document.getAnimations().map(a => a.finished.catch(() => {}))));
  const state = await page.evaluate(() => {
    document.querySelector('[data-enlarge]').click();
    const dialog = document.querySelector('dialog');
    const effects = dialog.getAnimations();
    document.querySelector('[data-close]').click();
    return { count: effects.length, running: effects.some(a => a.playState === 'running'), open: dialog.open };
  });
  assert.ok(state.count > 0, 'opening has a short native DOM animation');
  assert.equal(state.open, false, 'close takes effect synchronously');
  assert.equal(state.running, false, 'closing must cancel the opening effect');
  await page.waitForFunction(() => document.activeElement === document.querySelector('[data-enlarge]'));
  const reopened = await page.evaluate(async () => {
    const dialog = document.querySelector('dialog');
    document.querySelector('[data-enlarge]').click();
    document.querySelector('[data-close]').click();
    document.querySelector('[data-enlarge]').click();
    const effect = dialog.getAnimations()[0];
    await new Promise(resolve => dialog.addEventListener('close', resolve, { once: true }));
    return { open: dialog.open, cancelled: effect.playState === 'idle' };
  });
  assert.equal(reopened.open, true);
  assert.equal(reopened.cancelled, false, 'a queued old close must not cancel the reopened dialog');
  await page.keyboard.press('Escape');
  const hidden = await page.evaluate(() => {
    document.querySelector('#tab-components').click();
    document.querySelector('#tab-mods').click();
    const panel = document.querySelector('[role=tabpanel]');
    const before = panel.getAnimations().filter(a => a.playState === 'running').length;
    Object.defineProperty(document, 'hidden', { configurable: true, value: true });
    document.dispatchEvent(new Event('visibilitychange'));
    document.querySelector('#tab-servers').click();
    return { before, after: panel.getAnimations().length, selected: panel.getAttribute('aria-labelledby') };
  });
  assert.equal(hidden.before, 1, 'rapid selections replace the prior effect');
  assert.equal(hidden.after, 0, 'hidden document cancels and suppresses effects');
  assert.equal(hidden.selected, 'tab-servers');
  await page.evaluate(() => { delete document.hidden; document.dispatchEvent(new Event('visibilitychange')); document.querySelector('[data-enlarge]').click(); });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.waitForFunction(() => document.getAnimations().length === 0);
  assert.ok(await page.getByRole('dialog').isVisible());
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('dialog').evaluate(el => el.open), false);
  await page.close();
});

test('feature explanations and the initial screenshot remain readable without JavaScript', async () => {
  const page = await browser.newPage({ javaScriptEnabled: false, viewport: { width: 390, height: 900 } });
  await page.goto(base);
  assert.equal(await page.locator('.feature-summary').count(), 5);
  assert.equal(await page.locator('[data-feature-points] li').count(), 3);
  assert.ok(await page.locator('[data-feature-image]').evaluate(image => image.complete && image.naturalWidth >= 1280));
  assert.ok(await page.getByRole('link', { name: '下载 OZL ↓', exact: true }).isVisible());
  assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
  await page.close();
});

test('help and text-only changes have no historical download links and fit narrow screens', async () => {
  for (const width of [390, 768, 1280, 1440]) for (const name of ['help', 'changes']) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    await page.goto(base);
    await page.getByRole('link', { name: name === 'help' ? '使用帮助' : '更新记录', exact: true }).first().click();
    assert.ok(page.url().endsWith(`/${name}.html`));
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.equal(await page.locator('a[href*="/releases/download/"], a[href$=".exe"], a[href$=".zip"]').count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    assert.equal(await page.locator('nav [aria-current="page"]').getAttribute('href'), `${name}.html`);
    assert.deepEqual(errors, []);
    await page.screenshot({ path: path.resolve(__dirname, `../artifacts/website-shots/${name}-${width}.png`), fullPage: true, animations: 'disabled' });
    await page.close();
  }
});

test('secondary pages preserve native navigation and help disclosure without scripts', async () => {
  const page = await browser.newPage({ javaScriptEnabled: false, viewport: { width: 390, height: 900 } });
  for (const name of ['help', 'changes']) {
    await page.goto(base + '/' + name + '.html');
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    if (name === 'help') {
      await page.locator('summary').first().focus();
      await page.keyboard.press('Enter');
      assert.equal(await page.locator('details').first().getAttribute('open'), '');
    }
  }
  await page.close();
});

test('all actual preview assets load on demand in both themes and enlarge at native resolution', async () => {
  await fs.mkdir(path.resolve(__dirname, '../artifacts/website-shots'), { recursive: true });
  for (const width of [1440, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    const requested = [];
    page.on('request', request => { if (request.url().includes('/assets/launcher-')) requested.push(request.url()); });
    await page.goto(base);
    await page.locator('[data-feature-image]').evaluate(image => image.decode());
    assert.equal(requested.length, 1, 'unselected screenshots are not eagerly fetched');
    for (const theme of ['dark', 'light']) {
      await page.getByLabel('预览主题').selectOption(theme);
      for (const [id, name] of [['home', '启动与存档'], ['components', '组件安装'], ['mods', '模组配置'], ['servers', '服务器连接'], ['maintenance', '内存与维护']]) {
        await page.getByRole('tab', { name, exact: true }).click();
        const shot = page.locator('[data-feature-image]');
        await shot.evaluate(image => image.decode());
        assert.match(await shot.getAttribute('src'), new RegExp('launcher-' + id + '-' + theme));
        assert.ok(await shot.evaluate(image => image.naturalWidth >= 1280));
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
        if (width === 1440) assert.ok(await page.evaluate(() => document.documentElement.scrollHeight <= 2700));
        await page.evaluate(() => Promise.all(document.getAnimations().map(animation => animation.finished.catch(() => {}))));
        await page.locator('#features').screenshot({ path: path.resolve(__dirname, '../artifacts/website-shots/preview-' + id + '-' + theme + '-' + width + '.png'), animations: 'disabled' });
      }
    }
    await page.getByRole('button', { name: '查看大图 ↗', exact: true }).click();
    assert.ok(await page.locator('[data-dialog-image]').evaluate(image => image.width >= 1280));
    await page.screenshot({ path: path.resolve(__dirname, '../artifacts/website-shots/enlarged-' + width + '.png'), animations: 'disabled' });
    await page.getByRole('button', { name: '关闭大图 ×' }).click();
    console.log('Homepage dimensions ' + JSON.stringify(await page.evaluate(() => ({ viewport: innerWidth, width: document.documentElement.scrollWidth, height: document.documentElement.scrollHeight }))));
    await page.close();
  }
});

test('failed metadata retains a usable official GitHub fallback', async () => {
  const page = await browser.newPage();
  await page.route('**/release.json', route => route.abort());
  await page.goto(base); await page.locator('[data-release-status]').filter({ hasText: 'GitHub' }).waitFor();
  assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), 'https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/latest');
  assert.ok(await page.getByRole('link', { name: /GitHub 备用/ }).count() > 0);
  await page.close();
});

test('QQ group number is visible and can be copied from every page', async () => {
  const context = await browser.newContext({ permissions: ['clipboard-read', 'clipboard-write'] });
  const page = await context.newPage();
  for (const name of ['index', 'help', 'changes']) {
    await page.goto(base + '/' + name + '.html');
    assert.ok(await page.getByText('QQ群 1124858561', { exact: true }).isVisible());
    await page.getByRole('button', { name: '复制群号', exact: true }).click();
    assert.equal(await page.evaluate(() => navigator.clipboard.readText()), '1124858561');
    assert.ok(await page.getByRole('status').filter({ hasText: '已复制群号' }).isVisible());
  }
  await context.close();
});

test('clipboard rejection leaves the exact group available for manual copy', async () => {
  const page=await browser.newPage();
  await page.addInitScript(()=>Object.defineProperty(navigator,'clipboard',{value:{writeText:async()=>{throw new Error('denied');}}}));
  for(const name of ['index','help','changes']){
    await page.goto(base+'/'+name+'.html');
    await page.getByRole('button',{name:'复制群号',exact:true}).click();
    await page.locator('[data-copy-status]').filter({hasText:'请手动复制群号 1124858561'}).waitFor();
  }
  await page.close();
});

test('every page links to the user-provided QQ invitation with safe new-tab attributes', async () => {
  const page = await browser.newPage();
  for (const name of ['index', 'help', 'changes']) {
    await page.goto(base + '/' + name + '.html');
    const invitation = page.getByRole('link', { name: '加入QQ群', exact: true });
    assert.equal(await invitation.count(), 1);
    assert.ok(await invitation.isVisible());
    assert.equal(await invitation.getAttribute('href'), 'https://qm.qq.com/q/cuLGbwUx4A');
    assert.equal(await invitation.getAttribute('target'), '_blank');
    assert.equal(await invitation.getAttribute('rel'), 'noopener noreferrer');
  }
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
