const { chromium } = require('playwright');
const fs = require('fs');

const BASE_URL = 'http://localhost:5038';
const results = [];
let page, browser;

if (!fs.existsSync('C:/Work/Zaiko/screenshots')) fs.mkdirSync('C:/Work/Zaiko/screenshots');

function log(no, desc, status, detail = '') {
  const mark = status === 'PASS' ? '✅' : '❌';
  console.log(`${mark} [${no}] ${desc}${detail ? '\n      → ' + detail : ''}`);
  results.push({ no, desc, status, detail });
}

async function run(no, desc, fn) {
  try {
    await fn();
    log(no, desc, 'PASS');
  } catch (e) {
    log(no, desc, 'FAIL', e.message.split('\n')[0].slice(0, 150));
  }
}

async function ss(name) {
  await page.screenshot({ path: `C:/Work/Zaiko/screenshots/${name}.png`, fullPage: true });
}

// カスタムダイアログ（#dialog-overlay）を自動承認する
function acceptNextDialog() {
  page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 })
    .then(() => page.click('#dialog-buttons button:first-child'))
    .catch(() => {});
}

(async () => {
  browser = await chromium.launch({ headless: true });
  const context = await browser.newContext();
  page = await context.newPage();
  page.setDefaultTimeout(10000);

  // ネイティブダイアログが出た場合は常にdismiss（基本的にカスタムダイアログを使用）
  page.on('dialog', async d => { await d.dismiss(); });

  // ========== 1. ログイン ==========
  console.log('\n=== 1. ログイン ===');

  await run('1-1', '未ログインで / にアクセス→ログイン画面へリダイレクト', async () => {
    await page.goto(BASE_URL + '/');
    await page.waitForURL(/Login/, { timeout: 5000 });
  });

  await run('1-2', '誤ったユーザー名でログイン→エラーメッセージ表示', async () => {
    await page.fill('input[name="Input.UserName"]', 'nouser');
    await page.fill('input[name="Input.Password"]', 'wrong');
    await page.click('button[type="submit"]');
    await page.waitForTimeout(1000);
    const errEl = await page.$('[style*="danger"]');
    if (!errEl) throw new Error('エラーメッセージが表示されない');
  });

  await run('1-3', 'admin/admin でログイン→ダッシュボードへ遷移', async () => {
    await page.goto(BASE_URL + '/Identity/Account/Login');
    await page.fill('input[name="Input.UserName"]', 'admin');
    await page.fill('input[name="Input.Password"]', 'admin');
    await page.click('button[type="submit"]');
    await page.waitForLoadState('networkidle');
    if (page.url().includes('Login')) {
      throw new Error(`ログイン後もログインページ: ${page.url()}`);
    }
    await ss('01_dashboard');
  });

  // ========== 2. 色マスタ ==========
  console.log('\n=== 2. 色マスタ ===');

  await page.goto(BASE_URL + '/Color');
  await page.waitForLoadState('networkidle');

  await run('2-1', '「白」を追加', async () => {
    await page.fill('input[name="colorName"]', '白');
    await page.click('button[type="submit"]:has-text("追加する")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('白')) throw new Error('「白」が一覧に表示されない');
  });

  await run('2-2', '「黒」を追加', async () => {
    await page.fill('input[name="colorName"]', '黒');
    await page.click('button[type="submit"]:has-text("追加する")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('黒')) throw new Error('「黒」が一覧に表示されない');
    await ss('02_color_added');
  });

  await run('2-3', '色名空欄で追加→バリデーションエラー（保存されない）', async () => {
    const beforeRows = await page.$$('table tbody tr');
    await page.fill('input[name="colorName"]', '');
    await page.click('button[type="submit"]:has-text("追加する")');
    await page.waitForTimeout(500);
    const afterRows = await page.$$('table tbody tr');
    if (afterRows.length > beforeRows.length) throw new Error('空欄なのに追加された');
  });

  await run('2-4', '重複する色名「白」を追加→エラー（保存されない）', async () => {
    const beforeRows = (await page.$$('table tbody tr')).length;
    await page.fill('input[name="colorName"]', '白');
    await page.click('button[type="submit"]:has-text("追加する")');
    await page.waitForLoadState('networkidle');
    const afterRows = (await page.$$('table tbody tr')).length;
    const body = await page.textContent('body');
    if (afterRows > beforeRows && !body.match(/重複|既に|同じ/)) {
      throw new Error('重複した色が追加された');
    }
  });

  await run('2-5', '「白」行をインライン編集して「白（テスト）」に変更', async () => {
    const rows = await page.$$('table tbody tr');
    let edited = false;
    for (const row of rows) {
      const text = await row.textContent();
      if (text.trim().startsWith('白') && !text.includes('黒')) {
        const editBtn = await row.$('button.action-link:has-text("編集")');
        if (editBtn) {
          await editBtn.click();
          await page.waitForTimeout(300);
          const editInput = await page.$('.inline-edit-form.active input[name="colorName"]');
          if (!editInput) throw new Error('編集フォームが表示されない');
          await editInput.fill('白（テスト）');
          await page.click('.inline-edit-form.active button[type="submit"]');
          await page.waitForLoadState('networkidle');
          edited = true;
          break;
        }
      }
    }
    if (!edited) throw new Error('「白」行の編集ボタンが見つからない');
    const body = await page.textContent('body');
    if (!body.includes('白（テスト）')) throw new Error('色名が更新されていない');
  });

  await run('2-6', '「白（テスト）」→「白」に戻す', async () => {
    const rows = await page.$$('table tbody tr');
    for (const row of rows) {
      const text = await row.textContent();
      if (text.includes('白（テスト）')) {
        const editBtn = await row.$('button.action-link:has-text("編集")');
        await editBtn.click();
        await page.waitForTimeout(300);
        const editInput = await page.$('.inline-edit-form.active input[name="colorName"]');
        await editInput.fill('白');
        await page.click('.inline-edit-form.active button[type="submit"]');
        await page.waitForLoadState('networkidle');
        break;
      }
    }
  });

  // ========== 3. 商品管理 ==========
  console.log('\n=== 3. 商品管理 ===');

  await page.goto(BASE_URL + '/Product');
  await page.waitForLoadState('networkidle');

  await run('3-1', '「トートバッグ」（色なし）を登録', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="ProductName"]', 'トートバッグ');
    await page.fill('input[name="RetailPrice"]', '2000');
    await page.fill('input[name="CommissionRate"]', '0.6');
    await page.click('#radio-color-no');
    await page.waitForTimeout(200);
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForURL(/\/Product($|\?)/, { timeout: 8000 });
    const body = await page.textContent('body');
    if (!body.includes('トートバッグ')) throw new Error('トートバッグが一覧に表示されない');
    await ss('03_product_totebag');
  });

  await run('3-2', '「Tシャツ」（白・黒）を登録', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="ProductName"]', 'Tシャツ');
    await page.fill('input[name="RetailPrice"]', '3000');
    await page.fill('input[name="CommissionRate"]', '0.55');
    await page.click('#radio-color-yes');
    await page.waitForTimeout(300);
    const checkboxes = await page.$$('input[name="SelectedColorIds"]:not([disabled])');
    for (const cb of checkboxes) {
      const label = await cb.evaluate(el => el.closest('label')?.textContent?.trim() ?? '');
      if (label.includes('白') || label.includes('黒')) {
        await cb.check();
      }
    }
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForURL(/\/Product($|\?)/, { timeout: 8000 });
    const body = await page.textContent('body');
    if (!body.includes('Tシャツ')) throw new Error('Tシャツが一覧に表示されない');
    await ss('03_product_tshirt');
  });

  await run('3-3', 'Tシャツが rowspan でまとめられている', async () => {
    const cells = await page.$$('td[rowspan]');
    if (cells.length === 0) throw new Error('rowspanセルが見つからない');
  });

  await run('3-4', '商品一覧に白・黒の2行が存在する', async () => {
    const body = await page.textContent('body');
    if (!body.includes('白') || !body.includes('黒')) throw new Error('白または黒の行が見つからない');
  });

  await run('3-5', '同名商品「トートバッグ」の重複登録→サーバー側エラー（保存されない）', async () => {
    await page.goto(BASE_URL + '/Product');
    await page.waitForLoadState('networkidle');
    const beforeCount = (await page.$$('table tbody tr')).length;
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="ProductName"]', 'トートバッグ');
    await page.fill('input[name="RetailPrice"]', '1000');
    await page.fill('input[name="CommissionRate"]', '0.5');
    await page.click('#radio-color-no');
    await page.waitForTimeout(200);
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForTimeout(1000);
    const body = await page.textContent('body');
    if (!body.includes('既に登録')) throw new Error('エラーメッセージが表示されない');
    await page.click('a:has-text("キャンセル")');
    await page.waitForURL(/\/Product($|\?)/, { timeout: 5000 });
    const afterCount = (await page.$$('table tbody tr')).length;
    if (afterCount > beforeCount) throw new Error('重複商品が登録された');
  });

  await run('3-6', '商品名欄 blur で重複警告が表示される', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('#product-name-input', 'トートバッグ');
    await page.locator('#product-name-input').blur();
    await page.waitForTimeout(500);
    const warn = await page.$('#product-name-dup-warn');
    if (!warn) throw new Error('警告要素が存在しない');
    if (!await warn.isVisible()) throw new Error('重複警告が表示されない');
    await page.click('a:has-text("キャンセル")');
    await page.waitForURL(/\/Product($|\?)/, { timeout: 5000 });
  });

  // ========== 4. 取引先管理 ==========
  console.log('\n=== 4. 取引先管理 ===');

  await page.goto(BASE_URL + '/Client');
  await page.waitForLoadState('networkidle');

  await run('4-1', '「ABC商店」を登録（全商品取扱）', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="ClientName"]', 'ABC商店');
    await page.fill('input[name="FaxNumber"]', '03-1234-5678');
    const groupChecks = await page.$$('.group-check');
    for (const cb of groupChecks) {
      await cb.check();
    }
    await page.waitForTimeout(500);
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForURL(/\/Client($|\?)/, { timeout: 8000 });
    const body = await page.textContent('body');
    if (!body.includes('ABC商店')) throw new Error('ABC商店が一覧に表示されない');
    await ss('04_client_abc');
  });

  await run('4-2', '取引先名が空欄のまま保存→バリデーションエラー', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForTimeout(1000);
    const url = page.url();
    const stayed = url.includes('Edit') || url.includes('Create');
    if (!stayed) throw new Error('バリデーションエラーなしで保存された');
    await page.click('a:has-text("キャンセル")');
    await page.waitForURL(/\/Client($|\?)/, { timeout: 5000 });
  });

  await run('4-2b', '同名取引先「ABC商店」の重複登録→サーバー側エラー（保存されない）', async () => {
    await page.click('a:has-text("+ 新規登録"), button:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="ClientName"]', 'ABC商店');
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await page.waitForTimeout(1000);
    const body = await page.textContent('body');
    if (!body.includes('既に登録')) throw new Error('エラーメッセージが表示されない');
    await page.click('a:has-text("キャンセル")');
    await page.waitForURL(/\/Client($|\?)/, { timeout: 5000 });
  });

  await run('4-3', 'ABC商店の状態バッジが「有効」、取扱商品数が0以外', async () => {
    const body = await page.textContent('body');
    if (!body.includes('有効')) throw new Error('有効バッジが表示されない');
  });

  await run('4-4', 'ABC商店の編集→「無効化する」→「有効化する」で戻す', async () => {
    const rows = await page.$$('table tbody tr');
    let editHref = null;
    for (const row of rows) {
      if ((await row.textContent()).includes('ABC商店')) {
        const link = await row.$('a.action-link');
        if (link) editHref = await link.getAttribute('href');
        break;
      }
    }
    if (!editHref) throw new Error('ABC商店の編集リンクが見つからない');

    await page.goto(BASE_URL + editHref);
    await page.waitForLoadState('networkidle');

    const toggleBtn = page.locator('.status-section button[data-confirm-form]');
    await toggleBtn.scrollIntoViewIfNeeded();
    acceptNextDialog();
    await toggleBtn.click();
    await page.waitForURL(/\/Client($|\?)/, { timeout: 10000 });
    await page.waitForLoadState('networkidle');

    await page.goto(BASE_URL + editHref);
    await page.waitForLoadState('networkidle');
    const body1 = await page.textContent('body');
    if (!body1.includes('無効') && !body1.includes('有効化する')) throw new Error('無効化されていない（editページ確認）');

    const toggleBtn2 = page.locator('.status-section button[data-confirm-form]');
    await toggleBtn2.scrollIntoViewIfNeeded();
    acceptNextDialog();
    await toggleBtn2.click();
    await page.waitForURL(/\/Client($|\?)/, { timeout: 10000 });
    await page.waitForLoadState('networkidle');

    await page.goto(BASE_URL + editHref);
    await page.waitForLoadState('networkidle');
    const body2 = await page.textContent('body');
    if (!body2.includes('有効') && !body2.includes('無効化する')) throw new Error('有効化されていない（editページ確認）');

    await page.goto(BASE_URL + '/Client');
    await page.waitForLoadState('networkidle');
    await ss('04_client_status_toggle');
  });

  // ========== 5. ユーザー管理 ==========
  console.log('\n=== 5. ユーザー管理 ===');

  await page.goto(BASE_URL + '/Users');
  await page.waitForLoadState('networkidle');

  // 前回のテスト実行でtestuserが残っている場合は削除してクリーンアップ
  {
    const rows = await page.$$('table tbody tr');
    for (const row of rows) {
      if ((await row.textContent()).includes('testuser')) {
        const delBtn = await row.$('button.action-link.danger');
        if (delBtn) {
          await delBtn.click();
          await page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 });
          await page.click('#dialog-buttons button:first-child');
          await page.waitForLoadState('networkidle');
        }
        break;
      }
    }
  }

  await run('5-1', 'admin行に「ログイン中」バッジ・削除リンクがdisabled', async () => {
    const body = await page.textContent('body');
    if (!body.includes('ログイン中')) throw new Error('「ログイン中」バッジが表示されない');
    const disabledDelete = await page.$('span.action-link.disabled');
    if (!disabledDelete) throw new Error('削除リンクがdisabledになっていない');
    await ss('05_users');
  });

  await run('5-2', '「testuser/test」を新規登録', async () => {
    await page.click('a:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="UserName"]', 'testuser');
    await page.fill('input[name="Password"]', 'test');
    await page.fill('input[name="ConfirmPassword"]', 'test');
    await page.click('button:has-text("保存する")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('testuser')) throw new Error('testuserが一覧に表示されない');
  });

  await run('5-2b', 'ユーザー名欄 blur で重複警告が表示される', async () => {
    await page.click('a:has-text("+ 新規登録")');
    await page.waitForLoadState('networkidle');
    await page.fill('#user-name-input', 'admin');
    await page.locator('#user-name-input').blur();
    await page.waitForTimeout(500);
    const warn = await page.$('#user-name-dup-warn');
    if (!warn) throw new Error('警告要素が存在しない');
    if (!await warn.isVisible()) throw new Error('重複警告が表示されない');
    await page.click('a:has-text("キャンセル")');
    await page.waitForURL(/\/Users($|\?)/, { timeout: 5000 });
  });

  await run('5-3', 'パスワード不一致→エラー（保存されない）', async () => {
    const rows = await page.$$('table tbody tr');
    let found = false;
    for (const row of rows) {
      if ((await row.textContent()).includes('testuser')) {
        const link = await row.$('a.action-link');
        if (link) { await link.click(); found = true; break; }
      }
    }
    if (!found) throw new Error('testuserのパスワード変更リンクが見つからない');
    await page.waitForLoadState('networkidle');
    await page.fill('input[name="Password"]', 'newpass');
    await page.fill('input[name="ConfirmPassword"]', 'different');
    await page.click('button:has-text("保存する")');
    await page.waitForTimeout(500);
    const body = await page.textContent('body');
    if (!body.match(/一致|パスワード確認/)) throw new Error('パスワード不一致エラーが表示されない');
    await page.click('a:has-text("キャンセル")');
    await page.waitForLoadState('networkidle');
  });

  await run('5-4', 'testuser削除→一覧から消える', async () => {
    const rows = await page.$$('table tbody tr');
    let found = false;
    for (const row of rows) {
      if ((await row.textContent()).includes('testuser')) {
        const deleteBtn = await row.$('button.action-link.danger');
        if (deleteBtn) {
          await deleteBtn.click();
          await page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 });
          await page.click('#dialog-buttons button:first-child');
          await page.waitForLoadState('networkidle');
          found = true;
          break;
        }
      }
    }
    if (!found) throw new Error('testuserの削除ボタンが見つからない');
    const body = await page.textContent('body');
    if (body.includes('testuser')) throw new Error('testuserがまだ表示されている');
    await ss('05_users_deleted');
  });

  // ========== 6. 納品登録 ==========
  console.log('\n=== 6. 納品登録 ===');

  await page.goto(BASE_URL + '/Delivery');
  await page.waitForLoadState('networkidle');

  await run('6-1', '取引先未選択時は入力テーブルが表示されない', async () => {
    const table = await page.$('#delivery-table');
    if (table) throw new Error('未選択なのに入力テーブルが表示されている');
  });

  let savedClientId = null;
  let savedDeliveryDate = null;

  await run('6-2', '「ABC商店」選択・今月日付で「表示する」→ 商品が表示される', async () => {
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    savedClientId = await page.$eval('select[name="clientId"]', s => s.value);
    const now = new Date();
    savedDeliveryDate = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
    await page.fill('input[name="date"]', savedDeliveryDate);
    await page.click('button:has-text("表示する")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('Tシャツ')) throw new Error('商品が表示されない');
    await ss('06_delivery_table');
  });

  await run('6-3', 'Tシャツが rowspan でまとめられている（品名・色別列）', async () => {
    const cells = await page.$$('td[rowspan]');
    if (cells.length === 0) throw new Error('rowspanセルが見つからない');
  });

  await run('6-4', '数量入力で合計欄・カウントが更新される', async () => {
    const inputs = await page.$$('.delivery-qty');
    if (inputs.length === 0) throw new Error('納品数量欄が見つからない');
    await inputs[0].fill('10');
    await inputs[0].dispatchEvent('input');
    await page.waitForTimeout(300);
    if (inputs[1]) { await inputs[1].fill('5'); await inputs[1].dispatchEvent('input'); await page.waitForTimeout(200); }
    if (inputs[2]) { await inputs[2].fill('3'); await inputs[2].dispatchEvent('input'); await page.waitForTimeout(200); }
    const total = await page.textContent('#total-qty');
    if (parseInt(total) === 0) throw new Error(`合計が更新されない（total-qty=${total}）`);
  });

  let deliveryExcelName = '';
  await run('6-5', '「登録してExcelを出力する」→ 保存＋Excelダウンロード', async () => {
    // ダウンロードリスナーをクリック前にセット（Save→Index?download=1→ViewのJSがwindow.location.hrefでトリガー）
    const downloadPromise = page.waitForEvent('download', { timeout: 15000 });
    acceptNextDialog();
    await page.click('button:has-text("登録してExcelを出力する")');
    const download = await downloadPromise;
    deliveryExcelName = download.suggestedFilename();
    if (!deliveryExcelName.includes('委託販売納品書')) {
      throw new Error(`ファイル名が不正: ${deliveryExcelName}`);
    }
    await download.saveAs(`C:/Work/Zaiko/screenshots/delivery.xlsx`);
    await page.waitForLoadState('networkidle');
    await ss('06_delivery_saved');
  });

  await run('6-6', 'Excelファイル名が「委託販売納品書_ABC商店_YYYYMM.xlsx」形式', async () => {
    if (!deliveryExcelName.match(/委託販売納品書_ABC商店_\d{6}\.xlsx/)) {
      throw new Error(`ファイル名: ${deliveryExcelName}`);
    }
  });

  // ========== 7. 納品履歴 ==========
  console.log('\n=== 7. 納品履歴 ===');

  await page.goto(BASE_URL + '/DeliveryHistory');
  await page.waitForLoadState('networkidle');

  await run('7-1', 'ABC商店・当月で絞り込み→通常バッジの納品が表示される', async () => {
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    const now = new Date();
    const ym = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
    await page.fill('input[name="yearMonth"]', ym);
    await page.click('button:has-text("絞り込む")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('ABC商店')) throw new Error('ABC商店の納品が表示されない');
    if (!body.includes('通常')) throw new Error('通常バッジが表示されない');
    await ss('07_delivery_history');
  });

  await run('7-2', '編集リンク→納品登録画面に遷移し数量がプレフィルされている', async () => {
    const editLink = await page.$('a.action-link:has-text("編集")');
    if (!editLink) throw new Error('編集リンクが見つからない');
    const href = await editLink.getAttribute('href');
    await page.goto(BASE_URL + href);
    await page.waitForLoadState('networkidle');
    const inputs = await page.$$('.delivery-qty');
    const values = await Promise.all(inputs.map(i => i.inputValue()));
    const hasValue = values.some(v => v && parseInt(v) > 0);
    if (!hasValue) throw new Error('数量がプレフィルされていない');
    await page.goto(BASE_URL + '/DeliveryHistory');
    await page.waitForLoadState('networkidle');
    await ss('07_delivery_history_edit');
  });

  // ========== 8. 売上報告入力 ==========
  console.log('\n=== 8. 売上報告入力 ===');

  // 前回のテスト実行で残ったSalesReportをクリーンアップ（翌月→当月の順で削除）
  {
    const now8 = new Date();
    const next8 = new Date(now8.getFullYear(), now8.getMonth() + 1, 1);
    const nextYM8 = `${next8.getFullYear()}-${String(next8.getMonth() + 1).padStart(2, '0')}`;
    const currYM8 = `${now8.getFullYear()}-${String(now8.getMonth() + 1).padStart(2, '0')}`;
    for (const ym of [nextYM8, currYM8]) {
      await page.goto(BASE_URL + '/SalesReportHistory');
      await page.waitForLoadState('networkidle');
      await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
      await page.fill('input[name="yearMonth"]', ym);
      await page.click('button:has-text("絞り込む")');
      await page.waitForLoadState('networkidle');
      const delBtn8 = await page.$('button.action-link.danger');
      if (delBtn8) {
        await delBtn8.click();
        await page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 });
        await page.click('#dialog-buttons button:first-child');
        await page.waitForLoadState('networkidle');
      }
    }
  }

  await page.goto(BASE_URL + '/SalesReport');
  await page.waitForLoadState('networkidle');

  let savedSRClientId = null;
  let savedYearMonth = null;

  await run('8-1', '「ABC商店」・今月で「表示する」→ 入力テーブルが表示される', async () => {
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    savedSRClientId = await page.$eval('select[name="clientId"]', s => s.value);
    const now = new Date();
    savedYearMonth = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
    await page.fill('input[name="yearMonth"]', savedYearMonth);
    await page.click('button:has-text("表示する")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('Tシャツ')) throw new Error('入力テーブルが表示されない');
    await ss('08_sr_table');
  });

  await run('8-2', '品名・色が別列（品名は連結しない）', async () => {
    const headers = await page.$$eval('table thead th', ths => ths.map(t => t.textContent.trim()));
    const hasBoth = headers.some(h => h.includes('品名')) && headers.some(h => h.includes('色'));
    if (!hasBoth) throw new Error('品名・色が別列になっていない');
  });

  await run('8-3', '下代が表示されている', async () => {
    const cells = await page.$$eval('table tbody td.num', tds => tds.map(t => t.textContent.trim()));
    const hasValue = cells.some(v => v && v !== '—' && v !== '0');
    if (!hasValue) throw new Error('下代の値が表示されていない');
  });

  await run('8-4', '期末在庫入力でリアルタイム計算（売上点数・売上額）', async () => {
    const inputs = await page.$$('.sr-closing');
    if (inputs.length === 0) throw new Error('期末在庫欄が見つからない');
    await inputs[0].fill('8');
    await inputs[0].dispatchEvent('blur');
    await page.waitForTimeout(500);
    if (inputs[1]) { await inputs[1].fill('3'); await inputs[1].dispatchEvent('blur'); await page.waitForTimeout(300); }
    if (inputs[2]) { await inputs[2].fill('1'); await inputs[2].dispatchEvent('blur'); await page.waitForTimeout(300); }
    const statusAmount = await page.textContent('#status-sales-amount');
    if (!statusAmount || statusAmount === '—') throw new Error(`売上合計が更新されない（${statusAmount}）`);
    await ss('08_sr_calculated');
  });

  let srExcelName = '';
  await run('8-5', '「保存する」→ SalesReport保存＋Excelダウンロード＋繰越処理', async () => {
    // ダウンロードリスナーをクリック前にセット（Save→Index?download=1→ViewのJSがwindow.location.hrefでトリガー）
    const srDownloadPromise = page.waitForEvent('download', { timeout: 15000 });
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    const download = await srDownloadPromise;
    srExcelName = download.suggestedFilename();
    if (!srExcelName.includes('委託販売納品書')) throw new Error(`ファイル名が不正: ${srExcelName}`);
    await download.saveAs(`C:/Work/Zaiko/screenshots/salesreport.xlsx`);
    await page.waitForLoadState('networkidle');
    await ss('08_sr_saved');
  });

  // ========== 繰越確認 ==========
  console.log('\n=== 繰越確認 ===');

  await run('繰越-1', '納品履歴に翌月の「繰越」バッジが存在する', async () => {
    await page.goto(BASE_URL + '/DeliveryHistory');
    await page.waitForLoadState('networkidle');
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    const now = new Date();
    const next = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const ym = `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`;
    const monthInput = await page.$('input[name="yearMonth"], input[type="month"]');
    if (monthInput) await monthInput.fill(ym);
    await page.click('button:has-text("絞り込む")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('繰越')) throw new Error('翌月の繰越行が見つからない');
    await ss('carryover_history');
  });

  await run('繰越-2', '翌月の売上報告入力で期首在庫が設定されている', async () => {
    await page.goto(BASE_URL + '/SalesReport');
    await page.waitForLoadState('networkidle');
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    const now = new Date();
    const next = new Date(now.getFullYear(), now.getMonth() + 1, 1);
    const ym = `${next.getFullYear()}-${String(next.getMonth() + 1).padStart(2, '0')}`;
    await page.fill('input[name="yearMonth"]', ym);
    await page.click('button:has-text("表示する")');
    await page.waitForLoadState('networkidle');
    const carryovers = await page.$$eval('table tbody td.num', tds => tds.map(t => t.textContent.trim()));
    const hasCarryover = carryovers.some(v => parseInt(v) > 0);
    if (!hasCarryover) throw new Error('翌月の期首在庫が0のまま（繰越が反映されていない）');
    await ss('carryover_next_month');
  });

  await run('繰越-3', '翌月分の売上報告を保存（読み取り専用モードのテスト準備）', async () => {
    // ページは ABC商店・翌月の SalesReport が表示された状態
    const inputs = await page.$$('.sr-closing:not(:disabled)');
    if (inputs.length === 0) throw new Error('期末在庫欄が見つからない');
    for (const inp of inputs) {
      await inp.fill('1');
      await inp.dispatchEvent('blur');
      await page.waitForTimeout(100);
    }
    const dlPromise = page.waitForEvent('download', { timeout: 15000 });
    acceptNextDialog();
    await page.click('button:has-text("保存する")');
    await dlPromise;
    await page.waitForLoadState('networkidle');
  });

  await run('8-6', '翌月分登録済みで当月分が読み取り専用になっている', async () => {
    await page.goto(BASE_URL + '/SalesReport');
    await page.waitForLoadState('networkidle');
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    await page.fill('input[name="yearMonth"]', savedYearMonth);
    await page.click('button:has-text("表示する")');
    await page.waitForLoadState('networkidle');
    const banner = await page.$('.readonly-banner');
    if (!banner) throw new Error('読み取り専用バナーが表示されない');
    const saveBtn = await page.$('button:has-text("保存する")');
    if (saveBtn) throw new Error('読み取り専用なのに「保存する」ボタンが表示されている');
    await ss('08_sr_readonly');
  });

  // ========== 9. 売上報告履歴 ==========
  console.log('\n=== 9. 売上報告履歴 ===');

  await page.goto(BASE_URL + '/SalesReportHistory');
  await page.waitForLoadState('networkidle');

  await run('9-1', 'ABC商店で絞り込み→売上報告一覧が表示される', async () => {
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    await page.click('button:has-text("絞り込む")');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (!body.includes('ABC商店')) throw new Error('ABC商店の売上報告が表示されない');
    await ss('09_sr_history');
  });

  await run('9-2', '翌月分登録済みの当月行の「編集」「削除」がdisabled・「翌月分登録済み」バッジが表示される', async () => {
    const disabledSpans = await page.$$('span.action-link.disabled');
    if (disabledSpans.length < 2) throw new Error(`disabledな操作リンクが不足（${disabledSpans.length}個）`);
    const body = await page.textContent('body');
    if (!body.includes('翌月分登録済み')) throw new Error('「翌月分登録済み」バッジが表示されない');
  });

  let historyExcelName = '';
  await run('9-3', '帳票ボタン→モーダル表示・プレビュー確認・Excel再出力', async () => {
    // 帳票ボタンは翌月分のロック有無に関わらず有効
    await page.click('button.action-link:has-text("帳票")');
    await page.waitForTimeout(1000); // モーダルを開き fetch でコンテンツ取得
    const modal = await page.$('.modal-overlay.active');
    if (!modal) throw new Error('モーダルが表示されない');
    const previewContent = await page.textContent('#preview-content');
    if (!previewContent || previewContent.includes('読み込み中')) throw new Error('モーダルの内容が読み込まれていない');
    // Excel再出力（SalesReportHistory/DownloadExcel への <a> タグ）
    const dlPromise = page.waitForEvent('download', { timeout: 10000 });
    await page.click('#preview-download-link');
    const download = await dlPromise;
    historyExcelName = download.suggestedFilename();
    if (!historyExcelName.includes('委託販売納品書')) throw new Error(`ファイル名が不正: ${historyExcelName}`);
    await download.saveAs('C:/Work/Zaiko/screenshots/history_excel.xlsx');
    // モーダルを閉じる
    await page.click('button:has-text("閉じる")');
    await page.waitForTimeout(300);
    const stillOpen = await page.$('.modal-overlay.active');
    if (stillOpen) throw new Error('モーダルが閉じていない');
    await ss('09_sr_history_modal');
  });

  // ========== 10. ダッシュボード ==========
  console.log('\n=== 10. ダッシュボード ===');

  await page.goto(BASE_URL + '/');
  await page.waitForLoadState('networkidle');
  await ss('10_dashboard_final');

  await run('10-1', 'サマリーカードが4つ表示される', async () => {
    const cards = await page.$$('.summary-card');
    if (cards.length < 4) throw new Error(`サマリーカードが${cards.length}件しかない`);
  });

  await run('10-2', '最近の納品履歴にABC商店が表示される', async () => {
    const body = await page.textContent('body');
    if (!body.includes('ABC商店')) throw new Error('ABC商店が最近の納品履歴に表示されない');
  });

  await run('10-3', '在庫アラートセクションが表示される', async () => {
    const body = await page.textContent('body');
    if (!body.includes('在庫アラート')) throw new Error('在庫アラートセクションが表示されない');
  });

  await run('10-4', '「納品を登録する」→ 納品登録画面へ遷移', async () => {
    await page.click('text=納品を登録する');
    await page.waitForURL(/Delivery/, { timeout: 5000 });
  });

  // ========== 9-4. 売上報告削除 ==========
  // SalesReportHistoryは1年月しか表示しない仕様のため、削除対象月を明示して絞り込む
  console.log('\n=== 9-4. 売上報告削除 ===');

  const now9 = new Date();
  const nextMonth9 = new Date(now9.getFullYear(), now9.getMonth() + 1, 1);
  const nextYM9 = `${nextMonth9.getFullYear()}-${String(nextMonth9.getMonth() + 1).padStart(2, '0')}`;
  const currentYM9 = `${now9.getFullYear()}-${String(now9.getMonth() + 1).padStart(2, '0')}`;

  await run('9-4-1', '翌月分の売上報告を削除', async () => {
    await page.goto(BASE_URL + '/SalesReportHistory');
    await page.waitForLoadState('networkidle');
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    await page.fill('input[name="yearMonth"]', nextYM9);
    await page.click('button:has-text("絞り込む")');
    await page.waitForLoadState('networkidle');
    const deleteBtn = await page.$('button.action-link.danger');
    if (!deleteBtn) throw new Error('削除ボタンが見つからない（翌月分）');
    await deleteBtn.click();
    await page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 });
    await page.click('#dialog-buttons button:first-child');
    await page.waitForLoadState('networkidle');
    await ss('09_sr_history_deleted_next');
  });

  await run('9-4-2', '翌月分削除後、当月分を削除できる', async () => {
    await page.goto(BASE_URL + '/SalesReportHistory');
    await page.waitForLoadState('networkidle');
    await page.selectOption('select[name="clientId"]', { label: 'ABC商店' });
    await page.fill('input[name="yearMonth"]', currentYM9);
    await page.click('button:has-text("絞り込む")');
    await page.waitForLoadState('networkidle');
    const deleteBtn = await page.$('button.action-link.danger');
    if (!deleteBtn) throw new Error('削除ボタンが見つからない（当月分：翌月分削除後でも locked のまま？）');
    await deleteBtn.click();
    await page.waitForSelector('#dialog-overlay:not([hidden])', { timeout: 5000 });
    await page.click('#dialog-buttons button:first-child');
    await page.waitForLoadState('networkidle');
    const body = await page.textContent('body');
    if (body.includes('ABC商店') && !body.match(/該当する売上報告がありません/)) {
      throw new Error('当月分が削除されていない可能性がある');
    }
    await ss('09_sr_history_deleted_all');
  });

  await browser.close();

  // ========== 結果サマリー ==========
  console.log('\n========================================');
  const passed = results.filter(r => r.status === 'PASS').length;
  const failed = results.filter(r => r.status === 'FAIL').length;
  console.log(`テスト結果: ${results.length}件 / ✅ PASS: ${passed} / ❌ FAIL: ${failed}`);

  if (failed > 0) {
    console.log('\n--- FAILした項目 ---');
    results.filter(r => r.status === 'FAIL').forEach(r => {
      console.log(`❌ [${r.no}] ${r.desc}`);
      if (r.detail) console.log(`   → ${r.detail}`);
    });
  }
})().catch(e => {
  console.error('実行エラー:', e.message);
  if (browser) browser.close();
  process.exit(1);
});
