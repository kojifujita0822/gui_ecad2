# T-043 往復2周目修正レビュー（隠密、修正上限）

> 2026-07-07 隠密レビュー。対象コミット `e7e03c2`（`fix(app): T-043往復2周目 - Converter側の同族Id
> 完全一致退行をCategory/Role/IsOrEligibleベースへ`）。隠密指摘（`docs/ecad2-t043-review-onmitsu-2.md`
> 所見1、CONFIRMED）への対応。家老指定観点(1)〜(5)＋`code-review`スキル（medium、統合4角度→
> 1-vote verify）併用。**上限消化ゆえ最大限慎重に検証した。**

---

## 結論：**クリーン。忍者の軽い差分確認へ回してよい**

指摘した退行（Converter側のId完全一致判定によるコピー由来パーツの表示崩れ）は正しく解消された。
`code-review`スキルの検証で、その解決策（`Category==""`ゲート）自体が2つの新しい軽微な副作用を
生んでいることをCONFIRMEDしたが、いずれも**表示（グリフ）のみに限定され、配置される部品の実データ・
電気的挙動には一切影響しない**。トリガー条件もアプリのUI操作範囲外（Explorerでの直接ファイル操作）
であり、severityは低い。往復3周目を要求するほどの重大性はないと判断し、経過観察として記録するに留める。

---

## 対象差分

`git show e7e03c2`で確認。`PartEntryToGlyphGeometryConverter.cs`（+29/-10）。判定を
`Definition.Id`完全一致から`Category==""` && `(Role, IsOrEligible, IsOr)`ベースへ変更。新規テスト4件。

---

## 家老指定観点の検証

### (1) Converter側弱点の解消 —— **解消を確認**

`IsOrEligible`/`Role`はId再採番の影響を受けないため、コピー由来のパーツ（OR/非OR問わず）も正しく個別
グリフを返す。新規テスト4件（`Render_IdReassignedCopyOfContactNo`等）の実効性を検証：いずれも「旧Id
完全一致判定なら失敗し、新判定なら成功する」退行シナリオを正確に再現している。`Assert.Same`による
同一Geometryインスタンス比較も、Converterの`static readonly Geometry`実装と整合し妥当。

### (2) Category==""ゲートの正しさ —— **概ね正しいが、2つの副作用をCONFIRMED（下記所見参照）**

- 基本図形の**同一フォルダ内コピー**では`Category=""`が維持されることを`PartFolderStore.Enumerate()`
  の`Path.GetRelativePath`計算から確認した（正しい）。
- 自作パーツ（`SaveCustom`は`CustomDir`固定書き込み）の非巻き込みは構造的に保証されている（正しい）。
  新規テスト`GetGlyph_CustomPartWithBuiltinLikeRole_DoesNotUseBuiltinGlyph`も、Categoryゲートが実際に
  効いていることを正しく検証している（偶然通っているのではない）。
- ただし、Categoryは「ファイルの物理的配置場所」のみで機械的に決まる値であり、「公式の基本図形か」の
  専用マーカーではない。この性質により2つの副作用が生じる（所見1・所見2）。

### (3) `(PartRole.ContactNO, false, _) => SelectSwitch`の一意性 —— **現状は正しいが、将来リスクあり**

現在の5種の基本図形（`BasicPartTemplates.All()`）のRole/IsOrEligible組み合わせは全て一意で、重複・
取りこぼしはない。ただしこの一意性はコード上で強制されておらず、将来6種目の基本図形を追加する際、
既存の組み合わせ（特にRole=Coil/Terminalは`IsOrEligible`をワイルドカード`_`扱い）と衝突しないよう手動
確認が必要（`code-review`のAltitude角度指摘、技術的負債として記録）。

### (4) テスト実効性 —— **確認済み、妥当**

4件のテストはいずれも意図通り機能する。`PartFolderEntry`コンストラクタ呼び出しの引数順も定義と一致。

### (5) 便乗変更・退行なし —— **便乗変更なし。ただし新規の軽微な副作用2件を所見として記録**

---

## `code-review`スキル併用の追加所見

### 所見1（CONFIRMED・severity低）: サブフォルダ移動でConverter/Thumbnailの表示が不一致になる

