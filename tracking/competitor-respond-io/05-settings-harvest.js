// Phase 3.5: Harvest full Workspace Settings sidebar + visit each section.
// We arrived here via Connect Channel click in Phase 3. The left rail shows
// the complete settings nav which is the master feature map.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase3_5-settings');
const NOTES = path.join(__dirname, 'notes');
// We know channels is under /space/<id>/... - start there, then harvest left nav.
const START_URL = 'https://app.respond.io/space/408881/workspace/channels';

fs.mkdirSync(SHOTS, { recursive: true });

async function human(page, min = 2500, max = 4500) {
  await page.waitForTimeout(Math.floor(min + Math.random() * (max - min)));
}
function safeName(s) { return s.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase().slice(0, 50); }
async function shot(page, name) {
  const p = path.join(SHOTS, `${name}.png`);
  await page.screenshot({ path: p, fullPage: true }).catch(() => {});
  console.log('  shot:', name);
}

(async () => {
  console.log('[launch]');
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

  console.log('[1] Goto channels settings (should land inside Workspace Settings nav)');
  await page.goto(START_URL, { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5500);
  await shot(page, '00-landing');

  console.log('[2] Wait for current URL to stabilize');
  console.log('  URL:', page.url());

  console.log('[3] Extract full left settings nav');
  const settingsNav = await page.evaluate(() => {
    const items = [];
    // Any link inside the settings page area. Settings typically uses aside/nav.
    // Target left rail: everything with href containing /workspace/ or /settings/ etc.
    document.querySelectorAll('a').forEach(a => {
      const href = a.getAttribute('href');
      if (!href) return;
      if (!/\/(workspace|settings|space\/\d+\/workspace)/.test(href)) return;
      const rect = a.getBoundingClientRect();
      if (rect.left > 260 || rect.width < 40) return; // keep only left rail
      const txt = (a.innerText || a.getAttribute('aria-label') || a.title || '').trim();
      items.push({ href, text: txt, x: rect.left, y: rect.top });
    });
    // Also capture group headers (non-link sections)
    document.querySelectorAll('[class*="settings" i] h3, [class*="settings" i] h4, aside h3, aside h4').forEach(h => {
      const txt = (h.innerText || '').trim();
      if (txt && txt.length < 40) {
        const rect = h.getBoundingClientRect();
        if (rect.left < 260) items.push({ header: true, text: txt, x: rect.left, y: rect.top });
      }
    });
    items.sort((a, b) => a.y - b.y);
    return items;
  });
  console.log('  left rail items:', settingsNav.length);
  fs.writeFileSync(path.join(NOTES, 'settings-nav.json'), JSON.stringify(settingsNav, null, 2));

  // Dedup by href and visit each
  console.log('[4] Visit each settings section');
  const seen = new Set();
  const visited = [];
  for (const item of settingsNav) {
    if (item.header || !item.href) continue;
    if (seen.has(item.href)) continue;
    seen.add(item.href);
    const url = item.href.startsWith('/') ? `https://app.respond.io${item.href}` : item.href;
    const slug = safeName(item.text || item.href);
    try {
      console.log(`  -> ${item.text || item.href}`);
      await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 20000 });
      await human(page, 3000, 4800);
      await shot(page, `set-${slug}`);
      // Capture visible text excerpt + sub-navs
      const info = await page.evaluate(() => {
        const title = document.querySelector('h1, h2')?.innerText?.trim() || '';
        const subTabs = [];
        document.querySelectorAll('[role="tab"], [class*="tab" i][class*="list" i] > *, nav button').forEach(t => {
          const tt = (t.innerText || '').trim();
          if (tt && tt.length < 40) subTabs.push(tt);
        });
        return { title, subTabs: [...new Set(subTabs)].slice(0, 30) };
      });
      visited.push({ text: item.text, href: item.href, finalUrl: page.url(), ...info });
    } catch (e) {
      console.log('     ! failed:', e.message);
      visited.push({ text: item.text, href: item.href, error: e.message });
    }
  }
  fs.writeFileSync(path.join(NOTES, 'settings-visited.json'), JSON.stringify(visited, null, 2));

  console.log('DONE. Phase 3.5 settings harvest.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => {
  console.error('ERROR:', err.message);
  process.exit(1);
});
