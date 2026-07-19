# T-067基盤実装（コミット837b407）静的レビュー — 隠密

日付: 2026-07-18
対象: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（コミット837b407のみ、`git show 837b407 -- <path>`の範囲）
作業ツリーの未コミット3ファイル（App.xaml/MainWindow.xaml/MainWindow.xaml.cs、T-099/T-089用）は対象外。

## 経緯（effort levelについて）

家老の当初指示は「1周目ゆえ軽量既定」。`code-review`スキル呼び出し時、argsの記法が実際のlevel
解釈と噛み合わず、1回目はxhigh相当のテンプレートが展開された。levelを`high`へ明示し直して
Phase 1（8角度finder、onmitsu.md「effort levelは変更規模に応じて選ぶ」基準＝増分の初回実装は
high程度、に沿う）を並列実行し終えた直後、家老より「今回はeffort=lowで統一する、進行中なら
打ち切ってやり直してよい」との訂正が届いた。

Phase 1は既に完了しており、複数の独立した角度（B=removed-behavior auditor／C=cross-file
tracer／Reuse観点）が下記2件を独立に一致して検出していたため、全破棄してlowから再走するのは
コストの二重取りと判断。この2件のみを自分の手でコード読解により直接検証（追加のverify agentは
使わず軽量化）し、他の単発cleanup系指摘は参考記録に留めるかたちで収束させた。総体としては
「軽量に厳選して収める」という家老の意図は損なっていないと判断するが、Phase 1自体がhigh相当の
コストを要した点は率直に報告する。

## (a) 台帳DoDとの整合確認

`docs/todo.md` T-067節の「基盤区切り完了」記載（SelectedFrame新設・DeleteSelectedFrame/
RenameSelectedFrame・BeginDragFrame〜CancelDragFrame・BeginFrameDraft〜CancelFrameDraft・
IsFrameWithinGridBounds、回帰テスト17件、View層未着手）は、diff内容と過不足なく一致。
View層未配線の自己申告も、新規public/internalメンバーをGrepした結果（角度C）呼び出し元が
定義箇所以外に存在しないことで裏付けられた。build/test回帰なし（App.Tests 712件・Core.Tests
120件）は侍報告のとおりで、本レビューでは再実行していない（実機/実行確認は忍者領分、静的
レビューの範囲外と判断）。

## (c) 狙い撃ち観点＝SetProperty早期return罠

**該当なし**。`SelectedCell`のsetterへの新規追加（`SelectedFrame = null` / `ClearFrameDraftIfAny()`）は、
既存の確立パターン（SelectedConnector/SelectedFreeLine/SelectedImage等）と同じく、`SetProperty`の
早期return判定より**前**に置かれており、値が数値上一致する経路でもクリア処理は必ず実行される。
`SelectedFrame`自身のsetterも、`ForceCancelDragFrameIfAny()`とその後のOnPropertyChanged 2件を
`SetProperty`の戻り値に関わらず無条件で呼ぶ設計で、SelectedImageと同型。罠は回避できている。

## (b) code-reviewスキル併用 — CONFIRMED findings（2件）

いずれも自分でコードを読んで実際に確認済み。**両方とも`docs-notes/pattern-recurrence-log.md`
PR-01「新規選択可能状態の横展開漏れ」の再発**と判定し、台帳へ追記済み。

### 1. `ReplaceDocument`が`SelectedFrame`/`_frameDraft`をクリアしていない

