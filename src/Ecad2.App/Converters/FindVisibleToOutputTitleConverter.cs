using System.Globalization;
using System.Windows.Data;

namespace Ecad2.App.Converters;

/// <summary>
/// T-058増分2(殿ご指摘、実機確認): 出力パネルのAvalonDockタブタイトルをFind.IsVisibleに応じて
/// 「出力」⇔「検索結果」へ動的に切り替える。旧実装は手動タイトルTextBlockで表現していたが、
/// AvalonDock化後はタブヘッダー自体が既にタイトルを表示するため二重表示になっており、
/// この切替表示をタブタイトル側へ一本化した。
/// </summary>
public sealed class FindVisibleToOutputTitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "検索結果" : "出力";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
