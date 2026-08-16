using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Ecad2.Model;
using Ecad2.Rendering;
using Ecad2.Rendering.Wpf;

namespace Ecad2.App.Views;

/// <summary>形状編集キャンバスの描画ツール（GuiEcad原本と同じ9種）。</summary>
public enum PartEditTool { Select, Line, Polyline, Rect, Circle, Arc, Rotate, Text, Port }

/// <summary>
/// Undo/Redo のスナップショットに含める、キャンバスの外で編集されている状態。
/// GuiEcad原本の EditorSnapshot は Prims/Ports/W/H/Role の5項目を持つ。増分3-cで端子（Ports）が
/// キャンバスへ統合されたため、キャンバスの外に残るのは幅・高さ・役割の3項目。
/// </summary>
/// <remarks>
/// T-136(A)増分2で <see cref="SheetAffinity"/> を第4項目として加えた。<b>既定値は置いておらぬ</b>
/// ——生成箇所が <c>PartEditorDialog.CaptureExternalState</c> の1つしかなく改修の代償が小さいゆえ、
/// 渡し忘れをコンパイラに検出させる側を採った（T-133増分3・T-136増分1と同じ作法）。
/// </remarks>
public sealed record PartEditorExternalState(
    int WidthCells, int HeightCells, PartRole Role, SheetAffinity SheetAffinity,
    // T-152（殿ご裁可2026-08-16）: クロスリファレンス検査からの除外。配置先と同じく入力欄の一つゆえ、
    // T-144 の「四項目の作法を揃える」に倣い Undo 対象へ加える。
    // PartEditorUndoRules.ShouldRecord はレコードの値等価性へ委ねてあるゆえ、
    // 本項目を足すだけで判定は自動的に追随する（比較の書き漏らしが構造的に起きぬ形）。
    bool IsExcludedFromCrossReference);

/// <summary>
/// T-068増分3-b2: 自作パーツの形状編集キャンバス（選択/線/折れ線/矩形/円/弧/回転の7ツール）。
/// 増分0のPoC（<c>poc/t068-part-editor-poc/</c>）で操作感を検証した実装を本実装へ移植したもの。
/// PoCからの主な変更点は次の3つ:
///   1. 描画は <see cref="PartDrawing.DrawPrimitive"/>（増分3-b1でpublic化）へ委譲し、
///      PoCが持っていた描画ロジックの複製（約60行）を廃した
///   2. 幾何演算は <see cref="PartShapeGeometry"/>（増分3-b1で切り出し・単体テスト済み）へ委譲した
///   3. Undo/Redo はドラッグ中に実変化があった場合のみ記録する（PoC所見1の修正）
/// 座標系はパーツローカル（セル単位、原点=最左ポート点・行中心線=y0）。
/// </summary>
public sealed class PartEditorCanvas : FrameworkElement
{
    private const double MmToDip = 96.0 / 25.4;

    private readonly VisualCollection _children;
    private readonly GridGeometry _geo = new(cellMm: 9.0, marginMm: 30.0);
    private DrawingTheme _theme = DrawingTheme.Default;

    private List<PartPrimitive> _primitives = new();
    private List<PortDef> _ports = new();
    private readonly List<Snapshot> _undoStack = new();
    private readonly List<Snapshot> _redoStack = new();

    private PartEditTool _tool = PartEditTool.Select;

    // 選択状態は「図形」と「接続点」の2系統。同時に両方が選ばれることはなく、片方を選んだら
    // もう片方は解除する（削除・Undo等の分岐が二重に効くのを防ぐ）。
    private int _selectedIndex = -1;
    private int _selectedPortIndex = -1;

    // 接続点の移動ドラッグ
    private Point2D? _portDragStartCell;
    private PortDef? _portDragOriginal;
    private int _widthCells = 1;
    private int _heightCells = 1;

    private double _zoom = 1.0;
    private Point _panMm = new(0, 0);

    // 作図中（ドラフト）状態
    private Point2D? _dragStartCell;
    private PartPrimitive? _draftPrimitive;

    // 折れ線（殿裁定=案A=右クリックのみ確定）
    private readonly List<Point2D> _polylinePoints = new();
    private Point2D? _polylineCursorCell;

    // 選択プリミティブの移動ドラッグ（殿裁定=案A=頂点ハンドル無し・全体の平行移動のみ）
    private Point2D? _moveDragStartCell;
    private PartPrimitive? _moveDragOriginal;

    // 回転ドラッグ（単体のみ・15度スナップ）
    private Point2D? _rotateCenterCell;
    private double _rotateStartMouseAngleDeg;
    private PartPrimitive? _rotateDragOriginal;

    // ドラッグ開始"前"のスナップショット。ドラッグ中は _primitives を直接書き換え続けるため、
    // 確定時に「その場の _primitives」を積むと変更後の状態を積んでしまいUndoが機能しない。
    private Snapshot? _dragSnapshotBeforeChange;

