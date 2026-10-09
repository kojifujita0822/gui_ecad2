using System.Reflection;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// V1.0 で入れた二件のテスト——著作権表記（P-191）とリリースノート（P-192）。
/// いずれも殿ご承認2026-09-14、実装は2026-10-09。
/// </summary>
public class ReleaseNotesAndCopyrightTests
{
    /// <summary>殿ご裁可の文言（年は固定の2026、名は FK TEQUNO）がアセンブリへ刻まれておる。</summary>
    [Fact]
    public void ReadCopyright_ReturnsApprovedNotice()
    {
        Assert.Equal("Copyright © 2026 FK TEQUNO", AboutDialog.ReadCopyright());
    }

    /// <summary>バージョン情報ダイアログに、その表記が実際に出る。</summary>
    [Fact]
    public void AboutDialog_ShowsCopyrightLine()
        => StaTestRunner.Run(() =>
        {
            var dialog = new AboutDialog();

            Assert.Equal("Copyright © 2026 FK TEQUNO", dialog.CopyrightText.Text);
            Assert.StartsWith("Ecad2 v", dialog.VersionText.Text);
        });

    [Fact]
    public void LoadReleaseNotes_EmbeddedResourceIsReadable()
    {
        string content = ReleaseNotesWindow.LoadReleaseNotes();

        Assert.StartsWith("# リリースノート", content);
    }

    /// <summary>
    /// <b>今の版の節が載っておること。</b>リリースのたびに <c>docs/release-notes.md</c> へ節を足す運用ゆえ、
    /// 版数だけ上げてノートを書き忘れればここで落ちる（版数は csproj の <c>&lt;Version&gt;</c> 由来）。
    /// </summary>
    [Fact]
    public void LoadReleaseNotes_ContainsSectionForCurrentVersion()
    {
        var version = typeof(ReleaseNotesWindow).Assembly.GetName().Version!;
        string heading = $"## v{version.Major}.{version.Minor}.{version.Build}";

        Assert.Contains(heading, ReleaseNotesWindow.LoadReleaseNotes());
    }

    /// <summary>実際の本文が変換器を例外なく通り、ウィンドウが組み上がる。</summary>
    [Fact]
    public void ReleaseNotesWindow_ConvertsActualContent()
        => StaTestRunner.Run(() =>
        {
            var window = new ReleaseNotesWindow();

            Assert.NotNull(window.ContentViewer.Document);
            Assert.NotEmpty(window.ContentViewer.Document.Blocks);
        });
}
