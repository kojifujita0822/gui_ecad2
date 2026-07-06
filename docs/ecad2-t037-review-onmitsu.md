# T-037 部品選択リストへORa/ORb追加(案A) レビュー（隠密・静的レビュー）

対象: コミット`8ebc05d`（`feat(app): T-037 - 部品選択リストへORa/ORb追加(案A、サムネイルORバッジ)`）。
変更ファイル: `src/Ecad2.App/{MainWindow.xaml, MainWindow.xaml.cs, ViewModels/PartPaletteViewModel.cs,
ViewModels/PartSelectionEntryViewModel.cs}`・`src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`。

家老委任の5観点＋`code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）を併用。

---

## 総評

**要修正級の指摘2件（正確性バグ）と、改善価値の高い指摘1件（設計）を発見した。** うち正確性バグ
2件は、隠密の手動レビューと`code-review`のAngle A（line-by-line diff scan）が完全に独立に
到達した同一結論であり、確度が高い。

---

## 家老の5観点への回答

### 観点1: isOrが配置経路（縦コネクタ生成）まで正しく伝搬するか

**通常経路は伝搬するが、ダイアログ内でパーツ種別を変更した場合に不整合が生じる（CONFIRMED）。**

- `PartSelectionItem_Clicked`（`MainWindow.xaml.cs`562-566行目）→`TryPlaceElement(entry.Entry,
  entry.IsOr)`→`PlaceElementAtSelectedCell(partId, deviceName, isOr)`（600行目）→縦コネクタ生成
  （`MainWindowViewModel.cs`341-359行目）という伝搬チェーン自体は正しく実装されている。
- **しかし`ElementPlacementDialog`（594行目で開く）のPartComboBoxは`_viewModel.PartPalette.Entries`
  （全パーツ種別、フィルタなし）を持つため、ユーザーがダイアログ内で別パーツに切り替えられる。**
  `OkButton_Click`（`ElementPlacementDialog.xaml.cs`）は`SelectedPartId`をコンボボックスの現在
  選択値から取得するのみで`isOr`には一切関知しない。`TryPlaceElement`600行目は、`partId`は
  新しい選択、`isOr`はクリック時の初期値のまま、という不整合な組み合わせで呼び出す。
- **失敗シナリオ**: 部品選択リストで「ORa接点」（IsOr=true）をクリック→ダイアログ内で「端子台」
  等の非OR要素に切り替えてOK→端子台なのにOR接続の縦コネクタが誤生成される。

### 観点2: DisplayNameがT-031の日本語ラダー用語規約に沿うか

**妥当（事実）。** `DisplayName => IsOr ? "OR" + Definition.Name : Definition.Name;`により
「ORa接点」「ORb接点」となり、コミットメッセージが参照する design-brief 11節の命名規則
（ツールバーのShift+F5/F6と同じ用語）と一致する。code-reviewでも違反の指摘なし。

### 観点3: Entries/SelectionEntries 1:1前提崩れの実害有無（過去所見の再確認）

**実害なし（確認済み）。** `code-review`のAngle B（removed-behavior auditor）が`Entries`・
`SelectionEntries`の全利用箇所をGrepし、両者のインデックス対応や要素数一致を前提にしたコードが
他に存在しないことを確認した。`ElementPlacementDialog`は`Entries`（1:1対応が保たれている方）
のみを使い、`PartSelectionItem_Clicked`も`entry.Entry`/`entry.IsOr`を直接参照するため、
1:1対応の崩れ自体に起因する実害は無いと判断する（隠密の過去所見どおり）。

### 観点4: ORバッジ描画のRendering.Wpf層配置の妥当性

**妥当（事実）。** `PartThumbnailRenderer`は既存の設計上もUI層（App）から呼ばれるサムネイル
生成専用ヘルパーであり、ORバッジ（表示装飾）の追加はこの既存パターンの延長として自然。
`FormattedText`のコンストラクタも`pixelsPerDip`引数を含む非推奨でないオーバーロードを使用
しており、API的な問題もない（Angle A確認済み）。

### 観点5: 自作パーツ項目への波及なし

**基本的に波及なし、ただし関連する重要な指摘あり（CONFIRMED）。** `PartPaletteViewModel.cs`の
追加ループは`Category == ""`（ルート直下＝基本図形のみ）かつ`Name`一致の条件で、自作パーツ
（Category != ""）は対象外。**ただし、このName一致というフィルタ方式自体に、T-035で発見済みの
「ファイルコピーによる同名重複パーツ」問題との相互作用がある**（詳細は次項）。

---

## 発見事項

### [CONFIRMED・要修正] ダイアログ内でのパーツ種別変更でisOr不整合
上記観点1参照。`TryPlaceElement`が`isOr`を固定値として保持したままダイアログを開き、ユーザーの
ダイアログ内選択変更を反映しない設計。**verdict: CONFIRMED**（隠密手動確認＋`code-review`
Angle A独立確認、実装追跡で成立を確認済み）。

### [CONFIRMED・要修正] Name一致によるOR対象判定が同名重複パーツで破綻する
`PartPaletteViewModel.cs`53行目`Entries.Where(e => e.Category == "" && (e.Definition.Name ==
"a接点" || e.Definition.Name == "b接点"))`は`Definition.Id`の一意性を見ずName文字列のみで判定
する。T-035（`docs/ecad2-t035-review-onmitsu.md`）で明らかになった「ファイルコピーで
`Id`は重複検出・再採番されるが`Name`は変更されずそのまま残る」という挙動と組み合わさると、
図形/直下に同名"a接点"の複製（Idは異なる）が存在する場合、この`Where`が両方にヒットし、
部品選択リストに「ORa接点」が重複表示される。**verdict: CONFIRMED**（コードロジックの追跡で
成立を確認済み）。

### [CONFIRMED・改善推奨] Name一致は既存の型安全な代替（PartRole）を使わない不必要に脆い設計
上記2件目と根は同じだが、より根本的な指摘。`PartDefinition`には既に`Role`という強く型付けされた
enum（`PartRole.ContactNO`/`ContactNC`）が存在し、`BasicPartTemplates.ContactNO()`/`ContactNC()`
で`Name`と併せて正しく設定済み。`PartPaletteViewModel.cs`53行目は
`e.Definition.Role == PartRole.ContactNO || e.Definition.Role == PartRole.ContactNC`という
同義かつ堅牢な代替手段を使わず、あえて文字列完全一致を採用している。**修正コストは1行の
書き換えのみで、T-037の制約（Core層無変更）にも抵触しない。** この修正により、上記の
「Name重複による誤表示」問題も同時に解消される（`Role`は`Id`と同様にファイルコピーで
複製されても値は変わらないが、少なくとも文字列表記ゆれ・多言語化・リネームには強くなる。
ただし同名Idコピー問題自体の根本解消ではない点は留意）。**verdict: CONFIRMED**。

### [PLAUSIBLE・軽微] サムネイル生成コードの重複
通常エントリ生成とOR論理エントリ生成で`PartThumbnailRenderer.Render`呼び出し＋
`PartSelectionEntryViewModel`構築のパターンが2箇所に重複。ただし制御構造（`Select` vs
`foreach+Add`）が異なり、コンストラクタ全体もわずか27行と軽微なため、今すぐ対応すべき水準
ではないと判断する。

---

## 結論・提案

要修正2件（isOr不整合・Name一致重複）は、いずれもユーザーが実際に遭遇しうる具体的な誤動作
（意図しないOR接続の生成、部品選択リストの重複表示）につながるため、往復対応を推奨する。
`PartRole`活用への切り替え（1行修正）は、Name重複問題の緩和と保守性向上を同時に達成できる
低コストな対応として合わせて推奨する。isOr不整合は別途、ダイアログのOK確定時に選択パーツの
種別とisOrの整合性を検証する対応が必要。

---

## 出典
- コミット`8ebc05d`（`git show`・`git diff 8ebc05d~1..8ebc05d`で全文確認）
- `code-review`スキル（mediumレベル、8角度×並列finder→1-vote verify）
- `docs/ecad2-t035-review-onmitsu.md`（Name重複問題の背景）
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`（`PartRole`設定済みの実装確認）
- `src/Ecad2.App/Views/ElementPlacementDialog.xaml.cs`・`MainWindowViewModel.cs`（isOr伝搬経路の追跡）
