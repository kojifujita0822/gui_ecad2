using System.Runtime.CompilerServices;
using System.IO;

namespace Ecad2.App.Tests;

/// <summary>
/// T-140系統2 追い直し（殿裁可2026-08-02＝案3＋方向A）: 部品選択リストの「選択時Light不適合」への回帰テスト。
/// 設計＝<c>docs/ecad2-t140-partlist-selected-light-contrast-proposal-onmitsu.md</c> §6。
/// <para>
/// <b>経緯</b>：案W（固定灰を消すのみ）では選択時Lightが実測3.89〜4.28:1で不適合。
/// 選択背景のLight値は文字を純白にしても理論上限がちょうど4.50:1にて、<b>文字色側では構造的に届かぬ</b>
/// （隠密が理論値を独立に再現）。ゆえに背景側へ踏み込む。
/// </para>
/// <para>
/// <b>採った形＝案3（部品リスト限定）＋方向A（ダークで使うておる値をライトへ転用）。</b>
/// 部品リストの <c>Resources</c> で選択背景のブラシを局所的に上書きする。
/// </para>
/// <para>
/// <b>なぜ <c>ItemContainerStyle</c> の <c>Setter</c> ではないか</b>（隠密の起草からの変更点）：
/// 選択時の背景は <c>App.xaml</c> の <c>ListBoxItem</c> の <c>ControlTemplate.Triggers</c> が
/// <c>TargetName</c> 指定で内側の <c>Border</c> へ直接当てておる。外側の <c>Style</c> から
/// <c>ListBoxItem</c> の背景を変えても、その値は <c>TemplateBinding</c> 経由でしか内側へ届かず、
/// テンプレートのトリガーに上書きされる——<b>値は正しいが描画に反映されぬ</b>型である
/// （<c>samurai.md</c>「WPF『値は正しいが描画に反映されない』系の調査」）。
/// トリガーが参照しておるのは動的リソースゆえ、<b>そのリソースを部品リストの範囲だけで
/// 差し替えるのが、テンプレートを複製せずに射程を閉じ込められる唯一の道</b>である。
/// </para>
/// <para>
/// <b>【理論値である。確定は忍者の実測を待つ】</b>本案の想定コントラスト比は
/// 選択時Light・Darkとも理論6.40:1（白文字）。<b>本件はまさに理論4.50を実測3.89が下回った案件</b>ゆえ、
/// 本テスト群が保証するのは「意図した色が、意図した射程に入っておること」までである。
/// </para>
/// </summary>
public class PartSelectionListSelectedContrastTests
{
    /// <summary>部品リストへ局所的に置いた選択背景の上書き（方向A＝ダーク側と同値）。</summary>
    private const string LocalOverride =
        "<SolidColorBrush x:Key=\"PanelContentSelectedBackgroundBrush\" Color=\"#FF0E639C\"/>";

    /// <summary>ライトのテーマ定義。案4（全体の選択色を変える）を退けた証として、不変であることを測る。</summary>
    private const string LightThemeDefinition =
        "<SolidColorBrush x:Key=\"PanelContentSelectedBackgroundBrush\" Color=\"#FF0078D7\"/>";

    /// <summary>
    /// 観点1＝部品リストに選択背景の局所上書きが在ること。
    /// これが消えれば選択時Lightは元の不適合値へ戻る。
    /// </summary>
    [Fact]
    public void MainWindow_xaml_部品リストに選択背景の局所上書きが1件ある()
    {
        var content = File.ReadAllText(GetAppFilePath("MainWindow.xaml"));

        Assert.Equal(1, CountOccurrences(content, LocalOverride));
    }

    /// <summary>
    /// 観点2＝<b>射程が閉じておること</b>（本案の眼目）。
    /// ライトのテーマ定義そのものは変えておらぬ——変えれば <c>UsageWindow</c> のトピック一覧・
    /// <c>DataGrid</c> の行／セル・<c>ComboBox</c> の項目まで巻き込む（案4を殿が退けられた理由）。
    /// 観点1だけでは「局所に足した」と「全体を変えたうえに局所にも足した」を区別できぬ。
    /// </summary>
    [Fact]
    public void Theme_Light_xaml_全体の選択背景は変えておらぬ()
    {
        var content = File.ReadAllText(GetAppFilePath(Path.Combine("Themes", "Theme.Light.xaml")));

        Assert.Equal(1, CountOccurrences(content, LightThemeDefinition));
    }

    /// <summary>
    /// 観点3＝対照。無関係な画面へ同じ上書きを撒いておらぬこと。
    /// 「部品リストだけ」という射程を、足した側からも確かめる。
    /// </summary>
    [Fact]
    public void UsageWindow_xaml_同じ上書きを持たぬ()
    {
        var content = File.ReadAllText(GetAppFilePath(Path.Combine("Views", "UsageWindow.xaml")));

        Assert.Equal(0, CountOccurrences(content, LocalOverride));
    }

    /// <summary>
    /// 観点4＝ダークは据え置き。方向Aは「ライトがダークへ歩み寄る」形にて、
    /// ダーク側の値を動かした結果ではない。適合済みのものへ手を入れておらぬことを固定する。
    /// </summary>
    [Fact]
    public void Theme_Dark_xaml_選択背景は据え置き()
    {
        var content = File.ReadAllText(GetAppFilePath(Path.Combine("Themes", "Theme.Dark.xaml")));

        Assert.Equal(1, CountOccurrences(content, LocalOverride));
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

    private static string GetAppFilePath(string relativePath, [CallerFilePath] string thisFilePath = "")
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!;              // tests/Ecad2.App.Tests
        var repoRoot = Directory.GetParent(Directory.GetParent(testProjectDir)!.FullName)!.FullName;
        return Path.Combine(repoRoot, "src", "Ecad2.App", relativePath);
    }
}
