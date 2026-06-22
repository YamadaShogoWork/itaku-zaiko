# 商品一覧画面 仕様

対応モックアップ: [docs/mockups/product.html](../mockups/product.html)

## 1. 画面概要

登録済みの商品（`Product`）を一覧表示し、新規登録・編集・削除を行う画面。
登録・編集は [商品編集画面](product-edit.md) で行う。

---

## 2. 一覧

### 2.1 列定義

| 列 | データソース / 算出方法 |
|----|------|
| 品名 | `Product.ProductName` |
| 色 | `Color.ColorName`。`Product.ColorId`がnullの場合は「—」表示 |
| 上代 | `Product.RetailPrice` |
| 掛け率 | `Product.CommissionRate`（標準値） |
| 操作 | 編集・削除リンク（2.3参照） |

並び順: `Product.ProductName` でグルーピングし、品名列はrowspanでまとめる（[client-edit.md](client-edit.md) 4.1と同様の方式）。グループ内は `ColorId` 昇順でソート。グループ自体の表示順は `ProductId` 昇順（登録順）。

サイズ（`Size`マスタ）はテーブルとしてDB設計に残すが、現状未使用のため一覧には列を設けない。

### 2.2 ボタン動作（一覧上部）

| ボタン | 動作 |
|--------|------|
| + 新規登録 | [商品編集画面](product-edit.md) を新規登録モードで開く |

### 2.3 行ごとの操作

| 操作 | 対象 | 動作 |
|------|------|------|
| 編集 | 品名グループ単位（rowspan先頭行のみ表示） | [商品編集画面](product-edit.md) に該当グループ（同一`ProductName`を持つ`Product`群）を指定して遷移し、既存データをプレフィルする |
| 削除 | 行（`Product`1件）単位 | 確認ダイアログを表示し、OKなら3章の判定に従い削除する |

---

## 3. 削除処理

`Client`の`IsActive`のような無効化フラグは`Product`には設けない。「削除」操作時、対象`Product`について以下を判定する。

| 判定 | 条件 | 動作 |
|------|------|------|
| 削除可 | 該当`ProductId`を参照する`Delivery`・`SalesReport`・`ClientProduct`のいずれも存在しない | `Product`レコードを削除する |
| 削除不可 | 上記のいずれかが1件以上存在する | 削除せず、エラーメッセージ「この商品は使用されているため削除できません」を表示する |

---

## 4. 確定事項

- `Product`は無効化フラグを持たず、関連データがあれば削除をブロックする方針。
- 削除不可時のエラーメッセージ: 「この商品は使用されているため削除できません」（[color.md](color.md) 5章と統一）
