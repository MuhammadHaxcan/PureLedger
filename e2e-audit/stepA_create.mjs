import fs from 'fs';
import { open, login } from './lib.mjs';
const { browser, page, errors } = await open();
await login(page);
await page.goto('http://localhost:5173/home/entry'); await page.waitForTimeout(2000);
const pick = async (ph, rowIdx, text) => {
  const box = page.locator(`input[placeholder="${ph}"]`).nth(rowIdx);
  await box.click(); await box.fill(text); await page.waitForTimeout(800);
  await page.getByText(text, { exact: true }).last().click({ timeout: 5000 }); await page.waitForTimeout(600);
};
// row 0 = product
await pick('Search category...', 0, 'STOCK BERRY');
await pick('Search account...', 0, '18 BISCO L2PC 60PKT');
let nums = page.locator('tbody tr').nth(0).locator('input[type=number]');
await nums.nth(1).fill('10'); await nums.nth(2).fill('230'); await page.waitForTimeout(400);
await page.getByLabel('Entry 1 debit').fill(''); await page.getByLabel('Entry 1 credit').fill('138000'); await page.waitForTimeout(300);
// row 1 = customer
await page.getByRole('button', { name: /Add Row/ }).click(); await page.waitForTimeout(500);
await pick('Search category...', 1, 'HOUSE HOLD CUSTOMERS');
await pick('Search account...', 1, 'BURHAN TRADER');
await page.getByLabel('Entry 2 debit').fill('138000'); await page.waitForTimeout(500);
await page.screenshot({ path: 'shot-create-filled.png' });
const [resp] = await Promise.all([
  page.waitForResponse(r => r.url().includes('/api/HomeApi/entry') && r.request().method() === 'POST', { timeout: 20000 }),
  page.getByRole('button', { name: 'Submit Journal Entry' }).click()
]);
const body = await resp.json().catch(() => ({}));
console.log('create status', resp.status(), JSON.stringify(body).slice(0, 300));
await page.waitForTimeout(1500);
await page.screenshot({ path: 'shot-create-done.png' });
fs.writeFileSync('state.json', JSON.stringify({ voucherId: body.voucherId }));
console.log('errors', errors);
await browser.close();
