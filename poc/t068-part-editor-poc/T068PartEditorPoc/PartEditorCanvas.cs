using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace T068PartEditorPoc;

/// <summary>
/// T-068増分0 PoC: 自作パーツ形状編集キャンバス(選択/線/折れ線/矩形/円/弧/回転の7ツール、
/// 接続点・文字はPoCスコープ外)。GuiEcad原本(PartEditorWindow.xaml.cs)の操作フローを
/// WPFへ踏襲(殿裁定=6論点中3分岐すべて案A)。座標系はパーツローカル(セル単位、原点=最左ポート・
/// 行中心線)。PartDrawing(Ecad2.Core.Rendering)はinternalのため参照不可、同等ロジックを
/// DrawPrimitive/DistanceToPrimitiveへ複製する(Core層は無改変)。
/// </summary>
public enum EditTool { Select, Line, Polyline, Rect, Circle, Arc, Rotate }

public sealed class PartEditorCanvas : FrameworkElement
{
    private const double MmToDip = 96.0 / 25.4;
    private const double SnapFraction = 1.0 / 16.0;   // 1/16セルスナップ(設計書§3論点6)
    private const double HitToleranceCells = 0.3;      // GuiEcad原本と同値の点-線分距離ヒット許容
    private const double RotateSnapDeg = 15.0;         // GuiEcad原本と同値(設計書§2.1)

    private readonly VisualCollection _children;
    private readonly GridGeometry _geo = new(cellMm: 9.0, marginMm: 30.0);
    private readonly DrawingTheme _theme = DrawingTheme.Default;

    private List<PartPrimitive> _primitives = new();
    private readonly List<List<PartPrimitive>> _undoStack = new();
    private readonly List<List<PartPrimitive>> _redoStack = new();

    private EditTool _tool = EditTool.Select;
    private int _selectedIndex = -1;

    private double _zoom = 1.0;
    private Point _panMm = new(0, 0);

    // 作図中(ドラフト)状態(論点1・3)
    private Point2D? _dragStartCell;
    private PartPrimitive? _draftPrimitive;

    // 折れ線(論点2、殿裁定=案A=右クリックのみ確定)
    private readonly List<Point2D> _polylinePoints = new();
    private Point2D? _polylineCursorCell;

    // 選択プリミティブの移動ドラッグ(平行移動のみ、殿裁定=分岐点2は案A=頂点ハンドル無し)
    private Point2D? _moveDragStartCell;
    private PartPrimitive? _moveDragOriginal;

    // 回転ドラッグ(論点4、単体のみ・15度スナップ)
    private Point2D? _rotateCenterCell;
    private double _rotateStartMouseAngleDeg;
    private PartPrimitive? _rotateDragOriginal;

    // 論点5: 移動/回転ドラッグ開始"前"のリスト全体スナップショット。ドラッグ中は_primitivesを
    // 直接書き換え続けるため、確定時に「その場の_primitives」をUndoスタックへ積むと変更後の状態を
    // 積んでしまいUndoが機能しない(実装中に自己発見)。開始時点のスナップショットを別途保持し、
    // 確定時にそれをUndoスタックへ積む。
    private List<PartPrimitive>? _dragSnapshotBeforeChange;

    // パン(中ボタンドラッグ、ツール非依存)
    private Point? _panDragStartDip;
    private Point _panDragStartPanMm;

    public event EventHandler? StateChanged;

