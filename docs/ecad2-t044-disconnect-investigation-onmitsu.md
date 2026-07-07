# T-044 右合流側縦線「未接続に見える」現象の静的調査（隠密）

> 2026-07-07 隠密調査。家老采配（殿スクショ`ss_20260707_134207.png`、連鎖OR下端要素(行9)が右合流の
> 縦線に接続していないように見える現象）に基づくWチェックの独立調査系（忍者の実機再現と並行実施）。
> 観点(1)〜(3)を静的コード追跡のみで検証した（`code-review`スキルは今回未併用、既知の非対称
> [P-003/P-028]を起点に的絞り追跡）。

---

## 結論：**d04c9a3(T-044)の欠陥ではない。T-026(P-003)由来の描画層(DiagramRenderer)の既存欠陥が、
T-044の連鎖OR機能で初めて可視化された疑いが強い（CONFIRMED、code trace）。電気的ネットリストは
正常、見た目のみの断線（描画バグ）と判断する**

殿スクショの症状は、連鎖OR配置の各階層で**配置列が右方向にずれていく**（=各階層で新要素を基準行の
既存要素よりさらに右の列へ置く）ケースにおいて、`DiagramRenderer.RightTerminator`/`LeftTerminator`
が**`c.BottomRow == row`のみ**を見て**`c.TopRow == row`を一切見ない**という、T-026(P-003)で意図的に
導入された既存の非対称仕様が原因で、「基準行(上段)自身の横線が、実際に縦分岐している列より手前
（自分自身の右端）で止まってしまい、そこから先(連鎖の次階層への縦線)との間に見た目の隙間ができる」
という描画バグを起こすと判断した。**電気的には`NetlistBuilder`が`TopRow||BottomRow`を正しく見ており
ネットリストは分断されない**（後述、実害はシミュレーション・DRC上は無い＝表示のみの問題）。

---

## 観点(1): d04c9a3(T-044左分岐抑止修正)が右合流の縦線生成に影響しているか —— **無関係と判断**

`MainWindowViewModel.PlaceElementAtSelectedCell`（330-394行目）を確認。d04c9a3が変更したのは
`NothingBetweenRailAndColumn`（左分岐を省略するか判定するローカル関数、387-389行目）と、それを使う
**左側**縦コネクタの条件付き生成（391-392行目）のみ。

```csharp
if (!NothingBetweenRailAndColumn(pos.Row, leftColumn) || !NothingBetweenRailAndColumn(br, leftColumn))
    sheet.Connectors.Add(new VerticalConnector { Column = leftColumn, TopRow = br, BottomRow = pos.Row });
sheet.Connectors.Add(new VerticalConnector { Column = rightColumn, TopRow = br, BottomRow = pos.Row });
```

**右側**縦コネクタ（393行目）は`d04c9a3`前後を通じて無条件生成のまま変更されておらず（過去のonmitsu-2
レビューでも確認済み）、`rightColumn`の計算式（369-370行目、`Math.Max(baseElement.Pos.Column,
pos.Column) + cellWidth`）自体もT-044のどの往復でも変更されていない。d04c9a3は左側判定のみのコミット
であり、右合流側の生成ロジック・値には一切触れていない。**副作用の可能性は無い（無関係と判断）**。

---

## 観点(2): 連鎖ORで下端要素が別列にある時、右合流コネクタ/描画が届かない条件 —— **CONFIRMED（描画層のみ）**

### 反例の構築（手計算トレース）

以下、`leftColumn`/`rightColumn`は`PlaceElementAtSelectedCell`369-370行目の式、縦コネクタは
`sheet.Connectors`に蓄積される前提で追跡する。

1. 行0列0=A0、行0列2=A2（通常配置、`isOr:false`）
2. 行1列2=B（`isOr:true`、基準行=0、baseElement=A2〈列2、直近〉）
   - `leftColumn=2`, `rightColumn=3`
   - 左分岐：基準行0に列0でA0が挟まる（`NothingBetweenRailAndColumn(0,2)`=false）→ 左コネクタ
     `{Column:2, Top:0, Bottom:1}` 生成
   - 右コネクタ`{Column:3, Top:0, Bottom:1}` 生成（無条件）
   - **Bの右端(rb)は列3で、右コネクタの列(3)と一致**（B自身の右端＝分岐点）
