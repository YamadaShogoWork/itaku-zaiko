# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

委託販売在庫管理システム。親の自営業（ハンドメイド商品の製造・販売）における委託販売の在庫・売上管理をExcelからWebアプリに置き換える。
詳細な要件は [docs/requirements.md](docs/requirements.md)、引継ぎ状況は [docs/handover_memo.md](docs/handover_memo.md) を参照。

**現状**: 実装はまだ未着手。`docs/` に要件定義・モックアップHTML・Excelサンプルのみ存在する。`docs/handover_memo.md` の「次のステップ」に沿って ASP.NET Core MVC プロジェクトをこれから作成する。

## 技術スタック（予定）

- ASP.NET Core MVC (.NET 10)
- SQLite + Entity Framework Core
- ASP.NET Core Identity（ログイン必須、全ユーザーが全機能利用可）
- ClosedXML（Excel帳票出力）

## よく使うコマンド（プロジェクト作成後）

```
dotnet new mvc -o <project>          # プロジェクト作成
dotnet ef migrations add <Name>       # マイグレーション追加
dotnet ef database update             # DB更新
dotnet build                          # ビルド
dotnet run                            # 実行（VS Codeでは Ctrl+F5）
dotnet test                           # テスト実行
```

## ドメインモデルと重要なビジネスロジック

DB設計は [docs/requirements.md](docs/requirements.md) の「5. データベース設計」に詳細あり。全テーブル共通で監査カラム（CreatedAt/CreatedBy/UpdatedAt/UpdatedBy）を持つ。

主なエンティティ: `Client`（取引先）, `Color`, `Size`, `Product`（色・サイズはNULL可、`CommissionRate`は掛け率の標準値）, `Delivery`（納品記録、`IsCarryOver`フラグ）, `SalesReport`（月次の期末在庫数）, `ClientProduct`（取引先×取扱商品の中間テーブル、`CommissionRate`に取引先×商品ごとの実際の掛け率を持つ）。

### 売上点数・売上額の計算
```
売上点数 = 期首在庫 + 期間内納品計 − 期末在庫
売上額   = 売上点数 × 下代（下代 = 上代 × ClientProduct.CommissionRate）
```
- 掛け率は取引先×商品の組み合わせごとに異なる（`ClientProduct.CommissionRate`）。`ClientProduct`登録時は`Product.CommissionRate`（商品ごとの標準値）を初期値としてプレフィルし、その取引先だけ異なる場合は上書きする。
- 下代・売上額の計算で小数が生じる場合も、Excelの数式どおり丸め（四捨五入等）は行わない。
- FAXで届くのは期末在庫数（`SalesReport.ClosingStock`）のみ。売上点数はFAXでは来ず、上記の式で算出する。
- 期首在庫 = `Delivery` の `IsCarryOver=true` レコードの `Quantity`（前月末の繰越分）。
- 売上報告入力画面の入力チェック: 期首在庫が存在する商品は期末在庫の入力必須（未入力はエラー）、期首在庫が存在しない商品は任意。

### 月次繰越処理
- 売上報告入力画面の「保存する」ボタン押下時に**自動実行**される（独立したバッチ操作ではない）。
- フロー: 売上報告入力画面で取引先・対象年月を選択 → 期末在庫を入力 → 保存（確認ダイアログ）→ `SalesReport`にUPSERT + 委託販売納品書（月末版）をExcel出力 + 期末在庫を翌月の `Delivery`（`IsCarryOver=true`）として登録（既存の翌月繰越分があれば削除して再登録）。
- 翌月の `SalesReport` が既に登録されている場合、当月の売上報告（該当取引先・対象年月の `SalesReport`）は編集・削除不可（翌月の期首在庫の根拠データが変わってしまうため）。修正する場合は翌月以降の売上報告を先に削除する必要がある。

### ダッシュボードの在庫アラート（暫定ロジック）
- 現在庫 = 直近の `SalesReport.ClosingStock` + それ以降に登録された `IsCarryOver=false` の `Delivery.Quantity` 合計（SalesReportが無ければDelivery合計のみ）。
- 現在庫 1〜2 → 危険（赤）、3〜5 → 警告（橙）、6以上 → アラートなし。
- 詳細は [docs/requirements.md](docs/requirements.md) の「3-4. ダッシュボード」を参照。閾値・算出方法は運用しながら見直す前提の暫定仕様。

