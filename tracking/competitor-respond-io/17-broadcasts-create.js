// Phase 6b: Complete broadcast creation flow to reach config screen.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase6-broadcasts');
const NOTES = path.join(__dirname, 'notes');

async function human(page, min = 2500, max = 4500) {
  await page.waitForTimeout(Math.floor(min + Math.random() * (max - min)));
}
async function shot(page, name) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true }).catch(() => {});
  console.log('  shot:', name);
}

(async () => {
  const ctx = await chromium.launchPersistentContext(PROFILE_DIR, {
    channel: 'chrome',
    headless: false,
    viewport: { width: 1440, height: 900 },
    args: ['--disable-blink-features=AutomationControlled'],
    ignoreDefaultArgs: ['--enable-automation'],
  });
  await ctx.addInitScript(() => {
    Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
  });
  const page = ctx.pages()[0] || await ctx.newPage();
  page.setDefaultTimeout(25000);

  await page.goto('https://app.respond.io/space/408881/broadcast', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);

  console.log('[1] Click Add Broadcast');
  const addBtn = page.locator('button:has-text("Add Broadcast")').first();
  if (await addBtn.count()) {
    await addBtn.click();
    await human(page, 3000, 4500);
  }

  console.log('[2] Fill broadcast name');
  const nameI = page.locator('input[placeholder*="broadcast" i], input[placeholder*="name" i]').first();
  if (await nameI.count()) {
    await nameI.click();
    await human(page, 1200, 2000);
    await nameI.type('Explore Test Broadcast', { delay: 70 });
    await human(page, 1800, 2500);
    await shot(page, 'bc2-01-name-filled');
  }

  // Click Create
  const createBtn = page.locator('button:has-text("Create")').first();
  if (await createBtn.count()) {
    await createBtn.click();
    await human(page, 5000, 7000);
    await shot(page, 'bc2-02-after-create');
  }

  console.log('  url:', page.url());

  // Dismiss any coach marks
  const closeX = page.locator('button[aria-label="Close"]').first();
  if (await closeX.count().catch(() => 0)) { try { await closeX.click(); await human(page, 1500, 2500); } catch {} }

  // Capture the configuration UI
  console.log('[3] Capture broadcast config page');
  const cfg = await page.evaluate(() => {
    return {
      title: document.querySelector('h1, h2')?.innerText?.trim() || '',
      visibleSections: Array.from(document.querySelectorAll('h2, h3, h4, [class*="section"] > div:first-child'))
        .map(el => (el.innerText || '').trim()).filter(t => t && t.length < 80),
      labels: Array.from(document.querySelectorAll('label'))
        .map(el => (el.innerText || '').trim()).filter(t => t && t.length < 80),
      selects: Array.from(document.querySelectorAll('select, [role="combobox"]'))
        .map(el => (el.innerText || el.getAttribute('aria-label') || '').trim()).filter(t => t && t.length < 80),
      buttons: Array.from(document.querySelectorAll('button'))
        .map(el => (el.innerText || '').trim()).filter(t => t && t.length < 40 && t.length > 2),
    };
  });
  fs.writeFileSync(path.join(NOTES, 'broadcast-config.json'), JSON.stringify(cfg, null, 2));

  // Scroll through the page to see all fields
  for (let i = 0; i < 5; i++) {
    await shot(page, `bc2-03-scroll-${i}`);
    await page.mouse.move(720, 450);
    await page.mouse.wheel(0, 500);
    await human(page, 2500, 3500);
  }

  // Click channel dropdown if present
  console.log('[4] Try channel dropdown');
  const chanDd = page.locator('[role="combobox"], select, button:has-text("Select channel"), button:has-text("Channel")').first();
  if (await chanDd.count()) {
    try {
      await chanDd.click();
      await human(page, 3000, 4500);
      await shot(page, 'bc2-04-channel-dropdown');
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
    } catch {}
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
