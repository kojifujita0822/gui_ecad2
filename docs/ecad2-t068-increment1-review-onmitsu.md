# T-068増分1 静的レビュー（隠密）

対象コミット：`74eb164`（`PartEditorDialog`新設・`PartPaletteViewModel`拡張・メニュー配線、
6ファイル/+359行）。effort=low（新規実装1周目、既定通り軽量）。

## 結論：DoD整合OK、指摘1件（要確認）・気づき1件

### 1. DoD整合

`docs/todo.md` T-068増分1節のDoD（プロパティ編集・新規作成/保存/削除のUI導線・名前必須
バリデーション）を実装確認済み。

- 名前必須：`PartEditorDialog.OkButton_Click`で`name.Length == 0`時に`ShowError`、確認済み
- 幅高さ1〜12セル：`int.TryParse`+範囲チェック（境界値1・12は許容、0・13は拒否）、確認済み
- 役割：GuiEcad原本同一8種（殿裁定=案A）、`RoleChoices`配列で固定
- UI導線：メニュー「パーツ(_P)」→「新規作成」「自作パーツ」サブメニュー（編集/削除）、殿裁定=案B
  どおり実装確認済み。基本図形（`Category==""`）はサブメニュー対象外（`自作`のみ）を確認
- 編集時のファイル名変更対応：`SaveEditedPart`が新旧パス不一致時のみ旧ファイル削除、GuiEcad原本
  （`OpenFolderPartEditor`）踏襲どおり

### 2. Libraryインスタンス同一性維持の設計（家老依頼観点）

`PartPaletteViewModel.Library`は`{ get; } = new()`でフィールド初期化のみ、`Load()`内では
`Library.ById.Clear()`＋再構築のみでインスタンス自体は再代入しない設計を確認。
`MainWindowViewModel.cs:3212`で`PartLibrary = PartPalette.Library;`と一度だけ参照を取得している
ため、この設計により`SaveNewPart`/`SaveEditedPart`/`DeletePart`後も`MainWindowViewModel.PartLibrary`
が自動的に最新化される。**設計・実装とも妥当**。加えて`PartPaletteViewModelCrudTests.
SaveNewPart_KeepsSameLibraryInstance`で`Assert.Same`により明示的に固定済みで、将来の回帰も検知
できる。侍のコメント「実装中にUndoスタック同様の見落としを避けるため明示的に固定した」は妥当な
自己認識。

### 3. 増分0のUndo/Redo設計ミスと同型の罠の有無（家老依頼観点）

該当なし。増分0のバグは「ドラッグ中に直接書き換え続けるフィールドを、確定時点でそのままUndo
スタックへ積むと変更後の状態を積んでしまう」というタイミングの逆転が原因だった。今回の
`SaveNewPart`/`SaveEditedPart`/`DeletePart`はいずれも「ストアへの書き込み（`SaveCustom`/`Delete`）
→`Load()`で同期的に再構築」という単純なフローで、ドラッグ中の逐次書き換えのような中間状態を
持たない。同型のタイミング逆転が入り込む余地はない。

### 4. 指摘：「パーツ(_P)」メニューにCanEditDiagramガードが無い（要確認）

新規作成・編集・削除のいずれのメニュー項目にも`IsEnabled="{Binding CanEditDiagram}"`が付与されて
いない（`MainWindow.xaml`の他の編集系メニュー項目——画像挿入・行操作等——は軒並みこのガードを
持つ）。パターン再発台帳PR-13「上位モードゲート確立後に追加された新機能のゲート接続漏れ」の型に
該当しうる観点として確認した。

- **HasProjectガード不要は妥当**：パーツライブラリはプロジェクト（ドキュメント）に依存しない
  グローバルなリソースのため、プロジェクト作成前でも編集できてよいと考えられる
- **CanEditDiagram（テストモード中の編集禁止ゲート）については意図的な仕様か未確認**：テストモード
  中に既存の自作パーツを編集・削除すると、`PartResolver`が参照する`PartLibrary`の内容がテスト実行中
  に変わることになり、既に配置されている要素の解決結果（幅・高さ・役割等）へ実行時に影響しうる。
  図面自体への直接編集ではないためCanEditDiagramの対象外という設計判断もあり得るが、**意図的な
  除外か見落としかはコードから判別できない**ため、家老・侍へ確認を要望する（実装変更が必要と
  決まった場合は軽微な追加で対処可能）

### 5. 気づき（軽微、指摘の要否は家老判断）

新規作成時、既存と同名のパーツを作成すると`PartFolderStore.SaveCustom`が同一ファイルパスへ上書き
保存する（名前の一意性チェックが無い）。ただしGuiEcad原本調査（`docs/ecad2-t068-part-editor-plan-
onmitsu.md`）でも「名前必須以外の明示的バリデーションは見当たらない」と確認済みであり、原本踏襲の
範囲内。指摘としては見送ってよい水準と考える。

## ビルド・テスト

`dotnet build src/Ecad2.sln -c Debug`成功。侍報告どおりCore131+App808件、回帰なし（本レビューでの
再実行は見送り、侍のbuild/test合格申告を信頼）。

## code-reviewスキル併用

既知の恒久事象により手動レビューで代替（`git show`で範囲明示）。
