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

    // P-170(T-133増分5で隠密が検出、殿裁可2026-08-06): 静的なFreezableは凍結しておく。
    // 未凍結のまま置くと、生成したSTAスレッドに束縛されたままとなり、別のSTAスレッド
    // (xUnitが別テストクラスごとに立てるもの等)から描画で触れられた際に「異なるスレッドに
    // 属するDependencyObjectは使用できぬ」例外を招く(LadderCanvas.cs:93-97 と同じ理由。
    // あちらは T-133増分5往復1周目に5件まとめて凍結した=7dbc53b、本件はファイルが分かれる
    // ゆえ据え置かれていた最後の1件)。
    //
    // 静的コンストラクタで凍結するのは PartThumbnailRenderer.cs:30-34 と同じ作法。
    // 対象が1つだけゆえ、LadderCanvas のようなヘルパー関数は設けぬ。
    static SheetReorderInsertionAdorner()
    {
        LinePen.Freeze();
    }

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
