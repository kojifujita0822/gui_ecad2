using Ecad2.App;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-154: 機器表のメーカー・数量列を編集可能にした際の同値ガード
/// (<see cref="DeviceTableCellEdit.HasChanged"/>)。CellEditEnding はバインディングの
/// 書き戻し前に発火するため、判定は「新テキスト対 Device の旧値」で行う。
/// </summary>
public class DeviceTableCellEditTests
{
    private static Device Sample() => new() { Name = "CR1", Model = "型式A", Maker = "M社", Quantity = 3 };

    [Theory]
    [InlineData("Model", "型式A", false)]   // 同値
    [InlineData("Model", "型式B", true)]    // 変化
    [InlineData("Maker", "M社", false)]
    [InlineData("Maker", "N社", true)]
    [InlineData("Quantity", "3", false)]
    [InlineData("Quantity", "5", true)]
    public void 新テキストが旧値と異なるときだけ変化とみなす(string path, string newText, bool expected)
        => Assert.Equal(expected, DeviceTableCellEdit.HasChanged(path, newText, Sample()));

    [Fact]
    public void 型式メーカーがnullなら空文字と同一視する()
    {
        var device = new Device { Name = "CR1" };   // Model/Maker とも null
        Assert.False(DeviceTableCellEdit.HasChanged("Model", "", device));
        Assert.False(DeviceTableCellEdit.HasChanged("Maker", "", device));
        Assert.True(DeviceTableCellEdit.HasChanged("Model", "x", device));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2.5")]
    [InlineData("  ")]
    public void 数量に数値以外を打っても変化とみなさない(string newText)
        => Assert.False(DeviceTableCellEdit.HasChanged("Quantity", newText, Sample()));

    [Theory]
    [InlineData("Name")]     // 表示のみの列
    [InlineData("Class")]
    [InlineData(null)]
    [InlineData("Unknown")]
    public void 編集対象外のパスは常に変化なし(string? path)
        => Assert.False(DeviceTableCellEdit.HasChanged(path, "何か", Sample()));
}
