# T-068増分3-c 既存ポート再クランプ漏れ 独立調査（隠密）

最終更新: 2026-07-25（家老委譲、Wチェック隠密側。**侍の調査結果は見ずに独立で一次ソースのみを精読して判定**）

## 題目

忍者が3-c再検証中に発見した「`heightCells=1`のパーツに`RowOffset=-2`が保存されていた」事象。
新規追加時のクランプは効いている様子だが、後から基準枠（幅・高さ）を縮めると既存の接続点が
再クランプされず取り残される疑い。

## 結論（確定。疑いではない）

**一次ソース精読のみで根本原因を確定できた。** `src/Ecad2.App/Views/PartEditorCanvas.cs` の
`WidthCells`/`HeightCells` プロパティのsetterは以下のとおり：

```csharp
public int WidthCells
{
    get => _widthCells;
    set { if (_widthCells == value) return; _widthCells = value; Draw(); }   // 151-155行目
}

public int HeightCells
{
    get => _heightCells;
    set { if (_heightCells == value) return; _heightCells = value; Draw(); }  // 158-162行目
}
```

値変更時に `Draw()`（再描画）のみを呼び、既存 `_ports`（`List<PortDef>`）への
`PartShapeGeometry.ClampPort` 再適用を**一切行っていない**。

一方、新規追加・ドラッグ移動の経路では `ClampPort` が正しく呼ばれている：
- `AddPort()`（456行目）：`PartShapeGeometry.ClampPort(cell.X, cell.Y, _widthCells, _heightCells)`
- `UpdatePortDrag()`（472-474行目）：同様に`ClampPort`経由でクランプしてから`_ports[_selectedPortIndex]`へ代入

`ClampPort`本体（`src/Ecad2.Core/Model/PartShapeGeometry.cs` 79-85行目）：
```csharp
public static (int RowOffset, int BoundaryOffset) ClampPort(double cellX, double cellY, int widthCells, int heightCells)
{
    int boundary = Math.Clamp((int)Math.Round(cellX), 0, Math.Max(0, widthCells));
    int rowLimit = Math.Max(0, heightCells - 1);
    int row = Math.Clamp((int)Math.Round(cellY), -rowLimit, rowLimit);
    return (row, boundary);
}
```
`heightCells=1` なら `rowLimit=0` となり `RowOffset` は 0 のみが許容される。忍者の実例
（`heightCells=1`かつ`RowOffset=-2`）は、`heightCells=3`（この場合`rowLimit=2`で`RowOffset=-2`が
合法）の時点で追加・配置した接続点が、その後`HeightBox`入力で`heightCells`を1へ縮小した際に
再クランプされず取り残された、という経路でコードロジック上**必然的に**再現する。

**この非対称性（新規追加・ドラッグ＝クランプ有、基準枠変更＝クランプ無）が根本原因であり、
「疑い」ではなく一次ソースの読解だけで確定できる事実。**

なお、忍者の実機確認記録（`docs/ecad2-t068-increment3c-verification-ninja.md` 28行目）も
「`WidthCells`/`HeightCells`のsetterは`Draw()`のみ呼び既存`_ports`の再クランプをしていない実装と
見受けられる」と同一の見立てに達しているが、これは実機観察からの推定であり、本調査はそれとは
独立にコード精読のみで到達し確証を取った（家老指示どおり侍の調査結果は参照していない）。

## PR-03・PR-05との同型性判定（家老の予備の当たりに対する独立判定）

いずれも**完全一致ではない**と判定する。

- **PR-03（SetPropertyの早期return罠）とは不一致。** PR-03は「値が実質変化したのにSetPropertyの
  比較値（index等）が偶然一致し、本来発火すべき処理が丸ごとスキップされる」型。今回のsetterは
  値が変化した場合には`if`をすり抜けて`Draw()`を正しく実行しており、早期return自体は問題を
  起こしていない。欠陥は「値変化時に実行すべき処理（既存ポートの再クランプ）がそもそも
  実装されていない」という**欠落**であり、早期return判定ロジック自体に瑕疵はない。
