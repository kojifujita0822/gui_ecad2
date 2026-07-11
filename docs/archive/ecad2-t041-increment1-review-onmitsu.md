# T-041増分1 静的レビュー（隠密）

> 2026-07-07 隠密レビュー。対象コミット`592131c`（`feat(app): T-041増分1 - 縦コネクタの選択モデル
> 新設+Delete統合`）。家老指定観点(1)〜(5)＋`code-review`スキル（medium、line-by-line/cross-file
> 統合角度＋Reuse/Simplification/Altitude角度、2エージェント並行）併用。

---

## 結論：**要修正（中程度）。選択の排他制御が4箇所で漏れており、いずれも実際に「幽霊ハイライト」
「意図しないコネクタ削除」「偽の未保存フラグ」を起こしうる。ヒットテスト自体・Deleteキー優先順位
・Escape連動は設計通り機能している**

家老指定観点(1)(4)(5)は問題なし。観点(2)（排他制御）に**CONFIRMED4件**、観点(3)（Delete優先順位）
はその派生としての実害が生じる。加えて`code-review`併用で、増分3・5を見据えた設計上の懸念
（Altitude）と軽微な重複（Reuse）を検出した。**今回追加された新機能（SelectedConnector/
HitTestConnector/DeleteSelectedConnector）に対する新規テストは0件**であり、これが4件の見落としが
検出されずに残った一因と考える。

---

## 対象差分

`git show 592131c`で確認。`MainWindow.xaml.cs`+29/-6、`MainWindowViewModel.cs`+29、
`LadderCanvas.cs`+44/-2。テストファイルの変更は無し。

---

## 家老指定観点の検証

### (1) ヒットテスト（列境界へのmm距離、許容誤差2.0mm）の妥当性 —— **問題なし**

`LadderCanvas.HitTestConnector`（117-132行目）は`Math.Abs(xMm - geo.X(c.Column)) > tolerance`で
列境界への距離を、`yMm < yTop - tol || yMm > yBot + tol`で行範囲（余白込み）を判定する。
`VerticalConnector`の生成不変条件（`MainWindowViewModel.cs`の`el.Pos.Row < pos.Row`により
`TopRow < BottomRow`が常に保証）の下で境界値の誤りは無い。`GridGeometry`（CellMm=9.0既定）に対し
許容誤差2.0mmは「セル内部の死角（記入対象外）を残しつつクリックしやすい」妥当な値と判断する。

**軽微な設計上の留意点（severity低、対応不要）**：許容誤差内に複数の縦コネクタが該当する場合、
`foreach`のリスト順で最初に見つかったものを返すのみで、クリック位置に最も近いものを選ぶ実装には
なっていない（`sheet.Connectors`内の登録順＝通常は配置順が優先される）。T-044のOR連鎖で近接・
一部重複する縦コネクタが生成されうることは既知（`docs/ecad2-t044-*`）だが、実際に当たり判定が
重複する具体的な配置手順までは確認できておらず、現時点では推測に留める。将来「意図と違うコネクタが
選択される」報告があれば、この点を疑うとよい。

### (2) SelectedConnector/SelectedCellの排他制御 —— **CONFIRMED、4箇所で排他が破れる**

新設コメント（`MainWindowViewModel.cs`）は「SelectedCellとは排他（呼び出し元が明示的に切り替える）」
と明記するが、実際に両者を切り替えているのは**マウスクリック時の1箇所**（`MainWindow.xaml.cs`
222-229行目）と、**Escapeキー層3**（同305-310行目）、および**シート切替（CurrentSheetIndexの
setter、実際の値変化時のみ）**の3箇所に限られる。以下の4箇所は`SelectedCell`だけを変更・クリアし
`SelectedConnector`を素通りするため、排他が崩れる：

