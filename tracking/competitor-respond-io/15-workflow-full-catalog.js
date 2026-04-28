// Phase 5c: Capture the FULL trigger + action catalog via scroll-through.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase5-workflows');
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

  console.log('[1] Open existing workflow');
  await page.goto('https://app.respond.io/space/408881/workflows', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);
  const wf = page.locator('text=Test Workflow Explore').first();
  if (await wf.count()) { await wf.click(); await human(page, 5000, 6500); }
  await shot(page, 'wf3-01-builder');

  // Click the trigger dropdown INSIDE THE NODE (not the right panel one)
  console.log('[2] Click trigger dropdown');
  // There are two "Select a trigger" texts: one in the node (center) and one in right panel
  // Try right panel first (it's more reliable)
  const rightPanelDd = await page.evaluate(() => {
    // Find dropdown in right panel (x > 900)
    const dd = Array.from(document.querySelectorAll('div, button, [role="combobox"]'))
      .find(el => {
        const r = el.getBoundingClientRect();
        if (r.left < 900) return false;
        if (r.width < 150) return false;
        return /select\s*a\s*trigger/i.test((el.innerText || '').trim());
      });
    if (dd) { dd.click(); return true; }
    return false;
  });
  console.log('  dd opened:', rightPanelDd);
  await human(page, 3500, 5000);
  await shot(page, 'wf3-02-trigger-dropdown');

  // Capture all visible options (should be scrollable list)
  const triggerOptions = [];
  for (let scroll = 0; scroll < 6; scroll++) {
    const opts = await page.evaluate(() => {
      // Find list container visible
      const items = Array.from(document.querySelectorAll('[role="option"], [role="menuitem"], [class*="option" i], li'));
      return items
        .map(el => {
          const r = el.getBoundingClientRect();
          return { text: (el.innerText || '').trim(), top: r.top, visible: r.width > 0 && r.height > 0 };
        })
        .filter(x => x.visible && x.text && x.text.length < 200 && x.text.length > 2);
    });
    opts.forEach(o => triggerOptions.push(o.text));
    await shot(page, `wf3-03-trigger-scroll-${scroll}`);
    // Scroll the dropdown list
    await page.mouse.move(1200, 400);
    await page.mouse.wheel(0, 350);
    await human(page, 1800, 2800);
  }
  const uniqTriggers = [...new Set(triggerOptions)];
  fs.writeFileSync(path.join(NOTES, 'workflow-triggers.json'), JSON.stringify(uniqTriggers, null, 2));
  console.log('  unique triggers:', uniqTriggers.length);

  // Close dropdown
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1500, 2500);

  // Open action picker via + node
  console.log('[3] Click + to open Add Steps');
  await page.evaluate(() => {
    const btns = Array.from(document.querySelectorAll('button[aria-haspopup="menu"]'));
    for (const b of btns) {
      const r = b.getBoundingClientRect();
      if (r.top > 400 && r.top < 600 && r.left > 600 && r.left < 900) {
        b.click();
        return true;
      }
    }
  });
  await human(page, 3500, 5000);
  await shot(page, 'wf3-04-add-steps');

  // Capture Add Steps and scroll
  const actionOptions = [];
  for (let scroll = 0; scroll < 8; scroll++) {
    const opts = await page.evaluate(() => {
      // Add Steps menu contains items with title + subtitle
      const items = Array.from(document.querySelectorAll('[role="menuitem"], [role="option"], [class*="step" i] > div, [class*="action" i] button'));
      const result = [];
      items.forEach(el => {
        const r = el.getBoundingClientRect();
        if (r.width < 50 || r.height < 10) return;
        const txt = (el.innerText || '').trim();
        if (txt && txt.length < 300 && txt.length > 3) result.push(txt);
      });
      return result;
    });
    opts.forEach(o => actionOptions.push(o));
    await shot(page, `wf3-05-steps-scroll-${scroll}`);
    // Scroll inside the Add Steps list (mouse target center of list)
    await page.mouse.move(900, 650);
    await page.mouse.wheel(0, 400);
    await human(page, 1800, 2800);
  }
  const uniqActions = [...new Set(actionOptions)];
  fs.writeFileSync(path.join(NOTES, 'workflow-actions.json'), JSON.stringify(uniqActions, null, 2));
  console.log('  unique actions:', uniqActions.length);

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