- **PR-05（状態リセット処理の横展開漏れ）とも完全一致ではない。** PR-05は「既存の同種処理に
  確立済みの責務（UndoManagerクリア等）へ、新設処理が追従し忘れる」型。本件は性質として
  PR-04（境界値/クランプ検証漏れ——新しい図形要素・操作を追加する際に既存の境界クランプが
  移植されない）に近い。ただしPR-04は典型的に「新規要素の追加」を指すのに対し、本件は
  「既存の基準値（WidthCells/HeightCells）を**変更**した際、その基準値に依存する派生データ
  （Ports）の整合性が再検証されない」という型であり、微妙に異なる。新規パターン候補となり
  うるが、正式な分類・記帳要否は家老・殿の判断に委ねる（隠密の役儀は調査までであり、
  台帳への記帳判断は自らは行わない）。

## 実害の範囲（すべて一次ソース確認済み）

| 経路 | ファイル:行 | 状態 | 実害 |
|---|---|---|---|
| 保存 | `PartEditorDialog.xaml.cs` 273-292行目 | 無防備 | `ShapeCanvas.Ports`をそのまま`OrderBy(BoundaryOffset)`してJSON永続化。異常値がそのまま書き出される（忍者実例どおり） |
| 読込 | `PartEditorCanvas.cs` `LoadContent` 182-189行目 | 無防備 | `_ports = ports.ToList()`でそのままコピー、クランプ処理なし |
| エディタ内描画 | `PartEditorCanvas.cs` `Draw()` 692-698行目 | 無防備 | `_ports[i].BoundaryOffset`/`RowOffset`をそのまま`CellToLocalMm`へ渡す。基準枠の外側にポートのドットが視覚的に表示されうる（見た目異常） |
| ヒットテスト | `PartShapeGeometry.HitTestPort` 98-107行目 | 無防備 | 範囲チェックなし。範囲外のポートも選択・再ドラッグ可能 |
| 電気的結線判定 | `NetlistBuilder.cs` 139-158行目 | 無防備・実害大 | `e.Pos.Row + p.RowOffset`、`e.Pos.Column + p.BoundaryOffset`で実ノード座標を直接計算。異常値がそのまま意図しない座標のノードとして扱われ、他要素との誤結線・誤断線を招きうる（**シミュレーション結果に直結**） |
| 図面描画の左右境界 | `DiagramRenderer.cs` `LeftBoundary`/`RightBoundary` 744-760行目 | 無防備 | `BoundaryOffset`のmin/maxで結線の左右境界を決定。`WidthCells`縮小側で同型の異常値が生じれば描画位置がずれる可能性（**理論上、未実機確認**） |
| PDF出力 | `PdfExporter.cs` | 無防備 | `DiagramRenderer`を再利用しているため上記描画・境界計算の異常がPDFにも波及する（**理論上、未実機確認**） |

**波及範囲は保存・読込・エディタ内描画にとどまらず、実際に配置された図面の電気的シミュレーション
（NetlistBuilder）にまで及ぶ**。これが本欠陥の実害のうち最も重大な点。

---

## 追記（2026-07-25）救済処理（`LoadContent`側クランプ）の要否調査

家老委譲。題目＝実運用パーツライブラリに範囲外ポートが実在するか実測で裏取りし、`LoadContent`側での
救済（読込時クランプ）実装の要否判断材料を揃える。**ファイルは一切書き換えていない（Readのみ）。**

### 検分対象・結果

