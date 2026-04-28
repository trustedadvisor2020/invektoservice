// Phase 11: Thorough sweep - catch everything missed. Slow, exhaustive.
// Sections: AI Assist button, Summarize, right panel icons expanded, help bubble,
// notification bell, Personal settings (profile/notifications), Reports all 11 sub-tabs,
// onboarding checklist modal, workflow trigger dropdown (retry).

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase11-sweep');
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
async function tryClose(page) {
  for (const sel of ['button[aria-label="Close"]', 'button:has-text("Close")', 'button:has-text("Got it")', 'button:has-text("Skip")']) {
    const el = page.locator(sel).first();
    if (await el.count().catch(() => 0)) {
      try { await el.click({ timeout: 2000 }); return true; } catch {}
    }
  }
  await page.keyboard.press('Escape').catch(() => {});
  return false;
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

  // --- A. Open Inbox conversation + AI Assist button + Summarize
  console.log('[A] Inbox - AI Assist + Summarize');
  await page.goto('https://app.respond.io/space/408881/inbox', { waitUntil: 'domcontentloaded' });
  await human(page, 4500, 6000);
  await tryClose(page);
  await human(page, 2000, 3000);

  // Click All filter + open conv
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
  await tryClose(page);
  await shot(page, 'A-01-conv');

  // Click AI Assist (top-right of compose)
  console.log('  - Click AI Assist top-right');
  const aiClicked = await page.evaluate(() => {
    // AI Assist text button in right part of compose area (top ~760, left ~1300)
    const btns = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of btns) {
      const txt = (b.innerText || '').trim();
      const r = b.getBoundingClientRect();
      if (/^AI Assist$/i.test(txt) || (/ai assist/i.test(txt) && r.top > 700 && r.top < 800)) {
        b.click();
        return { text: txt, rect: r };
      }
    }
    return null;
  });
  console.log('    AI Assist:', JSON.stringify(aiClicked));
  await human(page, 4000, 5500);
  await shot(page, 'A-02-ai-assist-clicked');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1800, 2800);

  // Click Summarize
  console.log('  - Click Summarize');
  const sumClicked = await page.evaluate(() => {
    const btns = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of btns) {
      const txt = (b.innerText || '').trim();
      if (/^Summarize$/i.test(txt)) {
        b.click();
        return { text: txt, rect: b.getBoundingClientRect() };
      }
    }
    return null;
  });
  console.log('    Summarize:', JSON.stringify(sumClicked));
  await human(page, 5000, 7000);
  await shot(page, 'A-03-summarize-output');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1800, 2800);

  // Right panel icons - click each (column on far right, x > 1380)
  console.log('  - Right panel icons');
  const rpIcons = await page.evaluate(() => {
    return Array.from(document.querySelectorAll('button, [role="button"]'))
      .filter(b => {
        const r = b.getBoundingClientRect();
        return r.left > 1380 && r.top > 100 && r.top < 500 && r.width > 16 && r.width < 60;
      })
      .map((b, i) => ({ idx: i, rect: b.getBoundingClientRect(), label: b.getAttribute('aria-label') || '' }));
  });
  console.log('    RP icons:', rpIcons.length);
  for (let i = 0; i < rpIcons.length; i++) {
    const ic = rpIcons[i];
    try {
      await page.mouse.click(ic.rect.x + ic.rect.width/2, ic.rect.y + ic.rect.height/2);
      await human(page, 2500, 3800);
      await shot(page, `A-04-rp-icon-${i}-${(ic.label || 'x').replace(/[^a-z0-9]/gi, '-').slice(0, 20).toLowerCase()}`);
    } catch {}
  }

  // --- B. Help chat bubble (bottom right)
  console.log('[B] Help chat bubble');
  const helpBubble = await page.evaluate(() => {
    const btns = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of btns) {
      const r = b.getBoundingClientRect();
      if (r.left > 1350 && r.top > 700 && r.width > 30 && r.width < 80) {
        b.click();
        return { rect: r };
      }
    }
    return null;
  });
  console.log('  help bubble:', JSON.stringify(helpBubble));
  await human(page, 4500, 6500);
  await shot(page, 'B-01-help-chat');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1800, 2800);

  // --- C. Notification bell
  console.log('[C] Notification bell');
  // Bell icon in left sidebar bottom area
  await page.evaluate(() => {
    const btns = Array.from(document.querySelectorAll('button, [role="button"]'));
    for (const b of btns) {
      const r = b.getBoundingClientRect();
      if (r.left < 50 && r.top > 700 && r.width > 15 && r.width < 60) {
        const html = (b.outerHTML || '').toLowerCase();
        if (/bell|notif/i.test(html)) { b.click(); return true; }
      }
    }
  });
  await human(page, 3500, 5000);
  await shot(page, 'C-01-notifications');
  await page.keyboard.press('Escape').catch(() => {});
  await human(page, 1800, 2800);

  // --- D. Onboarding checklist modal
  console.log('[D] Onboarding checklist');
  await page.goto('https://app.respond.io/space/408881/dashboard', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  const ocBtn = page.locator('button:has-text("Onboarding checklist")').first();
  if (await ocBtn.count()) {
    await ocBtn.click();
    await human(page, 3500, 5000);
    await shot(page, 'D-01-onboarding-modal');
    // Expand each of the 4 accordion items
    const accordionTexts = ['Learn how Lifecycle', 'Set up AI Agents', 'Invite teammates'];
    for (const txt of accordionTexts) {
      const loc = page.getByText(txt, { exact: false }).first();
      if (await loc.count().catch(() => 0)) {
        try {
          await loc.click({ timeout: 2500 });
          await human(page, 2500, 3500);
          await shot(page, `D-02-accordion-${txt.replace(/[^a-z0-9]/gi, '-').slice(0, 25).toLowerCase()}`);
        } catch {}
      }
    }
  }

  // --- E. Gear dropdown - visit Personal + Organization settings
  console.log('[E] Personal + Organization settings');
  await page.goto('https://app.respond.io/space/408881/dashboard', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);

  // Open gear
  await page.evaluate(() => {
    const candidates = Array.from(document.querySelectorAll('aside button, nav button'));
    const scored = candidates.map(el => {
      const rect = el.getBoundingClientRect();
      if (rect.left > 60) return null;
      const attrs = Array.from(el.attributes || []).map(a => `${a.name}=${a.value}`).join(' ');
      const html = el.outerHTML || '';
      const s = /gear|setting|cog|dropdown-menu-trigger/i.test(attrs + ' ' + html) ? 100 : 0;
      return { el, rect, s };
    }).filter(Boolean);
    scored.sort((a, b) => b.s - a.s || b.rect.top - a.rect.top);
    if (scored.length) scored[0].el.click();
  });
  await human(page, 2500, 3800);
  await shot(page, 'E-01-gear-dropdown');

  // Click "Personal settings" - use Playwright text click
  let clicked = false;
  try {
    await page.getByText('Personal settings', { exact: false }).first().click({ timeout: 4000 });
    clicked = true;
  } catch {}
  if (clicked) {
    await human(page, 4000, 5500);
    await shot(page, 'E-02-personal-settings');
    // Scroll through it
    for (let i = 0; i < 3; i++) {
      await page.mouse.move(720, 450);
      await page.mouse.wheel(0, 500);
      await human(page, 2000, 3000);
      await shot(page, `E-03-personal-scroll-${i}`);
    }
  }

  // Back to dashboard, open gear, click Organization settings
  await page.goto('https://app.respond.io/space/408881/dashboard', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  await page.evaluate(() => {
    const cs = Array.from(document.querySelectorAll('aside button, nav button'));
    const s = cs.map(el => {
      const r = el.getBoundingClientRect();
      if (r.left > 60) return null;
      const html = el.outerHTML || '';
      const v = /gear|setting|cog|dropdown-menu-trigger/i.test(html) ? 100 : 0;
      return { el, r, v };
    }).filter(Boolean);
    s.sort((a, b) => b.v - a.v || b.r.top - a.r.top);
    if (s.length) s[0].el.click();
  });
  await human(page, 2500, 3500);
  try {
    await page.getByText('Organization settings', { exact: false }).first().click({ timeout: 4000 });
    await human(page, 4000, 5500);
    await shot(page, 'E-04-organization-settings');
    // Harvest org settings nav
    const orgNav = await page.evaluate(() => {
      return Array.from(document.querySelectorAll('a'))
        .filter(a => {
          const r = a.getBoundingClientRect();
          return r.left < 260 && r.left > 40 && r.width > 50 && r.height > 15;
        })
        .map(a => ({ text: (a.innerText || '').trim(), href: a.getAttribute('href') }))
        .filter(x => x.text && x.text.length < 40);
    });
    fs.writeFileSync(path.join(NOTES, 'org-settings-nav.json'), JSON.stringify(orgNav, null, 2));
    console.log('  org nav items:', orgNav.length);
    for (let i = 0; i < 4; i++) {
      await page.mouse.wheel(0, 400);
      await human(page, 1800, 2800);
      await shot(page, `E-05-org-scroll-${i}`);
    }
  } catch (e) { console.log('  org err:', e.message); }

  // --- F. Reports - click each of 11 sub-tabs
  console.log('[F] Reports sub-tabs');
  await page.goto('https://app.respond.io/space/408881/reports', { waitUntil: 'domcontentloaded' });
  await human(page, 3500, 5000);
  const reportTabs = ['Lifecycle', 'Calls', 'Conversations', 'Responses', 'Resolutions', 'Messages', 'Contacts', 'Assignments', 'Leaderboard', 'Users', 'Broadcasts'];
  for (const tab of reportTabs) {
    try {
      const loc = page.getByText(tab, { exact: true }).first();
      if (await loc.count().catch(() => 0)) {
        await loc.click({ timeout: 3000 });
        await human(page, 3000, 4500);
        const slug = tab.toLowerCase();
        await shot(page, `F-01-report-${slug}`);
      }
    } catch {}
  }

  console.log('DONE.');
  await human(page, 2500, 3500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
