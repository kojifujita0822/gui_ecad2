using Ecad2.Persistence;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 部品選択（自作パーツ含む全図形）用の ViewModel。MainWindowViewModel の子プロパティとして
/// 持たせ、God Class化を避ける（design-brief 3節#1）。Ecad2.Core.Persistence.PartFolderStore
/// （T-007で移植済み）から図形一覧を読み込む。配置操作自体はMainWindow.xaml.csのTryPlaceElement
/// (T-026段階4新配置フロー: セル選択→種別選択→浮動ダイアログ経由で配置)が担う。
/// </summary>
public sealed class PartPaletteViewModel : ViewModelBase
{
    public IReadOnlyList<PartFolderEntry> Entries { get; }

    public PartPaletteViewModel()
    {
        var store = PartFolderStore.CreateDefault();
        store.EnsureFolders();
        store.SeedBasics();
        Entries = store.Enumerate();
    }
}
