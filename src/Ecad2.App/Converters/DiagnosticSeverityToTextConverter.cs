using System.Globalization;
using System.Windows.Data;
using Ecad2.Simulation;

namespace Ecad2.App.Converters;

/// <summary>DRC診断の重大度→日本語表示名(T-018)。</summary>
public sealed class DiagnosticSeverityToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            DiagnosticSeverity.Error => "エラー",
            DiagnosticSeverity.Warning => "警告",
            DiagnosticSeverity.Info => "情報",
            _ => value?.ToString() ?? "",
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
