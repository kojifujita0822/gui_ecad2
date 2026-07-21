using System.Globalization;
using System.Windows.Data;

namespace Ecad2.App.Converters;

/// <summary>
/// T-101: 2つのstring値が一致するかを判定する。配置ツールバーボタンの恒久ハイライト判定
/// (ViewModel.ActiveToolTagとボタン自身のTagの一致比較)に使う。Style自体は全ボタンで共有
/// されるため、ConverterParameterではなくMultiBinding+RelativeSource Selfでボタンごとに
/// 異なるTagを個別評価する(MainWindow.xaml PlacementToolBarButtonStyle参照)。
/// </summary>
public sealed class StringEqualsMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2 && values[0] is string a && values[1] is string b && a == b;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
