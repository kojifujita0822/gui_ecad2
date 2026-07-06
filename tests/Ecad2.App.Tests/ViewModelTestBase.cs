using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-042(P-019由来): new MainWindowViewModel()がPartFolderStore.CreateDefault()経由で
/// 実MyDocuments(殿PCではOneDriveリダイレクト先)を叩いてしまう副作用を避けるための共通基底。
/// テストごとに一時フォルダを発行し、CreateViewModel()で注入する。Ecad2.Core.Testsの
/// PartFolderStoreTests.CreateTempDirと同種のロジックだが、プロジェクトを跨ぐ共有のための
/// 新規プロジェクト追加は本タスクの範囲に対して過大なため、意図的に独立させている(家老裁定)。
/// </summary>
public abstract class ViewModelTestBase : IDisposable
{
    private readonly string _tempDir;

    protected ViewModelTestBase()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ecad2-apptest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    protected MainWindowViewModel CreateViewModel() => new(new PartFolderStore(_tempDir));

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
