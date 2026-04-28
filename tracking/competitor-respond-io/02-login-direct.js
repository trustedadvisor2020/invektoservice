// Phase 1b: Direct email/password login to respond.io (bypass Google SSO).
// Credentials via env vars RIO_EMAIL, RIO_PASSWORD.

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const EMAIL = process.env.RIO_EMAIL;
const PASSWORD = process.env.RIO_PASSWORD;
const PROFILE_DIR = process.env.RIO_PROFILE_DIR || 'c:/tmp/respondio-profile';
const SHOTS = path.join(__dirname, 'screenshots');
const LOGIN_URL = 'https://app.respond.io/user/login';
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
  console.log('[1/5] Launching Chrome, profile:', PROFILE_DIR);
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
  page.setDefaultTimeout(30000);

  console.log('[2/5] Goto login page');
  await page.goto(LOGIN_URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2000);

  // Already signed in? Check URL.
  if (!page.url().includes('/login')) {
    console.log('Already signed in:', page.url());
    await shot(page, 'direct-already-in');
    await ctx.close();
    return;
  }

  console.log('[3/5] Filling email + password');
  const emailInput = page.locator('input[placeholder="Email address" i], input[type="email"]').first();
  const passInput = page.locator('input[placeholder="Password" i], input[type="password"]').first();
  await emailInput.fill(EMAIL);
  await passInput.fill(PASSWORD);
  await shot(page, 'direct-01-filled');

  const signInBtn = page.locator('button:has-text("Sign in")').first();
  await signInBtn.click();

  console.log('[4/5] Waiting for redirect or error (up to 20s)');
  // Poll URL change or visible error banner
  const start = Date.now();
  while (Date.now() - start < 20000) {
    await page.waitForTimeout(1000);
    if (!page.url().includes('/login')) break;
    const body = await page.locator('body').innerText().catch(() => '');
    if (/(invalid|incorrect|wrong|not found|locked|failed|verify)/i.test(body)) break;
  }
  await shot(page, 'direct-02-after-submit');

  const url = page.url();
  console.log('  URL after wait:', url);
  const body = await page.locator('body').innerText().catch(() => '');
  const errMatch = body.match(/(invalid|incorrect|wrong|not found|locked|failed|verify)[^\n]{0,120}/i);
  if (errMatch) {
    console.log('  error text on page:', errMatch[0]);
  } else {
    // Print visible text excerpt for diagnosis
    const snippet = body.split('\n').filter(l => l.trim()).slice(0, 20).join(' | ');
    console.log('  visible text:', snippet);
  }

  if (!url.includes('/login')) {
    console.log('\n=== LOGIN SUCCESS (direct email/pass) ===');
    console.log('Current URL:', url);
    await page.goto(ONBOARDING_URL).catch(() => {});
    await page.waitForTimeout(3000);
    await shot(page, 'direct-03-onboarding');
  } else {
    console.log('\n=== LOGIN FAILED ===');
    console.log('Still on login page. See screenshots.');
  }

  console.log('[5/5] Keeping browser open 8s for visual review');
  await page.waitForTimeout(8000);
  await ctx.close();
})().catch(err => {
  console.error('ERROR:', err.message);
  process.exit(1);
});
