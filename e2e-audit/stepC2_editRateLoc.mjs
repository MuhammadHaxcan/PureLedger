import { open, login, fetchVoucher, state } from './lib.mjs';
const { browser, page, errors } = await open();
await login(page);
await fetchVoucher(page, state().voucherId);
await page.locator('tbody tr').nth(1).getByTitle('Edit Entry').click(); await page.waitForTimeout(1500);
const nums = page.locator('.modal.show input[type=number]');   // qty, rate, disc, disc%, debit, credit
await nums.nth(1).fill('250');
await nums.nth(4).fill('0');
await nums.nth(5).fill(String(8 * 60 * 250));
await page.locator('select').filter({ has: page.locator('option', { hasText: 'ALI CARGO' }) }).selectOption({ label: 'ALI CARGO LHR' });
const [resp] = await Promise.all([
  page.waitForResponse(r => /journal-entry/i.test(r.url()) && r.request().method() === 'PUT', { timeout: 20000 }),
  page.getByRole('button', { name: 'Save Changes' }).click()
]);
console.log('PUT journal-entry ->', resp.status(), (await resp.text()).slice(0, 200));
console.log('errors', errors);
await browser.close();