### 商品名の表示仕様
- `Product.ProductName` は色・サイズを含まない基本名称（複合語間にスペースなし。例：`山菜図鑑Tシャツ`）。
- 表示時、`ColorId`/`SizeId` が設定されていれば `ProductName + " " + ColorName (+ " " + SizeName)` を連結する（例：`山菜図鑑Tシャツ 白`、`山菜図鑑パーカー 黒 L`）。未設定なら `ProductName` のみ表示。
- `SizeId` は現状未使用（null想定）で表示・連結の対象外。
- 例外：売上報告入力グリッドでは品名（連結前のProductName）と色を別列に表示する。詳細は [docs/requirements.md](docs/requirements.md) の「3-1. マスタ管理 > 商品名の表示仕様」を参照。

### 売上報告入力画面の品名取得ロジック
- 対象取引先の `ClientProduct` に登録された商品のみを表示対象とする。
- `Product`/`Color` をJOINし、`ProductName` でグルーピング（rowspan）、グループ内は `ColorId` 昇順で表示する。カテゴリ見出し（Tシャツ/パーカー等）は設けずフラットな一覧とする。
- 納品登録画面も同じロジックで品名一覧を表示する。

### Excel帳票（委託販売納品書）
- サンプルは [docs/委託販売納品書_サンプル.xlsx](docs/委託販売納品書_サンプル.xlsx)。
- 「期間内納品数」列は `IsCarryOver=false` の `Delivery` を納品日ごとに展開し、最大4列まで。
- 商品に色・サイズが未設定の場合は品名のみ表示する。
- 出力タイミングは3種類ある:
  - **納品時**（納品登録画面）: その納品（取引先×納品日）の納品数量のみ記載し、期末在庫数・売上点数・売上額は空欄で出力。月次繰越処理は実行しない。取引先はこれを印刷し期末在庫数を手書きしてFAX返送する。
  - **売上報告保存時**（売上報告入力画面）: 取引先・対象年月の全項目を出力し、月次繰越処理を自動実行する。
  - **再出力**（帳票出力画面）: 取引先・対象年月を指定してプレビュー表示後、保存済みの`SalesReport`データから売上報告保存時と同内容の帳票を再出力する。月次繰越処理は実行しない。

## UI / モックアップ

- [docs/mockups/css/common.css](docs/mockups/css/common.css) が全画面共通スタイル（CSS変数で色・余白・フォント等を定義）。各画面は固有スタイルのみ追加する方針。
- 実装済みモックアップ: `login.html`, `dashboard.html`, `sales-report.html`, `delivery.html`
- 未作成（要設計時に参照する画面一覧は requirements.md の「6. 画面一覧」）: 取引先管理, 商品管理, 色/サイズマスタ, 帳票プレビュー, ユーザー管理
- デザイン: アクセントカラー `#BA7517`、左サイドバー(220px)+メインコンテンツのレイアウト、フォントは Hiragino Kaku Gothic ProN / Yu Gothic。

## Controller実装の推奨順序

`docs/handover_memo.md` の方針: Client → Color → Size → Product → Delivery → SalesReport の順で、依存関係（マスタ→トランザクション）に沿って実装する。

## フォルダ構成ルール

```
C:\Work\Zaiko\
├── Zaiko/          # ASP.NET プロジェクト本体
├── docs/           # ドキュメント
│   ├── samples/    # 参照用サンプルファイル（Excel・PDF）
│   ├── mockups/    # デザインモックアップ HTML
│   ├── screen_specs/
│   └── work_logs/
├── tests/          # Playwright テストスクリプト
├── scripts/        # ユーティリティスクリプト（reset_db 等）
├── screenshots/    # テスト実行時のスクリーンショット（gitignore）
└── logs/           # ログ出力先（gitignore）
```

- **ログ**: `logs/` に出力すること（例: `node tests/test_flow.js > logs/test_run.log`）
- **テストスクリプト**: `tests/` に置く
- **ユーティリティスクリプト**: `scripts/` に置く
- **スクリーンショット**: `screenshots/` に置く（gitignore 対象）
- **生成物・取り込み済みファイル**: ルート直下に残さない（コミット前に確認）
- **サンプル参照ファイル**: `docs/samples/` に置く

## フロントエンド実装ルール

- サーバー通信を伴うバリデーション（重複チェック等）は `blur` イベントで行う（`input` イベント禁止）
- `input` イベントはクライアント側のみの処理（合計再計算・表示同期等）に限定する

## 作業方針

- 仕様・設計判断で不明な点や迷う点があれば、勝手に判断せず必ずユーザーに質問すること。
- 作業を行ったセッションでは、その日の作業内容を `docs/work_logs/YYYY-MM-DD.md` に記録する（同日に複数セッションがあれば追記）。要件・設計の変更点や未決事項の更新は `docs/requirements.md` 側に反映し、ここには「何をしたか」のみ書く。
