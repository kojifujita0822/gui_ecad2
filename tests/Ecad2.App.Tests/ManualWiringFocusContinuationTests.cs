using System.Reflection;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>
/// T-047修正(隠密2所見1+忍者実機4-c「矢印キーのツールバーナビゲーション奪取→隣接ボタン誤起動」
/// 対応、隠密設計書<c>docs/ecad2-t047-fix-test-design-onmitsu.md</c>2-2節・任意テスト)。
/// <c>MainWindow.RequiresCanvasFocusContinuation</c>はWPF依存の無い純粋な<see cref="ToolMode"/>
/// →bool判定のため、Window(STA/HWND)を一切インスタンス化せずreflection経由で全7値を検証できる
/// (実際のフォーカス移動先そのものは忍者のUI Automation実機観点で担保、本テストは条件判定
/// ロジックのみを対象とする)。
/// </summary>
public class ManualWiringFocusContinuationTests
{
    [Theory]
    [InlineData(ToolMode.Select, false)]
    [InlineData(ToolMode.PlaceElement, false)]
    [InlineData(ToolMode.PlaceConnector, true)]
    [InlineData(ToolMode.PlaceFrame, false)]
    [InlineData(ToolMode.PlaceLine, true)]
    [InlineData(ToolMode.PlaceDot, false)]
    [InlineData(ToolMode.PlaceWireBreak, false)]
    public void RequiresCanvasFocusContinuation_AllToolModeValues_MatchesExpected(ToolMode mode, bool expected)
    {
        var method = typeof(MainWindow).GetMethod(
            "RequiresCanvasFocusContinuation", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (bool)method!.Invoke(null, new object[] { mode })!;

        Assert.Equal(expected, actual);
    }
}
