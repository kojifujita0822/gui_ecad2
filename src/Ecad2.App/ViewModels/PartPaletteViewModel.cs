using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Persistence;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 左パレット（部品選択）用の ViewModel。MainWindowViewModel の子プロパティとして持たせ、
/// God Class化を避ける（design-brief 3節#1）。Ecad2.Core.Persistence.PartFolderStore
/// （T-007で移植済み）から図形一覧を読み込み、クリックで配置ツールを選択させる。
/// </summary>
public sealed class PartPaletteViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;

    public IReadOnlyList<PartFolderEntry> Entries { get; }

    public ICommand SelectCommand { get; }

    public PartPaletteViewModel(MainWindowViewModel owner)
    {
        _owner = owner;

        var store = PartFolderStore.CreateDefault();
        store.EnsureFolders();
        store.SeedBasics();
        Entries = store.Enumerate();

        SelectCommand = new RelayCommand(param =>
        {
            if (param is PartFolderEntry entry)
                _owner.Tool = new ToolState(ToolMode.PlaceElement, PartId: entry.Definition.Id);
        });
    }
}
