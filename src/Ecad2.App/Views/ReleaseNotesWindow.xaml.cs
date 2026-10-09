using System.IO;
using System.Reflection;
using System.Windows;

namespace Ecad2.App.Views;

/// <summary>
/// 「リリースノート」ウィンドウ（P-192、殿ご承認2026-09-14。原本 GuiEcad に在った機能の踏襲）。
/// <see cref="UsageWindow"/> と同じ仕組み——<c>docs/release-notes.md</c> を埋め込みリソースとして抱え、
/// <see cref="MarkdownFlowDocumentConverter"/> で表示する。非モーダルで、多重起動の防止は
/// 呼び出し元のインスタンスキャッシュが担う（これも使い方と同じ）。
/// </summary>
public partial class ReleaseNotesWindow : Window
{
    private const string ResourceName = "Ecad2.App.ReleaseNotes.release-notes.md";

    public ReleaseNotesWindow()
    {
        InitializeComponent();
        ContentViewer.Document = MarkdownFlowDocumentConverter.Convert(LoadReleaseNotes());
    }

    /// <summary>埋め込まれたリリースノートの本文。internal はIVT経由のテスト用。</summary>
    internal static string LoadReleaseNotes()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"埋め込みリソースが見つからない: {ResourceName}");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
