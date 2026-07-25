# T-068 増分3-c（最終増分）静的レビュー（隠密、1周目）

対象コミット：`85a4a5f`（`feat(app): T-068増分3-c - 接続点ツール統合・端子リスト廃止`）
対象ファイル：`PartEditorCanvas.cs`（127行差分）・`PartEditorDialog.xaml`（40行差分）・
`PartEditorDialog.xaml.cs`（76行差分）・`PartShapeGeometry.cs`（35行追加）・
`PartShapeGeometryTests.cs`（91行追加、テスト18件）

T-068の最終増分のため、丁寧に検証した。

## 結論：要修正なし

削除作業6項目とも独立検索で残存ゼロを確認。選択状態の二重化6点はいずれも正しく実装されている
（samurai.mdチェックリストとの突合で1点、明示列挙漏れを発見したが実装自体は正しい）。幾何ロジック
テスト18件は全て手計算で検出力を確認、原本準拠・既定値引数の必須化とも問題なし。

## 1. 【最重点】削除作業の取りこぼしの独立検算

侍の申告を鵜呑みにせず、削除対象6項目を`grep`で独立に検索した：

```
grep "PortRow|PortsGrid|AddPortButton|DeletePortButton|PortsPanel" src/Ecad2.App → 0件
grep 同上 tests/ → 0件
```

**5項目（`PortRow`/`PortsGrid`/`AddPortButton_Click`/`DeletePortButton_Click`/`PortsPanel`）は
残存ゼロを確認**。残る1項目「右列`ColumnDefinition`」は名前検索できないため、XAML差分を直接確認——
`Grid.ColumnDefinitions`（2列構成）自体が`Border`単体（`local:PartEditorCanvas`のみ）へ置き換わり、
削除されていることを確認した。

さらに、削除対象リストに明示されていない関連の残骸も確認した：
- `using System.Collections.ObjectModel;`（`ObservableCollection<PortRow>`用）が削除されている
- `PortRow`クラス定義（DataGrid行DTO）自体が削除されている
- `PortsGrid.CommitEdit`関連のコメント・呼び出し（DataGrid既知の罠対処）が削除されている
- 「端子（T-068増分2、増分3-cでキャンバス上の接続点ツールへ統合予定）」というセクションコメントが
  削除され、実態に即したコメントへ更新されている

**削除対象6項目・関連する残骸とも取りこぼしなしと判断する。**

## 2. 選択状態の二重化（6点の実装確認＋samurai.mdチェックリストとの突合）

侍が着手前に列挙した6点を実装コードで1つずつ確認した：

1. **選択の排他**：`BeginSelectOrMove`で接続点ヒット時`_selectedPortIndex=idx; _selectedIndex=-1;`、
   図形ヒット時`_selectedIndex=idx; _selectedPortIndex=-1;`——相互に確実にクリアしている
2. **Deleteキーの分岐**：`case Key.Delete when ... && (_selectedIndex>=0 || _selectedPortIndex>=0):`
   ——OR条件で両方カバー
3. **削除ボタンの分岐**：`DeleteShapeButton.IsEnabled = SelectedIndex>=0 || SelectedPortIndex>=0`、
   実処理`DeleteSelected()`内で`_selectedPortIndex>=0`を先にチェックしポート優先で削除
4. **Escの解除対象**：`HasDraft`に`|| _portDragStartCell is not null`を追加、`CancelDraft()`で
   ドラッグ中のポート位置を元に戻す処理を追加
5. **ヒットテストの優先順位**：`BeginSelectOrMove`で接続点を先にチェックしヒットすれば`return`
   （図形のヒットテストへ進まない）——GuiEcad原本の「接続点優先」を正しく踏襲
6. **Undo/Redo復元時のクリア**：`ApplySnapshot`で`_selectedIndex=-1; _selectedPortIndex=-1;`両方クリア

**6点とも実装で確認できた。**

