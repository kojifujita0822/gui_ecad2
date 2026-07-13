# T-069 往復4周目修正 再レビュー(隠密・フル観点)

- 対象コミット: `fdd101c`(侍、隠密テスト設計書`docs/ecad2-t069-fix4-test-design-onmitsu.md`に基づく修正2件)
- 実施日: 2026-07-13
- 実施者: 隠密
- 方式: テスト設計書との突合(状態遷移表・DoD)+`code-review`スキル(フル観点、4並列エージェント、テストコード静的レビュー含む)
- スコープ境界: レビューのみ、書き込みなし。P-072・経過観察2件(HasAnyDraftフェイルセーフ喪失・CommitDeviceNameEdit3箇所複製)は対象外のまま据え置き。

## 結論サマリ

修正1(ツールバードラフトクリア)・修正2(右クリック作業起点保護)いずれも設計書の期待する振る舞いを正しく実装している。ただし**修正2の副作用として新しい見た目のズレ**(severity中)と、**往復4周目のスコープ外だが同型の未発見バグ**(`ReplaceDocument`の画像ドラフトクリア漏れ、CONFIRMED)を発見した。テスト設計書との突合では、検証観点2(3件)が全て未実装(理由明記あり、手続き上は妥当だが1件は実装可能だった疑いあり)。

## 元指摘2件・テスト設計書との突合

### 修正1: ツールバーボタンのドラフトクリア漏れ

**実装**: `CancelResidualDraftForToolSwitch()`を新設し、`CancelConnectorDraft`/`CancelFreeLineDraft`/`CancelImageInsertDraft`を呼ぶ。`ActivateBuiltinTool`/`ActivateOpenPartSelection`双方の冒頭から呼び出し。3メソッドとも「記入中でなければ何もしない」ガードを先頭に持ち、副作用(Tool.Mode等の意図しない書き換え)は無いことを確認(2系統のエージェントが独立確認)。

**テスト突合**: 設計書が要求した状態遷移表の全行(PlaceConnector/PlaceLine/PlaceImage×ActivateBuiltinTool、対照ケース、ActivateOpenPartSelection側1件)が反映されている。

**新規指摘(要確認)**: 追加テストは`ActivateBuiltinTool`/`ActivateOpenPartSelection`(View層メソッド)自体を呼ばず、その本体ロジック(`CancelResidualDraftForToolSwitch()`+`Tool`代入の2行)をテスト内で模擬しているだけ。**これはT-069往復3周目レビューで指摘した既知の穴(「修正1のTheoryテストはView側の正規化コードを実行せず、テスト内で同じロジックを模擬再実装しているだけ」)と同型のパターン**。将来`ActivateBuiltinTool`内の呼び出し順序が変わる・削除される等の退行があってもこのテストでは検出できない。パターン再発の疑いあり(下記「パターン再発検知」参照)。

### 修正2: 右クリック作業起点保護

**実装**: `_viewModel.SelectedCell = hitElement.Pos;`をメニュー表示時点から削除し、`BuildElementContextMenuItems`内の3メニュー項目(削除・機器名変更・コメント編集)全てのClickハンドラ冒頭へ`_viewModel.SelectedCell = pos;`を追加(付け忘れなし、全項目確認済み)。設計書の期待(キャンセル時は現状維持・実行時のみ正規化)を満たす実装。

**新規指摘1(CONFIRMED、severity中)**: メニュー表示中、プロパティパネル(`SelectedElementDeviceName`等)・キャンバスの選択ハイライトは正規化前の旧選択(連続配置中ならP0)を表示し続け、メニューが指す要素とは無関係な内容になる。連続配置中に既存要素Xを右クリックすると、メニュー項目名は「削除」等Xに対する操作に見えるが、同時にプロパティパネルは「要素を選択してください」表示のまま(P0はセルのみでElementなし)という不整合な見た目が生じる。修正2が意図的に生んだトレードオフだが、コミットメッセージ・コードコメントいずれにも表示ラグへの言及がない。実害は「メニュー表示中の一時的な表示不一致」で、項目実行後は正しく反映されるため深刻ではないが、ユーザー体験としては要確認。

