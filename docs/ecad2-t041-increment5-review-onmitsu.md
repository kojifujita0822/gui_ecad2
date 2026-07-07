# T-041増分5 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット`d6f1747`（`feat(app): T-041増分5 - 自由線(FreeLine)/
> 接続点(ConnectionDot)の記入・消去・選択`）。家老指定観点(1)〜(6)＋`code-review`スキル
> （line-by-line/cross-file角度＋Reuse/Efficiency/Altitude/Conventions角度、2エージェント並行）
> 併用。実測検証（`dotnet test`）も併用した。

---

## 結論：**要修正（重大）。CONFIRMED：シートの削除操作が`CurrentSheetIndex`の数値を
たまたま維持したままシート実体だけを差し替えるケースで、`_freeLineDraft`を含む全選択状態の
クリア・キャンバス再描画の両方が発火しない。増分5固有のバグではなく増分1以来の既存欠陥だが、
今回`_freeLineDraft`/`SelectedFreeLine`/`SelectedConnectionDot`もこれを継承したまま増分5が
着地した**

家老指定観点(1)(2)(4)(5)(6)は問題なし。観点(3)（setter集約パターンの波及確認）は、想定していた
5経路（`SelectedCell`のsetter・`ReplaceDocument`・Escapeハンドラ・Delete優先順位チェーン・
マウスクリック排他分岐）については正しく波及しているが、**`code-review`の`cross-file`角度が
第6の経路（シート削除、`SheetNavigationViewModel.DeleteCommand`）を新たに発見しCONFIRMEDした**。
これを実際にコードトレースで検証したところ、増分5固有の新規バグではなく`CurrentSheetIndex`の
setter自体（増分1由来）に元々あった構造的な穴であることを確認した。新規テスト18件は実際に
`dotnet test`で実行し合格を実測、`src/Ecad2.sln`全体で**Core14件・App66件、全80件合格**を確認
（侍のregression proof報告「66件」と一致）。

---

## 対象差分

`git show d6f1747`で確認。`MainWindow.xaml.cs`+153/-6、`MainWindowViewModel.cs`+156、
`LadderCanvas.cs`+125/-1、新規`FreeLineConnectionDotSelectionTests.cs`(+137)・
`FreeLineDraftTests.cs`(+198)。増分4 PoC（`f11d478`、`poc/t041-freeline-hittest-poc`）も参照。

---

## 家老指定観点の検証

### (1) F9/sF9/F10のキー割当・シート種別切替が原案通りか —— **一致を確認**

`docs/ecad2-t041-key-flow-proposal-samurai.md`（案A）と照合：

- `case Key.F9 when noModifier:` → `TryBeginFreeLineDraft(horizontal: true)`。制御回路シートでは
  `TryBeginFreeLineDraft`内部の`!sheet.MainCircuit`ガードで「自由線の記入は主回路シートでのみ
  使用できます」を表示するのみで即return——原案「制御回路シート：F9＝当面未使用」と一致（案の
  意図した「無反応」に対し、ecad2は案内メッセージを出す形だが、既存のTryPlaceWireBreak等と
  同じ流儀であり実質的に相違はない）。
- `case Key.F9 when shift:` → `sf9Sheet.MainCircuit`で分岐し、主回路シートなら
  `TryBeginFreeLineDraft(horizontal: false)`（縦自由線）、制御回路シートなら
  `TryBeginConnectorDraft()`（縦コネクタ）——原案の「主回路：sF9＝縦自由線」「制御回路：
  sF9＝縦分岐線」と一致。
- `case Key.System when noModifier && e.SystemKey == Key.F10:` → `f10Sheet.MainCircuit`で分岐し
  `TryPlaceConnectionDot()`／`TryPlaceWireBreak()`——原案「F10＝両シート共通、シート種別で対象
  切替（制御回路→WireBreak／主回路→ConnectionDot）」と一致。

### (2) FreeLine/ConnectionDotのヒットテスト（nearest-wins）がPoC(f11d478)の設計と一致するか —— **完全一致を確認**

`LadderCanvas.HitTestFreeLine`の`DistancePointToSegment`は
`poc/t041-freeline-hittest-poc/T041FreeLineHitTestPoc/Program.cs:8-16`と計算式が一字一句一致
（`code-review`のReuse角度でも確認済み）。nearest-wins判定（`distance <= tolerance && distance <
bestDistance`）もPoCシナリオ5の「近接2セグメントから最短距離を選ぶ」設計を正しく汎化している。
許容誤差2.0mmもPoCの出発点と一致。`HitTestConnectionDot`（点と点の距離）も同様。

### (3) setter集約パターンがFreeLine/ConnectionDotへ正しく波及しているか、取りこぼしがないか —— **CONFIRMED：第6の経路（シート削除）に穴あり**

想定した5経路（`SelectedCell`のsetter・`ReplaceDocument`・Escapeハンドラ・Delete優先順位
チェーン・マウスクリック排他分岐）は、いずれも`SelectedFreeLine`/`SelectedConnectionDot`/
`_freeLineDraft`のクリアが正しく波及していることを確認した（grep・目視・実測テストの三重で
確認済み）。

**しかし`code-review`のcross-file角度が、これらとは別の第6の経路（シート削除）を発見し、
CONFIRMEDと判定した内容を独自に再トレースして検証した：**

`SheetNavigationViewModel.cs`の`DeleteCommand`：

```csharp
if (SelectedSheet is not Sheet sheet) return;
int index = Sheets.IndexOf(sheet);
_owner.Document.Sheets.RemoveAt(index);
Sheets.RemoveAt(index);
_owner.CurrentSheetIndex = Math.Min(index, Sheets.Count - 1);   // ★
```

`MainWindowViewModel.CurrentSheetIndex`のsetter：

```csharp
set
{
    if (!SetProperty(ref _currentSheetIndex, value)) return;   // ★数値が変わらなければ即return
    OnPropertyChanged(nameof(CurrentSheet));
    SelectedCell = null;   // ここでSelectedFreeLine/SelectedConnectionDot/_freeLineDraft等が
                           // すべて連鎖クリアされるはずだが、上のreturnで到達しない
    ...
}
```

**再現手順**：主回路シートS0(index0)・S1(index1)が存在し、S0を表示中（`CurrentSheetIndex==0`）。
セル選択→sF9で縦自由線の記入を開始（`_freeLineDraft`セット、`Tool.Mode=PlaceLine`）→矢印キーで
伸縮→Enter/Escで確定・取消せず、左パネルの「－」（シート削除）ボタンをマウスクリック
（`DeleteSheetButton`の`IsEnabled`は`DeleteCommand.CanExecute`＝`Sheets.Count > 1`のみで、
`Tool.Mode`による無効化は無いことを`MainWindow.xaml:291-292`で確認済み）。

`DeleteCommand`はS0を`Document.Sheets`/`Sheets`から除去し（S1がindex0へ繰り上がる）、
`index=0`・削除後`Sheets.Count=1`のため`_owner.CurrentSheetIndex = Math.Min(0, 0) = 0`——
**削除前から`_currentSheetIndex`は既に`0`だったため数値上「変化なし」と判定され、setterの
`SetProperty`が`false`を返して即return**。結果：

1. `OnPropertyChanged(nameof(CurrentSheet))`が発火せず、`RedrawCanvas()`が呼ばれない
   ——**画面には削除されたS0の最終描画がそのまま残り続ける**（実際にはS1を見ているはずなのに
   S0の古い見た目のまま）。
2. `SelectedCell = null`も実行されないため、その連鎖である`SelectedFreeLine`/
   `SelectedConnectionDot`/`ClearConnectorDraftIfAny()`/`ClearFreeLineDraftIfAny()`も一切
   実行されず、`_freeLineDraft`はS0の座標系（mm起点・`sheet.Grid`基準のクランプ）を保持した
   まま残留する。

この状態でEnterを押すと、`ConfirmFreeLineDraft()`は`CurrentSheet`（＝今はS1）に対して
`sheet.MainCircuit`（S1もtrueなら通過）をチェックするのみで、**S0基準で組み立てたFreeLineが
S1.FreeLinesへ混入する**——増分2で確認した「シート切替時のクロスリーク」と全く同型の実害が、
シート「切替」ではなく「削除」という第6の経路から到達可能であることを確認した。

**新規回帰テストとの関係**：`FreeLineDraftTests.cs`の
`SwitchingCurrentSheetIndex_WhileDraftingFreeLine_...`は`vm.CurrentSheetIndex = 1;`という
**数値が実際に変化する**ケースのみを検証しており、今回発見した「数値が偶然一致したまま実体が
差し替わる」削除経路はカバーしていない。この意味で、増分5自体は「想定した5経路については
正しく実装されている」が、**`CurrentSheetIndex`のsetter自体の構造的な穴（増分1由来、
`SelectedCell`のsetterの「値変化に関わらず常時クリア」という設計をこの上位のsetterには
適用していない）が、今回`_freeLineDraft`にも及ぶ形で表面化した**、と整理するのが正確である。

**対比（安全な経路）**：DRC出力パネルの同一シートジャンプ（`OutputPanelViewModel.JumpTo`）は、
`CurrentSheetIndex`の代入直後に`_owner.SelectedCell = ...`を無条件で再代入するため、setterの
早期returnの影響を受けない（増分2の再レビューで確認済みの一般原則）。今回のシート削除は
この「後追いのSelectedCell再代入」を持たないため、早期returnがそのまま影響してしまう。

**修正方針（参考、実装は侍判断）**：`CurrentSheetIndex`のsetter自体を、`SelectedCell`のsetterと
同じ設計（クリア処理を`SetProperty`の早期return判定より前に置き、値変化の有無に関わらず常時
実行する）へ改める、あるいは最低限`OnPropertyChanged(nameof(CurrentSheet))`と`SelectedCell = null`
を早期returnの対象から外すのが、既存パターンとの一貫性の観点で最も筋が良いと考える。

### (4) F10がWireBreak(制御回路)とConnectionDot(主回路)でキー共有する設計に矛盾がないか —— **矛盾なし**

sF9・F10とも同一の「`sheet.MainCircuit`で分岐」パターンで統一されており、キー共有の実装様式に
非対称は無い。`TryPlaceConnectionDot`/`TryPlaceWireBreak`とも同型の前提チェック
（HasProject→シート種別→SelectedCell）・即時記入・重複防止を持つ。

### (5) 水平・垂直限定(斜め線スコープ外)が正しく実装されているか —— **正しく実装**

`_freeLineDraft`の`IsHorizontal`フラグにより、`FreeLineDraftPreview`は水平線では`Y1Mm==Y2Mm`
（Y座標固定）、垂直線では`X1Mm==X2Mm`（X座標固定）を数式上保証する。`AdjustFreeLineDraft`
（`MainWindow.xaml.cs`）も水平線はLeft/Rightのみ、垂直線はUp/Downのみを有効な入力として扱い
（直交方向のキーは`delta=0`で無視）、斜め線が生成される余地がないことを確認した。

### (6) 便乗変更なし —— **確認済み**

---

## `code-review`スキル併用の追加所見

### 所見I（Efficiency、増分2所見Cの継続、対応不要と判断）: 矢印キー連打でのフルネットリスト再構築

`MoveFreeLineDraftEnd`も`MoveConnectorDraftRow`/`Column`と同型で、呼ぶたび
`OnPropertyChanged(nameof(FreeLineDraftPreview))`→`RedrawCanvas()`→
`NetlistBuilder.Build`のフル実行を誘発する。増分2で指摘済み・今回対応不要と判断した所見Cが
自由線側にも複製された形。severity・対応要否の判断は増分2レビュー時と同様（正しさの問題を
優先し、忍者実機確認で体感遅延の有無を見てもらう）。

### 所見J（Reuse、severity低）: 同一値のPen/Brushが複数のstatic fieldに分散

`SelectedConnectorPen`（`OrangeRed, 3.5`）と`SelectedFreeLinePen`（同じく`OrangeRed, 3.5`）が
別々のstatic fieldとして重複、`SelectedWireBreakBrush`と`SelectedConnectionDotBrush`も同様に
`Brushes.OrangeRed`の重複。実行時コスト・実害は無い（WPFの`Brushes.OrangeRed`はfrozen共有
インスタンス）が、共通の`SelectedHighlightPen`/`SelectedHighlightBrush`へ統合する余地がある。
同一コミット内で破線Pen側（`CreateConnectorDraftPen`→`CreateDraftPen`への一般化）は共有化した
のに対し、実践線側だけ非対称に個別定義のままという指摘も含む。

### 所見K（Altitude、増分1所見A・増分2所見Eの継続）: 選択プリミティブがさらに2種（+draft1種）増殖

`Tool`/`SelectedCell`/`SelectedConnector`/`SelectedWireBreak`/`_connectorDraft`に続き、
`SelectedFreeLine`/`SelectedConnectionDot`/`_freeLineDraft`が加わり、`SelectedCell`のsetter・
`ReplaceDocument`・Escapeハンドラ・Delete優先順位チェーンはいずれも「1状態につき数行」の
機械的増築を継続している。今回のコミット単体では新規の見落としを生んでいない（観点3の穴は
`CurrentSheetIndex`という別の箇所が原因）が、この分散構造自体が観点3のCONFIRMEDを生む土壌で
あることは明白であり、P-025（配置前検証サービス抽出）での状況証拠がまた1件積み上がった。

### Conventions —— **明確な違反は無し**

---

## 実測検証

`dotnet test --filter "FullyQualifiedName~FreeLineDraftTests|FullyQualifiedName~
FreeLineConnectionDotSelectionTests"`で新規18件全て合格（61ms）。`dotnet test src/Ecad2.sln`
全体でCore14件・App66件、計80件合格を確認した（侍のregression proof報告「66件」＝App側件数と
一致）。

