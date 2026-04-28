// Phase 6: Broadcasts deep dive - new broadcast flow, message types, scheduling, audience.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase6-broadcasts');
const NOTES = path.join(__dirname, 'notes');
fs.mkdirSync(SHOTS, { recursive: true });

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

  console.log('[1] Goto Broadcast');
  await page.goto('https://app.respond.io/space/408881/broadcast', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);
  // Dismiss coach
  const close1 = page.locator('button[aria-label="Close"]').first();
  if (await close1.count().catch(() => 0)) { try { await close1.click(); await human(page, 1500, 2500); } catch {} }
  await shot(page, 'bc-01-list');

  // Click Calendar view
  console.log('[2] Calendar view');
  const calBtn = page.locator('button:has-text("Calendar")').first();
  if (await calBtn.count()) {
    await calBtn.click();
    await human(page, 3500, 5000);
    await shot(page, 'bc-02-calendar');
  }
  // Back to Table
  const tblBtn = page.locator('button:has-text("Table")').first();
  if (await tblBtn.count()) {
    await tblBtn.click();
    await human(page, 2500, 3500);
  }

  // Click + Add Broadcast / New Broadcast
  console.log('[3] Create new broadcast');
  const newBtn = page.locator('button:has-text("Add Broadcast"), button:has-text("New Broadcast"), button:has-text("Create")').first();
  if (await newBtn.count()) {
    await newBtn.click();
    await human(page, 4000, 5500);
    await shot(page, 'bc-03-new-broadcast');
  }

  // If channel picker modal appears, capture then pick Telegram
  const chanModal = await page.evaluate(() => {
    const dlgs = document.querySelectorAll('[role="dialog"]');
    if (!dlgs.length) return null;
    return Array.from(dlgs[0].querySelectorAll('button, [role="button"], h1, h2, h3'))
      .map(el => (el.innerText || '').trim()).filter(t => t && t.length < 120);
  });
  if (chanModal) {
    fs.writeFileSync(path.join(NOTES, 'broadcast-channel-modal.json'), JSON.stringify(chanModal, null, 2));
    console.log('  modal content:', JSON.stringify(chanModal).slice(0, 200));
  }

  // Pick Telegram channel if options exist
  const tg = page.locator('div:has-text("Telegram")').filter({ hasText: /Telegram/i }).first();
  if (await tg.count().catch(() => 0)) {
    try {
      // Click the Telegram card
      await page.evaluate(() => {
        const cards = Array.from(document.querySelectorAll('button, [role="button"], [class*="card" i], [class*="option" i]'));
        for (const c of cards) {
          const t = (c.innerText || '').trim();
          if (/^Telegram/i.test(t) && t.length < 100) {
            c.click();
            return true;
          }
        }
      });
      await human(page, 3000, 4500);
      await shot(page, 'bc-04-channel-selected');
    } catch {}
  }

  // Continue / Next button
  const next1 = page.locator('button:has-text("Next"), button:has-text("Continue"), button:has-text("Create")').first();
  if (await next1.count()) {
    try { await next1.click(); await human(page, 4000, 5500); } catch {}
  }
  await shot(page, 'bc-05-configure');

  // Capture the broadcast configuration UI - audience, message, schedule
  const bcConfig = await page.evaluate(() => {
    return {
      title: document.querySelector('h1, h2')?.innerText?.trim() || '',
      inputs: Array.from(document.querySelectorAll('input, textarea, select'))
        .map(i => ({ type: i.tagName, name: i.getAttribute('name'), placeholder: i.getAttribute('placeholder'), label: i.getAttribute('aria-label') }))
        .filter(x => x.placeholder || x.label || x.name),
      buttons: Array.from(document.querySelectorAll('button'))
        .map(b => (b.innerText || '').trim())
        .filter(t => t && t.length < 40 && t.length > 2),
      tabs: Array.from(document.querySelectorAll('[role="tab"]'))
        .map(t => (t.innerText || '').trim()),
    };
  });
  fs.writeFileSync(path.join(NOTES, 'broadcast-config.json'), JSON.stringify(bcConfig, null, 2));

  // Scroll through the config page
  for (let i = 0; i < 4; i++) {
    await page.mouse.wheel(0, 500);
    await human(page, 2000, 3000);
    await shot(page, `bc-06-config-scroll-${i}`);
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
