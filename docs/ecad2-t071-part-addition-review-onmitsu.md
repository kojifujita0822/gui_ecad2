# T-071 経路B部品10種追加 差分レビュー（隠密）

対象: ブランチ`t062-net10-migration`、コミット`c779210`。家老指定4観点の手動確認＋`code-review`
スキル併用（medium相当、Agent複数並列）を実施。

**結論：要修正級2件（うち1件は最重要）を発見。DoD1〜3はクリーン。DoD4（重点確認）は裏付け確認できた。**

---

## 要修正級（最重要）：Motorが配置時の重複・境界チェックをすり抜ける

**`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1443,1497`**——`ValidatePlacement`
（`!sheet.Elements.Any(el => el.Pos == pos)`）・`IsWithinGridBounds`はいずれも**配置先セル1つだけ**を
見て判定する。`PlaceElementAtSelectedCell`には`const int cellWidth = 1; // 基本図形(BasicPartTemplates)
は全て1セル幅`という明示コメント付きの前提がある。

**本コミットで追加した`Motor`（`WidthCells = 3`、`MotorPorts()`が境界0/1/2の3端子）は、この
「全て1セル幅」という前提を初めて破る新規エントリ**——`BasicPartTemplates.All()`は
`PartPaletteViewModel.SelectionEntries`経由でこの配置フローに直結しているため、Motorはこの
バリデーション漏れの影響を直接受ける。

### 再現条件

種別選択ドロップダウン/部品選択リストから「モータ」を選び、シート右端2列以内、または既存要素の
右隣セルへ配置する。境界チェック・重複チェックとも対象セル1個しか見ないため通過し、**モータの
右2セル分のポート・図形が既存要素と重なる、またはグリッド列数を超えてはみ出す**。DRC側にも占有
重複を検出するチェックが無いため（`DesignRuleCheck.cs`内に相当なし）、警告も出ずに壊れた
トポロジー/描画が生成される。

`ElementInstance.CellWidth`は既定`1`のままで、`PartDefinition.WidthCells`はどこからも読まれて
おらず（grep該当なし）**値として死んでいる**。

### 意義

これは`docs/spec/ecad2-spec-placement.md`6節で「不明点」として記録していた「重複配置判定は`Pos`の
完全一致のみで、`CellWidth`（複数セル占有）を考慮した範囲重複チェックではない（F5〜F8対象は
全て1セル幅のため実害なしと推測されるが…）」という懸念が、**T-071でMotor（3セル幅）が初めて
実際に配置可能になったことにより、推測ではなく具体的な実害として顕在化したもの**。

---

## 要修正級：タイマ接点の限時/瞬時判別ラベル（「限」「瞬」）が絶対に描画されない

**`src/Ecad2.Core/Rendering/DiagramRenderer.cs:951,957-961`**：

```csharp
private static string? TimerContactMark(ElementKind k) => k switch
{
    ElementKind.TimerContactNO or ElementKind.TimerContactNC => "限",
    ElementKind.TimerInstantContactNO or ElementKind.TimerInstantContactNC => "瞬",
    _ => null,
};
```

唯一の呼び出し元（951行）は`TimerContactMark(e.Kind)`——**生の`ElementInstance.Kind`を参照して
おり、`PartResolver.ComponentKind(e, _lib)`で解決されたKindではない**。

### 実行パス（CONFIRMED）

1. `ElementInstance.Kind`は自動プロパティで初期化子が無く、既定値`ElementKind.ContactNO`のまま。
2. **本番の配置経路（`MainWindowViewModel.PlaceElementAtSelectedCell`）は`Kind`を一度も設定しない**
   （`new ElementInstance { Pos, PartId, DeviceName }`のみ、リポジトリ全体を`grep`しても`Kind =`は
   テストファイル内にしか現れない）。
3. タイマ接点は`PartId`のみで識別される（`BasicPartTemplates.TimerContactNOId`等）——配置された
   要素は常に`e.PartId = "basic-timer-contact-no"`かつ`e.Kind = ContactNO`（既定値）。
4. `TimerContactMark(ElementKind.ContactNO)`は`_ => null`に該当し、**限時/瞬時いずれのマークも
   常に描画されない**。

コード内の別箇所（852-855行、`isLoad`判定）では`part is not null ? PartResolver.ComponentKind(e,
_lib) : e.Kind`という正しい解決パターンが使われており、**開発時にこの罠は認識されていたが951行目
には適用されなかった**ことが読み取れる。

### 影響範囲

**表示のみの問題**。`NetlistBuilder.BuildComponents`（308行）は`PartResolver.ComponentKind(e,
parts)`経由で正しく`TimerContactNO`/`TimerInstantContactNO`等を解決しており、`Evaluator`の限時/
瞬時タイマの電気的動作自体は正常（確認済み）。ただしタイマ瞬時接点は「記号が通常接点と同形」
（コミット内コメントにも明記）であるため、**このミニラベルこそが視覚的な唯一の判別手段だった
はずが機能しない**——ユーザーは配置したタイマ接点が限時なのか瞬時なのか、盤面上で見分けられない。

