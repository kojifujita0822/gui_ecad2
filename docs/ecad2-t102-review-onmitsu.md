# T-102 静的レビュー（隠密）

- 対象: OR自動配線の合流先選択ロジック見直し（`BuildOrJoinCandidates`新設、案A・確定後自動遷移方式）
- 変更ファイル: `MainWindowViewModel.cs`（203行）・`MainWindow.xaml.cs`（48行）・`ToolState.cs`（5行）・`LadderCanvas.cs`（12行）・`OrWiringTests.cs`（75行）
- 侍報告どおり未コミット、`git diff --stat`と行数一致を確認済み

## 観点1: 回帰テストのRED先行証明

侍報告に記載なし。以下を確認した。

- 新設2テスト（`PlaceOr_T044Scenario_SelectingOuterCandidate_JoinsAtOuterNetInsteadOfInnerBlock`・
  `PlaceOr_T044Scenario_DefaultCandidate_StillJoinsAtInnerBlock`）はいずれも
  `MoveOrJoinTargetCandidate`/`ConfirmOrJoinTarget`という**今回新設のAPI**に依存しており、旧コード
  （`BuildOrJoinCandidates`導入前のbaseRow版）に対してそのまま実行することはコンパイルレベルで
  不可能（機械的なRED先行証明が構造的に困難な性質の変更）。
- ただし根本原因であるT-044バグ自体（「Cは常にB＝線番1側にしか合流できない」）の再現は、新設API
  無しでも**旧コード時点で示せたはず**（例：outerテストの前半部分だけ旧コードに当て、Cのネットが
  無名接点でなくBと一致してしまうことを確認する、という手順が可能だった）。
- 【確認事項】これを実施したか、侍に確認されたい。実装の正しさ自体への疑義ではなく、手続き上の
  確認である（新設2テストの狙い通りの結果=Green、既存4テストへの`ConfirmOrJoinTarget()`追記も
  自然な適合として妥当と判断している）。

## 観点2: 横展開4箇所の整合性（P-080懸念パターンの再発チェック）

家老指摘の4箇所（`AppMode` setter・`SelectedCell`のsetter・`CancelResidualDraftForToolSwitch`・
`ReplaceDocument`）全てで`ClearOrJoinTargetDraftIfAny()`ないし`CancelOrJoinTarget()`の追加を
コード上で確認した（`MainWindowViewModel.cs:112, 460, 1979, 3129`付近）。

加えて、既存4種ドラフト（`_connectorDraft`/`_freeLineDraft`/`_imageInsertDraft`/`_frameDraft`）の
クリア呼び出し箇所を`ClearConnectorDraftIfAny()`等でgrepし全数突合したところ、上記4箇所以外に
呼び出し漏れの候補は無かった。`HasAnyDraft`（`:1961`）にも`_orJoinTargetDraft`が追加済み。
`MainWindow.xaml.cs`側のEscキーハンドラのelse-ifチェーンにも`ToolMode.ConfirmOrJoinTarget`の
分岐（層2'''''、`:2435-2444`）が他の層2系と対称的に追加されており、既に確定済みの要素まで
取り消す必要がある特殊性（`RedrawCanvas()`明示呼び出し）にも言及がある。P-080懸念パターン
（N+1個目のドラフト追加時の横展開漏れ）の再発は無いと判断する。

## 観点3: 候補生成アルゴリズムの一般化（既存の単純ケースへの非破壊性）

旧`baseRow`探索（`.Max()`で最大行1件のみ）と新`BuildOrJoinCandidates`（`Distinct().OrderByDescending`で
全distinct行降順列挙）を突合した。

- 候補[0]の行 = 旧`baseRow`（両者とも「配置行未満で最大の既存要素行」）で一致
- 候補[0]の`baseElement`選択（列位置最近傍）・`leftColumn`/`rightColumn`算出・`NothingBetweenRailAndColumn`
  判定はロジック無変更（ローカル関数→`private static`への抽出のみ）
- 旧コードの`if (baseElement is null) return;`が新コードでは`continue`に変わっている点を確認したが、
  `candidateRows`の各行は必ず`sheet.Elements`にその行の要素を持つ行から生成されるため
  （`baseElement is null`は理論上到達不能）、意味的な差異は生じない
- `ConfirmOrJoinTarget()`内の`MarkDirty()`は新規呼び出しだが、要素追加時点（`PlaceElementAtSelectedCell:2866`）
  で既にMarkDirty済みのため実害なし、むしろコネクタ追加という別変更に対する適切なマーキングの補完

候補1件のみのケース（既存4テストで検証済みの単純パターン）では候補[0]で即Enter確定すれば旧挙動と
完全一致することをコードレベルで確認した。新設2テストもT-044実例の座標を用いた妥当な検証になっている。

## 総合判定

観点1の確認事項（RED先行証明の実施有無）を除き、重大な欠陥は見当たらない。忍者実機確認へ回して
差し支えないと判断する。