1. **`MainWindow.xaml.cs:406`（`MoveSelectedCell`、矢印キー移動）**：縦コネクタをクリック選択
   （`SelectedCell=null`, `SelectedConnector=connector`）→キャンバスにフォーカスがある状態で矢印
   キーを押下→`current = SelectedCell ?? GridPos(0,0)`（394行目）によりコネクタとは無関係な原点
   から1マス動いた位置が`SelectedCell`にセットされる（406行目）が、`SelectedConnector`はそのまま
   残る。両方が同時に非nullになり、セル矩形ハイライトと縦コネクタ強調線が**同時に描画**される
   （視覚的な二重ハイライト）。さらにこの状態で**Deleteキーを押すと**、移動先セルに要素が無ければ
   `DeleteSelectedElement()`がfalseを返し、`DeleteSelectedConnector()`が発火して**元々選んでいた
   （もう見た目上は無関係になったはずの）縦コネクタが無警告で削除される**。
2. **`MainWindow.xaml.cs:462-467`（`ActivateSelectDefault`、ツールバー「選択」ボタン）**：メソッド
   名・既存コメントは「選択セル・ツールを一括で全解除する」意図だが`SelectedConnector = null`が
   抜けている。縦コネクタ選択中にこのボタンを押しても強調線が残ったままになる。
3. **`OutputPanelViewModel.cs:88-99`（`JumpTo`、DRC出力パネルからのジャンプ）＋
   `MainWindowViewModel.cs:100-117`（`CurrentSheetIndex`のsetter）**：setterは`SetProperty`が
   「値が変わらない」場合に`false`を返し即`return`するため（`ViewModelBase.cs:17`）、
   `SelectedCell = null; SelectedConnector = null;`のクリア処理自体が実行されない。DRCジャンプ先が
   **現在表示中と同じシート**の場合にこの経路を通る（DRCは同一シート内の問題を指すことが多く、
   稀なケースではない）。この後`JumpTo`自身が`_owner.SelectedCell = ...`で新しいセルを設定するが、
   `SelectedConnector`はクリアされないまま残る——1と同型の二重ハイライト・意図しない削除リスクが
   生じる。
4. **`MainWindowViewModel.cs:468-505`（`ReplaceDocument`、新規作成/開く）**：`_selectedCell = null`
   をsetterをバイパスして直接代入しているが、対応する`_selectedConnector`のクリアが**存在しない**
   （今回のコミットがこの既存メソッドへの追従を漏らした）。縦コネクタ選択中にCtrl+N/Ctrl+Oすると、
   `SelectedConnector`が**破棄されたはずの旧文書のVerticalConnectorインスタンス**を握ったまま残り、
   新しい（多くの場合まっさらな）シートに対してその古いColumn/TopRow/BottomRowで強調線が描画
   される——**新規作成直後の白紙シートに謎の線が出る**。さらにDelete押下で
   `sheet.Connectors.Remove(旧connector)`が新シートのリストに対して行われ（実際には何も削除
   されない、下記(3)参照）、`MarkDirty()`が呼ばれて**新規作成直後の「未保存の変更なし」状態が
   Delete一発で偽って「未保存」扱いになる**。

**この4箇所には共通の構造的原因がある**：`SelectedCell`を変更・クリアする既存の入口（setter含め
5箇所）のうち、今回`SelectedConnector`のクリアを追従できたのは3箇所のみで、残り2箇所
（`ActivateSelectDefault`・`ReplaceDocument`の直接フィールド代入）は見落とされた。特に
`ReplaceDocument`の`_selectedCell = null`直接代入パターンは、コメント自身が「隠密レビュー指摘
#1 CONFIRMED/#2 CONFIRMED軽微/#3 PLAUSIBLE→格上げ」（T-019当時、旧文書の状態残留を巡る過去の
同種の見落とし）に言及しており、**今回また同じ轍を踏んだ**ことになる。

### (3) Deleteキー優先順位（部品優先→配線プリミティブ）の実装 —— **構造は案A通りだが、(2)の
排他崩れにより誤動作しうる**

`DeleteSelectedElement() || DeleteSelectedConnector()`（`MainWindow.xaml.cs:355`）という短絡評価
の実装自体は、侍の実装プラン（`docs/archive/ecad2-t041-implementation-plan-samurai.md`122-129行目「案A」）
の意図通り。ただし(2)で確認した通り、`SelectedConnector`が「もはや無関係になったはずの」インスタンス
を握ったまま残る経路が4つあるため、**この優先順位ロジックは「今ユーザーが見ている状態」と無関係な
古い選択を対象に削除を実行してしまう**リスクを負う。

