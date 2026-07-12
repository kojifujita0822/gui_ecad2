using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>T-080 DoD(6)(殿裁定=縮小フィット): DiagramRenderer.CalcPageScaleの検証。
/// ページ内容(グリッド全幅+行コメント域、主回路シートはmm実座標内容も含む)が図面枠(外枠線、
/// 用紙端からBorderMarginMm内側=A4縦で205mm)の幅を超える場合のみ縮小し、収まる場合は等倍(1.0)の
/// まま(不要な縮小をかけない)。判定基準が用紙物理端(210mm)でなく図面枠であること・縮小率の
/// 導出式(固定点=図面枠左上)はT-080往復1周目指摘A/B/Dの回帰テスト。</summary>
public class DiagramRendererPageScaleTests
{
    private static Sheet CreateSheet(int columns = 20) => new() { Grid = new GridSpec { Rows = 10, Columns = columns } };

    // A4縦の図面枠右端(mm)=用紙物理端210mmから余白5mm内側。実装定数(BorderMarginMm)から
    // 再計算せず意図的にリテラルで固定する(実装に合わせたテストにしない=仕様値のピン留め)。
    private const double PrintableRightMm = 205.0;

    [Fact]
    public void CalcPageScale_行コメント無し_縮小しない()
    {
        // 既定シート(Columns=20)のRightBusX=204.5mmは図面枠右端205mm以内。
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }

    [Fact]
    public void CalcPageScale_図面枠内に収まる行コメント_縮小しない()
    {
        // Columns=18ならRightBusX=186.5mm、1文字コメント(+2.0+3.3mm)=191.8mmで図面枠(205mm)に収まる。
        var dr = new DiagramRenderer();
        var sheet = CreateSheet(columns: 18);
        sheet.RungComments.Add(new RungComment { Row = 0, Text = "1" });

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }

    /// <summary>T-080往復1周目指摘Aの回帰テスト: 修正前は判定基準が用紙物理端(210mm)だったため、
    /// 必要幅209.8mm(既定シート+1文字コメント)が「縮小不要」と誤判定され、コメントが図面枠(205mm)の
    /// 外側に描かれていた。図面枠基準では縮小が発動する。</summary>
    [Fact]
    public void CalcPageScale_図面枠は超えるが用紙内に収まる行コメント_縮小する()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();   // RightBusX=204.5mm
        sheet.RungComments.Add(new RungComment { Row = 0, Text = "1" });   // 必要幅209.8mm

        double scale = dr.CalcPageScale(sheet);

        Assert.True(scale < 1.0);
        Assert.True(scale > 0.0);
    }

    /// <summary>T-080往復1周目指摘A/Bの回帰テスト: 縮小率は「図面枠の幅 / (必要幅 - BorderMarginMm)」
    /// (縮小の固定点=図面枠左上)。この率でRender側の変換式 x' = BorderMarginMm*(1-scale) + scale*x を
    /// 適用すると、縮小後の内容右端が図面枠右端(205mm)ちょうどに一致する(修正前のPageW/neededWidth
    /// では縮小後の右端が用紙物理端(210mm)となり、図面枠内へ構造的に戻らなかった)。</summary>
    [Fact]
    public void CalcPageScale_縮小後の内容右端が図面枠右端に一致する()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet();
        sheet.RungComments.Add(new RungComment { Row = 0, Text = new string('あ', 20) });
        // 必要幅 = RightBusX(204.5) + 2.0 + 20文字*3.3 = 272.5mm
        const double neededWidth = 272.5;

        double scale = dr.CalcPageScale(sheet);

        // 図面枠の幅(210-2*5=200mm) / (必要幅 - 図面枠余白5mm)。仕様値のピン留めのためリテラルで書く。
        double expected = 200.0 / (neededWidth - 5.0);
        Assert.Equal(expected, scale, 12);
        double scaledRight = 5.0 * (1 - scale) + scale * neededWidth;
        Assert.Equal(PrintableRightMm, scaledRight, 9);
    }

    [Fact]
    public void CalcPageScale_縮小率の上限は等倍_拡大はしない()
    {
        var dr = new DiagramRenderer();
        // Columns=1のように必要幅が図面枠幅より十分小さいケースでも1.0を超えて拡大しない。
        var sheet = CreateSheet(columns: 1);

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }

    /// <summary>T-080往復1周目指摘Dの回帰テスト(自由直線): 主回路シートは右母線が描画されず
    /// グリッド列数による右端の縛りが元々無いため、FreeLine等のmm実座標が図面枠を超えるなら
    /// 縮小する(修正前はグリッド幅+行コメントしか見ておらず、主回路シートで縮小フィットが
    /// 実質機能していなかった)。</summary>
    [Fact]
    public void CalcPageScale_主回路シートの自由直線が図面枠を超える_縮小する()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet(columns: 10);
        sheet.MainCircuit = true;
        sheet.FreeLines.Add(new FreeLine { X1Mm = 20, Y1Mm = 30, X2Mm = 250, Y2Mm = 30 });

        double scale = dr.CalcPageScale(sheet);

        Assert.True(scale < 1.0);
        // 固定点=図面枠左上(5mm)の変換式で、縮小後の内容右端(250mm)が図面枠右端(205mm)に一致する。
        double scaledRight = 5.0 * (1 - scale) + scale * 250.0;
        Assert.Equal(PrintableRightMm, scaledRight, 9);
    }

    /// <summary>同上(グルーピング枠、mm実座標のVisual*Mm)。</summary>
    [Fact]
    public void CalcPageScale_主回路シートの枠が図面枠を超える_縮小する()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet(columns: 10);
        sheet.MainCircuit = true;
        sheet.Frames.Add(new GroupFrame
        {
            TopLeft = new GridPos(0, 0), Width = 2, Height = 2,
            VisualXMm = 200, VisualYMm = 30, VisualWidthMm = 60, VisualHeightMm = 20,
        });

        Assert.True(dr.CalcPageScale(sheet) < 1.0);
    }

    [Fact]
    public void CalcPageScale_主回路シートでも内容が図面枠内なら縮小しない()
    {
        var dr = new DiagramRenderer();
        var sheet = CreateSheet(columns: 10);
        sheet.MainCircuit = true;
        sheet.FreeLines.Add(new FreeLine { X1Mm = 20, Y1Mm = 30, X2Mm = 100, Y2Mm = 30 });

        Assert.Equal(1.0, dr.CalcPageScale(sheet));
    }
}
