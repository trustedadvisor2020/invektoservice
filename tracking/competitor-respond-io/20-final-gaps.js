// Phase 11b: Close final 3 gaps — AI Assist button, Summarize button, Workflow trigger dropdown.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase11-sweep');

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

  // ===== G. AI Assist button at top-right of compose =====
  console.log('[G] AI Assist top-right button');
  await page.goto('https://app.respond.io/space/408881/inbox', { waitUntil: 'domcontentloaded' });
  await human(page, 4500, 6500);

  // All filter + open conv
  await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('div, button, a'));
    for (const el of rows) {
      const r = el.getBoundingClientRect();
      if (r.left < 50 || r.left > 260) continue;
      if (r.width < 180 || r.width > 260) continue;
      const txt = (el.innerText || '').trim();
      if (/^All\s*\d*$/.test(txt)) { el.click(); return true; }
    }
  });
  await human(page, 3500, 5000);
  await page.evaluate(() => {
    const rows = Array.from(document.querySelectorAll('div'));
    const cands = [];
    for (const el of rows) {
      const r = el.getBoundingClientRect();
      if (r.left < 260 || r.left > 560) continue;
      if (r.width < 200 || r.width > 320) continue;
      if (r.height < 50 || r.height > 140) continue;
      const txt = (el.innerText || '').trim();
      if (!txt || /No conversations|Chats|Calls|Open|Newest|Unreplied/i.test(txt.split('\n')[0])) continue;
      cands.push({ el, r });
    }
    cands.sort((a, b) => a.r.top - b.r.top);
    if (cands.length) cands[0].el.click();
  });
  await human(page, 4500, 6500);
  // close any popup
  await page.locator('button[aria-label="Close"]').first().click().catch(() => {});
  await human(page, 1800, 2800);

  // Focus reply editor first (to have content)
  console.log('  focus reply editor + type');
  const ed = await page.evaluate(() => {
    const eds = Array.from(document.querySelectorAll('[contenteditable="true"]'));
    for (const e of eds) {
      const r = e.getBoundingClientRect();
      if (r.top > 700 && r.top < 900 && r.width > 300) {
        return { x: r.left + r.width/2, y: r.top + r.height/2 };
      }
    }
  });
  if (ed) {
    await page.mouse.click(ed.x, ed.y);
    await human(page, 1500, 2500);
    await page.keyboard.type('Hi, thanks for reaching out about the price.', { delay: 60 });
    await human(page, 2500, 3500);
  }

  // Find AI Assist button - text exact "AI Assist"
  console.log('  locate & click AI Assist');
  const aiInfo = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('button, [role="button"], div, span, a'));
    for (const el of all) {
      const txt = (el.innerText || '').trim();
      if (txt === 'AI Assist') {
        const r = el.getBoundingClientRect();
        if (r.width > 40 && r.width < 200) {
          el.scrollIntoView({ block: 'center' });
          el.click();
          return { tag: el.tagName, rect: r };
        }
      }
    }
    return null;
  });
  console.log('    result:', JSON.stringify(aiInfo));
  await human(page, 4500, 6500);
  await shot(page, 'G-01-ai-assist-clicked');

  // Capture any panel/modal that appeared
  const aiPanel = await page.evaluate(() => {
    // Look for panel that opened - typically a right drawer or modal
    const panels = Array.from(document.querySelectorAll('[role="dialog"], aside, [class*="drawer" i], [class*="panel" i]'));
    const visible = panels.filter(p => {
      const r = p.getBoundingClientRect();
      return r.width > 200 && r.height > 200 && (new Date() - 0 > 0);
    });
    return visible.slice(0, 3).map(p => ({
      tag: p.tagName,
      text: (p.innerText || '').trim().slice(0, 400),
    }));
  });
  fs.writeFileSync(path.join(__dirname, 'notes', 'ai-assist-panel.json'), JSON.stringify(aiPanel, null, 2));

  // Close panel
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 2000, 3000);
  await page.locator('button[aria-label="Close"]').first().click().catch(() => {});
  await human(page, 2000, 3000);

  // ===== H. Summarize button =====
  console.log('[H] Summarize button');
  const sumInfo = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('button, [role="button"], div, span'));
    for (const el of all) {
      const txt = (el.innerText || '').trim();
      if (txt === 'Summarize' || /^Summarize$/i.test(txt)) {
        const r = el.getBoundingClientRect();
        if (r.width > 40 && r.width < 200 && r.top > 800) {
          el.scrollIntoView({ block: 'center' });
          el.click();
          return { tag: el.tagName, rect: r };
        }
      }
    }
    return null;
  });
  console.log('    result:', JSON.stringify(sumInfo));
  await human(page, 6000, 9000);
  await shot(page, 'H-01-summarize-output');

  // Capture summarize output
  const sumOut = await page.evaluate(() => {
    const maybe = Array.from(document.querySelectorAll('[role="dialog"], [class*="summary" i], [class*="popover" i]'));
    return maybe
      .filter(p => {
        const r = p.getBoundingClientRect();
        return r.width > 200 && r.height > 100;
      })
      .slice(0, 2)
      .map(p => (p.innerText || '').trim().slice(0, 600));
  });
  fs.writeFileSync(path.join(__dirname, 'notes', 'summarize-output.json'), JSON.stringify(sumOut, null, 2));
  console.log('    summarize texts captured:', sumOut.length);

  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 2000, 3000);

  // ===== I. Workflow trigger dropdown (retry) =====
  console.log('[I] Workflow trigger dropdown');
  await page.goto('https://app.respond.io/space/408881/workflows', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  const wfRow = page.locator('text=Test Workflow Explore').first();
  if (await wfRow.count()) {
    await wfRow.click();
    await human(page, 5000, 7000);
  }

  // Click the trigger node to open trigger picker (node click, not panel)
  console.log('  click trigger node center');
  await page.evaluate(() => {
    // Target: center node labeled "Trigger" in canvas area
    const all = Array.from(document.querySelectorAll('*'));
    for (const el of all) {
      const txt = (el.innerText || '').trim();
      if (txt === 'Trigger\nSelect a trigger' || txt === 'Select a trigger') {
        const r = el.getBoundingClientRect();
        if (r.left > 500 && r.left < 900 && r.top > 300 && r.top < 500) {
          el.click();
          return { rect: r };
        }
      }
    }
  });
  await human(page, 2500, 3500);
  // Now click the "Select a trigger" combobox in right panel
  const trigClicked = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('[role="combobox"], button, div'));
    for (const el of all) {
      const txt = (el.innerText || '').trim();
      if (/^Select a trigger$/i.test(txt)) {
        const r = el.getBoundingClientRect();
        if (r.left > 900) {
          el.click();
          return true;
        }
      }
    }
    return false;
  });
  console.log('    trigger combobox:', trigClicked);
  await human(page, 3500, 5000);
  await shot(page, 'I-01-trigger-dropdown');

  // Enumerate options
  const trigOpts = await page.evaluate(() => {
    const items = Array.from(document.querySelectorAll('[role="option"], [role="menuitem"]'));
    return items
      .map(el => ({
        text: (el.innerText || '').trim(),
        top: el.getBoundingClientRect().top,
      }))
      .filter(x => x.text && x.text.length < 200 && x.text.length > 2);
  });
  fs.writeFileSync(path.join(__dirname, 'notes', 'workflow-triggers.json'), JSON.stringify(trigOpts, null, 2));
  console.log('    triggers:', trigOpts.length);
  console.log('    sample:', trigOpts.slice(0, 10).map(o => o.text).join(' | '));

  // Scroll the trigger list
  for (let i = 0; i < 4; i++) {
    await page.mouse.move(1100, 400);
    await page.mouse.wheel(0, 350);
    await human(page, 1800, 2800);
    await shot(page, `I-02-trigger-scroll-${i}`);
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
