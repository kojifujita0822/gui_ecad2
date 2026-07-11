# GuiEcadにあってecad2のApp層に接続されていない機能の全量棚卸し

調査者: 隠密2　最終更新: 2026-07-11

殿直接指示（実装要否判断の材料）。`docs/archive/ecad2-ui-ux-inventory.md`（忍者、2026-07-03）を出発点に、
GuiEcad実ソース・ecad2実ソース双方を実物照合。GuiEcad側の調査はExploreサブエージェント4体
（ファイルメニュー系／編集・表示メニュー系／ツールバー・右クリック・ショートカット／ダイアログ・
部品管理・パレット）、ecad2側現状棚卸しは1体へ委譲し、判定の肝となる箇所（Core層の器の有無、
主要メソッドの実在）は隠密2本人が実物で裏取りした。読み取りのみ、実装・書き込みは行っていない。

## 区分の定義（家老補正、2026-07-11）

Core層（Model/Simulation/Rendering/Persistence/Pdf）はT-007で全量移植済みが前提。

- **A** = 実装済み（UI結線済み、GuiEcad相当かそれ以上）
- **B** = Core層に実体（器）があるが、App層の結線のみ欠く
- **C** = GuiEcadではApp層（WinUI3）に実装されていたロジックのため、ecad2には論理ごと存在しない
  （Core層にも器がない）

既知4件は再調査対象外（表の末尾に参照のみ記載）：PDF出力（T-060）／テストモード（T-061）／
GroupFrame作成UI（P-054）／Undo・Redo（T-051）。

---

## 1. ファイルメニュー系

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感・依存関係 |
|---|---|---|---|---|---|
| 1 | 新規作成 | App（`OnMenuNew`→`ApplyNewDocument`共通処理） | **A** | — | `NewButton_Click`実装済み、`ConfirmDiscardIfDirty`経由 |
| 2 | テンプレートから新規作成 | App専用（ビルトイン2種はコード生成、ユーザーテンプレは`MyDocuments\GuiEcad\templates`フォルダ管理） | **C** | 中 | Core/App双方に該当クラス皆無。`BasicPartTemplates`(Core)は部品テンプレで別物、混同注意。保存基盤(`GcadSerializer`)は流用可 |
| 3 | 開く | App（`OnMenuOpen`→`LoadFileAsync`） | **A** | — | `OpenButton_Click`実装済み。ただしオートセーブ新旧比較(7)は無い |
| 4 | 上書き保存 | App（`OnMenuSave`→`SaveCurrentAsync`） | **A** | — | `SaveDocument`実装済み、既存パスなら直接上書き |
| 5 | 名前を付けて保存 | App専用メニュー項目（`OnMenuSaveAs`） | ほぼ**A**（露出欠落） | 小 | `SaveDocumentAs()`は既に存在（上書き保存の内部フォールバックとしてのみ）。独立メニュー項目としての公開のみ欠く |
| 6 | テンプレートとして保存 | App専用（`MyDocuments\GuiEcad\templates`へ別名コピー保存） | **C** | 小〜中 | 2と共通基盤。テンプレ機構自体が無いため2との同時実装が自然 |
| 7 | オートセーブ設定・復元 | App専用（`DispatcherTimer`＋設定ファイル`autosave-interval.txt`＋`LoadFileAsync`内の新旧比較→復元確認ダイアログ） | **C** | 中 | Core/App双方皆無。3の「開く」処理に復元チェックを組み込む設計が必要 |
| 8 | 未保存変更の破棄確認 | App（`ConfirmDiscardIfDirtyAsync`。**GuiEcad側は抜け穴ありと既存調査で指摘済み**） | **A（GuiEcad比で改善）** | — | `ConfirmDiscardIfDirty`が New/Open/ウィンドウクローズの単一関門（T-019、殿裁定2026-07-05）。GuiEcadの弱点を教訓に最初から統一実装されている |

