using Ecad2.Model;
using Ecad2.Persistence;
using Ecad2.Rendering;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-145（殿ご下命2026-08-06「モーターのデバイス名が大円の中央に表示されない」）:
/// 機器名ラベルを本体大円の中心へ寄せる。<b>殿裁定＝x・y両方／PartId経路の <c>ContactNO</c> 解決は迂回。</b>
/// <para>
/// <b>【期待値に座標の定数を書き写しておらぬ】</b>「ラベルが大円の中心に載る」ことを、
/// <b>実際に描かれた大円の中心と突き合わせて測る</b>——期待値を定数で持てば、
/// <c>SymbolGlyphs</c>／<c>BasicPartTemplates</c> の意匠が動いたときに<b>絵とラベルが揃って
/// ずれても気づけぬ</b>（テストだけが古い位置を守る形になる）。意匠は殿がお描きになったものにて
/// 今後も動きうるゆえ、<b>絵を正として測る形にした。</b>
/// </para>
/// <para>
/// <b>【絶対座標の逆算】</b><see cref="RecordingRenderer"/> の <c>Circles</c> は
/// <c>PushTransform</c> の内側＝要素ローカル座標で記録され、<c>DrawText</c> は外側＝絶対座標である
/// （<c>DiagramRenderer</c> は記号を描いてから <c>PopTransform</c> し、ラベルはその後に描く）。
/// <b>ゆえに直に比べられぬ。</b> <c>Params["LabelDx"]="0"</c> を与えた対照を1つ描き、
/// そのラベル x（＝要素幅の中央の絶対座標）から要素左境界を逆算して橋を架けてある。
/// </para>
/// <para>
/// <b>【測っておらぬこと。縦ずれの手当て後に書き改めた】</b>
/// 初稿では「実機での見え方は測れておらぬ。同型のずれが出る見込みだが値まで同じとは限らぬ」と
/// 区切ってあった。<b>その見込みは当たり、忍者の実機で約1.15mm上ずれと出た</b>
/// （<c>Coil</c> の前例0.22mmは残差にすぎず、アンカー分の全量は1.22mmであったことが後に判明）。
/// <para>
/// 手当て＝モータのラベルのみ <see cref="VAlign.Middle"/> で描く（下の「揃え方」の段で測る）。
/// <b>これで約1.15mmが約0.10〜0.13mmまで縮むが、ゼロにはならぬ</b>——
/// <see cref="VAlign.Middle"/> が合わせるのはレイアウト箱の中心（<c>ft.Height/2</c> ＝ 1.3300mm）で
/// あって墨の中心（英数字のデバイス名で下端から1.202〜1.218mm）ではないゆえ。
/// 侍（英数字6種）と隠密（現実的なデバイス名19種で0.0996〜0.1284mm）が独立に測り一致した。
/// </para>
/// <para>
/// <b>ここで測れるのは揃え方（<see cref="VAlign"/>）と論理座標までである。</b>
/// 残る約0.1mmが実際にどう見えるかは忍者の画素採取に委ねる——
/// <see cref="RecordingRenderer"/> は <c>MeasureText</c> が
/// <c>text.Length * FontSizeMm</c> という粗い近似にて、字形の墨を持たぬゆえ。
/// </para>
/// </para>
/// </summary>
public class T145MotorLabelOffsetTests
{
    private const double Cell = 9.0;                  // RenderOptions.CellMm の既定
    private const double BodyR = 0.75 * Cell;         // 本体大円の半径（横・縦で共通）
    private static readonly GridPos Where = new(3, 4);   // 対称・退化を避けた位置

    // ===== 器そのものの検分 =====

    /// <summary>
    /// <c>ElementCatalog.MotorPartId</c> が組込みモータの Id と一致すること。
    /// <b>綴りを二重に持つゆえの網である</b>——<c>Model</c> 層から <c>Persistence</c> 層を参照すれば
    /// 層の向きが逆になるため写しを置いており、<b>食い違えば迂回が黙って効かなくなる</b>
    /// （絵は正しいのにラベルだけ元の位置へ戻り、どこも例外を出さぬ）。
    /// </summary>
    [Fact]
    public void モータのPartIdは組込みテンプレートのIdと一致する()
        => Assert.Equal(BasicPartTemplates.MotorId, ElementCatalog.MotorPartId);

