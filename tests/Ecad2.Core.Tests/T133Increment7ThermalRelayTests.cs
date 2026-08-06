using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分7（殿裁定7）: 原本の組み込みパーツ2件（thermal-relay a/b）を
/// <see cref="BasicPartTemplates"/> へ移植したことの回帰テスト。
/// <para>
/// <b>【メニューへは載せぬ。部品リストのみ】</b>殿裁可2026-08-06。
/// 原本の <c>OtherBuiltins</c> 配列に thermal-relay は無く（隠密の調査書§2-2）、
/// <b>原本では左パレット（<c>.gcadpart</c> 同梱パーツ）にのみ在る</b>——
/// 侍の計画書は当初「メニューへ載せる」としておったが、原本を直読して覆した。
/// ゆえに <c>MenuPlacementToolTests</c> の員数（10項目・<c>Skip(4)</c>）は動かぬ。
/// </para>
/// <para>
/// <b>【本テスト群の本命は§Dの上下対称】</b>座標を一つずつ書き写すだけでは、
/// <b>実装からコピーした数字を二度書くだけになり検出力を持たぬ</b>。
/// <b>a と b の「関係」を測れば、数字の書き写しでは得られぬ網になる</b>
/// ——実際、下ごしらえの調査書には「a＝斜線1本／b＝斜線2本」とあったが、
/// 原本を直読すると<b>両者は y の符号だけが違う同型</b>であった（侍、2026-08-06）。
/// <b>その訂正そのものを§Dで固定する。</b>
/// </para>
/// <para>
/// <b>【測っておらぬこと・侍が自ら区切る】</b>実際に描画されたときの見た目
/// （線幅・可読性・電気図面の記号として正しいか）は測っておらぬ——実機確認と殿の目に委ねる。
/// シミュレーション上の振る舞い（<c>Role</c> が導通判定へどう効くか）も本テスト群の外にて、
/// <c>Evaluator</c> 側の既存テストが受け持つ。
/// </para>
/// </summary>
public class T133Increment7ThermalRelayTests
{
    public static IEnumerable<object[]> BothRelays() => new[]
    {
        new object[] { BasicPartTemplates.ThermalRelayNOId },
        new object[] { BasicPartTemplates.ThermalRelayNCId },
    };

    private static PartDefinition Relay(string id)
        => BasicPartTemplates.All().Single(p => p.Id == id);

    private static PartDefinition RelayNO() => Relay(BasicPartTemplates.ThermalRelayNOId);
    private static PartDefinition RelayNC() => Relay(BasicPartTemplates.ThermalRelayNCId);

    private static List<PartLine> Lines(PartDefinition p) => p.Primitives.OfType<PartLine>().ToList();
    private static List<PartCircle> Circles(PartDefinition p) => p.Primitives.OfType<PartCircle>().ToList();

    /// <summary>横線＝両端の y が等しい線。×印の斜線と分ける。</summary>
    private static PartLine Horizontal(PartDefinition p) => Lines(p).Single(l => l.Y1 == l.Y2);
    private static List<PartLine> Diagonals(PartDefinition p) => Lines(p).Where(l => l.Y1 != l.Y2).ToList();

    // ===== 観点A: 実在と、原本どおりの基本属性 =====

    /// <summary>2件が過不足なくテンプレートに在ること。<b>Id の綴り誤りもここで鳴る。</b></summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void サーマルリレーはテンプレートに一件ずつ実在する(string id)
        => Assert.Single(BasicPartTemplates.All().Where(p => p.Id == id));

    /// <summary>原本どおり幅1・高さ1セル。</summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void 幅も高さも一セルである(string id)
    {
        var part = Relay(id);

        Assert.Equal(1, part.WidthCells);
        Assert.Equal(1, part.HeightCells);
    }

    /// <summary>原本どおり L(境界0)・R(境界1) の2端子。<b>行オフセットはどちらも0</b>。</summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void 左右二端子を境界零と一に持つ(string id)
    {
        var ports = Relay(id).Ports;

        Assert.Equal(2, ports.Count);
        Assert.Equal(("L", 0, 0), (ports[0].Name, ports[0].RowOffset, ports[0].BoundaryOffset));
        Assert.Equal(("R", 0, 1), (ports[1].Name, ports[1].RowOffset, ports[1].BoundaryOffset));
    }

