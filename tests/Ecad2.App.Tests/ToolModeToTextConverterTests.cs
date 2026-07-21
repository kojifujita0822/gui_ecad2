using Ecad2.App.Converters;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>
/// T-109(ステータスバー「ツール: {0}」表示の日本語化、殿裁定=訳語案B)の回帰テスト。
/// 内部実装(ToolMode enum自体)は変更しない、表示専用の変換ロジックを検証する。
/// </summary>
public class ToolModeToTextConverterTests
{
    private readonly ToolModeToTextConverter _converter = new();

    [Theory]
    [InlineData(ToolMode.Select, "選択")]
    [InlineData(ToolMode.PlaceElement, "要素配置")]
    [InlineData(ToolMode.PlaceConnector, "縦コネクタ記入")]
    [InlineData(ToolMode.PlaceFrame, "グループ枠記入")]
    [InlineData(ToolMode.PlaceLine, "自由線記入")]
    [InlineData(ToolMode.PlaceDot, "接続点記入")]
    [InlineData(ToolMode.PlaceWireBreak, "配線分断記入")]
    [InlineData(ToolMode.PlaceImage, "画像配置")]
    public void Convert_ToolModeの全8値を確定訳語へ変換する(ToolMode mode, string expected)
    {
        object result = _converter.Convert(mode, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_未対応のため例外を投げる()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("選択", typeof(ToolMode), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
