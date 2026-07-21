using System.Globalization;
using System.Windows.Data;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Converters;

/// <summary>アプリモード(AppMode enum)→日本語表示名(T-111)。ステータスバーの
/// 「モード: {0}」表示専用。内部実装(AppMode enum自体の名称・値)は変更しない、表示のみ日本語化する。</summary>
public sealed class AppModeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            AppMode.Drawing => "作画",
            AppMode.Test => "テスト",
            _ => value?.ToString() ?? "",
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