## 2. 編集メニュー系

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感・依存関係 |
|---|---|---|---|---|---|
| 9 | 検索・置換 | App（`FindController`+`MainPage.Find.cs`、機器名**完全一致**検索、循環ジャンプ、置換は`RenameDeviceCommand`経由） | **C** | 中 | Core/App双方皆無。既存のDRC結果ハイライトジャンプ機構（`OutputPanelViewModel`）が流用できる可能性あり |
| 10 | 削除（メニュー項目） | App（`DeleteSelected()`共通ロジック、種別ごとに`Delete*Command`） | ほぼ**A**（露出欠落） | 小 | `MainWindow.xaml.cs:876-884`でDeleteキー実装済み（`DeleteSelectedElement`等5種）。メニュー項目としての露出のみ欠く |
| 11 | ショートカットキー設定 | App専用（JSON永続化`keybindings.json`＋動的`KeyboardAccelerator`再構築＋競合検出＋既定値リセット） | **C** | 大 | 静的XAML定義のみで再割当て機構が皆無。汎用的なキー再割当て基盤の新設が必要、優先度は低いと所感 |
| 12 | 切り取り／コピー／貼り付け | **「切り取り」はGuiEcad自体に実装なし**（メニュー・コード共に存在せず）。コピー/貼り付けはApp（`ClipboardData`内部record、**右クリックメニュー＋ショートカットのみ**、GuiEcadのトップメニューには本来存在しない） | **C** | 中 | Core/App双方皆無。ecad2のメニュー項目（切り取り/コピー/貼り付け）はGuiEcadにすら無い体裁を先取りしているのみで実体なし。「切り取り」メニュー項目自体の要否は再検討の余地あり |
| 13 | 画像挿入 | App（`MainPage.Image.cs`、Win2Dプリロード・120mm超で自動縮小） | **B** | 小〜中 | **Core層完備**：`ImageInsert`(`Element.cs:103`)・`Sheet.Images`・`DiagramRenderer.DrawImages`(`:471`)まで実装済み。App層は参照0件（ファイルピッカー＋配置ツール1つの追加のみで着手可能、依存関係が少なく着手しやすい候補） |

## 3. 図面メニュー系

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感・依存関係 |
|---|---|---|---|---|---|
| 14 | ドキュメント情報ダイアログ | App（社名/図番/顧客/設計/製図/確認/日付＋改定履歴の動的追加・削除） | **B** | 中 | **Core層完備**：`DocumentInfo`/`RevisionEntry`(`Document.cs:17`)。App層は参照0件（改定履歴の動的リストUIがやや手間） |
| 15 | シート設定ダイアログ | App（シート名・左右母線名・電圧・列数2-20/行数1-60・既定母線名化・主回路チェック） | **A（機能限定的）** | 小 | ecad2の`SheetSettingsDialog.xaml`は**行数・左右母線名のみ**。列数・電源ラベル(`BusConfig.PowerLabel`)・主回路切替(`Sheet.MainCircuit`)はCore層に器はあるがダイアログから設定不可。既存ダイアログへの項目追加で対応可能 |
| 16 | 部品リスト(BOM)ダイアログ（編集） | App（機器名/種別/型式/メーカー/数量の編集可能Grid、ダイアログ幅を720へ拡張する対応入り） | **B** | 小 | **Core層完備**：`Device.Model`/`Maker`/`Quantity`(`Device.cs`)。App層は`DeviceTableGrid`が`IsReadOnly="True"`の表示専用（`MainWindow.xaml:454`）。IsReadOnly解除＋保存コマンド追加、または独立ダイアログ化で対応可能 |

