import { chromium } from 'playwright-core';
export async function open() {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const ctx = await browser.newContext({ viewport: { width: 1500, height: 950 } });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push('pageerror: ' + e.message));
  page.on('console', m => { if (m.type() === 'error') errors.push('console: ' + m.text().slice(0, 200)); });
  page.on('response', r => { if (r.status() >= 500) errors.push(`HTTP ${r.status()} ${r.url()}`); });
  return { browser, ctx, page, errors };
}
export async function login(page) {
  await page.goto('http://localhost:5173/login');
  const inputs = page.locator('input');
  await inputs.nth(0).fill('audit_admin');
  await page.locator('input[type=password]').fill('AuditPass#2026');
  await page.getByRole('button', { name: /log\s*in|sign\s*in/i }).first().click();
  await page.waitForURL(u => !u.pathname.includes('/login'), { timeout: 15000 });
}
export async function selectOption(page, input, text) {
  await input.click(); await input.fill(text); await page.waitForTimeout(800);
  await page.locator('.searchable-select-option').filter({ hasText: text }).first().click({ timeout: 8000 });
  await page.waitForTimeout(500);
}
export async function dcFilter(page, project = 'GHAZI TRADERS') {
  await page.goto('http://localhost:5173/delivery/process-dc'); await page.waitForTimeout(3500);
  await page.locator('select').first().selectOption({ label: 'Today' }).catch(()=>{});
  await selectOption(page, page.locator('input[placeholder="Select Project"]').first(), project);
  await page.getByRole('button', { name: /Generate Report/ }).click(); await page.waitForTimeout(3000);
}
import fs from 'fs';
export const state = () => JSON.parse(fs.readFileSync(new URL('./' + (process.env.STATE_FILE || 'state.json'), import.meta.url)));
export async function fetchVoucher(page, id) {
  await page.goto('http://localhost:5173/home/voucher-entry'); await page.waitForTimeout(3000);
  await page.locator('input[placeholder="Enter voucher ID to search"]').fill(String(id));
  await page.getByRole('button', { name: /Fetch Voucher/ }).click(); await page.waitForTimeout(2500);
}