`samurai.md`「新規選択可能状態の横展開チェックリスト」（9項目）と突き合わせたところ：
- **項目3・7（矢印キーによる分岐・平行移動）**：本キャンバスは矢印キーでの選択移動機能自体を
  持たない（マウスドラッグのみ）ため、該当なし・見送って良い
- **項目8（setterをバイパスする状態リセット経路）**：`LoadContent`（旧`LoadPrimitives`、ダイアログ
  起動時の全状態リセット）を確認したところ`_selectedIndex=-1; _selectedPortIndex=-1;`は
  **正しく両方クリアされている**。ただし、**侍の6点列挙にはこの経路が明示的に含まれていない**
  （「Undo/Redo復元時のクリア」＝`ApplySnapshot`とは別の経路）。結果は正しいが、列挙の機械的な
  網羅性という観点では、チェックリスト項目8に相当する経路が個別に意識されていなかった可能性がある。
  **実害なし（実装は正しい）が、次回同種の新規状態追加時は「setterバイパス経路」を独立した項目として
  明示的にチェックすることを推奨する**
- **項目9（派生表示プロパティ）**：接続点用の`SelectedElementXxx`相当のプロパティパネル表示は
  新設されていないため該当なし

## 3. 幾何ロジックの切り出しとテスト（PR-27の目で検算）

`ClampPort`/`IndexOfPortAt`/`HitTestPort`とテスト18件を1つずつ手計算で検算した。

**`ClampPort_KeepsPortWithinFrame`（Theory8ケース、width=5・height=3）**：全8ケースを検算し期待値と
一致。`rowLimit=Max(0,height-1)=2`（範囲`[-2,2]`）・`boundary`は`[0,5]`。各ケースで境界外・内側・
丸め（上下）・負値をカバーしており、境界（幅5≠高さ3）の非対称性によりX/Y取り違えも検出できる
（例：ケース4`(0,-7)→(row=-2,boundary=0)`は境界の期待値が偶然0だが、行の期待値`-2`は取り違え版では
`0`になり確実に検出できることを確認）

**`ClampPort_HeightOne_AllowsOnlyCenterRow`・`ClampPort_ZeroWidth_AllowsOnlyBoundaryZero`**：退化
ケース（高さ1・幅0）を検算、いずれも正しくクランプされることを確認

**`IndexOfPortAt_FindsPortAtGivenGridPosition`**：**特に優れた設計**——`P2(Row=1,Boundary=2)`と
`P3(Row=2,Boundary=1)`という「行と境界を入れ替えた」2点を意図的に配置。もし実装が引数を取り違えて
いたら`IndexOfPortAt(ports,1,2)`の呼び出しが`P3`（本来`P2`が正解）にヒットしてしまい確実に検出できる
——手計算で裏取り済み

**`HitTestPort_MapsBoundaryToXAndRowToY`**：`Port(Row=1,Boundary=4)`に対し`HitTestPort(ports,4,1)`＝
ヒット・`HitTestPort(ports,1,4)`＝非ヒットを検算。もし実装内でx/y対応が逆なら、両呼び出しの結果が
入れ替わり両方向で確実に検出できることを確認した。**まさに家老が伝えた「侍が着手時点で気づいた
取り違えやすさ」（`BoundaryOffset`=x・`RowOffset`=y、`PortDef`宣言順とは逆）を直接検証するテスト**

**`HitTestPort_BeyondTolerance_ReturnsMinusOne`・`OverlappingPorts_PrefersLastPlaced`・
`EmptyList`系**：境界値・優先順位・空リストとも検算し期待値と一致

**軽微な指摘**：`ClampPort`の丸め処理（`Math.Round`、C#既定はBankers Rounding＝偶数丸め）について、
`.5`ちょうどの境界値テストは無い（`.4`/`.6`のみ）。ただし既存の`Snap`関数と同じ丸め方式の流用であり
新規リスクではないため、修正不要と判断する。

**総合判断：18件とも正しい検出力を持ち、致命的な穴は見当たらない。**

## 4. 原本準拠（設計書§3.2との突合）

