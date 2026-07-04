using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Ecad2.Simulation;

namespace Ecad2.App.Converters;

/// <summary>
/// DRC診断の重大度→表示色(T-018、殿裁定の重大度色分け)。Error=赤、Warning=橙、Info=グレー。
/// </summary>
public sealed class DiagnosticSeverityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DiagnosticSeverity.Error => Brushes.Firebrick,
            DiagnosticSeverity.Warning => Brushes.DarkOrange,
            DiagnosticSeverity.Info => Brushes.Gray,
            _ => Brushes.Black,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
