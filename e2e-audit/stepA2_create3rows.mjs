import fs from 'fs';
import { open, login } from './lib.mjs';
const { browser, page, errors } = await open();
await login(page);
await page.goto('http://localhost:5173/home/entry'); await page.waitForTimeout(2500);
const pick = async (ph, rowIdx, text) => {
  const box = page.locator(`input[placeholder="${ph}"]`).nth(rowIdx);
  await box.click(); await box.fill(text); await page.waitForTimeout(800);
  await page.getByText(text, { exact: true }).last().click({ timeout: 5000 }); await page.waitForTimeout(600);
};
const products = [['18 BISCO L2PC 60PKT', '5', '100'], ['12 SMART FOOD ROUND 4PC SET 75PC', '4', '200']];
let total = 0;
for (let i = 0; i < products.length; i++) {
  if (i > 0) { await page.getByRole('button', { name: /Add Row/ }).click(); await page.waitForTimeout(500); }
  await pick('Search category...', i, 'STOCK BERRY');
  await pick('Search account...', i, products[i][0]);
  const nums = page.locator('tbody tr').nth(i).locator('input[type=number]');
  await nums.nth(1).fill(products[i][1]); await nums.nth(2).fill(products[i][2]); await page.waitForTimeout(500);
  const amt = (await page.getByLabel(`Entry ${i + 1} debit`).inputValue()).replace(/,/g, '');
  total += Number(amt);
  await page.getByLabel(`Entry ${i + 1} debit`).fill(''); await page.getByLabel(`Entry ${i + 1} credit`).fill(amt);
}
await page.getByRole('button', { name: /Add Row/ }).click(); await page.waitForTimeout(500);
await pick('Search category...', 2, 'HOUSE HOLD CUSTOMERS');
await pick('Search account...', 2, 'BURHAN TRADER');
await page.getByLabel('Entry 3 debit').fill(String(total)); await page.waitForTimeout(400);
const [resp] = await Promise.all([
  page.waitForResponse(r => r.url().includes('/api/HomeApi/entry') && r.request().method() === 'POST', { timeout: 20000 }),
  page.getByRole('button', { name: 'Submit Journal Entry' }).click()
]);
const body = await resp.json().catch(() => ({}));
console.log('create status', resp.status(), JSON.stringify(body).slice(0, 200), 'total', total);
fs.writeFileSync('state2.json', JSON.stringify({ voucherId: body.voucherId }));
console.log('errors', errors);
await browser.close();
