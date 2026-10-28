/* Production DOM and dialog lifecycle; WebView replies use the experience fixture. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {setup, route, chromium} = require('./experience-ui-tests.cjs');
const output = path.resolve(__dirname, '../../artifacts/configuration-editor-lifecycle-20261029');
fs.mkdirSync(output, {recursive:true});
const cases = [];
const test = (name, run) => cases.push({name, run});
const current = page => page.evaluate(() => {
  const editor = orange.launchConfigurationUi.editor();
  return editor ? {id:editor.id, open:editor.element.open, connected:editor.element.isConnected} : null;
});
const ready = page => page.waitForFunction(() => {
  const editor = orange.launchConfigurationUi.editor();
  return editor?.element.open && editor.element.isConnected && !!editor.view && !editor.reading;
});
const nativeClose = page => page.locator('#configuration-editor').evaluate(element => new Promise(resolve => {
  element.addEventListener('close', () => queueMicrotask(resolve), {once:true});
  element.close();
}));
const settled = page => page.evaluate(() => Promise.all(document.getAnimations()
  .filter(animation => Number.isFinite(animation.effect?.getComputedTiming().endTime))
  .map(animation => animation.finished.catch(() => {}))));
const open = async page => {
  await page.locator('[data-configuration-components-open]').first().click();
  await ready(page);
};
const revisit = async page => {
  await page.locator('nav [data-page="saves"]').click();
  await page.locator('nav [data-page="components"]').click();
};

test('native close releases the editor and both component entry buttons reopen it', async page => {
  await open(page);
  // Exercise HTMLDialogElement's actual close event, rather than the custom Close handler.
  await nativeClose(page);
  await page.waitForFunction(() => !document.querySelector('#configuration-editor[open]'));
  assert.equal(await current(page), null, 'a closed dialog must not remain the active editor');
  assert.equal(await page.locator('#configuration-editor').count(), 0);
  await revisit(page);
  for (let i=0; i<2; i++) {
    await page.locator('[data-configuration-components-open]').nth(i).click();
    await ready(page);
    await page.locator('[data-configuration-close]').click();
    assert.equal(await current(page), null);
  }
  await settled(page);
  await page.screenshot({path:path.join(output, 'components-after-reopen.png')});
});

test('a queued old close cannot clear the replacement editor', async page => {
  await open(page);
  await page.evaluate(() => {
    const previous = orange.launchConfigurationUi.editor();
    window.previousEditor = previous;
    previous.element.close();
    void orange.launchConfigurationUi.openEditor('launcher', 'components');
  });
  await ready(page);
  assert.equal(await page.evaluate(() => orange.launchConfigurationUi.editor() !== previousEditor), true,
    'reopen must recover even before the previous close event is delivered');
  await page.waitForFunction(() => !previousEditor.element.isConnected);
  assert.equal(await page.locator('#configuration-editor[open]').count(), 1);
  await page.locator('[data-configuration-tab="mods"]').click();
  assert.equal(await page.locator('[data-configuration-search]').isVisible(), true);
});

test('closed pending reads are cancelled and late responses cannot change a new editor', async page => {
  await page.evaluate(() => fx.hold='configurationRead');
  await page.locator('[data-configuration-components-open]').first().click();
  await page.waitForFunction(() => held.some(message => message.action==='configurationRead'));
  const readId = await page.evaluate(() => held.find(message => message.action==='configurationRead').payload.readId);
  await nativeClose(page);
  await page.waitForFunction(() => !document.querySelector('#configuration-editor[open]'));
  assert.equal(await current(page), null);
  await page.waitForFunction(token => sent.some(message => message.action==='configurationReadCancel' && message.payload.readId===token), readId);
  await page.evaluate(() => fx.hold='');
  await revisit(page);
  await open(page);
  await page.locator('[data-configuration-tab="mods"]').click();
  await page.locator('[data-configuration-search]').fill('保留新窗口输入');
  await page.evaluate(() => held.splice(0).forEach(replyNative));
  await page.waitForFunction(() => !orange.launchConfigurationUi.editor().readRequests.size);
  assert.equal(await page.locator('[data-configuration-search]').inputValue(), '保留新窗口输入');
  assert.equal(await page.locator('#configuration-editor[open]').count(), 1);
});

test('a detached editor does not silently block the next entry', async page => {
  await open(page);
  await page.locator('#configuration-editor').evaluate(element => element.remove());
  await revisit(page);
  await open(page);
  assert.deepEqual(await current(page), {id:'launcher', open:true, connected:true});
});

test('failed dialog presentation clears ownership and the next click retries', async page => {
  await page.evaluate(() => {
    const original = HTMLDialogElement.prototype.showModal;
    HTMLDialogElement.prototype.showModal = function() {
      HTMLDialogElement.prototype.showModal = original;
      throw new DOMException('测试：窗口未能打开', 'InvalidStateError');
    };
  });
  await page.locator('[data-configuration-components-open]').first().click();
  await page.waitForFunction(() => document.querySelector('#notice')?.textContent.includes('测试：窗口未能打开'));
  assert.equal(await current(page), null);
  assert.equal(await page.locator('#configuration-editor').count(), 0);
  await open(page);
});

test('a delayed discard confirmation cannot close a different editor', async page => {
  await open(page);
  await page.locator('[data-configuration-tab="mods"]').click();
  await page.locator('[data-configuration-available] button').first().click();
  await page.locator('[data-configuration-close]').click();
  await page.locator('#confirm[open]').waitFor();
  await nativeClose(page);
  await page.evaluate(() => orange.launchConfigurationUi.openEditor('live:'+'b'.repeat(32), 'mods'));
  await ready(page);
  // Resolve the old confirmation after the replacement dialog owns the editor.
  await page.locator('#confirm-yes').evaluate(button => button.click());
  await page.waitForFunction(() => !document.querySelector('#confirm').open);
  assert.deepEqual(await current(page), {id:'live:'+'b'.repeat(32), open:true, connected:true});
  await page.locator('[data-configuration-search]').fill('新配置仍可编辑');
  assert.equal(await page.locator('[data-configuration-search]').inputValue(), '新配置仍可编辑');
});

test('Escape keeps a dirty draft until one explicit discard and ordinary Close reopens', async page => {
  await open(page);
  await page.locator('[data-configuration-tab="mods"]').click();
  await page.locator('[data-configuration-available] button').first().click();
  const ids = await page.evaluate(() => [...orange.launchConfigurationUi.editor().ids]);
  await page.keyboard.press('Escape');
  await page.locator('#confirm-no').click();
  assert.deepEqual(await page.evaluate(() => orange.launchConfigurationUi.editor().ids), ids);
  await page.locator('[data-configuration-close]').click();
  await page.locator('#confirm-yes').click();
  assert.equal(await current(page), null);
  await revisit(page);
  await open(page);
  await settled(page);
  await page.screenshot({path:path.join(output, 'editor-open-after-discard.png')});
});

(async () => {
  const browser = await chromium.launch({channel:'msedge', headless:true});
  let failed=0;
  const results=[];
  try {
    for (const item of cases.filter(item => !process.env.OZL_TEST_FILTER || item.name.includes(process.env.OZL_TEST_FILTER))) {
      const {page, errors} = await setup(browser);
      await page.evaluate(() => addEventListener('unhandledrejection', event => {window.lifecycleRejections ||= [];lifecycleRejections.push(String(event.reason));}));
      try {
        await route(page, 'components');
        await item.run(page);
        assert.deepEqual(errors, []);
        assert.deepEqual(await page.evaluate(() => window.lifecycleRejections || []), []);
        results.push({name:item.name, status:'PASS'});
        console.log('PASS '+item.name);
      } catch (error) {
        failed++;
        results.push({name:item.name, status:'FAIL', error:error.stack, editor:await current(page)});
        console.error('FAIL '+item.name+'\n'+error.stack);
      } finally {await page.close();}
    }
  } finally {await browser.close();}
  fs.writeFileSync(path.join(output, 'ui-results.json'), JSON.stringify(results, null, 2));
  console.log(`${results.length-failed}/${results.length} editor lifecycle groups passed`);
  if(failed)process.exitCode=1;
})().catch(error => {console.error(error);process.exitCode=1;});
