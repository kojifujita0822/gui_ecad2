# T-043 往復1周目修正レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット `de93462`（`fix(app): T-043往復1周目 - ORサムネイル判定を
> Id完全一致からIsOrEligible/Roleベースへ`）。隠密レビュー（`docs/archive/ecad2-t043-review-onmitsu.md`所見1、
> CONFIRMED）指摘の退行への対応。家老指定観点(1)〜(4)＋`code-review`スキル（medium、統合3角度→
> 1-vote verify）併用。

---

## 結論：**指摘した退行(PartThumbnailRenderer側)は正しく解消。ただし同一クラスの弱点がConverter側に
残存していることを新たにCONFIRMED。要修正候補あり、忍者確認前に一考を推奨**

家老指定観点(1)〜(4)はいずれも問題なし。ただし`code-review`スキルのAltitude角度finderが提起し、
verifyエージェントがCONFIRMEDと判定した新規所見（下記所見1）がある：**`PartEntryToGlyphGeometryConverter`
にも、今回修正したのと全く同じ「Id完全一致判定」の弱点が残っており、コピー由来パーツで配置バー本体の
表示（サムネイルより広い影響範囲）が汎用フォルダアイコンに落ちる**。今回のコミット（de93462）は
`PartThumbnailRenderer`のみを修正し、`Converter`側は対象外だったため、この既存の弱点は未対処のまま。

---

## 対象差分

`git show de93462`で確認。`PartThumbnailRenderer.cs`のRenderシグネチャをstring partIdから
`PartDefinition definition`へ変更し、判定を`definition.IsOrEligible && definition.Role`ベースへ変更。
呼び出し元`PartPaletteViewModel.cs`2箇所も追随。新規テスト2件（`PartThumbnailRendererTests.cs`）追加。

---

## 家老指定観点の検証

### (1) 指摘した退行の解消 —— **解消を確認**

`IsOrEligible`と`Role`はId再採番の影響を受けない（`PartFolderStore.Enumerate()`の再採番はIdのみ書き
換え、`IsOrEligible`/`Role`等は維持）ため、コピー由来のパーツでも正しくOR判定される。新規テスト
`Render_IdReassignedCopyOfContactNo_StillRendersOrGlyph`の実効性も確認した：`copy`はRole/IsOrEligibleを
originalと同一に保ちIdのみ変更しており、T-035の再採番シナリオを正確に再現している。旧Id完全一致判定
なら本テストは失敗するはずのケースであり、テストとして機能する。

### (2) IsOrEligible&&Role判定でのセレクトSW巻き込み再発なし —— **問題なし**

`BasicPartTemplates.cs:127`でSelectSwitchは`Role = PartRole.ContactNO`だが`IsOrEligible`は明示未設定
（デフォルトfalse）。Role単独判定なら誤ってORaグリフに巻き込まれる（T-037再現条件）が、新判定は
`IsOrEligible`ゲートで正しく弾く。新規テスト`Render_SelectSwitch_NeverRendersOrGlyphEvenIfIsOrPassedTrue`
もこれを正しく検証している。

### (3) シグネチャ変更の呼び出し元追随漏れ —— **なし**

`PartThumbnailRenderer.Render`の呼び出し元をリポジトリ全体（src/tests）でGrepし、`PartPaletteViewModel.cs`
の2箇所（51/61行目）以外に呼び出しがないことを確認。両方とも`entry.Definition`（PartDefinition型）を
正しく渡している。

### (4) 便乗変更なし・退行なし —— **便乗変更なし。ただし既存の同種弱点が新たに判明（下記）**

diff自体に意図した範囲外の変更はない。ただし、code-reviewのAltitude角度finderが「この修正は同一クラスの
退行パターンに対する部分修正（点修正）に留まっている」と指摘し、verifyで実害をCONFIRMEDした（所見1）。

---

## `code-review`スキル併用の追加所見

### 所見1（CONFIRMED・要判断）: `PartEntryToGlyphGeometryConverter`にも同種の退行が残存（サムネイルより広い影響範囲）

**メカニズム**：`PartEntryToGlyphGeometryConverter.Convert`は`(entry.Definition.Id, entry.IsOr)`の
タプルswitchで、`BasicPartTemplates.ContactNOId`等の固定文字列Idとの完全一致判定を継続している。もし
ユーザーがExplorerで基本図形（a接点.gcadpart等）をコピーし、`PartFolderStore.Enumerate()`のId重複検出
により新Idが再採番された場合、このConverterの`switch`は**OR版だけでなく非OR版（通常のコピー）も含めた
全caseで一致せず**、`_ => Custom`（汎用フォルダアイコン）に落ちる。

**影響範囲**：verifyエージェントが確認した通り、このConverterは配置バーの種別選択コンボボックス
（`PlacementPartComboBox`）の`ItemTemplate`で使われており、「閉状態・ドロップダウン項目とも同一
ItemTemplate」のため、**ドロップダウン展開時だけでなく、閉じた状態のコンボボックス本体の表示にも
影響する**。`PartThumbnailRenderer`側の退行（サムネイルのみ、DisplayNameテキストは正しいまま）より
実害が大きく、配置バーで部品を選ぶ操作そのものに影響する。

**再現手順**：(1)「図形」フォルダ内でa接点.gcadpart等をExplorerでコピー (2)アプリ起動→
`Enumerate()`がId重複検出、コピー側に新GUID Idを再採番して書き戻す (3)配置バーの種別選択コンボ
ボックスに、コピー由来のエントリ（DisplayNameは正しいまま）が汎用フォルダアイコンとして表示される
（閉状態・展開時とも）。

**今回のコミットでの対処範囲**：`git show --stat de93462`で確認した変更ファイルは
`PartPaletteViewModel.cs`・`PartThumbnailRenderer.cs`・新規テスト1件のみ。`PartEntryToGlyphGeometryConverter.cs`
は変更に含まれておらず、今回未対処のまま残存している。

**位置づけ**：これは今回のコミット（de93462）が新たに持ち込んだ退行ではなく、既存（増分4・5由来）の
弱点がConverter側に残ったまま、という状況。ただし前回の隠密所見（`docs/archive/ecad2-t033-review-onmitsu-7.md`
所見1、`docs/observations.md`#17）は「グリフPath Dataの二重/三重ハードコード」という重複の観点であり、
「Id再採番でコピー由来パーツの表示が壊れる」という具体的な退行シナリオへの言及はしていなかった。今回の
verifyで新たに明確になった実害である。

**対処の方向性（参考、実装は侍判断）**：`PartEntryToGlyphGeometryConverter.Convert`の判定も、
`PartThumbnailRenderer`と同様に`IsOrEligible`/`Role`ベースへ置き換えることで解消できる可能性が高い。
ただし、コンバータには現状`PartSelectionEntryViewModel`が渡されており、`Definition.IsOrEligible`/
`Definition.Role`へのアクセス自体は可能なはずなので、シグネチャ変更は不要かもしれない（`value is
PartSelectionEntryViewModel entry`のまま、`entry.Definition.IsOrEligible`/`entry.Definition.Role`で
判定すればよい）。

### 検討したが不採用の候補

- ネストif文のswitch式化（Simplification）：フォールスルーの都合上switch式化は却って不自然、修正不要。
- テストのカバレッジ薄さ（IsOrEligible=trueだがRole非対象のケースが未検証）：現状の部品構成では発生
  しないため実害なし、severity低。
- Efficiency/Conventions角度：該当なし。

---

## 忍者への申し送り

- 家老指定の軽い差分確認（ORa/ORbサムネイル見た目・他5種回帰）は予定通り進めてよい。所見1（コピー由来
  パーツの配置バー表示退行）は通常検証範囲外のため、確認は不要（家老・侍判断待ち）。

---

## 出典・参照

- 対象コミット `de93462`（`git show`で全差分確認）
- `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`
- `src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`
- `src/Ecad2.Core/Persistence/PartFolderStore.cs`（Id重複検出・再採番ロジック）
- `src/Ecad2.App/MainWindow.xaml`（`PlacementPartComboBox`定義）
- `tests/Ecad2.App.Tests/PartThumbnailRendererTests.cs`（新規回帰テスト2件）
- `docs/archive/ecad2-t033-review-onmitsu-7.md`（グリフPath Data重複の前回所見）・`docs/observations.md`#17
- `docs/archive/ecad2-t043-review-onmitsu.md`（T-043初回レビュー、所見1のCONFIRMED退行）
- `code-review`スキル（medium、統合3角度→1-vote verify、CONFIRMED1・該当なし多数）
