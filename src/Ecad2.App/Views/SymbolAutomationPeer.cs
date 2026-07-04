using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>
/// LadderCanvas上の1記号(ElementInstance)に対応するAutomationPeer(T-023)。実ビジュアルを
/// 持たない論理要素のため、FrameworkElementAutomationPeerではなくAutomationPeerを直接継承する
/// (隠密調査docs/ecad2-preimplementation-survey-onmitsu.md T-023節の推奨方式)。親子関係は
/// LadderCanvasAutomationPeer.GetChildrenCoreが本ピアを返した時点でWPFが自動的に設定する。
/// </summary>
public sealed class SymbolAutomationPeer : AutomationPeer, ISelectionItemProvider
{
    private readonly ElementInstance _element;
    private readonly LadderCanvas _owner;
    private readonly LadderCanvasAutomationPeer _parent;

    public SymbolAutomationPeer(ElementInstance element, LadderCanvas owner, LadderCanvasAutomationPeer parent)
    {
        _element = element;
        _owner = owner;
        _parent = parent;
    }

    protected override string GetClassNameCore() => nameof(SymbolAutomationPeer);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.DataItem;
    protected override string GetAutomationIdCore() => $"symbol-{_element.Id}";
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override bool IsOffscreenCore() => false;
    protected override bool IsEnabledCore() => true;
    protected override bool HasKeyboardFocusCore() => false;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override bool IsPasswordCore() => false;
    protected override bool IsRequiredForFormCore() => false;
    protected override List<AutomationPeer>? GetChildrenCore() => null;
    protected override string GetAcceleratorKeyCore() => "";
    protected override string GetAccessKeyCore() => "";
    protected override string GetHelpTextCore() => "";
    protected override string GetItemStatusCore() => "";
    protected override string GetItemTypeCore() => nameof(SymbolAutomationPeer);
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override AutomationOrientation GetOrientationCore() => AutomationOrientation.None;
    protected override void SetFocusCore() { }

    protected override Point GetClickablePointCore()
    {
        var rect = GetBoundingRectangleCore();
        return rect.IsEmpty ? new Point(double.NaN, double.NaN) : rect.TopLeft + new Vector(rect.Width / 2, rect.Height / 2);
    }

    // 「種別[ 機器名] (行N列M)」形式(忍者が実機検証で座標・種別・機器名を一括取得できる粒度、
    // 家老依頼)。種別表示名はLadderCanvas.DisplayNameFor経由でMainWindowViewModelの
    // プロパティパネル表示と同じ日本語ラダー用語規則(T-031)を踏襲する。
    protected override string GetNameCore()
    {
        string kind = _owner.DisplayNameFor(_element);
        string device = string.IsNullOrEmpty(_element.DeviceName) ? "" : $" {_element.DeviceName}";
        return $"{kind}{device} (行{_element.Pos.Row + 1}列{_element.Pos.Column})";
    }

    protected override Rect GetBoundingRectangleCore()
    {
        if (!_owner.IsVisible) return Rect.Empty;
        var local = _owner.CellRectDip(_element.Pos);
        var topLeft = _owner.PointToScreen(local.TopLeft);
        var bottomRight = _owner.PointToScreen(local.BottomRight);
        return new Rect(topLeft, bottomRight);
    }

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.SelectionItem ? this : null;

    // ISelectionItemProvider: 選択状態の読み取りのみサポートする。実際の選択変更はキャンバスの
    // クリック/キーボード操作(既存フロー、MainWindowViewModel.SelectedCell)を正とし、UI
    // Automation経由の書き込みは行わない(InvokeがClickハンドラを迂回して内部状態を不安定化させる
    // 既知の罠と同種のリスクを避けるため、task-implementationスキルTroubleshooting節の教訓を踏襲)。
    bool ISelectionItemProvider.IsSelected => _owner.SelectedCellForAutomation == _element.Pos;
    IRawElementProviderSimple ISelectionItemProvider.SelectionContainer => ProviderFromPeer(_parent);
    void ISelectionItemProvider.Select() { }
    void ISelectionItemProvider.AddToSelection() { }
    void ISelectionItemProvider.RemoveFromSelection() { }
}
