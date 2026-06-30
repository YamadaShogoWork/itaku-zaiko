# タスク一覧

元要望: `テスト後追加要望.txt`

## ステータス凡例
`未着手` / `仕様検討` / `実装中` / `完了`

---

| # | 対象画面 | 内容 | ステータス | 備考 |
|---|---|---|---|---|
| 1 | ログイン | ログインユーザーを記憶するチェックボックス（Remember me）を実装 | 完了 | 60日有効。Program.csでConfigureApplicationCookie、Login.cshtml.csにRememberMeプロパティ追加、Login.cshtmlにチェックボックス追加 |
| 2 | 商品登録 | 掛け率のデフォルト値を `0.8` に設定（定数化） | 完了 | ProductEditViewModel.CommissionRate = 0.8m |
| 3 | 商品登録 | 掛け率の上下ボタンで 0.1 ずつ増減 | 完了 | step="0.1"に変更、min/max制限なし、Range検証削除 |
| 5 | 取引先登録 | Excel・納品登録・売上報告入力での表示順を指定できるようにしたい | 完了 | SortableJSでドラッグ&ドロップ。ClientProductにSortOrder列追加。チェックON→取扱あり末尾に移動＋ドラッグ可、チェックOFF→末尾移動＋ドラッグ不可 |
| 6 | 納品履歴 | 編集時に現在庫列が必要かどうか | 仕様検討 | 対応しない |
| 7 | 納品履歴 | 帳票ボタンを追加（売上報告履歴と同様） | 完了 | IsCarryOver=falseの行にのみ表示。DeliveryController.DownloadExcelに直接リンク |
| 8 | 納品登録 | カードのサイズ・レイアウトを納品履歴に合わせる | 完了 | search-barのmargin-bottom・min-widthを納品履歴に合わせてCSSを統一 |
| 9 | 売上報告入力 | 対象年月のデフォルトを15日境界で切り替え（〜15日→先月、16日〜→今月） | 完了 | SalesReportController.GetDefaultYearMonth()を追加、YearMonth初期値に適用 |
| 10 | 売上報告入力・売上報告履歴 | 数値列のヘッダーを右寄せにしてデータと揃える | 完了 | th.num { text-align:right } を両画面のCSSに追加 |
| 11 | 売上報告入力 | 期末在庫の初期値を未入力（空欄）にする | 完了 | inputからvalue属性を削除 |
| 12 | 売上報告入力 | 数値の桁数変化による列幅ずれを防ぐ（固定幅） | 完了 | td.num, th.num { white-space:nowrap; min-width:4.5em } |
| 13 | 売上報告入力 | 期首在庫ありの行で期末在庫が未入力の場合にエラー表示 | 完了 | 保存時のみ。条件は期首在庫 > 0。Controller・JS両方で検証済み（既存実装で対応済み） |
| 14 | 売上報告入力・売上報告履歴・取引先登録 | 下代・売上額の小数点以下を切り捨て表示 | 完了 | 画面・Excel両方でMath.Floor適用。SalesReport/Index.cshtml・SalesReportHistory/Index.cshtml・_PreviewContent.cshtml・Client/Edit.cshtml・ExcelOutputService.cs更新 |
| 15 | 取引先・色マスタ | 同名レコードがある場合に警告 | 完了 | 取引先・色のみ。フォーカスアウト時にCheckDuplicateエンドポイントを呼び出してインライン警告表示 |
| 16 | 共通 | 確認ダイアログをカスタムデザインに変更（モックアップ作成） | 完了 | パターンC採用。Dialog.confirm/alert を site.js に実装、全ビューで置き換え済み |
| 17 | 納品履歴・売上報告履歴 | 検索条件クリアボタンを追加 | 完了 | 「クリア」ボタンを検索フォームに追加。Indexへの引数なしリンク |
| 18 | 納品履歴・売上報告履歴 | 検索条件が全て空の場合は全件表示 | 完了 | HasSearch廃止。コントローラーを常にクエリ実行するよう変更、条件指定時のみフィルタ |
| 19 | 納品履歴・売上報告履歴 | 100件単位のページング（定数化） | 完了 | PageSize=100定数。ViewModelにPage/TotalCount/HasPrev/HasNext追加。コントローラーでSkip/Take。ビューに前へ/次へボタン表示 |
| 20 | 取引先・色マスタ | 「全@Model.Count件」がRazorで評価されず文字列のまま表示されるバグを修正 | 完了 | `全@(Model.Count)件` に修正（Client/Index.cshtml, Color/Index.cshtml） |
| 21 | 共通 | テストデータ投入バッチを作成 | 完了 | SeedDataController.cs（開発環境のみ /SeedData でアクセス）。A/B/Cパターン対応 |

---

## 2026-06-27 テスト結果から起票

| # | 対象画面 | 内容 | ステータス | 備考 |
|---|---|---|---|---|
| T-1 | 色マスタ | 警告表示時に追加ボタンの高さがずれる問題を修正（警告をフォーム外に移動） | 完了 | |
| T-2 | 色マスタ | 重複チェックのタイミングを blur → input イベントに変更（デバウンス 300ms） | 完了 | |
| T-3 | 色マスタ | インライン編集フォームにも重複チェックを追加（currentId 除外） | 完了 | |
| T-4 | 商品・取引先（共通） | 保存ボタン押下時に reportValidity() でバリデーション。無効な場合はダイアログを出さない | 完了 | form.reportValidity() で実装（サーバーサイド Validate エンドポイントは不要と判断） |
| T-5 | 商品管理 | 掛け率入力の step="0.1" を step="any" に変更して HTML5 バリデーション警告を解消 | 完了 | |
| T-6 | 商品管理 | 一覧の品名列グレー表示（上代・掛け率）を削除 | 完了 | |
| T-7 | 商品管理 | 掛け率更新範囲ダイアログの「プレフィル値」を「初期値」など一般的な表現に変更 | 完了 | |
| T-8 | 取引先管理 | 取扱商品テーブルの下代を初期表示から計算値（上代 × 掛け率）で表示 | 完了 | 未チェック行も含む |
| T-9 | 取引先管理 | 掛け率列ヘッダーを左寄せ・rate-input の step を any に変更（novalidate 追加） | 完了 | |
| T-10 | 取引先管理 | 削除ロジック変更：Delivery・SalesReport が 0 件なら ClientProducts ごと物理削除 | 完了 | |
| T-11 | 全画面 | 戻るボタン（キャンセル）を左上に配置 | 未着手 | **優先度低** |
| T-12 | 商品管理・取引先管理・納品登録・売上報告入力 | 数値入力をカスタムスピナー（−/＋ボタン）に置き換え。`step="any"` + `data-delta` で増減量を制御。T-3・T-5 も同時解消 | 完了 | common.css + site.js にスピナー共通実装。各画面で spinner-group/spinner-group-sm に置き換え |
| T-13 | 取引先管理 | 取扱商品テーブルの列幅調整（`table-layout:fixed`、色10em+padding1em、上代180px、下代220px、掛け率128px） | 完了 | |
| T-14 | 納品登録 | 入力テーブルの列幅調整（`table-layout:fixed`、色10em+padding1em、現在庫140px、納品数量180px、品名auto） | 完了 | |
| T-15 | 売上報告入力 | 入力テーブルの列幅調整（`table-layout:fixed`、`min-width:1120px`、`overflow-x:auto`、色140px、期末在庫180px、期間内納品数60px×4+colspan幅、日付列padding 4px） | 完了 | |
