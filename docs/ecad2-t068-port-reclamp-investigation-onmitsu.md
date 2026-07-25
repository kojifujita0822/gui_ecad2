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
