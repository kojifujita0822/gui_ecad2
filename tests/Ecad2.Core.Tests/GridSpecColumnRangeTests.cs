using Ecad2.Model;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-132: シート設定ダイアログで入力できる列数の範囲（<see cref="GridSpec.MinColumns"/>／
/// <see cref="GridSpec.MaxColumns"/>）。殿裁定2026-07-27＝下限2（原本 GuiEcad 準拠）・上限40。
/// <para>
/// <b>裁定そのものでなく、裁定の「根拠」を測る</b>（<c>samurai.md</c>【MUST】）。
/// 殿が上限を40とされた根拠は「ecad2 の <see cref="GridSpec.Columns"/> の既定値に揃える」ことであり、
/// 「40という数」そのものではない。ゆえに上限は既定値との一致で測る——
/// <b>既定値だけが変わって上限が取り残されれば、既定のまま作られたシートをダイアログで表現できなくなる</b>
/// という、裁定が避けようとした事態がそこで起きる。
/// </para>
/// <para>
/// 下限2は原本 GuiEcad の <c>NumberBox.Minimum</c> をそのまま採ったもので、ecad2 側に対応する
/// 既定値が無いため直接固定する。
/// </para>
/// </summary>
public class GridSpecColumnRangeTests
{
    /// <summary>下限＝2（原本準拠）。</summary>
    [Fact]
    public void 列数の下限は原本準拠の2()
        => Assert.Equal(2, GridSpec.MinColumns);

    /// <summary>
    /// 上限＝<see cref="GridSpec.Columns"/> の既定値と一致する。
    /// <b>これが裁定の根拠そのものである</b>——既定値のまま作られたシートが、ダイアログの上限を
    /// 超えて表現できなくなることを避けるための裁定であった。
    /// </summary>
    [Fact]
    public void 列数の上限はColumnsの既定値と一致する()
        => Assert.Equal(new GridSpec().Columns, GridSpec.MaxColumns);

    /// <summary>下限が上限を超えぬこと（範囲として成立していること）。</summary>
    [Fact]
    public void 列数の下限は上限より小さい()
        => Assert.True(GridSpec.MinColumns < GridSpec.MaxColumns);

    /// <summary>
    /// 行の範囲と混線していないこと。
    /// <b>行と列で別の定数を持つ以上、片方をもう片方へ書き写す誤りが起こりうる</b>——
    /// 上限は行60・列40で異なるため、取り違えればここで気づける。
    /// </summary>
    [Fact]
    public void 行の上限と列の上限は別の値である()
        => Assert.NotEqual(GridSpec.MaxRows, GridSpec.MaxColumns);
}