- 場所: `MainWindowViewModel.cs` 2844〜2899行目（`ReplaceDocument`メソッド）
- `ReplaceDocument`は新規作成/開く操作の単一ゲートウェイで、`_selectedCell`直接代入により
  `SelectedCell`のsetter経由の自動クリアをバイパスするため、`SelectedConnector`/
  `SelectedWireBreak`/`SelectedFreeLine`/`SelectedConnectionDot`/`SelectedImage`と
  `ForceCancelDragElementIfAny()`/`ClearConnectorDraftIfAny()`/`ClearFreeLineDraftIfAny()`/
  `CancelImageInsertDraft()`は個別に明示クリアされている（コメントに「T-064で同型の漏れが
  あった」と自ら記録）。新設の`SelectedFrame`と`_frameDraft`（`ClearFrameDraftIfAny()`または
  `CancelFrameDraft()`）だけがこの並びに追加されていない。
- 失敗シナリオ: 文書Aで枠(GroupFrame)を選択またはドラッグ/記入中の状態でCtrl+O等により別文書へ
  切り替えると、`SelectedFrame`が旧文書の実体を参照したまま残留する。右パネルのプロパティ表示が
  旧枠を指し続け、`DeleteSelectedFrame`内の`sheet.Frames.Contains(frame)`がfalseになり削除操作が
  無反応になる。記入中だった場合は`FrameDraftPreview`が新文書のグリッド範囲外座標のまま幽霊
  プレビューを描画し続ける（`ImageInsertDraft`で past に実際に起きた症状と同型）。

### 2. `HasAnyDraft`が`_frameDraft`を含んでいない

- 場所: `MainWindowViewModel.cs` 1869行目
  `public bool HasAnyDraft => _connectorDraft is not null || _freeLineDraft is not null || _imageInsertDraft is not null;`
- `HasAnyDraft`はAddRow/DeleteRowCommand（2983・2994行目）およびUndo/RedoCommand（3075・3083行目）の
  CanExecuteガードに使われている（1864〜1868行目のコメントが、まさに「記入中ドラフトの実体保持を
  見ずに静的なツールモードだけで判定すると過剰・過少双方の誤りを生む」という過去の教訓を記す
  箇所）。新設の`_frameDraft`がこの列挙に加わっていない。
- 失敗シナリオ: `BeginFrameDraft`で枠の記入を開始し、`AdjustFrameDraft`でグリッド最終行付近まで
  範囲を広げている最中に、`HasAnyDraft`がfalseのままなためDeleteRowCommandが実行可能でグリッド
  行数が縮小する。`_frameDraft`はクリアも再クランプもされないため、直後のEnterで
  `ConfirmFrameDraft()`が境界再検証なしにそのまま古いAnchor/Width/Heightで`GroupFrame`を追加し、
  新グリッド範囲をはみ出した枠が生成される。同様の理屈でUndo/Redoも記入中に素通しされる。

## 参考記録（cleanup系、正式指摘としては見送り・軽量方針により深追いせず）

- `IsFrameWithinGridBounds`（2572行目）が既存`IsWithinGridBounds`（2565行目）とほぼ同一ロジックの
  複製（height引数を足せば統合可能）。Reuse/Altitude両角度が独立に指摘、確度は高いが実害なし。
- `ConfirmFrameDraft`（1508〜1521行目）が`FrameDraftPreview`ゲッターと同じ4フィールド初期化を
  手書きで重複。
- `ClearFrameDraftIfAny`（1534行目）が`CancelFrameDraft()`を呼ぶだけの薄いラッパーで、既存の
  Clear*IfAny/Cancel*の命名慣習とやや逆転している。
- `FrameDraftPreview`（1472〜1479行目）がgetter呼び出しの都度`new GroupFrame`を生成（矢印キー
  連打時に軽微なアロケーション増）。

## 不明点

なし。

## 派生提案（範囲外の気づき）

- PR-01は制度化済み（`samurai.md`「新規選択可能状態の横展開チェックリスト」5項目）だが、その
  チェックリストが存在する状態で今回また同型の漏れ（`ReplaceDocument`・`HasAnyDraft`の2箇所）が
  発生した。チェックリストの実効性（侍が実装時に実際に参照・self-checkしたか等）を家老にて
  点検いただきたい。`docs/proposed.md`経由で提起する。
