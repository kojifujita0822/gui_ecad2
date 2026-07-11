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

`src/Ecad2.App/MainWindow.xaml`（111-142行）：

| メニュー | 項目 | InputGestureText | Click/Command | IsEnabled | 状態 |
|---|---|---|---|---|---|
| ファイル | 新規(_N) | Ctrl+N | Click=NewButton_Click | なし(常時有効) | 結線済 |
| | 開く(_O) | Ctrl+O | Click=OpenButton_Click | なし(常時有効) | 結線済 |
| | 上書き保存(_S) | Ctrl+S | Click=SaveButton_Click | `HasProject` | 結線済 |
| | 名前を付けて保存(_A)... | なし | Click=SaveAsMenuItem_Click | `HasProject` | 結線済(T-063) |
| | PDF出力(_P) | Ctrl+P | **なし** | なし | **未結線** |
| | 終了(_X) | なし | **なし** | なし | **未結線**（実終了は×ボタン/Alt+F4→`Window_Closing`） |
| 編集 | 元に戻す(_U) | Ctrl+Z | Command=UndoCommand | CanExecute連動 | 結線済 |
| | やり直し(_R) | Ctrl+Y | Command=RedoCommand | CanExecute連動 | 結線済 |
| | 切り取り(_T) | Ctrl+X | **なし** | なし | **未結線** |
| | コピー(_C) | Ctrl+C | **なし** | なし | **未結線** |
| | 貼り付け(_P) | Ctrl+V | **なし** | なし | **未結線** |
| | 削除(_D) | Delete | Click=DeleteMenuItem_Click | なし | 結線済(T-063) |
| 表示 | グリッド表示(_G) | Ctrl+G | `IsChecked={Binding IsGridVisible}` | なし | 結線済(T-056) |
| ツール | 設計チェック実行(_D) | なし | Command=RunDrcCommand | CanExecute連動 | 結線済 |
| | テストモード(_M) | F5 | **なし** | なし | **未結線**（T-061未着手） |
| ヘルプ | バージョン情報(_A) | なし | Click=AboutMenuItem_Click | なし | 結線済(T-074) |

「切り取り」はGuiEcad自体に実装がなく、ecad2でも同様に未実装
（`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md:92`）。

---

## 2. ツールバー全ボタン（2段構成）

**1段目（汎用操作）**

| ボタン | ショートカット | 実装 | IsEnabled |
|---|---|---|---|
| 新規作成 | Ctrl+N | Click=NewButton_Click（メニューと**同一メソッド共有**） | なし |
| 開く | Ctrl+O | Click=OpenButton_Click（同一メソッド共有） | なし |
| 上書き保存 | Ctrl+S | Click=SaveButton_Click（同一メソッド共有） | `HasProject` |
| 元に戻す | Ctrl+Z | Command=UndoCommand（**同一ICommandインスタンス共有**） | CanExecute連動 |
| やり直し | Ctrl+Y | Command=RedoCommand（同上） | CanExecute連動 |
| PDF出力 | Ctrl+P | **未結線**（メニューと同様） | なし |
| 行を追加 | Ctrl+Shift+↑ | Command=AddRowCommand | CanExecute連動 |
| 行を削除 | Ctrl+Shift+↓ | Command=DeleteRowCommand | CanExecute連動 |

**2段目（ラダー編集、GX Works3様式、全ボタン`PreviewKeyDown="ToolButtonPreviewKeyDown"`共通付与）**

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
| 自作パーツ | なし | `HasProject` |

Shift+F9・F10は各2ボタンに分かれ、シート種別で`IsEnabled`が排他的に切替（詳細は
`docs/spec/ecad2-spec-wiring.md`参照）。「行を追加/削除」はツールバー・ショートカットのみに存在し、
**メニューには未掲載**（メニュー・ツールバー間で完全対称ではない点に注意）。

---

## 3. 実装済みキーボードショートカット全体

`MainWindow.xaml.cs`の`Window_PreviewKeyDown`（672-977行）で網羅確認。メニュー/ツールバー表記の
あるものは全て実装と一致（Ctrl+N/O/S/Z/Y/G、F5〜F10、Ctrl+Shift+Up/Down）。

**表示上のショートカットキー表記があるにもかかわらず実装が存在しないもの**：

| キー | メニュー表記箇所 | 実装 |
|---|---|---|
| Ctrl+P | PDF出力 | なし |
| Ctrl+X | 切り取り | なし |
| Ctrl+C | コピー | なし |
| Ctrl+V | 貼り付け | なし |
| F5（テストモード側） | テストモード | なし（F5実キーはa接点配置のみが動作） |