### 副次発見（範囲外、未検証）

同じパターンが890行目付近（`e.Kind == ElementKind.Lamp`、ランプ色ラベル）にも存在する疑いがある
（新設した`Lamp`パーツも同様に`PartId`経由でのみ識別されるため）。本レビューでは検証していないが、
気づきとして記録する。

---

## DoD1：PartRole拡張・switch拡張の正確性

**問題なし。** `ElementKind.TimerContactNO`等6値は全てT-007（GuiEcad移植）時点から既存で、本コミット
では未変更。`PartResolver.ComponentKind`のswitch拡張は正確。`ElementCatalog`側のcatch-allロジック
（`DefaultCellWidth`/`Ports`/`DefaultLabelDy`）も適切に機能する。`IsInputControlled`でタイマ接点が
意図的に除外されている点（「タイマ接点は励磁＋経過時間で制御するため含まない」）も電気的に正しい
設計。既存Roleへの丸め回避の判断は妥当。

## DoD2：座標変換式の検算

**全10種、規約どおり。** `SymbolGlyphs.cs`の中心基準座標（cx=0.5）から`x+0.5`変換で導出される
既存7種の規約と、新規10種の座標を1点ずつ突き合わせ、誤りは見つからなかった。**Motorのみ座標変換
方式が異なる**（`x+0.5`変換なし、SymbolGlyphs.Motorが中心オフセットを使わない左端原点方式のため）
が、コード内コメントで正当に説明されている（共通規約コメントの直後に例外が来るため初見では
誤読しやすい、経過観察扱いで後述）。

## DoD3：配置バー方式B切替の動作・意匠混在の許容性

コード上、既存7種・新規10種すべてが同一の`PartThumbnailRenderer`経由でレンダリングされる設計に
なったため、**「意匠混在」という懸念は構造的に解消されている**（Path手作りGeometryとImage生成の
混在ではなく、全項目がImageに統一）。ただし実機での見た目（サイズ感14x14が既存の意匠と統一感を
保っているか等）は静的レビューでは判断できず、**忍者の実機確認に委ねるべき事項**として明記する。

## DoD4（重点確認）：`PartEntryToGlyphGeometryConverterTests.cs`削除の妥当性

**裏付け確認できた。** `PartThumbnailRenderer.cs`（33-36行）に「判定は`definition.IsOrEligible`/
`Role`ベースであり、Idには依存しない（隠密レビューCONFIRMED: 旧Id完全一致判定はExplorerコピー
由来の再採番パーツでOR視覚表現が欠落する退行を持ち込んでいた）」という削除された性質と**同一趣旨の
設計**が既に存在することをコメントで確認。さらに`tests/Ecad2.App.Tests/PartThumbnailRendererTests.cs`
（T-043時点で追加済み、本コミット外・未変更）に「Id再採番されたパーツでもORグリフが表示され続ける」
ことを検証する回帰テストが**既に存在**することも確認した。侍の主張どおり、削除されたテストが
検証していた性質は構造的に別の場所（`PartThumbnailRenderer`とそのテスト）で担保されている。
**削除は妥当。**

---

## 経過観察（機能に影響なし、スタイル・軽微な指摘）

- **`BasicPartTemplates.cs`**：`TimerInstantContactNO`/`NC`のPrimitivesが既存`ContactNO`/`NC`と
  座標完全一致（コピペ）。コメントで「図形はContactNOと同一、Role/Idのみ別」と意図的コピーである
  ことは明記済み。3箇所以上に増える兆しがあれば共通化を検討する余地あり。
- **`BasicPartTemplates.cs`**：Motorの座標変換規約の例外説明が、共通規約コメントの直後に位置し
  初見で誤読しやすい（DoD2参照）。
- **`MotorPorts()`**：`ElementCatalog.Ports(ElementKind.Motor, width)`と同一座標のハードコピー。
  将来どちらかだけ改修されると乖離するリスクがあるが、現時点では一致しており実害なし。
- **`ThermalOverload`の「暫定形」コメント**：上流`SymbolGlyphs.cs`側のコメントに追従した記述で
  場当たり的ではないと判断（ただし参照先自体の確認は本レビューでは行っていない）。

## まとめ

| 観点 | 判定 |
|---|---|
| DoD1: PartRole/switch拡張の正確性 | OK |
| DoD2: 座標変換の検算 | OK（Motorの例外は正当） |
| DoD3: 配置バー方式B・意匠混在 | 構造的に解消、実機確認は忍者へ |
| DoD4: テスト削除の妥当性（重点） | 裏付け確認できた、削除は妥当 |
| 要修正 | Motor重複/境界チェックすり抜け（最重要）、TimerContactMark不発火（2件） |
| 経過観察 | コピペ座標・コメント配置・MotorPortsハードコピー（3件） |
