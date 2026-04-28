// Phase 3.5c: Navigate into Workspace / Organization / Personal settings via gear dropdown.
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

async function openGearDropdown(page) {
  // Click the gear icon button we found earlier
  const gearClicked = await page.evaluate(() => {
    const candidates = Array.from(document.querySelectorAll('aside button, nav button'));
    const scored = candidates.map(el => {
      const rect = el.getBoundingClientRect();
      if (rect.left > 60) return null;
      const attrs = Array.from(el.attributes || []).map(a => `${a.name}=${a.value}`).join(' ');
      const html = el.outerHTML || '';
      const score = /gear|setting|cog|preference|dropdown-menu-trigger/i.test(attrs + ' ' + html) ? 100 : 0;
      return { el, rect, score };
    }).filter(Boolean);
    scored.sort((a, b) => b.score - a.score || b.rect.top - a.rect.top);
    if (scored.length) {
      scored[0].el.click();
      return true;
    }
    return false;
  });
  await human(page, 1500, 2500);
  return gearClicked;
}

async function clickDropdownItem(page, label) {
  // First, dump dropdown contents for debug
  const dump = await page.evaluate(() => {
    const all = Array.from(document.querySelectorAll('[role="menuitem"], [data-state="open"] *, [role="menu"] *, [class*="dropdown" i] *'));
    return all.slice(0, 30).map(el => ({
      tag: el.tagName,
      role: el.getAttribute('role'),
      text: (el.innerText || '').trim().slice(0, 60),
      rect: { top: el.getBoundingClientRect().top, left: el.getBoundingClientRect().left },
    })).filter(x => x.text);
  });
  console.log('  dd dump sample:', JSON.stringify(dump.slice(0, 8)));

  // Use Playwright locator with getByText
  try {
    const loc = page.getByText(label, { exact: false }).first();
    if (await loc.count()) {
      await loc.click({ timeout: 4000 });
      await human(page, 3500, 5000);
      return true;
    }
  } catch (e) {
    console.log('  locator click failed:', e.message);
  }
  return false;
}

async function harvestSettingsNav(page) {
  // Once on a settings page, the left rail shows the full nav
  return await page.evaluate(() => {
    const items = [];
    document.querySelectorAll('a').forEach(a => {
      const rect = a.getBoundingClientRect();
      if (rect.left < 30 || rect.left > 280) return;
      if (rect.width < 40) return;
      const href = a.getAttribute('href') || '';
      const txt = (a.innerText || a.getAttribute('aria-label') || '').trim();
      if (!txt) return;
      items.push({ href, text: txt, x: rect.left, y: rect.top });
    });
    // Section headers
    document.querySelectorAll('div').forEach(d => {
      const rect = d.getBoundingClientRect();
      if (rect.left < 30 || rect.left > 280) return;
      if (rect.width > 260) return;
      if (d.children.length > 1) return;
      const txt = (d.innerText || '').trim();
      if (txt && txt.length > 1 && txt.length < 30 && /^[A-Z]/.test(txt) && !/\n/.test(txt) && !d.querySelector('a,button')) {
        items.push({ header: true, text: txt, x: rect.left, y: rect.top });
      }
    });
    // Sort and dedupe
    items.sort((a, b) => a.y - b.y);
    const seen = new Set();
    return items.filter(it => {
      const k = `${it.header ? 'H' : 'L'}:${it.text}:${it.href || ''}`;
      if (seen.has(k)) return false;
      seen.add(k);
      return true;
    });
  });
}

async function visitEachSettingsLink(page, navItems, layer) {
  const visited = [];
  const seenHref = new Set();
  for (const it of navItems) {
    if (it.header || !it.href || seenHref.has(it.href)) continue;
    seenHref.add(it.href);
    const url = it.href.startsWith('/') ? `https://app.respond.io${it.href}` : it.href;
    const slug = `${layer}-${safeName(it.text || it.href)}`;
    try {
      console.log('  ->', it.text, '|', it.href);
      await page.goto(url, { waitUntil: 'domcontentloaded' });
      await human(page, 3500, 5000);
      await shot(page, slug);
      const info = await page.evaluate(() => {
        const title = document.querySelector('h1, h2')?.innerText?.trim() || '';
        const subTabs = new Set();
        document.querySelectorAll('[role="tab"], nav button, button').forEach(t => {
          const tt = (t.innerText || '').trim();
          if (tt && tt.length < 30 && tt.length > 1) subTabs.add(tt);
        });
        return { title, subTabs: [...subTabs].slice(0, 40) };
      });
      visited.push({ layer, text: it.text, href: it.href, finalUrl: page.url(), ...info });
    } catch (e) {
      visited.push({ layer, text: it.text, href: it.href, error: e.message });
    }
  }
  return visited;
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

  const allVisited = [];

  for (const layer of ['Workspace settings', 'Organization settings', 'Personal settings']) {
    console.log(`\n========== ${layer} ==========`);
    await page.goto('https://app.respond.io/space/408881/dashboard', { waitUntil: 'domcontentloaded' });
    await human(page, 3500, 5000);

    if (!(await openGearDropdown(page))) {
      console.log('  ! gear not found');
      continue;
    }
    await shot(page, `dd-${safeName(layer)}-before`);

    const ok = await clickDropdownItem(page, layer);
    if (!ok) {
      console.log('  ! dropdown item not found:', layer);
      continue;
    }
    await human(page, 3500, 5500);
    const layerSlug = safeName(layer);
    await shot(page, `dd-${layerSlug}-landed`);
    console.log('  landed:', page.url());

    const nav = await harvestSettingsNav(page);
    console.log('  nav items:', nav.length);
    fs.writeFileSync(path.join(NOTES, `${layerSlug}-nav.json`), JSON.stringify(nav, null, 2));

    const visited = await visitEachSettingsLink(page, nav, layerSlug);
    allVisited.push(...visited);
  }

  fs.writeFileSync(path.join(NOTES, 'all-settings-visited.json'), JSON.stringify(allVisited, null, 2));
  console.log('\nDONE. Total visits:', allVisited.length);
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
