# GuiEcad仕様書：メニュー・ツールバー全体構成

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のメニュー・ツールバー実装をExplore委譲調査＋実物照合で
纏め、`docs/spec/ecad2-spec-menu-toolbar.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-menu-toolbar.md`

**前提**：GuiEcadはWinUI3（`MenuBar`/`MenuBarItem`/`MenuFlyoutItem`＋独自`KeyboardAccelerator`管理）、
ecad2はWPF（`Menu`/`MenuItem`＋`InputGestureText`／`Window_PreviewKeyDown`）と基盤が異なるため、
対応関係は「同名機能」単位で突き合わせる。

---

## 1. メニュー全項目

`GuiEcad.App/MainPage.xaml:119-168`（`MenuBar x:Name="TopMenuBar"`）：

| メニュー | 項目 | ショートカット表記 | ハンドラ | 状態 |
|---|---|---|---|---|
| ファイル(F) | 新規(N) | Ctrl+N | `OnMenuNew`（`MainPage.Menu.cs:74`） | 結線済 |
| | テンプレートから新規作成... | なし | `OnMenuNewFromTemplate`（`MainPage.Templates.cs:94`） | 結線済 |
| | 開く(O)... | Ctrl+O | `OnMenuOpen`（`MainPage.Menu.cs:82`） | 結線済 |
| | 上書き保存(S) | Ctrl+S | `OnMenuSave`（`MainPage.Menu.cs:148`） | 結線済 |
| | 名前を付けて保存(A)... | なし | `OnMenuSaveAs`（`MainPage.Menu.cs:150`） | 結線済 |
| | テンプレートとして保存... | なし | `OnMenuSaveAsTemplate`（`MainPage.Templates.cs:135`） | 結線済 |
| | オートセーブ設定... | なし | `OnMenuAutosaveSettings`（`MainPage.Autosave.cs:68`） | 結線済 |
| | PDFプレビュー... | なし | `OnMenuPreviewPdf`（`MainPage.Menu.cs:217`） | 結線済 |
| | PDF出力... | なし | `OnMenuExportPdf`（`MainPage.Menu.cs:231`） | 結線済 |
| 編集(E) | 元に戻す(U) | Ctrl+Z | `OnMenuUndo`（`MainPage.Menu.cs:34`） | 結線済 |
| | やり直し(R) | Ctrl+Y | `OnMenuRedo`（`MainPage.Menu.cs:35`） | 結線済 |
| | 検索・置換(F)... | Ctrl+F | `OnMenuFind`（`MainPage.Menu.cs:72`） | 結線済 |
| | 削除(D) | Del | `OnDelete`（`MainPage.xaml.cs:511`） | 結線済 |
| | ショートカットキー設定... | なし | `OnMenuKeyBindingSettings`（`MainPage.KeyBindings.cs:184`） | 結線済 |
| 図面(D) | ドキュメント情報... | なし | `OnDocumentInfo`（`MainPage.Dialogs.cs:53`） | 結線済 |
| | シート設定... | なし | `OnSheetSettings`（`MainPage.Dialogs.cs:160`） | 結線済 |
| | 部品リスト(BOM)... | なし | `OnBomEditor`（`MainPage.Dialogs.cs:250`） | 結線済 |
| 表示(V) | 拡大(+)/縮小(-)/全体表示 | Ctrl++/Ctrl+-/Ctrl+0 | `OnZoomIn`/`OnZoomOut`/`OnFit`（`MainPage.xaml.cs:721-724`） | 結線済 |
| | 機器表を表示 | なし | `OnDevicePanelToggle`（`MainPage.Properties.cs:30`） | 結線済 |
| | グリッド表示（Toggle） | **表記なし** | `OnGridToggle`（`MainPage.xaml.cs:294`） | 結線済だがキー割当なし |
| | ダークモード（UIクロム／作図色の2系統） | なし | `OnDarkModeToggle`/`OnCanvasDarkToggle`（`MainPage.xaml.cs:281,320`） | 結線済 |
| 図形(G) | （動的生成、固定項目なし） | - | `RebuildShapeMenu`（`MainPage.Tools.cs:56`） | 結線済(動的) |
| ヘルプ(H) | 使い方... | なし | `OnMenuHowTo`（`MainPage.Menu.cs:456`） | 結線済 |
| | バージョン情報... | なし | `OnMenuAbout`（`MainPage.Menu.cs:373`） | 結線済 |
| | 再ビルドして再起動 | なし | `OnMenuRestart`（DEBUG限定表示、`MainPage.Menu.cs:300`） | 結線済(開発限定) |

**重要な構造差**：`MainPage.xaml`全体に`IsEnabled`バインディングは**1件も存在しない**。GuiEcadは
起動時に常に空ドキュメントを生成するため「プロジェクト未読込」状態自体が構造的に存在せず、
ecad2の`HasProject`/`CanExecute`連動グレーアウトに相当する機構がそもそも要らない設計。

