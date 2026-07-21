# T-107 機器コメント表示・入力機能の新設 — 静的レビュー（隠密）

**対象コミット**: `bd87250`（Core+App、217insertions/19deletions）
**レビュー日**: 2026-07-21
**effort**: low相当（1周目、ただし観点(3)で重大疑義を検出したため踏み込んで確認）
**手法**: `code-review`スキルは恒久的にSkillツールから起動不可（`onmitsu.md`既定事象）につき手動レビュー。

## 結論

**観点(1)(2)は問題なし。観点(3)で重大な指摘あり（CONFIRMED相当、修正必須と考える）。**

## 【重大】SelectedElementCommentのOnPropertyChanged通知漏れ

`MainWindowViewModel.cs`には、選択中要素（`SelectedElement`）が切り替わったタイミングで、プロパティパネル表示用の派生プロパティ群（`SelectedElementDeviceName`/`IsSelectedElementSelectSwitch`/`SelectedElementNotchPosition`/`IsSelectedElementLamp`/`SelectedElementLampColor`/`IsSelectedElementTimerRelated`/`SelectedElementSetpoint`/`SelectedElementSetpointSliderValue`/`SelectedElementLabelDy`）へ`OnPropertyChanged`を発火する箇所が以下4箇所ある：

1. `SelectedCell`のsetter内（460-469行目）
2. `DeleteSelectedElement()`内（2352-2360行目）
3. `NotifySelectedElementChanged()`（2480-2494行目、共通メソッド）
4. `ReplaceDocument`系のDocument差し替え処理内（3004-3012行目）

**今回新設された`SelectedElementComment`（2108行目）は、この4箇所いずれのリストにも追加されていない。** 8〜9個の既存プロパティ名が律儀に列挙されている一方、新設1件だけが抜けている状態で、単純な追加漏れと考えられる。

### 実害シナリオ

1. 要素Aを選択し、CommentBoxに要素Aのコメントが表示される
2. 要素Bへ選択を切り替える（`SelectedCell`のsetter経由）→ `SelectedElementComment`のPropertyChangedが発火しないため、WPFバインディングは再評価されずCommentBoxの表示は要素Aの値のまま残留する
3. この状態でEnterキーまたはフォーカス外し（`CommentBox_PreviewKeyDown`/`CommentBox_LostKeyboardFocus`）が発生すると`CommitDeviceNameEdit()`が呼ばれ、`CommentBox.GetBindingExpression(...)?.UpdateSource()`が実行される
4. 画面に残っていた**要素Aの古いコメント値**が、**要素Bのコメント**として誤ってコミットされる

これは`MainWindowViewModel.cs`2833-2838行目のコメントに記録されている**T-079(P-058)の`SelectedElementDeviceName`同型バグ**（「配置直後にCtrl+S等でCommitDeviceNameEditが走ると、古い表示値が誤って新要素のデバイス名としてコミットされ、機器表エントリが消失する」、侍実測で確定済み）と完全に同じ構造。DeviceNameは既にこの教訓を踏まえ4箇所すべてに通知が組み込まれているのに、Commentだけが新設時に横展開されなかった。

### テストで捕捉されなかった理由

新規追加テスト`T107CommentTests.cs`（6件）は、いずれも単一要素へのCRUD/Undo/トリム/未選択時無視のみを検証しており、「要素切替後のgetter再評価（PropertyChanged発火）」というシナリオは対象外。この種の不具合はWPFバインディングのタイミングに依存するため、ViewModelを直接操作する単体テストでは構造的に検出しづらい（T-079発覚時も侍の実機操作で確定したとの記録あり）。

### パターン台帳との照合

`docs-notes/pattern-recurrence-log.md` PR-01（新規選択可能状態の横展開漏れ）と根本原因の型は同じだが、対象が「新しい`Selected*`状態」ではなく「既存`SelectedElement`に付随する表示用派生プロパティの通知網羅」であるため、`samurai.md`の既存5項目チェックリスト（選択排他setter/Escキー/矢印キードラフト/削除OR連鎖/右クリック）はこの型を直接カバーしていない。PR-01の亜種、あるいは新規パターン候補として台帳への記帳を検討されたい（家老判断に委ねる）。

## 観点(1): DoD(4)機器表コメント列表示への回帰

**問題なし。** 機器表（クロスリファレンス表）の描画は`DrawCrossRefTable`内`DrawCellText(r, string.Join(" / ", e.Comments), ...)`（810行目、`TextRole.CrossRef`スタイル使用）で、`e.Comments`（複数形、`CrossReferenceEntry`が集約する別プロパティ）を参照する完全に独立したコードパス。今回変更した`DrawElementLabel`とは描画メソッド・使用するTextRole・参照するプロパティ（単数形`Element.Comment` vs 複数形`Comments`）のいずれも別物であり、diffにも`DrawCrossRefTable`側の変更は含まれていない。侍所見「無変更」は正確。

## 観点(2): DoD(5)PDF出力への反映

**問題なし。** `Ecad2.Pdf/PdfExporter.cs`（24行目）が`DiagramRenderer`をインスタンス化し、`PdfRenderer`（`IRenderer`実装、`Ecad2.Pdf/PdfRenderSurface.cs`）経由で`dr.Render(...)`を呼ぶ構造を確認。画面描画（WPF側`IRenderer`実装）とPDF出力は`DiagramRenderer.DrawElementLabel`を完全に共有しており、色（`DrawingTheme.Comment`、テーマ非依存の固定値）もCore層の定数のため画面/PDF問わず同一。侍所見「別実装不要で自動反映」は構造的に正しい。

## 派生提案

なし（横展開通知漏れの型自体をパターン台帳へ記帳するか否かは家老判断）。