**テスト突合**: 設計書が指名した3件のテストケース(`RightClickOnNonAnchorCell_..._PreservesOriginalSelectedCell`等)は**全て未実装**。省略理由(「View層のイベントハンドラ構造そのものに依存するためViewModelレベルの単体テスト不可」)はコミットメッセージに明記されており、設計書自体が「View層依存でどうしても単体テスト不可と判断した場合は実機確認へ委ねてよい」と事前承認しているため、**手続き上の瑕疵ではない**。ただし3件中1件(`DeleteExecuted_TargetsHitElement`、実行系の退行検知)は、既存の`RightClickElementSelection_OnAnyOccupiedCell_DeletesCorrectElement`(同一ファイル内、`HitTestElement`→`SelectedCell`正規化→`DeleteSelectedElement`という同型の模擬パターン)や、今回の修正1のTheoryテスト自身と同じ手法で実装可能だった疑いが残る。他の2件(表示のみに関わる観点)と一律で「テスト不可」として片付けられており、一貫性を欠く。侍の裁量の範囲内ではあるが、この1件だけでも省略前に家老へ一声確認する余地があったと考える。

## パターン再発検知

### 新規発見(往復4周目スコープ外、CONFIRMED): `ReplaceDocument`が画像挿入ドラフトをクリアしていない

**該当**: `MainWindowViewModel.cs:2085-2087`(`ReplaceDocument`)

`ClearConnectorDraftIfAny()`(2085行目)・`ClearFreeLineDraftIfAny()`(2087行目)は呼ばれているが、**`CancelImageInsertDraft()`の呼び出しが無い**。2113行目で`Tool = ToolState.SelectDefault`に上書きされるが、`_imageInsertDraft`フィールド自体は残留する。

**失敗シナリオ**: 画像挿入配置待機中(Tool.Mode=PlaceImage、`_imageInsertDraft`セット済み)に、確定・キャンセルせずファイル→新規/開くを実行すると`ReplaceDocument`が走る。Tool.ModeはSelectに変わるが`_imageInsertDraft`は旧文書の情報を保持したまま残り、`HasAnyDraft`が真のまま残留する。結果、新規/開く直後に右クリックすると`MainWindow.xaml.cs:863`の`if (_viewModel.HasAnyDraft) return;`ガードにより理由不明のまま右クリックメニューが一切出ない(左クリックでSelectedCellセッターを一度経由すれば解消するため症状は限定的だが、原因不明の一時的操作不能として現れる)。加えて、`LadderCanvas.cs`の画像挿入プレビュー描画がTool.Mode非依存で無条件実行されるため、旧文書の座標系に基づく幽霊プレビューが新文書上に表示される可能性がある(未実機確認)。

**パターン台帳との照合**: 既存PR-05(「状態リセット処理の横展開漏れ、`ReplaceDocument`等が担う状態クリア責務への追従漏れ」)に近いが、PR-05は「新設する処理側が既存の責務に追従し忘れる」方向、今回は逆に「既存の`ReplaceDocument`が新しいドラフト種別(画像挿入、T-064で追加)への追従を怠っている」方向であり完全一致ではない。**皮肉な点として、今回のコミット自身のテストコメントが「T-064で画像挿入ドラフトだけ横展開漏れが起きた前例と同型の見落としを防ぐため3種とも対称に確認する」と明記しているにもかかわらず、その同型の見落としが`ReplaceDocument`という別箇所に残存していた**。断定は避け「パターンの疑いあり(PR-05近縁)」として申し送る。

## code-reviewスキル併用の検分結果(4並列エージェント)

上記の他、severity低の指摘2件:
- `CancelResidualDraftForToolSwitch()`は`SelectedCell`のsetter(既に同じ3つのドラフトクリアを無条件で行う設計)と機能重複しており、`SelectedCell = SelectedCell`(自己代入)でも同等の効果を得られた可能性がある(既存の`ActivateSelectDefault`が同種の手法を採用済み)。ドラフト種別の列挙箇所が(1)setter内(2)Escapeハンドラ各層(3)本メソッド、の3箇所に分散する形になり、将来4つ目のドラフト種別が追加された際の更新漏れリスクが1箇所増えた(Simplification/Altitude角度、独立指摘)。
- `_viewModel.SelectedCell = pos;`が`BuildElementContextMenuItems`内の3メニュー項目へ個別コピペされており、将来項目追加時のコピペ漏れリスクがある。共通ヘルパー(例: `AddPosNormalizingMenuItem`)への切り出しで構造的に防げる(Reuse/Altitude角度、独立指摘)。

Conventions違反は無し。GridPos(readonly record struct)のクロージャキャプチャも都度新規MenuItem生成のため問題なし。

## 派生提案の有無

あり(上記「新規発見: ReplaceDocumentの画像ドラフトクリア漏れ」、CONFIRMEDだが往復4周目のスコープ外)。自らは着手せず家老へ報告のみ。
