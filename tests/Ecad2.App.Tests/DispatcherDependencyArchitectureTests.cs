using System.Runtime.CompilerServices;
using System.IO;

namespace Ecad2.App.Tests;

/// <summary>
/// P-040(T-045増分A隠密レビュー所見10対応): SheetNavigationViewModelがWPF
/// <c>Application.Current.Dispatcher</c>へ直接依存していた不具合(T-034で発覚、P-016で
/// <see cref="Ecad2.App.ViewModels.IDispatcherService"/>抽象化により解消)の再発を、ソース
/// スキャンで機械的に検出する。対象は<c>ViewModels/</c>配下(WPF非依存であるべきVM層)のみ。
/// View層(コードビハインド)はWPF <c>Window</c>基底の<c>Dispatcher</c>プロパティ利用が
/// 前提の層でありスコープ外(所見10原文もSheetNavigationViewModelの再発防止が主旨)。
/// 唯一の正規アダプタ<see cref="Ecad2.App.ViewModels.WpfDispatcherService"/>のみ除外する。
/// </summary>
public class DispatcherDependencyArchitectureTests
{
    private static readonly string[] ForbiddenPatterns =
    {
        "Application.Current.Dispatcher",
        "Dispatcher.CurrentDispatcher",
    };

    private static readonly string[] AllowedFileNames =
    {
        "WpfDispatcherService.cs",
    };

    [Fact]
    public void ViewModels_DoNotDirectlyReferenceWpfDispatcher()
    {
        var viewModelsDir = GetViewModelsDirectory();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(viewModelsDir, "*.cs", SearchOption.AllDirectories))
        {
            if (AllowedFileNames.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            foreach (var pattern in ForbiddenPatterns)
            {
                if (content.Contains(pattern))
                {
                    violations.Add($"{Path.GetFileName(file)} contains \"{pattern}\"");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"ViewModels配下でWPF Dispatcherへの直接依存が検出された(IDispatcherService経由にすべき): {string.Join(", ", violations)}");
    }

    private static string GetViewModelsDirectory([CallerFilePath] string thisFilePath = "")
    {
        var testProjectDir = Path.GetDirectoryName(thisFilePath)!; // tests/Ecad2.App.Tests
        var testsDir = Directory.GetParent(testProjectDir)!.FullName; // tests
        var repoRoot = Directory.GetParent(testsDir)!.FullName; // repo root
        return Path.Combine(repoRoot, "src", "Ecad2.App", "ViewModels");
    }
}
