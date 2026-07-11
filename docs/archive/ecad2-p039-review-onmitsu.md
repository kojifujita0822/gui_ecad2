# P-039 最終レビュー（隠密、新制度初適用の突合検証）

> 2026-07-08 隠密レビュー。対象コミット86bf96e（`feat(app): T-041増分7 VerticalConnector
> 列ドラッグ対応(P-039、新制度初適用)`、親767325b）。家老指定観点(1)〜(5)＋`code-review`
> スキル（1エージェント、設計書突合＋RED証明の再実測含む）併用。実測検証（`dotnet test`）
> 併用。

---

## 結論：**クリーン。忍者へ実機確認（列ドラッグ+ドラッグ回帰）を采配してよい**

新制度「テスト設計と実装の分離」の初適用として、設計書（`docs/ecad2-p039-test-design-
onmitsu.md`）とテストコードの突合を最重要観点として検証した。**設計書の全項目（4.1〜4.6）が
過不足なくコード化されている**ことを確認した。実装ロジックも案Xの要件を満たす。軽微な指摘
4件のみ、いずれも対応不要〜任意。

---

## 家老指定観点の検証

### (1) 実装が仕様（案X＝ドラッグとキーボードで同じ結果に到達）を正しく満たすか

**満たすことを確認**。`MoveSelectedConnectorColumn`（既存キーボード版）の
`Math.Clamp(c.Column + delta, 0, sheet.Grid.Columns)`と、今回追加した`UpdateDragConnector`の
`Math.Clamp(_dragConnectorOrigColumn + deltaColumn, 0, sheet.Grid.Columns)`は、
`_dragConnectorOrigColumn`（Begin時のスナップショット）＋`deltaColumn`（`currentColumn -
startColumn`）が、キーボード版の`delta`と数学的に同じ役割を果たすため、同じ入力に対して
同じ結果に収束する。新規テスト`DragAndKeyboardColumnMove_ConvergeToSameResult`（Theory5件）
でも実測確認済み。

### (2) 設計書どおりにテストが実装されたか（新制度の突合観点、最重要）

**完全に一致、省かれたケースなし**。`code-review`エージェントが設計書セクション4.1〜4.6の
各項目を実装コードと1つずつ突合した結果：

- 4.1（境界値B1〜B8）: `UpdateDragConnector_Move_UpdatesColumnWithClamp`のInlineData9件を
  実際に再計算し、全ての期待値が正しいことを確認。網羅漏れなし。
- 4.2〜4.6: 全て1対1で実装されている。設計書にあってテストコードに無いもの、逆にテスト
  コードにあって設計書に無いもの、いずれも検出されず。
- 4.5（「シート切替経由・Delete経由の両方」という設計書の明示的な指定）も、
  `_ViaDelete`/`_ViaSheetSwitch`の2テストで満たされている。

これは新制度が意図した「実装者バイアスの構造的な希釈」が機能した実例と言える。

### (3) RED証明の対象テストが本当に当該経路を突いているか

**おおむね正しい。ただし報告精度に軽微な指摘あり**。`code-review`エージェントが実際に
`UpdateDragConnector`のColumn更新2行・`ConfirmDragConnector`のColumn変化チェック・
`CancelDragConnector`のColumn復元を一時的にコメントアウトし、`dotnet test --filter
"FullyQualifiedName~ConnectorDragAndResizeTests"`を実行して再現した（作業後は`git checkout`
で完全復元、`git status`でsrc/配下に差分が残っていないことも確認済み）。

結果：44件中15件がRED（4.1系9件中7件・Independent1件・4.3系2件中1件・4.4系1件・4.5系2件・
**4.6系5件中3件**）。コミットメッセージは「4.1系Theory・Independent・4.3/4.4/4.5系がRED」と
記載しているが、**実際には4.6系（案Xの核心要件を検証するテスト）も3件REDになっており、
この事実がコミットメッセージから漏れている**（severity低、報告精度の問題であり実装自体に
欠陥はない）。

また、B2/B4（境界値ちょうどにロックされるケース）および4.6の一部（`(0.0,-2.0)`/`(20.0,5.0)`）
は、Column更新ロジックを丸ごと除去しても「クランプされて境界に留まる」結果と「そもそも
更新されない」結果が偶然一致するため、単独では実装欠落を検出できない性質を持つことが
判明した（Theory全体としては隣接する行がREDになるため正しく機能している、致命的ではない）。

### (4) 179件のregression維持 —— 実測で確認

`dotnet test src/Ecad2.sln`実行、Core14件・App165件、計179件全合格。`code-review`エージェント
も復元後に再実行し独立に確認済み。

### (5) HitTestConnectorDragModeの列ズレ制限を「掴む精度」として据え置いた侍判断の妥当性

**妥当と判断**。`HitTestConnectorDragMode`の許容誤差チェックは、ドラッグ**開始**時にクリック
位置がコネクタの線の近くにあるかどうかの判定（掴む精度）のみに使われ、ドラッグ**開始後**の
可動域は`UpdateDragConnector`内の`Math.Clamp(0, sheet.Grid.Columns)`が独立に決定しており、
両者にコード上の依存関係は無い。「掴み判定精度」と「操作可能範囲」を分離する設計として
一般的にも妥当と考える。

---

## 軽微な指摘（対応不要〜任意）

- **`RowAtDip`（`LadderCanvas.cs:237`付近）が死んだコードとして残置**：`MainWindow.xaml.cs`
  側の2箇所の呼び出し元（BeginDrag/UpdateDrag）が両方`ToRowBoundary`へ切り替わったため、
  `RowAtDip`自体は使われなくなったが削除されていない。ビルド警告は出ない（internal未使用
  メソッドは警告対象外）が、ボーイスカウト・ルールの観点で削除が望ましい。
- **RED証明の報告精度**：上記(3)節のとおり、4.6系もREDになった事実がコミットメッセージに
  記載されていない。実害はないが、新制度の透明性という観点では今後の報告に含めるのが
  望ましい。
- **一部境界値テストの検出力の限界**：B2/B4・4.6の一部行は「偶然パスする」性質を持つ。
  Theory全体としては機能しているため対応不要と判断する。

---

## 出典・参照

- 対象コミット86bf96e（`git show`で全差分確認）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`UpdateDragConnector`/`ConfirmDragConnector`
  /`CancelDragConnector`のColumn対応差分）
- `src/Ecad2.App/Views/LadderCanvas.cs`（`HitTestConnectorDragMode`コメント、`RowAtDip`残置）
- `src/Ecad2.App/MainWindow.xaml.cs`（`ToRowBoundary`への切替）
- `tests/Ecad2.App.Tests/ConnectorDragAndResizeTests.cs`（新規22件）
- `docs/archive/ecad2-p039-test-design-onmitsu.md`（隠密起草のテスト設計書、本レビューの突合対象）
- `code-review`スキル（1エージェント、設計書突合＋RED証明の再実測、指摘4件・いずれも軽微）
