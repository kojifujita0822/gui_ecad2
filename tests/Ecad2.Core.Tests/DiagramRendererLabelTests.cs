using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-071バグ修正(隠密テスト設計 docs/ecad2-t071-bugfix-test-design-onmitsu.md 表2)の回帰テスト。
/// タイマ限時/瞬時接点のミニラベル(「限」/「瞬」)・ランプ色ラベルが、PartId経由(BasicPartTemplates)で
/// 配置された組込みパーツに対しても正しく描画されることを検証する。DrawElementLabel/DrawElement内の
/// e.Kind直接比較(常に既定値ContactNOのまま)ではなく、PartResolver.ComponentKind経由の解決が
/// 必要であることの回帰防止(要修正2、951行目・890行目)。
/// </summary>
public class DiagramRendererLabelTests
{
    private static PartLibrary CreateLibrary()
    {
        var lib = new PartLibrary();
        foreach (var def in BasicPartTemplates.All())
            lib.ById[def.Id] = def;
        return lib;
    }

    private static RecordingRenderer RenderSingleElement(string partId, string deviceName, string? lampColor = null)
    {
        var elem = new ElementInstance { PartId = partId, Pos = new GridPos(0, 0), DeviceName = deviceName };
        if (lampColor is not null) elem.Params[ParamKeys.LampColor] = lampColor;
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(elem);

        var renderer = new RecordingRenderer();
        new DiagramRenderer().Render(renderer, sheet, CreateLibrary());
        return renderer;
    }

    [Theory]
    [InlineData(BasicPartTemplates.TimerContactNOId, "限")]
    [InlineData(BasicPartTemplates.TimerContactNCId, "限")]
    [InlineData(BasicPartTemplates.TimerInstantContactNOId, "瞬")]
    [InlineData(BasicPartTemplates.TimerInstantContactNCId, "瞬")]
    [InlineData(BasicPartTemplates.ContactNOId, null)]
    [InlineData(BasicPartTemplates.CoilId, null)]
    public void Render_TimerContactPart_DrawsCorrectMiniLabel(string partId, string? expectedMark)
    {
        var renderer = RenderSingleElement(partId, "X001");

        if (expectedMark is null)
            Assert.DoesNotContain(renderer.DrawnTexts, t => t == "限" || t == "瞬");
        else
            Assert.Contains(expectedMark, renderer.DrawnTexts);
    }

    [Fact]
    public void Render_LampPartWithColor_DrawsLampColorLabel()
    {
        var renderer = RenderSingleElement(BasicPartTemplates.LampId, "L001", lampColor: "赤");

        Assert.Contains("赤", renderer.DrawnTexts);
    }

