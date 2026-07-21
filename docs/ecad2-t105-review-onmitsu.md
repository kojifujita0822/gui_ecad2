# T-105 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `3ca9f5f`（GroupFrameの矢印キー移動対応、案A=無修飾矢印キー）
手法: `git show 3ca9f5f -- <path>` で範囲を明示した手動レビュー（code-reviewスキルはSkillツール経由起動不可のため代替）。effort=low（1周目既定）。

## 結論

**指摘なし。DoD全項目を満たすと判断する。** 忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認（`docs/ecad2-t105-investigation-samurai.md`着手前調査の実装方針との突き合わせ）

- `MoveSelectedFrame(int deltaRow, int deltaColumn)`新設：`MainWindowViewModel.cs`に実装確認。`MoveSelectedElement`型（RecordSnapshot→境界判定→TopLeft更新→MarkDirty）どおり。
- 境界判定：既存`IsFrameWithinGridBounds(newTopLeft, frame.Width, frame.Height, sheet)`をそのまま再利用（2678-2680行、変更なし）。全否定方式（満たさなければ移動しない）で調査書の方針と一致。
- Undo対応：`UndoManager.RecordSnapshot(Document)`を実際に移動する場合のみ呼ぶ（変化なし・境界外では呼ばない）。
- 呼び出し元配線位置：`MainWindow.xaml.cs`の無修飾矢印キーif-elseチェーンで`SelectedImage`判定直後・`else MoveSelectedCell(e.Key)`（Cellフォールバック）直前に`else if (_viewModel.SelectedFrame is not null) MoveSelectedFrameByKey(e.Key);`を確認。調査書の指定位置と一致。
- キー割当：`MoveSelectedFrameByKey`はUp/Down/Left/Rightをそれぞれ(-1,0)/(1,0)/(0,-1)/(0,1)へマップ、無修飾矢印キー（案A、殿裁定）で実装。修飾キー判定は呼び出し元の既存if-elseチェーン構造に依存し、他の独立選択状態群と同型。

DoDの構成要素いずれも欠落なし。

### (b) SetProperty早期returnの再発トラップ狙い撃ち

`MoveSelectedFrame`内に`if (newTopLeft == frame.TopLeft) return false;`という早期return分岐があるが、これは`docs-notes/roles/onmitsu.md`記載の罠パターン（値が数値上偶然一致する経路でクリア処理が丸ごとスキップされる）には該当しないと判断する。理由：

1. この早期returnでスキップされるのは`RecordSnapshot`/`TopLeft`更新/`MarkDirty`のみであり、いずれも「実際に移動が発生した場合にのみ実行すべき」処理。罠パターンの本質（本来常に実行すべき副作用が値一致でスキップされる）とは逆で、ここでは「値が変化しないなら実行しない」が正しい仕様。
2. 呼び出し元（`MoveSelectedFrameByKey`）が渡す`deltaRow`/`deltaColumn`は常にどちらか一方が`±1`（Up/Down/Left/Right以外の分岐は`_ => false`でメソッド自体を呼ばない）であり、`newTopLeft == frame.TopLeft`が成立する経路（delta=0,0）はキー入力からは到達しない。
3. `SelectedCell`setterのような「他状態のクリア」を副作用に持つプロパティではなく、`TopLeft`という単一フィールドの更新のみが対象のため、スキップによる連鎖的な状態不整合は生じない。

罠の型には該当せず、指摘なし。

### (c) Undo/RedoでのSelectedFrame幽霊参照懸念の裏取り

侍所見「`ApplyUndoRedoSnapshot`内の`SelectedCell`再代入経由で正しくnullクリアされる」をコードで裏取りした。

- `MainWindowViewModel.cs` 3216-3259行`ApplyUndoRedoSnapshot`：3230行で`Document = restored`によりDocument差し替え（この時点でSelectedFrameは旧Documentの実体を指す幽霊参照になりうる）、3238行で`SelectedCell = ClampSelectedCellToSheetRows(oldSelectedCell, CurrentSheet);`を実行。
- `SelectedCell`setter（408-430行）：430行に`SelectedFrame = null;`があり、これは`SetProperty`の早期return判定より前（無条件クリアブロック内、T-067実装時に既に追加済み）に配置されている。よって`SelectedCell`への代入が発生する限り、値の変化有無に関わらず必ず`SelectedFrame = null`が実行される。

侍所見は正しいとコード上で確認できた。加えて、新規テスト`MoveSelectedFrame_Undo後はSelectedFrameが幽霊参照にならずnullクリアされる()`で実測確認もされている（`Assert.Null(vm.SelectedFrame)` / `Assert.False(vm.HasSelectedFrame)`）。指摘なし。

### (d) code-reviewスキル併用

marketplace版導入後も`disable-model-invocation`エラーで起動不可（本セッション冒頭で再確認済み、家老へ報告済み）。`onmitsu.md`既定どおり手動レビューで代替。

## テストカバレッジ確認

`tests/Ecad2.App.Tests/T067GroupFrameTests.cs`に追加された11件を確認：境界値4方向（上/下/左/右）、連続移動、重複許容、変化なし時Undo履歴非作成、未選択時false、Undo/Redo往復、幽霊参照nullクリア。DoDの主要シナリオを網羅している。

## 不明点

なし。

## 派生提案（範囲外の気づき）

侍の調査書（着手前調査4節末尾）に「矢印キー分岐チェーンには`ToolMode.PlaceFrame`のcaseが存在せず、`AdjustFrameDraft`はマウスドラッグ経由のみで矢印キーステップ非対応」という参考記載があった。T-105の主題（既存枠の移動）とは別範囲であり、対応不要なら見送りでよいと侍自身が付記済み。隠密としても同意見（範囲外、`docs/proposed.md`行き）。
