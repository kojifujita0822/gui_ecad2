# P-167 再検分：主回路シートでDRC・接続判定を行うておるか（現時点の一次ソース）

隠密（key=1785925652116）記す。2026-08-05。家老采配より。

## 0. 題目・スコープ

**問い(1)のみ**＝**実装は本当にシート種別を見ておらぬか**。現時点の一次ソースで取り直す。
**(2)何が起きるか・(3)殿の御認識の性質**へは踏み込まぬ（家老のスコープ境界に従う）。

**前回所見**＝`docs/ecad2-t133-rowmatch-spec-questions-onmitsu.md`「采配4」節（2026-07-28）。
その後 **T-136(A)増分1（`SheetAffinity`新設）・T-136(C)・T-142** が入っておる。

---

## 1. 結論

### 1-1. **診断・接続判定は、今も主回路シートを見ておらぬ。7月28日の4件はすべて現時点でも成り立つ**

**箇所は行番号が動いたのみで、判定そのものは1つも変わっておらぬ。**

| # | 7月28日の所見 | 現時点 | 現在の箇所（検索文字列併記） |
|---|---|---|---|
| 1 | DRCが全シートを回す | **変わらず** | `OutputPanelViewModel.cs:73`／`foreach (var sheet in _owner.Document.Sheets)` |
| 2 | 右母線への自動接続が`MainCircuit`を見ぬ | **変わらず** | `NetlistBuilder.cs:227-242`／`private static void AddRightRailAutoConnections` |
| 3 | OR自動配線が`MainCircuit`を見ずに`Connectors.Add` | **変わらず**（旧`:3178-3179`→現`:3358-3359`） | `MainWindowViewModel.cs:3352-3359`／`public void ConfirmOrJoinTarget` |
| 4 | Simulation層は`MainCircuit`を知らぬ | **変わらず（0件）** | 下記2-1の再現手段 |

### 1-2. **されど7月28日の「シート種別による分岐は実装のどこにも無い」という言い回しは、今や不正確にござる**

**配置・移動の関門には分岐が入った**——T-136(A)増分1。

```csharp
// MainWindowViewModel.cs:3034-3037  （検索文字列＝`private bool ValidatePlacement`）
private bool ValidatePlacement(GridPos pos, int cellWidth, int cellHeight, Sheet sheet,
                               SheetAffinity affinity, ElementInstance? exclude = null)
    => PartResolver.IsAllowedOnSheet(affinity, sheet.MainCircuit)
    && IsWithinGridBounds(...) && !IsOccupied(...);
```

**すなわち現状は「置けるかどうかはシート種別を見る／診断するかどうかは見ない」という非対称にござる。**
**前回は両方とも「無い」であったゆえ一言で括れたが、今は括れぬ。**

### 1-3. 【差分】要素側の担保は1つ増えたが、**それもシート種別ではなく部品の性質による**

**`PartResolver.ParticipatesInWiring`**（T-136(C)＝三相モータ／T-142＝自作の`NonSimulated`、
`PartResolver.cs:59-64`／検索文字列＝`public static bool ParticipatesInWiring`）が新設され、
**非シミュレート部品が母線接続を横取りする穴が塞がれた。**

**7月28日に「前提を実際に守っておるのは要素側の性質のみ」と記した、その要素側が強まった形**にござる。
**判定の軸は`PartRole`／`ElementKind`にて、`Sheet.MainCircuit`ではない。**

---

## 2. 探した範囲（「無い」の証明ゆえ範囲を明示する）

### 2-1. 範囲A＝Simulation層の全ファイル → **0件**

**再現手段**：
```powershell
Get-ChildItem C:\ECAD2\src\Ecad2.Core\Simulation -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
  Select-String -Pattern 'MainCircuit|SheetAffinity|IsAllowedOnSheet'
```
**結果＝0件**（対象11ファイル＝`CircuitNumberer` `ConnectivityChecker` `CrossReference` `DesignRuleCheck`
`DeviceRenamer` `Evaluator` `Netlist` `NetlistBuilder` `SheetExtensions` `TestSession` `UnionFind`）。

**併せて`src`配下全体のファイル別集計でも、Simulation配下は1件も現れぬ**（下記2-4の表と二重に一致）。

### 2-2. 範囲B＝診断・接続判定の**入口を先に列挙してから尽くした**

**再現手段**（`src`配下、`.cs`＋`.xaml`＝**99ファイル**、`obj/bin`除外）：
```powershell
Get-ChildItem C:\ECAD2\src -Recurse -File -Include *.cs,*.xaml |
  Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } |
  Select-String -Pattern 'DesignRuleCheck\.|NetlistBuilder\.Build|ConnectivityChecker\.|RunDrc'
```
**生の一致＝19行。うち実呼び出しは11行**（内訳＝起動導線5行・コメント中の言及2行・定数参照1行を除いた残り。
`onmitsu.md`「grepの生の一致数をそのまま実呼び出し数として報じない」に従い分けて記す）。

**入口の単位＝「ネットリスト構築を起こす経路」で数えて3系統**（起動導線は別に4つ、下表）。

| # | 入口 | 箇所 | シート種別を見るか |
|---|---|---|---|
| 1 | **DRCパネル** | `OutputPanelViewModel.cs:66-83`／`private void RunDrc` | **見ぬ。** `Document.Sheets`を全件ループ（`:73`）。`CheckCrossReference`／`CheckDeviceTypeConsistency`／`CheckUnresolvedPartId`（`:69-71`）は`Document`を丸ごと受ける |
| 2 | **描画時の接続検査** | `DiagramRenderer.cs:270-271`／`var netlist = NetlistBuilder.Build(sheet, library);` | **見ぬ。** 描画のたび無条件に`Build`。接続診断の可否は`_opt.ConnectivityCheck`のみで分岐（`DiagramRenderer.cs:18`＝既定`false`）。**シート種別は条件に入らぬ** |
| 3 | **テストモード評価** | `TestSession.cs:27,48`／`var net = NetlistBuilder.Build(_sheet, _lib);` | **見ぬ。** `_sheet`の種別を問わず評価する |

