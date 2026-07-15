# T-092（ドラフト中のAddRow/DeleteRow/Undo/Redoブロック）静的レビュー（隠密）

レビュー日: 2026-07-15
対象コミット: e057bde
対象diff: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（4箇所）・`tests/Ecad2.App.Tests/T092FixTests.cs`（新規）
レビュー深度: 軽量既定（1周目、karo.md方針）
併用: code-reviewスキル（low effort）

## 総合判定

**設計・実装・テストとも妥当。指摘なし。**

## (a) 台帳DoDとの整合

`AddRowCommand`/`DeleteRowCommand`/`UndoCommand`/`RedoCommand`の4コマンドCanExecuteへ`!HasAnyDraft`を追加（`MainWindowViewModel.cs:2592,2603,2684,2692`）。T-091（F5〜F10のHasAnyDraft見落とし修正）と同一パターンの横展開であることを確認。殿裁定（ブロック方式採用）どおりの実装。

## (b) code-reviewスキル併用（low effort）

diffのhunkのみから見て取れる正しさのバグ・重複・デッドコードは無し（(none)）。4箇所とも既存条件式へ`!HasAnyDraft &&`を追加するのみの単純な変更で、削除されたコードブロックもない。

## (c) 狙い撃ち観点

### ドラフト種別3種×4コマンドの網羅漏れ
`HasAnyDraft`の定義（`MainWindowViewModel.cs:1674`）は`_connectorDraft is not null || _freeLineDraft is not null || _imageInsertDraft is not null`で、3種のドラフト全てを正しく参照している。新設テスト（`T092FixTests.cs`）も`[InlineData("Connector")]`/`[InlineData("FreeLine")]`/`[InlineData("Image")]`の3種×4コマンド(AddRow/DeleteRow/Undo/Redo)=12ケースを`[Theory]`で網羅しており、漏れなし。対照ケース（ドラフト無しでCanExecute=true、AddRow/DeleteRowの2件）も退行検知として適切に配置されている。

### RED先行証明12ケースが実際にバグを突いているか
コミットメッセージに「RED先行証明（git stash --keep-index、対象ファイル限定）: 12ケース、修正前コードで個別にRED実測確認済み」とある。これに加え、隠密独自にExecute呼び出し経路をExploreエージェントで洗い出した（`AddRowCommand`/`DeleteRowCommand`/`UndoCommand`/`RedoCommand`への全参照、XAML標準バインディング6箇所・コード側MenuItem.Command代入1箇所・キーボードショートカット直接Execute呼び出し4箇所、計11箇所）。

**結果：CanExecuteをバイパスしてExecuteが直接呼ばれる経路は存在しない。** キーボードショートカット4箇所は全て`if (cmd.CanExecute(null)) cmd.Execute(null);`という明示的ガード付き、XAML/コード側のCommandバインディングもWPF標準の`ICommandSource`機構でCanExecute=falseの間は自動的に無効化される。したがって、Execute側ラムダに`HasAnyDraft`チェックを追加していなくても、現在確認できる全呼び出し経路においてはCanExecute側の修正のみで実効性がある。RED証明12ケースは実際の防御対象（=各コマンドの起動経路）を正しく突いていると判断する。

### 補足（経過観察、指摘なし）
Execute側ラムダには元々`HasAnyDraft`はもとより`CanEditDiagram`のチェックも無く（境界値チェックのみ）、T-055増分1由来のコメント「Execute内部のガードは...に対する安全弁」という説明とは裏腹に、今回の修正はCanExecute側のみへの追加という既存パターンをそのまま踏襲している。現時点で実害はないが（上記経路調査で確認済み）、将来Execute直接呼び出しの新経路が追加された場合はこの限りでない、という設計上の前提を申し送りとして記録する。

## スコープ確認
diff対象は`MainWindowViewModel.cs`（4箇所）＋新規テストファイル1件のみ、侍自己申告と一致。範囲外変更なし。

## 総括
致命的指摘・cleanup指摘とも無し。1周目レビューとして完了扱いで問題ないと判断する。