---

## 侍への申し送り（修正方針、参考）

- 観点(3)のCONFIRMED（シート削除時の早期return）は、`CurrentSheetIndex`のsetter自体の修正で
  一括解消できると考える。`SelectedCell`のsetterが増分1修正で確立した「早期returnの前に無条件
  クリア」という設計を、この上位のsetterにも適用するのが最も一貫性が高い。
- 回帰テストとしては、`SheetNavigationViewModel.DeleteCommand`経由で「削除後もindex数値が
  変わらない」ケース（非末尾シートを削除、かつそのシートが現在表示中）を、記入中状態
  （`_freeLineDraft`または`_connectorDraft`）を絡めて追加することを推奨する。
- 所見I・J・Kはいずれも今回の対応必須ではないと判断する。

---

## 出典・参照

- 対象コミット`d6f1747`（`git show`で全差分確認）・`f11d478`（増分4 PoC）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`SelectedCell`/`SelectedFreeLine`/
  `SelectedConnectionDot`/`_freeLineDraft`関連メソッド、`CurrentSheetIndex`）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`DeleteCommand`、92-111行目付近）
- `src/Ecad2.App/MainWindow.xaml`（`DeleteSheetButton`のIsEnabledバインド、291-292行目）
- `src/Ecad2.App/MainWindow.xaml.cs`（F9/sF9/F10ハンドラ、`TryBeginFreeLineDraft`/
  `TryPlaceConnectionDot`/`AdjustFreeLineDraft`）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`HitTestFreeLine`/`HitTestConnectionDot`/
  `FreeLineEndpointDip`）
- `poc/t041-freeline-hittest-poc/T041FreeLineHitTestPoc/Program.cs`（比較対象PoC）
- `tests/Ecad2.App.Tests/FreeLineDraftTests.cs`・`FreeLineConnectionDotSelectionTests.cs`
  （新規18件、実行して合格を実測）
- `docs/ecad2-t041-key-flow-proposal-samurai.md`（案A、キー割当原案）
- `docs/ecad2-t041-increment2-review-onmitsu.md`・`-2.md`（シート切替中の状態リーク、対比参照）
- `code-review`スキル（line-by-line/cross-file角度＋Reuse/Efficiency/Altitude/Conventions角度、
  2エージェント並行、CONFIRMED1件・所見3件）