**起動導線（4つ）**——(a)メニュー`MainWindow.xaml:964` (b)モード切替`MainWindowViewModel.cs:123`／
`OutputPanel.RunDrcCommand.Execute(null)` (c)描画のたび (d)テストモード操作のたび。
**(a)(b)は入口1へ帰着するゆえ、系統としては数えておらぬ**（数の単位を明示する）。

**なお(b)の直前`:116`／`GetOrCreateTestSession(sheet).Evaluate()`も現在シートの種別を見ておらぬ**
——**主回路シートを開いたままテストモードへ入れば、そのシートが評価される。**

### 2-3. 範囲C＝`NetlistBuilder`の母線接続3関数を丸ごと直読

- `AddHorizontalWireUnions`（`:209-222`）＝左母線接続。**`MainCircuit`条件なし**
- `AddRightRailAutoConnections`（`:227-242`）＝右母線自動接続。**条件は`rightBoundary<columns`・`RightRailReached`・
  `severed`・`CreatesComponent`の4つのみ**
- `LeftRailReached`／`RightRailReached`（`:245-260`）＝縦コネクタの有無のみを見る

**`Build`本体（`:18-120`）にも`sheet.MainCircuit`の参照は無い**（`sheet.Grid` `sheet.Elements` `sheet.Connectors`
`sheet.Bus` `sheet.WireBreaks` を参照）。

### 2-4. 範囲D＝`src`配下の`MainCircuit`全参照をファイル別に集計

**再現手段**＝`Get-ChildItem C:\ECAD2\src -Recurse -File | Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' } | Select-String 'MainCircuit' | Group-Object Path`

| ファイル | 件数 | 用途 |
|---|---|---|
| `DiagramRenderer.cs` | 12 | 描画・ページ分割 |
| `MainWindowViewModel.cs` | 12 | UI分岐・配置関門・縦コネクタ／自由線の可否 |
| `MainWindow.xaml.cs` | 9 | UI |
| `MainWindow.xaml` | 7 | UI |
| `PartResolver.cs` | 4 | **T-136(A)の`IsAllowedOnSheet`／文書コメント（新規）** |
| `PartPaletteViewModel.cs` | 4 | **配置可否によるパレット無効化（新規）** |
| `LadderCanvas.cs` | 4 | 描画 |
| `PartDefinition.cs` | 3 | **`SheetAffinity` enum定義（新規）** |
| `AddSheetDialog.xaml{,.cs}` | 3 | シート新規作成UI |
| `SheetNavigationViewModel.cs` | 2 | シート生成 |
| `ElementCatalog.cs`／`Sheet.cs`／`PartEditorDialog.xaml.cs` | 各1 | 種別の枷・モデル定義・UI |

**合計63件**（7月31日調査時は34件）。**増えた29件はすべて T-136(A) 系統＝配置制約・UI・文書コメントにて、
Simulation層は依然0件にござる。**

### 2-5. 範囲E＝7月28日以降のコミット履歴

**再現手段**＝`git log --since=2026-07-28 --oneline -- src/Ecad2.Core/Simulation/`

- **Simulation層へ入った変更は1件のみ**＝`2d272e8` fix(core): T-136(C) - 三相モータを結線から外す
- **`OutputPanelViewModel.cs`は同期間の変更0件**

---

## 3. 7月28日の自分の所見との差分（DoD）

| 観点 | 7月28日 | 2026-08-05 | 変わったか |
|---|---|---|---|
| DRCがシート種別を見るか | 見ぬ | **見ぬ** | **変わらず** |
| ネットリスト構築が見るか | 見ぬ | **見ぬ** | **変わらず** |
| Simulation層の`MainCircuit`参照 | 0件 | **0件** | **変わらず** |
| OR自動配線が見るか | 見ぬ | **見ぬ**（行番号のみ移動） | **変わらず** |
| **配置・移動の関門が見るか** | **見ぬ** | **見る**（`ValidatePlacement`第5引数） | **変わった** |
| 要素側の担保 | `CreatesComponent`のみ | **`ParticipatesInWiring`が加わった** | **強まった（軸は部品の性質）** |
| 「分岐は実装のどこにも無い」 | 成り立つ | **成り立たぬ** | **言い回しの訂正が要る** |

**7月28日の記述のうち、事実として訂正を要するのは最後の1行のみにござる。**
**残りは今日でもそのまま通る。**

---

## 4. 不明点・申し送り

- **【既に起きておることか、将来起きうることか】**（`onmitsu.md`の時制の戒めに従い分けて書く）
  **既に起きておる**＝上記1-1の4件は**今この瞬間に成立しておる構造**にて、条件待ちではござらぬ。
  **確かめた手段は静的読解のみ**——**実機で診断が実際に鳴るところまでは見ておらぬ**（忍者の領分）。
- **`SheetAffinity`の既定が`Any`である点は事実として記すに留める**（`PartDefinition.cs:87`／
  `public SheetAffinity SheetAffinity { get; set; } = SheetAffinity.Any;`）。
  **それが何を招くかの評価は問い(2)ゆえ本書では述べぬ。**
- **原本GuiEcadに同じ分岐が無いことは7月28日に確認済み**にて、本調査では取り直しておらぬ
  （現時点の ecad2 実装が問いの対象ゆえ）。**この一点のみ二次情報にござる。**

---

## 5. 報告

家老へ`send_message`で本書のパスと要旨を送る。
