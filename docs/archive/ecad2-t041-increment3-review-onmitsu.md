# T-041増分3 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット`9cc5b32`（`feat(app): T-041増分3 - 配線分断(WireBreak)の
> 記入(F10)・選択・削除`）。家老指定観点(1)〜(5)を実測（`dotnet test`）併用で検証した。

---

## 結論：**クリーン。忍者実機確認へ回してよい（増分1/2の是正待ちと合わせて）**

家老指定観点(1)〜(5)いずれも問題なし。特筆すべき点として、**増分1の修正で確立した「`SelectedCell`
のsetterを選択排他クリアの唯一の真実源にする」パターンが、`SelectedWireBreak`という3つ目の選択
プリミティブへも正しく踏襲され、増分1で発生した4件のCONFIRMEDと同種の見落としが今回は1件も
発生していない**ことを確認した。これは増分2で指摘した「状態管理の分散リスク」への実証的な反証
（少なくとも「選択中の1インスタンス」という単純な形の状態については、確立されたテンプレートへ
機械的に載せる限り安全に横展開できる）と位置づけられる。新規テスト6件は実際に`dotnet test`で
実行し全件合格を実測した。

---

## 対象差分

`git show 9cc5b32`で確認。`MainWindow.xaml.cs`+75/-17、`MainWindowViewModel.cs`+44、
`LadderCanvas.cs`+42/-1、新規テスト`WireBreakSelectionTests.cs`+98（6件）。

---

## 家老指定観点の検証

### (1) F10・制御回路シート限定・即時記入(重複防止)の実装 —— **意図通り**

`TryPlaceWireBreak()`（`MainWindow.xaml.cs`）は`TryBeginConnectorDraft`（増分2）と同型の前提チェック
（`HasProject`→`sheet.MainCircuit`→`SelectedCell is null`）を経て、`PlaceWireBreakAtSelectedCell()`
（`MainWindowViewModel.cs`）を呼ぶ。同一位置（`Row`・`Boundary`の完全一致）に既存の`WireBreak`が
あれば`sheet.WireBreaks.Any(...)`で検出し追加しない設計（殿裁定「同一位置への重複記入は防止」）を
確認した。点系は原案4節通り確認フェーズ無し（Enter/Escの追加分岐は無く、F10押下のみで完結）。

**増分2との重要な違い**：`WireBreak`の記入は「押した瞬間に確定」の単発操作であり、増分2の
`VerticalConnector`のような「複数キー入力にまたがる作業中状態（`_connectorDraft`）」を持たない。
このため、増分2で問題になった「作業中にシートが切り替わる」というクラスのリスクが、この機能には
そもそも存在しない（毎回のF10押下時点の`CurrentSheet`に対して完結するため）。

### (2) WireBreakの点ヒットテスト（距離判定）の妥当性 —— **概ね妥当。1点、severity低の推測を記録**

`HitTestWireBreak`（`LadderCanvas.cs`）はX/Y独立の許容誤差判定（`Math.Abs(xMm - geo.X(b.Boundary))`・
`Math.Abs(yMm - geo.YRow(b.Row))`、いずれも2.0mm）で、正方形の当たり判定になる（真円ではない）。
`VerticalConnector`同様、単純だが実用上問題ない設計と判断する。

**推測（severity低、未検証）**：マウスクリック時のヒットテストは`HitTestConnector`が先、
`HitTestWireBreak`が後の順で試される（`MainWindow.xaml.cs`のクリックハンドラ）。`VerticalConnector.
Column`も`WireBreak.Boundary`も同じ「0.5刻み」のセル中央値を取りうるため、もし縦コネクタと配線
分断が同じ列位置・近い行に共存する図面があれば、その位置をクリックすると常に縦コネクタが先に
ヒットし、配線分断を選択できない（縦コネクタが配線分断を"覆い隠す"）状況が理論上ありうる。実際に
そのような共存配置が生じる具体的な操作手順までは確認できておらず、確度は低い推測に留める。

### (3) SelectedConnectorと同型パターンの踏襲確認 —— **正しく踏襲されている**

- `SelectedCell`のsetter（増分1修正で確立済み）に`SelectedWireBreak = null;`が追加され、増分1で
  4箇所必要だった個別クリアが今回は**この1行のみで済んでいる**ことを確認した（矢印キー移動・
  選択解除ボタン・DRC同一シートジャンプ・新規/開くの4経路いずれも、既に確立済みの一般機構
  経由で自動的にカバーされる）。
- `DeleteSelectedWireBreak()`は`sheet.WireBreaks.Remove(wireBreak)`の戻り値を最初から確認しており
  （増分1で追加で直した防御パターンを、増分3では**当初から**正しく実装している）。
- `ReplaceDocument`に`SelectedWireBreak = null;`が明示追加されている（`_selectedCell`のsetter
  バイパス直接代入と同じ理由で必要、増分1と同型の対応）。
- Delete優先順位チェーンが`DeleteSelectedElement() || DeleteSelectedConnector() ||
  DeleteSelectedWireBreak()`と正しく拡張されている。
- マウスクリック時の排他切替順序（`SelectedCell = null;`→`SelectedWireBreak = wireBreak;`）も、
  増分1で確立した「逆順だと打ち消される」という制約を守った順序になっている。

**実測検証**：`WireBreakSelectionTests.cs`（6件）を`dotnet test --filter
FullyQualifiedName~WireBreakSelectionTests`で実行し、**6件全て合格**を確認した（実行時間31ms）。
テスト内容も増分1の`SelectedConnectorExclusivityTests.cs`と対応するシナリオ（`SelectedCell`代入での
自動クリア、`ReplaceDocument`でのクリア、`Remove()`失敗時の防御）を正しくなぞっている。

### (4) 専用ハイライト表示の設計 —— **問題なし**

`WireBreak`は通常時「マーク無し」（`DiagramRenderer`は分断を横配線の空白として表現するのみ、
専用の図形記号を持たない）という提出品質の設計を維持しつつ、選択中のみ`LadderCanvas`が独自に
小さな塗り円（`SelectedWireBreakBrush`）を画面表示専用で描画する。この描画は`Ecad2.App.Views.
LadderCanvas`（WPF画面表示層）内に閉じており、PDF出力は別プロジェクト（`Ecad2.Pdf`）の別
`DiagramRenderer`インスタンス経由のため、印刷・PDF出力に影響しないというコメントの主張は
アーキテクチャ上正しいと判断する（T-040調査で確認済みの「`ShowGrid`等の画面専用オプションは
PDF出力に影響しない」という既存の設計原則と同型）。

### (5) 便乗変更なし —— **確認済み**

---

## 副次所見（severity低、対応不要と判断）

- **F10がTool.Modeを問わず発火する**：`case Key.F10 when noModifier:`に`Tool.Mode`のガードが無く、
  例えば縦コネクタ記入中（`Tool.Mode==PlaceConnector`、増分2）にF10を押すと、進行中のdraftとは
  独立に配線分断がその場で即時記入される。データ破損や競合は無い（`_connectorDraft`と
  `WireBreak`は完全に独立したデータ）が、「記入モード中に別の記入操作を割り込ませてよいか」は
  UI/UXの一貫性の観点でやや異例と考える。実害が小さいため今回の指摘のみに留める。
- 増分2レビュー所見C（矢印キー連打でのフルネットリスト再構築）はWireBreak機能には該当しない
  （F10は単発操作で連打を伴う調整キーが無いため）。

---

## 総括（増分1〜3を通じて）

- 増分1：4件CONFIRMED→修正済み・再レビュークリーン。
- 増分2：シート切替中の状態リーク（重大）をCONFIRMED→侍が修正往復1周目に着手中（家老采配済み、
  本レビュー未反映、別途再レビューを要する）。
- 増分3：単発（非draft型）操作のため増分2と同種のリスクがそもそも存在せず、増分1の確立済み
  パターンを正しく踏襲してクリーン。

**忍者実機確認は、増分2の修正完了・再レビュークリーンを待ってから増分1/2/3まとめて回すのが
効率的**と考える（増分3単独は先行してクリーンだが、家老采配の通り一括で回す方針に従う）。

---

## 出典・参照

- 対象コミット`9cc5b32`（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedWireBreak`/
  `DeleteSelectedWireBreak`/`PlaceWireBreakAtSelectedCell`）
- `src/Ecad2.App/MainWindow.xaml.cs`（`TryPlaceWireBreak`、F10ハンドラ、クリック排他分岐拡張）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`HitTestWireBreak`、`SelectedWireBreakBrush`）
- `tests/Ecad2.App.Tests/WireBreakSelectionTests.cs`（新規6件、実行して合格を実測）
- `docs/archive/ecad2-t041-key-flow-proposal-samurai.md`（4節、点系の記入フロー原案）
- `docs/archive/ecad2-t041-increment1-review-onmitsu-2.md`（`SelectedCell`setter集約パターンの確立経緯）
- `docs/archive/ecad2-t041-increment2-review-onmitsu.md`（シート切替中の状態リーク、対比参照）
