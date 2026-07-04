using System.Globalization;
using System.Windows.Data;
using Ecad2.Simulation;

namespace Ecad2.App.Converters;

/// <summary>DRC診断の該当箇所一覧→表示文字列(T-018)。GuiEcadの"[P1 行3]"表記を踏襲。</summary>
public sealed class CircuitRefListToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable<CircuitRef> refs
            ? string.Join(", ", refs.Select(c => $"P{c.PageNumber} 行{c.CircuitNumber}"))
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
