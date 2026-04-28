// Phase 9.6: AI Assist live test in the open conversation.
// - Focus the Reply editor (NOT the Comment editor)
// - Try slash commands: /, $, ::
// - Click the AI Assist button and capture its output
// - Click each compose-area icon (emoji, attach, image, etc.) and screenshot the menus

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase9_6-ai-assist');
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

async function openConversation(page) {
  await page.goto('https://app.respond.io/space/408881/inbox', { waitUntil: 'domcontentloaded' });
  await human(page, 4500, 6500);

  // Click All filter
  await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('div, button, a, [role="button"]'));
    for (const el of rows) {
      const rect = el.getBoundingClientRect();
      if (rect.left < 50 || rect.left > 260) continue;
      if (rect.width < 180 || rect.width > 260) continue;
      if (rect.height < 30 || rect.height > 60) continue;
      const txt = (el.innerText || '').trim();
      if (/^All\s*\d*$/.test(txt)) { el.click(); return true; }
    }
  });
  await human(page, 3000, 4500);

  // Click first conversation
  await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('div, [role="button"], [role="listitem"]'));
    const cands = [];
    for (const el of rows) {
      const r = el.getBoundingClientRect();
      if (r.left < 260 || r.left > 560) continue;
      if (r.width < 200 || r.width > 320) continue;
      if (r.height < 50 || r.height > 140) continue;
      const txt = (el.innerText || '').trim();
      if (!txt) continue;
      if (/No conversations|Chats|Calls|Open|Newest|Unreplied/i.test(txt.split('\n')[0])) continue;
      cands.push({ el, r });
    }
    cands.sort((a, b) => a.r.top - b.r.top);
    if (cands.length) cands[0].el.click();
  });
  await human(page, 4500, 6000);

  // Dismiss any popup modal (Create an AI Agent)
  const dismissBtn = page.locator('[role="dialog"] button[aria-label="Close"], [class*="modal"] button[aria-label*="close" i]').first();
  if (await dismissBtn.count().catch(() => 0)) {
    await dismissBtn.click().catch(() => {});
  }
  // Also click "×" within the pop-up if present
  await page.evaluate(() => {
    const closes = Array.from(document.querySelectorAll('button'));
    for (const b of closes) {
      const r = b.getBoundingClientRect();
      if (r.top > 300 && r.left > 1200 && r.width < 40 && r.height < 40) {
        const html = b.outerHTML || '';
        if (/close|×|x/i.test(html) || b.children.length === 1) {
          b.click();
          return true;
        }
      }
    }
  });
  await human(page, 2000, 3000);
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

  console.log('[1] Open conversation');
  await openConversation(page);
  await shot(page, '01-conv-open');

  // Identify compose-area editors by position (bottom of center column)
  console.log('[2] Find Reply editor (visible contenteditable at bottom)');
  const editor = await page.evaluate(() => {
    const eds = Array.from(document.querySelectorAll('[contenteditable="true"]'));
    for (const e of eds) {
      const r = e.getBoundingClientRect();
      if (r.top > 650 && r.top < 900 && r.width > 300) {
        // Is visible?
        const s = window.getComputedStyle(e);
        if (s.display === 'none' || s.visibility === 'hidden') continue;
        e.scrollIntoView({ block: 'center' });
        return {
          placeholder: e.getAttribute('data-placeholder') || e.getAttribute('placeholder') || '',
          classList: e.className.slice(0, 100),
          rect: { top: r.top, left: r.left, width: r.width, height: r.height },
        };
      }
    }
    return null;
  });
  console.log('  editor:', JSON.stringify(editor));

  if (!editor) {
    console.log('  ! no reply editor found');
    await shot(page, '02-no-editor');
    await ctx.close();
    process.exit(1);
  }

  // Click editor center
  const ex = editor.rect.left + editor.rect.width/2;
  const ey = editor.rect.top + editor.rect.height/2;
  await page.mouse.click(ex, ey);
  await human(page, 1500, 2500);
  await shot(page, '02-reply-focused');

  // Try slash command
  console.log('[3] Type / for snippets autocomplete');
  await page.keyboard.type('/', { delay: 150 });
  await human(page, 2500, 3500);
  await shot(page, '03-slash');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1500, 2500);

  // Clear & try $ for variable
  console.log('[4] Type $ for variable');
  await page.mouse.click(ex, ey);
  await human(page, 1000, 1800);
  await page.keyboard.press('Control+A');
  await page.keyboard.press('Delete');
  await human(page, 800, 1500);
  await page.keyboard.type('$', { delay: 150 });
  await human(page, 2500, 3500);
  await shot(page, '04-dollar');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1500, 2500);

  // Try :: for emoji
  console.log('[5] Type :: for emoji');
  await page.mouse.click(ex, ey);
  await human(page, 1000, 1800);
  await page.keyboard.press('Control+A');
  await page.keyboard.press('Delete');
  await human(page, 800, 1500);
  await page.keyboard.type('::smi', { delay: 150 });
  await human(page, 2500, 3500);
  await shot(page, '05-emoji-shortcode');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1500, 2500);

  // Type a real reply for AI to work on
  console.log('[6] Type a customer-style reply for AI operations');
  await page.mouse.click(ex, ey);
  await human(page, 1000, 1800);
  await page.keyboard.press('Control+A');
  await page.keyboard.press('Delete');
  await human(page, 800, 1500);
  await page.keyboard.type('Hi, the price is 100 usd per month. Do you want to try for free?', { delay: 60 });
  await human(page, 2500, 3500);
  await shot(page, '06-reply-typed');

  // Enumerate compose-area icons (bottom row)
  console.log('[7] Enumerate compose icons');
  const icons = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('button, [role="button"]'))
      .filter(b => {
        const r = b.getBoundingClientRect();
        return r.top > 700 && r.top < 900 && r.left > 260 && r.left < 1100 && r.width > 12 && r.width < 50;
      })
      .map((b, idx) => ({
        idx,
        label: b.getAttribute('aria-label') || b.title || (b.innerText || '').trim().slice(0, 40),
        rect: b.getBoundingClientRect(),
      }));
  });
  fs.writeFileSync(path.join(NOTES, 'compose-icons.json'), JSON.stringify(icons, null, 2));
  console.log('  icons:', icons.length, icons.map(i => i.label).join(' | '));

  // Click each icon sequentially, screenshot, Esc
  console.log('[8] Click each compose icon');
  for (let i = 0; i < icons.length; i++) {
    const ic = icons[i];
    try {
      await page.mouse.move(ic.rect.x + ic.rect.width/2, ic.rect.y + ic.rect.height/2, { steps: 6 });
      await human(page, 1500, 2500);
      await page.mouse.click(ic.rect.x + ic.rect.width/2, ic.rect.y + ic.rect.height/2);
      await human(page, 3000, 4500);
      const slug = (ic.label || `icon-${i}`).replace(/[^a-z0-9]/gi, '-').slice(0, 25).toLowerCase();
      await shot(page, `07-icon-${i}-${slug}`);
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
    } catch (e) { console.log('  icon err', i, e.message); }
  }

  // Specifically find and click the AI button we saw earlier (top 739, left 576)
  console.log('[9] Click AI Assist button directly');
  const aiBtn = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of all) {
      const al = (b.getAttribute('aria-label') || '').toLowerCase();
      if (/ai\s*assist|ai assist/i.test(al)) {
        const r = b.getBoundingClientRect();
        b.click();
        return { label: al, rect: r };
      }
    }
    return null;
  });
  console.log('  AI btn clicked:', JSON.stringify(aiBtn));
  await human(page, 3000, 4500);
  await shot(page, '08-ai-assist-menu');

  // Capture menu items
  const aiMenu = await page.evaluate(() => {
    const items = Array.from(document.querySelectorAll('[role="menuitem"], [role="menu"] * , [class*="dropdown" i] li, [class*="menu" i] button'));
    return items
      .filter(el => {
        const r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0 && r.top > 300;
      })
      .map(el => ({
        tag: el.tagName,
        text: (el.innerText || '').trim().slice(0, 80),
        role: el.getAttribute('role'),
      }))
      .filter(x => x.text && x.text.length < 120);
  });
  fs.writeFileSync(path.join(NOTES, 'ai-menu.json'), JSON.stringify(aiMenu, null, 2));
  console.log('  ai menu items:', aiMenu.length);
  console.log('  samples:', aiMenu.slice(0, 10).map(m => m.text).join(' | '));

  // Try clicking "Change tone" or similar AI prompt
  const tryClick = async (label) => {
    const loc = page.getByText(label, { exact: false }).first();
    if (await loc.count().catch(() => 0)) {
      try {
        await loc.click({ timeout: 3000 });
        await human(page, 3500, 5000);
        return true;
      } catch { return false; }
    }
    return false;
  };

  for (const cand of ['Change tone', 'Translate', 'Fix spelling', 'Simplify', 'Rephrase', 'Summarize']) {
    const clicked = await tryClick(cand);
    if (clicked) {
      console.log('  clicked:', cand);
      await shot(page, `09-ai-${cand.toLowerCase().replace(/\s+/g, '-')}`);
      // Nested sub-menu?
      await human(page, 2500, 3500);
      await shot(page, `09-ai-${cand.toLowerCase().replace(/\s+/g, '-')}-submenu`);
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1500, 2500);
      break; // one is enough to demo
    }
  }

  // Final
  await shot(page, '99-final');
  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
