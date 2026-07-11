# T-033 増分5 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット `d089f88`（`feat(app): T-033増分5 - 種別ドロップダウンを
> ORa/ORb込み7種へ統一・表示どおりの動作へ`）。家老指定観点(a)〜(f)＋`code-review`スキル（medium、
> 統合5角度→1-vote verify）併用。T-037暗黙保持ルール廃止という削除系変更を含むため入念に検証した。

---

## 結論：**クリーン。忍者確認（OR配置の実動作込み）へ回してよい**

---

## 対象差分

`git show d089f88`で確認。3ファイル+50/-41。
- `PartEntryToGlyphGeometryConverter.cs`：ORa/ORb用グリフ（`OrContactNo`/`OrContactNc`）追加、
  判別キーを`Definition.Id`単独から`(Definition.Id, IsOr)`のタプルへ変更。
- `MainWindow.xaml`：ComboBoxの`ItemsSource`を`PartPalette.Entries`から`PartPalette.SelectionEntries`
  （T-037で既に導入済みの7種、ORa/ORb込み）へ変更。ToolTip/AutomationProperties.Nameを
  `Definition.Name`から`DisplayName`へ変更。
- `MainWindow.xaml.cs`：`_placementIsOr`フィールドを完全削除。`TryPlaceElement`の初期選択ロジックを
  3段階フォールバックへ変更。`PlacementOkButton_Click`の`effectiveIsOr`算出を`entry.IsOr`直参照へ単純化。

---

## 家老指定観点の検証

### (a) 7種の並び・ORa/ORbグリフの一致 —— **確認済み**

`SelectionEntries`は`PartPaletteViewModel`が一元管理し、右パネルの部品選択リストと配置バーの両方が
同一リストを参照するため、並びは構造的に一致する。ORa/ORbグリフ（`OrContactNo`/`OrContactNc`）の
Path Dataを、ツールバーのsF5/sF6ボタン（`MainWindow.xaml`214/234行目）のPath Dataと逐一比較し、
**両方とも文字列として完全一致**することを確認した。

### (b) 初期選択ロジックの正しさ —— **正しい**

`TryPlaceElement`への呼び出し元は3箇所（`TryPlaceActiveTool`・`TryPlaceBuiltin`・
`PartSelectionItem_Clicked`）のみで、`code-review`のcross-file角度finderが全経路を追跡した。
sF5/sF6は`Window_PreviewKeyDown`のswitch文からコードビハインドで直接`TryPlaceBuiltin(..., isOr: true)`
を呼んでおり、`TryPlaceElement`内の`SelectionEntries.FirstOrDefault(e => e.Definition.Id ==
initialEntry.Definition.Id && e.IsOr == isOr)`という第1段階マッチで正しくOR版が初期選択されることを
確認した。`PartSelectionItem_Clicked`経由（`entry.IsOr`をそのまま渡す）は、entry自身が
`SelectionEntries`の要素のため第1段階が自己一致し、常に成立する。

### (c) T-037暗黙保持ルール廃止の完全性 —— **完全（削除系変更として特に入念に確認）**

Grepで`_placementIsOr`をリポジトリ全体検索した結果、実コードでの残存はゼロ。唯一のヒットは
`MainWindow.xaml.cs:677`のコメント内（旧ロジックの説明として意図的に残した記述）のみ。フィールド定義・
代入・`ClosePlacementBar`でのリセットのいずれも完全に削除されている。

**むしろ新設計の方が構造的に堅牢**であることを確認した：`PartPaletteViewModel`コンストラクタ
（60-61行目）で、`IsOr=true`のエントリは`Entries.Where(e => e.Category == "" &&
e.Definition.IsOrEligible)`というフィルタを通過したパーツからしか生成されない。つまり「OR対象外
（セレクトSW・端子台等）はIsOr=falseのエントリしか存在しない」という不変条件が、旧実装のように
「実行時にORを打ち消す」防御ではなく、**リストのデータ生成側で構造的に保証される**設計に変わっている。

`tests/Ecad2.App.Tests/`配下にも`_placementIsOr`や旧暗黙保持ロジックに依存する退行テストは存在しない
ことを確認した。

### (d) 非接点系選択時のisOr自然無効化 —— **確認済み**

上記(c)の通り、コイル・端子台・セレクトSW等は`IsOr=false`のエントリのみが`SelectionEntries`に存在する
ため、`effectiveIsOr = entry.IsOr`は自然にfalseになる。

### (e) UIA名/ToolTipのDisplayName正式名称 —— **確認済み**

ComboBox自体（`SelectedItem.DisplayName`）・ItemContainerStyle（`Binding DisplayName`）とも
`PartSelectionEntryViewModel.DisplayName`（IsOr時「OR」+Definition.Name、例：「ORa接点」）へ変更済み。

### (f) 便乗変更なし・増分1設計堅持 —— **問題なし**

意図した範囲外の変更はなし。`IsPlacementBarVisible`単一情報源等の増分1設計への介入もなし。

---

## `code-review`スキル併用の追加所見

### 所見1（PLAUSIBLE・修正不要・記録のみ）: OR版エントリ欠落時の静かなフォールバック

`TryPlaceElement`の第2段階フォールバック（`Id`一致のみ、`IsOr`不問）は、理論上「isOr=trueで呼ばれたが
対応するOR版エントリが`SelectionEntries`に存在しない」場合、警告なしに非OR版を初期選択してしまう
設計になっている。verifyエージェントが検証した結果、現状はこれが**三重に保証されて到達不能**
（`BasicPartTemplates.cs`のハードコード`IsOrEligible=true`、`PartFolderStore.Enumerate()`の
Id一致による自己修復、`SaveCustom`のカテゴリ分離）であり、CONFIRMEDには至らない。ただし、この
フォールバック自体を保護する自動テストは存在せず、将来「新規OR対象パーツ追加時にIsOrEligible設定を
忘れる」等の変更でCONFIRMEDになりうる技術的負債として記録する。改善案（第2段階に落ちた場合の
StatusMessage警告等）は低コストだが、**本増分での修正は不要**と判断する。

### 検討したが不採用の候補

- 3段階フォールバックの2回列挙という軽微な非効率（Reuse+Simplification+Efficiency角度）：リスト
  7件規模で実質無視できる、可読性とのトレードオフを考慮すると現状で問題ない。
- Reuse/Conventions角度：該当なし。

### Altitude角度の所見（参考、修正不要）

verifyエージェントは、この変更を単なる「前回の設計判断を覆した」ものではなく、「状態（初期値の暗黙
保持）を排除し、UIで見えている選択項目の属性=実際に配置される属性、という不変条件を成立させた、
より根本的に正しい設計への改善」と評価した。今後も同種の「殿裁定による仕様巻き戻し」が起こりうるが、
先回りした過剰な柔軟性は不要（YAGNI）で、「選択項目自身が状態を持つ」設計を保つことが今回同様に低
コストな巻き戻しを可能にする、という所感が付記された。

---

## 忍者への申し送り

- OR配置の実動作確認：sF5→行選択→OKで、ORa接点として実際にOR接続処理（基準行判定・縦コネクタ生成）が
  行われること。sF6（ORb接点）も同様。
- 表示どおりの動作確認：ORa接点で開いた状態から、ドロップダウンでb接点（非OR）へ切り替えてOKを押すと、
  ORなしのb接点として配置されること（T-037暗黙保持ルール廃止の実機確認、殿裁定の意図通りか）。
- グリフの見た目確認：ORa/ORbのシンボルがsF5/sF6ツールバーボタンと同じ意匠に見えること。

---

## 出典・参照

- 対象コミット `d089f88`（`git show`で全差分確認）
- `src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`
- `src/Ecad2.App/MainWindow.xaml`（200-270/480-521行目）
- `src/Ecad2.App/MainWindow.xaml.cs`（220-230/540-630/670-700行目）
- `src/Ecad2.App/ViewModels/PartPaletteViewModel.cs`・`PartSelectionEntryViewModel.cs`
- `src/Ecad2.Core/Persistence/BasicPartTemplates.cs`
- `docs/ecad2-t033-review-onmitsu-7.md`（前回レビュー、増分3/4）
- `code-review`スキル（medium、統合5角度→1-vote verify、PLAUSIBLE1・該当なし多数）