3. 行2列5=C（`isOr:true`、基準行=1、baseElement=B〈列2、行1で唯一〉）— **殿スクショに対応：新要素を
   基準行の既存要素より右の列へ置く**
   - `leftColumn=min(2,5)=2`, `rightColumn=max(2,5)+1=6`
   - 左分岐：行1(B)に列2で縦コネクタ`{2,0,1}`が既にBottomRow=1として存在 → 左コネクタ維持
     （d04c9a3の修正が正しく機能。ここは問題なし）
   - 右コネクタ`{Column:6, Top:1, Bottom:2}` 生成（無条件）

### 描画側トレース（`DiagramRenderer.RightTerminator`、385-392行目）

```csharp
private static double? RightTerminator(Sheet sheet, int row, int rb, int columns)
{
    double? best = null;
    foreach (var c in sheet.Connectors)
        if (c.BottomRow == row && c.Column >= rb && c.Column < columns)
            best = best is null ? c.Column : Math.Min(best.Value, c.Column);
    return best;
}
```

行1(B)の描画時（`rb=3`）：`RightTerminator(sheet, row:1, rb:3, columns)`は`c.BottomRow==1`の
コネクタのみを走査する。存在するコネクタは`{2,0,1}`（BottomRow=1、列2）と`{3,0,1}`（BottomRow=1、
列3）——**手順3で生成した`{6,1,2}`はTopRow=1・BottomRow=2のため、`BottomRow==1`条件に一致せず
完全に無視される**。結果`best=3`（`{3,0,1}`から）。`rt.Value(3)==rb(3)`により「要素端が分岐点→
母線へ延ばさない」分岐（353行目コメント）が成立し、**行1の横線は列3で止まる**。

しかし実際にCへ下る縦コネクタは列6にある（手順3で生成）。`DrawConnectors`（394-410行目）はこの
縦コネクタを条件なしで描く（列6、行1→行2の縦線＋行1側の接合点ドット）ため、**「列3で止まった行1の
横線」と「列6から生えている縦線＋ドット」の間、列3〜6の区間に、描画上何もつながっていない隙間が
生じる**。これが殿スクショの「連鎖ORの下端要素が右合流の縦線に接続していないように見える」の描画上
の正体と考えられる。

### 電気的には分断されない（`NetlistBuilder`側で確認）

`NetlistBuilder.RightRailReached`（250-256行目）は`(c.TopRow == row || c.BottomRow == row)`と
**TopRowも見る**ため、行1が列6の縦コネクタのTopRowであることを正しく検出し、行1の右母線への誤union
を防ぐ。さらに縦コネクタのノード解決（`ResolveNode`、58-80行目）は、境界が行の要素の`rightBoundary`
以上であれば`severed`でない限りその要素の`rightNode`へ帰着する仕組み（71-72行目）のため、行1に
他の要素が挟まっていなければ列6は列3(Bの右端)と同一ノードに正しく解決される。**シミュレーション・
DRC上はB・Cが正しく同一ネットとして扱われ、電気的な分断は無い**（今回の反例では行1に他要素が無い
前提。行1に別要素が存在し列3〜6間を占める場合は別途要検証だが、殿スクショの構図からは単一要素と
推測）。

### 発生条件の一般化

この隙間は「連鎖ORの各階層で、配置列が**前階層の基準要素の列より右**へ広がっていく」場合に生じる
（新要素の列が基準要素の列以下、または基準行自身に他の縦コネクタも列6以遠に無い場合は、行の横線が
右母線まで延びて列6を自然にカバーするため隙間は生じない——ケースによる）。「基準行の直近要素を
OR対応先に選ぶが、その基準行自身がさらに別の縦コネクタ(BottomRow側)で早期終端させられている」という
組み合わせが必要条件であり、**3階層以上の連鎖かつ列が右へ広がるケースで顕在化しやすい**。殿スクショ
（行5/7/9と段階を経るごとに列が右へ広がる構図に見える）はこの条件に合致する可能性が高い。

---

## 観点(3): T-044範囲内の欠陥か、既存の別問題かの切り分け —— **T-026(P-003)由来の既存欠陥。T-044は
これを新しく可視化しただけ**

`DiagramRenderer.LeftTerminator`/`RightTerminator`の`BottomRow`限定は、T-026実機検証で発見された
「OR入力で基準行まで母線から浮いて見えるバグ」への対処として**意図的に**導入されたもの（コミット
`282f3eb`、`docs/proposed.md` P-003）。**P-003の記録には対処当時から次の記述がある**：