    // GuiEcad原本の _dragChanged 相当。ドラッグで実際に値が変わった場合のみUndoへ記録する
    // （PoCは無条件に記録しており、クリックで選択しただけでUndoが1つ余分に積まれていた＝PoC所見1）。
    private bool _dragChanged;

    // パン（中ボタンドラッグ、ツール非依存）
    private Point? _panDragStartDip;
    private Point _panDragStartPanMm;

    public event EventHandler? StateChanged;

    /// <summary>Undo/Redo のスナップショットへ含める外部状態の取得。ダイアログ側が設定する。</summary>
    public Func<PartEditorExternalState>? CaptureExternalState { get; set; }

    /// <summary>Undo/Redo で外部状態を復元する。ダイアログ側が設定する。</summary>
    public Action<PartEditorExternalState>? RestoreExternalState { get; set; }

    /// <summary>文字ツールで配置する文字列の入力を求める（ダイアログ側が設定する）。
    /// null・空を返した場合は配置しない。キャンバス自身はモーダルを開かない——View部品が
    /// ダイアログを直に開くと、単体で使えず配置先ごとの見え方も制御できなくなるため。</summary>
    public Func<string?>? RequestText { get; set; }

    public PartEditorCanvas()
    {
        _children = new VisualCollection(this);
        Focusable = true;
        PreviewMouseLeftButtonDown += (_, _) => Focus();
    }

    protected override int VisualChildrenCount => _children.Count;
    protected override Visual GetVisualChild(int index) => _children[index];

    /// <summary>作図キャンバス色のテーマ。T-068増分3-b3（家老采配2026-07-25）: メインの
    /// ラダーキャンバス（<see cref="LadderCanvas.Theme"/>、T-083 PoC）はダークモードへ追従するのに
    /// 本キャンバスだけが固定値で、ダークモード時に白背景が浮いていた不整合を解消する。
    /// パーツエディタはモーダルゆえ、開いている最中のテーマ切替は起こりえないと見て起動時の反映のみとする。</summary>
    public DrawingTheme Theme
    {
        get => _theme;
        set
        {
            if (ReferenceEquals(_theme, value)) return;
            _theme = value;
            Draw();
        }
    }

