// Reproducible, headless visual review; uses an already-running local server.
const { chromium } = require('../Orange3DTool/Web/playwright-runtime.cjs');
const fs = require('node:fs/promises');
const path = require('node:path');
const base = (process.env.OZL_SITE_URL || 'http://127.0.0.1:3837').replace(/\/$/, '');
const output = process.env.OZL_CAPTURE_OUTPUT ? path.resolve(process.env.OZL_CAPTURE_OUTPUT) : path.resolve(__dirname, '../artifacts/website-showcase');
const features = ['home', 'components', 'mods', 'servers', 'maintenance'];

async function decodeRecording(browser) {
  const http = require('node:http');
  const crypto = require('node:crypto');
  const assert = require('node:assert/strict');
  const bytes = await fs.readFile(path.join(output, 'showcase-motion.webm'));
  const server = http.createServer((req, res) => {
    const range = /^bytes=(\d+)-(\d*)$/.exec(req.headers.range || '');
    const start = range ? Number(range[1]) : 0;
    const end = range?.[2] ? Math.min(Number(range[2]), bytes.length - 1) : bytes.length - 1;
    const headers = { 'Content-Type': 'video/webm', 'Accept-Ranges': 'bytes', 'Content-Length': end - start + 1 };
    if (range) headers['Content-Range'] = `bytes ${start}-${end}/${bytes.length}`;
    res.writeHead(range ? 206 : 200, headers); res.end(bytes.subarray(start, end + 1));
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  try {
    await page.setContent('<style>body{margin:0}video{display:block;width:1440px;height:900px}</style><video muted preload="auto"></video>');
    await page.locator('video').evaluate((video, url) => { video.src = url; }, `http://127.0.0.1:${server.address().port}/motion.webm`);
    await page.waitForFunction(() => document.querySelector('video').readyState >= 2);
    const review = await page.locator('video').evaluate(video => ({ width: video.videoWidth, height: video.videoHeight, duration: video.duration }));
    assert.ok(review.duration >= 8 && review.duration <= 12, `actual video duration ${review.duration}`);
    review.frames = [];
    for (const time of [1.85, 1.98, 4.7, 6.9, 8.5]) {
      await page.locator('video').evaluate((video, time) => new Promise(resolve => { video.addEventListener('seeked', resolve, { once: true }); video.currentTime = time; }), time);
      const file = path.join(output, `decoded-${time}.png`);
      await page.locator('video').screenshot({ path: file });
      review.frames.push({ time, sha256: crypto.createHash('sha256').update(await fs.readFile(file)).digest('hex') });
    }
    assert.notEqual(review.frames[0].sha256, review.frames[1].sha256, 'decoded selection frames visibly differ');
    await fs.writeFile(path.join(output, 'video-review.json'), JSON.stringify(review, null, 2));
    console.log('Decoded actual recording', JSON.stringify(review));
  } finally { await page.close(); await new Promise(resolve => server.close(resolve)); }
}

(async () => {
  await fs.mkdir(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const dimensions = [];
  try {
    for (const width of [1440, 768, 390, 1280]) {
      const context = await browser.newContext({ viewport: { width, height: 900 }, reducedMotion: 'reduce' });
      try {
        const page = await context.newPage();
        for (const name of width === 1280 ? ['index'] : ['index', 'help', 'changes']) {
          await page.goto(`${base}/${name}.html`);
          await page.screenshot({ path: path.join(output, `${name}-${width}.png`), fullPage: true });
          dimensions.push(await page.evaluate(name => {
            const r = document.querySelector('[data-feature-image]')?.getBoundingClientRect();
            return { name, viewport: innerWidth, pageWidth: document.documentElement.scrollWidth, pageHeight: document.documentElement.scrollHeight, image: r?.toJSON() };
          }, name));
        }
        if (width === 1440) {
          await page.goto(base);
          await page.screenshot({ path: path.join(output, 'home-viewport.png') });
          for (const theme of ['dark', 'light']) {
            await page.getByLabel('预览主题').selectOption(theme);
            for (const feature of features) {
              await page.locator(`#tab-${feature}`).click();
              await page.locator('[data-feature-image]').evaluate(image => image.decode());
              await page.locator('#features').screenshot({ path: path.join(output, `${feature}-${theme}.png`) });
            }
          }
        }
      } finally { await context.close(); }
    }
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, recordVideo: { dir: output, size: { width: 1440, height: 900 } } });
    const page = await context.newPage();
    const video = page.video();
    const frames = [];
    const start = Date.now();
    const at = async time => page.waitForTimeout(Math.max(0, time - (Date.now() - start)));
    const frame = async name => {
      frames.push({ name, elapsed: Date.now() - start, effects: await page.evaluate(() => document.getAnimations().map(a => ({ target: a.effect.target.id || a.effect.target.className, time: a.currentTime, duration: a.effect.getTiming().duration, transform: getComputedStyle(a.effect.target).transform }))) });
      await page.screenshot({ path: path.join(output, `motion-${name}.png`) });
    };
    try {
      await page.goto(base, { waitUntil: 'domcontentloaded' });
      await at(180); await frame('entrance');
      await at(1000); await frame('settled');
      await at(1800);
      await page.locator('#features').evaluate(el => el.scrollIntoView({ behavior: 'instant', block: 'start' }));
      await page.locator('#tab-components').evaluate(el => el.click());
      await page.waitForTimeout(70); await frame('components-moving');
      await at(3200); await page.locator('#tab-mods').evaluate(el => el.click());
      await page.waitForTimeout(100); await frame('mods-moving');
      await at(4600); await page.getByLabel('预览主题').selectOption('light');
      await page.waitForTimeout(100); await frame('light-moving');
      await at(6000); await page.locator('[data-enlarge]').click();
      await page.waitForTimeout(300); await frame('enlarged');
      await at(7600); await page.locator('[data-close]').click();
      await at(8400); await page.locator('#tab-servers').evaluate(el => el.click());
      await page.waitForTimeout(100); await frame('servers-moving');
      await at(10000);
    } finally { await context.close(); }
    await video.saveAs(path.join(output, 'showcase-motion.webm'));
    await decodeRecording(browser);
    await fs.writeFile(path.join(output, 'review.json'), JSON.stringify({ dimensions, frames, recordingMilliseconds: Date.now() - start, video: path.join(output, 'showcase-motion.webm') }, null, 2));
    console.log(JSON.stringify({ output, dimensions, video: path.join(output, 'showcase-motion.webm') }, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