`C:\Users\kojif\OneDrive\ドキュメント\Ecad2\図形\` 配下、直下15件＋`自作\`配下10件（家老は11件と
見積もっていたが実測では10件。`.bak`1件を含む）の全`.gcadpart`を目視検分した。判定基準は
`PartShapeGeometry.ClampPort`の許容範囲（`boundaryOffset∈[0, widthCells]`、
`rowOffset∈[-(heightCells-1), heightCells-1]`）。

**直下15件（実運用パーツライブラリ）**：a接点・b接点・コイル・サーマル・セレクトSW・タイマ瞬時接点NC/NO・
タイマ接点NC/NO・モータ・押釦NC/NO・端子台・非常停止・表示灯。**範囲外ポートは1件も無し**——全件が
`rowOffset=0`（モータのみ含め全て`heightCells=1`ゆえ許容範囲は`{0}`のみ）、`boundaryOffset`も
`0`〜`widthCells`の範囲内に収まっていた（モータは`widthCells=3`で`boundaryOffset=0,1,2`、いずれも合法）。

**`自作\`配下10件（すべて本日T-068検証用に作成されたテスト部品、殿の実運用データではない）**：
4件で範囲外値を確認した。

| ファイル | widthCells/heightCells | 範囲外ポート |
|---|---|---|
| `T068検証部品.gcadpart` | 3/2 | `rowOffset=3`（許容`[-1,1]`を超過）・`boundaryOffset=5`（許容`[0,3]`を超過） |
| `T068増分3a検証部品.gcadpart` | 3/2 | `rowOffset=5`・`rowOffset=2`（いずれも許容`[-1,1]`を超過。`boundaryOffset`は両ポートとも範囲内） |
| `T068増分3a検証部品.gcadpart.bak` | 3/2 | 同上（`.gcadpart`本体と同一内容のバックアップ） |
| `T068増分3c再検証部品.gcadpart` | 1/1 | `rowOffset=-2`（許容`[0,0]`を超過。**忍者が発見した実例そのもの**） |

残り6件（`T068増分3b2キャンセル検証部品`・`T068増分3b3直線併合検証部品`・`T068増分3c検証部品`・
`T068増分3c退化検証部品`・`T101検証用自作部品`・`忍者テストパーツ02`）はポートが空、または範囲内の
値のみで異常なし。

### 家老推定の判定：**当たっていた**

**「救済対象は本日の検証用ファイルのみで、実運用パーツには範囲外ポートは存在しない」という家老の
推定は実測で裏付けられた。** 実運用15件は全件クリーン、範囲外値を持つのは本日作成の検証用テスト
部品4件のみ（いずれも`自作\`配下、殿の実運用パーツライブラリの外）。

なお検分中の気づき（範囲外調査とは別件）：`T068増分3b2キャンセル検証部品.gcadpart`はP1・P2の両ポートが
完全に同一座標（`rowOffset=0, boundaryOffset=0`）で重複しているが、これは`UpdatePortDrag`のコメント
「原本どおり、移動先が既存の接続点と重なっても弾かない（追加時のみ重複を見る非対称を踏襲）」どおりの
既知仕様であり、本題（範囲外クランプ）とは無関係と判断した。派生提案としての記帳要否は家老判断に委ねる。

### 救済処理（`LoadContent`側クランプ）を入れる場合の副作用の見立て

1. **「開いただけで黙って値が変わる」という驚き最小原則（POLA）への抵触**：`LoadContent`でクランプを
   実装すると、範囲外ポートを含むファイルを開いた瞬間（保存操作をする前）にメモリ上の値が書き換わる。
   ユーザーが何も編集せずそのまま上書き保存すれば、意図せず元のデータが変更される。「開いただけで
   ファイルの中身（の一部）が変わる」という副作用は、他の読込処理（現状クランプを一切行わない）とは
   毛色が異なる。
2. **現時点で実害は理論上のみ**：実運用パーツ15件に範囲外値は存在しないため、救済処理を入れなくても
   現状の実害は無い。今回の非対称バグ自体は侍が別途修正采配済み（setter側でのクランプ）と承知しており、
   修正後は新規に範囲外ファイルが生成されなくなる。**読込時クランプの意義は「過去に生成された汚れた
   ファイル（今回の検証用4件等）を将来開いた時の防御」に限定される**。
3. **代替案の余地**：黙って直す（読込時クランプ）以外に、範囲外値を検出した場合にログ/警告を出す、
   あるいはUndo可能な形で明示的にユーザーへ提示する、という選択肢もありうる。ただし実装方式の選定は
   家老・侍の采配範囲であり、隠密はここでは判断材料の提示に留める。
4. **殿へ諮るべきか否かの判断材料**：救済処理は「データを無断で書き換える」という性質を持つ以上、
   UI/UX・データ整合性のポリシーに関わる分岐と見受けられる。`memory: feedback_route_design_decisions_to_user`
   （UI/UX分岐は既定方針の延長に見えても必ず殿に確認する）に照らせば、**家老裁量で決めず殿へ諮る対象**
   ではないかと考える。ただし最終判断は家老に委ねる。

## 不明点

- `WidthCells`（→`BoundaryOffset`）側で同型の異常値が実際に生じた実例は未確認（忍者が発見したのは
  `HeightCells`→`RowOffset`側のみ）。コードロジック上は`WidthCells`側も対称に無防備であり、
  同様に発生しうると推定するが、これは推測であり実例による裏取りはできていない。
- 実際に異常値を持つポートを配置した図面でシミュレーション実行・PDF出力した際の具体的な見た目・
  挙動（誤結線が実際に顕在化するか）は未実機確認（忍者の持ち越し事項と重複）。理論上の影響経路は
  上記の通りだが、配置先の座標次第で他要素との衝突が起きなければ実害が顕在化しない可能性もある。

## 派生提案の有無

なし（範囲外の新規気づきは無し。本件は委譲された題目そのもの）。

---

## 追記（2026-07-25 その2）コミット`0660149`静的レビュー

家老委譲。RED証明の検算・PR-27の目・Undo/Redo冪等性テストの妥当性・スコープ境界の4点を重点確認。
`code-review`スキルは既知の恒久仕様により起動不可のため手動レビューで代替。

### 実測確認（ビルド・テスト）

Ecad2.App.exe不起動を確認のうえ実施。`dotnet build tests/Ecad2.App.Tests/...` 成功、新設4件
`dotnet test --filter PartEditorCanvasPortReclampTests` **全件GREEN**、App.Tests全体**812件合格**、
Core.Tests**239件合格**（回帰なし）。侍の申告と一致。

### RED証明の検算（机上、静的読解のみ。共有main上での一時注入はせず）

修正前（`ReclampPorts()`呼び出しなしの旧`WidthCells`/`HeightCells`setter）を仮定して4テストを
机上トレースした：
- テスト1・2＝修正前は`RowOffset`/`BoundaryOffset`が範囲外のまま残り、Assertと不一致で**RED**。
- テスト3・4＝いずれも入力がそもそもクランプ不要な値（範囲内の境界値・冪等ケース）のため、
  修正前後を問わず**GREEN**（修正の有無で結果が変わらない非退行テスト）。

「修正前2件RED」という侍の申告と一致する。STAスレッド包み込み（`RunOnSta`）は例外を`captured`へ
捕捉し`Join`後に再スローする一般的な形で、xUnitのAssert失敗例外（`System.Exception`派生）も正しく
伝播する構造であり、手法自体に疑義なし。

### 【最重要】Undo/Redoで幅・高さが同時に変化する場合、中間状態の誤クランプでデータが破損しうる

`ApplySnapshot`（594-601行目を含む608-615行目）は`_ports = snapshot.Ports`で先にポートを復元してから
`RestoreExternalState?.Invoke(external)`を呼ぶ。`RestoreExternalState`（`PartEditorDialog.xaml.cs`
237-242行目）の実装：
```csharp
private void RestoreExternalState(PartEditorExternalState state)
{
    WidthBox.Text = state.WidthCells.ToString();
    HeightBox.Text = state.HeightCells.ToString();
    SelectRole(state.Role);
}
```
`WidthBox`・`HeightBox`はいずれも`TextChanged="SizeBox_TextChanged"`（同一ハンドラ、XAML42・48行目で
確認済み）に結線されており、`SizeBox_TextChanged`（118-122行目）は**呼ばれるたびにWidthCells/HeightCells
両方をその時点のTextBox内容から再セットする**：
```csharp
private void SizeBox_TextChanged(object sender, TextChangedEventArgs e)
{
    ShapeCanvas.WidthCells = ParseCells(WidthBox.Text, ShapeCanvas.WidthCells);
    ShapeCanvas.HeightCells = ParseCells(HeightBox.Text, ShapeCanvas.HeightCells);
}
```
WPFの`TextBox.Text`代入は`TextChanged`を同期発火させるため、`RestoreExternalState`の1行目
（`WidthBox.Text = ...`）実行中に`SizeBox_TextChanged`が即座に走る。**この時点で`HeightBox.Text`は
まだ更新前（旧値）のまま**——よってこの1回目の呼び出しでは`ShapeCanvas.WidthCells`だけが新値になり、
`ShapeCanvas.HeightCells`は旧値のまま再セット（setterの早期returnで実質no-op）される。

この「`_widthCells`=新・`_heightCells`=旧」という**不整合な中間状態**で`ReclampPorts()`が発火し、
`ApplySnapshot`が直前に復元したばかりの`_ports`（スナップショットの正しい値）を、**まだ古いままの
heightCellsのrowLimitで誤ってクランプしてしまう**。`ClampPort`は非可逆（一度クランプされた値は元の
座標情報を失う）ため、その直後に2行目（`HeightBox.Text = ...`）で正しいHeightCellsがセットされても、
既に破損したRowOffsetは修復されない。

**具体的な発生条件**：Undo/Redoで復元先スナップショットのHeightCellsが直前の状態のHeightCellsより
大きい方向へ戻る（rowLimitが拡大する）場合、かつ復元先ポートのRowOffsetが中間状態（旧HeightCellsの
狭いrowLimit）を超えている場合。例（机上シミュレーション）：
- 復元先スナップショット＝`WidthCells=5, HeightCells=3`, `Ports=[(RowOffset=2, BoundaryOffset=4)]`
- Undo実行前の現在値＝`WidthCells=1, HeightCells=1`
- Undo実行：`_ports`がまず`(RowOffset=2, BoundaryOffset=4)`に置き換わる→`WidthBox.Text="5"`セットで
  `WidthCells`のsetter発火・`ReclampPorts()`が`(widthCells=5, heightCells=1(旧))`で計算
  →`rowLimit=Max(0,1-1)=0`ゆえ`RowOffset`が**2→0へ誤ってクランプ**される→`HeightBox.Text="3"`セットで
  `HeightCells`のsetter発火・`ReclampPorts()`が`(widthCells=5, heightCells=3)`で再計算するが、
  **既に0になった`RowOffset`はそのまま**（`Clamp(0,-2,2)=0`で変化なし）
- **最終結果＝`Ports=[(RowOffset=0, BoundaryOffset=4)]`。本来復元されるべき`RowOffset=2`が消失する。**

なお`ClampPort`の`boundary`計算は`heightCells`に依存しない式のため、この中間状態不整合は
`RowOffset`側にのみ生じる（`WidthBox.Text`が常に先にセットされる非対称な実装ゆえ）。

**これは机上（静的読解）による強い推定であり、実測での確認はしていない**（共有main上での一時注入
禁止のため、既存コードを一時的に書き換えての動作確認は避けた）。侍の冪等性テスト（テスト4）は
`WidthCells`のみを変化させ`HeightCells`を固定するシナリオであり、この「両次元が同時に変化し
中間状態で不整合が生じる」パターンを検出できていない。**実測での裏取り（新規テストケース追加、
または忍者による実機Undo確認）が必要**と考える。

### PR-27型の検出力不足（副次的発見）

テスト1（`HeightCells_Shrink_ReclampsRowOffsetOutsideNewRange`）は復元後`HeightCells=1`
（`rowLimit=0`、許容値が`{0}`のみに潰れる退化設定）を使っており、`RowOffset`の結果は
`ReclampPorts`内で`BoundaryOffset`と`RowOffset`の引数を取り違えていても常に`0`に収束するため、
**その種の取り違えバグを検出できない**（`BoundaryOffset`は`Assert`対象外で検証されていない）。
現状の実装（`ClampPort(p.BoundaryOffset, p.RowOffset, ...)`）は引数順序を正しく踏襲しており
実害はないが、テスト自体の検出力としては弱い。改善案＝`Assert.Equal(1, canvas.Ports[0].BoundaryOffset)`
を追加する、または復元後`HeightCells`を`rowLimit>0`となる値にする。対照的にテスト2
（`WidthCells_Shrink_...`）は`heightCells=1`固定ながら`widthCells=2`（退化していない値域）を使って
おり取り違えを検出できる——同一コミット内で片方は検出力があり片方はない、という非対称。

### Undo/Redo冪等性テスト（テスト4）の妥当性判定

「同一寸法への再セットがno-opであること」の確認としては妥当（早期return経由でのReclampPorts
未発火ケースを含め机上検算でも整合）。ただし前述のとおり**「複数次元が同時に変化する」ケースは
範囲外**であり、テスト名・コメントが示す「Undo/Redo整合性の核心」という位置づけには実は届いて
いない（単一次元の冪等性は確認できるが、Undo/Redoの実運用で最も起こりうる「幅と高さを両方
変えた後のUndo」は未検証のまま）。

### スコープ境界確認

`LoadContent`・`NetlistBuilder`への変更は無し（`git show --stat`で変更2ファイルのみ確認済み、
いずれも家老裁定どおり不触）。

### 総括

RED証明・PR-27型の一部・スコープ境界は問題なし。**Undo/Redoの中間状態誤クランプ**は机上推定ながら
確度が高く、実測裏取りと追加テスト（または設計変更＝`RestoreExternalState`が両値をセットし終えて
から`ReclampPorts`を1回だけ呼ぶ形への変更等）を要すると考える。テスト1の検出力不足は軽微だが
PR-27節への追加事例として記帳の価値があるかもしれない（記帳要否は家老判断）。

## 派生提案の有無（追記分）

Undo/Redo中間状態誤クランプの発見は委譲範囲内（観点3の深掘り）であり範囲外の新規気づきではないが、
念のため記す：`SizeBox_TextChanged`が2つのTextBoxで共有され「片方だけ更新された中間状態」を経由する
設計自体は、本件以外の将来の類似実装（幅・高さのような複数依存パラメータを持つ別ダイアログ等）でも
同型の罠になりうる可能性がある。制度化要否の判断は家老に委ねる。

---

## 追記（2026-07-25 その3）殿裁定・案3（保存時クランプ）静的レビュー

家老委譲。コミット`5645f07`（`0660149`のRevert）・`536ae56`（`PartOptimizer.ClampPortsToFrame`
新設）を対象に、revert完全性・順序妥当性・保存経路接続・原本準拠・PR-27の目・RED証明の6観点を
検証した。**GuiEcad原本の一次ソース確認により「setter方式（即時クランプ）自体が原本に無い挙動の
追加」と判明し、方針が保存時クランプへ転換**された旨、侍のコミットメッセージで確認した——本日
隠密が指摘した「Undo中間状態の誤クランプ」も、setter方式自体を撤回したことで原理的に消滅している。

### 観点1：revertの完全性 — 問題なし

`git show 5645f07`で確認。`WidthCells`/`HeightCells`のsetterはいずれも`{ if (_x == value) return;
_x = value; Draw(); }`という元の形へ完全に復元されており、`ReclampPorts()`メソッド自体も削除
（残骸なし）。新設テストファイル`PartEditorCanvasPortReclampTests.cs`も82行まるごと削除確認。
`git diff`上、追加分18行に対し削除も18行と完全対称で、revert漏れは無い。

### 観点2：クランプ→並べ替えの順序 — 実装は正しいが、根拠説明に不正確な点あり

侍の実装（`PartEditorDialog.xaml.cs:278-279`）：
```csharp
var ports = PartOptimizer.ClampPortsToFrame(ShapeCanvas.Ports, width, height)
    .OrderBy(p => p.BoundaryOffset).ToList();
