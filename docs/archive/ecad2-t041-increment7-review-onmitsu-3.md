# T-041増分7 最終レビュー（隠密、往復2周目・所見X/Y/Z/AA修正の検証）

> 2026-07-08 隠密レビュー。対象コミットcbc74ac（所見X/Y/Z/AA修正、親4154fb8）。家老指定観点
> (1)〜(4)＋`code-review`スキル（所見X/Y検証・removed-behavior/cross-file、2エージェント
> 並行）併用。実測検証（`dotnet test`、WPF最小再現プログラムでの所見X修正後の順序検証）も
> 併用した。忍者の静的レビュー（`docs/ecad2-t041-increment7-test-review-ninja.md`）で提起
> された「ConnectionDotへの境界クランプ拡大」についても本レビューで判断を示す。

---

## 結論：**所見X・Y・Zは正しく解消。ただし所見AA対応自体が新たな重大バグ（実測でクラッシュ
再現）を作り込んでいる。要修正**

所見X（マウスドラッグ確定処理の順序問題）・所見Y（強制キャンセル時の位置復元）・所見Z
（偽陽性MarkDirty）はいずれも意図通り正しく解消されたことを実証した。しかし所見AA
（境界クランプ）対応自体が、VerticalConnectorで既に確立していた「min>maxガード」パターンを
FreeLineへ移植し忘れたことにより、**通常の操作（F9記入+矢印キー連打）だけで到達可能な
`ArgumentException`クラッシュ**を新たに作り込んでいる。2つの独立エージェントが同じ問題を
発見し、うち1件は実際にビルド済みDLLをロードして例外再現まで実測している。

---

## 家老指定観点の検証

### (1) 所見Xの順序入替えが4箇所すべて正しく解消されているか

**解消を確認（実測+コードトレース両方で検証）**。`MainWindow.xaml.cs`439〜475行目、
Connector/WireBreak/FreeLine/ConnectionDotの4分岐すべてで`ConfirmDrag*()` →
`ReleaseMouseCapture()`の順に統一されていることを確認した。`ConfirmDrag*()`/`CancelDrag*()`
自体は`OnPropertyChanged`を発火しない設計（`ForceCancelDrag*IfAny()`だけが発火）のため、
`ReleaseMouseCapture()`が同期発火する`LostMouseCapture`ハンドラは、その時点で既に
`IsDragging*=false`になっているガードで正しく素通りする。

自分でも独立したWPF最小再現プログラム（スクラッチパッド、`src`/`tests`は未変更）で
「Confirm→Release」の順序であれば`LostMouseCapture`発火時点で既に状態がfalseになっており
誤ったキャンセルが割り込まないことを実測確認した：

```
=== Step 2: Fixed order - ConfirmDrag() THEN ReleaseMouseCapture() ===
  [ConfirmDrag] called - setting _isDragging=false
  [Event] LostMouseCapture fired. _isDragging=False
  [Guard] _isDragging is already false - CancelDrag is correctly skipped.
```

### (2) 所見Yの位置復元が意図通りか、新たな副作用なきか

**意図通り、新たな副作用なし**。`ForceCancelDrag{Connector,WireBreak,FreeLine,
ConnectionDot}IfAny()`はいずれも`_dragging*=null`直書きから`CancelDrag*()`呼び出しへ統一され、
4種とも一貫している。Delete経由（対象が既にリストから削除済み）で`CancelDrag*()`が削除済み
オブジェクトへ位置復元の書き込みを行うことになるが、`TopRow`/`BottomRow`等は副作用のない
単純プロパティのため実害なし（2エージェント独立確認）。`ViewModel_PropertyChanged`の
`IsDragging*`購読ガードとの整合性も問題ない。

### (3) 所見Z/AAの修正妥当性

**所見Zは妥当**。`ResizeSelectedFreeLineEndpoint`に追加された「変化なし」チェック
（`if (newX1 == line.X1Mm) return false;`等）により、線の向きと逆軸のキーで偽陽性
`MarkDirty()`が発生しなくなった。

**所見AAは不完全、かつ新たな重大バグを作り込んでいる（下記所見AB、最重要）**。境界クランプ
自体（`UpdateDragFreeLine`/`MoveSelectedFreeLine`）は追加されたが：

- **所見AB（CONFIRMED・重大、実測再現）**：クランプ計算に`min>maxガード`が無く、
  ページ幅を超える長さの自由線を選択してドラッグ/矢印移動すると`Math.Clamp`が
  `ArgumentException`を投げる。VerticalConnectorの`UpdateDragConnector`には所見B対応として
  既に`if (minDelta > maxDelta) return;`というガードが存在するのに、同一コミットで追加した
  FreeLine側には移植されていない。**しかも、この「ページ幅を超える自由線」は外部データ由来
  の特殊ケースではなく、既存の`MoveFreeLineDraftEnd`（本コミット対象外の既存コード、記入中の
  伸縮）が`AnchorXMm`を考慮せず`±Grid.Columns`分のステップを無条件で許すため、通常のF9記入
  +矢印キー連打だけで容易に生成できる**ことを、独立した2エージェントがそれぞれ実測で確認した
  （うち1件は実際にビルド済みDLLをロードし`BeginDragFreeLine`→`UpdateDragFreeLine`を実行して
  `System.ArgumentException: '-0' cannot be greater than -450.'`という実例外を再現）。
  `DispatcherUnhandledException`が拾うためアプリ全体のクラッシュには至らないが、生の例外
  テキストがユーザーにダイアログ表示される。