## 4. 表示メニュー系

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感・依存関係 |
|---|---|---|---|---|---|
| 17 | 拡大／縮小（ズーム） | App（`CanvasViewport`、固定点ズーム0.2〜12.0、メニュー/ツールバー/Ctrl+ホイール/Ctrl+±/Ctrl+0） | **A（基盤のみ）** | 小 | `CanvasScale`(0.25〜4.0)がCtrl+マウスホイールで操作可能（`MainWindow.xaml.cs:137`）。**メニュー/ツールバーの明示的ボタン、キーボード操作(Ctrl++/Ctrl+-)は既存コメントで「段階8でまとめて対応予定」と明記の計画済み・未着手事項** |
| 18 | 全体表示（フィット） | App（`CanvasViewport.Reset()`、固定既定値へのリセットのみ、実際のフィット計算ではない） | **C（軽微）** | 小 | パン(PanX/PanY)・フィット相当の専用器はecad2に無いが、標準`ScrollViewer`（`MainWindow.xaml:407`）でスクロールバー相当のパンは既に可能。フィット機能はスクロール位置リセット処理の追加程度で対応可能 |
| 19 | 機器表を表示（トグル） | App（右パネルのスライド開閉アニメーション） | 該当なし（設計相違） | — | ecad2の機器表は右パネル上段に**常時表示**（`docs/ecad2-t058-docking-float-survey-onmitsu.md`4節）。トグル自体が不要という意図的な設計差でありギャップではない |
| 20 | グリッド表示切替 | App（表示メニューのトグル） | **A** | — | T-056として隠密2が別途調査済み（`docs/archive/ecad2-t056-grid-toggle-proposals-onmitsu2.md`）。**本セッション中に侍が実装しA区分へ到達したことを実物で確認**（`IsGridVisible`、`MainWindow.xaml:130`） |
| 21 | ダークモード／ダークモード(作図色) | App（UIテーマ`ElementTheme`とキャンバス`DrawingTheme`の2系統独立管理、それぞれ別ファイルへ永続化） | **C** | 中 | ecad2側にテーマ切替の実装・メニュー項目は皆無（Grep 0件）。2系統の独立設計ごと丸ごと無し |

## 5. 図形メニュー系（自作パーツ管理）

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感・依存関係 |
|---|---|---|---|---|---|
| 22 | 自作パーツ管理（作成/編集/削除/ピン留め/インポート・エクスポート） | App（`MainPage.Parts.cs`、メニュー動的構築）＋Core（`PartFolderStore`「マスター」＋`PartLibrary`埋め込みの二重管理、`PinnedPartStore`でJSON永続化） | **B** | **大** | **Core層完備**：`PartDefinition`/`PartFolderStore`/`PartLibrarySerializer`/`PinnedPartStore`/`PartResolver`/`PartOptimizer`が全て存在。App層は`PartPaletteViewModel`（配置用の部品選択のみ）に留まり、**作成・編集・削除・ピン留めのUIが皆無**。GuiEcadは独立ウィンドウ`PartEditorWindow`（描画ツール・Undo/Redo込みで約950行超）という大規模GUIエディタで実現しており、同等機能の新規実装はコスト大 |

## 6. ヘルプメニュー系

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感 |
|---|---|---|---|---|---|
| 23 | 使い方（ヘルプ） | Appダイアログ | **C** | 小〜中（内容次第） | ecad2にメニュー項目自体が存在しない。優先度は低いと所感 |
| 24 | バージョン情報 | Appダイアログ | **C（軽微）** | 小 | メニュー項目は存在するがClick未設定。単純なダイアログ1つで対応可能 |
| 25 | 終了 | — | **C（軽微）** | 小 | メニュー項目のみ存在、Click未設定。ウィンドウの×ボタンで実質代替できており実害は小さい |

## 7. 右クリックメニュー（作画モード）

GuiEcadの`ShowDrawingContextMenu`は状況依存で(a)コピー/貼り付け、(b)要素の削除/コメント編集/機器名変更、
(c)縦コネクタ削除、(d)枠のラベル編集/削除/線種変更、(e)行の挿入/追加/削除、の5系統を出し分ける。

