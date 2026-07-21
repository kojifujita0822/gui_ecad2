using System.Globalization;
using System.Windows.Data;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Converters;

/// <summary>ツールモード(ToolMode enum)→日本語表示名(T-109、殿裁定=訳語案B)。ステータスバーの
/// 「ツール: {0}」表示専用。内部実装(ToolMode enum自体の名称・値)は変更しない、表示のみ日本語化する。</summary>
public sealed class ToolModeToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ToolMode.Select => "選択",
            ToolMode.PlaceElement => "要素配置",
            ToolMode.PlaceConnector => "縦コネクタ記入",
            ToolMode.PlaceFrame => "グループ枠記入",
            ToolMode.PlaceLine => "自由線記入",
            ToolMode.PlaceDot => "接続点記入",
            ToolMode.PlaceWireBreak => "配線分断記入",
            ToolMode.PlaceImage => "画像配置",
            _ => value?.ToString() ?? "",
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
