using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 右パネル上段「機器表」用の ViewModel。MainWindowViewModel の子プロパティとして持たせる
/// （design-brief 3節#1: God Class化の再発防止）。
/// </summary>
public sealed class DeviceTableViewModel : ViewModelBase
{
    private DeviceTable _table;

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
        // T-050(隠密所見P-015): SetPropertyを経由しない直接代入のため、旧値を明示的に
        // OnPropertyChangedへ渡す(finding3=SelectedElementDeviceName等と同型の旧値null化対応)。
        var oldDevices = Devices;
        Devices = BuildList();
        OnPropertyChanged(nameof(Devices), oldDevices);
    }

    /// <summary>参照先のDeviceTableを丸ごと差し替える(T-019: 新規/開くでDocumentを差し替えた際に使う)。</summary>
    public void Rebind(DeviceTable table)
    {
        _table = table;
        Refresh();
    }

    private IReadOnlyList<Device> BuildList()
        => _table.ByName.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
}