- **クランプ値域**：`ClampPort`が`BoundaryOffset∈[0,幅]`・`RowOffset∈[-(高さ-1),高さ-1]`を実装、
  GuiEcad原本（Exploreエージェント一次ソース調査、`Math.Clamp`による同一範囲）と一致
- **追加時のみの重複チェック**：`AddPort`は`IndexOfPortAt`で重複を確認し無視（原本踏襲、警告なし）、
  `UpdatePortDrag`は重複チェックなし（コメント「原本どおり、移動先が既存の接続点と重なっても弾かない」）
  ——非対称性も含め原本と一致
- **ヒットテストのポート優先**：§2で確認済み、原本踏襲
- **自動命名`P{count+1}`**：`AddPort`内`$"P{_ports.Count + 1}"`、原本踏襲

**いずれも設計書§3.2と一致、逸脱なし。**

## 5. 既定値付き引数の必須化（3-b3申し送りの回収）

`public PartEditorDialog(PartDefinition? edit, bool isDarkMode)`——既定値を削除し必須引数化。
呼び出し元を`grep`で確認したところ`MainWindow.xaml.cs`470・517行の2箇所とも既に
`_viewModel.IsDarkMode`を渡しており、コンパイルエラーは発生しない。**3-b3で申し送った懸念
（既定値付き引数が将来の「渡し忘れ」の温床になりうる）が適切に回収された。**

## 6. 既知トラップ

- **SetProperty早期return罠（PR-03）**：該当なし。新規プロパティに該当パターンなし
- **PR-13型（CanEditDiagramガード漏れ）**：該当なし。モーダルダイアログ内の変更のみ
- **既存テストへの影響**：Core.Tests204→239（35件追加、往復1周目9件+3-b3 18件+3-c 18件の累計と
  整合）、App.Tests808不変。回帰なしの申告どおりと判断する

## 不明点

なし。

## 派生提案

§2の「setterバイパス経路（項目8）が侍の6点列挙に明示されていなかった」点は、結果的に実装は正しい
ため要修正ではないが、今後の新規選択可能状態追加時の着手前列挙に活かせる気づきとして家老へ申し送る。

---

## 忍者検出バグへの横断確認（Wチェック、家老采配2026-07-25）

忍者が3-c実機確認で検出した実装バグ（`PartEditorDialog.xaml.cs`の`Tool_Checked`switch式に
`"Port" => PartEditTool.Port`が欠落、デフォルト分岐`Select`へ落ちる）を自分の目で確認した
（124-140行、確かに`"Port"`ケースが無い）。原因は既に忍者の一次ソース確認で確定済みのため、
原因の独立調査は行わず、**「他に同型の漏れがないか」の横断確認**を実施した。

### 横断確認：`PartEditTool`が関わる全分岐箇所の洗い出し

`grep "PartEditTool\."`で全参照箇所を洗い出し、分岐箇所を1つずつ精査した。

| 箇所 | 9値の扱い | 判定 |
|---|---|---|
| `Tool_Checked`（`PartEditorDialog.xaml.cs`128-138行） | `Port`欠落、デフォルトへ落ちる | **バグ（確定済み）** |
| `ToolLabel`（同207-215行） | 8明示＋デフォルト(Select)＝9値全カバー | 問題なし |
| `ToolGuide`（同222-227行） | 5明示（Polyline/Arc/Rotate/Text/Port）＋デフォルト(4値)＝9値全カバー | 問題なし |
| `OnMouseLeftButtonDown`switch（`PartEditorCanvas.cs`240-266行） | 9値全て明示ケース（デフォルトなし） | 問題なし |
| `OnMouseMove`switch（同294-313行） | 7値のみ（Text・Port無し） | **意図的**（クリック一発で完結する単発操作のため、ドラッグ中のプレビュー更新が不要） |
| `OnMouseLeftButtonUp`switch（同320-340行） | 6値のみ（Polyline・Text・Port無し） | **意図的**（Polylineは右クリックで確定、Text・Portはクリック時点で完結） |
| `OnMouseRightButtonDown`の`if`（同346行） | Polylineのみ | **意図的**（右クリック確定はPolyline固有の操作） |
| `Key.Delete`のwhen条件（同401行） | Selectのみ | **意図的**（削除は選択中の対象への操作） |
| `BuildPrimitive`（同552-558行） | Line/Rect/Circle/Arc4値＋デフォルトnull | **意図的**（Line/Rect/Circle/Arcツールの`OnMouseLeftButtonDown`ケースからしか呼ばれない） |

