using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 右パネル上段「機器表」用の ViewModel。MainWindowViewModel の子プロパティとして持たせる
/// （design-brief 3節#1: God Class化の再発防止）。
/// </summary>
public sealed class DeviceTableViewModel : ViewModelBase
{
    public IReadOnlyList<Device> Devices { get; }

    public DeviceTableViewModel(DeviceTable table)
    {
        Devices = table.ByName.Values
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
