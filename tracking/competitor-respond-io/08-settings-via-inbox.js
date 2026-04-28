// Phase 3.5d: Enter Workspace Settings via Inbox empty-state "Connect a Channel" CTA,
// which reliably redirects into the settings UI. Then harvest the left rail and visit each link.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase3_5-settings');
const NOTES = path.join(__dirname, 'notes');
fs.mkdirSync(SHOTS, { recursive: true });

async function human(page, min = 2500, max = 4500) {
  await page.waitForTimeout(Math.floor(min + Math.random() * (max - min)));
}
function safeName(s) { return s.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase().slice(0, 60); }
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
  await human(page, 4000, 5500);

  console.log('[2] Click Connect Channel CTA');
  const cta = page.locator('button:has-text("Connect Channel"), button:has-text("Connect a Channel")').first();
  if (!(await cta.count())) {
    console.log('  ! no CTA found; going directly to channels settings URL');
    await page.goto('https://app.respond.io/space/408881/workspace/channel/connect', { waitUntil: 'domcontentloaded' });
  } else {
    await cta.click();
  }
  await human(page, 4500, 6500);
  console.log('  url:', page.url());
  await shot(page, 'via-inbox-00-landed');

  console.log('[3] Harvest left rail links (Workspace settings nav)');
  const navItems = await page.evaluate(() => {
    const items = [];
    document.querySelectorAll('a, [role="link"]').forEach(a => {
      const rect = a.getBoundingClientRect();
      if (rect.left < 40 || rect.left > 260) return;
      if (rect.width < 50 || rect.height < 14) return;
      const href = a.getAttribute('href') || '';
      const txt = (a.innerText || a.getAttribute('aria-label') || '').trim();
      if (!txt || txt.length > 60) return;
      items.push({ href, text: txt, x: rect.left, y: rect.top });
    });
    // Also buttons in left rail that are menu items (some nav is <button>)
    document.querySelectorAll('button').forEach(b => {
      const rect = b.getBoundingClientRect();
      if (rect.left < 40 || rect.left > 260) return;
      if (rect.width < 50) return;
      const txt = (b.innerText || '').trim();
      if (!txt || txt.length > 60) return;
      items.push({ href: '', text: txt, isBtn: true, x: rect.left, y: rect.top });
    });
    // Section headers
    document.querySelectorAll('div, h3, h4').forEach(h => {
      const rect = h.getBoundingClientRect();
      if (rect.left < 40 || rect.left > 260) return;
      if (rect.width > 260) return;
      if (h.children.length > 0) return;
      const txt = (h.innerText || '').trim();
      if (!txt || txt.length > 30 || /[:.,(]/.test(txt)) return;
      items.push({ header: true, text: txt, x: rect.left, y: rect.top });
    });
    items.sort((a, b) => a.y - b.y);
    const seen = new Set();
    return items.filter(it => {
      const k = `${it.header ? 'H' : 'L'}:${it.text}:${it.href || ''}`;
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  });
  console.log('  nav items:', navItems.length);
  fs.writeFileSync(path.join(NOTES, 'workspace-settings-nav.json'), JSON.stringify(navItems, null, 2));

  // Visit each link
  console.log('[4] Visit each settings link');
  const visited = [];
  const seenHref = new Set();
  for (const it of navItems) {
    if (it.header || !it.href) continue;
    // Skip external / full URLs
    if (it.href.startsWith('http') && !it.href.includes('respond.io')) continue;
    if (seenHref.has(it.href)) continue;
    seenHref.add(it.href);
    const url = it.href.startsWith('/') ? `https://app.respond.io${it.href}` : it.href;
    const slug = safeName(it.text || it.href);
    try {
      console.log('  ->', it.text);
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await human(page, 3200, 4800);
      await shot(page, `ws-${slug}`);
      const info = await page.evaluate(() => {
        const title = document.querySelector('h1, h2')?.innerText?.trim() || '';
        const subTabs = new Set();
        document.querySelectorAll('[role="tab"], main nav button, [role="tablist"] *').forEach(t => {
          const tt = (t.innerText || '').trim();
          if (tt && tt.length < 30 && tt.length > 1) subTabs.add(tt);
        });
        // Major section headers in main area
        const mainHeaders = [];
        document.querySelectorAll('main h1, main h2, main h3, [class*="main"] h3').forEach(h => {
          const txt = (h.innerText || '').trim();
          if (txt && txt.length < 80) mainHeaders.push(txt);
        });
        return { title, subTabs: [...subTabs].slice(0, 30), mainHeaders: [...new Set(mainHeaders)].slice(0, 15) };
      });
      visited.push({ text: it.text, href: it.href, finalUrl: page.url(), ...info });
    } catch (e) {
      visited.push({ text: it.text, href: it.href, error: e.message });
    }
  }
  fs.writeFileSync(path.join(NOTES, 'workspace-settings-visited.json'), JSON.stringify(visited, null, 2));

  console.log('DONE. Visits:', visited.length);
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
