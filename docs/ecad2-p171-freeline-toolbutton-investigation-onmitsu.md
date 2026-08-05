# `P-171` 自由線ツールのボタンが効かぬ——原因の特定

隠密（key=1785925652116）記す。2026-08-05。家老采配（**原因の特定のみ。修正は殿の裁可を要する**）。

**射程の断り【MUST】**——**本書は静的読解のみにござる。実機は見ておらず、実測ではござらぬ。**

---

## 0. 結論

| 家老の問い | 答え |
|---|---|
| 1. 忍者の見立てた型か | **当たり申さぬ。**`Tool_Checked`は`PartEditorDialog`専用にて、本件とは別物 |
| 2. 同じ穴を持つツールは | **シート種別で排他になるボタンが4件**（下記3節）。**ただし「穴」ではなく仕様の疑いが濃い** |
| 3. 直すなら何行か | **未確定。**そもそも不具合か否かが定まり申さぬ（下記4節） |

**【最も申し上げたきこと】忍者の観測に内部矛盾がござる。**
**自由線と縦分岐線は`IsEnabled`が排他にて、同一シートで両方を押せる道理がござらぬ。**
**観測条件（どのシートで試したか）の確認が要り申す。**

---

## 1. 【問い1】忍者の見立ては当たらぬ——`Tool_Checked`は別物にござる

**`Tool_Checked`の所在＝`PartEditorDialog.xaml.cs:184`のみ**
（**再現手段**＝`src`配下`.cs`＋`.xaml`で`Select-String 'Tool_Checked'`、`obj/bin`除外。
一致10件はすべて`PartEditorDialog`）。
**パーツエディタのRadioButton群のハンドラにて、メインウィンドウの配置ツールバーとは無縁にござる。**

**メインウィンドウの自由線ボタンは別の実装にござる**——

| 経路 | 実装 |
|---|---|
| **ボタン（横線）** | `MainWindow.xaml.cs:3686`／`FreeLineHorizontalButton_Click` → `TryBeginFreeLineDraft(horizontal: true)` |
| **ボタン（縦線）** | `:3692`／`FreeLineVerticalButton_Click` → `TryBeginFreeLineDraft(horizontal: false)` |
| **キーボードF9** | `:2755`／`case Key.F9 when noModifier && ...` → `TryBeginFreeLineDraft(horizontal: true)` |
| **キーボードsF9** | `:2762`／主回路シートなら`TryBeginFreeLineDraft(horizontal: false)` |

**ボタンとキーボードは、まったく同一のメソッドを同一の引数で呼んでおり申す。**
**switch式の分岐欠落のような「ボタン側だけ処理が無い」構造ではござらぬ。**

## 2. ボタンとキーボードの差は一行のみ。それも犯人ではござらぬ

```csharp
private void FreeLineHorizontalButton_Click(object sender, RoutedEventArgs e)
{
    TryBeginFreeLineDraft(horizontal: true);
    ConsumeToolButtonFocusRestore(sender);   // ← ボタンにのみ在る
}
```

**`ConsumeToolButtonFocusRestore`（`:3442`）は、マウス由来なら`FocusCanvas()`を呼び申す。**

**`FocusCanvas()`（検索文字列＝`private void FocusCanvas`）を開いて確かめたところ**——

```csharp
var scope = FocusManager.GetFocusScope(LadderCanvasHost);
FocusManager.SetFocusedElement(scope, LadderCanvasHost);
Keyboard.Focus(LadderCanvasHost);
```

**フォーカスを移すのみにて、`SelectedCell`を書き換えており申さぬ。**

**これは重要にござる**——**`SelectedCell`のsetterは`ClearFreeLineDraftIfAny()`を呼ぶ**（`:481`）ゆえ、
**もし`FocusCanvas()`が`SelectedCell`を触っておれば、開始直後のドラフトが即座に消えて
症状と符合しておった。** **疑って開いたが、触っておらなんだ。**

**`TryBeginFreeLineDraft`（`:3580-3606`）自体にもフォーカス判定は無く、ガードは4つのみ**——
(1)`HasProject` (2)**`sheet.MainCircuit`（主回路シート限定）** (3)`SelectedCell`が非null
(4)`BeginFreeLineDraft`が真。

**すなわち、静的読解の範囲では「ボタン経路だけが効かぬ」理由は見当たり申さぬ。**

## 3. 【問い2】代わりに見つけたもの——**シート種別で排他になるボタンが4件**

**全ツールボタンの`IsEnabled`を一度に数え申した**
（**再現手段**＝`MainWindow.xaml`から`AutomationProperties.Name`と近傍の`IsEnabled`を対で拾う。
**下表の4件は個別に直読して確かめ済み**）。

| ボタン | `IsEnabled` | 有効なシート |
|---|---|---|
| **自由線(横線) F9**（`:1284`） | `CanPlaceOnMainCircuit` | **主回路のみ** |
| **自由線(縦線) sF9**（`:1294`） | `CanPlaceOnMainCircuit` | **主回路のみ** |
| **接続点 F10** | `CanPlaceOnMainCircuit` | **主回路のみ** |
| **縦分岐線 sF9**（`:1304`） | `CanPlaceOnControlCircuit` | **制御回路のみ** |
| **配線分断 F10** | `CanPlaceOnControlCircuit` | **制御回路のみ** |

