using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Ecad2.App.Views;

/// <summary>
/// LadderCanvasのAutomationPeer(T-023)。キャンバスは単一DrawingVisualで全記号をまとめて描画する
/// ため(WPFの仕様上、実ビジュアルを持たない論理要素は自動的にはUI Automationツリーへ現れない)、
/// GetChildrenCoreでSheet.Elementsの1件ずつに対応するSymbolAutomationPeerを手動生成して返す
/// (GuiEcadに前例なし、隠密調査docs/ecad2-preimplementation-survey-onmitsu.md T-023節の推奨方式)。
/// </summary>
public sealed class LadderCanvasAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    private readonly LadderCanvas _owner;

    // ElementInstance.Id → 生成済みSymbolAutomationPeer。記号の追加・削除・再配置のたびに
    // ピアツリー同期を取る(隠密調査で指摘された継続コスト)ため、GetChildrenCoreの都度、
    // 現存する要素だけを残すよう突き合わせる。
    private readonly Dictionary<Guid, SymbolAutomationPeer> _peerCache = new();

    public LadderCanvasAutomationPeer(LadderCanvas owner) : base(owner)
    {
        _owner = owner;
    }

    protected override string GetClassNameCore() => nameof(LadderCanvas);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Selection ? this : base.GetPattern(patternInterface);

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var sheet = _owner.CurrentSheet;
        if (sheet is null) return null;

        var alive = new HashSet<Guid>();
        var children = new List<AutomationPeer>(sheet.Elements.Count);
        foreach (var element in sheet.Elements)
        {
            alive.Add(element.Id);
            if (!_peerCache.TryGetValue(element.Id, out var peer))
                _peerCache[element.Id] = peer = new SymbolAutomationPeer(element, _owner, this);
            children.Add(peer);
        }

        foreach (var staleId in _peerCache.Keys.Where(id => !alive.Contains(id)).ToList())
            _peerCache.Remove(staleId);

        return children;
    }

    bool ISelectionProvider.CanSelectMultiple => false;
    bool ISelectionProvider.IsSelectionRequired => false;

    IRawElementProviderSimple[] ISelectionProvider.GetSelection()
    {
        GetChildrenCore(); // _peerCacheを最新化してから引く

        var sheet = _owner.CurrentSheet;
        if (sheet is null || _owner.SelectedCellForAutomation is not { } cell)
            return Array.Empty<IRawElementProviderSimple>();

        var element = sheet.Elements.FirstOrDefault(e => e.Pos == cell);
        if (element is null || !_peerCache.TryGetValue(element.Id, out var peer))
            return Array.Empty<IRawElementProviderSimple>();

        return new[] { ProviderFromPeer(peer) };
    }
}
