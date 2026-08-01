using System.IO;
using System.Runtime.CompilerServices;

namespace Ecad2.App.Tests;

/// <summary>
/// T-136(A)経路2（殿裁可2026-08-01）：部品を選べる入口は<b>2つ</b>ある——右パネルのパレット
/// (<c>PartSelectionList</c>) と、配置バーのコンボ (<c>PlacementPartComboBox</c>)。
/// いずれも同じ <c>PartPaletteViewModel.SelectionEntries</c> を並べるゆえ、片方だけを
/// <c>IsPlaceable</c> で塞ぐと非対称が残る。増分2の時点で実際に残っており、忍者の実機実測で
/// 「同じシートで主回路専用の部品が、パレットでは灰・コンボでは選択可」と数に出た。
///
/// <para><b>【なぜソースを読む形にしたか】</b>両入口の無効化はいずれもXAMLの
/// <c>ItemContainerStyle</c> による宣言であり、ViewModel層には現れぬ（配置可否の値そのものは
/// <c>PartPaletteViewModelTests</c> が受け持つ）。<see cref="DispatcherDependencyArchitectureTests"/>
/// と同じソーススキャン型を採る。</para>
///
/// <para><b>【集約で測らぬ】</b>XAML全体に <c>IsPlaceable</c> が「何個あるか」を数える形では、
/// 一方に2つ入って他方が空でも通ってしまう。<b>入口ごとの宣言ブロックを切り出し、その中に
/// 在ること</b>を測る（<c>samurai.md</c>「本数・件数を測るテストは位置の誤りを検出せぬ」）。</para>
/// </summary>
public class T136PlacementEntryPointsTests
{
    [Theory]
    [InlineData("ListBox", "PartSelectionList")]        // 右パネルのパレット（増分2で対処済み）
    [InlineData("ComboBox", "PlacementPartComboBox")]   // 配置バーのコンボ（経路2、本増分）
    public void 部品を選べる入口はいずれもIsPlaceableで項目を無効化する(string tagName, string controlName)
    {
        var block = ExtractElementBlock(ReadMainWindowXaml(), tagName, controlName);

        var setterLine = block.Split('\n').FirstOrDefault(line => line.Contains("IsPlaceable"));

        Assert.True(setterLine is not null,
            $"{controlName} の宣言に IsPlaceable のバインドが無い（置けぬ部品を選ばせてしまう）");
        Assert.Contains("IsEnabled", setterLine!);
    }

    /// <summary>
    /// 対照。上のテストが効くのは、切り出しが<b>本当にその入口だけ</b>を取り出しておる時に限る。
    /// 切り出しが崩れて XAML 全文を返していれば、片方の宣言が空でも両ケースが通ってしまう。
    /// 互いの入口に固有の文字列が、相手のブロックへ混ざっておらぬことで健全性を押さえる。
    /// </summary>
    [Fact]
    public void 切り出しは入口ごとに分かれている()
    {
        var xaml = ReadMainWindowXaml();

        var list = ExtractElementBlock(xaml, "ListBox", "PartSelectionList");
        var combo = ExtractElementBlock(xaml, "ComboBox", "PlacementPartComboBox");

        Assert.Contains("PartSelectionItem_Clicked", list);      // パレット固有のEventSetter
        Assert.DoesNotContain("PartSelectionItem_Clicked", combo);
        Assert.Contains("ComboBoxItem", combo);                  // コンボ固有のTargetType
        Assert.DoesNotContain("ComboBoxItem", list);
    }

    /// <summary>指定した名前付きコントロールの開始タグから対応する終了タグまでを切り出す。
    /// 両入口とも同種要素の入れ子を持たぬゆえ、最初の終了タグまでで足りる。</summary>
    private static string ExtractElementBlock(string xaml, string tagName, string controlName)
    {
        int start = xaml.IndexOf($"<{tagName} x:Name=\"{controlName}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{controlName} の宣言が MainWindow.xaml に見つからぬ");

        int end = xaml.IndexOf($"</{tagName}>", start, StringComparison.Ordinal);
        Assert.True(end > start, $"{controlName} の終了タグ </{tagName}> が見つからぬ");

        return xaml.Substring(start, end - start);
    }

    private static string ReadMainWindowXaml([CallerFilePath] string thisFilePath = "")
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!;          // tests/Ecad2.App.Tests
        var testsDir = Directory.GetParent(testProjectDir)!.FullName;       // tests
        var repoRoot = Directory.GetParent(testsDir)!.FullName;             // repo root
        return File.ReadAllText(Path.Combine(repoRoot, "src", "Ecad2.App", "MainWindow.xaml"));
    }
}
