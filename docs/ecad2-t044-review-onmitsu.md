# T-044 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット `0d3425a`内のsrc/tests差分（`MainWindowViewModel.cs`+14行、
> `OrWiringTests.cs`新規83行）。家老指定観点(1)〜(6)＋`code-review`スキル（medium、統合4角度→
> 1-vote verify）併用。

---

## 結論：**要修正（重大）。忍者確認へ進める前に修正が必要**

家老指定観点(1)(2)(4)(5)(6)は問題ないが、`code-review`スキルの検証で**CONFIRMEDの重大なバグ**を
発見した：**連鎖的なOR配置（同一列で3階層以上の並列回路を重ねるケース）で、既存の縦コネクタを見落とし、
電気的ネットリスト自体が誤る**。これは表示上の見た目の問題ではなく、回路の等価性そのものが壊れる。

---

## 対象差分

`git show 0d3425a -- src/Ecad2.App/ViewModels/MainWindowViewModel.cs tests/Ecad2.App.Tests/OrWiringTests.cs`
で確認。`PlaceElementAtSelectedCell`に`NothingBetweenRailAndColumn(row, column)`ローカル関数を追加し、
配置行・基準行の両方で左母線との間に既存**要素**が無い場合のみ左側`VerticalConnector`を省略する。

---

## 家老指定観点の検証

### (1) 省略条件が殿最終裁定と一致するか —— **論理的に一致（ただし判定範囲に欠陥あり、下記所見参照）**

`if (!NothingBetweenRailAndColumn(pos.Row, leftColumn) || !NothingBetweenRailAndColumn(br, leftColumn))`
という実装を、A=配置行に要素なし・B=基準行に要素なしとしてド・モルガンの法則で検証：`!A || !B` は
`!(A && B)`と等価。「追加（維持）する」条件が`!(A && B)`＝「両方空ではない場合に維持」であり、裏を
返せば「両方空の場合のみ省略」＝殿裁定と論理的に完全一致することを確認した。

**ただし**、この判定は`sheet.Elements`（配置済み要素）のみを見ており、`sheet.Connectors`（既存の縦
コネクタ）を一切考慮していない。これが下記所見1の重大バグの原因である。

### (2) 列0の自明ケース包含 —— **確認済み**

`leftColumn=0`の場合、`el.Pos.Column < 0`は常にfalseとなり（GridPosの列は運用上常に非負、
キーボードナビゲーションで`Math.Max(0, ...)`クランプされていることを確認）、常に「何もない」判定に
なる。事前調査の候補1（列0のみ省略）を正しく包含している。

### (3) フォールバック機構への安全な依存 —— **一般化ケースで破綻をCONFIRMED（所見1）**

事前調査（`docs/ecad2-t044-presurvey-onmitsu.md`）で検証したのは「候補1：`leftColumn==0`限定」の
安全性であり、この場合は`LeftTerminator`/`LeftRailReached`のスキャン範囲`(0, lb]`が空集合に退化する
ため無害だった。しかし実装は「候補2寄り」に一般化されており（`leftColumn>0`でも両行に要素が無ければ
省略）、この場合はスキャン範囲が非空になり、**別のOR配置由来の既存縦コネクタと干渉しうる**。事前調査
自身が「候補2はP-003スコープ外・改めてリスク評価が必要」と明記していた通りの領域であり、今回その
リスクが実際に顕在化した（所見1参照）。

### (4) 右合流側の縦分岐維持 —— **確認済み**

`sheet.Connectors.Add(new VerticalConnector { Column = rightColumn, ... })`は条件なしで常に実行される。

### (5) テスト実効性 —— **4テストは正しいが、連鎖ケースが未検証（所見1の再発防止に必要）**

`OrWiringTests.cs`の4テスト（列0／列途中両行クリア／基準行に遮る要素あり／配置行に遮る要素あり）は
いずれも計算過程を追跡し、意図通り機能することを確認した。ただし全テストが「単一のOR配置」のみを
検証しており、**同一行が複数回OR配置の基準行・配置行になる連鎖ケース**（所見1のバグが顕在化する条件）
を検証するテストが1件も存在しない。

### (6) 便乗変更なし —— **確認済み**

---

## `code-review`スキル併用の追加所見

### 所見1（CONFIRMED・重大）: 連鎖的なOR配置で電気的ネットリストが誤る

verifyエージェントが具体的な反例を構築し、手計算で追跡して確認した。

