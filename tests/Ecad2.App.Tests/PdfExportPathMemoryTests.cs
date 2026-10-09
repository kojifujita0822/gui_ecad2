using System.IO;
using Ecad2.App.Views;

namespace Ecad2.App.Tests;

/// <summary>
/// PDF出力の保存先を起動中だけ覚える件（殿ご下命2026-10-09）のテスト。
/// 発端は殿ご指摘——名前を変えて保存しても、次のPDF出力で前回の名が出ぬ
/// （保存ダイアログの初期値が毎回ドキュメント情報のタイトルから作られておった）。
/// </summary>
public class PdfExportPathMemoryTests : ViewModelTestBase
{
    /// <summary>前回の保存先が無ければ従来どおり。タイトルが空なら diagram。</summary>
    [Theory]
    [InlineData(null, "制御盤A", "制御盤A")]
    [InlineData("", "制御盤A", "制御盤A")]
    [InlineData(null, "", "diagram")]
    [InlineData(null, null, "diagram")]
    [InlineData("   ", "  ", "diagram")]
    public void ResolveSaveDefaults_NoPreviousPath_UsesTitle(string? suggested, string? title, string expectedName)
    {
        var (fileName, initialDirectory) = PdfPreviewDialog.ResolveSaveDefaults(suggested, title);

        Assert.Equal(expectedName, fileName);
        Assert.Null(initialDirectory);
    }

    /// <summary>前回の保存先が在れば、タイトルでなくその名とフォルダを出す。</summary>
    [Fact]
    public void ResolveSaveDefaults_PreviousPath_UsesItsNameAndFolder()
    {
        string folder = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        string previous = Path.Combine(folder, "改訂3_提出用.pdf");

        var (fileName, initialDirectory) = PdfPreviewDialog.ResolveSaveDefaults(previous, "制御盤A");

        Assert.Equal("改訂3_提出用.pdf", fileName);
        Assert.Equal(folder, initialDirectory);
    }

    /// <summary>前回のフォルダが消えておれば、名だけを引き継ぎフォルダは渡さぬ。</summary>
    [Fact]
    public void ResolveSaveDefaults_PreviousFolderMissing_KeepsNameOnly()
    {
        string previous = Path.Combine(Path.GetTempPath(), $"ecad2-missing-{Guid.NewGuid():N}", "出力.pdf");

        var (fileName, initialDirectory) = PdfPreviewDialog.ResolveSaveDefaults(previous, "制御盤A");

        Assert.Equal("出力.pdf", fileName);
        Assert.Null(initialDirectory);
    }

    /// <summary>覚えるのは図面ごと。新規・開くで図面が入れ替われば忘れ、Undo では忘れぬ。</summary>
    [Fact]
    public void LastPdfExportPath_ClearedWhenDocumentReplaced_ButKeptAcrossUndo()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new Ecad2.Model.GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(Ecad2.Model.ElementKind.ContactNO, orient: null);
        vm.LastPdfExportPath = @"C:\out\改訂3.pdf";

        vm.UndoCommand.Execute(null);
        Assert.Equal(@"C:\out\改訂3.pdf", vm.LastPdfExportPath);

        vm.NewDocument();
        Assert.Null(vm.LastPdfExportPath);
    }
}
