import { open, login, state } from './lib.mjs';
const tag = process.argv[2] || 'x';
const { browser, ctx, page, errors } = await open();
await login(page);
for (const type of ['Invoice', 'Delivery Challan']) {
  await page.goto('http://localhost:5173/reporting/single-doc'); await page.waitForTimeout(3000);
  await page.locator('input[placeholder="Enter voucher ID"]').fill(String(state().voucherId));
  await page.locator('select').first().selectOption({ label: type });
  const [popup] = await Promise.all([ctx.waitForEvent('page', { timeout: 15000 }), page.getByRole('button', { name: /Open in New Tab/ }).click()]);
  await popup.waitForLoadState('networkidle').catch(()=>{}); await popup.waitForTimeout(3000);
  const text = (await popup.locator('body').innerText()).replace(/\s+\n/g, '\n');
  const file = `shot-doc-${tag}-${type.replace(/\s/g,'')}.png`;
  await popup.screenshot({ path: file, fullPage: true });
  console.log(`--- ${type} (${popup.url().replace(/.*\/reporting/, '/reporting')})`);
  console.log(text.split('\n').filter(l => l.trim()).slice(0, 40).join('\n'));
  await popup.close();
}
console.log('errors', errors);
await browser.close();
