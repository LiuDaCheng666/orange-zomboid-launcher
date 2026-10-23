// Reproducible, headless visual review; uses an already-running local server.
const { chromium } = require('../Orange3DTool/Web/playwright-runtime.cjs');
const fs = require('node:fs/promises');
const path = require('node:path');
const base = (process.env.OZL_SITE_URL || 'http://127.0.0.1:3837').replace(/\/$/, '');
const output = path.resolve(__dirname, '../artifacts/website-showcase');
const features = ['home', 'components', 'mods', 'servers', 'maintenance'];

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
    await fs.writeFile(path.join(output, 'review.json'), JSON.stringify({ dimensions, frames, recordingMilliseconds: Date.now() - start, video: path.join(output, 'showcase-motion.webm') }, null, 2));
    console.log(JSON.stringify({ output, dimensions, video: path.join(output, 'showcase-motion.webm') }, null, 2));
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
