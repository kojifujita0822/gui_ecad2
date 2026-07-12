# T-080往復2周目 (a)ダブルクリック不成立の根本原因調査+テスト設計(隠密、独立系統)

- task_id: T-080
- 対象: (a) 物理ダブルクリックで行コメントエディタが開かない(忍者実機実測でNG確定、
  `docs-notes/ecad2-t080-ninja-verification-round2.md`)
- 手法: 静的読解 + Web一次情報調査。侍の並行修正(Wチェック方式)は参照していない(独立系統)。
- 対象コミット: `737abf0`時点の`src/Ecad2.App/MainWindow.xaml.cs`・`src/Ecad2.App/Views/LadderCanvas.cs`

---

## 結論

**根本原因を確定(高確度)**: WPFの仕様上、`MouseLeftButtonUp`系イベント(`PreviewMouseLeftButtonUp`含む)の
`MouseButtonEventArgs.ClickCount`は**ダブルクリックの2回目でも1に固定**され、2以上の値は
`MouseLeftButtonDown`系イベントでしか取得できない。ところが本実装(`MainWindow.xaml.cs:602`
`LadderCanvasHost_PreviewMouseLeftButtonUp`)は、**Up側イベントで`e.ClickCount == 2`を判定条件に
している**ため、物理的に正しくダブルクリックしても条件が構造的に成立し得ない。これは実装の
書き間違いというより、WPFのDown/Up非対称仕様(ダブルクリック判定はDown側でのみ有効)を見落とした
設計ミスである。

**反証した仮説**: 調査序盤に有力候補と見た`LadderCanvas.cs:53`の
`PreviewMouseLeftButtonDown += (_, _) => Focus();`(クリックごとに無条件でFocus()を呼ぶ設計)は、
WPF本体のソース確認により**否定**した(後述「反証の経緯」参照)。

---

## 根拠

### 1. 実測事実(忍者、一次情報)

`docs-notes/ecad2-t080-ninja-verification-round2.md`より:
- 着弾位置はヒット領域内(`xMm=212.33 > RightBusX=204.50`)、正しくヒットしている
- 殿の物理ダブルクリック(同一座標`posDip=(802.50,99.04)`、間隔約273ms)で、2件の
  `LadderCanvasHost_PreviewMouseLeftButtonUp`ログがいずれも`ClickCount=1`。`ClickCount==2`に
  到達せず

### 2. コード該当箇所

`MainWindow.xaml.cs:600-607`:
```csharp
// T-080: 行コメント記入(右母線右側のダブルクリック)。ツールモードを問わず優先判定する
// (GuiEcad踏襲、殿裁定=ダブルクリックトリガー)。
if (e.ClickCount == 2 && _viewModel.CurrentSheet is Ecad2.Model.Sheet rcSheet
    && LadderCanvasHost.HitTestRungCommentRow(position, rcSheet) is int rcRow)
{
    OpenRungCommentEditor(rcRow, rcSheet);
    return;
}
```
これは`LadderCanvasHost_PreviewMouseLeftButtonUp`(Up側ハンドラ)の中にある。

### 3. WPFの仕様(Web調査、複数独立情報源)