    /// <summary>モータ以外は横オフセットを持たぬ（従来どおり要素幅の中央）。
    /// <b>向きを渡しても変わらぬ</b>ことを併せて押さえる——3極記号は <c>Orient</c> を持つゆえ、
    /// 向き別の器を入れたことで巻き込まれておらぬかを見る。</summary>
    [Theory]
    [InlineData(ElementKind.ContactNO)]
    [InlineData(ElementKind.Coil)]
    [InlineData(ElementKind.Lamp)]
    [InlineData(ElementKind.Terminal)]
    [InlineData(ElementKind.Breaker3P)]
    public void モータ以外は横オフセットを持たぬ(ElementKind kind)
    {
        Assert.Equal(0.0, ElementCatalog.DefaultLabelDx(kind, null));
        Assert.Equal(0.0, ElementCatalog.DefaultLabelDx(kind, "V"));
        Assert.Equal(0.0, ElementCatalog.DefaultLabelDx(kind, "H"));
    }

    /// <summary>モータ以外の高さオフセットは向きによらず従来値のままであること。
    /// <b>向き引数を足したことで既存の値が動いておらぬ</b>ことの回帰網。</summary>
    [Theory]
    [InlineData(ElementKind.ContactNO, -1.5)]
    [InlineData(ElementKind.ContactNC, -1.5)]
    [InlineData(ElementKind.PushButtonNO, -0.5)]
    [InlineData(ElementKind.Coil, -5.72)]
    [InlineData(ElementKind.Lamp, -1.5)]
    [InlineData(ElementKind.Terminal, -2.0)]
    [InlineData(ElementKind.Breaker3P, 0.0)]
    public void モータ以外の高さオフセットは向きによらず不変(ElementKind kind, double expected)
    {
        Assert.Equal(expected, ElementCatalog.DefaultLabelDy(kind, null));
        Assert.Equal(expected, ElementCatalog.DefaultLabelDy(kind, "V"));
        Assert.Equal(expected, ElementCatalog.DefaultLabelDy(kind, "H"));
    }

    /// <summary>モータは向きによって値が変わること。<b>横と縦で異なる</b>ことだけを押さえ、
    /// 値そのものは下の描画側で「絵と合うか」として測る
    /// ——ここで数値を書けば、意匠が動いたときに二箇所を直す羽目になる。</summary>
    [Fact]
    public void モータのオフセットは向きで変わる()
    {
        Assert.NotEqual(ElementCatalog.DefaultLabelDx(ElementKind.Motor, null),
                        ElementCatalog.DefaultLabelDx(ElementKind.Motor, "V"));
        Assert.NotEqual(ElementCatalog.DefaultLabelDy(ElementKind.Motor, null),
                        ElementCatalog.DefaultLabelDy(ElementKind.Motor, "V"));
    }

    /// <summary><c>"H"</c> はモータには効かぬ（3極記号の値）。<b>綴りを取り違えても横向きへ倒れる</b>
    /// ——<c>SymbolGlyphs</c> の <c>motorVert = orient == "V"</c> と同じ規則である。
    /// <b>両者がずれれば絵とラベルが食い違う</b>が、あちらは <c>Rendering</c> 層の private ゆえ
    /// 直に突き合わせられぬ——ここは規則の側を固定するに留め、<b>実際の一致は下の描画テストが測る</b>。</summary>
    [Fact]
    public void モータの向き判定はV以外を横向きとみなす()
    {
        Assert.Equal(ElementCatalog.DefaultLabelDx(ElementKind.Motor, null),
                     ElementCatalog.DefaultLabelDx(ElementKind.Motor, "H"));
        Assert.Equal(ElementCatalog.DefaultLabelDy(ElementKind.Motor, null),
                     ElementCatalog.DefaultLabelDy(ElementKind.Motor, "H"));
    }

    // ===== ラベル用の種別・向きの解決 =====

    /// <summary>
    /// <b>本タスクの核心</b>——<c>PartId</c> 経路のモータが <see cref="ElementKind.Motor"/> と解けること。
    /// <b>従来は <see cref="ElementKind.ContactNO"/> であった</b>（<c>Kind</c> が既定値のまま置かれ、
    /// <c>CreatesComponent=false</c> ゆえ <c>ComponentKind</c> へも行けぬ）。
    /// <b>ここが解けねば、モータの既定値をいくら入れても届かぬ。</b>
    /// </summary>
    [Fact]
    public void PartId経路のモータはモータとして解ける()
    {
        var e = new ElementInstance { PartId = BasicPartTemplates.MotorId, Pos = Where };

        Assert.Equal(ElementKind.Motor, PartResolver.LabelKind(e, Library()));
        Assert.Equal(ElementKind.ContactNO, e.Kind);   // 生の Kind は既定値のまま＝迂回が効いておる証
    }