```
侍の見立て「クランプでBoundaryOffsetが変われば昇順の並びも変わるため、クランプを先に行う」を
検算した。**`Math.Clamp`は単調非減少写像**（`a<=b ⟹ Clamp(a,min,max)<=Clamp(b,min,max)`）であり、
`ClampPort`内部の丸め処理（`Math.Round`）も単調非減少のため、合成写像として`ClampPort`全体が
単調写像となる。C#の`OrderBy`は安定ソートが仕様で保証されているため、**「クランプ→並べ替え」と
「並べ替え→クランプ」は、複数の具体例（境界値の衝突・順序逆転を狙った反例含む）で机上検算した
限り、常に同一の最終結果になる**（数学的にも、単調写像を挟んでも大小関係・同値関係が保たれる
ため成立する）。

**評価**：実装（クランプを先に行う）自体は正しく機能し、機能バグはない。ただし**「逆順だと並びが
崩れる」という根拠説明は理論的には不正確**——実際にはどちらの順序でも同じ結果になる。事実として
報告するが、実装のクローズ自体を妨げるものではないと判断する。

### 観点3：保存経路への接続（`OkButton_Click`） — 静的読解で正しさを確認

`width`/`height`は`OkButton_Click`冒頭（252-261行目）で`int.TryParse`＋範囲検証（`MinCells=1`〜
`MaxCells=12`）済みのローカル変数。パース失敗・範囲外の場合は`ShowError`して即returnするため、
`ClampPortsToFrame`へ渡る時点では常に妥当な値と確定する。`ShapeCanvas.WidthCells`/`HeightCells`
とは別変数だが、`SizeBox_TextChanged`（`WidthBox`/`HeightBox`双方に結線）が通常操作では両者を
常に同期させているため（不正値・範囲外時は`ParseCells`のフォールバックで`ShapeCanvas`側は不変、
かつ`OkButton_Click`側も同条件で早期returnするため、保存に到達する経路では食い違いが生じない）、
実質的に等価な値が渡ると判断した。接続自体は正しい。

### 観点4：原本準拠（編集中は一切クランプせぬこと） — 問題なし

`PartEditorCanvas.cs`を全文確認。`WidthCells`/`HeightCells`のsetterは`Draw()`のみ（151-162行目）、
`ClampPort`の呼び出しは`AddPort`（456行目）・`UpdatePortDrag`（472行目）の2箇所のみで、いずれも
増分3-c当初からの既存仕様（新規追加・ドラッグ時のクランプ、今回の変更対象外）。基準枠変更時の
即時クランプは影を残さず消えている。編集中にクランプが紛れ込む経路は無い。

### 観点5：PR-27の目（対称性・退化性チェック） — 概ね機能、コメントに軽微な不正確あり

新設8テストいずれも`RowOffset`と`BoundaryOffset`に異なる値を与えており、退化設定
（`heightCells=1`、rowLimit=0）は実例再現用の1件（`ClampPortsToFrame_HeightOne_AllowsOnlyRowZero`）
に意図的に限定——侍の配慮は実装に反映されている。

ただし、このテストのコメント「rowLimit=0の退化設定で...上記の取り違えが期待値と偶然一致して
しまう」は**部分的に不正確**と判定した。机上検算（`PortDef("P1",-2,1)`, width=3, height=1）で
`ClampPort`の引数（`BoundaryOffset`/`RowOffset`）を取り違えた場合、`RowOffset`側の結果は確かに
偶然一致する（0=0）が、**`BoundaryOffset`側の結果は不一致になる**（正しい実装=1、取り違え=0）。
このテストは`RowOffset`・`BoundaryOffset`の両方を`Assert`しているため、単独でも取り違えバグを
検出できる（コメントが示唆する「単独では検出できない」は不正確）。ただし実害はない——他の
テストと合わせた全体の検出力には影響しない、軽微な記述上の指摘に留まる。

### 観点6：RED証明の検算（机上、静的読解のみ）

コミット申告「引数取り違えで7件RED・高さ境界+1ずらしで4件RED・幅境界+1ずらしで2件RED」を
8テスト全件について机上トレースした。

- **引数取り違え**（`ClampPort(p.RowOffset, p.BoundaryOffset, ...)`のように取り違えたと仮定）：
  8件中7件（テスト1〜6・8）でRowOffset・BoundaryOffsetいずれかが期待値と不一致→RED。
  テスト7（`ClampPortsToFrame_DoesNotMutateInputList`、入力リスト非破壊性の確認）のみ、
  戻り値の中身でなく入力リストの不変性を見るテストのため取り違えの影響を受けず、GREENのまま
  残る。**7件REDという申告と完全に一致**。
- **高さ境界+1ずらし**（`rowLimit`計算を誤り1大きくしたと仮定）：影響を受けるのは`heightCells`が
  結果に効くテスト（1・2・5・8）で、いずれも期待値と不一致→RED。テスト6（範囲内不変ケース）は
  ずれても依然範囲内に収まり不変のままのため検出できない。**4件REDという申告と一致**。
- **幅境界+1ずらし**（`widthCells`上限を誤り1大きくしたと仮定）：`BoundaryOffset`が上限に
  達しているテスト（3・8のNetA）で不一致→RED、他は上限未達のため影響なし。**2件という申告と
  傾向が一致**（全数の網羅的トレースは行っていないが、代表例の検算で符合を確認した）。

机上検算はいずれもコミット申告と整合し、RED証明の信頼性に疑義は無いと判断する。

### 実測（ビルド・テスト）

Ecad2.App.exe不起動を確認のうえ`dotnet build src/Ecad2.sln`成功、`dotnet test`実測＝
**Core247件・App808件、全件合格**。侍申告と一致。

### 総括

6観点いずれも機能バグは無し。観点2（順序の根拠説明の不正確性）・観点5（コメントの部分的不正確性）
は軽微な記述上の指摘に留まり、実装のクローズを妨げるものではないと判断する。本方式（保存時のみ
クランプ、編集中は原本どおり不変）は、前回発見した「Undo中間状態での非可逆な誤クランプ」の
リスクをも構造的に消滅させており、setter方式より堅牢と考える。忍者の実機確認へ回してよいと
判断する。
