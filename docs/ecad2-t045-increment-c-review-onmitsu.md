# T-045増分C（P-025 View層：TryPlaceElement境界ガード+RowAtDip除去）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`bf0e2c2`（`feat(app): T-045増分C View層(TryPlaceElement
> 境界ガード+RowAtDip除去)`、親`3742cd4`）。`code-review`スキル（8角度、4エージェントへ統合し
> 並行実行、1-vote検証を含む）をmedium effortで併用。実測検証（`dotnet test`、grep横断確認）も
> 併用した。

---

## 結論：**クリーン、指摘なし**

DoD(1)〜(6)いずれも出典付きで確認でき、`code-review`8角度でも指摘は0件だった。忍者実機検証
（必須）へ回した。

---

## DoD(1)〜(6) の検証結果

### (1)(2) 計画書増分C節どおり・殿裁定（既存様式に揃える）どおりか

`docs/ecad2-t045-implementation-plan-samurai.md`増分C節の「`TryPlaceElement`が増分Bの検証関数を
呼ぶよう変更し、UXの即時フィードバックを強化する」という記述どおり、`MainWindow.xaml.cs:1327`
付近に境界チェックが追加された：

```csharp
if (!_viewModel.IsSelectedCellWithinGrid())
{
    _viewModel.StatusMessage = "選択したセルはグリッド範囲外です";
    return;
}
if (_viewModel.IsSelectedCellOccupied())
{
    _viewModel.StatusMessage = "選択したセルには既に要素があります";
    return;
}
```

境界チェックが既存の占有チェックより**前**に配置されており、両者は`StatusMessage`表示＋`return`
という同一様式。殿裁定「既存様式に揃える」（2026-07-09）どおりで、新規UI要素（ダイアログ・
ポップアップ等）の追加はない。

### (3) RowAtDip除去

`LadderCanvas.cs:233-237`（P-039で`ToRowBoundary`に置き換え後の残存死にコード）が削除された。
リポジトリ全体（`src/`・`tests/`）を`RowAtDip`でgrepし、実コードからの参照が完全に無いことを
実測確認した（ヒットしたのは`docs-notes/`・`docs/`配下の除去記録・経緯説明のみ）。

### (4) 境界判定のDRY化がValidatePlacement経路の挙動を変えていないか

```csharp
private static bool IsWithinGridBounds(GridPos pos, Sheet sheet)
    => pos.Row >= 0 && pos.Row < sheet.Grid.Rows
    && pos.Column >= 0 && pos.Column < sheet.Grid.Columns;

private bool ValidatePlacement(GridPos pos, Sheet sheet)
    => IsWithinGridBounds(pos, sheet) && !sheet.Elements.Any(el => el.Pos == pos);

public bool IsSelectedCellWithinGrid()
    => SelectedCell is { } pos && CurrentSheet is Sheet sheet && IsWithinGridBounds(pos, sheet);
```
（`MainWindowViewModel.cs:1286-1303`付近）。`IsWithinGridBounds`への抽出は旧`ValidatePlacement`の
インライン条件式と完全に等価（byte-for-byte一致）であり、境界判定の挙動は変わっていない。
`IsSelectedCellWithinGrid()`の`SelectedCell is { } pos`パターンは既存`IsSelectedCellOccupied()`と
同じ慣用句でnull安全。

### (5) スコープ遵守

`git show bf0e2c2 --stat`で変更ファイルが以下4件のみであることを確認：
`MainWindow.xaml.cs`／`MainWindowViewModel.cs`／`LadderCanvas.cs`／`MainWindowViewModelTests.cs`。
`docs/`配下のファイル変更は含まれず、横展開（`TryPlaceWireBreak`/`TryPlaceConnectionDot`）も
diffに含まれていない（コミットメッセージにも「家老判断により本増分に含めない」と明記）。

### (6) dotnet test実測

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 194、スキップ: 0、合計: 194 - Ecad2.App.Tests.dll
```
Core14+App194＝208件全合格。コミットメッセージの報告と一致。

---

## code-review追加指摘

**指摘なし。** 8角度（line-by-line diff scan／removed-behavior＋cross-file tracer／Reuse＋
Simplification＋Efficiency／Altitude＋Conventions、4エージェントへ統合し並行実行）いずれも
空配列。`IsWithinGridBounds`の`static`ヘルパー化は適切（インスタンス状態を参照しないため）、
`IsSelectedCellWithinGrid()`/`IsSelectedCellOccupied()`の呼び出し元もView層の意図した箇所のみ
（他からの誤参照なし）。

---

## 忍者検証観点（引き継ぎ）

計画書記載どおり：境界外セル・占有セルへの配置試行がUI上でも正しく弾かれること、通常の配置
操作に回帰がないこと。