- **所見AC（severity中、対称性の欠如）**：`ResizeSelectedFreeLineEndpoint`（Tab+Shift矢印に
  よる端点個別リサイズ）には所見AAの境界クランプが一切追加されていない。ドラッグ版の端点
  リサイズ（`UpdateDragFreeLine`687/692/700/705行目付近）は`Math.Clamp(…, 0, maxMm)`で
  クランプ済みなのに、キーボード版だけ対称性が崩れている。選択中のFreeLineの端点をShift+
  矢印キーで繰り返し伸ばすと、ページ境界を超えて際限なく伸びる。

- **所見AD（適用漏れ、忍者と3者独立発見）**：`MoveSelectedConnectionDot`/
  `UpdateDragConnectionDot`には所見AA相当の境界クランプが一切適用されていない。コミット
  メッセージは「FreeLineの…クランプを追加」と明記しておりスコープ外に見えるが、根拠
  （Undo機能無し・mm実座標系）はFreeLineと全く同じであり、モデル定義（`ConnectionDot`は
  単純な`XMm`/`YMm`のみ）を確認した限り、適用漏れと判断するのが妥当。**忍者の静的レビュー
  でも同一の指摘が独立に上がっており（3者独立発見）、拡大適用を推奨する**。

### (4) 132件のregression維持 —— 実測で確認

`dotnet test src/Ecad2.sln`実行、Core14件・App118件、計132件全合格。侍の報告と一致。ただし
所見AA/Yの回帰テストは「実際に位置が動いた後」のケースを検証しておらず、上記所見AB（クラッシュ）
・所見AC（対称性欠如）を検出できない構成のまま残っている。

---

## severity整理

| 所見 | 対象 | severity | 対応要否 |
|---|---|---|---|
| X修正 | Connector/WireBreak/FreeLine/ConnectionDot | ― | 解消確認済み |
| Y修正 | 同上 | ― | 解消確認済み |
| Z修正 | FreeLine | ― | 解消確認済み |
| AB（新規） | FreeLine（本体移動、ドラッグ+キーボード） | **重大（実測クラッシュ再現）** | 至急対応要 |
| AC（新規） | FreeLine（キーボード端点リサイズ） | 中 | 対応推奨 |
| AD（適用漏れ、忍者と一致） | ConnectionDot | 中 | 対応推奨（家老・侍判断） |

---

## 忍者提起「ConnectionDotへの境界クランプ拡大」への回答

**拡大を推奨する**。ConnectionDotはFreeLineと同じmm実座標系・Undo機能無しという条件を共有
しており、モデル定義（`Ecad2.Core/Model/Element.cs:92-98`、単純な`XMm`/`YMm`のみ）を確認した
限り、境界外に飛んで見失うリスクはFreeLineと同型である。所見ABのmin>maxガード対応と合わせて
一度に手当てするのが、往復回数を増やさない観点で効率的と考える。

---

## 出典・参照

- 対象コミットcbc74ac（`git show`で全差分確認）、親4154fb8
- `src/Ecad2.App/MainWindow.xaml.cs`（MouseUp分岐439〜475行目、`LadderCanvasHost_
  LostMouseCapture`511〜545行目付近、`MoveSelectedFreeLineByKey`963〜980行目）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`UpdateDragFreeLine`661〜680行目付近、
  `MoveSelectedFreeLine`737〜750行目付近、`ResizeSelectedFreeLineEndpoint`760行目付近、
  `MoveSelectedConnectionDot`/`UpdateDragConnectionDot`863〜901行目）
- `src/Ecad2.Core/Model/Element.cs:92-98`（`ConnectionDot`モデル定義）
- `src/Ecad2.Core/Persistence/GcadSerializer.cs`（スキーマ検証無し）
- `tests/Ecad2.App.Tests/FreeLineDragAndResizeTests.cs`
- WPF最小再現プログラム（スクラッチパッド、所見X修正後の順序を実測検証、`src`/`tests`は
  未変更）
- `docs/ecad2-t041-increment7-review-onmitsu-2.md`（前回レビュー、所見X/Y/Z/AAの原本）
- `docs/ecad2-t041-increment7-test-review-ninja.md`（忍者の静的レビュー、ConnectionDot境界
  クランプの指摘）
- `code-review`スキル（所見X/Y検証・removed-behavior/cross-file、2エージェント並行、
  CONFIRMED2件（所見AB、独立2エージェント発見・うち1件実測クラッシュ再現）・所見複数）