> 「基準行に複数要素があり新要素が末尾寄りの複雑ケースは今回のOR実装スコープ外として扱う」

これは、まさに今回確認した「基準行自身が別の縦コネクタのTopRowになる複雑ケース」を**T-026時点で
既にスコープ外と認識した上で保留していた**ことを示す一次記録である。T-044（連鎖OR機能）は、まさに
この「保留されていたスコープ」を初めて実際に踏む機能であり、**T-044自体（`0d3425a`・`d04c9a3`の
どちらも）に新規のバグを持ち込んだわけではない**。d04c9a3が対処した「連鎖ORでの左分岐見落とし」
（`docs/ecad2-t044-review-onmitsu.md`所見1）とは別の欠陥であり、**今回の描画ギャップは`d04c9a3`
適用前の`0d3425a`時点でも既に再現する**（左側の縦コネクタ生成条件はこの反例の右側描画経路に一切
関与しないため）。

`docs/ecad2-t044-review-onmitsu-2.md`所見1・`docs/ecad2-t044-guiecad-diff-survey-onmitsu.md`追加
所見1で既に指摘した「母線到達判定ロジックの3モジュール間(NetlistBuilder/DiagramRenderer/ViewModel)
不一致」（`docs/proposed.md` P-028）は、当時「今回の反例(同一列の単純連鎖)では両基準が一致するため
表面化しない」と評価していたが、**今回の列が右へ広がる反例ではP-028の非対称が実際に可視化する**
ことを確認した。P-028はこの意味で「理論上の技術的負債」から「具体的な描画欠陥の実証」へ格上げする
必要がある。

---

## 総合評価・申し送り

- **切り分け**：T-044(`d04c9a3`)は無罪。真因はT-026(P-003)で意図的にスコープ外とされた
  `DiagramRenderer.LeftTerminator`/`RightTerminator`のBottomRow限定仕様が、T-044(連鎖OR)により
  初めて到達可能になったケース。
- **実害範囲**：描画（見た目）のみ。シミュレーション・DRCへの影響は無いと判断する（ただし
  `NetlistBuilder`側の検証は今回のコード追跡ベースであり、実データでの`dotnet test`実行検証は
  未実施——時間の都合、必要なら追加検証可）。
- **忍者への申し送り**：実機で「連鎖OR配置時、各階層の配置列を基準行の既存要素よりさらに右へずらす」
  操作を行い、(a)見た目の隙間再現、(b)シミュレーション結果(DRC出力含む)でB・Cが正しく同一ネット
  として扱われるか、の両方を確認されたい。後者が正常であれば「描画のみのバグ」との判断が実証される。
- **修正方針（参考、実装は侍判断）**：単純に`LeftTerminator`/`RightTerminator`へ`TopRow`条件を
  戻すとT-026が修正した「基準行が母線から浮いて見える」バグが再発する。P-003が回避した問題と今回の
  問題は同じ`TopRow`条件の要否が絡むが**条件を全面的に戻すのではなく、「自分自身より右にある別の
  縦コネクタ」と「自分を経由して下流へ分岐する縦コネクタ」を区別できる、より精密な条件**が必要と
  考えられる。P-028（Core層への母線到達判定集約）で本格対応するのが筋が良いと考える。

---

## 出典・参照

- 殿スクショ `C:\Users\kojif\Pictures\claude_screenshots\ss_20260707_134207.png`
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（330-394行目、`PlaceElementAtSelectedCell`）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（297-365行目`DrawRungWires`、367-392行目
  `LeftTerminator`/`RightTerminator`、394-410行目`DrawConnectors`）
- `src/Ecad2.Core/Simulation/NetlistBuilder.cs`（58-80行目`ResolveNode`、220-256行目
  `AddRightRailAutoConnections`/`LeftRailReached`/`RightRailReached`）
- `tests/Ecad2.App.Tests/OrWiringTests.cs`（既存回帰テストは全て同一列の連鎖のみを検証、今回の
  列が広がるケースは未カバーと確認）
- `docs/proposed.md` P-003（T-026時点でのスコープ外宣言、一次記録）・P-028（3モジュール不一致、
  今回で実証格上げ）
- `docs/ecad2-t044-review-onmitsu-2.md`（d04c9a3正式再レビュー、所見1）
- `docs/ecad2-t044-guiecad-diff-survey-onmitsu.md`（GuiEcad比較、追加所見1）
