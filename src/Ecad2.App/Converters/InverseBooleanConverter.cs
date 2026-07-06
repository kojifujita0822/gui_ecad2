using System.Globalization;
using System.Windows.Data;

namespace Ecad2.App.Converters;

/// <summary>
/// bool→boolの単純な反転。T-033増分1: 配置バー表示中(IsPlacementBarVisible=true)はメイン
/// コンテンツのIsEnabledをfalseにする(バー表示状態と真逆の値が要る箇所向け、隠密レビュー指摘)。
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? false : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? false : true;
}
