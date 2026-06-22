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

実装はまだ未着手。`docs/` に要件定義・モックアップ・仕様書のみ存在する。

- 画面一覧（requirements.md 6章）の全画面のモックアップ・screen_specsが作成済み（帳票出力は売上報告履歴画面のモーダルに統合済み）
- `screen_specs/`: `sales-report.md`, `sales-report-history.md`, `delivery.md`, `delivery-history.md`, `client.md`, `client-edit.md`, `product.md`, `product-edit.md`, `color.md`, `users.md`, `user-edit.md`, `dashboard.md`
- `mockups/`: `login.html`, `dashboard.html`, `sales-report.html`, `sales-report-history.html`, `delivery.html`, `delivery-history.html`, `client.html`, `client-edit.html`, `product.html`, `product-edit.html`, `color.html`, `users.html`, `user-edit.html`
- サイズマスタ管理画面は作らない方針（`Size`テーブル・`Product.SizeId`はDB設計上保持するが、UIは現状不要）
- システムは基本1人での利用・1台のPC内で完結する運用を想定（外部公開・サーバーホスティングは行わない）。ログイン機能・ユーザー登録機能は維持
- 月次繰越処理・Excel出力のタイミングを変更: 売上報告入力画面の保存時に「全項目入りの帳票出力＋月次繰越処理」を自動実行する方針に変更。保存済みデータの再出力は売上報告履歴画面の「帳票」モーダルから行う（プレビュー付き、繰越処理なし）。翌月のSalesReportが既に登録されている場合、当月の売上報告は編集・削除不可（[screen_specs/sales-report.md](screen_specs/sales-report.md) 8章、[screen_specs/sales-report-history.md](screen_specs/sales-report-history.md) 3.3）

---

## 未決事項

`requirements.md` の「7. 未決事項」は全項目解決済み。掛け率は `Product.CommissionRate`（標準値）と `ClientProduct.CommissionRate`（取引先×商品ごとの実際値）の2段構成に確定（詳細は5章・7章参照）。

---

## 次のステップ（Claude Codeで着手すること）

1. `dotnet new mvc` でプロジェクト作成
2. ASP.NET Core Identity の追加
3. EF Core + SQLite の設定
4. Modelクラスの作成（上記テーブル設計をもとに）
5. DBマイグレーション
6. Controllerの作成（Client → Color → Size → Product → Delivery → SalesReport の順推奨）
7. Viewの作成（モックアップHTMLを参考に）
8. Excel出力機能（ClosedXML）

---

## ツール

- `tools/XlsxReader`：実データのExcelファイル（数式・値）を確認するための簡易C#コンソールツール（ClosedXML使用）
