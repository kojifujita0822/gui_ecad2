using System.Runtime.CompilerServices;
using System.IO;

namespace Ecad2.App.Tests;

/// <summary>
/// T-140系統2・P-158（殿裁可2026-08-02）: `Foreground="Gray"`固定色をOpacity方式へ改めた回帰テスト。
/// 設計書=docs/ecad2-t140-keitou2-test-design-onmitsu.md §4.1。
/// 【重要】素朴な"Gray"検索は偽陽性を7件生む(SystemColors.GrayTextBrushKey 6件・コメント中の
/// ファイル名1件、設計書§2.1)。`Foreground="Gray"`という属性の形で照合する。
/// 【範囲の但し書き】設計書は3件（プレースホルダ・画像パス・部品カテゴリ）を前提に書かれたが、
/// 3件目（部品カテゴリ、MainWindow.xaml:1615）は選択行のコントラスト不適合が別途見つかり
/// 保留（家老采配2026-08-02）。ゆえに観点7の期待値は0件ではなく1件（3件目のみ残る）。
/// </summary>
public class MainWindowGrayForegroundTests
{
    [Fact]
    public void MainWindow_xaml_Foreground等Grayは部品カテゴリの1件のみ残る()
    {
        var content = File.ReadAllText(GetMainWindowXamlPath());
        var count = CountOccurrences(content, "Foreground=\"Gray\"");

        Assert.Equal(1, count);
    }

    /// <summary>
    /// 観点8＝巻き込み防止の網（設計書§4.1）。「Grayを消す」作業が正当なコード
    /// （SystemColors.GrayTextBrushKey、無効状態のグレー文字表現）まで巻き込んでいないことの対照。
    /// これが無ければ「全部消した」と「正しく2件だけ消した」を区別できない。
    /// </summary>
    [Fact]
    public void App_xamlとMainWindow_xamlのGrayTextBrushKeyは6件のまま残る()
    {
        var appContent = File.ReadAllText(GetAppXamlPath());
        var mainWindowContent = File.ReadAllText(GetMainWindowXamlPath());
        var count = CountOccurrences(appContent, "SystemColors.GrayTextBrushKey")
            + CountOccurrences(mainWindowContent, "SystemColors.GrayTextBrushKey");

        Assert.Equal(6, count);
    }

    private static int CountOccurrences(string content, string pattern)
    {
        int count = 0, index = 0;
        while ((index = content.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private static string GetMainWindowXamlPath([CallerFilePath] string thisFilePath = "")
        => Path.Combine(GetAppDirectory(thisFilePath), "MainWindow.xaml");

    private static string GetAppXamlPath([CallerFilePath] string thisFilePath = "")
        => Path.Combine(GetAppDirectory(thisFilePath), "App.xaml");

    private static string GetAppDirectory(string thisFilePath)
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!; // tests/Ecad2.App.Tests
        var testsDir = Directory.GetParent(testProjectDir)!.FullName; // tests
        var repoRoot = Directory.GetParent(testsDir)!.FullName; // repo root
        return Path.Combine(repoRoot, "src", "Ecad2.App");
    }
}
