using System.Text.Json.Serialization;

namespace Ecad2.Model;

/// <summary>自作パーツの電気的役割（テストモード挙動）。NetlistBuilder で既定種別へ写像する。</summary>
public enum PartRole
{
    ContactNO, ContactNC, Coil, Lamp, Terminal, NonSimulated, InputNO, InputNC,
    // T-071: 経路B部品追加（押釦以外の残り6種）。既存Roleへの丸めは電気的挙動・機器分類双方が
    // 劣化するため、専用Roleとして追加する（着手前調査 docs/ecad2-t071-part-addition-design-onmitsu2.md 2節）。
    TimerContactNO, TimerContactNC, TimerInstantContactNO, TimerInstantContactNC,
    ThermalOverload, EmergencyStop,
    // T-061 A-1構造対処: セレクトSWは配置経路でRole=ContactNOとして生成されていたためElementKind.
    // SelectSwitchへのマッピング経路が存在せず、電気的導通判定(Evaluator.IsConducting)のノッチ専用
    // 分岐がデッドコード化していた(docs/ecad2-t061-a1-select-switch-design-onmitsu.md 1節)。専用Role
    // を追加しマッピングを完成させる。
    SelectSwitch,
}

/// <summary>
/// 部品を置けるシートの種別（T-136(A)、殿裁定2026-07-31＝3値・既定は <see cref="Any"/>）。
/// <para>
/// 原本GuiEcadは3極部品を制御回路シートへ置けてしまい、DRCも効かぬまま中心接続点を左右母線へ
/// 引いていた。ecad2はその構図を引き継いでおり、本enumはその枷を土台から入れるためのもの。
/// </para>
/// <para>
/// <b>既定を <see cref="Any"/> としたのは段取りのため</b>——既存の組込み15件・自作部品を
/// 無改修のまま従来どおり置けるようにし、殿が絞りたいものから順に設定なされる運用を採る
/// （殿裁定＝3値。2値＝全部品がどちらかに属す案は、既存部品すべての設定が揃うまで使えぬため不採用）。
/// </para>
/// </summary>
public enum SheetAffinity
{
    /// <summary>どちらのシートにも置ける（既定）。</summary>
    Any,
    /// <summary>制御回路シート専用（<see cref="Sheet.MainCircuit"/> が false のシートのみ）。</summary>
    ControlOnly,
    /// <summary>主回路シート専用（<see cref="Sheet.MainCircuit"/> が true のシートのみ）。</summary>
    MainCircuitOnly,
}

/// <summary>
/// パーツ図形プリミティブ（パーツローカル座標＝セル単位。原点=最左ポート点・行中心線=y0、+x右/+y下）。
/// JSON 多態シリアライズ対応。
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PartLine), "line")]
[JsonDerivedType(typeof(PartCircle), "circle")]
[JsonDerivedType(typeof(PartArc), "arc")]
[JsonDerivedType(typeof(PartRect), "rect")]
[JsonDerivedType(typeof(PartPolyline), "polyline")]
[JsonDerivedType(typeof(PartText), "text")]
public abstract record PartPrimitive;

public sealed record PartLine(double X1, double Y1, double X2, double Y2) : PartPrimitive;
public sealed record PartCircle(double Cx, double Cy, double R) : PartPrimitive;
public sealed record PartArc(double Cx, double Cy, double R, double StartDeg, double SweepDeg, double Ry = 0, double Rot = 0) : PartPrimitive
{
    /// <summary>縦半径。0 以下なら真円弧（=横半径 R）として扱う（旧データ後方互換）。</summary>
    [JsonIgnore] public double EffRy => Ry > 0 ? Ry : R;
}
public sealed record PartRect(double X, double Y, double W, double H, double Rot = 0) : PartPrimitive;   // Rot=中心まわり回転（度）
public sealed record PartPolyline(double[] Points) : PartPrimitive;   // x0,y0,x1,y1,...
public sealed record PartText(string Text, double X, double Y, double SizeCells = 0.25) : PartPrimitive;

