// Phase 1: Login to respond.io via Google SSO, land on app.
// Credentials come from env vars RIO_EMAIL, RIO_PASSWORD. Never log them.
// Profile persisted in c:/tmp/respondio-profile so later phases reuse session.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const EMAIL = process.env.RIO_EMAIL;
const PASSWORD = process.env.RIO_PASSWORD;
const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots');
const ONBOARDING_URL = 'https://app.respond.io/space/408881/onboarding';

if (!EMAIL || !PASSWORD) {
  console.error('Missing RIO_EMAIL or RIO_PASSWORD env vars.');
  process.exit(1);
}
fs.mkdirSync(SHOTS, { recursive: true });

function ts() { return new Date().toISOString().replace(/[:.]/g, '-'); }
async function shot(page, name) {
  const p = path.join(SHOTS, `${ts()}__${name}.png`);
  await page.screenshot({ path: p, fullPage: true }).catch(() => {});
  console.log('  shot:', path.basename(p));
}

(async () => {
  console.log('[1/6] Launching Chrome with persistent profile:', PROFILE_DIR);
  const ctx = await chromium.launchPersistentContext(PROFILE_DIR, {
    channel: 'chrome',
    headless: false,
    viewport: { width: 1440, height: 900 },
    args: [
      '--disable-blink-features=AutomationControlled',
      '--disable-features=IsolateOrigins,site-per-process',
    ],
    ignoreDefaultArgs: ['--enable-automation'],
  });

  // Stealth: remove webdriver flag
  await ctx.addInitScript(() => {
    Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
  });

  const page = ctx.pages()[0] || await ctx.newPage();
  page.setDefaultTimeout(30000);

  console.log('[2/6] Navigating to respond.io onboarding');
  await page.goto(ONBOARDING_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  await shot(page, '01-initial-landing');
  const url1 = page.url();
  console.log('  landed URL:', url1);

  const isLoggedIn = url1.includes('app.respond.io') && !url1.includes('login');
  if (isLoggedIn) {
    console.log('[3/6] Already logged in, session reused.');
    await shot(page, '02-already-logged-in');
    console.log('DONE. Session persisted. Run subsequent phases without re-login.');
    await ctx.close();
    return;
  }

  console.log('[3/6] Login page detected. Locating Google SSO button.');
  // Common respond.io login flow: button "Continue with Google" or similar
  // Try several selectors
  const googleSelectors = [
    'button:has-text("Continue with Google")',
    'button:has-text("Sign in with Google")',
    'a:has-text("Continue with Google")',
    'a:has-text("Sign in with Google")',
    '[data-provider="google"]',
    'button:has(img[alt*="Google" i])',
  ];
  let clicked = false;
  for (const sel of googleSelectors) {
    const el = page.locator(sel).first();
    if (await el.count() > 0) {
      console.log('  clicking:', sel);
      await el.click();
      clicked = true;
      break;
    }
  }
  if (!clicked) {
    console.log('  Google SSO button not found. Dumping page content for diagnosis.');
    await shot(page, '02-login-page-no-google-button');
    const html = await page.content();
    fs.writeFileSync(path.join(SHOTS, 'login-page.html'), html);
    await ctx.close();
    process.exit(2);
  }

  console.log('[4/6] Waiting for Google login screen');
  await page.waitForURL(/accounts\.google\.com/, { timeout: 20000 }).catch(() => {});
  await page.waitForTimeout(2000);
  await shot(page, '03-google-email-screen');

  // Enter email
  const emailInput = page.locator('input[type="email"]').first();
  await emailInput.waitFor({ timeout: 15000 });
  await emailInput.fill(EMAIL);
  await shot(page, '04-email-filled');
  await page.keyboard.press('Enter');
  await page.waitForTimeout(3000);
  await shot(page, '05-after-email-next');

  // Check for "browser not secure" block
  const bodyText = await page.locator('body').innerText().catch(() => '');
  if (/couldn.t sign you in|not secure|try again later|browser or app may not be secure/i.test(bodyText)) {
    console.log('  ! Google blocked automated sign-in. Fallback needed.');
    await shot(page, '06-google-blocked');
    console.log('\nFALLBACK: Open this profile in a normal Chrome:');
    console.log('  chrome.exe --user-data-dir=' + PROFILE_DIR);
    console.log('Log in manually once. Session will persist for later scripts.');
    await ctx.close();
    process.exit(3);
  }

  // Enter password
  console.log('[5/6] Entering password');
  const passInput = page.locator('input[type="password"]').first();
  await passInput.waitFor({ timeout: 15000 });
  await passInput.fill(PASSWORD);
  await shot(page, '07-password-filled');
  await page.keyboard.press('Enter');

  console.log('[6/6] Waiting for redirect back to respond.io');
  await page.waitForURL(/app\.respond\.io/, { timeout: 30000 }).catch(() => {});
  await page.waitForTimeout(5000);
  await shot(page, '08-after-login');
  console.log('  final URL:', page.url());

  if (page.url().includes('app.respond.io')) {
    console.log('\n=== LOGIN SUCCESS ===');
    console.log('Session saved to:', PROFILE_DIR);
  } else {
    console.log('\n=== LOGIN INCOMPLETE ===');
    console.log('Current URL:', page.url());
    console.log('Check screenshots in:', SHOTS);
  }

  // Keep browser open 10s so Q can see
  await page.waitForTimeout(5000);
  await ctx.close();
})().catch(err => {
  console.error('ERROR:', err.message);
  process.exit(1);
});