**メニュー/ツールバー表記のない裏機能キー**（キーボードファースト設計の一部として存在するが、
UI上には現れない）：Shift+Tab（パネル循環）、矢印キー各種（セル移動・ドラフト調整・要素移動）、
Tab（線プリミティブの端点切替）、Enter（各種確定）。

---

## 4. F5重複（T-061既知事項）の正確な実態

**「重複」は表記上のみであり、実行時の機能競合は起きない。**

- メニュー「テストモード(_M)」（`MainWindow.xaml:137`）は`InputGestureText="F5"`を**表示するのみ**
  で、Click/Commandとも無く完全に未結線。
- 実際のF5キー（`MainWindow.xaml.cs:784`）は`TryPlaceBuiltin("a接点")`（a接点配置）を呼ぶのみ。
  テストモード切替処理は一切存在しない。
- 結論：F5を押すと常にa接点配置が起き、テストモードには絶対に入らない。片方（テストモード側）が
  完全な空表示であり、両方に機能が同時に割り当てられているわけではない。
- F6/F7/F8/F9/F10のInputGestureTextはツールバー側のみに存在し、メニュー側に同キー表示はないため、
  これ以外のキー競合は確認されなかった。

### 裁定根拠

`docs/todo.md:276`：「メニュー『テストモード』の`InputGestureText="F5"`表示がa接点配置（F5実利用中）
と矛盾——実装前に解消必須（P-053起票）」。**解消方針についての殿裁定はまだ記載なし**（T-061は
調査完了・実装未着手、開かれた論点6点は着手時に殿確認【MUST】、`docs/todo.md:278-280`）。

---

## 5. メニューとツールバーの実装共有パターン

| 機能 | 共有関係 |
|---|---|
| 新規/開く/上書き保存 | メニュー・ツールバーとも同一コードビハインドメソッドを共有 |
| Undo/Redo | メニュー・ツールバー・Ctrl+Z/Yキーの3経路すべてが同一`RelayCommand`インスタンスへ収束 |
| 行を追加/削除 | ツールバー・Ctrl+Shift+Up/Downキーのみ共有、メニューには未掲載 |
| PDF出力 | メニュー・ツールバーとも未結線（共有以前に機能自体が存在しない） |

保存系（`SaveButton_Click`→`SaveDocument()`）は`HasProject`ガード・`CommitDeviceNameEdit()`・
未保存パス時のフォールバックまで一本化されており、「名前を付けて保存」（T-063）も同じ
`CommitDeviceNameEdit`/`SaveDocumentAs`を再利用する形で実装が分岐していない
（詳細は`docs/spec/ecad2-spec-sheet-document.md`4節参照）。

---

## 6. 未結線項目の一覧と扱い

| 項目 | メニュー | ツールバー | 状態 |
|---|---|---|---|
| PDF出力 | あり(未結線) | あり(未結線) | T-060調査完了・実装未着手 |
| テストモード | あり(未結線、F5表記のみ) | なし | T-061調査完了・実装未着手 |
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

---

## 8. ドック化・フロート化検討（T-058、AvalonDock導入決定）

対象範囲はツールバー1・2段目、左パレット、右パネル、下部出力パネル。WPF標準に真のフロート化能力が
ないため外部ライブラリが必要と判明、隠密調査でAvalonDockを最有力推奨、**殿裁定（2026-07-11）＝
「外部ライブラリ導入で進めて」**（`docs/todo.md:344`）。

.NET8非対応（AvalonDock 4.74.1はnet5フォールバック見込み）だったが、T-062（.NET 10移行）完了により
このバージョン制約は解消。5.0.0系はnet48/net9/net10のみ対応でnet5緩衝が消滅するため、**T-062の
net10先行移行が結果的に正しい判断だったことが裏付けられた**（`docs/todo.md:365`）。PoC着手可能な
段階にある。

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
- **未結線項目（PDF出力・テストモード等）の実機クリック確認記録は見当たらない**（該当記録なし、
  静的コード調査止まり）。

## 不明点

- 未結線メニュー項目をクリックした際の実際の見た目・反応（グレーアウトなし・無反応と推測されるが
  実機未確認）。
- キーボードショートカット実機検証の運用ルールに幅がある点（`docs-notes/roles/ninja.md`では殿代行
  操作が原則とされる一方、短いショートカットに限りSendKeys使用を安全策とする運用実績もあり、
  適用範囲の線引きが厳密には統一されていない）。本仕様書の範囲外の運用論点として記録のみ。

## 気づき（範囲外、着手せず）

- T-060（PDF出力）・T-061（テストモードF5重複）は隠密2により先行調査済みだった。本調査と結論は
  完全一致したが、今後の同種タスクで調査の重複が起きないよう、家老の采配時に既存調査の有無を
  確認する運用を検討されたい。