    /// <summary>
    /// 原本の <c>role</c> フィールドどおり a＝<see cref="PartRole.ContactNO"/>・
    /// b＝<see cref="PartRole.ContactNC"/> であること。
    /// <b><see cref="PartRole.ThermalOverload"/> を選んでおらぬことを押さえるのが要</b>
    /// ——名称が似た既存の「サーマル(OL)」（コの字形）専用の Role にて、
    /// そちらへ寄せると電気的な振る舞いが原本と変わる。
    /// </summary>
    [Fact]
    public void 役割は原本どおりa接点とb接点である()
    {
        Assert.Equal(PartRole.ContactNO, RelayNO().Role);
        Assert.Equal(PartRole.ContactNC, RelayNC().Role);
    }

    // ===== 観点B: 「書かねば付け忘れと区別できぬ」設定を固定する =====

    /// <summary>
    /// <c>IsOrEligible</c> が偽であること。
    /// <para>
    /// <b>これは意図であって付け忘れではない</b>——<c>Role=ContactNO/NC</c> を持ちながら
    /// OR対象でないのは本テンプレート群で初の組み合わせにて、<b>読み手が最も疑いやすい箇所</b>。
    /// 原本にも該当フィールドは無く、OR論理は a接点／b接点の2件専用である（隠密の検算）。
    /// <b>付ければ部品リストの表示件数が19ではなく21になる。</b>
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void OR対象ではない(string id)
        => Assert.False(Relay(id).IsOrEligible);

    /// <summary>
    /// <see cref="SheetAffinity.Any"/>（既定）であること。
    /// <b>増分6で確立した「枷は3極記号6項目のみ個別、他は編集可否のみ」という構造に乗る証</b>
    /// ——<see cref="SheetAffinity.MainCircuitOnly"/> にすると、
    /// 増分6で殿裁定により解いた経路間の非対称が再燃する。
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void どちらのシートにも置ける(string id)
        => Assert.Equal(SheetAffinity.Any, Relay(id).SheetAffinity);

    // ===== 観点C: 名称衝突への網（原本に「サーマル」が三つある） =====

    /// <summary>
    /// 既存の「サーマル」（コの字形、増分6でメニュー再掲）と Id も表示名も重ならぬこと。
    /// <b>三実体が紛らわしく、忍者が実機で名を頼りに同定する際の手がかりでもある</b>
    /// （忍者の下ごしらえ§1-a）。
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void 既存のサーマルとはIdも表示名も異なる(string id)
    {
        var relay = Relay(id);
        var overload = Relay(BasicPartTemplates.ThermalOverloadId);

        Assert.NotEqual(overload.Id, relay.Id);
        Assert.NotEqual(overload.Name, relay.Name);
    }

    /// <summary>
    /// 表示名が原本の表記ゆれを持ち込んでおらぬこと。
    /// <b>原本は「サーマルリレ-a」（長音が半角ハイフン）・「サーマルリレーｂ」（全角小文字）</b>
    /// ——機械的に写せば表記ゆれごと持ち込む。<b>正字へ整えたことをここで固定する</b>（隠密の具申）。
    /// </summary>
    [Fact]
    public void 表示名は原本の表記ゆれを正してある()
    {
        Assert.Equal("サーマルリレーa", RelayNO().Name);
        Assert.Equal("サーマルリレーb", RelayNC().Name);
    }

    // ===== 観点D: 図形（本テスト群の本命） =====

    /// <summary>
    /// 図形が円2つ・線3本の計5件であること。
    /// <b>下ごしらえの調査書は「a＝斜線1本／b＝斜線2本」としておったが、原本はどちらも斜線2本</b>
    /// ——線の内訳は「横線1本＋斜線2本」で a・b とも同じ。
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void 図形は円二つと線三本の計五件である(string id)
    {
        var part = Relay(id);

        Assert.Equal(5, part.Primitives.Count);
        Assert.Equal(2, Circles(part).Count);
        Assert.Single(Lines(part).Where(l => l.Y1 == l.Y2));      // 横線1本
        Assert.Equal(2, Diagonals(part).Count);                    // ×印2本
    }

    /// <summary>
    /// 端子円が左右対称（x=0.5 を軸に）で、同じ半径であること。
    /// <b>記号としての妥当性を測る</b>——片方の円だけ位置や大きさを誤れば鳴る。
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void 端子円は左右対称で同じ大きさである(string id)
    {
        var circles = Circles(Relay(id));

        Assert.Equal(2, circles.Count);
        Assert.All(circles, c => Assert.Equal(0.0, c.Cy));                 // 行中心線上
        Assert.Equal(circles[0].R, circles[1].R);
        Assert.Equal(1.0, circles[0].Cx + circles[1].Cx);                  // x=0.5 を軸に対称
    }

