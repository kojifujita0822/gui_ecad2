using Ecad2.App.Converters;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>
/// T-111(ステータスバー「モード: {0}」表示の日本語化)の回帰テスト。
/// 内部実装(AppMode enum自体)は変更しない、表示専用の変換ロジックを検証する。
/// </summary>
public class AppModeToTextConverterTests
{
    private readonly AppModeToTextConverter _converter = new();

    [Theory]
    [InlineData(AppMode.Drawing, "作画")]
    [InlineData(AppMode.Test, "テスト")]
    public void Convert_AppModeの全2値を確定訳語へ変換する(AppMode mode, string expected)
    {
        object result = _converter.Convert(mode, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertBack_未対応のため例外を投げる()
    {
        Assert.Throws<NotSupportedException>(() =>
            _converter.ConvertBack("作画", typeof(AppMode), null, System.Globalization.CultureInfo.InvariantCulture));
    }
}
