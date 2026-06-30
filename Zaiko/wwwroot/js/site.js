const Dialog = (function () {
  var ICONS = {
    confirm: '<path d="M9 12l2 2 4-4"/><circle cx="12" cy="12" r="9"/>',
    danger:  '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/>',
    success: '<polyline points="20 6 9 17 4 12"/>',
    warning: '<path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
    error:   '<circle cx="12" cy="12" r="9"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/>'
  };

  function el(id) { return document.getElementById(id); }

  function show(type, title, message, buttons) {
    el('dialog-icon-wrap').className = 'dialog-icon-wrap ' + type;
    el('dialog-icon-svg').innerHTML = ICONS[type] || ICONS.confirm;
    el('dialog-title').textContent = title;
    var msgEl = el('dialog-message');
    msgEl.textContent = message;
    msgEl.style.display = message ? '' : 'none';

    var btnsEl = el('dialog-buttons');
    btnsEl.innerHTML = '';
    buttons.forEach(function (b) {
      var btn = document.createElement('button');
      btn.className = 'btn ' + (b.cls || 'btn-secondary');
      btn.textContent = b.text;
      btn.addEventListener('click', function () { hide(); if (b.fn) b.fn(); });
      btnsEl.appendChild(btn);
    });

    el('dialog-overlay').removeAttribute('hidden');
  }

  function hide() {
    el('dialog-overlay').setAttribute('hidden', '');
  }

  // オーバーレイ背景クリックで閉じる
  document.addEventListener('click', function (e) {
    if (e.target.id === 'dialog-overlay') hide();
  });

  // data-confirm-title を持つフォームの送信をインターセプト
  document.addEventListener('submit', function (e) {
    var form = e.target;
    if (!form.dataset.confirmTitle) return;
    e.preventDefault();
    var type = form.dataset.confirmType || 'danger';
    show(type, form.dataset.confirmTitle, form.dataset.confirmMessage || '', [
      { text: form.dataset.confirmOk || 'はい', cls: type === 'danger' ? 'btn-danger' : 'btn-primary', fn: function () { form.submit(); } },
      { text: 'キャンセル', cls: 'btn-secondary' }
    ]);
  });

  // data-confirm-form を持つボタンのクリックをインターセプト
  document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-confirm-form]');
    if (!btn) return;
    e.preventDefault();
    var form = document.getElementById(btn.dataset.confirmForm);
    if (!form) return;
    var type = btn.dataset.confirmType || 'confirm';
    show(type, btn.dataset.confirmTitle || 'よろしいですか？', btn.dataset.confirmMessage || '', [
      { text: btn.dataset.confirmOk || 'はい', cls: type === 'danger' ? 'btn-danger' : 'btn-primary', fn: function () { form.submit(); } },
      { text: 'キャンセル', cls: 'btn-secondary' }
    ]);
  });

  return {
    confirm: function (opts) {
      opts = opts || {};
      var type = opts.type || 'confirm';
      show(type, opts.title || 'よろしいですか？', opts.message || '', [
        { text: opts.confirmText || 'はい', cls: type === 'danger' ? 'btn-danger' : 'btn-primary', fn: opts.onConfirm },
        { text: opts.cancelText || 'キャンセル', cls: 'btn-secondary', fn: opts.onCancel }
      ]);
    },
    alert: function (opts) {
      opts = opts || {};
      show(opts.type || 'success', opts.title || '', opts.message || '', [
        { text: opts.closeText || 'OK', cls: 'btn-primary', fn: opts.onClose }
      ]);
    }
  };
}());

// スピナー入力（−/＋ボタン）
document.addEventListener('click', function(e) {
  var btn = e.target.closest('.spinner-btn');
  if (!btn || btn.disabled) return;
  var group = btn.closest('.spinner-group, .spinner-group-sm');
  if (!group) return;
  var inp = group.querySelector('.spinner-input');
  if (!inp || inp.disabled) return;
  var delta = parseFloat(btn.dataset.delta) || 0;
  var decimals = parseInt(inp.dataset.decimals);
  decimals = isNaN(decimals) ? 0 : decimals;
  var current = parseFloat(inp.value) || 0;
  var next = Math.round((current + delta) * 1000) / 1000;
  var minVal = inp.min !== '' ? parseFloat(inp.min) : null;
  if (minVal !== null && next < minVal) next = minVal;
  inp.value = decimals > 0 ? next.toFixed(decimals) : String(Math.round(next));
  inp.dispatchEvent(new Event('input', { bubbles: true }));
  inp.dispatchEvent(new Event('change', { bubbles: true }));
});
