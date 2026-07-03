using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 配置ツールの相互排他モード。GuiEcadでは複数の bool フラグ（_placeKind/_placePartId/_placeOrient/...）の
/// 束として実装され、フラグの取りこぼし（クリア漏れ・排他崩れ）バグの温床になった
/// （design-brief 3節#1「状態管理の分散」）。ecad2では最初から単一enum+パラメータの record に
/// 集約し、この状態は MainWindowViewModel が公開する１つのプロパティとして扱う（code-behindや
/// 複数箇所に散らばせない）。
/// </summary>
public enum ToolMode { Select, PlaceElement, PlaceConnector, PlaceFrame, PlaceLine, PlaceDot, PlaceWireBreak }

/// <summary>現在の配置ツール状態。Kind/PartId/Orient/IsOr は Mode==PlaceElement のときのみ意味を持つ。</summary>
public readonly record struct ToolState(
    ToolMode Mode,
    ElementKind? Kind = null,
    string? PartId = null,
    string? Orient = null,
    bool IsOr = false)
{
    public static ToolState SelectDefault => new(ToolMode.Select);
}