    /// <summary><c>Kind</c> 経路のモータも従来どおりモータのままであること（迂回が巻き込んでおらぬ）。</summary>
    [Fact]
    public void Kind経路のモータはモータのまま()
    {
        var e = new ElementInstance { Kind = ElementKind.Motor, Pos = Where };

        Assert.Equal(ElementKind.Motor, PartResolver.LabelKind(e, Library()));
    }

    /// <summary>
    /// 解決できぬ <c>PartId</c> は迂回せぬこと。<b>絵の側も <c>Kind</c> で描かれるゆえ</b>、
    /// ラベルだけモータの位置へ寄れば<b>絵と名が食い違う</b>。
    /// <para>
    /// <b>【本件は迂回のガードを弁別せぬ・壊す実測で判明した】</b>
    /// <c>Kind</c> が既定値 <see cref="ElementKind.ContactNO"/> のときは
    /// <c>CreatesComponent</c> が真になり、<b>迂回の手前で <c>ComponentKind</c> の枝へ抜ける</b>
    /// ——ゆえに迂回側のガード（実体を引けるか）を外しても本件は通ってしまう
    /// （改変Hで実測、25件すべてGREEN）。<b>ガードの検出力は下のテストが負う。</b>
    /// 本件は<b>「第1の枝が守っておる」ことの網</b>として残してある。
    /// </para></summary>
    [Fact]
    public void 解決できぬPartIdは迂回せぬ()
    {
        var e = new ElementInstance { PartId = BasicPartTemplates.MotorId, Pos = Where };

        Assert.Equal(ElementKind.ContactNO, PartResolver.LabelKind(e, new PartLibrary()));
    }

    /// <summary>
    /// <b>非シミュレートの種別でも、解決できぬ <c>PartId</c> は迂回せぬこと。</b>
    /// <para>
    /// <b>【上のテストとの違い＝迂回のガードを実際に弁別する】</b>
    /// <see cref="ElementKind.Breaker3P"/> は <c>CreatesComponent</c> が偽ゆえ<b>迂回の枝まで到達する</b>。
    /// ここで実体を引けるかを見ておらねば、<b>ライブラリを失ったブレーカの名が
    /// モータの位置へ飛ぶ</b>——絵は <c>SymbolGlyphs</c> がブレーカとして描くゆえ、名だけが離れる。
    /// </para>
    /// <para>
    /// <b>【この網は壊す実測から生まれた】</b>当初は上のテスト1件で足ると見ておったが、
    /// ガードを外す改変（改変H）を当てても<b>一件も鳴らなんだ</b>——入力の <c>Kind</c> が
    /// 既定値ゆえ手前の枝で弾かれ、ガードの有無を弁別できておらなんだ。
    /// <b>「探して見つかったテストが、その変更を弁別できるかまで問う」（<c>samurai.md</c>）の実例にござる。</b>
    /// </para></summary>
    [Fact]
    public void 非シミュレート種別でも解決できぬPartIdは迂回せぬ()
    {
        var e = new ElementInstance
        {
            Kind = ElementKind.Breaker3P, PartId = BasicPartTemplates.MotorId, Pos = Where,
        };

        Assert.Equal(ElementKind.Breaker3P, PartResolver.LabelKind(e, new PartLibrary()));
    }

    /// <summary>通常のパーツは従来どおり <c>ComponentKind</c> で解けること（迂回の巻き込み無し）。</summary>
    [Fact]
    public void 通常のパーツは従来どおり役割から解ける()
    {
        var e = new ElementInstance { PartId = BasicPartTemplates.CoilId, Pos = Where };

        Assert.Equal(ElementKind.Coil, PartResolver.LabelKind(e, Library()));
    }

    /// <summary>
    /// <c>PartId</c> 経路では向きを見ぬこと。<b>あちらの絵は <c>PartDrawing.Draw</c> が描き、
    /// 向きを引数に持たぬ</b>——ラベルだけが向きを見れば、絵は横のまま名が縦の位置へ寄る。
    /// </summary>
    [Fact]
    public void PartId経路では向きを見ぬ()
    {
        var e = new ElementInstance { PartId = BasicPartTemplates.MotorId, Pos = Where };
        e.Params[ParamKeys.Orient] = "V";

        Assert.Null(PartResolver.LabelOrient(e, Library()));
    }

    /// <summary><c>Kind</c> 経路では向きをそのまま見ること（縦向きモータはこちらで置かれる）。</summary>
    [Fact]
    public void Kind経路では向きをそのまま見る()
    {
        var e = new ElementInstance { Kind = ElementKind.Motor, Pos = Where };
        e.Params[ParamKeys.Orient] = "V";

        Assert.Equal("V", PartResolver.LabelOrient(e, Library()));
    }

