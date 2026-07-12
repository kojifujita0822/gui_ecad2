using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Ecad2.App.Views;

/// <summary>
/// T-082: シートナビゲーション(SheetNavList)のドラッグ&amp;ドロップ並び替え中、挿入位置を示す
/// 水平線(殿裁定「案A」の視覚フィードバック要素、AdornedElement=ドロップ候補のListBoxItem)。
/// </summary>
internal sealed class SheetReorderInsertionAdorner : Adorner
{
    private static readonly Pen LinePen = new(Brushes.DodgerBlue, 2.0);
    private readonly bool _insertAfter;

    public SheetReorderInsertionAdorner(UIElement adornedElement, bool insertAfter) : base(adornedElement)
    {
        _insertAfter = insertAfter;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double y = _insertAfter ? AdornedElement.RenderSize.Height : 0;
        drawingContext.DrawLine(LinePen, new Point(0, y), new Point(AdornedElement.RenderSize.Width, y));
    }
}