/// <summary>
/// 自作パーツの定義。基準範囲(W×Hセル)の枠内に図形を描き、接続点(境界ノード)を持つ。
/// 接続点は既存の <see cref="PortDef"/>（境界ノード一致で結線）を再利用する。
/// </summary>
public sealed class PartDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    /// <summary>基準範囲（外形枠）の幅・高さ（セル単位）。</summary>
    public int WidthCells { get; set; } = 1;
    public int HeightCells { get; set; } = 1;
    public PartRole Role { get; set; } = PartRole.ContactNO;
    /// <summary>部品選択リストのOR入力（ORa/ORb）対象とするか。電気的Role（ContactNO/NC）とは
    /// 独立した分類——セレクトSW等はシミュレーション上ContactNO扱いだがOR対象ではないため、
    /// Role単独では区別できない（T-037往復2周目）。</summary>
    public bool IsOrEligible { get; set; }
    /// <summary>置けるシートの種別（T-136(A)）。既定は <see cref="Model.SheetAffinity.Any"/>。
    /// <para>
    /// 永続化＝<c>JsonSerializer</c> による自動反映（<c>PartLibrarySerializer</c> は手書きの
    /// マッピングを持たぬ）。旧 <c>.gcadpart</c> に本フィールドが無ければ既定値のまま読まれる。
    /// </para></summary>
    public SheetAffinity SheetAffinity { get; set; } = SheetAffinity.Any;
    /// <summary>クロスリファレンス検査（<c>DRC-XREF-001</c>／<c>DRC-XREF-002</c>）から除外するか
    /// （T-152、殿ご裁可2026-08-16＝案B）。既定 false。
    /// <para>
    /// <b>【何のために要るか】</b>電磁弁のソレノイドは電気的にはコイルにて、役割も <c>Coil</c> が正しい。
    /// されどリレーのコイルとは違い<b>対応する接点を持たぬ</b>ゆえ、死にリレー検査
    /// （<c>コイルがありますが接点が図面上に一つもありません</c>）が消せぬ警告として出続けておった。
    /// 役割を正した結果として現れる警告にて、役割の側を歪めて黙らせるのは筋が違う——ゆえに
    /// <b>役割とは直交する印</b>を一つ置き、検査の側で除外する形を採った。
    /// </para>
    /// <para>
    /// <b>【役割の選択肢を増やす案は退けられておる】</b>「コイル（接点なし）」という
    /// <see cref="PartRole"/> を新設する案もあったが、<c>PartResolver.ComponentKind</c> の switch が
    /// 未対応の役割で例外を投げる設計ゆえ、<b>洗い出し漏れがコンパイルエラーでなく実行時例外として
    /// 現れる</b>（隠密の見立て）。加えて <see cref="IsOrEligible"/> と同じ「役割に依らぬ直交フラグ」の
    /// 思想とも競合する。殿は案Bをお選びになった。
    /// </para>
    /// <para>
    /// 永続化＝<c>JsonSerializer</c> による自動反映（<see cref="SheetAffinity"/> と同じ）。
    /// <b>本フィールドを持たぬ旧 <c>.gcadpart</c> は既定の false で読まれる</b>——すなわち
    /// 既存のパーツはすべて従来どおり検査の対象に留まる。
    /// </para></summary>
    public bool IsExcludedFromCrossReference { get; set; }
    /// <summary>接続点。2端子役割は先頭=NetA・末尾=NetB（境界オフセット昇順を想定）。</summary>
    public List<PortDef> Ports { get; set; } = new();
    public List<PartPrimitive> Primitives { get; set; } = new();
}

/// <summary>自作パーツのライブラリ（Id→定義）。ドキュメント埋め込み／外部JSONの両方で持てる。</summary>
public sealed class PartLibrary
{
    public Dictionary<string, PartDefinition> ById { get; set; } = new();

    public PartDefinition? Get(string? id) => id is not null && ById.TryGetValue(id, out var d) ? d : null;
}
