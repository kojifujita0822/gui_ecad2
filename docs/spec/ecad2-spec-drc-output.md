# ecad2 仕様書：設計チェック(DRC)・出力パネル

T-075（殿裁定、2026-07-11起票）体系の第7号、第4弾2件目。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`）・忍者実機検証記録（`docs-notes/`配下）を突き合わせ、
「仕様として確定している挙動」を出典付きで明文化する。

---

## 1. `DesignRuleCheck`のチェック項目一覧（全8種）

`src/Ecad2.Core/Simulation/DesignRuleCheck.cs`（静的クラス、非破壊の静的検査）。診断は`Diagnostic`
レコード（`Severity`/`Code`/`DeviceName`/`Message`/`Locations`）。`DiagnosticSeverity`は
`Info`/`Warning`/`Error`の3段階。

| メソッド | コード | 重大度 | 検出内容 |
|---|---|---|---|
| `CheckCrossReference` | `DRC-XREF-001` | Warning | 接点はあるが駆動コイルが図面上に無い（駆動元不明） |
| 同上 | `DRC-XREF-002` | Warning | コイルはあるが接点が図面上に一つも無い（死にリレー） |
| 同上 | `DRC-XREF-003` | Warning | 1コイルに対しリレー接点が物理上限（4個）超過 |
| `CheckDeviceTypeConsistency` | `DRC-TYPE-001` | Error | 同一機器名に励磁系接点と入力系接点が混在 |
| 同上 | `DRC-TYPE-002` | Warning | コイルで駆動される機器の接点種別が入力系になっている |
| `CheckVerticalCrossings` | `DRC-CONN-001` | Warning | 縦コネクタが中間行の横配線とドット無しで交差（電気的非接続） |
| `CheckLoadReachability` | `DRC-LOAD-001`/`-002` | Error | 負荷の入力側／出力側が左／右母線から到達不可 |
| `CheckSeriesCoils` | `DRC-LOAD-003` | Warning | 2つ以上のコイル(負荷)が直列接続（二重コイル） |
| `CheckUnresolvedPartId` | `DRC-PART-001` | Warning | 自作パーツ参照(PartId)がライブラリで解決できず、a接点へ暗黙フォールバック |

`CheckVerticalCrossings`/`CheckLoadReachability`/`CheckSeriesCoils`はシート単位で`Sheet`＋`Netlist`
を引数に取り、後2者は内部の`FloodContacts`（母線起点BFS）を共通利用する。

---

## 2. DRC実行フロー

メニュー「ツール(_T)」→「設計チェック実行(_D)」（`MainWindow.xaml:136`、
`Command="{Binding OutputPanel.RunDrcCommand}"`）——**ツールバーボタンは存在せず、メニューのみ**。

`RunDrc()`（`OutputPanelViewModel.cs:66-83`）の実行順序：

1. `DesignRuleCheck.CheckCrossReference(Document, PartLibrary)`
2. `DesignRuleCheck.CheckDeviceTypeConsistency(...)`
3. `DesignRuleCheck.CheckUnresolvedPartId(...)`
4. 各シートについて`NetlistBuilder.Build`でネットリストを構築し、`CheckVerticalCrossings`/
   `CheckLoadReachability`/`CheckSeriesCoils`を実行

`Diagnostics.Clear()`後に`Diagnostics.Add(diagnostic)`——毎回全チェックを再実行し結果を丸ごと
差し替える（差分更新ではない）。`Diagnostics`は`ObservableCollection<Diagnostic>`。

---

## 3. 出力パネルの実装

View：`MainWindow.xaml:518-550`の`DockPanel x:Name="OutputPanelArea"`（下部、`GridSplitter`で
ドラッグ調整可、T-059）。DataGrid列構成（`AutoGenerateColumns="False"`、`IsReadOnly="True"`）は5列：

| 列 | バインド先 | 表示変換 |
|---|---|---|
| 重大度 | `Severity` | `DiagnosticSeverityToTextConverter`で日本語化（Error→エラー/Warning→警告/Info→情報）、文字色（Error=赤`Firebrick`/Warning=橙`DarkOrange`/Info=灰`Gray`）、太字 |
| コード | `Code` | 例：`DRC-XREF-001` |
| 機器名 | `DeviceName` | — |
| メッセージ | `Message` | `Width="*"`で残り幅を占有 |
| 該当箇所 | `Locations` | `CircuitRefListToTextConverter`が`"P{ページ番号} 行{回路番号}"`形式（GuiEcad踏襲）に変換 |

---

## 4. ジャンプ機能

**実装あり**。`SelectedDiagnostic`のsetterで`Locations.Count>0`なら`JumpTo`を呼ぶが、**DataGridは
同一行の再クリックでは`SelectedItem`バインディングが変化を検知せず発火しない**（WPF既知挙動）。
これに対処するため`JumpToDiagnostic`公開メソッドを用意し、`DataGridRow.PreviewMouseLeftButtonDown`
（`OutputGridRow_Clicked`）から直接呼び出す設計になっている。

`JumpTo`：該当ページ番号のシートへ`CurrentSheetIndex`を切替え、行内で`DeviceName`一致要素を探索
（見つからなければ`DRC-PART-001`の場合は`PartResolver.IsUnresolvedPartId`一致要素を優先、それも
無ければ行内先頭要素、それも無ければ列0）を`SelectedCell`にセットしてジャンプ。

### 裁定経緯

T-018殿裁定（2026-07-03）：「構造化DataGrid＋重大度色分けを採用（GuiEcad側の素朴なListView文字列
整形案ではなく）」。着手時の優先順位づけでも「UI/UX殿裁定済み、着手障壁が最も低い」として最初に
アサインされた経緯がある。

---

## 5. `ConnectivityChecker`との関係（独立した2つの到達可能性判定）

`DesignRuleCheck.cs`は`ConnectivityChecker`を**一切参照していない**。`ConnectivityChecker`を利用
しているのは`DiagramRenderer.cs`のみで、「接続検査モード」の配線色分け（青=接続／黒=未結線
スタブ）専用の別機構。`DesignRuleCheck`側は同様の到達可能性判定を独自の`FloodContacts`
（母線起点BFS）で実装しており、**`ConnectivityChecker`とはロジックを共有していない**。目的
（DRC診断 vs 描画時の配線色分け）も呼び出し経路も完全に独立している。

---

## 6. DRC結果のクリアタイミング

`OutputPanel.ClearResults()`の呼び出し箇所は**2箇所のみ**（プロジェクト全体でコード確認済み）：

1. `ReplaceDocument`（新規作成／開く時の文書差し替え）
2. `ApplyUndoRedoSnapshot`（Undo/Redo時、T-051バグ修正#4で追加）

保存時・シート追加削除時・要素編集時等のクリア呼び出しは存在しない——**DRC結果は明示的に再実行
（メニュー操作）されるまで、または文書差し替え/Undo・Redoが起きるまで残り続ける**。

---

## 7. 関連タスク

- **T-052「未解決PartIdフォールバックのDRC警告追加」**（完全Done、2026-07-11）：起票=P-017殿裁定
  承認。既存の出力パネル・ジャンプ機構を流用。隠密レビューで誤ジャンプバグ（`OutputPanelViewModel.cs`
  内、`DRC-PART-001`診断が意図した未解決要素と異なる位置へジャンプ）を検出、往復1周目で修正完了。
- **T-059「出力パネルの高さ調整」**（完全Done）：殿直接指示「出力パネルの高さをスライダーで調整
  できるようにしたい」から起票。「スライダー」という文言だが、実装は既存の`GridSplitter`パターン
  踏襲のドラッグ式と家老が判断。

---

## 8. 既知の罠（実機検証固有、UI Automation経由の検証時のみ該当）

**DataGridのUI仮想化により、スクロール範囲外（非表示）の行がUIAツリーに存在しない**——T-052実機
検証で、出力パネルの高さが小さいウィンドウでDRC実行したところUIAの`FindAll`で7件しかDataItemが
取得できず「`DRC-PART-001`が出ない」と誤診断しかけた実例がある。実際は12件全て生成されており、
ウィンドウを拡大したところ判明した。**DataGridの全件検証時は、ウィンドウ/パネルを十分な高さに
するかスクロールしてから確認する必要がある**（`docs/spec/ecad2-spec-device-table.md`7節と同一の
既知事項、出力パネル固有の実例として本仕様書でも記録。忍者から`ecad2-ui-automation`スキルへの
追記候補として申告済みだが、本調査時点でスキル本体には未反映）。

---

## 9. 実機確認記録

- T-018時点：観点1/4・同一/別シートジャンプ全OK。UI Automation偽結果の罠にも留意し物理クリックで
  再検証済み。
- T-050：DDDシートにコイル孤立配置→DRC実行で警告発生、警告行クリックで1回切替＋該当セル選択＋
  プロパティパネル同期を確認（二重発火・表示食い違いなし）。
- T-051（Undo/Redo）：DRC実行→シート追加→Ctrl+Zで出力パネルに古い診断が残留しないことを確認
  （`ClearResults()`が正しく機能）。
- T-052：無名要素の診断クリックで未解決要素そのものへ正しくジャンプ（往復1周目の誤ジャンプ修正が
  実機で機能）。
- T-059：リサイズ後も診断行クリックで正しくジャンプすることを確認。
- 本日T-062検証：DRC・出力パネルへの言及なし（検証観点は配置/結線/保存読込/Undo・Redoの4点のみ、
  該当記録なし）。

## 10. 検索・置換機能（2026-07-21追記、T-070反映。出力パネルと統合）

**T-070（完全Done、2026-07-14）は本仕様書のスコープ策定時（T-075、2026-07-11）にはまだ存在せず、
旧版に一切の記述が無かった（欠落）。** 検索・置換は独立機能だが、実装上は出力パネルと密結合して
いるため本節で扱う。

### 検索バー（`FindBar`、作図エリア上部オーバーレイ）

`Ctrl+F`でトグル表示。`ElementPlacementBar`と同型の同一Window内オーバーレイ設計（`MainWindow.xaml`
1409-1444行）。検索対象は**機器名の完全一致のみ**（殿裁定）。検索欄・前へ/次へボタン・置換後欄・
置換/全置換ボタン・閉じるボタンで構成。

### 出力パネルとの表示切替（重要）

出力パネル（3節）は「出力」というタイトルの単一タブ内で、`Find.IsVisible`（検索バー表示中か）に
応じて`FindResultsGrid`（検索結果：シート/機器名/該当箇所の3列）と`OutputGrid`（DRC診断結果）を
`Visibility`で相互排他的に切り替える（`MainWindow.xaml`1626-1680行）。**検索バーを開いている間は
出力パネルの表示内容がDRC結果から検索結果へ切り替わる**——DRC結果を見ながら同時に検索結果も見る、
という並行表示はできない。

### AvalonDockタブタイトルのバインディング制約（既知の罠）

`LayoutAnchorable`（AvalonDockのタブコンテナ）は`FrameworkElement`ではなくWPFのDataContext継承
（Visual/Logical Tree経由）の対象外のため、タブタイトルへの通常の`{Binding Find.IsVisible}`は
機能しない（一次ソースで確認済み）。`MainWindow.xaml.cs`側で`Find.PropertyChanged`を購読し
コードビハインドから直接`Title`を更新する方式で対処している。

## 不明点

- 本仕様書に該当する不明点は特になし（調査範囲内で全項目が事実確認できた）。
