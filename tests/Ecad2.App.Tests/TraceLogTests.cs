using System.Reflection;
using Ecad2.App.Diagnostics;

namespace Ecad2.App.Tests;

/// <summary>
/// T-050(P-014): 環境変数判定の全角正規化ロジックの回帰テスト。TraceLog.Initialize()自体は
/// プロセス一回限りの静的ガード(_initialized)と実環境変数に依存し繰り返し可能な単体テストに
/// 適さないため、正規化の純粋関数(NormalizeFullWidth)を対象にする(MainWindowViewModelTestsの
/// MapToDeviceClassと同型のリフレクション経由アクセス)。
/// </summary>
public class TraceLogTests
{
    private static string Invoke(string value)
    {
        var method = typeof(TraceLog).GetMethod("NormalizeFullWidth", BindingFlags.NonPublic | BindingFlags.Static);
        return (string)method!.Invoke(null, new object[] { value })!;
    }

    [Theory]
    [InlineData("ｆａｌｓｅ", "false")]
    [InlineData("ｏｆｆ", "off")]
    [InlineData("ｎｏ", "no")]
    [InlineData("ＦＡＬＳＥ", "FALSE")]
    [InlineData("０", "0")]
    [InlineData("１", "1")]
    [InlineData("false", "false")]
    [InlineData("", "")]
    public void NormalizeFullWidth_ConvertsFullWidthLatinAndDigitsToHalfWidth(string input, string expected)
    {
        Assert.Equal(expected, Invoke(input));
    }
}