「切り取り/コピー/貼り付け」はメニュー項目自体が**存在しない**（編集メニューは上表5件のみ）。
ただしCopy/Pasteの実処理はショートカット・右クリックメニュー経由で実装済み（4節参照）——
ecad2（メニュー表記のみで未結線）とは逆の非対称。

---

## 2. ツールバー全ボタン

GuiEcadは「横1段の標準ツールバー」＋「左端の縦ツールパレット（配置ツール選択）」の2系統構成で、
ecad2の「横2段ツールバー」とは配置構造自体が異なる（段数対応はしない、並記のみ）。

**標準ツールバー**（`MainPage.xaml:170-256`）：テスト（ToggleButton）／キー配置（Collapsed）／
元に戻す・やり直し・削除（メニューと同一メソッド共有）／接続検査（ToggleButton）／拡大・縮小・
全体表示（メニューと共有）／列－・列＋／行＋・行－（Ctrl+Shift+↑/↓）／PDFプレビュー・PDF
（メニューと共有）／一時停止（テストモード時のみ表示）。

**縦ツールパレット**（`MainPage.xaml:298-441`、`ToolDockHost`、ドック⇄フロート切替可）：選択／
a接点・b接点／押釦NO・NC／タイマNO・NC／瞬時NO・NC／コイル／表示灯／端子／分岐(縦コネクタ)／
分断／枠／直線／点／画像挿入／その他▼（セレクトSW・サーマル・非常停止・三相モータ）。全項目
`RadioButton GroupName="Tools"`の排他選択。

**新規/開く/保存に対応するツールバーボタンは存在しない**（ファイルメニューのみ）——ecad2
（ツールバーにも専用ボタンあり）と構造が異なる。両系統ともボタンに`IsEnabled`バインディングなし。

---

## 3. 実装済みキーボードショートカット一覧

### 3-A. カスタマイズ可能ショートカット（GuiEcad独自機構）

`MainPage.KeyBindings.cs:48-113`の`CommandDefs`（19コマンド：Undo/Redo/Save/Delete/Find/
CommentEdit/InsertRow/DeleteRow/Copy/Paste/ToolContactNO/ToolContactNC/ToolCoil/
ToolPushButtonNO/ZoomIn/ZoomOut/ZoomFit/New/Open）は、ユーザーが「ショートカットキー設定」
ダイアログ（`OnMenuKeyBindingSettings`、`MainPage.KeyBindings.cs:184-264`）で再割当可能。設定は
`%MyDocuments%\GuiEcad\keybindings.json`へ永続化（同45-46,152-164行）。

### 3-B. 固定（カスタマイズ不可）ショートカット

Enter（機器名編集起動／キーボード配置モード確定、`MainPage.xaml:98`）、Escape（7分岐、
`MainPage.KeyboardMode.cs:113,119-131`）、Backspace（削除、テキスト入力中は無効化）、
PageUp/PageDown（1行スクロール）、Space押下中+ドラッグ（画面パン）。

### 3-C. キーボード配置モード（UI入口は非表示、ロジックは現存）

`MainPage.xaml:183-196`のコメントに「マウスレス配置（キーボード配置モード）は動作不良のため
2026-07-01 UI入り口を非表示化」と明記。数字キー1-0でツール選択・矢印キーでフォーカスセル移動
のロジック自体は`MainPage.KeyboardMode.cs`に現存するが、起動ボタン`KeyboardModeBtn`が
`Visibility="Collapsed"`のため実機到達不能。

### 3-D. グリッド表示切替（Ctrl+G）に対応するショートカットキーは存在しない

`VirtualKey.G`/`Ctrl+G`はGuiEcad.App配下で0件ヒット。マウスクリック（表示メニュー）限定。

---

## 4. メニュー・ツールバー間の実装共有パターン

| 機能 | 共有関係 |
|---|---|
| Undo/Redo/削除/ズーム | メニュー・ツールバーが同一`Click`ハンドラメソッドを共有。ショートカットは別ヘルパー
  （`DoUndo`/`DoRedo`等）を直接呼ぶが、そのハンドラ自身も同じヘルパーを呼ぶため3経路とも収束 |
| 新規/開く/保存/検索 | メニューとショートカットが同一ヘルパー（`OnMenuNew`等）を共有。対応する
  **ツールバーボタンは存在しない** |
| PDFプレビュー/PDF出力 | メニュー・ツールバーとも同一メソッド共有（ecad2と異なり両方実装済み） |
| コピー/貼り付け | ショートカットと右クリックメニューが`CopySelection`/`PasteSelection`を共有。
  **メニューバーに項目自体が存在しない** |
| 行を追加/削除 | ツールバー・ショートカットのみ、メニューには未掲載（ecad2と同型の非対称） |
| 列を追加/削除 | ツールバーのみの単独機能。メニュー・ショートカットに対応項目なし |

**全体傾向**：ecad2は単一`ICommand`（`RelayCommand`）インスタンスをXAMLで複数コントロールへ
バインドする方式だが、GuiEcadは「複数のイベントハンドラが同一の非公開ヘルパーメソッドを呼ぶ」
方式で共有を実現。到達する処理は収束するが共有の実装機構が異なる。

