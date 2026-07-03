using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.Views;

/// <summary>
/// ラダー図面を描画するキャンバス。T-002 PoCの SymbolCanvas パターン(DrawingVisualホスト)を踏襲した
/// 新規実装。Ecad2.Core.Rendering.DiagramRenderer(上位描画器)と Ecad2.Rendering.Wpf.WpfRenderer
/// (IRendererのWPF実装、T-007成果物)を使って Sheet を描画する。
///
/// フォーカス: Focusable=true とし、クリックで明示的にフォーカスを取得する
/// (T-002/T-006で検証したFocusScope制御パターンの最小反映)。MainWindow.xaml側で
/// このキャンバスを含む領域を FocusManager.IsFocusScope="True" として独立させている。
/// 要素単位の選択・編集フォーカス制御（PreviewLostKeyboardFocusのキャンセル等）は
/// 配置ツール機能の実装に合わせて将来追加する。
/// </summary>
public sealed class LadderCanvas : FrameworkElement
{
    // WpfRenderer内部のK(mm→DIP)と同じ換算率。DiagramRenderer.PageSizeはmm単位を返すため、
    // Width/Height(WPF DIP)へ変換するのはビュー側の責務。
    private const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;
    // ShowGrid=true: 作図ガイドの薄いグリッド線を画面表示する(T-030、殿裁定)。実機テストで
    // 行位置を目視で合わせやすくする狙い。PDF出力(Ecad2.Pdf)側は別のDiagramRendererインスタンス
    // を使うため、ここでの設定は画面表示にのみ影響する。
    private readonly DiagramRenderer _renderer = new(options: new Ecad2.Rendering.RenderOptions { ShowGrid = true });

    public LadderCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        PreviewMouseLeftButtonDown += (_, _) => Focus();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public void Draw(Sheet sheet, PartLibrary? library = null)
    {
        _children.Clear();

        var size = _renderer.PageSize(sheet);
        double widthDip = size.Width * MmToDip;
        double heightDip = size.Height * MmToDip;

        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen())
        {
            // DrawingVisualは実際に何か描画された領域のみがヒットテスト対象になる(WPFの仕様)。
            // 罫線・要素が無い空きセルもクリックで拾えるよう、まず透明な背景矩形をページ全体に
            // 描画しておく(T-026 OR入力実機検証で発覚: 空き行が常にヒットテスト対象外だった)。
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, widthDip, heightDip));

            var wpfRenderer = new WpfRenderer(dc);
            _renderer.Render(wpfRenderer, sheet, library);
        }
        _children.Add(visual);

        Width = widthDip;
        Height = heightDip;
        InvalidateMeasure();
    }

    /// <summary>
    /// このキャンバス自身のローカル座標系（DIP単位、LayoutTransform適用前の内部座標）を
    /// グリッド座標へ変換する。要素配置（T-016）のクリック位置判定に使う。
    /// </summary>
    public GridPos ToGridPos(Point localPositionDip)
    {
        double xMm = localPositionDip.X / MmToDip;
        double yMm = localPositionDip.Y / MmToDip;
        var geo = _renderer.Geometry;
        return new GridPos(geo.RowAt(yMm), geo.ColAt(xMm));
    }
}
