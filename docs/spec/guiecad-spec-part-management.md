# GuiEcad仕様書：部品選択・自作パーツ管理

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）の部品選択・自作パーツ管理実装をExplore委譲調査で纏め、
`docs/spec/ecad2-spec-part-management.md`（ecad2側、T-075起票）と比較可能な形で整理する。

対応するecad2側仕様書：`docs/spec/ecad2-spec-part-management.md`

---

## 1. 自作パーツの追加方法（ecad2と最大の差分）

**ecad2と異なり、GuiEcadにはアプリ内作成・編集UIが存在する。** 独立ウィンドウ`PartEditorWindow`
（`GuiEcad.App/PartEditorWindow.xaml.cs`、全956行）が描画ツール一式を備える：描画ツール
（select/line/polyline/rect/circle/arc/text/port、35行）、回転ツール（15度スナップ）、
Undo/Redo（`Stack<EditorSnapshot>`2本、55-56行）、ズーム/パン、テンプレート読込
（`BasicPartTemplates.All()`）、保存時バリデーション（名前必須・ポート2個未満エラー、
`NonSimulated`役割は例外、923-928行）。

呼び出し口は「図形(G)」メニューと左パレット「その他▼」の2系統（両方とも配線済み）：
「自作図形を作成...」（`OnCreateFolderPart`）／「自作図形を読み込んで編集...」
（`OnLoadAndEditPart`）／各自作図形サブメニューの「編集...」（`OnEditFolderPart`）・「削除」
（`OnDeleteFolderPart`、確認ダイアログ付き）——いずれも`MainPage.Parts.cs`。

**死んだコード**：`OnCreatePart`/`OnEditPart`/`OnDeletePart`/`BuildPartSubMenu`
（`MainPage.Tools.cs:116,132,139,218`）はドキュメント埋め込みライブラリのみを対象とする旧設計の
名残で、いずれもクリックハンドラとして参照されず`BuildPartSubMenu`自体も呼び出し元0件——
フォルダストア連動版（`OnCreateFolderPart`等）に完全移行済み。

---

## 2. パーツファイル形式・保存先

- `.gcadpart`（単体）：`PartLibrarySerializer.SerializeOne`が`PartDefinition`をそのままJSON化。
  `SchemaVersion`を持たない設計（ecad2と共通）。
- `.gcadparts`（複数一括）も存在し「エクスポート/インポート」メニューで使用
  （`MainPage.Parts.cs:321-389`）——**ecad2にこの機能に相当する記述はなく、GuiEcad固有**。
- `PartDefinition`：`Id`（既定`Guid.NewGuid()`）、`Name`、`WidthCells`/`HeightCells`、`Role`
  （`PartRole` enum）、`Ports`、`Primitives`。**ecad2にある`IsOrEligible`フラグはGuiEcad側に
  存在しない**（フィールド0件）。
