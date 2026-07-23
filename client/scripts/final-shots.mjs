import { chromium } from '@playwright/test';
const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const BASE = 'http://localhost:5015';

// 1. landing
await page.goto(BASE + '/', { waitUntil: 'networkidle' });
await page.waitForTimeout(1400);
await page.screenshot({ path: '/tmp/v2-1-landing.png' });

// 2. login
await page.goto(BASE + '/login', { waitUntil: 'networkidle' });
await page.waitForTimeout(800);
await page.screenshot({ path: '/tmp/v2-2-login.png' });

// sign in via autofill
await page.getByTestId('login-autofill').click();
await page.getByTestId('login-submit').click();
await page.waitForTimeout(1500);

// 3. overview
await page.screenshot({ path: '/tmp/v2-3-overview.png' });

// 4. project
await page.getByTestId('project-card-Coffee Run').click();
await page.waitForTimeout(1400);
await page.screenshot({ path: '/tmp/v2-4-project.png' });

// 5. asset config (Bella has seeded draft)
await page.getByTestId('asset-row-Bella — Character Rig').click();
await page.waitForTimeout(1500);
await page.screenshot({ path: '/tmp/v2-5-config.png' });

// 6. timeline with compare
await page.getByTestId('tab-timeline').click();
await page.waitForTimeout(900);
const cmp = page.getByRole('button', { name: /Compare to HEAD/ }).first();
await cmp.scrollIntoViewIfNeeded();
await cmp.click();
await page.waitForTimeout(900);
await page.screenshot({ path: '/tmp/v2-6-timeline.png' });

// 7. renders
await page.getByTestId('tab-renders').click();
await page.waitForTimeout(900);
await page.locator('ul li').nth(1).click();
await page.waitForTimeout(700);
await page.screenshot({ path: '/tmp/v2-7-renders.png' });

await browser.close();
console.log('done');
