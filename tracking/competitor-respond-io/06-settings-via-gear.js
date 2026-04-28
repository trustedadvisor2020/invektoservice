// Phase 3.5b: Navigate to settings via the gear icon in the main sidebar.
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
function safeName(s) { return s.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase().slice(0, 50); }
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

  console.log('[1] Goto dashboard');
  await page.goto('https://app.respond.io/space/408881/dashboard', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5500);

  console.log('[2] Click gear icon (settings) - find by svg or aria');
  // Try multiple approaches
  const gearClicked = await page.evaluate(() => {
    // Find the lowest-positioned icon in the left sidebar that has "setting" in any attribute
    const candidates = Array.from(document.querySelectorAll('aside button, aside a, nav button, nav a'));
    const scored = candidates.map(el => {
      const rect = el.getBoundingClientRect();
      if (rect.left > 60) return null;
      const attrs = Array.from(el.attributes || []).map(a => `${a.name}=${a.value}`).join(' ');
      const html = el.outerHTML || '';
      const score = /gear|setting|cog|preference/i.test(attrs + ' ' + html) ? 100 : 0;
      return { el, rect, score, html };
    }).filter(Boolean);
    // Pick highest-scoring or lowest-y (bottom of sidebar)
    scored.sort((a, b) => b.score - a.score || b.rect.top - a.rect.top);
    if (scored.length && scored[0].score > 0) {
      scored[0].el.click();
      return { via: 'attr-match', html: scored[0].html.slice(0, 200) };
    }
    // Fallback: click the lowest icon in sidebar
    scored.sort((a, b) => b.rect.top - a.rect.top);
    if (scored.length) {
      scored[0].el.click();
      return { via: 'bottom-sidebar', html: scored[0].html.slice(0, 200) };
    }
    return null;
  });
  console.log('  gear click result:', gearClicked);
  await human(page, 4000, 6000);
  console.log('  url:', page.url());
  await shot(page, 'gear-00-after-click');

  // If URL still dashboard, try clicking by coordinates - gear is the bottom icon we saw
  if (page.url().includes('/dashboard')) {
    console.log('[2b] Fallback: click by sidebar position');
    // From screenshots, gear icon is around (23, 437)
    await page.mouse.click(23, 437);
    await human(page, 3500, 5500);
    console.log('  url:', page.url());
    await shot(page, 'gear-00b-coord-click');
  }

  console.log('[3] Extract left settings nav');
  const settingsNav = await page.evaluate(() => {
    const items = [];
    // Collect all links whose bounding rect is in left-rail area but BEYOND the main 60px sidebar
    document.querySelectorAll('a').forEach(a => {
      const rect = a.getBoundingClientRect();
      if (rect.left < 60 || rect.left > 280) return;
      if (rect.width < 40 || rect.height < 15) return;
      const href = a.getAttribute('href') || '';
      const txt = (a.innerText || a.getAttribute('aria-label') || '').trim();
      if (!txt && !href) return;
      items.push({ href, text: txt, x: rect.left, y: rect.top });
    });
    // Section headers (divs/h3/h4 in left rail)
    document.querySelectorAll('div, h3, h4').forEach(h => {
      const rect = h.getBoundingClientRect();
      if (rect.left < 60 || rect.left > 280) return;
      if (rect.width > 260) return;
      const txt = (h.innerText || '').trim();
      // Heuristic: short, all caps or title case headers that appear like section labels
      if (txt && txt.length > 0 && txt.length < 30 && !/[:.,]/.test(txt)) {
        // Skip if child has link (already captured)
        if (h.querySelector('a')) return;
        items.push({ header: true, text: txt, x: rect.left, y: rect.top });
      }
    });
    items.sort((a, b) => a.y - b.y);
    // Filter duplicates
    const seen = new Set();
    return items.filter(it => {
      const k = `${it.header ? 'H' : 'L'}:${it.text}:${it.href || ''}`;
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  });
  console.log('  left rail items:', settingsNav.length);
  fs.writeFileSync(path.join(NOTES, 'settings-nav.json'), JSON.stringify(settingsNav, null, 2));
  await shot(page, 'settings-landed');

  // Visit each settings link
  console.log('[4] Visit each settings link');
  const visited = [];
  const seenHref = new Set();
  for (const it of settingsNav) {
    if (it.header || !it.href) continue;
    if (seenHref.has(it.href)) continue;
    seenHref.add(it.href);
    const url = it.href.startsWith('/') ? `https://app.respond.io${it.href}` : it.href;
    const slug = safeName(it.text || it.href);
    try {
      console.log('  ->', it.text || it.href);
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await human(page, 3500, 5200);
      await shot(page, `set-${slug}`);
      const info = await page.evaluate(() => {
        const title = document.querySelector('h1, h2')?.innerText?.trim() || '';
        const subTabs = new Set();
        document.querySelectorAll('[role="tab"], nav button, [class*="tab" i] > *').forEach(t => {
          const tt = (t.innerText || '').trim();
          if (tt && tt.length < 30 && tt.length > 1) subTabs.add(tt);
        });
        // Capture visible section headers in main area
        const mainHeaders = [];
        document.querySelectorAll('main h1, main h2, main h3, [class*="content"] h1, [class*="content"] h2, [class*="content"] h3').forEach(h => {
          const txt = (h.innerText || '').trim();
          if (txt && txt.length < 60) mainHeaders.push(txt);
        });
        return { title, subTabs: [...subTabs].slice(0, 40), mainHeaders: [...new Set(mainHeaders)].slice(0, 20) };
      });
      visited.push({ text: it.text, href: it.href, finalUrl: page.url(), ...info });
    } catch (e) {
      visited.push({ text: it.text, href: it.href, error: e.message });
    }
  }
  fs.writeFileSync(path.join(NOTES, 'settings-visited.json'), JSON.stringify(visited, null, 2));

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
