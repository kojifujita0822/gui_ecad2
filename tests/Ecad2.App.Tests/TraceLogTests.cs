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

    /// <summary>
    /// T-050修正(隠密指摘1): string.Normalize(FormKC)はUTF-16の不対サロゲートを含む不正な文字列で
    /// ArgumentExceptionを投げる(旧char単位ループは投げなかった)。TraceLogのベストエフォート原則
    /// (機構の失敗が本来の起動処理を道連れにしない)に沿い、例外を投げず原文を返すことを検証する。
    /// 環境変数への不対サロゲート混入という異常入力でも起動が失敗しないことを保証する回帰テスト。
    ///
    /// 不対サロゲートはInlineDataに文字列リテラルで直接渡してはならない: xUnitのデータシリアライズが
    /// 不対サロゲートをU+FFFD(置換文字)へ変換するため、テスト到達時には正常文字列となり検証が
    /// 骨抜きになる(実測で判明)。コードポイント(int)と位置(string)を渡し、本メソッド内で
    /// 不対サロゲートを組み立てることで、真に不正なUTF-16文字列をNormalizeへ与える。
    /// </summary>
    [Theory]
    [InlineData(0xD800, "alone")]   // 単独high surrogate(下限)
    [InlineData(0xDBFF, "alone")]   // 単独high surrogate(上限)
    [InlineData(0xDC00, "alone")]   // 単独low surrogate(下限)
    [InlineData(0xDFFF, "alone")]   // 単独low surrogate(上限)
    [InlineData(0xD800, "suffix")]  // 正常値の末尾に不対サロゲート混在("false"+surrogate)
    [InlineData(0xD800, "prefix")]  // 正常値の先頭に不対サロゲート混在(surrogate+"false")
    public void NormalizeFullWidth_DoesNotThrow_OnIllFormedSurrogates(int surrogateCodePoint, string position)
    {
        char surrogate = (char)surrogateCodePoint;
        string input = position switch
        {
            "suffix" => "false" + surrogate,
            "prefix" => surrogate + "false",
            _ => surrogate.ToString(),
        };

        var exception = Record.Exception(() => Invoke(input));

        Assert.Null(exception);
    }
}