---

## 5. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 | 備考 |
|---|---|---|
| ショートカットキー設定ダイアログ（カスタマイズ・JSON永続化） | `MainPage.KeyBindings.cs`全体 | ecad2は完全固定 |
| 「図形(G)」メニュー（自作/組込みパーツの動的階層メニュー） | `MainPage.Tools.cs:56` | ecad2に対応項目なし |
| 「図面(D)」メニュー（ドキュメント情報／シート設定／BOM） | `MainPage.xaml:143-148` | ecad2の5メニュー構成にはこの括りがない |
| PDFプレビュー（実装済み） | `MainPage.Menu.cs:217` | ecad2はPDF自体未結線 |
| テンプレート機能（新規作成/保存）、オートセーブ設定 | `MainPage.xaml:122,127-128` | ecad2に対応メニューなし |
| ダークモード切替（UIクロム／作図色2系統） | `MainPage.xaml:157-158` | ecad2に対応記載なし |
| 検索・置換（Ctrl+F、専用バー） | `MainPage.Find.cs` | ecad2はT-070未着手 |
| Copy/Paste実働実装 | `MainPage.Clipboard.cs` | ecad2はメニュー表記のみで未結線（逆非対称） |
| 縦パレットのドック⇄フロート切替 | `MainPage.Palette.cs:161-` | ecad2はAvalonDock PoC未着手（T-058） |
| 列＋/列－ボタン（グリッド列数増減） | `MainPage.xaml:226-227` | ecad2の仕様書に対応項目なし |
| キーボード配置モード（現在UI入口非表示、ロジック現存） | `MainPage.KeyboardMode.cs` | 既知バグのため意図的に隠されている点も含め特徴的 |
| 再ビルドして再起動（DEBUG限定の開発補助） | `MainPage.Menu.cs:300` | ecad2に対応なし |
| 使い方ダイアログ（ショートカット一覧込み） | `MainPage.Menu.cs:456` | `guiecad-spec-undo-redo.md`等でも既出のT-077関連前例 |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 | GuiEcad側の状況 |
|---|---|---|
| `IsEnabled`(`HasProject`/`CanExecute`)連動の自動グレーアウト機構 | `ecad2-spec-menu-toolbar.md`表内`IsEnabled`列 | GuiEcadは`IsEnabled`バインディング皆無（常時ドキュメント保持のため構造的に不要） |
| ツールバー2段構成＋`PreviewKeyDown`共通付与（矢印キー誤操作対策） | `ecad2-spec-menu-toolbar.md:58,188-190` | GuiEcadの縦パレットに同種の共通ハンドラなし（別文脈でキーボード配置モード自体を隠す対処） |
| 「終了」メニュー項目（未結線、Alt+F4等で代替） | `ecad2-spec-menu-toolbar.md:26,149` | GuiEcadにファイルメニュー「終了」項目自体が存在しない |

### (3) 両方にあるが挙動が異なる点

| 項目 | ecad2 | GuiEcad |
|---|---|---|
| グリッド表示切替 | Ctrl+G結線済 | メニュークリックのみ、キー割当なし |
| コピー/貼り付け | メニュー表記のみ未結線 | 実働だがメニュー項目自体が無い |
| 新規/開く/保存 | メニュー・ツールバー・ショートカット3経路 | メニュー・ショートカットのみ、専用ボタンなし |
| PDF出力 | 未結線（T-060未着手） | 実装済み＋プレビュー併設 |
| ショートカットのカスタマイズ | 完全固定 | 19コマンドがユーザー再割当可能（JSON永続化） |

---

## 出典

- GuiEcad: `GuiEcad.App/MainPage.xaml`（119-441行各所）、`MainPage.Menu.cs`（各所）、
  `MainPage.KeyBindings.cs`（各所）、`MainPage.xaml.cs`（各所）、`MainPage.Clipboard.cs`、
  `MainPage.ContextMenu.cs`、`MainPage.Tools.cs`、`MainPage.KeyboardMode.cs`、
  `MainPage.Dialogs.cs`、`MainPage.Templates.cs`、`MainPage.Autosave.cs`、`MainPage.Properties.cs`、
  `MainPage.Palette.cs`、`MainWindow.xaml.cs`（Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-menu-toolbar.md`（比較対象）

## 不明点

- 縦ツールパレットにecad2の`IsMainCircuitSheet`/`IsControlCircuitSheet`相当のシート種別依存
  排他切替があるか、`MainPage.xaml`上の構文的確認では判断できず（`IsEnabled`バインディング皆無
  のため）。コード側で別途制御している可能性は排除できない。
- Space+ドラッグのパン実処理箇所（`MainPage.Pointer.cs`）は本調査で行番号まで未確認。
- GuiEcad側メニュー・ツールバー項目の実機クリック挙動の検証記録有無は調査対象外。
