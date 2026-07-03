using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ecad2.App.Converters;

/// <summary>
/// bool→Visibilityの逆変換(true→Collapsed, false→Visible)。右パネル下段のプロパティ⇔部品選択
/// 状況依存切替(T-026段階4-7)で、片方のパネルにBooleanToVisibilityConverter、もう片方にこれを
/// 使うことで相互排他表示を実現する。
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility.Visible;
}