    /// <summary>
    /// <b>×印は横線の上下に在るのではなく、横線を中心にまたぐこと。</b>
    /// <para>
    /// <b>下ごしらえの調査書の「×印（横線の上／下）」という記述を正した点である</b>
    /// ——「上／下」が指すのは<b>横線そのものの位置</b>（a が上側・b が下側）にて、
    /// ×印の位置ではない。a なら横線 y=-0.1875 に対し×印は y=-0.3125〜-0.0625 で、
    /// <b>その中心が横線に一致する</b>。
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(BothRelays))]
    public void バツ印は横線を中心にまたぐ(string id)
    {
        var part = Relay(id);
        var diagonals = Diagonals(part);
        var ys = diagonals.SelectMany(l => new[] { l.Y1, l.Y2 }).ToList();

        Assert.Equal(Horizontal(part).Y1, ys.Average());
        Assert.True(ys.Min() < Horizontal(part).Y1, "×印が横線の片側へ寄っておる");
        Assert.True(ys.Max() > Horizontal(part).Y1, "×印が横線の片側へ寄っておる");
    }

    /// <summary>
    /// a の横線は行中心線より上（y が負）、b は下（y が正）にあること。
    /// <b>忍者が実機で a／b を見分ける手がかりそのもの</b>——
    /// <b>斜線の本数では見分けられぬ（両者とも2本）</b>ゆえ、横線の位置が唯一の判別点になる
    /// （侍の訂正を受け、忍者が判別手順を改めた点）。
    /// </summary>
    [Fact]
    public void 横線の位置がaとbを分ける()
    {
        Assert.True(Horizontal(RelayNO()).Y1 < 0, "a の横線は上側（y が負）にあるはず");
        Assert.True(Horizontal(RelayNC()).Y1 > 0, "b の横線は下側（y が正）にあるはず");
    }

    /// <summary>
    /// <b>【本テスト群の要】a の y をすべて符号反転すると、b の図形と完全に一致すること。</b>
    /// <para>
    /// <b>座標を一つずつ書き写すテストより強い</b>——実装からコピーした数字を二度書くのでは
    /// 検出力を持たぬが、<b>a と b の「関係」は片方だけを誤れば必ず崩れる</b>。
    /// a の斜線を1本消しても、b の横線を動かしても、円の半径を片方だけ変えても鳴る。
    /// </para>
    /// <para>
    /// <b>【線分は端点の順序に依らぬ形へ正規化する】</b>原本では a と b で端点の書き順が
    /// 入れ替わっており（a の斜線2 を反転したものが b の斜線1 に当たる）、
    /// <b>順序をそのまま比べると一致せぬ</b>。線分としての同一性を測るのが目的ゆえ正規化する。
    /// </para>
    /// <para>
    /// <b>【浮動小数の厳密比較で差し支えない】</b>用いる値はすべて 2 の冪分の整数
    /// （0.0625=1/16・0.1875=3/16・0.3125=5/16・0.375=3/8・0.625=5/8・0.875=7/8）にて
    /// <c>double</c> で正確に表せ、符号反転でも誤差が出ぬ。
    /// </para>
    /// </summary>
    [Fact]
    public void aのyを反転するとbの図形と一致する()
    {
        var flippedA = RelayNO().Primitives.Select(FlipY).ToHashSet();
        var b = RelayNC().Primitives.Select(Normalize).ToHashSet();

        Assert.Equal(b, flippedA);
    }

    /// <summary>y を符号反転し、線分は端点の順序に依らぬ形へ揃える。</summary>
    private static PartPrimitive FlipY(PartPrimitive p) => p switch
    {
        PartLine l => Normalize(new PartLine(l.X1, -l.Y1, l.X2, -l.Y2)),
        PartCircle c => new PartCircle(c.Cx, -c.Cy, c.R),
        _ => throw new InvalidOperationException($"想定しておらぬ図形: {p.GetType().Name}"),
    };

    /// <summary>線分の端点を辞書順で並べ替え、書き順の違いを吸収する。</summary>
    private static PartPrimitive Normalize(PartPrimitive p) => p switch
    {
        PartLine l => (l.X1, l.Y1).CompareTo((l.X2, l.Y2)) <= 0
            ? new PartLine(l.X1, l.Y1, l.X2, l.Y2)
            : new PartLine(l.X2, l.Y2, l.X1, l.Y1),
        _ => p,
    };
}