加えて、`DeleteSelectedConnector()`（`MainWindowViewModel.cs:190-197`）は`sheet.Connectors.Remove
(connector)`の戻り値（実際に削除できたかの`bool`）を確認せず、無条件に`MarkDirty()`を呼び`true`を
返す。対比として`DeleteSelectedElement()`の`SelectedElement`は`CurrentSheet.Elements`からの
`FirstOrDefault`により導出されるため「今のシートに実在する」ことが保証されるが、
`SelectedConnector`にはその保証が無い（(2)の4経路で他シートの参照が残りうる）ため、この関数だけが
「実は何も削除していないのに成功したと報告する」リスクを抱える。(2)の4箇所を修正すれば実害は
消えるが、**保険として`Remove()`の戻り値を見て`false`ならMarkDirty()もtrueも返さない**という
防御的な実装に倣った方が、`DeleteSelectedElement()`との一貫性・堅牢性の両面で望ましいと考える。

### (4) Escape層3との連動 —— **問題なし（唯一正しく両方をクリアできている箇所）**

`MainWindow.xaml.cs:305-310`は`_viewModel.SelectedCell is not null || _viewModel.SelectedConnector
is not null`を条件に、`SelectedCell = null; SelectedConnector = null;`と両方を明示的にクリアして
おり、既存のEsc多層構造（T-021、「1回のEscは1層だけ」原則）とも整合する。皮肉なことに、この箇所が
「両方クリアする」パターンの模範であり、(2)で指摘した4箇所はこれに倣えば直せる。

### (5) 便乗変更なし —— **確認済み**

---

## `code-review`スキル併用の追加所見

### 所見A（Altitude、高確度）: 「選択プリミティブごとにN箇所へクリア処理を追加する」設計は増分3・5で破綻が拡大する

今回`SelectedConnector`という1つの選択状態を追加するために触れた箇所は最低10箇所（ViewModel側
フィールド・プロパティ・`CurrentSheetIndex`セッター・`DeleteSelectedConnector`新設、View側の
`PropertyChanged`フィルタ・`RedrawCanvas`引数・クリック時排他切替・Escape層3・Delete統合・
ハイライト専用Pen・`HitTestConnector`）。**この増分1単体で早くも4箇所の見落とし（観点2）が
発生した実績**を踏まえると、実装プラン（`archive/ecad2-t041-implementation-plan-samurai.md`）が予告する
増分3（`WireBreak`）・増分5（`FreeLine`/`ConnectionDot`）で同型の`SelectedWireBreak`/
`SelectedFreeLine`/`SelectedConnectionDot`を同じ方式で追加すれば、「`SelectedCell`を触る全箇所で
4つのプロパティ全てのクリアを漏らさず追従する」という手作業の不変条件維持が要求され、見落としの
リスクは今回以上に増大すると考える。**増分3着手前に、「選択中の配線プリミティブ」を単一の
`object?`ないし小さな判別共用体へ統合し、選択状態のクリアを1箇所に集約する設計を検討すべき**
（`docs/proposed.md` P-025「配置前検証サービス抽出」と同根の、App層の状態分散問題）。

### 所見B（Reuse、severity低）: `HitTestConnector`が`ToGridPos`と同一のDIP→mm変換を複製

`LadderCanvas.cs:118-121`の
```csharp
double xMm = localPositionDip.X / MmToDip;
double yMm = localPositionDip.Y / MmToDip;
var geo = _renderer.Geometry;
```
は`ToGridPos`（151-156行目）と一字一句同一。共通のprivateヘルパー（例:
`(double xMm, double yMm) ToMm(Point dip)`）へ切り出せば重複が解消する。実害は無いが、増分3・5で
同種のヒットテストが増えるたびにこの複製が繰り返される懸念があり、所見Aの統合設計と合わせて
解消するのが効率的と考える。

