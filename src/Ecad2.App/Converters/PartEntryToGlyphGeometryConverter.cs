using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Ecad2.Persistence;

namespace Ecad2.App.Converters;

/// <summary>
/// 部品(PartFolderEntry)から配置バー種別選択(ComboBox)表示用のGX様式アイコン(Geometry)を返す
/// (T-033増分4、種別選択を文字表記からシンボルのみ表示へ変更)。ツールバー2段目(T-040)と同じ
/// Path Dataを流用し、対応するツールバーグリフの無いセレクトSWは同系統で新規作成(侍起草、
/// 忍者スクショ・殿目視の型で確定させる想定)。既知5種以外(自作パーツ等)は「自作パーツ」
/// ツールバーボタンと同じ汎用フォルダアイコンへフォールバックする。
/// </summary>
public sealed class PartEntryToGlyphGeometryConverter : IValueConverter
{
    private static readonly Geometry ContactNo = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14");
    private static readonly Geometry ContactNc = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M6,4 L6,14 M12,4 L12,14 M4,15 L14,3");
    private static readonly Geometry Coil = Geometry.Parse("M2,9 L4,9 M14,9 L16,9 M9,4 A5,5 0 1 1 8.999,4");
    private static readonly Geometry Terminal = Geometry.Parse("M2,9 L6,9 M12,9 L16,9 M9,6 A3,3 0 1 1 8.999,6 M9,3 L9,15");
    private static readonly Geometry SelectSwitch = Geometry.Parse(
        "M2,9 L5,9 M13,9 L16,9 M6,8 A1,1 0 1 1 5.999,8 M12,8 A1,1 0 1 1 11.999,8 M6,14 L12,4");
    private static readonly Geometry Custom = Geometry.Parse("M3,3 L15,3 L15,15 L3,15 Z M9,6 L9,12 M6,9 L12,9");

    static PartEntryToGlyphGeometryConverter()
    {
        ContactNo.Freeze();
        ContactNc.Freeze();
        Coil.Freeze();
        Terminal.Freeze();
        SelectSwitch.Freeze();
        Custom.Freeze();
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? id = (value as PartFolderEntry)?.Definition.Id;
        return id switch
        {
            BasicPartTemplates.ContactNOId => ContactNo,
            BasicPartTemplates.ContactNCId => ContactNc,
            BasicPartTemplates.CoilId => Coil,
            BasicPartTemplates.TerminalId => Terminal,
            BasicPartTemplates.SelectSwitchId => SelectSwitch,
            _ => Custom,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
