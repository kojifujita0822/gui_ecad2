using Ecad2.Model;

namespace Ecad2.App;

/// <summary>
/// 機器表グリッド(<c>DeviceTableGrid</c>)のセル編集確定時に「実際に値が変わったか」を判ずる。
/// <para>
/// <c>CellEditEnding</c> はバインディングがモデルへ書き戻す前に発火するため、ここでは編集中
/// テキストボックスの新しい文字列と <see cref="Device"/> の現在値(旧値)を比べる。変わったときだけ
/// <c>MarkDirty()</c> を呼ぶ同値ガード規約(型式列を足した T-066、メーカー・数量を足した T-154)。
/// </para>
/// <para>
/// View から切り出したのは、<c>DataGridCellEditEndingEventArgs</c> を組み立てずに判定だけを
/// STA なしで測れるようにするため(<c>samurai.md</c>「テストしにくいは設計の匂い」)。
/// </para>
/// </summary>
internal static class DeviceTableCellEdit
{
    /// <param name="bindingPath"><c>Device</c> のプロパティ名
    /// (<c>Model</c>／<c>Maker</c>／<c>Quantity</c>)。表示のみの列や未知のパスは常に <c>false</c>。</param>
    /// <param name="newText">編集中テキストボックスの新しい文字列。</param>
    /// <param name="device">編集対象の機器(この時点ではまだ旧値を保持している)。</param>
    public static bool HasChanged(string? bindingPath, string newText, Device device) => bindingPath switch
    {
        nameof(Device.Model) => newText != (device.Model ?? ""),
        nameof(Device.Maker) => newText != (device.Maker ?? ""),
        // 数量は数値として解釈できぬ入力なら DataGrid が確定自体を拒む。ここでは解釈でき、
        // かつ現在値と異なるときだけ dirty とする(不正入力で無用に dirty にせぬ)。
        nameof(Device.Quantity) => int.TryParse(newText, out int q) && q != device.Quantity,
        _ => false,
    };
}