ecad2は**T-055増分3で(e)行操作系のみ実装済み**（`MainWindow.xaml.cs:558-587`、「行{n}の前に行を挿入」
「末尾に行を追加」「行{n}を削除」の3項目、`InsertRowBeforeCommand`/`AddRowCommand`/`DeleteRowAtCommand`
にCommand+CommandParameterでバインド、コメントに「ecad2初のContextMenu」と明記）。

| # | 機能 | ecad2状態 | 規模 | 所感 |
|---|---|---|---|---|
| 26 | (e)行の挿入/追加/削除 | **A** | — | 実装済み（T-055増分3） |
| 27 | (a)コピー/貼り付け | **C** | 中 | 2節12の機能そのもの、右クリック経路も含め未実装 |
| 28 | (b)要素の削除/コメント編集/機器名変更（右クリック経由） | **C（機能自体は別経路で存在）** | 小〜中 | 削除・コメント編集(F2)・機器名変更(Enter)自体はキーボード操作で別途実装済みと見込まれるが、右クリックメニューからの導線は無い |
| 29 | (c)縦コネクタ削除（右クリック経由） | **C（機能自体はDelキーで存在）** | 小 | 削除自体はDelキー経由(`DeleteSelectedConnector`)で可能、右クリック導線のみ欠く |
| 30 | (d)枠のラベル編集/削除/線種変更 | GroupFrame関連はP-054既知（再調査対象外） | — | — |

## 8. キーボードショートカット

GuiEcadのカスタマイズ可能ショートカット19種のうち、ecad2で未対応・要注意なもの：

| # | ショートカット | GuiEcadでの動作 | ecad2状態 | 所感 |
|---|---|---|---|---|
| 31 | Ctrl+F（検索） | 検索バートグル | **C** | 2節9と同一機能 |
| 32 | Ctrl+ +/-, Ctrl+0（ズーム） | ズームイン/アウト/フィット | **C（計画済み）** | 4節17参照、既存コメントで「段階8」対応予定と明記済み |
| 33 | Ctrl+C/Ctrl+V | コピー/貼り付け | **C** | 2節12と同一 |
| 34 | F2（コメント編集） | 選択要素のコメント編集 | **不明** | ecad2側の実装有無は本調査で未確認、別途確認要 |
| 35 | PageUp/PageDown（離散スクロール） | セル1行分のスクロール | **不明** | 標準ScrollViewerのキーボード操作で類似挙動が出る可能性あり、要確認 |

**注記**：GuiEcadの「キーボード配置モード」自体はWinUI3のフォーカス制御バグにより2026-07-01付けで
GuiEcad本体でも入口が非表示化され実質使用不能（Microsoft "not planned"）。ecad2はWPF選定＋T-002/T-006
PoCでこの種のフォーカス制御課題を解消済みのため、移植ではなく最初からの再設計が既定方針（design-brief
に明記済み、既知の方針でありギャップ一覧には含めない）。

## 9. ダイアログ・パレット等その他機構

| # | 機能 | GuiEcadでの実装層・概要 | ecad2状態 | 規模 | 所感 |
|---|---|---|---|---|---|
| 36 | エラー表示（一般向けメッセージ/スタックトレース分離） | App（`ShowErrorAsync`、メッセージのみ表示。スタックトレースは`AppLog.Crash`で別ファイルへ） | **不明** | 小〜中 | ecad2側の現行エラー表示パターンの有無は今回未確認（`TrySaveToFile`のcatchでは一般向け文言のみ表示している実装は確認済み、`MainWindow.xaml.cs:241-246`。個別ケースごとの徹底度は未確認） |
| 37 | プロパティパネル（動的組み立て） | App（`RefreshPropertiesPanel`、種別ごとに完全動的UI構築） | **不明** | — | ecad2の右パネル「プロパティ」タブの実装詳細は本調査では未確認（範囲外、別途確認要） |
| 38 | 可動ツールパレット（ドック/フロート/上下吸着） | App（`PaletteDock` 4値enum、`palette-pos.txt`永続化） | **C** | 大 | T-058で既に外部ライブラリ(AvalonDock)導入検討が進行中（別調査`docs/ecad2-t058-*`参照）。自前実装ではなくライブラリ導入で代替する方針が既に走っており、本項目は独立実装しない見込み |
| 39 | シート名変更 | Appダイアログ方式（`ContentDialog`＋TextBox） | **不明** | 小 | ecad2側の実装有無は本調査では未確認（`SheetNavigationViewModel.RenameCommand`が存在することはecad2側エージェント報告で判明、ダイアログ方式かインライン編集かは要確認） |

