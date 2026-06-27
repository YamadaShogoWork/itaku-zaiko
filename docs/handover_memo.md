# 委託販売在庫管理システム ハンドオーバーメモ

このファイルは「現在の状況」と「次に何をすべきか」への入口。詳細は各ドキュメントを参照すること。

| ドキュメント | 内容 |
|---|---|
| [requirements.md](requirements.md) | 要件定義・DB設計・画面一覧・未決事項（仕様のSSOT） |
| [screen_specs/](screen_specs/) | 画面ごとの詳細仕様（ボタン動作・DB取得項目など） |
| [work_logs/](work_logs/) | 日々の作業履歴 |
| [mockups/](mockups/) | モックアップHTML |

---

## 現在のステータス

**全タスク実装完了。手動テスト待ち。**

- フェーズ0〜6（基盤・マスタ・ユーザー管理・サービス・納品・売上報告・ダッシュボード）の実装がすべて完了（2026-06-22）
- 追加タスク18件（`docs/tasks.md`）の実装がすべて完了（2026-06-27）
- GitHubリポジトリ: https://github.com/YamadaShogoWork/itaku-zaiko
- Playwright自動テスト 34/34 通過済み（2026-06-23時点）。追加タスク対応後の手動テストは未実施

### 実装済み画面
- `screen_specs/`: `sales-report.md`, `sales-report-history.md`, `delivery.md`, `delivery-history.md`, `client.md`, `client-edit.md`, `product.md`, `product-edit.md`, `color.md`, `users.md`, `user-edit.md`, `dashboard.md`
- `mockups/`: `login.html`, `dashboard.html`, `sales-report.html`, `sales-report-history.html`, `delivery.html`, `delivery-history.html`, `client.html`, `client-edit.html`, `product.html`, `product-edit.html`, `color.html`, `users.html`, `user-edit.html`

### 方針メモ
- サイズマスタ管理画面は作らない方針（`Size`テーブル・`Product.SizeId`はDB設計上保持するが、UIは現状不要）
- システムは基本1人での利用・1台のPC内で完結する運用を想定（外部公開・サーバーホスティングは行わない）。ログイン機能・ユーザー登録機能は維持
- 月次繰越処理・Excel出力のタイミング: 売上報告入力画面の保存時に「全項目入りの帳票出力＋月次繰越処理」を自動実行。保存済みデータの再出力は売上報告履歴画面の「帳票」モーダルから行う（プレビュー付き、繰越処理なし）。翌月のSalesReportが既に登録されている場合、当月の売上報告は編集・削除不可（[screen_specs/sales-report.md](screen_specs/sales-report.md) 8章、[screen_specs/sales-report-history.md](screen_specs/sales-report-history.md) 3.3）

---

## 未決事項

`requirements.md` の「7. 未決事項」は全項目解決済み。掛け率は `Product.CommissionRate`（標準値）と `ClientProduct.CommissionRate`（取引先×商品ごとの実際値）の2段構成に確定（詳細は5章・7章参照）。

---

## 次のステップ

1. **手動テスト**（`docs/manual_test.md` の手順に従う）
   - テストデータ投入バッチ（`/SeedData`）でデータを準備してから実施する
   - Excel出力の確認（納品時・売上報告保存時・再出力の3パターン）を含む
2. 問題なければ本番運用開始

---

## ツール

- `tools/XlsxReader`：実データのExcelファイル（数式・値）を確認するための簡易C#コンソールツール（ClosedXML使用）
