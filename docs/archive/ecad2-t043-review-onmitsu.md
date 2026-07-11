# T-043 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット `c56b13c`（`feat(app): T-043 - 部品選択リストのORa/ORbサムネイルを
> ツールバー同意匠へ統一`）。家老指定観点(a)〜(e)＋`code-review`スキル（medium、統合3角度→1-vote verify）
> 併用。

---

## 結論：**要修正候補あり（軽微〜中程度）。忍者の軽い差分確認へ回す前に一考の価値あり**

家老指定観点(a)〜(e)はいずれも問題なし（Id完全一致判定は誤対象を巻き込まない、3箇所のPath Dataは完全
一致、DrawOrBadge削除は完全、5種現状維持・便乗変更なしを確認）。ただし`code-review`スキルのAngle A
finderが提起し、verifyエージェントが実装ロジック（`PartFolderStore`のId重複再採番）と過去コミット
（T-037時点の実装）の両方を精読して**CONFIRMED**と判定した所見が1件ある：**T-043自体が、コピー由来で
Id再採番された接点パーツに対するOR視覚表現の退行を新規に持ち込んでいる**（詳細は所見1参照）。実害は
サムネイルの見た目のみ（`DisplayName`のテキストでは正しく「OR」と表示され続ける）で機能的な誤動作は
ない。忍者確認自体を止める必要はないが、家老・侍の判断で対処するか経過観察に留めるかを一考されたい。

---

## 対象差分

`git show c56b13c -- src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`で確認。1ファイル、+40/-15。
`DrawOrBadge`（OR右下バッジ合成）を削除し、ORa/ORb（`isOr=true`かつ`partId`がContactNOId/ContactNCId）
の場合はツールバーsF5/sF6と同じGX様式グリフを描画する`RenderGlyph`へ置き換え。

---

## 家老指定観点の検証

### (a) ORa/ORb判定方式の誤対象巻き込み —— **問題なし**

`if (isOr && partId == BasicPartTemplates.ContactNOId)`という**Id定数の直接比較**方式であり、T-037で
問題になった「Role判定によるセレクトSW巻き込み」（Role判定は電気的な役割で分類するため、セレクトSWも
接点と同じRoleに分類されて誤って巻き込まれた）は再発しない。`PartPaletteViewModel.cs:60`の
`Entries.Where(e => e.Category == "" && e.Definition.IsOrEligible)`により、`isOr=true`で`Render`へ渡る
`partId`は通常時（ファイルコピー等が絡まない限り）ContactNOId/ContactNCIdのみに限定されることも確認した。

### (b) Path Dataの3箇所一致 —— **完全一致確認**

`PartThumbnailRenderer.cs`の`OrContactNoGlyph`/`OrContactNcGlyph`と、`PartEntryToGlyphGeometryConverter.cs`
の`OrContactNo`/`OrContactNc`、`MainWindow.xaml`のsF5/sF6ボタンのPath Data、計3箇所を文字列として逐一
比較し、両パターンとも完全一致することを確認した。

### (c) 旧DrawOrBadge削除の完全性 —— **完全**

Grepでリポジトリ全体（src/tests含む）を検索し、`DrawOrBadge`メソッド定義・呼び出しとも一切残存していない
ことを確認した。

### (d) ORa/ORb以外5種の現状維持 —— **確認済み（ただし関連する新規懸念あり、下記所見1参照）**

新しい`if`分岐に該当しない場合は従来の`DrawPreview`経路がそのまま実行される。5種（コイル・端子台・
セレクトSW等）は`IsOrEligible=false`のため`isOr=true`で渡ることがなく、現状維持されている。ただし、
削除された`DrawOrBadge`が本来「isOrフラグのみに基づくId非依存の視覚化」だった点が、今回のId依存判定
への置き換えで意図せず失われた副作用がある（所見1参照）。

### (e) 便乗変更の有無 —— **なし**

diff確認済み。意図した範囲外の変更はない。

---

## `code-review`スキル併用の追加所見

### 所見1（CONFIRMED・要判断）: コピー由来の再採番パーツでOR視覚表現が失われる新規退行

**メカニズム**: `PartFolderStore.Enumerate()`はId重複を検出すると、後発ファイル（例：ユーザーが
Explorerで「a接点.gcadpart」を直接コピーした場合の複製）へ新しいId（GUID等）を再採番するが、
`IsOrEligible`フラグはそのまま維持して書き戻す（T-035由来、コピー耐性を意図した既存設計）。
`PartPaletteViewModel.cs:60`は`IsOrEligible`のみでOR論理エントリを生成するため、この再採番済みコピーも
`SelectionEntries`にOR論理エントリとして正しく載る。しかし`PartThumbnailRenderer.Render`の新しい判定
（`partId == BasicPartTemplates.ContactNOId`という固定文字列完全一致）には該当せず、サムネイルは
OR視覚表現（グリフ）なしの通常形状描画に落ちる。

**T-043自身が持ち込んだ退行であることの確認**: verifyエージェントがT-037時点（コミット`8ebc05d`）の
実装を確認したところ、旧`DrawOrBadge`呼び出しは`if (isOr) DrawOrBadge(dc, sizeDip);`という**Id非依存・
isOrフラグのみ**の判定だった（コメントにも「PartDefinitionの形状のみではOR/非ORの区別がつかない問題
への対応」と明記）。つまりT-037時点ではId再採番が起きてもOR視覚表現は失われない設計であり、**この
退行はT-043（Id完全一致判定への置き換え）が新規に持ち込んだものであり、既存弱点の単純継承ではない**。

**現実的な操作手順の評価**: アプリ内に「図形」フォルダ直下（Category==""の基本図形）を複製する専用UI
は存在しない（`SaveCustom`は`Category="自作"`にのみ書き込む）。複製手段はExplorer等でのファイル
システム直接操作のみだが、`PartFolderStore`のクラス設計自体が「フォルダをマスターとし外部からの直接
操作を許容する」思想であり、`Enumerate()`のId重複検出・再採番ロジック自体が「ファイルコピー等でIdが
重複した場合」（T-035、殿裁定）に対処するために存在する。つまりこのシナリオは想定外の誤用ではなく、
**既存コードが正面から扱うために作られた運用パターン**である。

**実害の程度**: 機能的な誤動作（配置ロジック等）はない。`PartSelectionEntryViewModel.DisplayName`は
`IsOr`フラグ由来で「OR」を前置し続けるため、テキストでは区別可能。実害は「サムネイルの見た目のみ、
OR視覚表現（グリフ）が欠落する」という限定的なもの。

**対処の方向性（参考、実装は侍判断）**: `Render`の判定条件をId完全一致ではなく、`PartDefinition.IsOrEligible`
ベース（電気的Role非依存でコピー・再採番耐性のある既存の専用フラグ）に変更すれば、この退行を回避しつつ
T-037で問題になったRole判定の巻き込みも避けられる可能性がある。ただし`Render`メソッドは現状`partId`
のみを引数に取るため、`PartDefinition`または`IsOrEligible`自体を渡すシグネチャ変更が必要になる。

### 所見2（PLAUSIBLE・軽微・記録のみ）: `observations.md`#17番目の更新

`docs/observations.md`#17番目（グリフPath Dataの二重ハードコード、T-033増分4由来）は「双方に二重
ハードコード」（2箇所：ツールバーXAML・PartEntryToGlyphGeometryConverter）と記載されているが、本コミット
で`PartThumbnailRenderer`にも同じPath Dataが複製され、**実態は3箇所**になった。次にグリフを修正する
担当者がこのエントリを見て「2箇所直せば良い」と誤認するリスクがあるため、記載を「3箇所」へ更新すべき
と判断する（家老の所見依頼どおり）。なお、App→Rendering.Wpfという参照方向制約（コミットメッセージに
明記）のため、Rendering.Wpf側からApp層のConverterを直接参照して共通化することは構造的に不可能であり、
この3箇所複製という設計自体は正当な制約下でのやむを得ない選択と評価する。

### 検討したが不採用の候補

- `Render`と`RenderGlyph`のDrawingVisual/RenderTargetBitmap生成コードの重複（Simplification）：軽微、
  修正不要。
- `sizeDip`計算式の2箇所重複：値は完全一致、DRY観点の軽微な指摘に留まる。
- Conventions角度：該当なし。

---

## 忍者への申し送り

- ORa/ORbサムネイルの見た目確認（殿目視材料スクショ）：部品選択リストでORa接点・ORb接点のサムネイルが
  ツールバーsF5/sF6と同じ意匠に見えること。
- 他5種の回帰確認：コイル・端子台・セレクトSW等のサムネイルが従来どおり表示されること。
- 所見1（コピー由来の退行）は忍者の通常検証範囲外と考えられるため、確認は不要（家老・侍判断待ち）。

---

## 出典・参照

- 対象コミット `c56b13c`（`git show`で全差分確認）
- `src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs`
- `src/Ecad2.Core/Persistence/PartFolderStore.cs`（Id重複検出・再採番ロジック、59-123行目）
- `src/Ecad2.App/ViewModels/PartPaletteViewModel.cs`
- T-037時点の実装（`git show 8ebc05d`、旧`DrawOrBadge`のId非依存判定との比較）
- `docs/observations.md`#17番目（グリフPath Data二重ハードコード、更新要）
- `code-review`スキル（medium、統合3角度→1-vote verify、CONFIRMED1・PLAUSIBLE1・該当なし多数）
