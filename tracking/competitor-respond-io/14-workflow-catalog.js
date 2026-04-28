// Phase 5b: Click the trigger dropdown to capture all trigger options, then click Add step to capture action catalog.
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

  // Go back to workflows and find existing (we created "Test Workflow Explore")
  console.log('[1] Goto Workflows list');
  await page.goto('https://app.respond.io/space/408881/workflows', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  await shot(page, 'wf2-01-list');

  // Click the workflow we created
  console.log('[2] Open Test Workflow Explore');
  const wf = page.locator('text=Test Workflow Explore').first();
  if (await wf.count()) {
    await wf.click();
    await human(page, 4500, 6500);
  } else {
    console.log('  workflow not found, creating new');
    const scratch = page.locator('button:has-text("Start From Scratch")').first();
    if (await scratch.count()) {
      await scratch.click();
      await human(page, 4000, 5500);
      const nameI = page.locator('input').first();
      if (await nameI.count()) {
        await nameI.type('Explore2', { delay: 70 });
        await human(page, 1500, 2500);
        const next = page.locator('button:has-text("Next"), button:has-text("Create")').first();
        if (await next.count()) { await next.click(); await human(page, 4500, 6500); }
      }
    }
  }
  await shot(page, 'wf2-02-builder-open');

  // Click Select a trigger
  console.log('[3] Open trigger dropdown');
  const trigDd = page.locator('text=Select a trigger').first();
  if (await trigDd.count()) {
    await trigDd.click();
    await human(page, 3500, 5000);
    await shot(page, 'wf2-03-trigger-dropdown');
    // Capture all options
    const triggers = await page.evaluate(() => {
      const opts = Array.from(document.querySelectorAll('[role="option"], [role="menuitem"], [class*="select" i] li, [class*="dropdown" i] button'));
      return opts
        .map(el => (el.innerText || '').trim())
        .filter(t => t && t.length < 200 && t.length > 2);
    });
    fs.writeFileSync(path.join(NOTES, 'workflow-triggers.json'), JSON.stringify([...new Set(triggers)], null, 2));
    console.log('  triggers:', triggers.length);
    console.log('  sample:', [...new Set(triggers)].slice(0, 20).join(' | '));

    // Pick a trigger (Conversation Opened - common)
    const pickTrig = async (name) => {
      const l = page.getByText(name, { exact: false }).first();
      if (await l.count().catch(() => 0)) { try { await l.click(); return true; } catch {} }
      return false;
    };
    const picked = (await pickTrig('Conversation Opened')) || (await pickTrig('New Contact'));
    await human(page, 3500, 5000);
    await shot(page, 'wf2-04-trigger-selected');
    console.log('  trigger picked:', picked);
  } else {
    console.log('  ! trigger dropdown not found');
  }

  // Click the + node to open action picker
  console.log('[4] Click + add step in canvas');
  const plusClicked = await page.evaluate(() => {
    // Find SVG with + pattern in canvas area
    const candidates = Array.from(document.querySelectorAll('button, [role="button"], svg'));
    for (const c of candidates) {
      const r = c.getBoundingClientRect();
      if (r.top < 300 || r.top > 700) continue;
      if (r.left < 300 || r.left > 1100) continue;
      if (r.width > 18 && r.width < 50) {
        const html = c.outerHTML || '';
        if (/plus|add|\+/i.test(html) || c.tagName === 'BUTTON' || c.tagName === 'svg') {
          (c.click && c.click()) || c.dispatchEvent(new MouseEvent('click', { bubbles: true }));
          return { html: html.slice(0, 100), rect: r };
        }
      }
    }
    return null;
  });
  console.log('  plus click:', JSON.stringify(plusClicked));
  await human(page, 3500, 5000);
  await shot(page, 'wf2-05-after-plus');

  // Now capture the action catalog - usually sidebar / modal
  console.log('[5] Capture action catalog');
  const actions = await page.evaluate(() => {
    // Action picker is typically a list in the right panel or modal
    const items = Array.from(document.querySelectorAll('[role="option"], [role="menuitem"], [class*="action" i] [class*="item" i], [class*="step" i] li'));
    const result = [];
    items.forEach(el => {
      const txt = (el.innerText || '').trim();
      if (txt && txt.length < 100 && txt.length > 2) result.push(txt);
    });
    // Also generic buttons in right panel
    document.querySelectorAll('button').forEach(b => {
      const r = b.getBoundingClientRect();
      if (r.left < 900) return;
      const txt = (b.innerText || '').trim();
      if (txt && txt.length < 60 && txt.length > 2) result.push(txt);
    });
    return [...new Set(result)];
  });
  fs.writeFileSync(path.join(NOTES, 'workflow-actions.json'), JSON.stringify(actions, null, 2));
  console.log('  actions captured:', actions.length);

  // Scroll through action list
  await page.mouse.move(1200, 450);
  for (let i = 0; i < 3; i++) {
    await page.mouse.wheel(0, 400);
    await human(page, 2000, 3000);
    await shot(page, `wf2-06-actions-scroll-${i}`);
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
