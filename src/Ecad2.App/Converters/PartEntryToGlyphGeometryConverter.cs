using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Converters;

/// <summary>
/// 部品選択エントリ(PartSelectionEntryViewModel)から配置バー種別選択(ComboBox)表示用のGX様式
/// アイコン(Geometry)を返す(T-033増分4/5、種別選択を文字表記からシンボルのみ表示へ変更)。
/// ツールバー2段目(T-040)と同じPath Dataを流用し(ORa/ORbはsF5/sF6のグリフ)、対応するツールバー
/// グリフの無いセレクトSWは同系統で新規作成(侍起草、忍者スクショ・殿目視の型で確定させる想定)。
/// 既知5種以外(自作パーツ等)は「自作パーツ」ツールバーボタンと同じ汎用フォルダアイコンへ
/// フォールバックする。
///
/// T-043往復2周目(隠密レビューCONFIRMED、docs/ecad2-t043-review-onmitsu-2.md所見1): 判定は
/// Definition.Idの固定文字列完全一致ではなくCategory/Role/IsOrEligibleベース。Explorerコピー由来で
/// Id再採番された基本図形(T-035、Category/Role/IsOrEligibleは維持されIdのみ変わる)でも正しく
/// 個別グリフを表示できる。Category==""(基本図形フォルダ直下)をゲートにするのは、Role/IsOrEligible
/// の組だけで判定すると、たまたま同じ組み合わせを持つ自作パーツ(Category="自作")まで誤って既知5種の
/// グリフに巻き込みかねないため(PartPaletteViewModel.cs:60のOR論理エントリ生成が同じ
/// Category==""ゲートを使っている前例踏襲)。
/// </summary>
public sealed class PartEntryToGlyphGeometryConverter : IValueConverter
{
    private static readonly Geometry ContactNo = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14");
    private static readonly Geometry OrContactNo = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M2,4 L2,9 M16,4 L16,9");
    private static readonly Geometry ContactNc = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M4,15 L14,3");
    private static readonly Geometry OrContactNc = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M2,4 L2,9 M16,4 L16,9 M4,15 L14,3");
    private static readonly Geometry Coil = Geometry.Parse("M2,9 L4,9 M14,9 L16,9 M9,4 A5,5 0 1 1 8.999,4");
    private static readonly Geometry Terminal = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M9,6 A3,3 0 1 1 8.999,6 M9,3 L9,15");
    private static readonly Geometry SelectSwitch = Geometry.Parse(
        "M2,9 L5,9 M13,9 L16,9 M6,8 A1,1 0 1 1 5.999,8 M12,8 A1,1 0 1 1 11.999,8 M6,14 L12,4");
    private static readonly Geometry Custom = Geometry.Parse("M3,3 L15,3 L15,15 L3,15 Z M9,6 L9,12 M6,9 L12,9");

    static PartEntryToGlyphGeometryConverter()
    {
        ContactNo.Freeze();
        OrContactNo.Freeze();
        ContactNc.Freeze();
        OrContactNc.Freeze();
        Coil.Freeze();
        Terminal.Freeze();
        SelectSwitch.Freeze();
        Custom.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PartSelectionEntryViewModel entry || entry.Category != "") return Custom;
        var def = entry.Definition;
        return (def.Role, def.IsOrEligible, entry.IsOr) switch
        {
            (PartRole.ContactNO, true, false) => ContactNo,
            (PartRole.ContactNO, true, true) => OrContactNo,
            (PartRole.ContactNC, true, false) => ContactNc,
            (PartRole.ContactNC, true, true) => OrContactNc,
            (PartRole.ContactNO, false, _) => SelectSwitch,
            (PartRole.Coil, _, _) => Coil,
            (PartRole.Terminal, _, _) => Terminal,
            _ => Custom,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
