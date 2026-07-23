import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const BASE = 'http://localhost:5015';

await page.goto(BASE + '/');
await page.getByTestId('project-card-Ledgerline').click();
await page.waitForTimeout(1200);
await page.getByTestId('hook-row-Stripe Payments').click();
await page.waitForTimeout(1200);
await page.getByTestId('input-url').fill('https://api.ledgerline.dev/hooks/stripe-v3');
await page.waitForTimeout(1800); // debounce + save + refresh
await page.locator('[data-testid="draft-diff"]').scrollIntoViewIfNeeded();
await page.waitForTimeout(400);
await page.screenshot({ path: '/tmp/rejudge-4-draft-diff.png' });

await page.getByTestId('tab-deliveries').click();
await page.waitForTimeout(1200);
await page.screenshot({ path: '/tmp/rejudge-6-deliveries.png' });
await browser.close();
console.log('done');
