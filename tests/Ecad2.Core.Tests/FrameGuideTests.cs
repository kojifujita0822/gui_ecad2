using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-140系統2・P-150（殿裁可2026-08-02）: パーツエディタの基準枠色を原本回帰(青)へ戻した回帰テスト。
/// 設計書=docs/ecad2-t140-keitou2-test-design-onmitsu.md §3.2。
/// StrokeRole.PartFrameGuideとしてCore層のGet()経由にしたため、色・太さ・線種の3要素すべてを
/// 測れる(系統1のXAMLリソース定義=存在の有無しか測れないのとは異なる、設計書§1)。
/// </summary>
public class FrameGuideTests
{
    [Fact]
    public void FrameGuideは原本GuiEcadの値と一致する()
    {
        Assert.Equal(new Color(255, 96, 150, 230), DrawingTheme.FrameGuide);
    }

    [Fact]
    public void Get_PartFrameGuideの色はFrameGuideと一致する()
    {
        Assert.Equal(DrawingTheme.FrameGuide, DrawingTheme.Default.Get(StrokeRole.PartFrameGuide).Color);
    }

    [Fact]
    public void Get_PartFrameGuideの太さは現状維持の0_1である()
    {
        // 色だけ変える裁定ゆえ、旧PartEditorCanvas.cs直書き値(0.1)を維持する。
        Assert.Equal(0.1, DrawingTheme.Default.Get(StrokeRole.PartFrameGuide).Width);
    }

    [Fact]
    public void Get_PartFrameGuideの線種は現状維持のDashedである()
    {
        Assert.Equal(LineStyle.Dashed, DrawingTheme.Default.Get(StrokeRole.PartFrameGuide).Style);
    }

    [Theory]
    [InlineData(false)] // Default(Light)
    [InlineData(true)]  // Dark
    public void FrameGuideはGridColor及びForegroundのいずれとも異なる(bool dark)
    {
        var theme = dark ? DrawingTheme.Dark : DrawingTheme.Default;

        // 案Aの根拠そのもの——色相で弁別する。区切り線(GridColor)・中心線相当(Foreground)の
        // いずれとも同値では、枠が「灰の一段」に埋没する(隠密の色案§0)。
        Assert.NotEqual(theme.GridColor, DrawingTheme.FrameGuide);
        Assert.NotEqual(theme.Foreground, DrawingTheme.FrameGuide);
    }

    [Fact]
    public void FrameGuideはテーマ非依存である()
    {
        // 「意味色はテーマ間で固定」という既存の宣言(DrawingTheme.cs冒頭コメント)と実装が
        // 一致することの確認。DefaultとDarkでGet()の結果が完全に一致するかを見る。
        Assert.Equal(
            DrawingTheme.Default.Get(StrokeRole.PartFrameGuide),
            DrawingTheme.Dark.Get(StrokeRole.PartFrameGuide));
    }
}