### 検討したが不採用の候補

- `GridGeometry.BoundaryAt`/`BoundaryAtHalf`を`HitTestConnector`が使っていない点（Reuse）：既存API
  は「クリック位置に最も近い境界」を返す設計で、`HitTestConnector`は「実在するコネクタへの距離」を
  見る設計のため、目的が微妙に異なる。統合は可能だが優先度は低いと判断（severity低）。
- `LadderCanvas.cs`のハイライト描画が`DiagramRenderer.DrawConnectors`の座標計算を手で複製している
  点（Simplification）：既存の`SelectedCellPen`ハイライトも同型のため、今回新規に持ち込んだ複雑さ
  ではなく既存トレードオフの踏襲。指摘のみに留める。
- `ConnectorHitToleranceMm`（マジックナンバー）：著者自身がコメントで「隠密レビュー対象」と自己
  申告済み。値自体は(1)で問題なしと判断したため独立指摘はしない。

---

## テストカバレッジについて（申し送り）

`tests/`配下を確認したが、`SelectedConnector`・`DeleteSelectedConnector`・`HitTestConnector`を
対象とする新規テストは**0件**（grep確認）。侍の実装プラン（5節）も増分1の忍者検証観点として
「`SelectedElement`（部品）とのDelete優先順位が誤動作しないこと」を明記しており、これはまさに
今回CONFIRMEDした(2)(3)の観点そのものである。**忍者実機検証の前に、少なくとも(2)で挙げた4経路
（矢印キー・選択ツールボタン・DRC同一シートジャンプ・新規/開く）のApp層回帰テストを追加することを
強く推奨する**（いずれもUI操作なしにViewModel単体で再現可能なはずで、実機確認より低コスト）。

---

## 侍への申し送り（修正方針、参考）

- (2)の4箇所は、いずれも「`SelectedCell`をクリア・変更する箇所には必ず`SelectedConnector`の
  クリアも併記する」という同一パターンの機械的な追加で解消できる（Escape層3の実装に倣う）。
- (3)の`DeleteSelectedConnector()`は`sheet.Connectors.Remove(connector)`の戻り値を見て、`false`
  ならMarkDirty()を呼ばず`false`を返すよう防御的にすることを推奨する（(2)の修正後は実害が無くなる
  防御線だが、`DeleteSelectedElement()`との一貫性のためにも直しておく方が良いと考える）。
- 所見Aの設計統合（選択プリミティブの単一プロパティ化）は増分1自体のやり直しを要求するものではない
  （現状の4箇所修正で増分1は十分に健全になる）が、**増分3着手前に家老・侍で一度検討する価値がある**
  と考える。

---

## 出典・参照

- 対象コミット`592131c`（`git show`で全差分確認）
- `src/Ecad2.App/MainWindow.xaml.cs`（222-229行目クリック排他、305-310行目Escape層3、349-358行目
  Delete統合、388-420行目`MoveSelectedCell`、462-467行目`ActivateSelectDefault`）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（100-117行目`CurrentSheetIndex`、147-197行目
  `SelectedCell`/`SelectedConnector`/`DeleteSelectedConnector`、468-505行目`ReplaceDocument`）
- `src/Ecad2.App/ViewModels/OutputPanelViewModel.cs`（88-99行目`JumpTo`）
- `src/Ecad2.App/ViewModels/ViewModelBase.cs`（15-21行目`SetProperty`の早期return）
- `src/Ecad2.App/Views/LadderCanvas.cs`（109-132行目`ToGridPos`/`HitTestConnector`）
- `src/Ecad2.Core/Rendering/GridGeometry.cs`
- `docs/archive/ecad2-t041-implementation-plan-samurai.md`（122-129行目、案Aの意図・忍者検証観点）
- `docs/proposed.md` P-025（App層の状態分散、選択プリミティブ統合の関連提案）
- `code-review`スキル（medium、line-by-line/cross-file統合角度＋Reuse/Simplification/Altitude角度、
  2エージェント並行、CONFIRMED5件・Altitude1件・Reuse1件）
