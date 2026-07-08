# T-041増分7 最終レビュー（隠密、往復4周目・所見AB/AC/AD修正の検証）

> 2026-07-08 隠密レビュー。対象コミット767325b（所見AB/AC/AD修正、親bfcb1f1→cbc74ac）。
> 家老指定観点(1)〜(4)＋`code-review`スキル（1エージェント）併用。実測検証（`dotnet test`）
> 併用。

---

## 結論：**クリーン。忍者へ増分7全体の実機確認を采配してよい**
（所見X本体＝マウスドラッグのスナップバック有無を最優先観点とすることに同意）

---

## 家老指定観点の検証

### (1) 所見ABのmin>maxガードで実際にクラッシュが再現しなくなっているか

**解消を確認**。`UpdateDragFreeLine`/`MoveSelectedFreeLine`の両方に
`minDeltaX > maxDeltaX ? 0 : Math.Clamp(...)`という三項演算子ガードがX/Y軸それぞれ独立に
追加されている。新規テスト（`MoveSelectedFreeLine_WhenLineWiderThanPageBoundary_DoesNotThrow`
/`DragFreeLine_Move_WhenLineWiderThanPageBoundary_DoesNotThrow`）は、実際にクラッシュを
再現した条件（線幅500mm・ページ境界450mm）をそのまま使い、`Record.Exception`で例外なしを
検証している。`dotnet test`実行で実際に例外が発生しないことを確認した。

VerticalConnectorの`UpdateDragConnector`は行方向1軸のみのため関数全体の早期returnで足りるが、
FreeLineはX/Y2軸を同時に持つため「片軸だけロックし他軸は正常に動く」設計になっている。これは
単純な移植ではなく、Connectorの発想をFreeLineの2軸構造に合わせて正しく拡張したものと判断する。

### (2) 所見AC/ADの境界クランプが4種間で一貫した実装になっているか

**一貫している**。所見AC（`ResizeSelectedFreeLineEndpoint`への`Math.Clamp(value, 0, maxMm)`
追加）・所見AD（ConnectionDotの`MoveSelectedConnectionDot`/`UpdateDragConnectionDot`/
`BeginDragConnectionDot`への拡大適用）とも、「0が下限のためmin>maxは起こらない」という判断は
妥当と確認した。`GridSpec`（`Ecad2.Core/Model/Sheet.cs:31-35`）はRows/Columnsに検証を持たない
プレーンPOCOだが、現在のUI操作（シート追加ダイアログは名前・種別のみで寸法は固定値
`Rows=10, Columns=20`）からはこれらが負になる経路が無いため、実害はない。

### (3) 今回の修正が新たな副作用を生んでいないか、他3種への波及確認

**新規の副作用は見つからなかった**。`ForceCancelDragConnectionDotIfAny`→`CancelDragConnectionDot()`
は単純な位置復元のみでMath.Clampを経由しないため、所見AD対応の影響を受けない。また、
`Math.Clamp`全呼び出し箇所（約20箇所）を洗い出したところ、`MoveSelectedConnectorColumn`・
`UpdateDragWireBreak`/`MoveSelectedWireBreak`にも同じ「Grid.Rows/Columnsが正」という前提が
残っているが、これは本コミットで新規に持ち込まれたものではなく、既存設計から踏襲されている
共有前提であり、現在のUIからは到達不能なため今回のスコープでの修正は不要と判断する（永続化層
の堅牢化を検討する将来の申し送り事項としては認識しておく価値がある）。

### (4) 157件のregression維持 —— 実測で確認

`dotnet test src/Ecad2.sln`実行、Core14件・App143件、計157件全合格。侍の報告と一致。

---

## 申し送り事項（対応不要、将来の参考）

`GcadSerializer`（永続化層）は`GridSpec.Rows`/`Columns`の下限検証を持たない。現在のUI操作
からはこれらが0以下になる経路は無いが、将来ファイル読込の堅牢化（手編集・破損ファイル対策）
を検討する際の対象になりうる。T-041のスコープではないため、対応は不要と判断する。

---

## 出典・参照

- 対象コミット767325b（`git show`で全差分確認）、親bfcb1f1（テスト補強のみ）→cbc74ac
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`UpdateDragFreeLine`/`MoveSelectedFreeLine`
  の三項演算子ガード、`ResizeSelectedFreeLineEndpoint`/ConnectionDot系のクランプ）
- `src/Ecad2.Core/Model/Sheet.cs:31-35`（`GridSpec`定義）
- `tests/Ecad2.App.Tests/FreeLineDragAndResizeTests.cs`・`ConnectionDotDragTests.cs`
  （新規クラッシュ再現テスト）
- `docs/ecad2-t041-increment7-review-onmitsu-3.md`（前回レビュー、所見AB/AC/ADの原本）
- `code-review`スキル（1エージェント、新規所見0件）
