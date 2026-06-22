# 取引先編集画面 仕様

対応モックアップ: [docs/mockups/client-edit.html](../mockups/client-edit.html)

## 1. 画面概要

取引先（`Client`）の基本情報と、取扱商品・掛け率（`ClientProduct`）を1画面で登録・編集する画面。
[取引先一覧画面](client.md)から「新規登録」または「編集」で遷移する。

---

## 2. 基本情報

| 項目 | 種別 | 内容 |
|------|------|------|
| 取引先名 | テキスト入力 | `Client.ClientName`。必須 |
| FAX番号 | テキスト入力 | `Client.FaxNumber`。任意、書式チェックなし |

---

## 3. 状態（有効/無効）

- 編集時のみ表示する（新規登録時は非表示。新規作成時は常に `IsActive=true`）。
- 現在の状態をバッジ表示（`IsActive=true`→「有効」`badge-success`、`false`→「無効」`badge`）。
- バッジの隣に切り替えボタンを表示する。
  - `IsActive=true`の場合：「無効にする」ボタン → 確認ダイアログ表示 → OKなら即時に`Client.IsActive=false`へ更新（保存するボタンとは独立した操作）
  - `IsActive=false`の場合：「有効にする」ボタン → 確認ダイアログなしで即時に`Client.IsActive=true`へ更新
- 切り替え後はボタン・バッジの表示をその場で更新する（画面遷移なし）。
- 補足文言（4章テーブル下部の注記と同様の趣旨）を表示: 「関連する納品・売上データが存在する取引先は一覧画面から物理削除できないため、削除の代わりに無効へ切り替えてください。無効な取引先は納品登録・売上報告入力画面の選択肢から除外されます。」

---

## 4. 取扱商品・掛け率

### 4.1 表示対象・並び順

- `Product` の全件を表示対象とする（この取引先が現在取り扱っているかどうかに関わらず、マスタに登録済みの全商品を表示）。
- `Product` を `Color`（`Product.ColorId`でJOIN、null可）と結合する。
- `Product.ProductName` でグルーピングし、同一品名内は `ColorId` 昇順でソート（rowspanでまとめる）。グループ自体の表示順は `ProductId` 昇順（登録順）。

### 4.2 既存データのプレフィル

- 編集時、対象`Client`の`ClientProduct`に登録済みの`ProductId`については、「取扱」チェックボックスをON、「掛け率」欄に`ClientProduct.CommissionRate`をプレフィルする。
- `ClientProduct`未登録の商品は、チェックボックスOFF、「掛け率」欄に`Product.CommissionRate`（標準値）を表示するが編集不可（`disabled`）とする。
- 新規登録時は全商品チェックボックスOFF、「掛け率」欄は全て`Product.CommissionRate`を表示（`disabled`）。

### 4.3 列定義

| 列 | データソース / 算出方法 |
|----|------|
| 取扱 | チェックボックス。ONの場合、この商品を`ClientProduct`として登録する |
| 品名 | `Product.ProductName` |
| 色 | `Color.ColorName`。`Product.ColorId`がnullの場合は「—」表示 |
| 上代 | `Product.RetailPrice` |
| 掛け率 | 入力欄（数値、`step=0.01`）。「取扱」がONの行のみ編集可。初期値は4.2参照 |
| 下代（参考） | `上代 × 掛け率` の計算値。「取扱」がOFFの行は「—」表示 |

### 4.4 リアルタイム計算

JavaScriptにより以下を再計算・更新する。

- 「取扱」チェックボックスのON/OFF切り替え時:
  - 掛け率欄の有効/無効（`disabled`）を切り替える
  - OFF→ONにした場合、掛け率欄が初期状態（プレフィル値）のままなら`Product.CommissionRate`を初期値として設定する
  - OFF時は「下代（参考）」を「—」表示にする
- 掛け率欄の入力（input）時: 「下代（参考）」を `上代 × 掛け率` で再計算する（小数の丸めは行わない。[sales-report.md](sales-report.md)と同じ方針）

---

## 5. ボタン動作

| ボタン | 動作 |
|--------|------|
| 保存する | 確認ダイアログを表示し、OKなら6章の入力チェックを行い、7章の保存処理を実行。完了後トーストで通知し、[取引先一覧画面](client.md)へ遷移する |
| キャンセル | 確認なしで[取引先一覧画面](client.md)へ遷移する（未保存の変更は破棄） |
| 無効にする / 有効にする | 3章参照 |

---

## 6. 入力チェック

- 取引先名: 必須（未入力はエラー）
- 掛け率: 「取扱」がONの行について、0以上の数値を必須とする（空欄はエラー）

---

## 7. 保存処理

### 7.1 基本情報

- `Client.ClientName`、`Client.FaxNumber` を更新する（新規登録時は `Client` を新規作成し、`IsActive=true` とする）。

### 7.2 取扱商品（ClientProduct）

表示中の全商品行について、以下のルールで`ClientProduct`（`ClientId`・`ProductId`）をUPSERT/削除する。

| 「取扱」チェック | 既存`ClientProduct` | 動作 |
|---|---|---|
| ON | あり | `CommissionRate` を入力値で更新 |
| ON | なし | 新規作成（`CommissionRate`は入力値） |
| OFF | あり | 削除 |
| OFF | なし | 何もしない |

`ClientProduct`の削除は、過去の`Delivery`・`SalesReport`レコード（`ClientId`・`ProductId`を直接参照）には影響しない。削除後はその商品が[納品登録画面](delivery.md)・[売上報告入力画面](sales-report.md)の対象から外れるが、過去データはそのまま保持される。

---

## 8. 確定事項

- FAX番号は任意項目（`FaxNumber` は NULL 許可）。
