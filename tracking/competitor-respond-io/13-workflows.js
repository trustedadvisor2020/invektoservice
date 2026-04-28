// Phase 5: Workflows deep dive - template library, builder canvas, trigger/action catalog.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase5-workflows');
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

  console.log('[1] Goto Workflows');
  await page.goto('https://app.respond.io/space/408881/workflows', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);
  // Dismiss coach mark
  const close1 = page.locator('button[aria-label="Close"], button:has-text("Close")').first();
  if (await close1.count().catch(() => 0)) { try { await close1.click(); await human(page, 1500, 2500); } catch {} }
  await shot(page, '01-workflows-empty');

  // Click "Start With A Template"
  console.log('[2] Open template library');
  const tmplBtn = page.locator('button:has-text("Start With A Template"), a:has-text("Start With A Template")').first();
  if (await tmplBtn.count()) {
    await tmplBtn.click();
    await human(page, 4000, 5500);
    await shot(page, '02-template-library');

    // Capture template names
    const templates = await page.evaluate(() => {
      const cards = Array.from(document.querySelectorAll('[role="article"], [class*="card" i], [class*="template" i] > div, article'));
      const names = new Set();
      cards.forEach(c => {
        const t = (c.innerText || '').trim();
        if (t && t.length < 300 && t.length > 5) names.add(t.split('\n').slice(0, 2).join(' | '));
      });
      return [...names].slice(0, 60);
    });
    fs.writeFileSync(path.join(NOTES, 'workflow-templates.json'), JSON.stringify(templates, null, 2));
    console.log('  templates:', templates.length);

    // Scroll down to see more
    await page.mouse.wheel(0, 600);
    await human(page, 2500, 3500);
    await shot(page, '03-templates-scroll1');
    await page.mouse.wheel(0, 600);
    await human(page, 2500, 3500);
    await shot(page, '04-templates-scroll2');

    // Capture filter categories (left sidebar often shows categories)
    const cats = await page.evaluate(() => {
      return Array.from(document.querySelectorAll('button, [role="button"], label'))
        .filter(el => {
          const r = el.getBoundingClientRect();
          return r.left < 260 && r.left > 20 && r.width > 80 && r.top > 80 && r.top < 800;
        })
        .map(el => (el.innerText || '').trim())
        .filter(t => t && t.length < 50 && t.length > 2);
    });
    fs.writeFileSync(path.join(NOTES, 'workflow-template-categories.json'), JSON.stringify([...new Set(cats)], null, 2));

    // Scroll back to top and close template gallery
    await page.mouse.wheel(0, -2000);
    await human(page, 2000, 3000);
    await page.keyboard.press('Escape').catch(() => {});
    await human(page, 1500, 2500);
  }

  // Start from scratch - to see builder canvas
  console.log('[3] Start from scratch - see builder');
  await page.goto('https://app.respond.io/space/408881/workflows', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  const scratchBtn = page.locator('button:has-text("Start From Scratch"), a:has-text("Start From Scratch")').first();
  if (await scratchBtn.count()) {
    await scratchBtn.click();
    await human(page, 5000, 7000);
    await shot(page, '05-builder-landing');

    // There's often a modal for "Name your workflow" first
    // Check for input field
    const nameInput = page.locator('input[placeholder*="name" i], input[placeholder*="workflow" i]').first();
    if (await nameInput.count().catch(() => 0)) {
      try {
        await nameInput.click();
        await human(page, 1500, 2500);
        await nameInput.type('Test Workflow Explore', { delay: 70 });
        await human(page, 1800, 2800);
        await shot(page, '06-workflow-named');
        // Click next/continue
        const nextBtn = page.locator('button:has-text("Next"), button:has-text("Create"), button:has-text("Continue")').first();
        if (await nextBtn.count()) {
          await nextBtn.click();
          await human(page, 4500, 6500);
          await shot(page, '07-builder-canvas');
        }
      } catch (e) { console.log('  name input err:', e.message); }
    }

    // The builder should show a trigger picker. Capture trigger catalog
    const triggers = await page.evaluate(() => {
      // Trigger options usually shown as cards / list items
      return Array.from(document.querySelectorAll('[role="option"], [class*="trigger" i] *, [class*="option" i]'))
        .map(el => (el.innerText || '').trim())
        .filter(t => t && t.length < 150 && t.length > 3);
    });
    fs.writeFileSync(path.join(NOTES, 'workflow-triggers.json'), JSON.stringify([...new Set(triggers)].slice(0, 100), null, 2));

    // Look for and click "+" or node - add an action to see action catalog
    // In most flow builders there's a big + at the bottom of trigger
    console.log('[4] Look for add-action button / plus node');
    await shot(page, '08-before-add-action');

    // Try to click a "+" or "Add step" button in the canvas
    await page.evaluate(() => {
      const all = Array.from(document.querySelectorAll('button, [role="button"]'));
      for (const b of all) {
        const txt = (b.innerText || '').trim();
        const al = (b.getAttribute('aria-label') || '').toLowerCase();
        if (txt === '+' || /add.{0,10}step|add.{0,10}action|add.{0,10}node/i.test(al)) {
          b.click();
          return true;
        }
      }
      // Try SVG + icons in canvas area (center)
      const svgs = Array.from(document.querySelectorAll('svg, [class*="node" i]'));
      for (const s of svgs) {
        const r = s.getBoundingClientRect();
        if (r.top < 200 || r.top > 700) continue;
        if (r.left < 400 || r.left > 1100) continue;
        if (r.width > 20 && r.width < 60) {
          s.click();
          return true;
        }
      }
      return false;
    });
    await human(page, 3500, 5000);
    await shot(page, '09-action-picker');

    // Capture action catalog
    const actions = await page.evaluate(() => {
      return Array.from(document.querySelectorAll('[role="option"], [role="menuitem"], [class*="action" i] li, [class*="option" i]'))
        .map(el => (el.innerText || '').trim())
        .filter(t => t && t.length < 150 && t.length > 3);
    });
    fs.writeFileSync(path.join(NOTES, 'workflow-actions.json'), JSON.stringify([...new Set(actions)].slice(0, 200), null, 2));
    console.log('  actions captured:', actions.length);

    // Take a few more screenshots of the canvas
    await page.keyboard.press('Escape').catch(() => {});
    await human(page, 1800, 2800);
    await shot(page, '10-builder-final');
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
