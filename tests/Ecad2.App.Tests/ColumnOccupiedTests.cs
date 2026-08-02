using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-132増分2: 列に要素が存在するかの判定（<c>MainWindowViewModel.IsColumnOccupied</c>）。
/// 設計＝<c>docs/ecad2-t132-increment2-columnoccupied-test-design-onmitsu.md</c>（隠密）。
/// <para>
/// <b>行側テスト（<see cref="RowCommandsTests"/> の <c>IsRowOccupied_*</c>）と同型に置くが、
/// 要素を置くヘルパーは別に起こした</b>——行側の <c>PlaceElementAt</c> は
/// <c>VerticalConnector</c> を <c>Column = 2</c>、<c>WireBreak</c> を <c>Boundary = 2.5</c> で置いている。
/// <b>行の判定では列座標を一切見ないため任意の値でよかったもの</b>だが、列側では意味を持つ。
/// 既存ヘルパーを書き換えれば行側テストへ波及するため、列側は独立させる。
/// </para>
/// <para>
/// <b>【対称性検証が成り立つ範囲・設計書§6】</b>
/// <c>Elements</c>／<c>Frames</c> は行版・列版とも整数区間同士の判定ゆえ対称性を問えるが、
/// <c>Connectors</c>／<c>WireBreaks</c> は<b>行側がそもそも列方向を見ていない</b>ため、
/// 「行側と対応する結果になるべきだ」という基準自体が存在しない。
/// ゆえにこの2種について対称性テストは書かない（書けば意図が空虚になる）。
/// </para>
/// </summary>
public class ColumnOccupiedTests
{
    private static Sheet NewSheet() => new() { Grid = new GridSpec { Rows = 10, Columns = 20 } };

    /// <summary>列 3 を占める形で各種別を置く。境界を持つ2種は、
    /// もっとも素直に「列内」と判じられる<b>整数境界＝対象列の左端</b>に置く
    /// （境界の作り分けは観点Bで別途測る）。</summary>
    private static void PlaceAtColumn3(Sheet sheet, string elementType)
    {
        switch (elementType)
        {
            case "ElementInstance":
                sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 3) });
                break;
            case "VerticalConnector":
                sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 1, BottomRow = 2 });
                break;
            case "WireBreak":
                sheet.WireBreaks.Add(new WireBreak { Boundary = 3, Row = 1 });
                break;
            case "GroupFrame":
                sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(1, 3), Width = 1, Height = 1 });
                break;
        }
    }

    // ===== 観点A: 同値分割（4種それぞれの「列内／列外」） =====

    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    public void IsColumnOccupied_ReturnsTrue_WhenElementAtColumn(string elementType)
    {
        var sheet = NewSheet();
        PlaceAtColumn3(sheet, elementType);

        Assert.True(MainWindowViewModel.IsColumnOccupied(sheet, 3));
    }

    [Fact]
    public void IsColumnOccupied_ReturnsFalse_WhenColumnEmpty()
    {
        var sheet = NewSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 1) });

        Assert.False(MainWindowViewModel.IsColumnOccupied(sheet, 3));
    }

    /// <summary>
    /// 列座標を持たぬ種別（<c>RungComment</c>／<c>CircuitLine</c>）は判定に影響しない。
    /// <b>行側では <c>RungComment</c> が判定対象である</b>ため、列側で対象外としたことを固定しておく
    /// ——行側の5種をそのまま写す誤りへの網。
    /// </summary>
    [Fact]
    public void IsColumnOccupied_列座標を持たぬ種別は影響せぬ()
    {
        var sheet = NewSheet();
        sheet.RungComments.Add(new RungComment { Row = 1, Text = "注記" });
        sheet.Lines.Add(new CircuitLine { Row = 1, CircuitNumber = 1 });

        Assert.False(MainWindowViewModel.IsColumnOccupied(sheet, 3));
    }

    // ===== 観点B: 境界値分析（Connectors／WireBreaks 固有） =====
    // 設計書§4「ここが本増分でもっとも事故りやすい箇所」。整数境界とセル中央を必ず対にして測る
    // ——実装が誤って片側だけ（floor のみ等）を採っても、X.5 のケースだけでは検出できない。

    /// <summary>縦コネクタが整数境界（b=3）に在るとき、左右両方の列に掛かる。</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsColumnOccupied_Connector_整数境界は両隣の列に掛かる(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 3, TopRow = 1, BottomRow = 2 });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    /// <summary>縦コネクタがセル中央（b=3.5）に在るとき、列3のみに一意に掛かる。</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsColumnOccupied_Connector_セル中央は一列のみに掛かる(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.Connectors.Add(new VerticalConnector { Column = 3.5, TopRow = 1, BottomRow = 2 });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    /// <summary>分断マークが整数境界（b=3）に在るとき、左右両方の列に掛かる。</summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsColumnOccupied_WireBreak_整数境界は両隣の列に掛かる(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.WireBreaks.Add(new WireBreak { Boundary = 3, Row = 1 });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    /// <summary>分断マークがセル中央（b=3.5）に在るとき、列3のみに一意に掛かる。</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsColumnOccupied_WireBreak_セル中央は一列のみに掛かる(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.WireBreaks.Add(new WireBreak { Boundary = 3.5, Row = 1 });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    // ===== 観点C: Elements／Frames の列幅境界（半開区間 [Column, Column+幅)） =====

    /// <summary>幅2の要素を列3へ置くと占有列は {3,4}。5は含まれない（半開区間）。</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void IsColumnOccupied_Element_幅を尊重する(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.Motor,
            Pos = new GridPos(1, 3),
            CellWidth = 2,
        });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    /// <summary>幅3の枠を列3へ置くと占有列は {3,4,5}。6は含まれない（半開区間）。</summary>
    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void IsColumnOccupied_Frame_幅を尊重する(int column, bool expected)
    {
        var sheet = NewSheet();
        sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(1, 3), Width = 3, Height = 1 });

        Assert.Equal(expected, MainWindowViewModel.IsColumnOccupied(sheet, column));
    }

    // ===== 観点D: 対称性点検（Elements／Frames のみ・設計書§6） =====

    /// <summary>
    /// 同一の要素について、行側・列側の判定が対応すること。
    /// <b>対象は <c>Elements</c>／<c>Frames</c> のみ</b>——<c>Connectors</c>／<c>WireBreaks</c> は
    /// 行側が列方向を見ていないため比較の基準が無い（設計書§6）。
    /// </summary>
    [Fact]
    public void IsColumnOccupied_と_IsRowOccupied_は要素の配置で対応する()
    {
        var sheet = NewSheet();
        sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(5, 3) });

        // 置いた行・列では双方 true、置いていない行・列では双方 false。
        Assert.True(MainWindowViewModel.IsRowOccupied(sheet, 5));
        Assert.True(MainWindowViewModel.IsColumnOccupied(sheet, 3));
        Assert.False(MainWindowViewModel.IsRowOccupied(sheet, 7));
        Assert.False(MainWindowViewModel.IsColumnOccupied(sheet, 7));
    }
}
