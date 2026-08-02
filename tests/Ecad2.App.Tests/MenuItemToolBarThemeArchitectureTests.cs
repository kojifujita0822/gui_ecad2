using System.Runtime.CompilerServices;
using System.IO;

namespace Ecad2.App.Tests;

/// <summary>
/// T-140(隠密テスト設計書=docs/ecad2-t140-test-design-onmitsu.md §1): App.xamlに
/// MenuItem.Roleの全4種(TopLevelHeader/TopLevelItem/SubmenuHeader/SubmenuItem)への派生
/// テンプレートと、ToolBarが差し替えるStyleKeyのうちecad2で使用する4種(Button/RadioButton/
/// Separator/ToggleButton)への派生スタイルが定義されていることをソーステキストとして検査する。
/// 層1(定義の存在)のみを測るテストであり、実際にダークモードで正しく描画されるかは層3
/// (忍者の実機画素測定)に委ねる(設計書§0の限界参照)。
/// 検査は「定義行そのもの」の形で照合し、コメント中の語句への偽陽性(設計書§1.5の罠、
/// App.xaml旧694-695行のコメントが"SubmenuHeaderTemplateKey"の語を含んでいた)を避ける。
/// </summary>
public class MenuItemToolBarThemeArchitectureTests
{
    [Theory]
    [InlineData("TopLevelHeaderTemplateKey")]
    [InlineData("TopLevelItemTemplateKey")]
    [InlineData("SubmenuHeaderTemplateKey")]
    [InlineData("SubmenuItemTemplateKey")]
    public void App_xaml_MenuItemの全Roleに派生テンプレートが定義されている(string resourceId)
    {
        var content = File.ReadAllText(GetAppXamlPath());
        var definitionPattern = "ResourceId=" + resourceId + "}\" TargetType=\"{x:Type MenuItem}\">";

        Assert.Contains(definitionPattern, content);
    }

    [Theory]
    [InlineData("ToolBar.ButtonStyleKey")]
    [InlineData("ToolBar.RadioButtonStyleKey")]
    [InlineData("ToolBar.SeparatorStyleKey")]
    [InlineData("ToolBar.ToggleButtonStyleKey")]
    public void App_xaml_ToolBarの必須StyleKeyに派生スタイルが定義されている(string styleKey)
    {
        var content = File.ReadAllText(GetAppXamlPath());
        var definitionPattern = "x:Key=\"{x:Static " + styleKey + "}\"";

        Assert.Contains(definitionPattern, content);
    }

    private static string GetAppXamlPath([CallerFilePath] string thisFilePath = "")
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!; // tests/Ecad2.App.Tests
        var testsDir = Directory.GetParent(testProjectDir)!.FullName; // tests
        var repoRoot = Directory.GetParent(testsDir)!.FullName; // repo root
        return Path.Combine(repoRoot, "src", "Ecad2.App", "App.xaml");
    }
}