---

## 既知4件（参照のみ、再調査対象外）

| 機能 | 既存調査 |
|---|---|
| PDF出力 | `docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`（本セッション既実施） |
| テストモード | `docs/ecad2-t061-testmode-ui-wiring-survey-onmitsu2.md`（本セッション既実施） |
| GroupFrame作成UI | P-054（別途起票済み） |
| Undo・Redo | T-051進行中（対象=シート追加/削除のみ、拡張は別途） |

---

## 総括所感

- **着手しやすい候補（B区分・小〜中規模）**：画像挿入（13）・ドキュメント情報（14）・部品リストBOM編集（16）・
  シート設定の項目追加（15）——いずれもCore層の器が完備しており、App層の結線のみで完結する。
- **設計が既に固まっている候補**：グリッド表示切替（20、実装済み）・シート設定の基本部分（15、実装済み）。
- **大物・要判断**：自作パーツ管理（22、独立エディタが必要、規模大）・ショートカットキー設定（11、汎用キー
  再割当て基盤が必要、規模大）・可動ツールパレット（38、AvalonDock導入検討と表裏一体、T-058参照）。
- **優先度が低いと考えられる候補**：ヘルプ（23）・切り取り（12、GuiEcad自体に実装がない）・ダークモード（21）。
- **「不明」のまま残った項目（34, 35, 36, 37, 39）**は、ecad2側の該当コード箇所を特定しきれなかったもの。
  必要であれば追加調査可能。

## 出典一覧

- サブエージェント5体の調査結果（ファイルメニュー系／編集・表示メニュー系／ツールバー・右クリック・
  ショートカット／ダイアログ・部品管理・パレット／ecad2側現状棚卸し）
- 隠密2本人による実物裏取り：`src/Ecad2.Core/Model/Element.cs`・`Sheet.cs`・`Device.cs`、
  `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（`DrawImages`実在確認）、
  `src/Ecad2.Core/Persistence/`配下Glob（検索・クリップボード・オートセーブ相当クラスの不在確認）、
  `src/Ecad2.App/MainWindow.xaml`・`MainWindow.xaml.cs`（`ConfirmDiscardIfDirty`・`SaveDocumentAs`・
  `CanvasScale`・`ContextMenu`実装箇所）、`src/Ecad2.App/Views/SheetSettingsDialog.xaml`、
  `src/Ecad2.App/ViewModels/DeviceTableViewModel.cs`
- `docs/archive/ecad2-ui-ux-inventory.md`（忍者、2026-07-03、出発点）
- `docs/ecad2-t060-pdf-ui-wiring-survey-onmitsu2.md`・`docs/ecad2-t061-testmode-ui-wiring-survey-onmitsu2.md`・
  `docs/archive/ecad2-t056-grid-toggle-proposals-onmitsu2.md`（本セッション既実施分）

## 不明点

- 34（F2コメント編集）・35（PageUp/PageDown）・36（エラー表示の徹底度）・37（プロパティパネルの
  動的組み立て有無）・39（シート名変更の方式）はecad2側の実装詳細を本調査で特定できず。

## 派生提案の有無

なし（家老采配の範囲内で完結）。
