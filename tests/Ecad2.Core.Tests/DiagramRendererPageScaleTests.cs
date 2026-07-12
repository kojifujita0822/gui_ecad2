using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>T-080 DoD(6)(殿裁定=縮小フィット): DiagramRenderer.CalcPageScaleの検証。
/// グリッド全幅+行コメント域(右母線右側)が用紙の印字可能幅を超える場合のみ縮小し、
/// 超えない場合は等倍(1.0)のまま(不要な縮小をかけない)。</summary>
public class DiagramRendererPageScaleTests
{
    private static Sheet CreateSheet(int columns = 20) => new() { Grid = new GridSpec { Rows = 10, Columns = columns } };

    [Fact]
    public void CalcPageScale_行コメント無し_縮小しない()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }

    [Fact]
    public void CalcPageScale_1文字の行コメント_用紙内に収まれば縮小しない()
    {
        // Columns=20(既定シート幅)ではRightBusX≈204.5mmでA4(210mm)まで余裕約5.5mmしかないため、
        // 「短い」に相当するのは1文字程度(2.0mmオフセット+3.3mm=5.3mm)まで。
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();
        sheet.RungComments.Add(new RungComment { Row = 0, Text = "1" });

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }

    [Fact]
    public void CalcPageScale_20文字の行コメント_用紙幅を超えるため縮小する()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();
        sheet.RungComments.Add(new RungComment { Row = 0, Text = new string('あ', 20) });

        double scale = dr.CalcPageScale(sheet);

        Assert.True(scale < 1.0);
        Assert.True(scale > 0.0);
    }

    [Fact]
    public void CalcPageScale_縮小率の上限は等倍_拡大はしない()
    {
        var dr = new DiagramRenderer();
        // Columns=1のように必要幅が用紙幅より十分小さいケースでも1.0を超えて拡大しない。
        var sheet = CreateSheet(columns: 1);

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }
}
