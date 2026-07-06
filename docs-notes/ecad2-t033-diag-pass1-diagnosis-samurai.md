# T-033 増分2 診断ログ一次パス 原因診断（侍）

> 対象: `docs-notes/ecad2-t033-diag-pass1-ninja.log`（8行）。家老采配により、忍者一次パスログを解析。
> 結論: `IsArrangeValid`/`IsMeasureValid`の実測を待たずとも、既存8行だけで原因を数式レベルで確定できた。

## 結論：**CONFIRMED（実測完全一致・推測不要）**

`ElementPlacementBar.Margin`が**前回呼び出し終了時の値のまま**、今回呼び出しの`Measure()`実行時点でも
残留している。WPFの`FrameworkElement.DesiredSize`は「子要素の自然サイズ + Margin」で計算される仕様
（公式挙動）のため、前回のMarginがそのまま今回のMeasure結果に混入し、`barDesiredSize`を汚染していた。

`PositionPlacementBar`（`MainWindow.xaml.cs:646-666`）の現行コード：
```csharp
ElementPlacementBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));  // ← Margin未リセットのまま測る
Size barSize = ElementPlacementBar.DesiredSize;
...
ElementPlacementBar.Margin = new Thickness(x, y, 0, 0);  // ← 今回の結果を書き込むのはここ(測定後)
```
`ElementPlacementBar.Margin`をリセットする処理は`PositionPlacementBar`・`ClosePlacementBar`のどこにも
無く（grep確認済み）、初回呼び出し(XAML既定値`0,0,0,0`)以降は常に「前回の`postClamp`値」が居座り続ける。

## 検証：8行全てが「content(540,38) + 前回postClamp」に一致

| # | 直前(前回)postClamp | 予測DesiredSize = (540+前回X, 38+前回Y) | ログ実測値 | 一致 |
|---|---|---|---|---|
| 1 | (初回、Margin既定0,0) | (540.00, 38.00) | (540.00, 38.00) | ✅ |
| 2 | 前回(#1)=(299.59,219.57) | (839.59, 257.57) | (839.59, 257.57) | ✅ |
| 3 | 前回(#2)=(299.59,321.47) | (839.59, 359.47) | (839.59, 359.47) | ✅ |
| 4 | 前回(#3)=(299.59,219.57) | (839.59, 257.57) | (839.59, 257.57) | ✅ |
| 5 | 前回(#4)=(299.59,321.47) | (839.59, 359.47) | (839.59, 359.47) | ✅ |
| 6 | 前回(#5)=(299.59,219.57) | (839.59, 257.57) | (839.59, 257.57) | ✅ |
| 7 | 前回(#6)=(299.59,321.47) | (839.59, 359.47) | (839.59, 359.47) | ✅ |
| 8 | 前回(#7)=(337.39,219.57) | (877.39, 257.57) | (877.39, 257.57) | ✅ |

8/8完全一致。`barActualSize`が常に`(540.00,38.00)`で安定しているのも整合する
（ActualSizeは実際のArrangeパス後の見た目サイズで、Margin混入の影響を受けない）。

行10の3連続開き直しで`257.57`⇔`359.47`が往復したのも、「今回の結果(postClamp)が次回のDesiredSizeを
汚染し、それが次回のclampMaxを変え、その結果(postClamp)がさらに次回を汚染する」という**自己参照の
フィードバックループ**として説明がつく（セル位置自体の計算=`translateOut`は全8行で当該セルにつき完全に
安定しており、ブレの発生源は`barDesiredSize`のみという忍者所見と整合）。

## 家老申し送りの理論仮説（レイアウト無効化の過渡状態）との関係

本診断により、`IsArrangeValid`/`IsMeasureValid`不整合説を持ち出さずとも8/8完全一致で説明がつくため、
**当該仮説は今回の主原因ではないと判断する**（残留Marginという別の実装バグが唯一かつ十分な説明）。
ただし、今回コミット済みの`canvasHostIsArrangeValid`等のログ項目は無害であり、修正後の再測定で
「常にtrue」であることを確認する副次チェックとしてそのまま残しても支障はない。

## 提案する修正方針（実装はまだしていない、家老裁可待ち）

`PositionPlacementBar`の`Measure()`呼び出し直前に、`ElementPlacementBar.Margin`を`Thickness(0)`へ
明示的にリセットしてから測定する（1行追加のみ）。これにより`DesiredSize`は常に「その時点の内容だけに
基づく自然サイズ」を返すようになり、前回位置の残留汚染・自己参照フィードバックループが根本から絶たれる。

```csharp
ElementPlacementBar.Margin = new Thickness(0);  // 前回位置の残留を除去してから測る(新規追加)
ElementPlacementBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
Size barSize = ElementPlacementBar.DesiredSize;
```

副作用・リスクは極めて小さいと判断する（Measure→Arrangeの間にMarginを触るだけで、他の計算式・
クランプロジック・座標系(RootLayoutGrid、増分2位置バグ(a)修正分)には影響しない）。

## 出典
- `docs-notes/ecad2-t033-diag-pass1-ninja.log`（実測8行）
- `docs-notes/ecad2-t033-diag-pass1-analysis-ninja.md`（忍者の客観対応表）
- `src/Ecad2.App/MainWindow.xaml.cs:646-666`（`PositionPlacementBar`）・`:726-`（`ClosePlacementBar`、Margin未リセット確認）
