import { open, login, fetchVoucher, state } from './lib.mjs';
const rowIndex = Number(process.argv[2] || 0);
const { browser, page, errors } = await open();
await login(page);
await fetchVoucher(page, state().voucherId);
await page.locator('tbody tr').nth(rowIndex).getByTitle('Delete Entry').click(); await page.waitForTimeout(1000);
await page.screenshot({ path: 'shot-delete-confirm.png' });
console.log(JSON.stringify(await page.evaluate(() => [...document.querySelectorAll('.modal.show button, .swal2-container button')].filter(e=>e.offsetParent).map(e => (e.innerText||'').trim()))));
const [resp] = await Promise.all([
  page.waitForResponse(r => /journal-entry/i.test(r.url()) && r.request().method() === 'DELETE', { timeout: 20000 }),
  page.locator('.modal.show button, .swal2-container button').filter({ hasText: /^(Delete|Yes.*|Confirm|OK)/i }).first().click()
]);
console.log('DELETE journal-entry ->', resp.status(), (await resp.text()).slice(0, 200));
await page.waitForTimeout(1500);
await page.screenshot({ path: 'shot-after-delete.png', fullPage: true });
console.log('errors', errors);
await browser.close();