    // ===== 描画——「絵の大円の中心に名が載るか」 =====

    /// <summary>
    /// <b>横向きモータ（<c>PartId</c> 経路＝メニューの「三相モータ 横」・既存ファイルの経路）</b>の
    /// 機器名が、実際に描かれた本体大円の中心に載ること。
    /// </summary>
    [Fact]
    public void 横向きモータの名は大円の中心に載る()
        => AssertLabelSitsOnBodyCircle(MotorElement(partId: BasicPartTemplates.MotorId, orient: null));

    /// <summary>
    /// <b>縦向きモータ（<c>Kind</c> 経路＝メニューの「三相モータ 縦」）</b>も同じく大円の中心に載ること。
    /// <b>縦向きは大円が行中心より1.75セル下に来る</b>ゆえ、横向きと同じ値では合わぬ
    /// ——向き別の器が要った理由がここにある。
    /// </summary>
    [Fact]
    public void 縦向きモータの名も大円の中心に載る()
        => AssertLabelSitsOnBodyCircle(MotorElement(partId: null, orient: "V"));

    /// <summary>
    /// モータ以外は従来どおり要素幅の中央に出ること（回帰）。
    /// <b>横オフセットの器を入れたことで、他の種別が巻き込まれておらぬ</b>ことを押さえる。
    /// </summary>
    [Fact]
    public void モータ以外の名は従来どおり幅の中央に出る()
    {
        var coil = new ElementInstance
        {
            Kind = ElementKind.Coil, Pos = Where, CellWidth = 1, DeviceName = "Y001",
        };
        var withZero = new ElementInstance
        {
            Kind = ElementKind.Coil, Pos = Where, CellWidth = 1, DeviceName = "Y001",
            Params = new() { [ParamKeys.LabelDx] = "0" },
        };

        Assert.Equal(LabelPosition(withZero).X, LabelPosition(coil).X, precision: 9);
    }

    /// <summary>
    /// 個別の <c>Params["LabelDx"]</c> が種別の既定より優先されること（<c>LabelDy</c> と同じ作法）。
    /// <b>既定が効いておるだけの実装でも上のテストは通る</b>——個別値の経路はここでしか測れぬ。
    /// </summary>
    [Fact]
    public void 個別の横オフセットは種別の既定より優先される()
    {
        var byDefault = MotorElement(partId: BasicPartTemplates.MotorId, orient: null);
        var overridden = MotorElement(partId: BasicPartTemplates.MotorId, orient: null);
        overridden.Params[ParamKeys.LabelDx] = "0";

        double moved = LabelPosition(byDefault).X;
        double atCenter = LabelPosition(overridden).X;

        Assert.NotEqual(atCenter, moved);
        // 既定は「幅の中央より右」——大円が要素の右寄り（x=2.0セル、中央は1.5セル）に在るゆえ。
        Assert.True(moved > atCenter, $"既定の名が中央より右に出ておらぬ: 既定={moved} 中央={atCenter}");
    }

    // ===== 揃え方——アンカーが文字の下端でなく箱の中央に来ること =====

    /// <summary>
    /// モータの機器名が <see cref="VAlign.Middle"/> で描かれること（T-145の縦ずれ手当て、殿裁定＝案B）。
    /// <para>
    /// 座標を大円の中心へ合わせても、<see cref="VAlign.Bottom"/> のままでは渡した座標に来るのは
    /// 文字の下端にて、文字は約1.2mm上へ出る（忍者の実機で1.13〜1.16mm、殿の目視で1.2mm）。
    /// 揃え方の側を直すことで、<c>DefaultLabelDy</c> は幾何的な意味を保ったままでよくなる。
    /// </para>
    /// <para>
    /// 横（<c>PartId</c> 経路）と縦（<c>Kind</c> 経路）の両方を測る。経路が違えば
    /// <c>labelKind</c> の解け方も違うゆえ、片方だけでは足りぬ。
    /// </para></summary>
    [Theory]
    [InlineData(BasicPartTemplates.MotorId, null)]
    [InlineData(null, "V")]
    public void モータの名は中央揃えで描かれる(string? partId, string? orient)
        => Assert.Equal(VAlign.Middle, LabelStyle(MotorElement(partId, orient)).VAlign);