**再現手順**：
1. 行R0列0にa接点A0（通常配置）
2. 行R0列2にa接点A2（通常配置）
3. 行R1列2にb接点BをOR配置（基準行R0、`leftColumn=2`）
   - 行R1は空・行R0には列0にA0がある → 条件を満たし左コネクタ生成
     `{Column=2, TopRow=R0, BottomRow=R1}`。右コネクタ`{Column=3, TopRow=R0, BottomRow=R1}`も生成。
   - この時点でBの真の給電源はA0の出力ノードであり、母線には直結していない。
4. 行R2列2にb接点CをOR配置（基準行R1、`leftColumn=2`、**手順3と同一列**）
   - `NothingBetweenRailAndColumn(R1, 2)`は`sheet.Elements`のみを見るため、行R1にはB（列2）しか
     要素がなく`el.Pos.Column < 2`を満たす要素は無い→**true判定**。
   - しかし行R1には手順3で生成した縦コネクタ`{Column=2, TopRow=R0, BottomRow=R1}`が既に存在して
     おり、これは`sheet.Connectors`側の情報のため`NothingBetweenRailAndColumn`の判定には一切
     反映されない。
   - 結果、条件が「両行とも空」と誤判定され、**左コネクタが省略される**。

**誤りの実害**：`DiagramRenderer.LeftTerminator(sheet, R2, lb=2)`は、省略により該当コネクタが無い
ため`null`を返し、行R2（C）に母線への**直結線が描画**される。同様に`NetlistBuilder.LeftRailReached`
も`true`を返し、Cの左ノードが**左母線ネットへ直接union**される。これは表示だけの問題ではなく、
**電気的ネットリスト自体の誤り**：本来Cは「Bと並列（＝A0起点）」であるべきなのに、実際にはA0を経由
せず常時母線と同電位（常時オン相当）のノードとして扱われてしまう。BとCで給電源が食い違う、意図しない
回路等価性の破れが生じる。

**トリガー条件の一般性**：これは特殊な操作ではなく、「同一列で3階層以上の並列回路（OR配置）を重ねる」
という、ラダー図では十分にありうる回路パターンである。忍者確認前に必ず修正すべき重大度と判断する。

**対処の方向性（参考、実装は侍判断）**：`NothingBetweenRailAndColumn`の判定に、`sheet.Connectors`
のうち対象行に紐づく既存の縦コネクタの有無も含める必要がある。例えば「その行にBottomRowまたはTopRow
として紐づく縦コネクタがCoうち、Column<leftColumnのものが無いか」も確認する形へ拡張する等。

### 所見2（参考・要確認、severity不明）: レンダリングとネットリストの判定条件の不一致

verifyエージェントが所見1の調査中に気づいた点：`NetlistBuilder.LeftRailReached`/`RightRailReached`
は`(c.TopRow == row || c.BottomRow == row)`とTopRow/BottomRow両方を条件に含めているが、
`DiagramRenderer.LeftTerminator`/`RightTerminator`はT-026（P-003）で「BottomRow側のみを対象にする」
よう修正済みという非対称性が既に存在する（隠密の事前調査でも言及済み、`docs/ecad2-t044-presurvey-onmitsu.md`
の「留意点」参照）。今回の反例には直接影響しなかったが、レンダリングとネットリストの判定条件が
食い違ったままである点は、今回のバグ修正と合わせて検討の価値があるかもしれない（今回のスコープ外、
経過観察として記録するに留める）。

### 検討したが不採用の候補

- `NothingBetweenRailAndColumn`の2回のLINQ呼び出しを1回に統合できる（Simplification）：軽微、修正
  優先度低い。
- ViewModel層のprivateローカル関数がSheet/Core層のAPIになっていない（Altitude）：P-025（App層リファ
  クタ検討）と同種の問題意識、技術的負債として記録。
- Efficiency/Conventions角度：該当なし。

---

## 忍者への申し送り

- **所見1（重大）の修正が完了するまで、忍者実機確認は待たれたい。** 修正後、連鎖的なOR配置（同一列で
  3階層以上重ねるケース）のシミュレーション結果・DRC出力も確認観点に加えることを推奨する。

---

## 出典・参照

- 対象コミット `0d3425a`（`git show`で該当ファイルの差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（330-390行目、`PlaceElementAtSelectedCell`）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（`LeftTerminator`、373-380行目）
- `src/Ecad2.Core/Simulation/NetlistBuilder.cs`（`LeftRailReached`/`AddHorizontalWireUnions`）
- `tests/Ecad2.App.Tests/OrWiringTests.cs`（新規回帰テスト4件）
- `docs/ecad2-t044-presurvey-onmitsu.md`（隠密事前調査、候補1/候補2のリスク評価）
- `code-review`スキル（medium、統合4角度→1-vote verify、CONFIRMED1・該当なし多数）
