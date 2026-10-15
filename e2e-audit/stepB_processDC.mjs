import { open, login, dcFilter, selectOption } from './lib.mjs';
const mode = process.argv[2] || 'Post All';
const { browser, page, errors } = await open();
await login(page);
await dcFilter(page);
await page.getByRole('button', { name: 'Process' }).first().click(); await page.waitForTimeout(1200);
await selectOption(page, page.locator('.modal input[placeholder="Select Location"]'), 'FACTORY SITE');
await page.locator('.modal input[type=text]').nth(2).fill('AUDIT STATION').catch(()=>{});
const [resp] = await Promise.all([
  page.waitForResponse(r => /\/api\/DeliveryChallanApi\/(single-entry|process-all)/i.test(r.url()) && r.request().method() === 'POST', { timeout: 20000 }),
  page.getByRole('button', { name: mode }).click()
]);
console.log(mode, '->', resp.status(), (await resp.text()).slice(0, 300));
await page.waitForTimeout(1500);
await page.screenshot({ path: `shot-dc-after-${mode.replace(/\s/g,'')}.png`, fullPage: true });
console.log('errors', errors);
await browser.close();
