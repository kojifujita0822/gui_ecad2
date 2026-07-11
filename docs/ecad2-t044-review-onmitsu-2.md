# T-044 往復1周目修正レビュー（隠密、d04c9a3正式再レビュー）

> 2026-07-07 隠密レビュー。対象コミット `d04c9a3`（`fix(app): T-044往復1周目 - 連鎖OR配置での
> 縦コネクタ見落としを修正(重大)`）。隠密指摘（`docs/ecad2-t044-review-onmitsu.md`所見1、
> CONFIRMED・重大）への対応。家老指定観点(1)〜(5)＋`code-review`スキル（medium、統合4角度→
> 1-vote verify）併用。

---

## 結論：**クリーン。忍者実機確認（殿スクショ事例の再現＋連鎖OR＋回帰）へ回してよい**

指摘した重大バグ（連鎖OR配置での既存縦コネクタ見落とし）は正しく解消された。家老指定観点(1)〜(5)は
いずれも問題なし。`code-review`スキルの検証で1件の既存技術的負債（今回の修正では新規に持ち込んで
いない）を確認したのみ。

---

## 対象差分

`git show d04c9a3`で確認。`MainWindowViewModel.cs`+9行、`OrWiringTests.cs`+27行、
`NetlistBuilderOrChainTests.cs`新規61行。`NothingBetweenRailAndColumn`に
`!sheet.Connectors.Any(c => (c.TopRow == row || c.BottomRow == row) && c.Column <= column)`を追加。

---

## 家老指定観点の検証

### (1) Connectors条件追加の正しさ —— **完全解消を確認。境界条件`<=`は必須**

verifyエージェントが指摘した反例（行0列0=A0・行0列2=A2→行1列2=B(OR基準行0)→行2列2=C(OR基準行1、
Bと同一列)）を手計算・実測（`dotnet test`合格確認）の両方で追跡し、修正後は正しく行1(B)の既存縦
コネクタ（列2、TopRow0-BottomRow1）が検出され、Cの左縦分岐が維持されることを確認した。

境界条件`c.Column <= column`（等号を含む）は必須：OR連鎖では基準行と新要素が同一列に揃うのが典型形
であり、この場合既存コネクタの`Column`は新しい`leftColumn`と完全一致する。もし`<`（等号を含まない）
にすると、まさに隠密指摘の反例（同一列での連鎖）を再び取りこぼす。`<=`による「過剰安全側化」（電気的に
無関係な既存コネクタでもヒットして縦分岐を残す）の懸念も検討したが、`NetlistBuilder`はコネクタ両端を
unionするだけなので、冗長な縦コネクタが1本増える程度の実害（見た目のみ）に留まり、電気的トポロジーは
壊れない。

新規要素・新規コネクタの自己参照混入（判定対象のsheet.Elements/Connectorsに新要素自身が紛れ込まないか）
も確認し、問題ないことを検証した（`el != newElement`の明示除外、判定時点で新規コネクタは未Add）。

### (2) テスト実効性 —— **実測で確認、意図通り機能**

App層新規テスト（3階層連鎖ケース）・Core層`NetlistBuilderOrChainTests`（2件）とも、実際に
`NetlistBuilder.Build`を実行して数値を確認した：正常系（`B.NetA == C.NetA`かつ`≠ LeftRailNet`）・
バグ再現系（縦分岐省略時に`C.NetA == LeftRailNet`）とも意図通りの結果を得た。

**副次所見（既存の設計上の隙間、今回の欠陥ではない）**：App層テスト（配置ロジック→Connector生成の
検証）とCore層テスト（Connector構成→Netlist正しさの検証）を実際に接続する「橋渡しテスト」
（`NetlistBuilder.Build(vm.CurrentSheet!, vm.PartLibrary)`形式）が存在しない。verifyエージェントが
発見した追加の隙間（App層テストの`"contact-no"`というPartId文字列が実カタログID
`"basic-contact-no"`と不一致で、たまたま`ElementKind`の既定値に頼っている等）も含め、これは
`OrWiringTests.cs`の既存前提（T-044以前から）であり、今回の3階層連鎖ケーステスト自体はこの前提の
上で正しく機能している。今回の増分の対応は不要と判断するが、技術的負債として記録する。

### (3) 省略条件の他性質の非退行 —— **回帰なし。殿裁定の文言拡張は意図の忠実な実装と評価**

既存4テスト（列0省略・列途中両行クリア・基準行に遮る要素あり・配置行に遮る要素あり）を手計算で
再確認した。いずれも「テスト実行時点でシート上に既存コネクタが1本も無い」ケースのため、新条件
（Connectorsチェック）の影響を受けず、回帰なし。

