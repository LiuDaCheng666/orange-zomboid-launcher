/* Real production DOM/events; only native backup inventory reads are synthetic.
 * These tests do not create, restore, delete, or inspect real save/backup files. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {setup, route, chromium} = require('./experience-ui-tests.cjs');

const evidence = path.resolve(__dirname, '../../artifacts/dialog-lifecycle-audit-1238');
const runTag = process.env.OZL_LIFECYCLE_RUN_TAG || new Date().toISOString().replace(/[:.]/g, '-');
const tests = [];
const test = (name, run) => tests.push({name, run});

async function capture(page, options) {
  await page.evaluate(() => Promise.all(document.getAnimations().filter(animation => Number.isFinite(animation.effect?.getComputedTiming().endTime)).map(animation => animation.finished.catch(() => {}))));
  await page.screenshot(options);
}

async function inventoryFixture(browser) {
  const fixture = await setup(browser);
  await fixture.page.evaluate(() => {
    const base = chrome.webview.postMessage;
    window.backupInventoryHeld = [];
    window.backupListHeld = [];
    fx.saves = [
      {id: 'Sandbox/Test', name: '第一个夹具存档', mode: 'Sandbox', editable: true},
      {id: 'Sandbox/Second', name: '第二个夹具存档', mode: 'Sandbox', editable: true},
    ];
    orange.state.saves = null;
    chrome.webview.postMessage = message => {
      const action = message.action === 'configurationRequest' ? message.payload.action : message.action;
      if (!['backupInventory', 'backupList'].includes(action)) return base(message);
      sent.push(message);
      (action === 'backupList' ? backupListHeld : backupInventoryHeld).push(message);
    };
    window.replyBackupInventory = (index, name, failure = '') => {
      const message = backupInventoryHeld[index];
      const data = {saves: [{id: 'Sandbox/Test', name, mode: 'Sandbox', count: 1, latestUtc: '2026-10-28T00:00:00Z'}]};
      emitNative(failure
        ? {id: message.id, ok: false, error: failure}
        : {id: message.id, ok: true, data: message.action === 'configurationRequest'
          ? {configurationId: message.payload.id, data} : data});
    };
    window.replyBackupList = (index, label, failure = '') => {
      const message = backupListHeld[index];
      const data = {archives: [{id: 'fixture-backup-' + index, label, createdUtc: '2026-10-28T00:00:00Z', bytes: 1024, fileCount: 1, recovery: false, automatic: false}]};
      emitNative(failure
        ? {id: message.id, ok: false, error: failure}
        : {id: message.id, ok: true, data: message.action === 'configurationRequest'
          ? {configurationId: message.payload.id, data} : data});
    };
  });
  await route(fixture.page, 'saves');
  await fixture.page.locator('[data-backup-inventory]').waitFor();
  return fixture;
}

async function openInventory(page, requestCount) {
  await page.locator('[data-backup-inventory]').click();
  await page.locator('#backup-inventory-dialog[open]').waitFor();
  await page.waitForFunction(count => backupInventoryHeld.length === count, requestCount);
}

async function nativeCloseInventory(page) {
  await page.locator('#backup-inventory-dialog').evaluate(dialog => dialog.close());
  await page.waitForFunction(() => !document.querySelector('#backup-inventory-dialog')?.open);
}

async function replyInventory(page, index, name, failure = '') {
  await page.evaluate(({index, name, failure}) => {
    replyBackupInventory(index, name, failure);
    return new Promise(resolve => requestAnimationFrame(() => resolve()));
  }, {index, name, failure});
}

async function openBackups(page, saveId, requestCount) {
  await page.locator(`[data-save-backups="${saveId}"]`).click();
  await page.locator('#save-backup-dialog[open]').waitFor();
  await page.waitForFunction(count => backupListHeld.length === count, requestCount);
}

async function nativeCloseBackups(page) {
  await page.locator('#save-backup-dialog').evaluate(dialog => dialog.close());
  await page.waitForFunction(() => !document.querySelector('#save-backup-dialog')?.open);
}

async function replyHistory(page, index, label, failure = '') {
  await page.evaluate(({index, label, failure}) => {
    replyBackupList(index, label, failure);
    return new Promise(resolve => requestAnimationFrame(() => resolve()));
  }, {index, label, failure});
}

// Break caught: a previous opening's late success replaces the reopened inventory.
test('closed inventory success cannot replace a newer reopened result', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openInventory(page, 1);
    await nativeCloseInventory(page);
    await openInventory(page, 2);
    await replyInventory(page, 1, '重新打开后的新备份记录');
    await page.getByText('重新打开后的新备份记录', {exact: true}).waitFor();
    await replyInventory(page, 0, '关闭前的旧备份记录');
    await capture(page, {path: path.join(evidence, `${runTag}-old-success.png`)});
    assert.match(await page.locator('#backup-inventory-dialog').innerText(), /重新打开后的新备份记录/,
      'late success from a closed opening must preserve the current inventory');
    assert.equal(await page.getByText('关闭前的旧备份记录', {exact: true}).count(), 0);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

// Break caught: an error from the closed opening erases the reopened inventory.
test('closed inventory error cannot replace a newer reopened result', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openInventory(page, 1);
    await nativeCloseInventory(page);
    await openInventory(page, 2);
    await replyInventory(page, 1, '仍应显示的新备份记录');
    await page.getByText('仍应显示的新备份记录', {exact: true}).waitFor();
    await replyInventory(page, 0, '', '关闭前的读取失败');
    await capture(page, {path: path.join(evidence, `${runTag}-old-error.png`)});
    assert.match(await page.locator('#backup-inventory-dialog').innerText(), /仍应显示的新备份记录/,
      'late error from a closed opening must preserve the current inventory');
    assert.equal(await page.getByText('关闭前的读取失败', {exact: true}).count(), 0);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

// Guard against hiding the owning request's real error or leaving retry disabled.
test('current inventory error stays visible and close/reopen can retry', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openInventory(page, 1);
    await replyInventory(page, 0, '', '本次备份记录暂时不可读');
    await page.getByText('本次备份记录暂时不可读', {exact: true}).waitFor();
    assert.equal(await page.locator('[data-backup-inventory-close]').isEnabled(), true);
    await page.locator('[data-backup-inventory-close]').click();
    await page.waitForFunction(() => !document.querySelector('#backup-inventory-dialog')?.open);
    assert.equal(await page.locator('[data-backup-inventory]').isEnabled(), true);
    await openInventory(page, 2);
    await replyInventory(page, 1, '重试读取的备份记录');
    await page.getByText('重试读取的备份记录', {exact: true}).waitFor();
    assert.equal(await page.getByText('本次备份记录暂时不可读', {exact: true}).count(), 0);
    await capture(page, {path: path.join(evidence, `${runTag}-retry.png`)});
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

// Break caught: epoch/save-id checks accept a previous opening of the same save.
test('closed same-save history success cannot replace a newer reopened result', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openBackups(page, 'Sandbox/Test', 1);
    await nativeCloseBackups(page);
    await openBackups(page, 'Sandbox/Test', 2);
    await replyHistory(page, 1, '同一存档重新打开的新备份');
    await page.getByText('同一存档重新打开的新备份', {exact: true}).waitFor();
    await replyHistory(page, 0, '同一存档关闭前的旧备份');
    await capture(page, {path: path.join(evidence, `${runTag}-history-old-success.png`)});
    assert.match(await page.locator('#save-backup-dialog').innerText(), /同一存档重新打开的新备份/,
      'late history success from an earlier same-save opening must preserve the current list');
    assert.equal(await page.getByText('同一存档关闭前的旧备份', {exact: true}).count(), 0);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

// Break caught: history's old catch writes an error into a different save's dialog.
test('closed history error cannot enter a different save reopened dialog', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openBackups(page, 'Sandbox/Test', 1);
    await nativeCloseBackups(page);
    await openBackups(page, 'Sandbox/Second', 2);
    await replyHistory(page, 1, '第二个存档的新备份');
    await page.getByText('第二个存档的新备份', {exact: true}).waitFor();
    await replyHistory(page, 0, '', '第一个存档关闭前的读取失败');
    await capture(page, {path: path.join(evidence, `${runTag}-history-old-error.png`)});
    assert.equal(await page.getByText('第一个存档关闭前的读取失败', {exact: true}).count(), 0,
      'a closed save history error must not be shown against the newly selected save');
    assert.match(await page.locator('#save-backup-dialog').innerText(), /第二个夹具存档/);
    assert.match(await page.locator('#save-backup-dialog').innerText(), /第二个存档的新备份/);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

// Guard against ignoring the current history error or breaking its retry path.
test('current history error stays visible and close/reopen can retry', async browser => {
  const {page, errors} = await inventoryFixture(browser);
  try {
    await openBackups(page, 'Sandbox/Test', 1);
    await replyHistory(page, 0, '', '本次存档备份暂时不可读');
    await page.getByText('本次存档备份暂时不可读', {exact: true}).waitFor();
    assert.equal(await page.locator('[data-backup-close]').isEnabled(), true);
    await page.locator('[data-backup-close]').click();
    await page.waitForFunction(() => !document.querySelector('#save-backup-dialog')?.open);
    await openBackups(page, 'Sandbox/Test', 2);
    await replyHistory(page, 1, '重试读取的存档备份');
    await page.getByText('重试读取的存档备份', {exact: true}).waitFor();
    assert.equal(await page.getByText('本次存档备份暂时不可读', {exact: true}).count(), 0);
    assert.equal(await page.locator('[data-backup-create]').isEnabled(), true);
    await capture(page, {path: path.join(evidence, `${runTag}-history-retry.png`)});
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});

(async () => {
  fs.mkdirSync(evidence, {recursive: true});
  const browser = await chromium.launch({channel: process.env.ORANGE_BROWSER_CHANNEL || 'msedge', headless: true});
  const results = [];
  try {
    for (const item of tests) {
      try { await item.run(browser); results.push({name: item.name, passed: true}); console.log('PASS ' + item.name); }
      catch (error) { results.push({name: item.name, passed: false, error: error.stack}); console.error('FAIL ' + item.name + '\n' + error.stack); }
    }
  } finally { await browser.close(); }
  fs.writeFileSync(path.join(evidence, `${runTag}-backup-results.json`), JSON.stringify({boundary: 'Production DOM and synthetic WebView reads; no native filesystem operations.', results}, null, 2));
  const passed = results.filter(item => item.passed).length;
  console.log(`${passed}/${results.length} backup dialog lifecycle groups passed`);
  if (passed !== results.length) process.exitCode = 1;
})().catch(error => { console.error(error); process.exitCode = 1; });