- 保存先：`マイドキュメント\GuiEcad\図形`（自作サブフォルダ`図形\自作\`）——ecad2の
  `マイドキュメント\Ecad2\図形\自作\`とフォルダ名以外の命名・配置規約は同一。
- `SeedBasics()`（基本図形の実フォルダシード）は定義済みだが呼び出し0件、コメント
  「基本図形の実フォルダへのシードは廃止」と整合するデッドコード。

---

## 3. ID重複時の扱い（ecad2のT-035相当機構が存在しない）

**ecad2のT-035再採番ロジック（`CreationTimeUtc`昇順走査＋重複検出＋`Guid.NewGuid()`再採番＋
書き戻し＋`TraceLog`記録）に相当する処理はGuiEcad側に一切存在しない。**

`PartFolderStore.Enumerate()`は`Category`→`Name`の`OrderBy`のみで、ID重複チェック・再採番ロジック
はコード上0件。メニュー構築時`_folderPartMap[e.Definition.Id]=e`という単純辞書代入で、同一Idが
複数あれば列挙順で後勝ちとなるだけ、警告・ログ・再採番は一切行われない。

ただし新規パーツは`PartEditorWindow`コンストラクタで常に`Guid.NewGuid()`を採番するため、**アプリ内
エディタ経由の通常操作ではID重複はほぼ発生しない**。ecad2が問題視するID重複（Explorerでの手動
コピー等）は理論上GuiEcadでも起こりうるが、対処コードを持たない。

---

## 4. サムネイル生成（存在しない）

**GuiEcadには自作パーツ用のサムネイル生成機能が存在しない。** ecad2の`PartThumbnailRenderer`に
相当するクラス・処理は0件。「図形(G)」/「その他▼」メニューの自作パーツ項目はテキストのみの
`MenuFlyoutItem`。

唯一画像アイコンが使われるのは縦ツールパレットの固定11種（a接点・b接点等）で、これは
`SvgRenderer.GenerateSymbolSvg(kind,color)`による**`ElementKind` enumベースの組込み記号アイコン**
（`PartDefinition`＝自作パーツを元にした動的サムネイルではない）。

---

## 5. 部品選択UI構成

「縦ツールパレット」＋「図形(G)メニュー」の2系統（`guiecad-spec-menu-toolbar.md`既出と整合）。

- 縦ツールパレット：固定11項目のRadioButton（画像アイコン付き）＋画像挿入ボタン＋「その他▼」
  DropDownButton。ドック⇄フロート切替・上下端吸着可能。
- 「その他▼」Flyout動的内容：組込み記号10種＋組込みパーツ（EmbeddedResource）＋ピン留め済み
  自作図形＋「自作図形」サブメニュー＋作成/インポート/エクスポート。
- 「図形(G)」メニューは「その他▼」とほぼ同一内容をメニューバーに複製。

いずれもサムネイル・アイコンは持たずテキストラベルのみ。ecad2の`PartSelectionList`（ListBox、
24x24 Image＋Category＋DisplayName）のような専用パレットはGuiEcadに存在しない。

---

## 6. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | 出典 | 備考 |
|---|---|---|
| 自作パーツ作成・編集用専用エディタウィンドウ`PartEditorWindow` | `PartEditorWindow.xaml.cs`全体 | ecad2はCore層完備・App層未結線（T-068未着手） |
| メニューからの直接削除（確認ダイアログ付き） | `MainPage.Parts.cs:282-303` | ecad2は削除UIなし |
| ピン留め機能のUI結線（トグル・専用メニュー行・直接配置） | `MainPage.Parts.cs:168-171,176-208` | ecad2は`PinnedPartStore`実装済みだがUI結線ゼロの孤立クラス |
| `.gcadparts`（複数一括）のエクスポート/インポートUI | `MainPage.Parts.cs:321-389` | ecad2に対応UIなし |
| `.gcadpart`単体を開いて既存パスへ上書き編集する専用フロー | `MainPage.Parts.cs:306-319` | ecad2に対応なし |

### (2) ecad2のみにある機能

| 機能 | ecad2側出典 | GuiEcad側の状況 |
|---|---|---|
| サムネイル自動生成（`PartThumbnailRenderer`、24x24表示） | `ecad2-spec-part-management.md`3節 | 相当機能なし（4節参照） |
| ID重複検出・`CreationTimeUtc`昇順再採番・`TraceLog`記録（T-035） | 同2節 | 重複検出コード自体が0件（3節参照） |
| OR論理エントリ専用サムネイル描画（`IsOrEligible`ベース） | 同3節 | `PartDefinition`に`IsOrEligible`フィールドなし |

### (3) 両方にあるが挙動が異なる点

| 項目 | ecad2 | GuiEcad |
|---|---|---|
| 自作パーツ追加経路 | Explorerで`.gcadpart`直接配置が唯一の実運用経路 | アプリ内`PartEditorWindow`が正規経路（旧「ドキュメント埋め込み」版は死んだコード） |
| ID重複時の挙動 | 検出→最古優先で再採番→書き戻し→ログ記録 | 検出・再採番ロジック自体が存在せず、後勝ち上書きのみ |
| サムネイル/アイコン | 全パーツに24px角サムネイル自動生成 | 固定11種のみSVGアイコン、自作パーツはテキストラベルのみ |
| ピン留め機能 | 実装済みだがUI結線ゼロ | 実際にメニューへ結線済み |
| 自作パーツのファイル削除 | 削除UIなし | メニューから確認ダイアログ付きで削除可能 |

---

## 出典

- GuiEcad: `PartEditorWindow.xaml.cs`（全956行）、`MainPage.Parts.cs:56-389`、`MainPage.Tools.cs:56-215`、
  `GuiEcad.Core/Model/PartDefinition.cs:12-46`、`GuiEcad.Core/Persistence/PartFolderStore.cs:16-105`、
  `GuiEcad.Core/Persistence/PartLibrarySerializer.cs:57-65`（Explore委譲調査、行番号は本文各所参照）
- ecad2: `docs/spec/ecad2-spec-part-management.md`（比較対象）

## 不明点

- `PartResolver`のフォールバック挙動（未解決PartId→組込み記号）がecad2の同種挙動と一致するかは
  ecad2側実装を本調査で読んでいないため比較不可。
- エクスポート/インポート機能がecad2に本当に存在しないか（Core層`PartLibrarySerializer.Save/Load`
  自体はecad2にも存在しうる）はecad2側App層を読んでいないため未確認。
- 縦ツールパレットのシート種別依存排他制御差異は未確認（`guiecad-spec-menu-toolbar.md`既出の
  不明点と同様、持ち越し）。
