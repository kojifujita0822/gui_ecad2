# ecad2 仕様書：メニュー・ツールバー全体構成

T-075（殿裁定、2026-07-11起票）体系の第4号、第2弾。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`）・忍者実機検証記録（`docs-notes/`配下）を突き合わせ、
「仕様として確定している挙動」を出典付きで明文化する。以降の全領域仕様書から参照される「入口」の
位置づけ。

**先行調査との関係**：本領域の一部（T-060 PDF出力・T-061テストモードF5重複）は既に隠密2が
`docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`／`docs/ecad2-t061-testmode-ui-wiring-survey-onmitsu2.md`
として先行調査済み。本調査は独立にコードを読解したが結論は完全に一致しており、クロスチェックとして
機能した。今後の重複調査は家老の采配で調整されたい（気づきとして付記）。

---

## 1. メニュー全項目

**（2026-07-21更新、T-060・T-061・T-077反映）** `src/Ecad2.App/MainWindow.xaml`（910-956行）：

| メニュー | 項目 | InputGestureText | Click/Command | IsEnabled | 状態 |
|---|---|---|---|---|---|
| ファイル | 新規(_N) | Ctrl+N | Click=NewButton_Click | なし(常時有効) | 結線済 |
| | 開く(_O) | Ctrl+O | Click=OpenButton_Click | なし(常時有効) | 結線済 |
| | 上書き保存(_S) | Ctrl+S | Click=SaveButton_Click | `HasProject` | 結線済 |
| | 名前を付けて保存(_A)... | なし | Click=SaveAsMenuItem_Click | `HasProject` | 結線済(T-063) |
| | PDF出力(_P) | Ctrl+P | Click=PdfExportMenuItem_Click | `HasProject` | **結線済(T-060)** |
| | 終了(_X) | なし | **なし** | なし | **未結線**（実終了は×ボタン/Alt+F4→`Window_Closing`） |
| 編集 | 元に戻す(_U) | Ctrl+Z | Command=UndoCommand | CanExecute連動 | 結線済 |
| | やり直し(_R) | Ctrl+Y | Command=RedoCommand | CanExecute連動 | 結線済 |
| | 切り取り(_T) | Ctrl+X | **なし** | なし | **未結線** |
| | コピー(_C) | Ctrl+C | **なし** | なし | **未結線** |
| | 貼り付け(_P) | Ctrl+V | **なし** | なし | **未結線** |
| | 削除(_D) | Delete | Click=DeleteMenuItem_Click | なし | 結線済(T-063) |
| 図面 | ドキュメント情報(_I) | なし | Click=DocumentInfoMenuItem_Click | `HasProject` | 結線済 |
| | 画像挿入(_M) | なし | Click=InsertImageMenuItem_Click | `CanEditDiagram` | 結線済 |
| 表示 | グリッド表示(_G) | Ctrl+G | `IsChecked={Binding IsGridVisible}` | なし | 結線済(T-056) |
| | ダークモード(作図色)(_D) | なし | `IsChecked={Binding IsDarkMode}` | なし | 結線済(T-083) |
| | 現在のレイアウトを既定として保存(_L) | Ctrl+Alt+S | Click=SaveDockingLayoutMenuItem_Click | なし | 結線済(T-058) |
| ツール | 設計チェック実行(_D) | なし | Command=RunDrcCommand | CanExecute連動 | 結線済 |
| | テストモード(_M) | **Ctrl+T** | `IsChecked={Binding IsTestMode}`(TwoWay) | `HasProject` | **結線済(T-061)**。ショートカットは表示どおりCtrl+T（F5ではない） |
| ヘルプ | 使い方(_G) | F1 | Click=UsageMenuItem_Click | なし | 結線済(T-077) |
| | バージョン情報(_A) | なし | Click=AboutMenuItem_Click | なし | 結線済(T-074) |

**旧版本節の「PDF出力＝未結線」「テストモード＝未結線・F5表記」という記述はT-060・T-061実装
（いずれもT-075完了2026-07-11の直後）以前の情報であり誤り。** テストモードはCtrl+Tに結線され、
F5キーは現在もa接点配置専用のまま重複は生じていない（詳細は4節参照）。

「切り取り」はGuiEcad自体に実装がなく、ecad2でも同様に未実装
（`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md:92`）。

---

## 2. ツールバー構成（タブ切替、T-104で2段構成から変更）

**（2026-07-21更新、T-101・T-104反映）** 旧版本節は「2段固定表示」の構成を記載していたが、
T-104（2026-07-20）でAvalonDockの`LayoutAnchorablePane`（`PlacementToolBarDockingManager`、
`MainWindow.xaml`990行）内の2つの`LayoutAnchorable`（タブ）へ変更された。**現在は「基本機能」
「配置ツール」のタブ切替表示**（タブヘッダークリックで表示内容を切替、常時両方表示される
固定2段ではない）。

**「基本機能」タブ**（`Title="基本機能"`、`MainWindow.xaml`1018-1108行付近）

| ボタン | ショートカット | 実装 | IsEnabled |
|---|---|---|---|
| 新規作成 | Ctrl+N | Click=NewButton_Click（メニューと**同一メソッド共有**） | なし |
| 開く | Ctrl+O | Click=OpenButton_Click（同一メソッド共有） | なし |
| 上書き保存 | Ctrl+S | Click=SaveButton_Click（同一メソッド共有） | `HasProject` |
| 元に戻す | Ctrl+Z | Command=UndoCommand（**同一ICommandインスタンス共有**） | CanExecute連動 |
| やり直し | Ctrl+Y | Command=RedoCommand（同上） | CanExecute連動 |
| PDF出力 | Ctrl+P | Click=PdfExportMenuItem_Click（メニューと同一メソッド共有） | `HasProject`。**結線済(T-060)**、旧版「未結線」は誤り |
| 行を追加 | Ctrl+Shift+↑ | Command=AddRowCommand | CanExecute連動 |
| 行を削除 | Ctrl+Shift+↓ | Command=DeleteRowCommand | CanExecute連動 |
| テストモード | Ctrl+T | `ToggleButton`、`IsChecked={Binding IsTestMode}` | `HasProject`。**結線済(T-061)**、旧版「未結線・F5表記」は誤り |

**「配置ツール」タブ**（`Title="配置ツール"`、`MainWindow.xaml`1109行以降。全ボタン
`PreviewKeyDown="ToolButtonPreviewKeyDown"`共通付与）

| ボタン | ショートカット | IsEnabled |
|---|---|---|
| 選択ツール | Esc | `HasProject` |
| a接点配置 | F5 | `HasProject` |
| OR a接点配置 | Shift+F5 | `HasProject` |
| b接点配置 | F6 | `HasProject` |
| OR b接点配置 | Shift+F6 | `HasProject` |
| コイル配置 | F7 | `HasProject` |
| 端子台配置 | F8 | `HasProject` |
| 自由線(横線)記入 | F9 | `IsMainCircuitSheet` |
| 自由線(縦線)記入 | Shift+F9 | `IsMainCircuitSheet` |
| 縦分岐線記入 | Shift+F9 | `IsControlCircuitSheet` |
| 接続点記入 | F10 | `IsMainCircuitSheet` |
| 配線分断記入 | F10 | `IsControlCircuitSheet` |
| グループ枠 | なし | `HasProject` |
| 自作パーツ | **F11** | `HasProject`。旧版「ショートカットなし」は誤り（T-087で追加） |

Shift+F9・F10は各2ボタンに分かれ、シート種別で`IsEnabled`が排他的に切替（詳細は
`docs/spec/ecad2-spec-wiring.md`参照）。「行を追加/削除」は「基本機能」タブ・ショートカットのみに
存在し、**メニューには未掲載**（メニュー・ツールバー間で完全対称ではない点に注意）。

**T-101（2026-07-21）で新設された恒久ハイライト**：「配置ツール」タブのボタン群は、現在有効な
ツールを背景色+枠線でカーソル位置に依存せず恒久的にハイライトする（接続点記入・配線分断記入を
除く。詳細は`docs/spec/ecad2-spec-statusbar.md`0節参照）。

---

## 3. 実装済みキーボードショートカット全体

**（2026-07-21更新）** `MainWindow.xaml.cs`の`Window_PreviewKeyDown`（2134-2716行、旧版記載の
672-977行は行番号ズレのため更新）で網羅確認。メニュー/ツールバー表記のあるものは全て実装と一致
（Ctrl+N/O/S/Z/Y/G/T/P/Alt+S、F1、F5〜F11、Ctrl+Shift+Up/Down）。

**表示上のショートカットキー表記があるにもかかわらず実装が存在しないもの**：

| キー | メニュー表記箇所 | 実装 |
|---|---|---|
| Ctrl+X | 切り取り | なし |
| Ctrl+C | コピー | なし |
| Ctrl+V | 貼り付け | なし |

旧版に記載されていた「Ctrl+P（PDF出力）」「F5（テストモード側）」は、それぞれT-060・T-061
（いずれもT-075完了直後）で実装され表示どおり動作するため、この表からは除外した（1節参照）。

**メニュー/ツールバー表記のない裏機能キー**（キーボードファースト設計の一部として存在するが、
UI上には現れない）：Shift+Tab（パネル循環）、矢印キー各種（セル移動・ドラフト調整・要素移動）、
Tab（線プリミティブの端点切替）、Enter（各種確定）。

---

## 4. F5重複（T-061既知事項）は解消済み

**（2026-07-21更新）旧版本節が記録していた「F5表記重複」は、T-061（テストモード実装、
T-075完了直後）でテストモードのショートカットがCtrl+Tへ変更されたことにより解消済み。**

- メニュー「テストモード(_M)」は現在`InputGestureText="Ctrl+T"`を表示し、実装（`IsChecked`
  双方向バインディング）と一致している（1節参照）。
- F5キーは引き続き`TryPlaceBuiltin("a接点")`（a接点配置）専用のまま変更されていない。
- 結論：表示と実装の重複・矛盾は解消済み。F5=a接点配置、Ctrl+T=テストモード切替で完全に分離
  されている。

### 裁定根拠（解決の経緯）

`docs/todo.md`（当時）：「メニュー『テストモード』の`InputGestureText="F5"`表示がa接点配置
（F5実利用中）と矛盾——実装前に解消必須（P-053起票）」という問題提起を受け、T-061実装時に
テストモードのショートカットをCtrl+Tへ変更する形で解消された（解消方針の詳細な殿裁定記録は
本調査では特定できず、T-061完了記録から実装結果のみ確認、**不明点**）。

---

## 5. メニューとツールバーの実装共有パターン

| 機能 | 共有関係 |
|---|---|
| 新規/開く/上書き保存 | メニュー・ツールバーとも同一コードビハインドメソッドを共有 |
| Undo/Redo | メニュー・ツールバー・Ctrl+Z/Yキーの3経路すべてが同一`RelayCommand`インスタンスへ収束 |
| 行を追加/削除 | ツールバー・Ctrl+Shift+Up/Downキーのみ共有、メニューには未掲載 |
| PDF出力 | メニュー・ツールバーとも`PdfExportMenuItem_Click`を共有（**T-060で結線済み**、旧版「未結線」は誤り） |
| テストモード | メニュー・ツールバーとも`IsTestMode`双方向バインディングを共有、Ctrl+Tキーも同一プロパティを切替（**T-061で結線済み**） |

保存系（`SaveButton_Click`→`SaveDocument()`）は`HasProject`ガード・`CommitDeviceNameEdit()`・
未保存パス時のフォールバックまで一本化されており、「名前を付けて保存」（T-063）も同じ
`CommitDeviceNameEdit`/`SaveDocumentAs`を再利用する形で実装が分岐していない
（詳細は`docs/spec/ecad2-spec-sheet-document.md`4節参照）。

---

## 6. 未結線項目の一覧と扱い

**（2026-07-21更新）PDF出力・テストモードはT-060・T-061でそれぞれ結線済みとなったため、この表
からは削除した（1節参照）。**

| 項目 | メニュー | ツールバー | 状態 |
|---|---|---|---|
| 切り取り | あり(未結線) | なし | ecad2自体に未実装（GuiEcadにも無し） |
| コピー | あり(未結線) | なし | 未実装 |
| 貼り付け | あり(未結線) | なし | 未実装 |
| 終了 | あり(未結線) | なし | ×ボタン/Alt+F4の標準終了で代替済み |

未結線項目はいずれも`IsEnabled`バインディングが無いため、**実機上は常時クリック可能な見た目で
グレーアウトなし、クリックしても無反応**と推測される（`docs/archive/ecad2-t063-menu-review-onmitsu.md`の
DoD3表と整合）。**この推測を裏付ける忍者実機確認記録は本調査では見当たらなかった**（不明点）。

---

## 7. アイコン意匠（T-013、未着手）

T-009段階3では簡易プレースホルダ（Path Geometry/Unicode記号）のみで、本格意匠制作はT-013として
切り出されたが**現在も未着手**（`docs/todo-archive.md:124`）。個別タスク（T-040配置系ボタン、
T-047手動配線ボタン、T-048グリフ変更）では以下の慣行が定着している：

- 「意匠は既存様式踏襲＋侍起草→殿目視確認」というプレビュー承認制（T-047殿裁定）。
- 無効時ボタンは半透明化（opacity 0.35）で表現、実機の等倍表示で判別可能と確認済み
  （`docs/archive/ecad2-t047-fix-ninja-verification.md:108-125`）。
- **T-089（2026-07-18、追記2026-07-21）でボタン押下時の視覚フィードバックを新設**：
  Button/ToggleButtonのPressed/MouseOver状態が明示的に色変化するようになった（既定Aero2
  テンプレートの単純継承のみではフィードバックが弱かったための改善）。「配置ツール」タブの
  ボタン群は、この押下フィードバックに加えT-101の恒久ハイライトも重なる（2節参照）。

---

## 8. ドック化・フロート化（T-058、AvalonDock導入→本格実装済み）

対象範囲はツールバー1・2段目、左パレット、右パネル、下部出力パネル。WPF標準に真のフロート化能力が
ないため外部ライブラリが必要と判明、隠密調査でAvalonDockを最有力推奨、**殿裁定（2026-07-11）＝
「外部ライブラリ導入で進めて」**（`docs/todo.md:344`）。

.NET8非対応（AvalonDock 4.74.1はnet5フォールバック見込み）だったが、T-062（.NET 10移行）完了により
このバージョン制約は解消。5.0.0系はnet48/net9/net10のみ対応でnet5緩衝が消滅するため、**T-062の
net10先行移行が結果的に正しい判断だったことが裏付けられた**（`docs/todo.md:365`）。

**（2026-07-21更新）旧版本節の「PoC着手可能な段階にある」は導入決定直後の記述であり、その後
本格実装まで進んでいる。** AvalonDockは左パレット・右パネル・出力パネル・配置ツールバー
（2節参照）全てにドッキング可能なペインとして導入済み。ダークモード連動（T-083）、配置
ツールバーの独自ドロップ枠方式（T-103）・タブ切替表示（T-104、2節参照）等、多数の関連改修が
完了している。

---

## 9. 実機確認の裏付け状況

- 入口3系統（メニュー/ツールバー/ショートカット）の一致は新規/開く/保存でOK確認済み
  （`docs-notes/ecad2-t019-verification-ninja.md:21`）。
- シート0件時、F5〜F8・Shift+F5/F6全キー無反応＋案内メッセージ表示を確認
  （`docs-notes/ecad2-t019-zoku-verification-ninja.md:11`）。
- T-047検証で発見された重大バグ（ボタン起動直後の矢印キーがツールバーナビゲーションに奪われ隣接
  ボタンを誤起動、意図しないデータが確定配置される）は修正確認済み（全観点GREEN、
  `docs/archive/ecad2-t047-fix-ninja-verification.md`）。
- T-063（名前を付けて保存・削除）は忍者実機確認で全観点OK（`docs/todo.md:213-214`）。
- 本日T-062検証：ツールバー`IsEnabled`状態（シート0件時の無効化、シート種別依存、Undo/Redo連動）
  すべて既存仕様と確認、退行なし（`docs-notes/ecad2-t062-main-operations-regression-ninja.md`）。
- **（2026-07-21更新）旧版本節の「未結線項目（PDF出力・テストモード等）の実機クリック確認記録は
  見当たらない」は解消済み**：PDF出力・テストモードはT-060・T-061で結線され、以後の各種タスク
  （T-077・T-101等）で実機確認記録が蓄積されている。切り取り・コピー・貼り付け・終了の4項目は
  今も未結線のまま、実機クリック確認記録は見当たらない。

## 不明点

- 未結線メニュー項目（切り取り・コピー・貼り付け・終了の4項目、旧版はPDF出力・テストモードも
  含めていたが結線済みとなったため対象から除外）をクリックした際の実際の見た目・反応
  （グレーアウトなし・無反応と推測されるが実機未確認）。
- キーボードショートカット実機検証の運用ルールに幅がある点（`docs-notes/roles/ninja.md`では殿代行
  操作が原則とされる一方、短いショートカットに限りSendKeys使用を安全策とする運用実績もあり、
  適用範囲の線引きが厳密には統一されていない）。本仕様書の範囲外の運用論点として記録のみ。

## 気づき（範囲外、着手せず）

- T-060（PDF出力）・T-061（テストモードF5重複）は隠密2により先行調査済みだった。本調査と結論は
  完全一致したが、今後の同種タスクで調査の重複が起きないよう、家老の采配時に既存調査の有無を
  確認する運用を検討されたい。
