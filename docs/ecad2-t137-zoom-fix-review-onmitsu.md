# T-137ズーム直し静的レビュー（1周目・軽量既定）

隠密（key=1785485896132）記す。2026-07-31。対象＝`1d65b6e`（push済み）。

## 結論：指摘なし。ただし(c)3に検討の余地1件（軽微・要修正ではない）

## (a)(b) 実装・テストの整合

`DrawingTheme.ZoomInvariantWidthMm(baseWidthMm, zoom) => zoom > 0 ? baseWidthMm / zoom : baseWidthMm;`（`DrawingTheme.cs:70-71`）を`PartEditorCanvas.DrawCellGrid`が`_theme.Get(StrokeRole.Grid)`の幅へ適用する1行差し替えを確認。テスト15件（`DrawingThemeTests.cs`、数え直し＝Fact1+Theory(3+4+2+3+2)=15、commitの主張と一致）を`dotnet test`で実行し全合格を確認。

## 最重点：射程（倍率2.0まで一定）の数理的検算

`WpfRenderer.Pen`のクランプ式`Math.Max(s.Width, MinStrokeWidthMm)`と、`PushTransform`のスケール適用（画面上の太さ＝クランプ後の値×zoom）を踏まえ、以下を算術で確かめた（共有main上での実注入は行わず、静的な計算で代替）。

- **zoom≦2.0**：`baseWidth/zoom ≥ MinStrokeWidthMm(0.05)`（`0.10/2.0=0.05`ちょうど）ゆえクランプは効かず、画面上の太さ＝`(baseWidth/zoom)×zoom＝baseWidth`で厳密に一定。
- **zoom>2.0**：`baseWidth/zoom < 0.05`ゆえクランプが効き、画面上の太さ＝`0.05×zoom`。これは`baseWidth(0.10)`より太い（`0.05×zoom>0.10 ⟺ zoom>2`、代数的に全域で成立）が、**改修前**（zoom倍率を割らず直接描いた場合の太さ＝`Math.Max(0.10,0.05)×zoom=0.10×zoom`）より必ず細い（`0.05×zoom<0.10×zoom`は`zoom>0`で自明に成立）。

**docコメント・テストの主張（`zoom≦2.0`は完全一定、`zoom>2.0`は太くなるが改修前より必ず細い）は、代数的に全域で成立する性質であり、テストの境界値・代表値の選び方（2.0/4.0/8.0）も的確**と判ずる。

## RED証明3件の机上検算

| 改変 | 侍の実測 | 隠密の机上検算 |
|---|---|---|
| A（`/zoom`を`*zoom`へ取り違え） | 11件RED | `zoom=1`を含むテスト（`倍率1なら素の値のまま`）は乗除どちらでも同値に潰れ検出せぬ設計だが、他5メソッドのうち`zoom≠1`のケースを数えると、`逆数を掛けた値になる`(3)+`画面上の太さが一致する`(4)+`倍率2までは完全に一定`のうち`zoom≠1`の2ケース+`倍率2超`(2)＝3+4+2+2=**11件で一致** |
| B（`zoom>0`ガード除去＝ゼロ除算・負値をそのまま返す） | 2件RED | `倍率が0以下なら素の値を返す`の2ケース（`zoom=0.0`→`Infinity`、`zoom=-1.0`→負値）がまさにこのガードを狙い撃ちしており、両方失敗する。**2件で一致** |
| C（View層の繋ぎ込みを外す＝`DrawCellGrid`を改修前の直値へ戻す） | 0件RED | `Ecad2.Core.Tests`は`DrawingTheme.ZoomInvariantWidthMm`という純粋関数のみを対象にしており、`PartEditorCanvas.DrawCellGrid`（`Ecad2.App`、`FrameworkElement`派生）の呼び出し配線そのものを検証するテストは存在しない。**0件で一致**（侍の申告どおり、繋ぎ込みは単体テストの網の外） |

3件とも侍の実測値と一致し、RED証明の正確性に疑義なし。

## (c) 侍の見立て「純粋関数化を尽くした」の検分——**一部に検討の余地あり（軽微、修正不要と判ずる）**

`memory: feedback_hard_to_test_is_design_smell`（テストしにくいは設計の匂い、特殊な仕掛けは最後の手段）に照らし、`DrawCellGrid`の配線がこれ以上純粋関数化できないかを検分した。

`DrawCellGrid`内の該当箇所は現在こうなっている：

```csharp
var inner = baseStroke with { Width = DrawingTheme.ZoomInvariantWidthMm(baseStroke.Width, _zoom) };
var outer = inner with { Color = inner.Color with { A = (byte)(inner.Color.A / 2) } };
```

**`ZoomInvariantWidthMm`自体はテスト済みの純粋関数だが、「この2行が`_theme`・`_zoom`という実行時の値から`outer`/`inner`の`StrokeStyle`を組み立てる」という結合ロジック自体は、まだ`PartEditorCanvas`（View層）に residing しており、`Ecad2.App.Tests`からも未検証**——`PartResolver.SheetAffinityOf`同様、`Ecad2.App.Tests`は`Ecad2.App`を参照可能（`T136SheetAffinityTests.cs`で確認済みの前例）ゆえ、**この2行を`(DrawingTheme theme, double zoom) => (StrokeStyle outer, StrokeStyle inner)`という戻り値を持つ`static`メソッドへ切り出せば、`RenderTargetBitmap`・STAを一切要さずに`Ecad2.App.Tests`から単体テストできる見込み**である（`_theme`・`_zoom`は共に単純な値であり、`IRenderer`や実描画には触れていないため）。

**ただし、これを行っても「`Draw()`が実際にこの切り出し関数を`_theme`・`_zoom`の正しい値で呼んでいるか」という最後の1行の結線までは埋まらぬ**——侍が指す「`RenderTargetBitmap`＋STAを要する」網の必要性そのものは、より薄い形で残る。**ゆえに侍の見立て「これ以上の純粋関数化の余地はない」は、正確には「”太さの算出式”自体はこれ以上分解できないが、”theme・zoomからStrokeStyleへの組み立て”という、もう一段小さな結合ロジックの切り出しはまだ可能」という形に改まる**と隠密は見る。

**この程度の追加切り出しは1周目の軽量既定を超える改修提案ゆえ、本レビューでは要修正としない**。家老・侍の判断で、次回同種の配線が増えた際にでも検討されたい程度の軽微な指摘として記録する。

## 出典

`1d65b6e`の全diff直読、`WpfRenderer.cs:60-61,146-166`（`Pen`のクランプ式）、`PartEditorCanvas.cs:733-751`、`dotnet test`実測（`DrawingThemeTests`15件全合格）。

## 派生提案

(c)の追加純粋関数化の余地1件（軽微、上記参照）。実装を求めるものではなく気づきの共有に留める。
