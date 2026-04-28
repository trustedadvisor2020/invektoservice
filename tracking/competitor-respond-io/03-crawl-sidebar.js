// Phase 2: Crawl onboarding checklist + every sidebar top-level page.
// Uses persistent profile - no re-login needed.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase2');
const NOTES = path.join(__dirname, 'notes');
const START_URL = 'https://app.respond.io/space/408881/dashboard';

fs.mkdirSync(SHOTS, { recursive: true });
fs.mkdirSync(NOTES, { recursive: true });

function safeName(s) { return s.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase(); }
async function shot(page, name) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true }).catch(() => {});
  console.log('  shot:', name);
}
async function saveDom(page, name) {
  try {
    const html = await page.content();
    fs.writeFileSync(path.join(NOTES, `${name}.html`), html);
  } catch {}
}
async function visibleText(page) {
  return await page.locator('body').innerText().catch(() => '');
}

(async () => {
  console.log('[1/4] Launch (reuse profile)');
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

  console.log('[2/4] Goto dashboard');
  await page.goto(START_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(4000);
  await shot(page, '00-dashboard');

  // Extract sidebar structure via DOM
  console.log('[3/4] Extracting sidebar nav');
  const navItems = await page.evaluate(() => {
    const items = [];
    // Sidebar <nav> or similar. Try common patterns.
    const candidates = document.querySelectorAll('nav a, [role="navigation"] a, aside a, [class*="sidebar" i] a');
    const seen = new Set();
    candidates.forEach(a => {
      const href = a.getAttribute('href');
      if (!href || href.startsWith('http') || seen.has(href)) return;
      const txt = (a.innerText || a.getAttribute('aria-label') || a.title || '').trim();
      const rect = a.getBoundingClientRect();
      if (rect.width < 20 || rect.height < 20) return;
      seen.add(href);
      items.push({ href, text: txt, y: rect.top });
    });
    items.sort((a, b) => a.y - b.y);
    return items;
  });
  console.log('  sidebar items:', navItems.length);
  fs.writeFileSync(path.join(NOTES, 'sidebar.json'), JSON.stringify(navItems, null, 2));

  // Also grab icon-only sidebar buttons (aria-labels on <button> or tooltip-triggering divs)
  const iconButtons = await page.evaluate(() => {
    const arr = [];
    document.querySelectorAll('button[aria-label], [role="button"][aria-label]').forEach(b => {
      const label = b.getAttribute('aria-label');
      const rect = b.getBoundingClientRect();
      if (rect.left > 100 || rect.width < 20) return; // left sidebar area
      arr.push({ label, y: rect.top });
    });
    arr.sort((a, b) => a.y - b.y);
    return arr;
  });
  fs.writeFileSync(path.join(NOTES, 'sidebar-icons.json'), JSON.stringify(iconButtons, null, 2));
  console.log('  icon buttons in sidebar area:', iconButtons.length);

  // Click Onboarding checklist button if present
  console.log('[4/4] Onboarding checklist');
  const ocBtn = page.locator('button:has-text("Onboarding checklist")').first();
  if (await ocBtn.count() > 0) {
    await ocBtn.click();
    await page.waitForTimeout(2500);
    await shot(page, '01-onboarding-checklist');
    // Close any modal
    await page.keyboard.press('Escape').catch(() => {});
    await page.waitForTimeout(800);
  }

  // Visit each sidebar URL
  const visited = [];
  for (const item of navItems) {
    const url = item.href.startsWith('/') ? `https://app.respond.io${item.href}` : item.href;
    const slug = safeName(item.text || item.href).slice(0, 40) || safeName(item.href).slice(0, 40);
    try {
      console.log(`  visit: ${item.text || item.href} -> ${url}`);
      await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 20000 });
      await page.waitForTimeout(3500);
      await shot(page, `nav-${slug}`);
      await saveDom(page, `nav-${slug}`);
      const txt = await visibleText(page);
      visited.push({
        text: item.text,
        href: item.href,
        finalUrl: page.url(),
        visibleTextExcerpt: txt.split('\n').filter(l => l.trim()).slice(0, 40).join(' | ').slice(0, 2000),
      });
    } catch (e) {
      console.log('   ! failed:', e.message);
      visited.push({ text: item.text, href: item.href, error: e.message });
    }
  }
  fs.writeFileSync(path.join(NOTES, 'visited.json'), JSON.stringify(visited, null, 2));

  console.log('DONE. Visited', visited.length, 'pages.');
  await page.waitForTimeout(3000);
  await ctx.close();
})().catch(err => {
  console.error('ERROR:', err.message);
  process.exit(1);
});
