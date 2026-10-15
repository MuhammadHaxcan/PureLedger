import { open, login, fetchVoucher, state } from './lib.mjs';
const qty = process.argv[2] || '8';
const { browser, page, errors } = await open();
await login(page);
await fetchVoucher(page, state().voucherId);
await page.locator('tbody tr').nth(1).getByTitle('Edit Entry').click(); await page.waitForTimeout(1500);
const nums = page.locator('.modal input[type=number]');
await nums.nth(0).fill(qty);                       // quantity
await nums.nth(4).fill(String(Number(qty) * 60 * 230)); // credit (60 pkg x rate 230)
await page.waitForTimeout(300);
const [resp] = await Promise.all([
  page.waitForResponse(r => /journal-entry/i.test(r.url()) && r.request().method() === 'PUT', { timeout: 20000 }),
  page.getByRole('button', { name: 'Save Changes' }).click()
]);
console.log('PUT journal-entry ->', resp.status(), (await resp.text()).slice(0, 200));
await page.waitForTimeout(1500);
await page.screenshot({ path: 'shot-ve-afteredit.png', fullPage: true });
console.log('errors', errors);
await browser.close();
