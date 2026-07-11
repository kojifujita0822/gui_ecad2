using System.Globalization;
using System.Windows.Data;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.App.Converters;

/// <summary>機器表「種別」列のDeviceClass→日本語表示名(T-053)。PDF出力(機器表BOM)の
/// DiagramRenderer.DeviceClassLabelをそのまま参照し、画面とPDFの表記を一致させる
/// (殿裁定=P-020案A採用+PDF表記統一、2026-07-10)。文言をApp層へコピーせず単一箇所
/// (Core層DeviceClassLabel)で保守することで、将来の追従漏れを構造的に防ぐ。</summary>
public sealed class DeviceClassToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DeviceClass c ? DiagramRenderer.DeviceClassLabel(c) : value?.ToString() ?? "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