**結論：`Tool_Checked`の1箇所のみが真の漏れ。他の箇所はいずれも「一部のツールのみ機能上関係する」
という設計上の意図であり、追加漏れではない。同型の漏れは他に見当たらない。**

### 振り返り：なぜ静的レビューで捕まらなかったか

3-b2往復2周目レビュー（`docs/ecad2-t068-increment3b2-review-onmitsu.md`）で「`PartEditTool`全7種を
`ToolLabel`・`ToolGuide`とも漏れなくカバー」と確認した。3-b3（文字ツール追加）・3-c（接続点ツール
追加）のレビューでも、**「ツール別分岐の網羅性」という観点を`ToolLabel`・`ToolGuide`という表示系
2箇所に固定化し続けており、`Tool_Checked`（RadioButtonのTag文字列→enum変換という入力系の分岐箇所）
を横断的に洗い出す視点が一度も無かった**——自分のレビュー記録を確認しても、3件とも`Tool_Checked`
という関数名への直接の言及が無い。**「前回のレビューで確認した箇所を再確認し、確認範囲外だった
箇所を見落とす」という家老の指摘は正確**である。

### 位置づけについての所見（家老依頼）

`samurai.md`「新規上位モード導入時の全入力経路列挙チェックリスト」（PR-12）とは、対象の性質が
異なると考える——PR-12は「マウス/キーボード/メニュー/ツールバー」という**機能的な入力経路の種類**
を列挙するのに対し、本件は「特定のenum型（`PartEditTool`）を参照する全switch文/switch式という
**構文的な分岐箇所**」を列挙する話である。根本の「新要素導入時に関連箇所への横展開が必要」という
リスク構造は同型（PR-01/PR-12の親戚筋）だが、発見手段は異なる（PR-12は経路の機能的列挙、本件は
`grep "PartEditTool\."`のような構文的な機械列挙が有効）。

加えて、**根本要因の一端はPR-06（型強制不足）にも通じる**と考える——`Tool_Checked`は
「`RadioButton.Tag`という文字列」から`PartEditTool`enumへ変換する設計であり、C#の`switch`式は
enum値に対する網羅性チェック（コンパイラ警告）が効く場合があるが、**文字列パターンマッチには
効かない**（文字列は無限の値を取りうるため）。新しいenum値を追加するたびに対応する文字列ケースを
人力で追加する必要があり、コンパイラによる強制が及ばない構造的弱点がある。

**新規パターン候補としての提起**：「新しいenum値を追加する際、その型を参照する全switch文/switch式を
`grep`等で機械的に列挙し突合する」チェックリストの新設を提案する。PR-12（入力経路列挙）と対になる
「enum値の分岐箇所列挙」という観点として、`samurai.md`・`onmitsu.md`双方への追記を検討いただきたい。
制度化の要否・記帳先は家老の判断に委ねる。

---

## 総括（T-068最終増分を受けて）

増分0（PoC）〜3-c（最終増分）まで、隠密の静的レビューを通じて一貫して確認してきたのは、GuiEcad原本
との対比・座標変換の数式検算・テスト検出力の手計算裏取りという手法が、いずれも実際にバグ・穴の
発見に結びついたという実績である（増分3-b1のRotatePoint符号穴・増分3-cのIndexOfPortAt/HitTestPortの
意図的な取り違え検出テスト等）。T-068は静的レビューのみで最終増分まで到達し、要修正の見送りなく
決着する見込みとなった。
