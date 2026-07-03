using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 右パネル上段「機器表」用の ViewModel。MainWindowViewModel の子プロパティとして持たせる
/// （design-brief 3節#1: God Class化の再発防止）。
/// </summary>
public sealed class DeviceTableViewModel : ViewModelBase
{
    private readonly DeviceTable _table;

    public IReadOnlyList<Device> Devices { get; private set; }

    public DeviceTableViewModel(DeviceTable table)
    {
        _table = table;
        Devices = BuildList();
    }

    /// <summary>
    /// DeviceTable(モデル)への外部からの変更(T-017: プロパティパネルでのデバイス名編集等)を
    /// 一覧へ反映する。DeviceはINotifyPropertyChangedを実装していないため、一覧自体を作り直して
    /// 通知する(個別Add/Removeの追跡が要らない単純なスナップショット更新)。
    /// </summary>
    public void Refresh()
    {
        Devices = BuildList();
        OnPropertyChanged(nameof(Devices));
    }

    private IReadOnlyList<Device> BuildList()
        => _table.ByName.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
