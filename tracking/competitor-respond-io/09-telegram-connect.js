// Phase 9 (early): Connect Telegram channel via bot token.
// Token from env var RIO_TELEGRAM_TOKEN. Never hard-code.
const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots', 'phase9-telegram');
const TOKEN = process.env.RIO_TELEGRAM_TOKEN;
const CHANNELS_URL = 'https://app.respond.io/space/408881/settings/channels/connect/all';

if (!TOKEN) { console.error('Missing RIO_TELEGRAM_TOKEN'); process.exit(1); }
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

  console.log('[1] Goto Telegram direct connect URL');
  await page.goto('https://app.respond.io/space/408881/settings/channels/connect/telegram', { waitUntil: 'domcontentloaded' });
  await human(page, 4000, 5500);
  await shot(page, '01-telegram-connect-page');
  console.log('  url:', page.url());
  // If redirected to catalog, fall back to catalog click flow
  if (!page.url().includes('telegram')) {
    console.log('[1b] Fallback: catalog click flow');
    await page.goto(CHANNELS_URL, { waitUntil: 'domcontentloaded' });
    await human(page, 4000, 5500);
  // Telegram is in Business Messaging category. Look for card containing 'Telegram' + 'Connect'.
  const tgCard = page.locator('div:has(> :text-is("Telegram"))').first();
  // Fallback: find a button:near('Telegram') - try scoped approach
  const connectInTgCard = page.locator('div:has(:text("Telegram"))').filter({
    hasText: 'Connect',
  }).locator('button:has-text("Connect"), [role="button"]:has-text("Connect")').first();
  if (await connectInTgCard.count()) {
    await connectInTgCard.click();
  } else {
    // Alternative: iterate all cards with "Connect" text, find the one whose ancestor has "Telegram"
    const clicked = await page.evaluate(() => {
      const buttons = Array.from(document.querySelectorAll('button, [role="button"]'));
      for (const b of buttons) {
        const txt = (b.innerText || '').trim();
        if (txt !== 'Connect') continue;
        // Walk up to find a container mentioning Telegram
        let p = b.parentElement;
        for (let i = 0; i < 6 && p; i++, p = p.parentElement) {
          if ((p.innerText || '').toLowerCase().includes('telegram')) {
            b.click();
            return true;
          }
        }
      }
      return false;
    });
    if (!clicked) {
      console.log('  ! Could not find Telegram Connect button. Dumping DOM.');
      const html = await page.content();
      fs.writeFileSync(path.join(__dirname, 'notes', 'channels-page.html'), html);
      await shot(page, '02-no-telegram-button');
      await ctx.close();
      process.exit(2);
    }
  }
  }
  await human(page, 3500, 5000);
  await shot(page, '03-after-click-telegram');
  console.log('  url:', page.url());

  // On Telegram connect page — click "Connect an existing bot"
  console.log('[2b] Click "Connect an existing bot"');
  const existingBtn = page.locator('button:has-text("Connect an existing bot"), a:has-text("Connect an existing bot"), [role="button"]:has-text("Connect an existing bot")').first();
  if (await existingBtn.count()) {
    await existingBtn.click();
    await human(page, 3500, 5000);
    await shot(page, '03b-after-existing-clicked');
  } else {
    console.log('  ! "Connect an existing bot" not found');
  }

  // Expect a modal/page asking for bot token
  console.log('[3] Find bot token input');
  // Try input[name*=token] or placeholder
  const tokenInput = page.locator(
    'input[name*="token" i], input[placeholder*="token" i], input[type="text"], input[type="password"]'
  ).first();
  await tokenInput.waitFor({ state: 'visible', timeout: 15000 }).catch(() => {});
  await shot(page, '04-token-prompt');

  if (!(await tokenInput.count())) {
    console.log('  ! no input visible, dumping DOM');
    fs.writeFileSync(path.join(__dirname, 'notes', 'tg-prompt.html'), await page.content());
    await ctx.close();
    process.exit(3);
  }
  console.log('[4] Type token with human delay');
  await tokenInput.click();
  await human(page, 600, 1200);
  await tokenInput.type(TOKEN, { delay: 60 });
  await human(page, 1500, 2500);
  await shot(page, '05-token-typed');

  // Click connect / next / submit button
  const submitBtn = page.locator('button:has-text("Connect"), button:has-text("Next"), button:has-text("Submit"), button[type="submit"]').first();
  await submitBtn.click();
  console.log('[5] Submitted, waiting for outcome');
  await human(page, 6000, 9000);
  await shot(page, '06-after-submit');
  console.log('  url:', page.url());

  // Capture any success/error indicators
  const body = await page.locator('body').innerText().catch(() => '');
  const snippet = body.split('\n').filter(l => l.trim()).slice(0, 30).join(' | ');
  console.log('  page text excerpt:', snippet.slice(0, 800));

  // Wait more and re-screenshot in case a modal appears
  await human(page, 4000, 5500);
  await shot(page, '07-final');

  console.log('DONE.');
  await human(page, 3000, 4500);
  await ctx.close();
})().catch(err => { console.error('ERROR:', err.message); process.exit(1); });
