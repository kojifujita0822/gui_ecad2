using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

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
    /// <summary>
    /// <b>【偽陰性経路を塞いだ経緯 2026-08-01】</b>初版は「<c>IsPlaceable</c> を含む行を1つ拾い、
    /// そこに <c>IsEnabled</c> があるか」を見ておった。<b>隠密の静的レビューがその穴を捕らえた</b>——
    /// パレット側のコメント（<c>MainWindow.xaml:1616-1619</c>）は既に <c>IsEnabled</c> の語を含んでおり、
    /// <b>そこへ <c>IsPlaceable</c> の語が一つ加わるだけで、Setter本体を消してもGREENのまま</b>通る。
    /// しかもそのコメントは両者の関係を説く箇所そのものにて、<b>次に触る者のごく自然な筆で失われる。</b>
    /// <para>
    /// 手当ては二重。(1)<b>コメントを取り除いてから探す</b>——コメントは実装ではないゆえ、
    /// そこに何が書かれておっても判定に混ざってはならぬ。(2)<b><c>Property="IsEnabled"</c> と
    /// <c>{Binding IsPlaceable}</c> の対応を1行で照合する</b>（家老裁定＝隠密の案b）——
    /// これにより<b>取り違え</b>（片方の入口へ他方のバインドを書く）も同時に塞がる。
    /// </para>
    /// <para><b>【ソーススキャン型に固有の性質】</b>RED証明は<b>測った時点の本文にしか効かぬ</b>。
    /// 改変Bが当時REDになったのは「その時コメントに語が無かった」だけであり、実装の堅さの証では
    /// なかった（隠密の指摘）。<b>本文が変われば同じ証明が同じ結論を与えるとは限らぬ。</b></para>
    /// </summary>
    [Theory]
    [InlineData("ListBox", "PartSelectionList")]        // 右パネルのパレット（増分2で対処済み）
    [InlineData("ComboBox", "PlacementPartComboBox")]   // 配置バーのコンボ（経路2、本増分）
    public void 部品を選べる入口はいずれもIsPlaceableで項目を無効化する(string tagName, string controlName)
    {
        var block = StripXmlComments(ExtractElementBlock(ReadMainWindowXaml(), tagName, controlName));

        var setter = block.Split('\n').FirstOrDefault(line =>
            line.Contains("Property=\"IsEnabled\"") && line.Contains("{Binding IsPlaceable}"));

        Assert.True(setter is not null,
            $"{controlName} の宣言に IsEnabled を IsPlaceable へ結ぶ Setter が無い（置けぬ部品を選ばせてしまう）");
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

    /// <summary>XMLコメントを取り除く。<b>コメントは実装ではない</b>——そこに書かれた語が
    /// 判定に混ざれば、実装を消してもテストが通る偽陰性を生む（隠密の静的レビュー2026-08-01）。</summary>
    private static string StripXmlComments(string xaml)
        => Regex.Replace(xaml, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

    private static string ReadMainWindowXaml([CallerFilePath] string thisFilePath = "")
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!;          // tests/Ecad2.App.Tests
        var testsDir = Directory.GetParent(testProjectDir)!.FullName;       // tests
        var repoRoot = Directory.GetParent(testsDir)!.FullName;             // repo root
        return File.ReadAllText(Path.Combine(repoRoot, "src", "Ecad2.App", "MainWindow.xaml"));
    }
}
