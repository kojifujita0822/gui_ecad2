namespace Ecad2.Model;

/// <summary>1ページ（シート）。グリッド・母線・配置要素・接続・枠・回路番号を保持。</summary>
public sealed class Sheet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PageNumber { get; set; }
    public string Name { get; set; } = "";
    public GridSpec Grid { get; set; } = new();
    public BusConfig Bus { get; set; } = new();
    public List<ElementInstance> Elements { get; set; } = new();
    public List<VerticalConnector> Connectors { get; set; } = new();
    /// <summary>同一行の自動横配線を断ち切る分断マーク（同一行内で別ネットを作る）。</summary>
    public List<WireBreak> WireBreaks { get; set; } = new();
    public List<GroupFrame> Frames { get; set; } = new();
    public List<CircuitLine> Lines { get; set; } = new();
    /// <summary>右母線の右側に表示する行コメント一覧。</summary>
    public List<RungComment> RungComments { get; set; } = new();
    /// <summary>グリッドに依存しない自由直線（主回路の母線・結線・注記線）。mm 実座標。</summary>
    public List<FreeLine> FreeLines { get; set; } = new();
    /// <summary>手動で配置する接続点（●）。mm 実座標。</summary>
    public List<ConnectionDot> ConnectionDots { get; set; } = new();
    /// <summary>挿入した画像（BMP/PNG）。mm 実座標。旧ファイルは空配列で互換。</summary>
    public List<ImageInsert> Images { get; set; } = new();
    /// <summary>主回路（動力回路）モード: 左右母線・母線名・自動横配線を描かず、自由直線で結線する。
    /// 旧ファイルは false（=従来の制御回路）で互換。</summary>
    public bool MainCircuit { get; set; }
}

/// <summary>均一グリッドの行数・列数（セルの mm 寸法は DrawingTheme 側）。Row は内部座標のみ。</summary>
public sealed class GridSpec
{
    /// <summary>行数の下限（T-055、殿裁定）。</summary>
    public const int MinRows = 1;
    /// <summary>行数の上限（T-055、殿裁定）。</summary>
    public const int MaxRows = 60;

    /// <summary>列数の下限（T-132、殿裁定2026-07-27＝原本 GuiEcad 準拠）。</summary>
    public const int MinColumns = 2;
    /// <summary>列数の上限（T-132、殿裁定2026-07-27）。
    /// <para>
    /// <b>原本 GuiEcad の上限は 20 だが、ecad2 は 40 を採る。</b> 原本準拠の 20 とすると
    /// <see cref="Columns"/> の既定値 40 と矛盾し、既定のまま作られたシートをシート設定ダイアログで
    /// 表現できなくなるため、上限のみ ecad2 側の既定値に合わせた。
    /// </para>
    /// <para>
    /// <b>UI の新規シート作成が使う 20 とは別物である</b>——`SheetNavigationViewModel` 等は
    /// `new GridSpec { Rows = 10, Columns = 20 }` と明示指定しており、本クラスの既定値 40 を使っていない
    /// （P-162 として起票済み。どちらが意図された既定かの確定は本タスクの範囲外）。
    /// 本定数が縛るのは「ダイアログで入力できる範囲」であって、新規シートの既定寸法ではない。
    /// </para></summary>
    public const int MaxColumns = 40;

    public int Rows { get; set; } = 22;
    public int Columns { get; set; } = 40;
}

/// <summary>左右母線の名称（固定でなく設定可）と電源ラベル。</summary>
public sealed class BusConfig
{
    public string LeftName { get; set; } = "N24";
    public string RightName { get; set; } = "P24";
    public string? PowerLabel { get; set; }
}

/// <summary>右母線右側に記入する行コメント。</summary>
public sealed class RungComment
{
    public int Row { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>横の回路線に付与する回路番号（図面全体通しの連番。自動順送り採番）。</summary>
public sealed class CircuitLine
{
    public int Row { get; set; }
    public int CircuitNumber { get; set; }
}
