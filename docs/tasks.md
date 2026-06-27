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