**定義（`MainWindowViewModel.cs:386,389`）**——
```csharp
public bool CanPlaceOnMainCircuit    => IsMainCircuitSheet && CanEditDiagram;
public bool CanPlaceOnControlCircuit => IsControlCircuitSheet && CanEditDiagram;
// IsControlCircuitSheet => CurrentSheet is Sheet sheet && !sheet.MainCircuit;（:382）
```

**`IsMainCircuitSheet`と`IsControlCircuitSheet`は同一シートで両立せぬ**
——**すなわち上の5件は、常にどちらか一方の群だけが押せる。**

**「ボタンを押しても何も起きぬ」という症状は、相手シートに居る限り5件すべてで起き申す**
（**正確には押せぬ＝グレーアウトにござる**）。

## 4. 【最重要】忍者の観測に内部矛盾がござる——**観測条件の確認が要る**

**忍者の報告**（`proposed.md` `P-171`）＝
- **自由線ボタンは効かぬ**（`Invoke-Ecad2Button`でも物理クリックでも）
- **キーボードF9でのみ「ツール: 自由線記入」へ切り替わる**
- **他のツールは効く——a接点・グループ枠・縦分岐線**

**この三つは、同一シートでは両立いたし申さぬ。**

| もし試したシートが | 自由線ボタン | 縦分岐線ボタン | キーボードF9 |
|---|---|---|---|
| **主回路** | **有効（押せる）** | **グレーアウト（押せぬ）** | **切り替わる** |
| **制御回路** | **グレーアウト（押せぬ）** | **有効（押せる）** | **「主回路シートでのみ使用できます」で切り替わらぬ** |

- **「縦分岐線はボタンで切り替わった」が真なら、そこは制御回路シート**。
  **ならば自由線ボタンはグレーアウトしており、「効かぬ」ではなく「押せぬ」**
- **「キーボードF9で切り替わった」が真なら、そこは主回路シート**。
  **ならば縦分岐線ボタンはグレーアウトしており、押せなんだはず**

**→ 忍者は複数のシートを跨いで試し、それを一つの症状として束ねた疑いがござる。**

**【某は「忍者が誤った」と断じており申さぬ】**
**某は実機を見ておらず、忍者がどのシートでどの順に操作したかを知り申さぬ。**
**上は「実装がこうである以上、観測がこうなるはずだ」という静的な導出にすぎ申さぬ。**
**`CanEditDiagram`が偽（テストモード中）であれば両群とも押せぬ、という第三の筋もござる。**

**確かめていただきたきこと（忍者へ）**——
1. **自由線ボタンを押した時、そのシートは主回路であったか制御回路であったか**
2. **その時ボタンはグレーアウトしておらなんだか**（**「青くハイライト」は押下の視覚効果であって、
   活性状態とは別物にござる**）
3. **縦分岐線ボタンを試した時と、自由線ボタンを試した時は、同じシートであったか**

## 5. 【問い3】直すなら何行か——**未確定にござる**

**現状の実装は「シート種別で排他」という設計として筋が通っており申す**
（`MainWindowViewModel.cs:376-389`のdocコメントが「T-047、手動配線F9/sF9/F10系ボタンの活性制御に使う」
と明記、**意図的な設計**）。

- **仕様どおりであれば、直すべきものは無く0行にござる**
- **仮に「制御回路シートでも自由線ボタンを押せるようにする」なら、それは仕様変更にて殿の裁可事項**
- **仮に真に不具合（主回路シートでもボタンが効かぬ）であれば、
  本書2節のとおり静的読解では原因が見当たらず、診断ログの注入が要り申す**
  （`memory: feedback_diagnostic_log_escalation`）

**すなわち「何行か」を答える前に、4節の観測条件の確認が要り申す。**

---

## 6. 射程・不明点

- **静的読解のみ。実機は見ておらず、実測ではござらぬ。**
- **忍者の観測条件（シート種別・操作順・`CanEditDiagram`の状態）を知らぬ**ゆえ、
  **4節の矛盾は「実装からの導出」であって「忍者の誤りの証明」ではござらぬ。**
- **`ecad2-ui-automation`スキル6.2節の既知の罠**（忍者が引いたもの）**は某も開いており申さぬ**
  ——**忍者の領分の資料ゆえ。ただし`Tool_Checked`が`PartEditorDialog`専用である以上、
  同スキルの記述がメインウィンドウの配置ツールバーを指しておるなら、そちらも見直しが要る見込み。**
- **`Invoke-Ecad2Button`がグレーアウトしたボタンをどう扱うか**（例外か、無反応か）**は見ており申さぬ**
  ——**忍者が「物理クリックでも切り替わらぬ」と述べておるゆえ、UIA固有の問題ではない見込み。**

## 7. 報告

家老へ`send_message`で本書のパスと要旨を送る。