    public PartEditorCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        PreviewMouseLeftButtonDown += (_, _) => Focus();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    public EditTool Tool
    {
        get => _tool;
        set
        {
            if (_tool == value) return;
            CancelDraft();
            _tool = value;
            Notify();
        }
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.2, 8.0);
            Draw();
        }
    }

    public int SelectedIndex => _selectedIndex;
    public IReadOnlyList<PartPrimitive> Primitives => _primitives;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    /// <summary>選択中プリミティブがPartArcの場合のみ非null(論点3=扁平率の事後調整用)。</summary>
    public PartArc? SelectedArc => _selectedIndex >= 0 && _primitives[_selectedIndex] is PartArc a ? a : null;

    /// <summary>論点3: 選択中のPartArcの縦半径(Ry)を事後調整する(NumberBox相当)。</summary>
    public void SetSelectedArcRy(double ry)
    {
        if (_selectedIndex < 0 || _primitives[_selectedIndex] is not PartArc a) return;
        PushUndo();
        _primitives[_selectedIndex] = a with { Ry = Math.Max(0.05, ry) };
        Notify();
    }

    private void Notify()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        Draw();
    }

    // ===== 座標変換(セル単位パーツローカル座標 <-> mm(Margin込みworld) <-> DIP画面座標) =====

    private Point2D CellToWorldMm(double cellX, double cellY) => new(_geo.X(cellX), _geo.MarginMm + cellY * _geo.CellMm);

    private Point CellToDip(Point2D cell)
    {
        var world = CellToWorldMm(cell.X, cell.Y);
        double dipX = (world.X * _zoom + _panMm.X) * MmToDip;
        double dipY = (world.Y * _zoom + _panMm.Y) * MmToDip;
        return new Point(dipX, dipY);
    }

    private Point2D DipToCell(Point dip)
    {
        double worldMmX = (dip.X / MmToDip - _panMm.X) / _zoom;
        double worldMmY = (dip.Y / MmToDip - _panMm.Y) / _zoom;
        double cellX = (worldMmX - _geo.MarginMm) / _geo.CellMm;
        double cellY = (worldMmY - _geo.MarginMm) / _geo.CellMm;
        return new Point2D(cellX, cellY);
    }

    private static double SnapValue(double v) => Math.Round(v / SnapFraction) * SnapFraction;
    private static Point2D SnapCell(Point2D p) => new(SnapValue(p.X), SnapValue(p.Y));

    // ===== マウス操作 =====

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var cell = SnapCell(DipToCell(e.GetPosition(this)));
        switch (_tool)
        {
            case EditTool.Select:
                BeginSelectOrMove(cell);
                break;
            case EditTool.Line:
            case EditTool.Rect:
            case EditTool.Circle:
            case EditTool.Arc:
                _dragStartCell = cell;
                _draftPrimitive = BuildPrimitive(_tool, cell, cell);
                CaptureMouse();
                break;
            case EditTool.Polyline:
                _polylinePoints.Add(cell);
                Notify();
                break;
            case EditTool.Rotate:
                BeginRotate(cell, e.GetPosition(this));
                break;
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var dip = e.GetPosition(this);
        var cell = SnapCell(DipToCell(dip));

        if (_panDragStartDip is { } panStart && e.MiddleButton == MouseButtonState.Pressed)
        {
            var d = dip - panStart;
            _panMm = new Point(_panDragStartPanMm.X + d.X / (MmToDip * _zoom), _panDragStartPanMm.Y + d.Y / (MmToDip * _zoom));
            Draw();
            return;
        }

        switch (_tool)
        {
            case EditTool.Line:
            case EditTool.Rect:
            case EditTool.Circle:
            case EditTool.Arc:
                if (_dragStartCell is { } start && e.LeftButton == MouseButtonState.Pressed)
                    _draftPrimitive = BuildPrimitive(_tool, start, cell);
                break;
            case EditTool.Polyline:
                _polylineCursorCell = cell;
                break;
            case EditTool.Select:
                if (_moveDragStartCell is not null) UpdateMoveDrag(cell);
                break;
            case EditTool.Rotate:
                if (_rotateCenterCell is not null) UpdateRotateDrag(dip);
                break;
        }
        Draw();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        switch (_tool)
        {
            case EditTool.Line:
            case EditTool.Rect:
            case EditTool.Circle:
            case EditTool.Arc:
                if (_draftPrimitive is not null && !IsDegenerate(_draftPrimitive))
                    CommitAdd(_draftPrimitive);
                _dragStartCell = null;
                _draftPrimitive = null;
                ReleaseMouseCapture();
                Notify();
                break;
            case EditTool.Select:
                if (_moveDragStartCell is not null) CommitMove();
                break;
            case EditTool.Rotate:
                if (_rotateCenterCell is not null) CommitRotate();
                break;
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (_tool == EditTool.Polyline)
        {
            if (_polylinePoints.Count >= 2)
            {
                var pts = _polylinePoints.SelectMany(p => new[] { p.X, p.Y }).ToArray();
                CommitAdd(new PartPolyline(pts));
            }
            _polylinePoints.Clear();
            _polylineCursorCell = null;
            Notify();
        }
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            _panDragStartDip = e.GetPosition(this);
            _panDragStartPanMm = _panMm;
            CaptureMouse();
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            _panDragStartDip = null;
            ReleaseMouseCapture();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        Zoom = _zoom * factor;
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape:
                CancelDraft();
                Notify();
                break;
            case Key.Delete when _tool == EditTool.Select && _selectedIndex >= 0:
                DeleteSelected();
                break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                Undo();
                break;
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                Redo();
                break;
        }
    }

    // ===== 選択・移動(論点6、殿裁定=分岐点2案A=頂点ハンドル無し・プリミティブ全体の平行移動のみ) =====

    private void BeginSelectOrMove(Point2D cell)
    {
        int idx = HitTest(cell);
        _selectedIndex = idx;
        if (idx >= 0)
        {
            _moveDragStartCell = cell;
            _moveDragOriginal = _primitives[idx];
            _dragSnapshotBeforeChange = _primitives.ToList();
            CaptureMouse();
        }
        Notify();
    }

    private void UpdateMoveDrag(Point2D cell)
    {
        if (_moveDragStartCell is not { } start || _moveDragOriginal is null || _selectedIndex < 0) return;
        double dx = cell.X - start.X, dy = cell.Y - start.Y;
        _primitives[_selectedIndex] = Translate(_moveDragOriginal, dx, dy);
    }

    private void CommitMove()
    {
        if (_moveDragStartCell is null) return;
        PushUndoSnapshot(_dragSnapshotBeforeChange);
        _moveDragStartCell = null;
        _moveDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        ReleaseMouseCapture();
        Notify();
    }

    private static PartPrimitive Translate(PartPrimitive p, double dx, double dy) => p switch
    {
        PartLine l => l with { X1 = l.X1 + dx, Y1 = l.Y1 + dy, X2 = l.X2 + dx, Y2 = l.Y2 + dy },
        PartCircle c => c with { Cx = c.Cx + dx, Cy = c.Cy + dy },
        PartArc a => a with { Cx = a.Cx + dx, Cy = a.Cy + dy },
        PartRect r => r with { X = r.X + dx, Y = r.Y + dy },
        PartPolyline pl => pl with { Points = TranslatePoints(pl.Points, dx, dy) },
        PartText t => t with { X = t.X + dx, Y = t.Y + dy },
        _ => p,
    };

    private static double[] TranslatePoints(double[] pts, double dx, double dy)
    {
        var result = new double[pts.Length];
        for (int i = 0; i < pts.Length; i += 2) { result[i] = pts[i] + dx; result[i + 1] = pts[i + 1] + dy; }
        return result;
    }

    // ===== 回転(論点4、単体のみ・15度スナップ。線/折れ線=座標焼き込み、矩形/弧=Rot加算という
    // GuiEcad原本の型ごとに異なる実装をそのまま踏襲=殿裁定案A) =====

    private void BeginRotate(Point2D cell, Point mouseDip)
    {
        int idx = HitTest(cell);
        _selectedIndex = idx;
        if (idx < 0) { Notify(); return; }
        _rotateDragOriginal = _primitives[idx];
        _rotateCenterCell = CenterOf(_rotateDragOriginal);
        var centerDip = CellToDip(_rotateCenterCell.Value);
        _rotateStartMouseAngleDeg = AngleDeg(centerDip, mouseDip);
        _dragSnapshotBeforeChange = _primitives.ToList();
        CaptureMouse();
        Notify();
    }

    private void UpdateRotateDrag(Point mouseDip)
    {
        if (_rotateCenterCell is not { } center || _rotateDragOriginal is null || _selectedIndex < 0) return;
        var centerDip = CellToDip(center);
        double currentAngle = AngleDeg(centerDip, mouseDip);
        double deltaDeg = currentAngle - _rotateStartMouseAngleDeg;
        deltaDeg = Math.Round(deltaDeg / RotateSnapDeg) * RotateSnapDeg;
        _primitives[_selectedIndex] = RotateBy(_rotateDragOriginal, center, deltaDeg);
    }

    private void CommitRotate()
    {
        if (_rotateCenterCell is null) return;
        PushUndoSnapshot(_dragSnapshotBeforeChange);
        _rotateCenterCell = null;
        _rotateDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        ReleaseMouseCapture();
        Notify();
    }

    private static double AngleDeg(Point center, Point p) => Math.Atan2(p.Y - center.Y, p.X - center.X) * 180 / Math.PI;

    private static Point2D CenterOf(PartPrimitive p) => p switch
    {
        PartLine l => new((l.X1 + l.X2) / 2, (l.Y1 + l.Y2) / 2),
        PartCircle c => new(c.Cx, c.Cy),
        PartArc a => new(a.Cx, a.Cy),
        PartRect r => new(r.X + r.W / 2, r.Y + r.H / 2),
        PartPolyline pl => new(AverageEven(pl.Points), AverageOdd(pl.Points)),
        PartText t => new(t.X, t.Y),
        _ => new(0, 0),
    };

    private static double AverageEven(double[] pts)
    {
        double sum = 0; int n = 0;
        for (int i = 0; i < pts.Length; i += 2) { sum += pts[i]; n++; }
        return n == 0 ? 0 : sum / n;
    }

    private static double AverageOdd(double[] pts)
    {
        double sum = 0; int n = 0;
        for (int i = 1; i < pts.Length; i += 2) { sum += pts[i]; n++; }
        return n == 0 ? 0 : sum / n;
    }

    /// <summary>型ごとに異なる回転実装(GuiEcad原本踏襲)。線/折れ線=座標を直接回転して焼き込み、
    /// 矩形/弧=Rotフィールドへ加算するのみ(座標自体は変えない)。円は回転しても見た目不変のため無変化、
    /// 文字の回転はPoCスコープ外(設計書§5)。</summary>
    private static PartPrimitive RotateBy(PartPrimitive p, Point2D center, double deg) => p switch
    {
        PartLine l => RotateLine(l, center, deg),
        PartPolyline pl => RotatePolyline(pl, center, deg),
        PartRect r => r with { Rot = r.Rot + deg },
        PartArc a => a with { Rot = a.Rot + deg },
        _ => p,
    };

    private static PartLine RotateLine(PartLine l, Point2D center, double deg)
    {
        var (x1, y1) = RotatePoint(l.X1, l.Y1, center, deg);
        var (x2, y2) = RotatePoint(l.X2, l.Y2, center, deg);
        return l with { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 };
    }

    private static PartPolyline RotatePolyline(PartPolyline pl, Point2D center, double deg)
    {
        var pts = new double[pl.Points.Length];
        for (int i = 0; i + 1 < pl.Points.Length; i += 2)
        {
            var (rx, ry) = RotatePoint(pl.Points[i], pl.Points[i + 1], center, deg);
            pts[i] = rx; pts[i + 1] = ry;
        }
        return pl with { Points = pts };
    }

    private static (double, double) RotatePoint(double x, double y, Point2D center, double deg)
    {
        double rad = deg * Math.PI / 180.0;
        double dx = x - center.X, dy = y - center.Y;
        double cos = Math.Cos(rad), sin = Math.Sin(rad);
        return (center.X + dx * cos - dy * sin, center.Y + dx * sin + dy * cos);
    }

    // ===== 作図(論点1・2・3) =====

    private static bool IsDegenerate(PartPrimitive p) => p switch
    {
        PartLine l => Math.Abs(l.X1 - l.X2) < 1e-6 && Math.Abs(l.Y1 - l.Y2) < 1e-6,
        PartRect r => r.W < 1e-6 || r.H < 1e-6,
        PartCircle c => c.R < 1e-6,
        PartArc a => a.R < 1e-6,
        _ => false,
    };

    private PartPrimitive BuildPrimitive(EditTool tool, Point2D start, Point2D end) => tool switch
    {
        EditTool.Line => new PartLine(start.X, start.Y, end.X, end.Y),
        EditTool.Rect => BuildRect(start, end),
        EditTool.Circle => BuildCircle(start, end),
        EditTool.Arc => BuildArc(start, end),
        _ => throw new InvalidOperationException($"BuildPrimitive未対応のツール: {tool}"),
    };

    private static PartRect BuildRect(Point2D a, Point2D b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
        return new PartRect(x, y, w, h);
    }

    private static PartCircle BuildCircle(Point2D a, Point2D b)
    {
        double r = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
        return new PartCircle(a.X, a.Y, r);
    }

    /// <summary>論点3: 外接矩形ドラッグ→PartArc変換。殿裁定=案A(半楕円弧のみ、SweepDeg=180固定)。</summary>
    private static PartArc BuildArc(Point2D a, Point2D b)
    {
        double cx = (a.X + b.X) / 2, cy = (a.Y + b.Y) / 2;
        double rx = Math.Max(0.05, Math.Abs(b.X - a.X) / 2);
        double ry = Math.Max(0.05, Math.Abs(b.Y - a.Y) / 2);
        return new PartArc(cx, cy, rx, StartDeg: 180, SweepDeg: 180, Ry: ry, Rot: 0);
    }

    // ===== Undo/Redo(論点5、EditorSnapshot=Listのシャローコピー方式。record型のイミュータブル性に
    // 依存、ドラッグ中は積まず確定時のみ1エントリ) =====

    private void PushUndo() => PushUndoSnapshot(_primitives.ToList());

    /// <summary>変更"前"のスナップショットをUndoスタックへ積む。追加・削除は呼び出し時点の
    /// _primitivesがそのまま「変更前」なのでPushUndo()で足りるが、移動/回転ドラッグは確定時点では
    /// 既に_primitivesが書き換わっているため、ドラッグ開始時に採取したスナップショットを渡す。</summary>
    private void PushUndoSnapshot(List<PartPrimitive>? snapshot)
    {
        if (snapshot is null) return;
        _undoStack.Add(snapshot);
        _redoStack.Clear();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Add(_primitives.ToList());
        _primitives = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _selectedIndex = -1;
        Notify();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Add(_primitives.ToList());
        _primitives = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _selectedIndex = -1;
        Notify();
    }

    private void CommitAdd(PartPrimitive p)
    {
        PushUndo();
        _primitives.Add(p);
    }

    public void DeleteSelected()
    {
        if (_selectedIndex < 0) return;
        PushUndo();
        _primitives.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        Notify();
    }

    private void CancelDraft()
    {
        _dragStartCell = null;
        _draftPrimitive = null;
        _polylinePoints.Clear();
        _polylineCursorCell = null;
        if (_moveDragStartCell is not null && _moveDragOriginal is not null && _selectedIndex >= 0)
            _primitives[_selectedIndex] = _moveDragOriginal;
        _moveDragStartCell = null;
        _moveDragOriginal = null;
        if (_rotateDragOriginal is not null && _selectedIndex >= 0)
            _primitives[_selectedIndex] = _rotateDragOriginal;
        _rotateCenterCell = null;
        _rotateDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        ReleaseMouseCapture();
    }

    // ===== ヒットテスト(論点6、GuiEcad原本と同値=点-線分距離0.3セル、回転考慮) =====

    private int HitTest(Point2D cell)
    {
        for (int i = _primitives.Count - 1; i >= 0; i--)
            if (DistanceToPrimitive(_primitives[i], cell) <= HitToleranceCells) return i;
        return -1;
    }

    private static double Dist(Point2D a, Point2D b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double DistPointToSegment(Point2D p, Point2D a, Point2D b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Dist(p, a);
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        return Dist(p, new Point2D(a.X + t * dx, a.Y + t * dy));
    }

    private static double DistanceToPrimitive(PartPrimitive p, Point2D pt) => p switch
    {
        PartLine l => DistPointToSegment(pt, new(l.X1, l.Y1), new(l.X2, l.Y2)),
        PartCircle c => Math.Abs(Dist(pt, new(c.Cx, c.Cy)) - c.R),
        PartArc a => DistanceToArc(a, pt),
        PartRect r => DistanceToRect(r, pt),
        PartPolyline pl => DistanceToPolyline(pl, pt),
        PartText t => Dist(pt, new(t.X, t.Y)),
        _ => double.MaxValue,
    };

    private static double DistanceToPolyline(PartPolyline pl, Point2D pt)
    {
        double min = double.MaxValue;
        for (int i = 0; i + 3 < pl.Points.Length; i += 2)
        {
            var a = new Point2D(pl.Points[i], pl.Points[i + 1]);
            var b = new Point2D(pl.Points[i + 2], pl.Points[i + 3]);
            min = Math.Min(min, DistPointToSegment(pt, a, b));
        }
        return min;
    }

    private static double DistanceToRect(PartRect r, Point2D pt)
    {
        double cx = r.X + r.W / 2, cy = r.Y + r.H / 2;
        var (lx, ly) = RotatePoint(pt.X, pt.Y, new Point2D(cx, cy), -r.Rot);
        double dx = Math.Max(Math.Max(r.X - lx, lx - (r.X + r.W)), 0);
        double dy = Math.Max(Math.Max(r.Y - ly, ly - (r.Y + r.H)), 0);
        if (dx == 0 && dy == 0)
        {
            double distLeft = lx - r.X, distRight = (r.X + r.W) - lx, distTop = ly - r.Y, distBottom = (r.Y + r.H) - ly;
            return Math.Min(Math.Min(distLeft, distRight), Math.Min(distTop, distBottom));
        }
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double DistanceToArc(PartArc a, Point2D pt)
    {
        int seg = Math.Max(8, (int)Math.Ceiling(Math.Abs(a.SweepDeg) / 6.0));
        double a0 = a.StartDeg * Math.PI / 180.0, sw = a.SweepDeg * Math.PI / 180.0;
        double min = double.MaxValue;
        Point2D? prev = null;
        for (int i = 0; i <= seg; i++)
        {
            double t = a0 + sw * i / seg;
            var raw = new Point2D(a.Cx + a.R * Math.Cos(t), a.Cy + a.EffRy * Math.Sin(t));
            var (rx, ry) = RotatePoint(raw.X, raw.Y, new Point2D(a.Cx, a.Cy), a.Rot);
            var cur = new Point2D(rx, ry);
            if (prev is { } pv) min = Math.Min(min, DistPointToSegment(pt, pv, cur));
            prev = cur;
        }
        return min;
    }

    // ===== 描画(PartDrawing.Drawの複製。internalのため参照不可、Core層無改変方針により複製) =====

    public void Draw()
    {
        _children.Clear();
        double w = ActualWidth > 0 ? ActualWidth : 900;
        double h = ActualHeight > 0 ? ActualHeight : 650;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bg = _theme.Background;
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(bg.A, bg.R, bg.G, bg.B)), null, new Rect(0, 0, w, h));

            var renderer = new WpfRenderer(dc);
            renderer.PushTransform(_panMm.X, _panMm.Y, _zoom);

            // 基準枠(パーツ外形の視覚的目安、8x4セル)
            var frameStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 180, 180, 180), 0.1, LineStyle.Dashed);
            renderer.DrawRectangle(new Rect2D(_geo.MarginMm, _geo.MarginMm, 8 * _geo.CellMm, 4 * _geo.CellMm), frameStroke);

            var normalStroke = new StrokeStyle(_theme.Foreground, 0.3);
            var selectedStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 255, 69, 0), 0.5);
            var draftStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 30, 144, 255), 0.4, LineStyle.Dashed);

            for (int i = 0; i < _primitives.Count; i++)
                DrawPrimitive(renderer, _primitives[i], i == _selectedIndex ? selectedStroke : normalStroke);

            if (_draftPrimitive is not null) DrawPrimitive(renderer, _draftPrimitive, draftStroke);

            if (_polylinePoints.Count > 0)
            {
                var pts = new List<Point2D>(_polylinePoints);
                if (_polylineCursorCell is { } cur) pts.Add(cur);
                if (pts.Count >= 2)
                {
                    var worldPts = pts.Select(p => CellToWorldMm(p.X, p.Y)).ToArray();
                    renderer.DrawPolyline(worldPts, draftStroke);
                }
            }

            if (_tool == EditTool.Rotate && _selectedIndex >= 0)
            {
                var center = CenterOf(_primitives[_selectedIndex]);
                var cWorld = CellToWorldMm(center.X, center.Y);
                double m = _geo.CellMm * 0.15;
                renderer.DrawLine(new(cWorld.X - m, cWorld.Y), new(cWorld.X + m, cWorld.Y), selectedStroke);
                renderer.DrawLine(new(cWorld.X, cWorld.Y - m), new(cWorld.X, cWorld.Y + m), selectedStroke);
            }
        }
        _children.Add(visual);
    }

    private void DrawPrimitive(IRenderer r, PartPrimitive p, StrokeStyle s)
    {
        switch (p)
        {
            case PartLine l:
                r.DrawLine(CellToWorldMm(l.X1, l.Y1), CellToWorldMm(l.X2, l.Y2), s);
                break;
            case PartCircle c:
                r.DrawCircle(CellToWorldMm(c.Cx, c.Cy), c.R * _geo.CellMm, s);
                break;
            case PartArc a when a.Ry <= 0 && a.Rot == 0:
                r.DrawArc(CellToWorldMm(a.Cx, a.Cy), a.R * _geo.CellMm, a.StartDeg, a.SweepDeg, s);
                break;
            case PartArc a when a.Ry <= 0:
                r.DrawArc(CellToWorldMm(a.Cx, a.Cy), a.R * _geo.CellMm, a.StartDeg + a.Rot, a.SweepDeg, s);
                break;
            case PartArc a:
            {
                int seg = Math.Max(8, (int)Math.Ceiling(Math.Abs(a.SweepDeg) / 6.0));
                var pts = new Point2D[seg + 1];
                double a0 = a.StartDeg * Math.PI / 180.0, sw = a.SweepDeg * Math.PI / 180.0;
                for (int i = 0; i <= seg; i++)
                {
                    double t = a0 + sw * i / seg;
                    var raw = new Point2D(a.Cx + a.R * Math.Cos(t), a.Cy + a.EffRy * Math.Sin(t));
                    var (rx, ry) = RotatePoint(raw.X, raw.Y, new Point2D(a.Cx, a.Cy), a.Rot);
                    pts[i] = CellToWorldMm(rx, ry);
                }
                r.DrawPolyline(pts, s);
                break;
            }
            case PartRect rc when rc.Rot == 0:
            {
                var origin = CellToWorldMm(rc.X, rc.Y);
                r.DrawRectangle(new Rect2D(origin.X, origin.Y, rc.W * _geo.CellMm, rc.H * _geo.CellMm), s);
                break;
            }
            case PartRect rc:
            {
                double cx = rc.X + rc.W / 2, cy = rc.Y + rc.H / 2;
                var corners = new[]
                {
                    RotatePoint(rc.X, rc.Y, new Point2D(cx, cy), rc.Rot),
                    RotatePoint(rc.X + rc.W, rc.Y, new Point2D(cx, cy), rc.Rot),
                    RotatePoint(rc.X + rc.W, rc.Y + rc.H, new Point2D(cx, cy), rc.Rot),
                    RotatePoint(rc.X, rc.Y + rc.H, new Point2D(cx, cy), rc.Rot),
                    RotatePoint(rc.X, rc.Y, new Point2D(cx, cy), rc.Rot),
                };
                r.DrawPolyline(corners.Select(pt => CellToWorldMm(pt.Item1, pt.Item2)).ToArray(), s);
                break;
            }
            case PartPolyline pl when pl.Points.Length >= 4:
            {
                var pts = new Point2D[pl.Points.Length / 2];
                for (int i = 0; i < pts.Length; i++) pts[i] = CellToWorldMm(pl.Points[2 * i], pl.Points[2 * i + 1]);
                r.DrawPolyline(pts, s);
                break;
            }
            case PartText t:
                r.DrawText(t.Text, CellToWorldMm(t.X, t.Y),
                    _theme.Text(TextRole.DeviceName) with { FontSizeMm = t.SizeCells * _geo.CellMm });
                break;
        }
    }
}
