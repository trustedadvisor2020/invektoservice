// Phase 3: Inbox deep dive with human-like pacing.
// Explores: coach mark dismissal, all filters (All/Mine/Unassigned/Incoming Calls/AI Agent),
// lifecycle filters, team inbox + custom inbox, compose area, sidebar icon tooltips.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase3-inbox');
const NOTES = path.join(__dirname, 'notes');
const INBOX_URL = 'https://app.respond.io/space/408881/inbox';

fs.mkdirSync(SHOTS, { recursive: true });

async function human(page, min = 2500, max = 4500) {
  const ms = Math.floor(min + Math.random() * (max - min));
  await page.waitForTimeout(ms);
}
async function shot(page, name) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true }).catch(() => {});
  console.log('  shot:', name);
}
async function mouseJiggle(page) {
  // Move mouse to random points to simulate human hesitation
  const w = 1440, h = 900;
  for (let i = 0; i < 3; i++) {
    const x = 200 + Math.random() * (w - 400);
    const y = 200 + Math.random() * (h - 400);
    await page.mouse.move(x, y, { steps: 10 });
    await page.waitForTimeout(200 + Math.random() * 400);
  }
}

(async () => {
  console.log('[launch]');
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
  page.setDefaultTimeout(20000);

  console.log('[1] Goto Inbox');
  await page.goto(INBOX_URL, { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5500);
  await mouseJiggle(page);
  await shot(page, '01-inbox-initial');

  // Try to dismiss any coach mark / tour modal
  console.log('[2] Dismiss coach mark(s)');
  const dismissSelectors = [
    'button:has-text("Skip")',
    'button:has-text("Close")',
    'button:has-text("Got it")',
    'button[aria-label="Close"]',
    'button[aria-label="close" i]',
    '[role="dialog"] button:has-text("×")',
    'svg[aria-label="close" i]',
  ];
  for (let round = 0; round < 4; round++) {
    let closed = false;
    for (const sel of dismissSelectors) {
      const el = page.locator(sel).first();
      if (await el.count() && await el.isVisible().catch(() => false)) {
        try {
          await el.click({ timeout: 2000 });
          console.log('  closed via:', sel);
          closed = true;
          await human(page, 1500, 2500);
          break;
        } catch {}
      }
    }
    if (!closed) {
      // Try ESC as last resort
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1200, 2000);
      break;
    }
  }
  await shot(page, '02-after-dismiss');

  // Inspect sidebar icon tooltips (hover one by one)
  console.log('[3] Hover sidebar icons to capture tooltips');
  const sidebarIcons = await page.locator('nav a, aside a').all().catch(() => []);
  console.log('  sidebar icon count:', sidebarIcons.length);
  const tooltips = [];
  for (let i = 0; i < Math.min(sidebarIcons.length, 10); i++) {
    try {
      await sidebarIcons[i].hover();
      await human(page, 1500, 2500);
      // Tooltip may appear — capture aria-labelledby or role=tooltip text
      const tipText = await page.evaluate(() => {
        const tip = document.querySelector('[role="tooltip"], [class*="tooltip" i]:not([style*="display: none"])');
        return tip ? (tip.innerText || tip.textContent || '').trim() : null;
      });
      const href = await sidebarIcons[i].getAttribute('href').catch(() => null);
      tooltips.push({ idx: i, href, tooltip: tipText });
      if (i < 4) await shot(page, `03-sidebar-hover-${i}`);
    } catch (e) {
      tooltips.push({ idx: i, error: e.message });
    }
  }
  fs.writeFileSync(path.join(NOTES, 'sidebar-tooltips.json'), JSON.stringify(tooltips, null, 2));

  // Inspect inbox left panel — click each filter in order
  console.log('[4] Click each inbox filter');
  const filters = ['All', 'Mine', 'Unassigned', 'Incoming Calls', 'Create AI Agent'];
  for (const f of filters) {
    const el = page.locator(`div:has-text("${f}"), button:has-text("${f}"), [role="button"]:has-text("${f}")`).first();
    if (await el.count().catch(() => 0)) {
      try {
        await el.click({ timeout: 3000 });
        await human(page, 2500, 4000);
        await shot(page, `04-filter-${f.toLowerCase().replace(/\s+/g, '-')}`);
        console.log('  clicked filter:', f);
        // Close any modal that popped
        await page.keyboard.press('Escape').catch(() => {});
        await human(page, 1200, 2000);
      } catch (e) {
        console.log('  ! filter click failed:', f, e.message);
      }
    }
  }

  // Back to Mine / All
  await page.locator('text=All').first().click().catch(() => {});
  await human(page, 2500, 4000);

  // Click lifecycle filters
  console.log('[5] Click lifecycle sidebar filters');
  const lifecycles = ['New Lead', 'Hot Lead', 'Payment', 'Customer'];
  for (const lc of lifecycles) {
    const el = page.locator(`text=${lc}`).first();
    if (await el.count().catch(() => 0)) {
      try {
        await el.click({ timeout: 3000 });
        await human(page, 2500, 4000);
        await shot(page, `05-lifecycle-${lc.toLowerCase().replace(/\s+/g, '-')}`);
        console.log('  lifecycle:', lc);
      } catch (e) {
        console.log('  ! lc click failed:', lc);
      }
    }
  }

  // Open "Connect a Channel" modal from inbox empty state
  console.log('[6] Connect a Channel modal');
  const ccBtn = page.locator('button:has-text("Connect a Channel"), button:has-text("Connect Channel")').first();
  if (await ccBtn.count().catch(() => 0)) {
    try {
      await ccBtn.click({ timeout: 3000 });
      await human(page, 3500, 5000);
      await shot(page, '06-connect-channel-modal');
      // Extract channel catalog
      const channels = await page.evaluate(() => {
        const out = [];
        document.querySelectorAll('[role="dialog"] button, [role="dialog"] [role="button"], [role="dialog"] a').forEach(el => {
          const txt = (el.innerText || '').trim();
          if (txt && txt.length < 60) out.push(txt);
        });
        return out;
      });
      fs.writeFileSync(path.join(NOTES, 'channel-catalog.json'), JSON.stringify(channels, null, 2));
      console.log('  channel-like entries captured:', channels.length);
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1800, 2800);
    } catch (e) {
      console.log('  ! modal open failed:', e.message);
    }
  }

  // Search box in inbox
  console.log('[7] Click search box');
  const searchIcons = page.locator('svg').all ? await page.locator('[aria-label*="search" i]').all() : [];
  console.log('  search-labeled elements:', searchIcons.length);
  for (let i = 0; i < Math.min(searchIcons.length, 2); i++) {
    try {
      await searchIcons[i].click({ timeout: 2000 });
      await human(page, 2000, 3000);
      await shot(page, `07-search-${i}`);
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1200, 2000);
    } catch {}
  }

  // Expand team inbox / custom inbox sections (+ buttons)
  console.log('[8] Team Inbox / Custom Inbox + buttons');
  const plusButtons = await page.locator('button[aria-label*="add" i], button:has-text("+")').all().catch(() => []);
  for (let i = 0; i < Math.min(plusButtons.length, 2); i++) {
    try {
      await plusButtons[i].click({ timeout: 2000 });
      await human(page, 2500, 3800);
      await shot(page, `08-plus-btn-${i}`);
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1200, 2000);
    } catch {}
  }

  // Click Calls tab
  console.log('[9] Calls tab');
  const callsTab = page.locator('div:has-text("Calls"), button:has-text("Calls")').first();
  if (await callsTab.count().catch(() => 0)) {
    try {
      await callsTab.click({ timeout: 3000 });
      await human(page, 2500, 4000);
      await shot(page, '09-calls-tab');
    } catch (e) {
      console.log('  ! calls tab failed:', e.message);
    }
  }

  // Final full-page shot
  await page.goto(INBOX_URL, { waitUntil: 'domcontentloaded' }).catch(() => {});
  await human(page, 3000, 4500);
  await shot(page, '10-inbox-final');

  console.log('DONE. Phase 3 complete.');
  await human(page, 2000, 3500);
  await ctx.close();
})().catch(err => {
  console.error('ERROR:', err.message);
  process.exit(1);
});