殿裁定の文言「配置行と基準行の両方で要素なしの場合のみ省略」は字面上「要素(Elements)」のみを指すが、
この裁定の実質的意図は「トポロジー等価保証」（縦分岐を省略しても電気的分断が起きないケースに限定
すること）であり、「要素なし」はその代理指標に過ぎない。3階層連鎖ケースで「要素は無いが既存コネクタ
により既に分岐済み」という状態が判明した以上、コネクタチェックの追加は殿裁定の文言を超えた拡張という
より、**その実質的意図（誤ったバイパス配線の防止）を正確に実装したもの**と評価する。UI/UXの新規分岐
ではなく内部の電気的正しさに関する技術判断のため、殿への再確認は必須ではないと判断する。

### (4) 便乗変更なし —— **確認済み**

### (5) WireBreak考慮の有無 —— **REFUTED（メカニズムは実在するが、ecad2に到達手段が無く実害なし）**

`code-review`のfinderが「`NothingBetweenRailAndColumn`は`sheet.WireBreaks`を考慮しておらず、
基準行/配置行の左側にWireBreakのみ存在する場合、縦分岐を誤って省略し孤立ネットが生じうる」という
メカニズムを提起した。これを検証するため、ecad2内で`WireBreak`を生成するUI操作の有無をGrepで確認
した：`ToolMode.PlaceWireBreak`はenum値として定義されているのみで、ツールバーボタン・キーバインド・
専用コマンドは一切実装されていない（前回のT-040調査`docs/archive/ecad2-t040-wire-survey-onmitsu.md`でも
「専用コマンド・RoutedCommand・KeyBinding(XAML InputBinding含む)も無し」と確認済み）。`sheet.WireBreaks`
自体はCore層のデータモデルとしてGuiEcadから移植済みで、`NetlistBuilder`/`DiagramRenderer`は消費する
実装を持つが、**ユーザーがWireBreakを実際に生成する手段が現状のecad2に存在しない**ため、この懸念は
理論上のみに留まり、現時点で実害はない（REFUTED）。将来WireBreak生成UIが実装される際は、
`NothingBetweenRailAndColumn`への対応検討が必要になる点を申し送る。

---

## `code-review`スキル併用の追加所見

### 所見1（参考記録・severity低）: 母線到達判定ロジックの3モジュール間不一致がさらに1箇所定着

`DiagramRenderer.LeftTerminator`はT-026（P-003）で「TopRow側を含めると母線浮き見えバグになる」ため
`BottomRow`側のみに意図的に限定済みだが、`NetlistBuilder.LeftRailReached`は今も`TopRow || BottomRow`
のままであり、今回のViewModel側の新条件もこれに倣って`TopRow || BottomRow`を採用した。これにより、
「行が母線から既に分岐済みか」の判定基準が3モジュール間で不揃いという既存の技術的負債（隠密の事前
調査・GuiEcad比較調査で既に把握済み）が、今回の修正でさらに1箇所定着した。今回の反例（A→B→Cの単純
連鎖）では両基準が一致するため表面化しないが、将来「基準行を再利用する複雑な分岐パターン」で電気的
判定と描画判定の乖離が顕在化しうる。今回の対応は不要（P-025等でCore層への共有関数集約を検討する際の
材料として記録するに留める）。

### 検討したが不採用の候補

- LINQ走査4回（pos.Row/br × Elements/Connectors）の効率懸念：単発UI操作でありパフォーマンス上の懸念
  なし。
- Simplification/Conventions角度：該当なし。

---

## 忍者への申し送り

- 殿スクショ事例の再現＋連鎖OR（3階層以上）＋既存5観点（列0/列途中/基準行遮り/配置行遮り/右合流）の
  回帰確認を予定通り進めてよい。
- 所見1（判定基準の3モジュール不一致）は今回の検証範囲では顕在化しないため、通常の忍者確認では確認
  不要。

---

## 出典・参照

- 対象コミット `d04c9a3`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`PlaceElementAtSelectedCell`、330-394行目）
- `tests/Ecad2.App.Tests/OrWiringTests.cs`（新規3階層連鎖テスト）
- `tests/Ecad2.Core.Tests/NetlistBuilderOrChainTests.cs`（新規、2テスト）
- `src/Ecad2.Core/Simulation/NetlistBuilder.cs`（`LeftRailReached`/`Severed`/`AddHorizontalWireUnions`）
- `src/Ecad2.Core/Rendering/DiagramRenderer.cs`（`LeftTerminator`、T-026/P-003の意図的差分）
- `src/Ecad2.App/ViewModels/ToolState.cs`（`ToolMode.PlaceWireBreak`未配線の確認）
- `docs/archive/ecad2-t040-wire-survey-onmitsu.md`（WireBreak/手動配線UI未実装の前回調査）
- `docs/ecad2-t044-review-onmitsu.md`（所見1、CONFIRMED重大バグの初回指摘）
- `docs/ecad2-t044-guiecad-diff-survey-onmitsu.md`（GuiEcad比較、判定基準集約の技術的負債の既出言及）
- `code-review`スキル（medium、統合4角度→1-vote verify、REFUTED1・参考記録1・該当なし多数）