    /// <summary>
    /// T-097差し戻し1周目(隠密指摘、家老裏付け済み)の回帰テスト。DrawElementLabel(1118行)の
    /// LabelDy既定値フォールバックが生のe.Kindを直接参照しており、PartId経由配置のCoil要素
    /// (e.Kind=既定値ContactNOのまま、T-071由来の既知の制約)ではDefaultLabelDy(ContactNO)=-1.5が
    /// 誤って使われ、正しいDefaultLabelDy(Coil)=-5.5との間に4mmのズレが生じていた。PartId経由配置の
    /// Coilと、直接ElementKind.Coilを指定した要素(PartId無し、resolvedKind解決の影響を受けない
    /// 対照群)を同じRowに配置し、LabelDy未設定時の機器名ラベルY座標が一致することを検証する。
    /// </summary>
    [Fact]
    public void Render_PartIdPlacedCoilWithoutLabelDy_UsesCoilDefaultLabelDy_MatchingDirectKindElement()
    {
        var partIdElem = new ElementInstance { PartId = BasicPartTemplates.CoilId, Pos = new GridPos(0, 0), DeviceName = "Y001" };
        var directKindElem = new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 5), DeviceName = "Y002" };
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(partIdElem);
        sheet.Elements.Add(directKindElem);

        var renderer = new RecordingRenderer();
        new DiagramRenderer().Render(renderer, sheet, CreateLibrary());

        double y001 = renderer.DrawnTextEntries.Single(t => t.Text == "Y001").Position.Y;
        double y002 = renderer.DrawnTextEntries.Single(t => t.Text == "Y002").Position.Y;
        Assert.Equal(y002, y001, precision: 6);
    }

    /// <summary>T-107 DoD(1)(2): Element.Commentが設定されていれば緑色(DrawingTheme.Comment)で
    /// 描画され、未設定(null/空文字)なら描画されないことを検証する。</summary>
    [Theory]
    [InlineData("負荷側過電流保護", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Render_ElementComment_DrawsOnlyWhenPresent(string? comment, bool expectDrawn)
    {
        var elem = new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 0), DeviceName = "X001", Comment = comment };
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(elem);

        var renderer = new RecordingRenderer();
        new DiagramRenderer().Render(renderer, sheet, CreateLibrary());

        if (expectDrawn)
        {
            var entry = renderer.DrawnTextEntries.Single(t => t.Text == comment);
            Assert.Equal(DrawingTheme.Comment, entry.Style.Color);
        }
        else
        {
            Assert.DoesNotContain(renderer.DrawnTextEntries, t => t.Text == comment);
        }
    }

    /// <summary>T-107 DoD(1): コメントは機器名(記号の上)と対称に記号の下へ描画される
    /// (同一セル内で行を専有しない、上下2分割構造)。機器名ラベルのY座標より下(Y値が大きい)
    /// になることを検証する。</summary>
    [Fact]
    public void Render_ElementWithCommentAndDeviceName_CommentIsBelowDeviceName()
    {
        var elem = new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 0), DeviceName = "X001", Comment = "手動リセット" };
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(elem);

        var renderer = new RecordingRenderer();
        new DiagramRenderer().Render(renderer, sheet, CreateLibrary());

        double deviceNameY = renderer.DrawnTextEntries.Single(t => t.Text == "X001").Position.Y;
        double commentY = renderer.DrawnTextEntries.Single(t => t.Text == "手動リセット").Position.Y;
        Assert.True(commentY > deviceNameY, $"comment Y({commentY}) should be below device name Y({deviceNameY})");
    }
}

/// <summary>DrawTextの呼び出し引数(text)のみを記録するIRendererのテストダブル。他の描画命令は
/// no-op(アサーション対象外、隠密テスト設計 表2 検証方法(a)を採用)。</summary>
internal sealed class RecordingRenderer : IRenderer
{
    public List<string> DrawnTexts { get; } = new();
    public List<(string Text, Point2D Position, TextStyle Style)> DrawnTextEntries { get; } = new();

    public void PushTransform(double translateX, double translateY, double scale = 1.0) { }
    public void PopTransform() { }
    public void PushClip(Rect2D rect) { }
    public void PopClip() { }
    public void DrawLine(Point2D a, Point2D b, StrokeStyle stroke) { }
    public void DrawPolyline(ReadOnlySpan<Point2D> points, StrokeStyle stroke) { }
    public void DrawRectangle(Rect2D rect, StrokeStyle stroke) { }
    public void FillRectangle(Rect2D rect, Color color) { }
    public void DrawCircle(Point2D center, double radius, StrokeStyle stroke) { }
    public void FillCircle(Point2D center, double radius, Color color) { }
    public void DrawEllipse(Point2D center, double radiusX, double radiusY, StrokeStyle stroke) { }
    public void DrawArc(Point2D center, double radius, double startDeg, double sweepDeg, StrokeStyle stroke) { }
    public void DrawText(string text, Point2D position, TextStyle style)
    {
        DrawnTexts.Add(text);
        DrawnTextEntries.Add((text, position, style));
    }
    public Size2D MeasureText(string text, TextStyle style) => new(0, 0);
    public void DrawImage(string filePath, Rect2D bounds) { }
}
