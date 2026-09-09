using System.Windows.Controls;
using System.Windows.Data;

namespace Ecad2.App.Tests;

/// <summary>
/// T-154(殿ご下命): 機器表グリッドの列構成。メーカー・数量を編集可能な列として足した。
/// <para>
/// <b>列の増減・読み取り専用の付け外し・バインディング先の綴り誤りを捕らえる網</b>——
/// いずれも実機でしか気づけぬ形で現れる(列が消える／打っても保存されぬ)。
/// </para>
/// </summary>
public class DeviceTableGridColumnsTests
{
    private static (string Header, string Path, bool ReadOnly) Describe(DataGridColumn c)
    {
        var path = ((c as DataGridTextColumn)?.Binding as Binding)?.Path.Path ?? "";
        return ((string)c.Header, path, c.IsReadOnly);
    }

    [Fact]
    public void 五列が機器名種別型式メーカー数量の順で並ぶ()
        => StaTestRunner.Run(() =>
        {
            var window = new MainWindow();

            var cols = window.DeviceTableGrid.Columns.Select(Describe).ToList();

            Assert.Equal(new[]
            {
                ("機器名", "Name", true),
                ("種別", "Class", true),
                ("型式", "Model", false),
                ("メーカー", "Maker", false),
                ("数量", "Quantity", false),
            }, cols);
        });
}
