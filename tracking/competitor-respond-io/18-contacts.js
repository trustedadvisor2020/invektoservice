// Phase 4: Contacts deep dive - Trusted Advisor detail, Add Contact modal, Add Segment builder.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase4-contacts');
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

  console.log('[1] Goto Contacts');
  await page.goto('https://app.respond.io/space/408881/contact', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);
  // Dismiss any popup
  const closeX = page.locator('button[aria-label="Close"]').first();
  if (await closeX.count().catch(() => 0)) { try { await closeX.click(); await human(page, 1500, 2500); } catch {} }
  await shot(page, 'ct-01-list');

  // Click "All" segment first to ensure we see Trusted Advisor
  console.log('[2] Click All');
  const allSeg = page.getByText('All', { exact: true }).first();
  if (await allSeg.count()) { try { await allSeg.click(); await human(page, 3000, 4500); } catch {} }
  await shot(page, 'ct-02-all');

  // Click the Trusted Advisor contact row
  console.log('[3] Click Trusted Advisor contact');
  const taRow = page.locator('text=Trusted Advisor').first();
  if (await taRow.count()) {
    try {
      await taRow.click();
      await human(page, 4000, 5500);
      await shot(page, 'ct-03-trusted-advisor-detail');
    } catch (e) { console.log('  ! click err:', e.message); }
  }

  // Scroll the contact detail
  console.log('[4] Scroll detail panel');
  for (let i = 0; i < 4; i++) {
    await page.mouse.move(1100, 450);
    await page.mouse.wheel(0, 500);
    await human(page, 2000, 3000);
    await shot(page, `ct-04-detail-scroll-${i}`);
  }

  // Capture custom field schema
  console.log('[5] Capture fields/labels');
  const fields = await page.evaluate(() => {
    // Labels + accompanying input placeholders
    return Array.from(document.querySelectorAll('label, [class*="field" i] > :first-child'))
      .map(el => (el.innerText || '').trim())
      .filter(t => t && t.length < 60 && t.length > 1);
  });
  fs.writeFileSync(path.join(NOTES, 'contact-fields.json'), JSON.stringify([...new Set(fields)], null, 2));

  // Close detail, go back to list
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1800, 2800);
  await page.goto('https://app.respond.io/space/408881/contact', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);

  // Click + Add Contact
  console.log('[6] Add Contact modal');
  const addBtn = page.locator('button:has-text("Add contact")').first();
  if (await addBtn.count()) {
    try {
      await addBtn.click();
      await human(page, 3500, 5000);
      await shot(page, 'ct-05-add-contact-modal');

      // Capture all fields in modal
      const modalFields = await page.evaluate(() => {
        const dlg = document.querySelector('[role="dialog"]');
        if (!dlg) return null;
        return {
          inputs: Array.from(dlg.querySelectorAll('input, textarea, select'))
            .map(i => ({ type: i.tagName, placeholder: i.getAttribute('placeholder'), name: i.getAttribute('name') }))
            .filter(x => x.placeholder || x.name),
          labels: Array.from(dlg.querySelectorAll('label'))
            .map(l => (l.innerText || '').trim()),
        };
      });
      fs.writeFileSync(path.join(NOTES, 'add-contact-modal.json'), JSON.stringify(modalFields, null, 2));

      // Scroll modal if needed
      for (let i = 0; i < 3; i++) {
        await page.mouse.move(720, 450);
        await page.mouse.wheel(0, 400);
        await human(page, 2000, 3000);
        await shot(page, `ct-06-add-modal-scroll-${i}`);
      }
      await page.keyboard.press('Escape').catch(() => {});
      await human(page, 1800, 2800);
    } catch (e) { console.log('  ! add err:', e.message); }
  }

  // Add Segment flow
  console.log('[7] Add Segment builder');
  const segBtn = page.locator('button:has-text("Add segment")').first();
  if (await segBtn.count()) {
    try {
      await segBtn.click();
      await human(page, 3500, 5000);
      await shot(page, 'ct-07-add-segment-modal');

      // Click "Add filter" / condition builder
      const addFilter = page.locator('button:has-text("Add filter"), button:has-text("Add condition")').first();
      if (await addFilter.count()) {
        try {
          await addFilter.click();
          await human(page, 3000, 4500);
          await shot(page, 'ct-08-segment-filter');
        } catch {}
      }

      // Capture segment builder structure
      const segBuilder = await page.evaluate(() => {
        const dlg = document.querySelector('[role="dialog"]');
        if (!dlg) return null;
        return {
          dropdowns: Array.from(dlg.querySelectorAll('[role="combobox"], select, button[aria-haspopup="listbox"]'))
            .map(d => ({ label: d.getAttribute('aria-label'), text: (d.innerText || '').trim().slice(0, 60) })),
          fields: Array.from(dlg.querySelectorAll('label'))
            .map(l => (l.innerText || '').trim()),
          buttons: Array.from(dlg.querySelectorAll('button'))
            .map(b => (b.innerText || '').trim()).filter(t => t && t.length < 40),
        };
      });
      fs.writeFileSync(path.join(NOTES, 'segment-builder.json'), JSON.stringify(segBuilder, null, 2));
      await page.keyboard.press('Escape').catch(() => {});
    } catch (e) { console.log('  ! seg err:', e.message); }
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