    /// <summary>
    /// モータ以外は従来どおり下端揃えのままであること（回帰）。
    /// <para>
    /// 他の種別は <c>DefaultLabelDy</c> の側にアンカー分を織り込んだ実測値を持っておる
    /// （コイルの -5.72 が幾何値 -4.5 より1.22mm深いのがそれにあたる）。
    /// ここで揃え方を変えれば、その織り込みが二重に効いて逆へ1.2mm動く。
    /// </para></summary>
    [Theory]
    [InlineData(ElementKind.Coil)]
    [InlineData(ElementKind.ContactNO)]
    [InlineData(ElementKind.Lamp)]
    [InlineData(ElementKind.Terminal)]
    public void モータ以外の名は下端揃えのまま(ElementKind kind)
    {
        var e = new ElementInstance { Kind = kind, Pos = Where, CellWidth = 1, DeviceName = "Y001" };

        Assert.Equal(VAlign.Bottom, LabelStyle(e).VAlign);
    }

    /// <summary>
    /// 揃え方を変えても、渡す座標そのものは大円の中心を指したままであること。
    /// <para>
    /// 本タスクの手当ては座標ではなく揃え方の側を直すものにて、
    /// <c>DefaultLabelDy</c>／<c>DefaultLabelDx</c> は幾何値のまま据え置く。
    /// ここが崩れれば、揃え方の修正が座標の修正へすり替わったことになる。
    /// </para></summary>
    [Fact]
    public void 揃え方を変えても座標は大円の中心を指す()
    {
        AssertLabelSitsOnBodyCircle(MotorElement(partId: BasicPartTemplates.MotorId, orient: null));
        AssertLabelSitsOnBodyCircle(MotorElement(partId: null, orient: "V"));
    }

    // ===== ヘルパ =====

    private static PartLibrary Library()
    {
        var lib = new PartLibrary();
        foreach (var p in BasicPartTemplates.All()) lib.ById[p.Id] = p;
        return lib;
    }

    private static ElementInstance MotorElement(string? partId, string? orient)
    {
        var e = new ElementInstance
        {
            Kind = partId is null ? ElementKind.Motor : ElementKind.ContactNO,
            Pos = Where,
            CellWidth = 3,
            CellHeight = 2,
            PartId = partId,
            DeviceName = "M1",
        };
        if (orient is not null) e.Params[ParamKeys.Orient] = orient;
        return e;
    }

    private static RecordingRenderer Render(ElementInstance element)
    {
        var sheet = new Sheet { Grid = new GridSpec { Rows = 10, Columns = 20 } };
        sheet.Elements.Add(element);

        var recorder = new RecordingRenderer();
        new DiagramRenderer().Render(recorder, sheet, Library());
        return recorder;
    }

    private static Point2D LabelPosition(ElementInstance element)
        => Render(element).DrawnTextEntries.Single(t => t.Text == element.DeviceName).Position;

    /// <summary>機器名ラベルに用いられた文字スタイル。
    /// <see cref="RecordingRenderer"/> は座標だけでなく <see cref="TextStyle"/> も記録しておるゆえ、
    /// 揃え方（<see cref="VAlign"/>）はテストで測れる——実機を待たずに済む。</summary>
    private static TextStyle LabelStyle(ElementInstance element)
        => Render(element).DrawnTextEntries.Single(t => t.Text == element.DeviceName).Style;

    /// <summary>
    /// 機器名が、実際に描かれた本体大円の中心へ載っていることを確かめる。
    /// <para>
    /// <b>座標系の橋渡し</b>——円は要素ローカル、ラベルは絶対。同じ要素へ
    /// <c>LabelDx="0"</c>／<c>LabelDy="0"</c> を与えた対照を描き、そのラベル位置から
    /// 要素左境界と行中心の絶対座標を逆算する（対照のラベルは定義により
    /// x＝幅の中央、y＝行中心の <c>Cell*0.50</c> 上に出る）。
    /// </para></summary>
    private static void AssertLabelSitsOnBodyCircle(ElementInstance element)
    {
        var recorder = Render(element);
        var label = recorder.DrawnTextEntries.Single(t => t.Text == element.DeviceName).Position;
        var body = recorder.Circles.Single(c => Math.Abs(c.Radius - BodyR) < 1e-9).Center;

        var control = MotorElement(element.PartId, element.Params.GetValueOrDefault(ParamKeys.Orient));
        control.Params[ParamKeys.LabelDx] = "0";
        control.Params[ParamKeys.LabelDy] = "0";
        var origin = LabelPosition(control);

        double leftBoundary = origin.X - element.CellWidth * Cell / 2;
        double rowCenter = origin.Y + Cell * 0.50;

        Assert.Equal(leftBoundary + body.X, label.X, precision: 9);
        Assert.Equal(rowCenter + body.Y, label.Y, precision: 9);
    }
}
