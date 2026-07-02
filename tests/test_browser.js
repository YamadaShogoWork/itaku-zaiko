const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  // ログイン
  await page.goto('http://localhost:5050/Identity/Account/Login', { waitUntil: 'domcontentloaded', timeout: 15000 });
  const inputs = await page.$$eval('input', els => els.map(e => ({ name: e.name, type: e.type })));
  console.log('Inputs:', JSON.stringify(inputs));

  await page.fill('input[name="Input.UserName"]', 'admin');
  await page.fill('input[name="Input.Password"]', 'admin');
  await page.click('button[type="submit"]');
  await page.waitForLoadState('networkidle');

  console.log('ログイン後URL:', page.url());
  console.log('Title:', await page.title());
  await page.screenshot({ path: 'screenshot_02_dashboard.png', fullPage: true });
  console.log('Screenshot saved: screenshot_02_dashboard.png');

  await browser.close();
})();