verifyエージェントがコードを追跡した結果：基本図形（a接点等）をExplorerで**サブフォルダへ移動**（コピー
ではなく移動、Idは変わらない）すると、`Category`が`""`以外になる。Converter側（配置バーComboBox）は
`Category != ""`ゲートで通常版（isOr=false）も含めてCustom（汎用フォルダアイコン）に落ちる。一方
`PartThumbnailRenderer`（右パネルサムネイル）はCategoryを一切参照せず、`isOr=false`の場合は常に
`DrawPreview`で実形状を描画するため、正しいa接点シンボルのまま表示され続ける。結果、**同一パーツが
右パネルでは正しい形状・配置バーでは汎用アイコン、という表示不一致**が生じる。

旧Converter（e7e03c2の親、`d089f88`時点）はId完全一致のみでCategoryを見ておらず、単純移動（Id不変）
ではこの問題は起きなかった。**この副作用はe7e03c2が`Category==""`ゲートを導入したことで新規に生じた
もの**であり、de93462由来の設計差の踏襲ではない。ただし実害は「配置されるデータ・電気的挙動には一切
影響しない、表示スタイルの不一致のみ」（`PartSelectionEntryViewModel.Entry`自体は変わらず正しいまま）。
トリガー条件（アプリ内に移動UIはなく、Explorerでの外部操作＋再起動が必要）も低頻度。

### 所見2（CONFIRMED・severity低〜中）: 図形フォルダ直下への直接配置でグリフのなりすましが起こりうる

verifyエージェントが検証：アプリ内には「図形」フォルダ直下（RootDir）へユーザーが直接書き込む正規経路
はない（`SeedBasics`は起動時専用、`SaveCustom`は`CustomDir`固定）。しかし、ユーザーがExplorerで直接
RootDir直下に、`Role`/`IsOrEligible`を省略またはRole=ContactNO・IsOrEligible=false（＝`PartDefinition`
の無指定デフォルト値そのもの、かつSelectSwitchの署名と偶然一致）を持つ自作`.gcadpart`を配置すると、
`Category==""`を満たし、配置バーで「セレクトSW」のグリフとして誤表示される。`PartLibrarySerializer`は
フィールド欠落があってもC#既定値で埋めて正常にデシリアライズを完了させ、例外を投げないためこれを防げ
ない。実害は「配置バーのアイコン表示のみ」で、部品の名前・Ports・Primitives・シミュレーション挙動には
影響しない。この操作は「アプリが一切案内しないRootDirの実パスへ、自作パーツ保存機能を使わず意図的に
踏み込む」という非典型的な逸脱であり、T-043所見1（通常のExplorerコピー）ほど自然発生的ではない。

### 検討したが不採用の候補

- `(ContactNO, false, _) => SelectSwitch`の可読性懸念（コメント不足）：軽微、severity低。
- Efficiency/Conventions角度：該当なし。

### 総合評価（severity判断）

所見1・2はいずれも「表示スタイルの不一致・誤表示」に限定され、データ破損・誤配置・機能停止には至らない。
トリガー条件も通常のアプリ操作範囲外（Explorerでの意図的な直接操作）であり、往復3周目を要求するほどの
緊急性はないと判断する。`docs/observations.md`への経過観察としての記録を推奨する。

---

## 忍者への申し送り

- 家老指定の軽い差分確認（ORa/ORbサムネイル見た目・他5種回帰）を予定通り進めてよい。所見1・2は通常
  検証範囲外（サブフォルダ移動・図形フォルダ直下への直接配置という非典型操作が必要）のため確認不要。

---

## 出典・参照

- 対象コミット `e7e03c2`（`git show`で全差分確認）
- `src/Ecad2.App/Converters/PartEntryToGlyphGeometryConverter.cs`
- `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`
- `src/Ecad2.Core/Persistence/PartFolderStore.cs`（Category計算・SaveCustom・SeedBasics）
- `src/Ecad2.Core/Model/PartDefinition.cs`（Role/IsOrEligibleのデフォルト値）
- `tests/Ecad2.App.Tests/PartEntryToGlyphGeometryConverterTests.cs`（新規回帰テスト4件）
- `docs/ecad2-t043-review-onmitsu-2.md`（往復1周目レビュー、所見1のCONFIRMED退行）
- `code-review`スキル（medium、統合4角度→1-vote verify、CONFIRMED2・該当なし多数）