    public PartEditTool Tool
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
            double clamped = Math.Clamp(value, 0.2, 8.0);
            if (_zoom == clamped) return;
            _zoom = clamped;
            Notify();   // PoC所見2の修正: Draw()だけでは倍率表示が更新されない
        }
    }

    /// <summary>基準枠（外形枠）の幅。プロパティ欄の入力に連動させる。</summary>
    public int WidthCells
    {
        get => _widthCells;
        set { if (_widthCells == value) return; _widthCells = value; Draw(); }
    }

    /// <summary>基準枠（外形枠）の高さ。プロパティ欄の入力に連動させる。</summary>
    public int HeightCells
    {
        get => _heightCells;
        set { if (_heightCells == value) return; _heightCells = value; Draw(); }
    }

    public int SelectedIndex => _selectedIndex;
    public int SelectedPortIndex => _selectedPortIndex;
    public IReadOnlyList<PartPrimitive> Primitives => _primitives;
    public IReadOnlyList<PortDef> Ports => _ports;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>選択中プリミティブが弧の場合のみ非null（扁平率の事後調整用）。</summary>
    public PartArc? SelectedArc => _selectedIndex >= 0 && _primitives[_selectedIndex] is PartArc a ? a : null;

    /// <summary>選択中の接続点。選ばれておらぬ場合はnull（T-136(B)増分5、種類の表示合わせ用）。
    /// <see cref="SelectedArc"/> と同じ流儀。</summary>
    public PortDef? SelectedPort
        => _selectedPortIndex >= 0 && _selectedPortIndex < _ports.Count ? _ports[_selectedPortIndex] : null;

    /// <summary>作図中・ドラッグ中の状態があるか（Escで取り消せるものがあるか）。</summary>
    private bool HasDraft => _draftPrimitive is not null || _polylinePoints.Count > 0
        || _moveDragStartCell is not null || _rotateCenterCell is not null || _portDragStartCell is not null;

    /// <summary>編集対象の図形と接続点を読み込む。呼び出し元のリストからは切り離したコピーを保持するため、
    /// 編集をキャンセルしても元の <see cref="PartDefinition"/> は壊れない（要素は record ゆえ共有でよい）。</summary>
    public void LoadContent(IEnumerable<PartPrimitive> primitives, IEnumerable<PortDef> ports)
    {
        _primitives = primitives.ToList();
        _ports = ports.ToList();
        _undoStack.Clear();
        _redoStack.Clear();
        _selectedIndex = -1;
        _selectedPortIndex = -1;
        CancelDraft();
        Notify();
    }

    /// <summary>選択中の弧の縦半径(Ry)を事後調整する。</summary>
    public void SetSelectedArcRy(double ry)
    {
        if (_selectedIndex < 0 || _primitives[_selectedIndex] is not PartArc a) return;
        PushUndo();
        _primitives[_selectedIndex] = a with { Ry = Math.Max(0.05, ry) };
        Notify();
    }

    /// <summary>選択中の接続点の種類を変える（T-136(B)増分5、殿裁定2026-08-02＝案イ）。
    /// <para>
    /// 書き込むか否かの判定は <see cref="PartEditorPortKindRules.ShouldApply"/> が持つ——
    /// WPFに依らぬ条件ゆえ切り出してある（境界の実測は App.Tests 側）。
    /// </para>
    /// <para>
    /// Undoは <c>Snapshot.Ports</c> 経由で自動的に効く（<c>_ports.ToList()</c> の値コピーゆえ
    /// 参照共有の懸念も無い）。<c>PartEditorExternalState</c> は幅・高さ・役割・配置先の4項目専用にて
    /// <c>Ports</c> を含まぬ設計ゆえ、そちらへの追加は不要である。
    /// </para></summary>
    public void SetSelectedPortKind(PortKind kind)
    {
        if (!PartEditorPortKindRules.ShouldApply(_ports, _selectedPortIndex, kind)) return;
        PushUndo();
        _ports[_selectedPortIndex] = _ports[_selectedPortIndex] with { Kind = kind };
        Notify();
    }

    private void Notify()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
        Draw();
    }

    // ===== 座標変換 =====
    // 描画は2段のPushTransformで行う（外側=パン・ズーム、内側=原点余白）。PartDrawing はセル座標を
    // ×cell するだけで余白を知らないため、余白分を内側の変換として積む必要がある。合成すると
    // 最終DIP = (cellCoord*cellMm + margin)*K*zoom + pan*K となり、下記の CellToDip と一致する。

    private Point CellToDip(Point2D cell)
    {
        double worldMmX = _geo.MarginMm + cell.X * _geo.CellMm;
        double worldMmY = _geo.MarginMm + cell.Y * _geo.CellMm;
        return new Point((worldMmX * _zoom + _panMm.X) * MmToDip, (worldMmY * _zoom + _panMm.Y) * MmToDip);
    }

    private Point2D DipToCell(Point dip)
    {
        double worldMmX = (dip.X / MmToDip - _panMm.X) / _zoom;
        double worldMmY = (dip.Y / MmToDip - _panMm.Y) / _zoom;
        return new Point2D((worldMmX - _geo.MarginMm) / _geo.CellMm, (worldMmY - _geo.MarginMm) / _geo.CellMm);
    }

    private static Point2D SnapCell(Point2D p) => new(PartShapeGeometry.Snap(p.X), PartShapeGeometry.Snap(p.Y));

    /// <summary>セル座標を内側変換の基準（余白を含まない mm）へ移す。</summary>
    private Point2D CellToLocalMm(double cellX, double cellY) => new(cellX * _geo.CellMm, cellY * _geo.CellMm);

    // ===== マウス操作 =====

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        var cell = SnapCell(DipToCell(e.GetPosition(this)));
        switch (_tool)
        {
            case PartEditTool.Select:
                BeginSelectOrMove(cell);
                break;
            case PartEditTool.Line:
            case PartEditTool.Rect:
            case PartEditTool.Circle:
            case PartEditTool.Arc:
                _dragStartCell = cell;
                _draftPrimitive = BuildPrimitive(_tool, cell, cell);
                CaptureMouse();
                break;
            case PartEditTool.Polyline:
                _polylinePoints.Add(cell);
                Notify();
                break;
            case PartEditTool.Rotate:
                BeginRotate(cell, e.GetPosition(this));
                break;
            case PartEditTool.Text:
                PlaceText(cell);
                break;
            case PartEditTool.Port:
                AddPort(cell);
                break;
        }
        e.Handled = true;
    }

    /// <summary>文字ツール: クリック位置へ文字を置く（GuiEcad原本どおり、入力はダイアログで受ける）。
    /// 文字の大きさを変えるUIは設けないため <see cref="PartText.SizeCells"/> は既定値のまま。</summary>
    private void PlaceText(Point2D cell)
    {
        string? text = RequestText?.Invoke();
        if (string.IsNullOrEmpty(text)) return;
        CommitAdd(new PartText(text, cell.X, cell.Y));
        Notify();
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
            case PartEditTool.Line:
            case PartEditTool.Rect:
            case PartEditTool.Circle:
            case PartEditTool.Arc:
                if (_dragStartCell is { } start && e.LeftButton == MouseButtonState.Pressed)
                    _draftPrimitive = BuildPrimitive(_tool, start, cell);
                break;
            case PartEditTool.Polyline:
                _polylineCursorCell = cell;
                break;
            case PartEditTool.Select:
                if (_portDragStartCell is not null) UpdatePortDrag(cell);
                else if (_moveDragStartCell is not null) UpdateMoveDrag(cell);
                break;
            case PartEditTool.Rotate:
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
            case PartEditTool.Line:
            case PartEditTool.Rect:
            case PartEditTool.Circle:
            case PartEditTool.Arc:
                if (_draftPrimitive is not null && !PartShapeGeometry.IsDegenerate(_draftPrimitive))
                    CommitAdd(_draftPrimitive);
                _dragStartCell = null;
                _draftPrimitive = null;
                ReleaseMouseCapture();
                Notify();
                break;
            case PartEditTool.Select:
                if (_portDragStartCell is not null) CommitPortDrag();
                else if (_moveDragStartCell is not null) CommitMove();
                break;
            case PartEditTool.Rotate:
                if (_rotateCenterCell is not null) CommitRotate();
                break;
        }
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        if (_tool == PartEditTool.Polyline)
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
        Zoom = _zoom * (e.Delta > 0 ? 1.1 : 1.0 / 1.1);
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        switch (e.Key)
        {
            // 作図中のときだけEscを消費する。何も作図していないEscはダイアログのキャンセル
            // (IsCancel="True"のボタン)へ通す——常に消費するとダイアログを閉じられなくなる。
            case Key.Escape when HasDraft:
                CancelDraft();
                Notify();
                e.Handled = true;
                break;
            case Key.Delete when _tool == PartEditTool.Select && (_selectedIndex >= 0 || _selectedPortIndex >= 0):
                DeleteSelected();
                e.Handled = true;
                break;
            case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
                Undo();
                e.Handled = true;
                break;
            case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
                Redo();
                e.Handled = true;
                break;
        }
    }

    // ===== 選択・移動 =====

    private void BeginSelectOrMove(Point2D cell)
    {
        // 接続点を図形より優先して拾う（GuiEcad原本踏襲）。図形の線上に接続点が重なっていても
        // 接続点を掴めるようにするため。
        int portIdx = HitTestPort(cell);
        if (portIdx >= 0)
        {
            _selectedPortIndex = portIdx;
            _selectedIndex = -1;                     // 選択は接続点と図形で排他
            _portDragStartCell = cell;
            _portDragOriginal = _ports[portIdx];
            _dragSnapshotBeforeChange = CaptureSnapshot();
            _dragChanged = false;
            CaptureMouse();
            Notify();
            return;
        }

        int idx = PartShapeGeometry.HitTest(_primitives, cell.X, cell.Y);
        _selectedIndex = idx;
        _selectedPortIndex = -1;                     // 選択は接続点と図形で排他
        if (idx >= 0)
        {
            _moveDragStartCell = cell;
            _moveDragOriginal = _primitives[idx];
            _dragSnapshotBeforeChange = CaptureSnapshot();
            _dragChanged = false;
            CaptureMouse();
        }
        Notify();
    }

    // ===== 接続点（T-068増分3-c、GuiEcad原本の接続点ツール踏襲） =====

    /// <summary>接続点を置く。位置は整数へ丸めて基準枠の範囲へクランプする。
    /// 既に同じ位置に接続点があれば何もしない（原本どおり警告は出さない）。</summary>
    private void AddPort(Point2D cell)
    {
        var (row, boundary) = PartShapeGeometry.ClampPort(cell.X, cell.Y, _widthCells, _heightCells);
        if (PartShapeGeometry.IndexOfPortAt(_ports, row, boundary) >= 0) return;

        PushUndo();
        _ports.Add(new PortDef($"P{_ports.Count + 1}", row, boundary));
        Notify();
    }

    private int HitTestPort(Point2D cell) => PartShapeGeometry.HitTestPort(_ports, cell.X, cell.Y);

    private void UpdatePortDrag(Point2D cell)
    {
        if (_portDragStartCell is not { } start || _portDragOriginal is not { } original || _selectedPortIndex < 0) return;
        double dx = cell.X - start.X, dy = cell.Y - start.Y;
        if (dx != 0 || dy != 0) _dragChanged = true;
        // 原本どおり、移動先が既存の接続点と重なっても弾かない（追加時のみ重複を見る非対称を踏襲）。
        var (row, boundary) = PartShapeGeometry.ClampPort(
            original.BoundaryOffset + dx, original.RowOffset + dy, _widthCells, _heightCells);
        _ports[_selectedPortIndex] = original with { RowOffset = row, BoundaryOffset = boundary };
    }

    private void CommitPortDrag()
    {
        if (_portDragStartCell is null) return;
        if (_dragChanged) PushUndoSnapshot(_dragSnapshotBeforeChange);
        _portDragStartCell = null;
        _portDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        _dragChanged = false;
        ReleaseMouseCapture();
        Notify();
    }

    private void UpdateMoveDrag(Point2D cell)
    {
        if (_moveDragStartCell is not { } start || _moveDragOriginal is null || _selectedIndex < 0) return;
        double dx = cell.X - start.X, dy = cell.Y - start.Y;
        if (dx != 0 || dy != 0) _dragChanged = true;
        _primitives[_selectedIndex] = PartShapeGeometry.Translate(_moveDragOriginal, dx, dy);
    }

    private void CommitMove()
    {
        if (_moveDragStartCell is null) return;
        if (_dragChanged) PushUndoSnapshot(_dragSnapshotBeforeChange);
        _moveDragStartCell = null;
        _moveDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        _dragChanged = false;
        ReleaseMouseCapture();
        Notify();
    }

    // ===== 回転 =====

    private void BeginRotate(Point2D cell, Point mouseDip)
    {
        int idx = PartShapeGeometry.HitTest(_primitives, cell.X, cell.Y);
        _selectedIndex = idx;
        if (idx < 0) { Notify(); return; }
        _rotateDragOriginal = _primitives[idx];
        var (cx, cy) = PartShapeGeometry.CenterOf(_rotateDragOriginal);
        _rotateCenterCell = new Point2D(cx, cy);
        _rotateStartMouseAngleDeg = AngleDeg(CellToDip(_rotateCenterCell.Value), mouseDip);
        _dragSnapshotBeforeChange = CaptureSnapshot();
        _dragChanged = false;
        CaptureMouse();
        Notify();
    }

    private void UpdateRotateDrag(Point mouseDip)
    {
        if (_rotateCenterCell is not { } center || _rotateDragOriginal is null || _selectedIndex < 0) return;
        double deltaDeg = PartShapeGeometry.SnapAngleDeg(AngleDeg(CellToDip(center), mouseDip) - _rotateStartMouseAngleDeg);
        if (deltaDeg != 0) _dragChanged = true;
        _primitives[_selectedIndex] = PartShapeGeometry.Rotate(_rotateDragOriginal, center.X, center.Y, deltaDeg);
    }

    private void CommitRotate()
    {
        if (_rotateCenterCell is null) return;
        if (_dragChanged) PushUndoSnapshot(_dragSnapshotBeforeChange);
        _rotateCenterCell = null;
        _rotateDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        _dragChanged = false;
        ReleaseMouseCapture();
        Notify();
    }

    private static double AngleDeg(Point center, Point p) => Math.Atan2(p.Y - center.Y, p.X - center.X) * 180 / Math.PI;

    // ===== 作図 =====

    /// <summary>ツールに応じた形状を組み立てる。形状そのものの構築は <see cref="PartShapeGeometry"/> が持ち、
    /// ツール種別による分岐（UI状態）だけを本クラスが受け持つ。</summary>
    private static PartPrimitive? BuildPrimitive(PartEditTool tool, Point2D start, Point2D end) => tool switch
    {
        PartEditTool.Line => new PartLine(start.X, start.Y, end.X, end.Y),
        PartEditTool.Rect => PartShapeGeometry.BuildRect(start.X, start.Y, end.X, end.Y),
        PartEditTool.Circle => PartShapeGeometry.BuildCircle(start.X, start.Y, end.X, end.Y),
        PartEditTool.Arc => PartShapeGeometry.BuildArc(start.X, start.Y, end.X, end.Y),
        _ => null,
    };

    // ===== Undo/Redo（GuiEcad原本の EditorSnapshot 相当＝Prims/Ports/W/H/Role の5項目） =====

    private sealed record Snapshot(List<PartPrimitive> Primitives, List<PortDef> Ports, PartEditorExternalState? External);

    private Snapshot CaptureSnapshot() => new(_primitives.ToList(), _ports.ToList(), CaptureExternalState?.Invoke());

    private void PushUndo() => PushUndoSnapshot(CaptureSnapshot());

    private void PushUndoSnapshot(Snapshot? snapshot)
    {
        if (snapshot is null) return;
        _undoStack.Add(snapshot);
        _redoStack.Clear();
    }

    /// <summary>
    /// T-144（殿ご裁可2026-08-02）: 入力欄（幅・高さ・役割・シート種別）の変更を Undo 履歴へ積む。
    /// <para>
    /// <b>なぜ <see cref="PushUndo"/> をそのまま使えぬか</b>：<see cref="PushUndo"/> は
    /// <see cref="CaptureSnapshot"/> 経由で<b>現在の</b>外部状態を取るが、本経路が呼ばれる時点では
    /// 入力欄は既に変わっている。積むべきは<b>変更前</b>の状態ゆえ、呼び出し側が保持しているものを
    /// 受け取る形にした。
    /// </para>
    /// <para>
    /// 図形と接続点は現在の値をそのまま写す——入力欄の変更でそれらは変わらぬゆえ、
    /// 変更前後で同一である。
    /// </para>
    /// <para>
    /// <b>積むか否かの判定は呼び出し側が <see cref="PartEditorUndoRules.ShouldRecord"/> で行う。</b>
    /// ここで再判定はせぬ——判定に要る「変更前の状態」を持っているのは呼び出し側であり、
    /// 二重に持たせれば食い違いの余地が生まれる。
    /// </para>
    /// <para>
    /// <b>【<see cref="Notify"/> を必ず呼ぶこと】</b>履歴を積んだだけでは「元に戻す」ボタンが有効にならぬ。
    /// <see cref="PartEditorDialog"/> は <see cref="StateChanged"/> を購読して
    /// <c>UndoButton.IsEnabled = CanUndo</c> を更新する設計ゆえ、通知を欠くと
    /// <b>履歴は内部に積まれておるのに UI から辿り着けぬ</b>状態になる。
    /// <b>本メソッドの初版はこれを欠き、忍者の実機確認で観点1がNGとなった</b>（2026-08-02）——
    /// 幅・高さ・役割・配置先のいずれを変えてもボタンが一度も有効にならず、
    /// Undo を実行する手段が無かった（<c>Ctrl+Z</c> のキーバインドは未実装ゆえ、ボタンが唯一の経路）。
    /// <b>履歴を積む他の経路（<see cref="SetSelectedPortKind"/> 等）はいずれも直後に
    /// <see cref="Notify"/> を呼んでおり、本メソッドだけが作法から外れていた。</b>
    /// </para>
    /// </summary>
    public void PushExternalStateSnapshot(PartEditorExternalState before)
    {
        PushUndoSnapshot(new Snapshot(_primitives.ToList(), _ports.ToList(), before));
        Notify();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        _redoStack.Add(CaptureSnapshot());
        ApplySnapshot(_undoStack[^1]);
        _undoStack.RemoveAt(_undoStack.Count - 1);
        Notify();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        _undoStack.Add(CaptureSnapshot());
        ApplySnapshot(_redoStack[^1]);
        _redoStack.RemoveAt(_redoStack.Count - 1);
        Notify();
    }

    private void ApplySnapshot(Snapshot snapshot)
    {
        _primitives = snapshot.Primitives;
        _ports = snapshot.Ports;
        if (snapshot.External is { } external) RestoreExternalState?.Invoke(external);
        _selectedIndex = -1;
        _selectedPortIndex = -1;
    }

    private void CommitAdd(PartPrimitive p)
    {
        PushUndo();
        _primitives.Add(p);
    }

    /// <summary>選択中の接続点、または選択中の図形を削除する（選択は排他ゆえどちらか一方）。</summary>
    public void DeleteSelected()
    {
        if (_selectedPortIndex >= 0)
        {
            PushUndo();
            _ports.RemoveAt(_selectedPortIndex);
            _selectedPortIndex = -1;
            Notify();
            return;
        }

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
        if (_portDragOriginal is { } portOriginal && _selectedPortIndex >= 0)
            _ports[_selectedPortIndex] = portOriginal;
        _portDragStartCell = null;
        _portDragOriginal = null;
        _dragSnapshotBeforeChange = null;
        _dragChanged = false;
        ReleaseMouseCapture();
    }

    // ===== 描画 =====

    public void Draw()
    {
        _children.Clear();
        double w = ActualWidth > 0 ? ActualWidth : 600;
        double h = ActualHeight > 0 ? ActualHeight : 400;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var bg = _theme.Background;
            dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(bg.A, bg.R, bg.G, bg.B)), null, new Rect(0, 0, w, h));

            var renderer = new WpfRenderer(dc);
            renderer.PushTransform(_panMm.X, _panMm.Y, _zoom);            // 外側: パン・ズーム
            renderer.PushTransform(_geo.MarginMm, _geo.MarginMm, 1.0);    // 内側: 原点余白

            // T-140系統2・P-150(殿裁可2026-08-02): 基準枠の色をDrawingTheme.FrameGuide(原本回帰の青、
            // テーマ非依存の意味色)へ委ねる。View層から色・太さ・線種の直書きが消える
            // (docs/ecad2-t140-keitou2-test-design-onmitsu.md §3.1案あ、本日のPortColorと同じ形)。
            var frameStroke = _theme.Get(StrokeRole.PartFrameGuide);
            var normalStroke = new StrokeStyle(_theme.Foreground, 0.3);
            var selectedStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 255, 69, 0), 0.5);
            var draftStroke = new StrokeStyle(new Ecad2.Rendering.Color(255, 30, 144, 255), 0.4, LineStyle.Dashed);

            // セルの区切り線。基準枠より前＝最背面に描く（枠・図形・接続点を覆わぬため）。
            DrawCellGrid(renderer, w, h);

            // 基準枠（プロパティ欄の幅・高さに連動する外形の目安）。T-133増分1(殿裁定6)で
            // 行中心基準へ移した——接続点のRowOffsetと基準が揃う。算出はCore層の純粋関数へ。
            var (frameX, frameY, frameW, frameH) =
                PartShapeGeometry.FrameRect(_widthCells, _heightCells, _geo.CellMm);
            renderer.DrawRectangle(new Rect2D(frameX, frameY, frameW, frameH), frameStroke);

            for (int i = 0; i < _primitives.Count; i++)
                PartDrawing.DrawPrimitive(renderer, _theme, _primitives[i], _geo.CellMm,
                    i == _selectedIndex ? selectedStroke : normalStroke);

            if (_draftPrimitive is not null)
                PartDrawing.DrawPrimitive(renderer, _theme, _draftPrimitive, _geo.CellMm, draftStroke);

            if (_polylinePoints.Count > 0)
            {
                var pts = new List<Point2D>(_polylinePoints);
                if (_polylineCursorCell is { } cur) pts.Add(cur);
                if (pts.Count >= 2)
                    renderer.DrawPolyline(pts.Select(p => CellToLocalMm(p.X, p.Y)).ToArray(), draftStroke);
            }

            // 接続点は図形の上に重ねて描く（ヒットテストで優先するのと同じ順序）
            //
            // T-136(B)増分3（殿裁定2026-07-31、逸脱の承知は2026-08-01）: 選択を「色」でなく「形」で表す。
            // 従来は塗り色そのものを選択で切り替えており（選択=OrangeRed／非選択=DodgerBlue）、
            // 一つの色軸が「選択」と「種類」を兼ねられなんだ。選択リングを重ねる形へ改め、
            // 赤を空ける——増分4で入る「接続点の種類」の色は、この塗りの側が受け持つ。
            //
            // リング色は _theme.Foreground の流用（ライト=黒／ダーク=明灰）。殿裁定3=「リングは
            // 『形』で既に選択を表しており、色に意味を負わせずとも足りる」。
            // なお原本GuiEcadは選択を色で表し（青=選択／赤=非選択）、常時描かれる白い輪郭は縁取りで
            // あって選択表現ではない——リングで表すのはecad2独自の形にて、殿が承知のうえで裁可なされた。
            //
            // T-136(B)増分4（殿裁定2026-08-02）: 塗り色を接続点の種類（赤=電源に接続される点／
            // 青=制御配線でDRC無効な点）で描き分ける。色の決定はDrawingTheme.PortColorへ切り出し
            // （View層に色分岐を持たせずCore層でテスト可能にする、docs/ecad2-t136-increment4-plan-
            // samurai.md §2.3）。赤の具体的な色調（論点7）は殿裁可2026-08-02＝Light/Dark双方の
            // 実機の絵をご覧のうえ確定。
            var (portFillRadius, portRingRadius) = PartShapeGeometry.PortVisualRadiiMm(_geo.CellMm);
            var portRingStroke = new StrokeStyle(_theme.Foreground, PartShapeGeometry.PortRingStrokeMm);
            for (int i = 0; i < _ports.Count; i++)
            {
                var c = CellToLocalMm(_ports[i].BoundaryOffset, _ports[i].RowOffset);
                renderer.FillCircle(c, portFillRadius, DrawingTheme.PortColor(_ports[i].Kind));
                if (i == _selectedPortIndex)
                    renderer.DrawCircle(c, portRingRadius, portRingStroke);
            }

            if (_tool == PartEditTool.Rotate && _selectedIndex >= 0)
            {
                var (ccx, ccy) = PartShapeGeometry.CenterOf(_primitives[_selectedIndex]);
                var c = CellToLocalMm(ccx, ccy);
                double m = _geo.CellMm * 0.15;
                renderer.DrawLine(new(c.X - m, c.Y), new(c.X + m, c.Y), selectedStroke);
                renderer.DrawLine(new(c.X, c.Y - m), new(c.X, c.Y + m), selectedStroke);
            }

            renderer.PopTransform();
            renderer.PopTransform();
        }
        _children.Add(visual);
    }

    /// <summary>
    /// セルの区切り線を描く（T-137、殿裁定2026-07-31）。狙いは「基準枠の中が何セル分あるか」を
    /// 絵で示すこと。
    /// <para>
    /// <b>【P-168・2026-08-05訂正】</b>起票時点（T-137）は「高さ設定 h に対し枠が覆う行は 2h-1 であり、
    /// 設定値と実体が食い違う」ことが狙いの根拠だったが、この前提は<see cref="PartShapeGeometry.FrameRect"/>
    /// を h セルちょうどへ改めた T-139(C) 裁定（2026-07-31、T-137実装の2時間27分後）で消えている
    /// （隠密が時系列を特定、<c>docs/proposed.md</c> の P-168 欄）。区切り線が今も要るかは殿のご判断だが、
    /// 「残す」と裁定（2026-08-05）されたため機能はそのまま、誤った根拠の記述のみをここで正す。
    /// </para>
    /// <para>
    /// 殿裁定＝<b>案B（キャンバス全域）＋枠の外は一段薄く</b>。中心行の強調・表示の入切は設けぬ。
    /// 線の意味はセルの境目——縦は列の境界（整数）、横は行の境目（半整数。行は中心基準ゆえ）。
    /// 位置の算出は <see cref="PartShapeGeometry.GridLineXs"/>／<see cref="PartShapeGeometry.GridLineYs"/>。
    /// </para>
    /// <para>
    /// 線の本数はズーム倍率だけで決まり、<see cref="Zoom"/> の下限 0.2 のとき 1セル＝約6.8DIP。
    /// 既定の大きさなら計180本弱にて、本数の上限ガードは設けておらぬ。
    /// </para>
    /// </summary>
    /// <summary>刻み（セル単位）。原本の DrawGrid が 0.25 刻みの薄線と整数境界の線を重ねるのに倣う。</summary>
    private const double FineGridStepCells = 0.25;
    private const double BoundaryGridStepCells = 1.0;

    private void DrawCellGrid(IRenderer renderer, double canvasWidthDip, double canvasHeightDip)
    {
        var baseStroke = _theme.Get(StrokeRole.Grid);
        // ズーム倍率の分を先に割り、画面上の太さを一定に保つ（殿裁定2026-07-31）。
        // 忍者の実測＝倍率0.20では線が背景に沈み、在るべき13本のうち5本が区別できなかった
        // （PushTransform が ScaleTransform を積むゆえ、ペンの太さにも倍率が掛かるため）。
        double lineWidth = DrawingTheme.ZoomInvariantWidthMm(baseStroke.Width, _zoom);

        // 濃さは3段。原本（PartEditorWindow.xaml.cs:246-249）の faint / line / center に対応する。
        var fineInner = new StrokeStyle(Fade(baseStroke.Color, 1.0 / 3.0), lineWidth);
        var boundaryInner = baseStroke with { Width = lineWidth };
        // 枠の外は同じ色のまま不透明度を半分にする（T-137殿裁定＝枠の範囲を線の中でも際立たせる）。
        var fineOuter = fineInner with { Color = Fade(fineInner.Color, 0.5) };
        var boundaryOuter = boundaryInner with { Color = Fade(boundaryInner.Color, 0.5) };

        // 可視範囲をセル座標へ逆算する（パン・ズームで動くため毎回求める）。
        var visibleTopLeft = DipToCell(new Point(0, 0));
        var visibleBottomRight = DipToCell(new Point(canvasWidthDip, canvasHeightDip));

        // 枠の範囲（FrameRect は mm ゆえセル単位へ戻す）。
        var (frameX, frameY, frameW, frameH) =
            PartShapeGeometry.FrameRect(_widthCells, _heightCells, _geo.CellMm);
        var frameTopLeft = new Point2D(frameX / _geo.CellMm, frameY / _geo.CellMm);
        var frameBottomRight = new Point2D((frameX + frameW) / _geo.CellMm, (frameY + frameH) / _geo.CellMm);

        // 薄い順に重ねる（後から描くものが上に乗る）。
        DrawCellGridLines(renderer, visibleTopLeft, visibleBottomRight, FineGridStepCells, fineOuter, horizontal: true);
        DrawCellGridLines(renderer, visibleTopLeft, visibleBottomRight, BoundaryGridStepCells, boundaryOuter, horizontal: false);
        DrawCellGridLines(renderer, frameTopLeft, frameBottomRight, FineGridStepCells, fineInner, horizontal: true);
        DrawCellGridLines(renderer, frameTopLeft, frameBottomRight, BoundaryGridStepCells, boundaryInner, horizontal: false);

        // 行中心線 y=0（配線が通る基準線）。原本は最も濃い色を用いる（:262）。
        // 枠の幅だけに引く——原本と同じ範囲であり、かつ「この部品の基準線」という意味に適う。
        var centerStroke = new StrokeStyle(Fade(_theme.Foreground, 0.55), lineWidth * 1.5);
        renderer.DrawLine(CellToLocalMm(frameTopLeft.X, 0), CellToLocalMm(frameBottomRight.X, 0), centerStroke);
    }

    /// <summary>指定した範囲（セル座標）へ格子線を引く。範囲の端に乗る線は含む。
    /// <paramref name="horizontal"/> が true なら横線も引く（薄線は縦横とも、整数境界は縦のみ——原本と同じ）。</summary>
    private void DrawCellGridLines(IRenderer renderer, Point2D topLeft, Point2D bottomRight,
                                   double stepCells, StrokeStyle stroke, bool horizontal)
    {
        foreach (double x in PartShapeGeometry.GridLinesAt(topLeft.X, bottomRight.X, stepCells))
            renderer.DrawLine(CellToLocalMm(x, topLeft.Y), CellToLocalMm(x, bottomRight.Y), stroke);
        if (!horizontal) return;
        foreach (double y in PartShapeGeometry.GridLinesAt(topLeft.Y, bottomRight.Y, stepCells))
            renderer.DrawLine(CellToLocalMm(topLeft.X, y), CellToLocalMm(bottomRight.X, y), stroke);
    }

    /// <summary>色の不透明度を割合で下げる（0.5 なら半分）。</summary>
    private static Ecad2.Rendering.Color Fade(Ecad2.Rendering.Color c, double factor)
        => c with { A = (byte)Math.Clamp(c.A * factor, 0, 255) };

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Draw();
    }
}
