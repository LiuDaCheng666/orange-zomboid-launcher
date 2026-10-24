const { test, before, after } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const fs = require('node:fs/promises');
const path = require('node:path');
const { chromium } = require('../Orange3DTool/Web/playwright-runtime.cjs');
const root = path.resolve(__dirname, '../website');
const approvedStaticDownloads = {
  setup: '/releases/v1.2.16/OrangeZomboidLauncher-win-Setup.exe',
  portable: '/releases/v1.2.16/OrangeZomboidLauncher-win-Portable.zip'
};
async function assertApprovedStaticDownloads(page) {
  for (const [kind, href] of Object.entries(approvedStaticDownloads)) {
    const links = page.locator(`[data-download="${kind}"]`);
    assert.ok(await links.count() > 0);
    for (const link of await links.all()) assert.equal(await link.getAttribute('href'), href);
  }
}
let server, browser, base;
function assertPreviewExposure(shot, viewportHeight) {
  assert.ok(shot && Math.min(shot.y + shot.height, viewportHeight) - Math.max(shot.y, 0) >= 180, `desktop first screen exposes at least 180px of the launcher preview; got ${JSON.stringify(shot)}`);
}
test('preview exposure rejects collapsed, undersized and clipped images', () => {
  for (const shot of [{ y: 300, height: 0 }, { y: 300, height: 179 }, { y: -100, height: 200 }]) {
    assert.throws(() => assertPreviewExposure(shot, 900), /at least 180px/);
  }
  assertPreviewExposure({ y: 720, height: 180 }, 900);
});
before(async () => {
  server = http.createServer(async (req, res) => {
    try {
      const name = decodeURIComponent(new URL(req.url, 'http://localhost').pathname);
      const file = path.join(root, name === '/' ? 'index.html' : ['/help','/changes'].includes(name) ? name+'.html' : name);
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

test('glass framing exposes translucent layered navigation, choices and preview', async () => {
  const page = await browser.newPage({ reducedMotion: 'reduce' });
  try {
    await page.goto(base);
    for (const selector of ['.site-header', '.tabs button', '[role=tabpanel]', '.panel-copy']) {
      const style = await page.locator(selector).first().evaluate(el => {
        const s = getComputedStyle(el);
        return { backdrop: s.backdropFilter, shadow: s.boxShadow, background: s.backgroundColor };
      });
      assert.match(style.backdrop, /blur\(/, `${selector} refracts the ambient layers`);
      assert.notEqual(style.shadow, 'none', `${selector} has a lit edge and depth`);
      assert.match(style.background, /rgba\(/, `${selector} remains translucent`);
    }
  } finally { await page.close(); }
});

test('rapid feature selection retires the prior glass control motion', async () => {
  const page = await browser.newPage();
  try {
    await page.goto(base);
    const result = await page.evaluate(() => {
      const first = document.querySelector('#tab-components');
      const next = document.querySelector('#tab-mods');
      first.click();
      const started = first.getAnimations().some(a => a.constructor.name === 'Animation');
      next.click();
      return { started, previous: first.getAnimations().filter(a => a.constructor.name === 'Animation').length, next: next.getAnimations().filter(a => a.constructor.name === 'Animation').length };
    });
    assert.equal(result.started, true, 'selected glass control visibly settles into place');
    assert.equal(result.previous, 0, 'obsolete control effect is cancelled');
    assert.equal(result.next, 1);
  } finally { await page.close(); }
});

test('showcase keeps a readable full screenshot and five horizontal choices on desktop', async () => {
  for (const width of [1440, 1280]) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    try {
      await page.goto(base);
      const shot = await page.locator('[data-feature-image]').boundingBox();
      assert.ok(shot.width >= 1050, `software preview must remain readable: ${shot.width}px at ${width}`);
      assert.ok(Math.abs(shot.width / shot.height - 1.6) < .015, 'show the complete screenshot proportionally');
      assert.ok(900 - shot.y >= 360, `first screen must expose 360px of the software preview: top=${shot.y} at ${width}`);
      const boxes = await Promise.all((await page.getByRole('tab').all()).map(tab => tab.boundingBox()));
      assert.equal(boxes.length, 5);
      assert.ok(boxes.every(box => Math.abs(box.y - boxes[0].y) < 1 && box.height >= 44));
      for (const summary of await page.locator('.feature-summary').all()) assert.ok(await summary.isVisible());
      assert.equal(await page.locator('[data-feature-image]').evaluate(img => getComputedStyle(img).objectFit), 'contain');
    } finally { await page.close(); }
  }
});

test('showcase entrance and selection have visible finite movement and cancel on pagehide', async () => {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  try {
    await page.goto(base, { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => document.querySelector('.hero').getAnimations().length > 0);
    const entrance = await page.locator('.hero').evaluate(el => {
      const effect = el.getAnimations()[0].effect;
      return { duration: effect.getTiming().duration, frames: effect.getKeyframes() };
    });
    assert.ok(entrance.duration >= 450 && entrance.duration <= 650, `entrance duration ${entrance.duration}`);
    assert.match(entrance.frames[0].transform, /translateY\((1[6-9]|2[0-4])px\)/);
    await page.evaluate(() => Promise.all(document.getAnimations().map(a => a.finished.catch(() => {}))));
    const motion = await page.evaluate(async () => {
      document.querySelector('#tab-components').click();
      const panel = document.querySelector('[role=tabpanel]');
      const animation = panel.getAnimations()[0];
      const timing = animation.effect.getTiming();
      const frames = animation.effect.getKeyframes();
      await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      const first = getComputedStyle(panel).transform;
      await new Promise(resolve => setTimeout(resolve, 110));
      const intermediate = getComputedStyle(panel).transform;
      window.dispatchEvent(new Event('pagehide'));
      return { duration: timing.duration, iterations: timing.iterations, frames, first, intermediate, remaining: panel.getAnimations().length };
    });
    assert.ok(motion.duration >= 300 && motion.duration <= 420);
    assert.equal(motion.iterations, 1);
    assert.match(motion.frames[0].transform, /translateY\((1[6-9]|2[0-4])px\)/);
    assert.notEqual(motion.first, motion.intermediate);
    assert.notEqual(motion.intermediate, 'none');
    assert.equal(motion.remaining, 0);
  } finally { await page.close(); }
});

test('responsive navigation and preview controls have usable targets without crowding the first screen', async () => {
  for (const width of [1440, 768, 390]) for (const name of ['index', 'help', 'changes']) {
    const page = await browser.newPage({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
    try {
      await page.goto(base + '/' + name + '.html');
      const navigation = await page.locator('.site-header nav a, .site-header > .button').all();
      const boxes = [];
      for (const control of navigation) {
        const box = await control.boundingBox();
        assert.ok(box && box.height >= 44 && box.width >= 44, `${name} ${width}: navigation needs a 44px target, got ${JSON.stringify(box)}`);
        assert.ok(box.x >= 0 && box.x + box.width <= width, 'navigation stays within the viewport');
        for (const previous of boxes) {
          assert.ok(box.x >= previous.x + previous.width || previous.x >= box.x + box.width || box.y >= previous.y + previous.height || previous.y >= box.y + box.height, 'navigation targets do not overlap');
        }
        boxes.push(box);
      }
      assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
      if (width === 1440) assert.ok(await page.evaluate(() => document.documentElement.scrollHeight <= 2700));
      if (name === 'index') {
        for (const selector of ['select[aria-label="预览主题"]', '[data-enlarge]']) {
          const box = await page.locator(selector).boundingBox();
          assert.ok(box && box.height >= 44 && box.width >= 44, `${width}: preview controls need a 44px target, got ${JSON.stringify(box)}`);
        }
        if (width === 1440) {
          const shot = await page.locator('[data-feature-image]').boundingBox();
          assertPreviewExposure(shot, 900);
        }
      }
    } finally { await page.close(); }
  }
});

test('desktop and mobile expose real downloads without horizontal overflow', async () => {
  const release = JSON.parse(await fs.readFile(path.join(root, 'release.json'), 'utf8'));
  for (const width of [1440, 1280, 768, 390]) {
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const response = await page.goto(base);
    assert.equal(response.status(), 200, 'website must serve an actual page');
    await page.locator('[data-version]').filter({ hasText: `v${release.version}` }).waitFor();
    assert.equal(await page.locator('[data-version]').textContent(), `v${release.version}`);
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.ok(await page.locator('[data-download="setup"]').first().isVisible());
    assert.equal(await page.locator('[data-download="setup"]').first().getAttribute('href'), release.setup.url);
    assert.equal(await page.locator('[data-download="portable"]').getAttribute('href'), release.portable.url);
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
  assert.ok(await page.locator('a[href="https://github.com/LiuDaCheng666/orange-zomboid-launcher"]').count() >= 1);
  assert.equal(await page.getByRole('link', { name: '辽ICP备2026024866号-1' }).getAttribute('href'), 'https://beian.miit.gov.cn/');
  await page.getByRole('link', { name: '使用帮助', exact: true }).first().click();
  assert.match(new URL(page.url()).pathname, /^\/help(?:\.html)?$/);
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
  assert.match(await panel.locator('[data-feature-image]').getAttribute('src'), /launcher-mods-dark/);
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
    assert.match(new URL(page.url()).pathname, new RegExp(`^/${name}(?:\\.html)?$`));
    assert.ok(await page.getByRole('heading', { level: 1 }).isVisible());
    assert.equal(await page.locator('a[href*="/releases/download/"], a[href$=".exe"], a[href$=".zip"]').count(), 0);
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    assert.match(await page.locator('nav [aria-current="page"]').getAttribute('href'), new RegExp(`^/?${name}(?:\\.html)?$`));
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

test('preview requests bypass prior screenshot caches in every feature and theme', async () => {
  const page = await browser.newPage({ reducedMotion: 'reduce' });
  const requested = [];
  page.on('request', request => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/assets/launcher-')) requested.push(url);
  });
  try {
    await page.goto(base);
    await page.locator('[data-feature-image]').evaluate(image => image.decode());
    assert.equal(requested.length, 1, 'initial preview has a single image request');
    assert.equal(requested[0].pathname, '/assets/launcher-home-dark.png');
    assert.equal(requested[0].searchParams.get('v'), '20261024-c-glass', 'initial request must bypass the old unversioned cache');
    for (const theme of ['dark', 'light']) {
      await page.getByLabel('预览主题').selectOption(theme);
      for (const feature of ['home', 'components', 'mods', 'servers', 'maintenance']) {
        await page.locator(`#tab-${feature}`).click();
        await page.locator('[data-feature-image]').evaluate(image => image.decode());
        const selected = await page.locator('[data-feature-image]').getAttribute('src');
        const url = new URL(selected, base);
        assert.equal(url.pathname, `/assets/launcher-${feature}-${theme}.png`);
        assert.equal(url.searchParams.get('v'), '20261024-c-glass');
        await page.locator('[data-enlarge]').click();
        assert.equal(new URL(await page.locator('[data-dialog-image]').getAttribute('src'), base).href, url.href, 'enlargement uses the exact selected versioned image');
        await page.locator('[data-close]').click();
      }
    }
    assert.equal(new Set(requested.map(url => url.pathname)).size, 10, 'all ten screenshot URLs are actually requested');
    assert.ok(requested.every(url => url.searchParams.get('v') === '20261024-c-glass'), 'dynamic requests use the same cache version as the initial image');
  } finally { await page.close(); }
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

test('failed screenshot offers retry and successful recovery restores enlargement', async () => {
  const page = await browser.newPage({ reducedMotion: 'reduce' });
  try {
    let attempts = 0;
    await page.route(url => url.pathname === '/assets/launcher-components-dark.png', route => ++attempts === 1 ? route.abort() : route.continue());
    await page.goto(base);
    await page.locator('#tab-components').click();
    await page.waitForFunction(() => { const image = document.querySelector('[data-feature-image]'); return image.complete && image.naturalWidth === 0; });
    assert.match(await page.getByRole('tabpanel').innerText(), /预览图片加载失败/);
    assert.equal(await page.locator('[data-enlarge]').isDisabled(), true);
    await page.getByRole('button', { name: '重试图片', exact: true }).click();
    await page.waitForFunction(() => document.querySelector('[data-feature-image]').naturalWidth === 1440);
    assert.equal(attempts, 2);
    assert.equal(await page.getByText('预览图片加载失败', { exact: true }).isVisible(), false);
    assert.equal(await page.locator('[data-enlarge]').isEnabled(), true);
    await page.locator('[data-enlarge]').click();
    assert.match(await page.locator('[data-dialog-image]').getAttribute('src'), /launcher-components-dark/);
  } finally { await page.close(); }
});

test('superseded screenshot recovery cannot overwrite the latest selection or error state', async () => {
  const page = await browser.newPage({ reducedMotion: 'reduce' });
  let pending;
  try {
    let attempts = 0;
    await page.route(url => url.pathname === '/assets/launcher-components-dark.png', route => {
      if (++attempts === 1) return route.abort();
      pending = route;
    });
    await page.goto(base);
    await page.locator('#tab-components').click();
    await page.waitForFunction(() => { const image = document.querySelector('[data-feature-image]'); return image.complete && image.naturalWidth === 0; });
    assert.match(await page.getByRole('tabpanel').innerText(), /预览图片加载失败/);
    await page.getByRole('button', { name: '重试图片', exact: true }).click();
    for (let wait = 0; !pending && wait < 100; wait++) await page.waitForTimeout(10);
    assert.ok(pending, 'retry makes a new image request');
    await page.locator('#tab-mods').click();
    await page.locator('[data-feature-image]').evaluate(image => image.decode());
    await pending.fulfill({ contentType: 'image/png', body: await fs.readFile(path.join(root, 'assets/launcher-components-dark.png')) });
    pending = null;
    await page.waitForTimeout(100);
    assert.match(await page.locator('[data-feature-image]').getAttribute('src'), /launcher-mods-dark/);
    assert.equal(await page.locator('#tab-mods').getAttribute('aria-selected'), 'true');
    assert.equal(await page.getByText('预览图片加载失败', { exact: true }).isVisible(), false);
    assert.equal(await page.locator('[data-enlarge]').isEnabled(), true);
    // A late success must also leave a newer request's failure intact.
    await page.route(url => url.pathname === '/assets/launcher-components-light.png', route => { pending = route; });
    await page.route(url => url.pathname === '/assets/launcher-servers-light.png', route => route.abort());
    await page.getByLabel('预览主题').selectOption('light');
    await page.locator('#tab-components').click();
    for (let wait = 0; !pending && wait < 100; wait++) await page.waitForTimeout(10);
    assert.ok(pending, 'new theme makes an uncached image request');
    await page.locator('#tab-servers').click();
    await page.locator('[data-preview-error]').waitFor();
    await pending.fulfill({ contentType: 'image/png', body: await fs.readFile(path.join(root, 'assets/launcher-components-light.png')) });
    pending = null;
    await page.waitForTimeout(100);
    assert.match(await page.locator('[data-feature-image]').getAttribute('src'), /launcher-servers-light/);
    assert.equal(await page.getByText('预览图片加载失败', { exact: true }).isVisible(), true);
    assert.equal(await page.locator('[data-enlarge]').isDisabled(), true);
  } finally { await pending?.abort().catch(() => {}); await page.close(); }
});

test('failed metadata retains the approved current own-server downloads', async () => {
  const page = await browser.newPage();
  await page.route('**/release.json', route => route.abort());
  await page.goto(base); await page.locator('[data-release-status]').filter({ hasText: '暂不可用' }).waitFor();
  await assertApprovedStaticDownloads(page);
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
  await page.goto(base); await page.locator('[data-release-status]').filter({ hasText: '暂不可用' }).waitFor();
  assert.equal(await page.evaluate(() => window.injected), undefined);
  assert.equal(await page.locator('a[href^="https://evil.example"]').count(), 0);
  await assertApprovedStaticDownloads(page);
  await page.close();
});

test('Velopack metadata retains both exact versioned delivery links and rejects unknown formats', async () => {
  const version = '1.2.16';
  const data = { distribution: 'velopack', version, date: '2026-10-25', notes: ['Full package updates'] };
  for (const [kind, name] of Object.entries({ setup: 'OrangeZomboidLauncher-win-Setup.exe', portable: 'OrangeZomboidLauncher-win-Portable.zip' })) {
    data[kind] = { url: `/releases/v${version}/${name}`, github: `https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v${version}/${name}`, length: 100, sha256: 'A'.repeat(64) };
  }
  const page = await browser.newPage();
  try {
    await page.route('**/release.json', route => route.fulfill({ json: data }));
    await page.goto(base);
    await page.waitForFunction(() => document.querySelector('[data-version]').textContent === 'v1.2.16');
    for (const kind of ['setup', 'portable']) assert.equal(await page.locator(`[data-download="${kind}"]`).first().getAttribute('href'), data[kind].url);
    data.distribution = 'unknown';
    await page.reload();
    await page.waitForFunction(() => document.querySelector('[data-release-status]').textContent.includes('暂不可用'));
    await assertApprovedStaticDownloads(page);
  } finally { await page.close(); }
});

test('static downloads without JavaScript point to both current Velopack artifacts', async () => {
  const page = await browser.newPage({ javaScriptEnabled: false });
  try {
    await page.goto(base);
    await assertApprovedStaticDownloads(page);
    assert.equal(await page.locator('[data-version]').textContent(), 'v1.2.16');
  } finally { await page.close(); }
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
