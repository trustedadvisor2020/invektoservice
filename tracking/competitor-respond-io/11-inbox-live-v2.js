// Phase 9.5 v2: Properly click All filter, open the conversation, explore everything.
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
  await human(page, 4500, 6500);

  // Click "All" filter row specifically in the left inbox nav
  // The row contains text "All" and count "1". It's at x < 260.
  console.log('[2] Click All filter row (left inbox nav)');
  const allClicked = await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('div, button, a, [role="button"]'));
    for (const el of rows) {
      const rect = el.getBoundingClientRect();
      if (rect.left < 50 || rect.left > 260) continue;
      if (rect.width < 180 || rect.width > 260) continue;
      if (rect.height < 30 || rect.height > 60) continue;
      const txt = (el.innerText || '').trim();
      // "All\n1" or just "All"
      if (/^All\s*\d*$/.test(txt)) {
        el.click();
        return { text: txt };
      }
    }
    return null;
  });
  console.log('  all clicked:', JSON.stringify(allClicked));
  await human(page, 3500, 5000);
  await shot(page, 'v2-01-all-filter');

  // Now the conversation should be visible in center column. Click it.
  console.log('[3] Click first conversation in chat list (center column)');
  const convClicked = await page.evaluate(() => {
    // Chat list column is roughly 260 < x < 560
    const rows = Array.from(document.querySelectorAll('div, [role="button"], [role="listitem"]'));
    const candidates = [];
    for (const el of rows) {
      const rect = el.getBoundingClientRect();
      if (rect.left < 260 || rect.left > 560) continue;
      if (rect.width < 200 || rect.width > 320) continue;
      if (rect.height < 50 || rect.height > 140) continue;
      const txt = (el.innerText || '').trim();
      if (!txt || txt.length < 3) continue;
      if (/No conversations|Chats|Calls|Open|Newest|Unreplied/i.test(txt.split('\n')[0])) continue;
      candidates.push({ el, rect, text: txt.slice(0, 100) });
    }
    // Sort by top position - want the highest one after headers
    candidates.sort((a, b) => a.rect.top - b.rect.top);
    if (candidates.length) {
      candidates[0].el.click();
      return { clicked: true, text: candidates[0].text };
    }
    return { clicked: false };
  });
  console.log('  conv:', JSON.stringify(convClicked));
  await human(page, 4500, 6500);
  await shot(page, 'v2-02-conversation-open');

  // Scroll center pane to view all messages
  console.log('[4] Scroll the chat area');
  await page.mouse.move(820, 450);
  await page.mouse.wheel(0, -2000);
  await human(page, 2000, 3000);
  await shot(page, 'v2-03-chat-history');
  await page.mouse.wheel(0, 4000);
  await human(page, 2000, 3000);
  await shot(page, 'v2-04-chat-bottom');

  // Explore all buttons in viewport (just label + rect, capture tooltips via hover)
  console.log('[5] Enumerate all buttons with labels in viewport');
  const allBtns = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('button, [role="button"]'))
      .map(b => {
        const r = b.getBoundingClientRect();
        return {
          label: b.getAttribute('aria-label') || b.title || (b.innerText || '').trim().slice(0, 40),
          top: r.top, left: r.left, width: r.width, height: r.height,
        };
      })
      .filter(b => b.label && b.top > 0 && b.top < 900 && b.width > 8 && b.width < 120);
  });
  fs.writeFileSync(path.join(NOTES, 'inbox-buttons.json'), JSON.stringify(allBtns, null, 2));
  console.log('  buttons:', allBtns.length);

  // Hover interesting compose-area buttons (bottom of center pane)
  const composeBtns = allBtns.filter(b => b.top > 700 && b.left > 260 && b.left < 1000);
  console.log('  compose area buttons:', composeBtns.length);
  for (let i = 0; i < composeBtns.length; i++) {
    const b = composeBtns[i];
    try {
      await page.mouse.move(b.left + b.width/2, b.top + b.height/2, { steps: 6 });
      await human(page, 1400, 2300);
      const tooltip = await page.evaluate(() => {
        const t = document.querySelector('[role="tooltip"], [class*="tooltip" i][style*="opacity: 1"]');
        return t ? (t.innerText || '').trim().slice(0, 60) : null;
      });
      if (tooltip) console.log(`    [${i}] hover(${b.label || '?'}): ${tooltip}`);
      if (i < 10) await shot(page, `v2-05-compose-hover-${i}`);
    } catch {}
  }

  // Focus text area
  console.log('[6] Type in compose');
  const textarea = page.locator('textarea, [contenteditable="true"], [role="textbox"]').last();
  if (await textarea.count()) {
    try {
      await textarea.click();
      await human(page, 1500, 2500);
      await shot(page, 'v2-06-compose-focused');
      await textarea.type('Merhaba! Size nasil yardimci olabilirim?', { delay: 70 });
      await human(page, 2500, 3500);
      await shot(page, 'v2-07-compose-typed');
    } catch (e) { console.log('  ! textarea fail:', e.message); }
  }

  // AI Assist / AI Prompts menu — usually button near compose
  console.log('[7] Look for AI button / sparkle / wand');
  const aiFound = await page.evaluate(() => {
    const buttons = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of buttons) {
      const al = (b.getAttribute('aria-label') || '').toLowerCase();
      const txt = (b.innerText || '').toLowerCase();
      if (/ai|assist|sparkle|wand|prompt|magic/i.test(al + ' ' + txt)) {
        const r = b.getBoundingClientRect();
        if (r.width > 0 && r.top > 0 && r.top < 900) {
          b.click();
          return { label: al || txt.slice(0, 40), top: r.top, left: r.left };
        }
      }
    }
    return null;
  });
  console.log('  AI button found:', JSON.stringify(aiFound));
  await human(page, 3000, 4500);
  await shot(page, 'v2-08-ai-clicked');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1500, 2500);

  // Right panel — screenshot as-is and also try clicking sections
  console.log('[8] Right panel exploration');
  await shot(page, 'v2-09-right-panel');

  // Try clicking various right-panel headers (Details / Contact / Conversation / etc.)
  for (const label of ['Details', 'Contact', 'Conversation', 'Tags', 'Notes', 'About', 'Channels']) {
    const el = page.getByText(label, { exact: true }).first();
    if (await el.count().catch(() => 0)) {
      try {
        const r = await el.boundingBox();
        if (r && r.x > 900) {
          await el.click({ timeout: 2000 });
          await human(page, 2000, 3000);
        }
      } catch {}
    }
  }
  await shot(page, 'v2-10-right-panel-expanded');

  // Top bar - 3 dot menu
  console.log('[9] Top action bar of conversation');
  const topBarBtns = allBtns.filter(b => b.top < 120 && b.left > 600);
  for (let i = 0; i < Math.min(topBarBtns.length, 6); i++) {
    const b = topBarBtns[i];
    try {
      await page.mouse.move(b.left + b.width/2, b.top + b.height/2, { steps: 6 });
      await human(page, 1500, 2500);
    } catch {}
  }
  await shot(page, 'v2-11-topbar-hover');

  // Close any lingering menu
  await page.keyboard.press('Escape').catch(() => {});
  await shot(page, 'v2-99-final');

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