- [In WPF MapControlMouseLeftButtonUp.MouseButtonEventArgs.ClickCount is always 1 · Issue #344 · Mapsui/Mapsui](https://github.com/Mapsui/Mapsui/issues/344) ——
  「MouseUpイベントではClickCountが1に固定される。MouseDownイベントでは2以上を取得できる」仕様として言及。
- [#658 – An Easier Way to Handle Mouse Double Clicks | 2,000 Things You Should Know About WPF](https://wpf.2000things.com/2012/10/01/658-an-easier-way-to-handle-mouse-double-clicks/) ——
  「MouseDoubleClickイベントのClickCountも常に1。ダブルクリック検知はMouseDown側でClickCount==2を
  見るべき」と明記。
- [WPF Tutorial – Getting the DoubleClick Event - Wahab's Blog](https://hellowahab.wordpress.com/2012/12/03/wpf-tutorial-getting-the-doubleclick-event/) —— 同旨。
- 複数の独立ソースが一致しており、コミュニティで広く知られた仕様上の制約と判断してよい。

### 4. dotnet/wpf本体ソースでの裏取り

`https://github.com/dotnet/wpf` の`MouseDevice.cs`(WebFetch経由で確認、要約):
- `_clickCount`フィールドの計算(`CalculateClickCount`メソッド)は**`PreNotifyInput`
  (Down側のプレビュー段階)でのみ実行**され、`mouseButtonArgs.ClickCount = _clickCount;`という
  形でその場のイベント引数へ反映される
- `PostProcessInput`でMouseUp用の`MouseButtonEventArgs`を生成する箇所には、Down側のような
  ClickCount計算ロジックが見当たらない(Up側は独自に計算し直さない)
- 判定基準(`CalculateClickCount`)は「同じ場所・同じボタン・設定時間内」で、フォーカス状態は
  判定基準に含まれていない(要約からの読み取り、原文の全行確認はしていない=不明点として残す)

以上(2)(3)(4)を総合すると、実測結果(Up側で常に`ClickCount=1`)はWPFの仕様どおりの挙動であり、
コード側が本来Down側で判定すべきものをUp側で判定していたことが直接の原因と判断できる。

---

## 反証の経緯: Focus()呼び出し仮説について

調査序盤、`LadderCanvas.cs:49-54`のコンストラクタ内:
```csharp
public LadderCanvas()
{
    _children = new VisualCollection(this);
    Focusable = true;
    PreviewMouseLeftButtonDown += (_, _) => Focus();
}
```
が、クリックごとに無条件でFocus()を呼ぶ設計(T-002/T-006由来、コメントに明記)であり、
「PreviewMouseLeftButtonDownハンドラ内でのFocus()呼び出しがWPFの内部クリックカウント判定を
乱す」というアンチパターンに該当するのではと疑い、最有力候補として調査を進めた。

しかし上記(4)の確認により、**ClickCountの計算(`CalculateClickCount`)はイベントが実際に
ハンドラへディスパッチされる前の`PreNotifyInput`段階で完了しており、Focus()の呼び出しは
そのディスパッチ後(ハンドラ内)に発生する**ことが判明した。つまりFocus()呼び出しは、その
クリック自身のClickCount値には時系列上影響を与えられない。判定基準にフォーカス状態が
含まれないことも踏まえ、Focus()呼び出しは(a)の原因ではないと判断し、この仮説は取り下げる。

一次情報を確認せず先入観で仮説を確定させかけた点は反省点。[[feedback_self_correct_with_primary_sources]]の教訓どおり、実測・一次情報による再検証で率直に訂正する。

---

## 修正の方向性(参考情報、実装は侍マター)

- ダブルクリック判定(`e.ClickCount == 2`)を、Up側ハンドラ(`LadderCanvasHost_PreviewMouseLeftButtonUp`)
  から**Down側ハンドラ(`LadderCanvasHost_PreviewMouseLeftButtonDown`)へ移す**必要がある
- `LadderCanvas`は`FrameworkElement`継承であり`Control`ではないため、`Control.MouseDoubleClick`
  イベント(Bubbling、WPF標準機構)は使えない。Down側での自前`ClickCount==2`判定が妥当な方向性
- Down側には既に4つの分岐(選択中プリミティブのドラッグ開始判定、393-457行)があり、既存コメント
  「ツールモードを問わず優先判定」の優先順位をDown側でも維持する必要がある。**どこに挿入するかで
  優先順位が変わる**ため、この点はテスト設計(下記)でも観点として明示した
- 現在Up側にある行コメント判定ブロックを丸ごとDown側の先頭(既存4分岐より前、398行より前)へ
  移設するのが最小差分の案と見るが、ドラッグ系分岐との相互作用(ダブルクリック2回目のDownで
  ドラッグ開始条件が誤って成立しないか等)は侍が実装時に精査されたい

---

## テスト設計(仕様側からの起草、onmitsu.md「テスト設計の起草」節に基づく)

対象: (a)修正後の「行コメントダブルクリック検知」ロジック(Down側への移設後)

### 適用したテスト設計技法

- 同値分割・境界値分析(ClickCount値、ヒット領域内外)
- 状態遷移(エディタ開閉状態)
- ペア構成の対称性(既存4ドラッグ分岐との優先順位、F2キー経路との対称性)
- パラメタライズド活用(xUnit `[Theory]`+`[InlineData]`)

### 観点1: ClickCount値による同値分割・境界値(パラメタライズド)

| ClickCount | ヒット領域 | シート種別 | 期待結果 |
|---|---|---|---|
| 1 | 内 | 制御回路 | エディタは開かない(境界値: 1回目単独) |
| 2 | 内 | 制御回路 | エディタが開く(主要な正常系) |
| 3 | 内 | 制御回路 | 要仕様確認(下記「不明点」参照)——トリプルクリックで開くか、2回目で既に開いた
  状態から更に何か起きるか、既存コードの`==`(等値)条件をそのまま踏襲するなら**開かない**
  (`>=2`ではなく`==2`のため)。この差異は仕様上意図的か家老へ確認要 |
| 2 | 外(`xMm <= RightBusX`) | 制御回路 | エディタは開かない(`HitTestRungCommentRow`がnull) |
| 2 | 内 | 主回路(MainCircuit) | エディタは開かない(既存指摘G、`sheet.MainCircuit`ガード) |

`[Theory]`で`(int clickCount, bool inHitArea, bool mainCircuit, bool expectOpen)`の組を列挙する形を推奨。

### 観点2: 状態遷移

状態: `EditorClosed`(通常) / `EditorOpen(row)`(該当行のエディタ表示中)

| 現在状態 | 入力 | 次状態 | 備考 |
|---|---|---|---|
| EditorClosed | ClickCount==2・ヒット領域内・有効行 | EditorOpen(該当行) | 主要遷移 |
| EditorClosed | ClickCount==2・ヒット領域外 | EditorClosed(不変) | 誤発火防止 |
| EditorOpen(row=N) | 別行(row=M≠N)へClickCount==2 | 要仕様確認(下記) | 現行のUp側実装は
  `IsMainContentEnabled`でメインキャンバス自体を無効化するため、エディタ表示中は物理的に
  別行のダブルクリックが到達しない設計と思われるが、Down側移設後も同じ前提が保たれるか
  要確認(無効化のタイミングがDown処理より先に効いているか) |
| EditorOpen(row=N) | Escキー / Enter確定 / フォーカスロスト | EditorClosed | 既存(b)の範囲、本タスクの対象外 |

### 観点3: ペア構成の対称性

- **F2キー経路との対称性**: 往復1周目指摘I(F2キー経路)とダブルクリック経路は、いずれも
  最終的に`OpenRungCommentEditor(row, sheet)`へ到達する設計(コメント確認済み)。両経路で
  「同一行に対して同一の結果(エディタが開き、同じ初期テキストが表示される)」になることを
  ペアテストで保証する
- **既存Down側4分岐との優先順位対称性**: 縦コネクタ/配線分断/自由線/接続点のいずれかが
  選択中の状態で、その要素の上でダブルクリックした場合に、ドラッグ開始とダブルコメント編集の
  どちらが優先されるか。現行Up側コメント「ツールモードを問わず優先判定」を素直に読むなら
  ダブルクリック(コメント編集)が優先のはずだが、これは`ToolMode`(配置ツール)に対する優先であり
  「選択中プリミティブのドラッグ開始」に対する優先は明言されていない。**4パターンそれぞれで
  観点1の表と同じ行(ClickCount==2・ヒット領域内での期待結果)が定義されているか点検する**
  (T-041増分7のカバレッジ不整合の再発防止、onmitsu.md該当節に倣う)

### 観点4: 回帰確認(Down側既存4分岐への影響)

Down側への移設が、既存の縦コネクタ/配線分断/自由線/接続点のドラッグ開始判定(既存4分岐)を
壊していないことを、ClickCount==1の通常クリックでの各既存ドラッグ開始テスト(既存テストが
あればそれを再実行、無ければ新規に最小1件ずつ)で確認する。

---

## 不明点

- ClickCount>=3(トリプルクリック以降)の挙動仕様は未定義(GuiEcad踏襲元の挙動含め不明)。
  家老・殿へ確認要
- Down側移設後、`IsMainContentEnabled`によるメインキャンバス無効化(エディタ表示中)が、
  Down側のダブルクリック判定より先に効いて到達を防ぐか、通常のヒットテスト経路と同様の
  タイミングになるかは静的読解のみでは断定できず(不明、実装後の実機確認要)
- `MouseButtonEventArgs.ClickCount`のsetアクセサが public/internal のいずれか未確認
  (公式リファレンスの型シグネチャは`get()`のみ表示、テストコードから直接
  `new MouseButtonEventArgs { ClickCount = 2 }`のような初期化が可能かは侍の実装時に確認要。
  不可であれば、ロジックを`ClickCount`を引数に取る形へ抽出するなどテスト容易性の工夫が
  実装側で必要になる可能性がある)

---

## 検証の限界

本調査は静的読解+Web一次情報調査のみ(共有mainへの一時注入なし、侍の並行修正は未参照)。
WPF本体ソースの確認はWebFetchによる要約経由であり、`MouseDevice.cs`全文を一行ずつ検証しては
いない。実装後の実機確認(忍者マター)による最終裏取りを推奨する。
