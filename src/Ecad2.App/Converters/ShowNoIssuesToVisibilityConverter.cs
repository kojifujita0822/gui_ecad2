using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Ecad2.App.Converters;

/// <summary>
/// 出力パネルの「異常なし」プレースホルダ表示可否（殿ご指摘2026-09-14＝「設計チェックで問題が
/// 無かった場合に何も表示されないと、判定されたのか未実行なのか分かりにくい」）。
/// 検索結果表示中でなく(Find.IsVisible=false)、かつ設計チェックが指摘0件で完了した
/// (OutputPanel.ShowNoIssuesMessage=true)ときのみVisible。
/// </summary>
public sealed class ShowNoIssuesToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
        => values.Length == 2 && values[0] is bool findVisible && values[1] is bool showNoIssues && !findVisible && showNoIssues
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
