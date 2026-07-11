# T-074 バージョン情報ダイアログ 差分レビュー（隠密）

対象: ブランチ`main`（マージ後）、コミット`70d1bb3`。家老指定3観点の手動確認を実施。対象4ファイル
（`MainWindow.xaml`/`MainWindow.xaml.cs`/`Views/AboutDialog.xaml`/`Views/AboutDialog.xaml.cs`）。

**結論：全3観点クリーン。忍者実機検証へ回して差し支えない。**

## (1) 表示文言（アプリ名+バージョン番号のみ）

`AboutDialog.xaml.cs:14`：
```csharp
VersionText.Text = version is null ? "Ecad2" : $"Ecad2 v{version.Major}.{version.Minor}.{version.Build}";
```
「Ecad2 vX.Y.Z」のみ。著作権表示・ビルド日時・会社名等の混入なし。殿裁定どおり。

## (2) `Assembly.GetName().Version`とcsprojの`<Version>`の連動確認

`Ecad2.App.csproj`に`<Version>0.3.0</Version>`のみ指定（`<AssemblyVersion>`の明示指定なし）。
.NET SDKの標準動作により`<Version>`から`AssemblyVersion`/`FileVersion`/`InformationalVersion`が
自動生成される（本プロジェクトの既存リリース運用、`ecad2-release-procedure.md`のVersion更新手順とも
整合）。`Assembly.GetExecutingAssembly().GetName().Version`はハードコードではなく実行時取得であり、
連動方式は正しい。

表示は`Major.Minor.Build`の3点のみで`Revision`を含まないが、`<Version>0.3.0</Version>`（3点形式）
から生成される`AssemblyVersion`は`0.3.0.0`（Revision=0固定）となるため、現状の運用では表示漏れは
発生しない。将来`<Version>`の運用がRevisionを使う4点形式に変わった場合のみ表示に反映されない点は
留意点として記録するが、現状の3点形式運用とは整合しており問題ではない。

## (3) RenameDialogとのモーダル実装の一貫性

`RenameDialog`/`AddSheetDialog`/`SheetSettingsDialog`と`AboutDialog`を比較。

- `WindowStartupLocation="CenterOwner" ResizeMode="NoResize" ShowInTaskbar="False"`が完全一致。
- `StackPanel Margin="12"`のレイアウト構造、`<summary>`コメント「design-brief 4節#4: 非ネスト方針、
  単一階層のみ」の明記も踏襲。
- 起動側（`MainWindow.xaml.cs`）は`new Views.XxxDialog(...) { Owner = this }`→`ShowDialog()`の
  パターンで一致（`RenameSheetButton_Click`: 175-184行、`AboutMenuItem_Click`: 168-173行）。

差異点はいずれも機能上必然：
- `RenameDialog`はOK/キャンセル2ボタン（入力の確定/破棄が必要）、`AboutDialog`はOK1ボタンのみ
  （表示専用、確定/破棄の区別が不要）。
- `AboutDialog`のOKボタンは`IsDefault="True" IsCancel="True"`両方を持つ（Enter/Escいずれでも
  閉じられる、単一ボタンなので合理的）。`RenameDialog`はOK側に`IsDefault`のみ、キャンセル側に
  `IsCancel`を分離（2ボタンなので区別が必要）。
- `AboutMenuItem_Click`は戻り値`ShowDialog()`の結果を見ない（`AboutMenuItem_Click`コメント
  「表示のみで状態変更を伴わないためViewModelへの委譲なし」）。`RenameSheetButton_Click`は
  `dialog.ShowDialog() == true`を見てViewModelへ委譲。これも表示専用と結果確定の機能差に対応した
  妥当な差異であり、パターンの逸脱ではない。

**評価：一貫性あり。既存3ダイアログと同型のパターンを踏襲しつつ、表示専用という性質に応じた
最小限の差異のみ。**

## まとめ

| 観点 | 判定 |
|---|---|
| 表示文言（著作権表示等の混入なし） | OK |
| Assembly.GetName().Versionのcsproj連動（ハードコードなし） | OK |
| RenameDialogとの実装一貫性 | OK |

指摘事項なし。
