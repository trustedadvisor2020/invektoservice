// Phase 9.5: Inbox deep-dive with real Telegram conversation.
// Opens the conversation, explores compose area, right panel, AI Assist, tags, assign, etc.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase9_5-inbox-live');
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
async function tryClose(page) {
  // Try common dismiss patterns for coach marks / tour modals
  const sels = [
    'button[aria-label="Close"]', 'button[aria-label="close" i]',
    'button:has-text("Close")', 'button:has-text("Skip")', 'button:has-text("Got it")',
    'button:has-text("Dismiss")', '[role="dialog"] button:has-text("×")',
  ];
  for (const s of sels) {
    const el = page.locator(s).first();
    if (await el.count() && await el.isVisible().catch(() => false)) {
      try { await el.click({ timeout: 2000 }); await human(page, 1200, 2000); return true; } catch {}
    }
  }
  await page.keyboard.press('Escape').catch(() => {});
  return false;
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
  page.setDefaultTimeout(20000);

  console.log('[1] Goto Inbox');
  await page.goto('https://app.respond.io/space/408881/inbox', { waitUntil: 'domcontentloaded' });
  await human(page, 4500, 6000);
  await tryClose(page);
  await human(page, 2000, 3000);
  await shot(page, '01-inbox-with-chat');

  // Switch to "All" filter to ensure conversation is visible
  console.log('[2] Click All filter');
  const allBtn = page.locator('text=All').first();
  if (await allBtn.count()) {
    try { await allBtn.click(); await human(page, 2500, 4000); } catch {}
  }
  await shot(page, '02-inbox-all');

  // Click the first conversation in the list
  console.log('[3] Click first conversation');
  const firstConv = page.locator('[class*="conversation" i], [role="listitem"], [data-testid*="conv" i]').first();
  // Better: generic approach - click anything in the chat list column that looks like a row
  await page.evaluate(() => {
    // Find the middle column (conversation list)
    const lists = Array.from(document.querySelectorAll('div'));
    // Heuristic: find an element containing message-like rows with user names
    for (const el of lists) {
      const rect = el.getBoundingClientRect();
      if (rect.left < 150 || rect.left > 400) continue;
      if (rect.width < 200 || rect.width > 500) continue;
      // Look for child rows
      const rows = el.querySelectorAll('div[class], [role="button"]');
      for (const r of rows) {
        const rt = r.getBoundingClientRect();
        if (rt.width < 200) continue;
        if (rt.height < 40 || rt.height > 120) continue;
        const txt = (r.innerText || '').trim();
        if (!txt || txt.length < 3) continue;
        r.click();
        return { clicked: true, text: txt.slice(0, 100) };
      }
    }
    return { clicked: false };
  }).then(r => console.log('  conv click:', JSON.stringify(r)));
  await human(page, 4000, 5500);
  await shot(page, '03-conversation-open');

  // Take multiple screenshots at different scroll positions
  console.log('[4] Scroll chat to top (history)');
  await page.mouse.move(700, 450);
  await page.mouse.wheel(0, -1500);
  await human(page, 2000, 3000);
  await shot(page, '04-chat-scrolled-top');

  // Scroll back to bottom
  await page.mouse.wheel(0, 3000);
  await human(page, 2000, 3000);
  await shot(page, '05-chat-back-bottom');

  // Explore compose area — click each button we can find
  console.log('[5] Explore compose area buttons');
  const composeBtns = await page.evaluate(() => {
    // Compose area is usually at the bottom of the center pane
    const h = window.innerHeight;
    const buttons = Array.from(document.querySelectorAll('button, [role="button"]'));
    return buttons
      .filter(b => {
        const r = b.getBoundingClientRect();
        return r.top > h - 250 && r.top < h - 20 && r.left > 200 && r.left < 1200 && r.width > 10 && r.width < 100;
      })
      .map(b => ({
        label: b.getAttribute('aria-label') || b.title || (b.innerText || '').trim().slice(0, 30),
        rect: b.getBoundingClientRect(),
      }))
      .filter(x => x.label);
  });
  fs.writeFileSync(path.join(NOTES, 'compose-buttons.json'), JSON.stringify(composeBtns, null, 2));
  console.log('  compose button count:', composeBtns.length);

  // Hover over each compose button to capture tooltips
  for (let i = 0; i < composeBtns.length; i++) {
    const b = composeBtns[i];
    try {
      await page.mouse.move(b.rect.x + b.rect.width/2, b.rect.y + b.rect.height/2, { steps: 8 });
      await human(page, 1500, 2500);
      if (i < 8) await shot(page, `06-compose-hover-${i}-${(b.label || 'x').replace(/[^a-z0-9]/gi, '-').slice(0, 20).toLowerCase()}`);
    } catch {}
  }

  // Click in the text area and see what happens
  console.log('[6] Focus message text area');
  const textAreas = await page.locator('textarea, [contenteditable="true"], [role="textbox"]').all();
  console.log('  text inputs found:', textAreas.length);
  if (textAreas.length) {
    try {
      await textAreas[textAreas.length - 1].click();
      await human(page, 2000, 3000);
      await shot(page, '07-compose-focused');
      // Type a sample (don't send)
      await page.keyboard.type('Merhaba, nasil yardimci olabilirim?', { delay: 80 });
      await human(page, 2500, 3500);
      await shot(page, '08-compose-typed');
    } catch (e) { console.log('  ! compose focus failed:', e.message); }
  }

  // Look for AI Assist / AI button specifically (usually a sparkle icon or "AI" label)
  console.log('[7] AI Assist button');
  const aiBtn = page.locator('button:has-text("AI"), [aria-label*="AI" i], button:has([class*="sparkle" i])').first();
  if (await aiBtn.count()) {
    try {
      await aiBtn.click();
      await human(page, 3500, 5000);
      await shot(page, '09-ai-assist-open');
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
    } catch {}
  }

  // Right panel — contact info
  console.log('[8] Right panel screenshot + exploration');
  // Click contact name in header to pop full contact panel
  await shot(page, '10-right-panel-default');

  // Try to expand various right-panel sections by clicking visible accordions
  const accordions = await page.locator('[aria-expanded], button:has-text("Details"), button:has-text("Notes"), button:has-text("Tags"), button:has-text("Fields")').all();
  for (let i = 0; i < Math.min(accordions.length, 6); i++) {
    try {
      await accordions[i].click({ timeout: 2000 });
      await human(page, 1800, 2800);
    } catch {}
  }
  await shot(page, '11-right-panel-expanded');

  // Hover top conversation bar to find 3-dot menu / status buttons
  console.log('[9] Top bar actions');
  const topBtns = await page.evaluate(() => {
    const buttons = Array.from(document.querySelectorAll('button, [role="button"]'));
    return buttons
      .filter(b => {
        const r = b.getBoundingClientRect();
        return r.top < 120 && r.top > 20 && r.left > 400 && r.width > 10 && r.width < 100;
      })
      .map(b => ({
        label: b.getAttribute('aria-label') || b.title || (b.innerText || '').trim().slice(0, 30),
      }))
      .filter(x => x.label && x.label.length < 40);
  });
  fs.writeFileSync(path.join(NOTES, 'topbar-buttons.json'), JSON.stringify(topBtns, null, 2));
  console.log('  topbar buttons:', topBtns.length);

  // Look for assign dropdown
  console.log('[10] Assign dropdown');
  const assignBtn = page.locator('button:has-text("Assign"), [aria-label*="assign" i]').first();
  if (await assignBtn.count()) {
    try {
      await assignBtn.click({ timeout: 3000 });
      await human(page, 3000, 4500);
      await shot(page, '12-assign-dropdown');
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
    } catch {}
  }

  // Try snippet button / close button / tag
  console.log('[11] Click "/" to trigger snippet autocomplete if supported');
  if (textAreas.length) {
    try {
      await textAreas[textAreas.length - 1].click();
      await human(page, 1500, 2500);
      // Clear prev input
      await page.keyboard.press('Control+A');
      await page.keyboard.press('Delete');
      await human(page, 1000, 1800);
      await page.keyboard.type('/', { delay: 150 });
      await human(page, 3000, 4500);
      await shot(page, '13-slash-autocomplete');
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
    } catch {}
  }

  // Final full page
  await shot(page, '99-final');

  console.log('DONE.');
  await human(page, 3000, 4500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
