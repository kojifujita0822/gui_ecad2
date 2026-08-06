using Ecad2.Model;

namespace Ecad2.Rendering;

/// <summary>
/// 種別ごとの記号グリフ（ローカル座標）。原点=要素の最左ポート点、行中心線=y0、+x右・+y下。
/// 形状はユーザー提供の Jw_cad 図（temp の個別DXF）に準拠し、1セルに収まるよう正規化済み。
/// 各記号は中央 cx を基準に描き、リード線（横配線）は <see cref="Leads"/> で母線側へ伸ばす。
/// 座標値は「中心基準・セル単位」の実測値（DXFを cell=150 で割り、1セルへフィットしたもの）。
/// </summary>
internal static class SymbolGlyphs
{
    public static void Draw(IRenderer r, StrokeStyle s, ElementKind kind, double width, double cell,
                            Color? manualFill = null, string? variant = null, string? orient = null)
    {
        bool horiz = orient == "H";   // 主回路3極記号の向き（既定=縦流れ V）。
        // 三相モータの向き（T-133増分8、殿裁定5）。同じ Params[Orient] を見ながら、3極記号とは
        // 既定が逆である——3極記号は V が既定で H が別、モータは横向き（端子が縦に3つ）が既定で
        // V が別。既存データは Orient を持たぬゆえ、V 以外はすべて従来の描画へ倒れる。
        bool motorVert = orient == "V";
        double cx = width / 2;

        switch (kind)
        {
            case ElementKind.ContactNO: ContactNO(r, s, cx, width, cell, manualFill); break;
            case ElementKind.ContactNC: ContactNC(r, s, cx, width, cell, manualFill); break;

            case ElementKind.PushButtonNO: PushButtonNO(r, s, cx, width, cell); break;
            case ElementKind.PushButtonNC: PushButtonNC(r, s, cx, width, cell); break;
            case ElementKind.EmergencyStop: EmergencyStop(r, s, cx, width, cell); break;

            case ElementKind.SelectSwitch: SelectSwitch(r, s, cx, width, cell); break;

            case ElementKind.TimerContactNO: TimerContactNO(r, s, cx, width, cell); break;
            case ElementKind.TimerContactNC: TimerContactNC(r, s, cx, width, cell); break;

            // タイマ瞬時接点: 記号は通常接点と同形（JIS慣行）。機器名 TIM で判別。
            case ElementKind.TimerInstantContactNO: ContactNO(r, s, cx, width, cell, manualFill); break;
            case ElementKind.TimerInstantContactNC: ContactNC(r, s, cx, width, cell, manualFill); break;

            case ElementKind.ThermalOverload: Thermal(r, s, cx, width, cell); break;

            case ElementKind.Coil: Coil(r, s, cx, width, cell); break;
            case ElementKind.Timer: TimerCoil(r, s, cx, width, cell); break;
            case ElementKind.Lamp: Lamp(r, s, cx, width, cell); break;
            case ElementKind.Terminal: Terminal(r, s, cx, width, cell); break;
            case ElementKind.Motor: Motor(r, s, width, cell, motorVert); break;

            case ElementKind.Breaker3P: Breaker3P(r, s, width, variant, horiz); break;
            case ElementKind.ContactorMain3P: ContactorMain3P(r, s, width, horiz); break;
            case ElementKind.ThermalOverload3P: ThermalOverload3P(r, s, width, horiz); break;

            default:
                Leads(r, s, cx, width, cell, 0.3);
                r.DrawRectangle(new(cx - cell * 0.3, -cell * 0.2, cell * 0.6, cell * 0.4), s);
                break;
        }
    }

    // ===== 描画ヘルパ（中心基準・セル単位 → ローカル mm）=====

    private static void L(IRenderer r, StrokeStyle s, double cx, double cell,
        double x1, double y1, double x2, double y2)
        => r.DrawLine(new(cx + x1 * cell, y1 * cell), new(cx + x2 * cell, y2 * cell), s);

    private static void C(IRenderer r, StrokeStyle s, double cx, double cell, double x, double y, double rad)
        => r.DrawCircle(new(cx + x * cell, y * cell), rad * cell, s);

    /// <summary>左母線側(0)と右母線側(width)から本体端 ±half まで横配線（リード線）を引く。</summary>
    private static void Leads(IRenderer r, StrokeStyle s, double cx, double width, double cell, double half)
    {
        r.DrawLine(new(0, 0), new(cx - half * cell, 0), s);
        r.DrawLine(new(cx + half * cell, 0), new(width, 0), s);
    }

    // ===== 各記号（座標は temp/appstyle.py の正規化出力に一致）=====

    // a接点(NO): 2本の縦ブレード。manualFill 指定時はブレード間を塗りつぶす（手動強制マーク）。
    private static void ContactNO(IRenderer r, StrokeStyle s, double cx, double width, double cell,
                                  Color? manualFill = null)
    {
        Leads(r, s, cx, width, cell, 0.158);
        if (manualFill is Color fc)
            r.FillRectangle(new(cx - 0.158 * cell, -0.317 * cell, 0.316 * cell, 0.634 * cell), fc);
        L(r, s, cx, cell, -0.158, -0.317, -0.158, 0.317);
        L(r, s, cx, cell, 0.158, -0.317, 0.158, 0.317);
    }

    // b接点(NC): 2ブレード＋斜線。manualFill 指定時はブレード間を塗りつぶす（手動強制マーク）。
    private static void ContactNC(IRenderer r, StrokeStyle s, double cx, double width, double cell,
                                  Color? manualFill = null)
    {
        Leads(r, s, cx, width, cell, 0.1875);
        if (manualFill is Color fc)
            r.FillRectangle(new(cx - 0.1875 * cell, -0.3125 * cell, 0.375 * cell, 0.625 * cell), fc);
        L(r, s, cx, cell, -0.1875, -0.3125, -0.1875, 0.3125);
        L(r, s, cx, cell, 0.1875, -0.3125, 0.1875, 0.3125);
        L(r, s, cx, cell, 0.42, -0.42, -0.42, 0.42);
    }

    // タイマ接点(NO): 端子円(中心線上)＋上の限時バー＋上向き△
    private static void TimerContactNO(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.460);
        C(r, s, cx, cell, -0.307, 0, 0.153);
        C(r, s, cx, cell, 0.307, 0, 0.153);
        L(r, s, cx, cell, -0.460, -0.245, 0.460, -0.245);
        L(r, s, cx, cell, 0, -0.399, -0.089, -0.245);
        L(r, s, cx, cell, 0, -0.399, 0.089, -0.245);
    }

    // タイマ接点(NC): 端子円(中心線上)＋下の限時バー＋下向き△
    private static void TimerContactNC(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.460);
        C(r, s, cx, cell, -0.307, 0, 0.153);
        C(r, s, cx, cell, 0.307, 0, 0.153);
        L(r, s, cx, cell, -0.460, 0.153, 0.460, 0.153);
        L(r, s, cx, cell, 0, 0, -0.089, 0.153);
        L(r, s, cx, cell, 0, 0, 0.089, 0.153);
    }

    // 押釦(NO): 端子円(中心線上)＋上の可動バー＋ステム
    private static void PushButtonNO(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.420);
        C(r, s, cx, cell, -0.280, 0, 0.140);
        C(r, s, cx, cell, 0.280, 0, 0.140);
        L(r, s, cx, cell, -0.420, -0.280, 0.420, -0.280);
        L(r, s, cx, cell, 0, -0.280, 0, -0.420);
    }

    // 押釦(NC): 端子円(中心線上)＋下の橋絡バー＋ステム
    private static void PushButtonNC(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.460);
        C(r, s, cx, cell, -0.307, 0, 0.153);
        C(r, s, cx, cell, 0.307, 0, 0.153);
        L(r, s, cx, cell, -0.460, 0.153, 0.460, 0.153);
        L(r, s, cx, cell, 0, 0.153, 0, -0.307);
    }

    // 非常停止: 押釦(NC形)＋ドーム(キノコ頭)
    private static void EmergencyStop(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.460);
        C(r, s, cx, cell, -0.307, 0, 0.153);
        C(r, s, cx, cell, 0.307, 0, 0.153);
        L(r, s, cx, cell, -0.460, 0.153, 0.460, 0.153);
        L(r, s, cx, cell, 0, 0.153, 0, -0.380);
        Span<Point2D> dome = stackalloc Point2D[]
        {
            new(cx - 0.373 * cell, -0.239 * cell), new(cx - 0.311 * cell, -0.289 * cell),
            new(cx - 0.239 * cell, -0.329 * cell), new(cx - 0.160 * cell, -0.358 * cell),
            new(cx - 0.076 * cell, -0.375 * cell), new(cx + 0.010 * cell, -0.380 * cell),
            new(cx + 0.096 * cell, -0.372 * cell), new(cx + 0.179 * cell, -0.352 * cell),
            new(cx + 0.256 * cell, -0.321 * cell), new(cx + 0.326 * cell, -0.278 * cell),
            new(cx + 0.373 * cell, -0.239 * cell),
        };
        r.DrawPolyline(dome, s);
    }

    // セレクトSW: 端子円(中心線上)＋段付きハンドル
    private static void SelectSwitch(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.5);
        C(r, s, cx, cell, -0.375, 0, 0.125);
        C(r, s, cx, cell, 0.375, 0, 0.125);
        L(r, s, cx, cell, -0.25, 0, -0.0625, 0);     // 左リード（円→左接片）
        L(r, s, cx, cell, 0.0625, 0, 0.25, 0);       // 右リード（右接片→円）
        L(r, s, cx, cell, -0.0625, 0, -0.0625, -0.1875);   // 左接片（縦）
        L(r, s, cx, cell, 0.0625, 0, 0.0625, -0.1875);     // 右接片（縦）
    }

    // サーマル(OL): 端子円＋斜線（暫定: 端子台に準ずる）。専用DXF未提供のため従来形を維持。
    private static void Thermal(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        double w = cell * 0.26, top = -cell * 0.30;
        Leads(r, s, cx, width, cell, 0.26);
        L(r, s, cx, cell, -0.26, 0, -0.26, top / cell);
        L(r, s, cx, cell, -0.26, top / cell, 0.26, top / cell);
        L(r, s, cx, cell, 0.26, top / cell, 0.26, 0);
    }

    // コイル: 円(r=0.420)
    private static void Coil(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.420);
        C(r, s, cx, cell, 0, 0, 0.420);
    }

    // タイマコイル: コイル円＋上向き△（限時要素マーク）
    private static void TimerCoil(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.420);
        C(r, s, cx, cell, 0, 0, 0.420);
        L(r, s, cx, cell, 0, -0.550, -0.089, -0.420);
        L(r, s, cx, cell, 0, -0.550,  0.089, -0.420);
        L(r, s, cx, cell, -0.089, -0.420, 0.089, -0.420);
    }

    // 表示灯: 円(r=0.323)＋外向き4放射線
    private static void Lamp(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.323);
        C(r, s, cx, cell, 0, 0, 0.323);
        L(r, s, cx, cell, 0.275, 0.275, 0.420, 0.420);
        L(r, s, cx, cell, -0.275, 0.275, -0.420, 0.420);
        L(r, s, cx, cell, -0.275, -0.275, -0.420, -0.420);
        L(r, s, cx, cell, 0.275, -0.275, 0.420, -0.420);
    }

    // 端子台: 円(r=0.15)＋斜線貫通（直径0.3セル）
    private static void Terminal(IRenderer r, StrokeStyle s, double cx, double width, double cell)
    {
        Leads(r, s, cx, width, cell, 0.15);
        C(r, s, cx, cell, 0, 0, 0.15);
        L(r, s, cx, cell, -0.17, 0.17, 0.17, -0.17);
    }

    // 三相モータ: 大円＋左に縦3端子(⊘)＋リード。U/V/W 端子は 1セル間隔（y=-1/0/1・グリッド線上）に
    // 揃え、上流の主回路記号（接触器・サーマル等）の極とまっすぐつながるようにする。k = width/3 = 1セル。
    //
    // 【意匠は殿がお描きになったもの】sample/モーター(横).gcadparts（殿裁定2026-08-06＝一括で採る）。
    // 旧意匠（端子 r=0.18・大円 r=0.92・U/W が一直線の斜め）から、一回り小さく・リードは折れ線へ改めた。
    //
    // 【入線はこちらにのみ在る】殿の図柄は接続点を持たぬ形（ports:[]）ゆえ入線も無いが、Kind 経路は
    // ElementCatalog.Ports(Motor) の U/V/W を持つ（殿裁定5「ポート定義は不変」）。接続点から端子まで
    // 線が届かねば隙間が空くゆえ、こちらだけ入線3本を残す。PartId 経路（BasicPartTemplates.Motor）は
    // 殿の図柄どおり入線なし——両経路の絵は、この3本の分だけ食い違う（殿裁定＝案C、承知のうえの選択）。
    //
    // 【U/W のリードが左右非対称なのは殿の図柄のまま】終点は U(1.5,-0.5625)／W(1.4375,0.5) にて、
    // 厳密な対称なら W は (1.5,0.5625) になる。いずれも大円の周（中心からの距離 0.7526）に載っており
    // 実害は無いが、揃えれば殿の意匠を改めることになるゆえ、そのまま写した。
    private static void Motor(IRenderer r, StrokeStyle s, double width, double cell, bool vert)
    {
        if (vert) { MotorV(r, s, width); return; }
        double k = width / 3.0;
        void M(double x1, double y1, double x2, double y2) => r.DrawLine(new(x1 * k, y1 * k), new(x2 * k, y2 * k), s);
        void O(double x, double y, double rad) => r.DrawCircle(new(x * k, y * k), rad * k, s);

        const double xT = 0.25, rT = 0.125;   // 端子 ⊘ の x 位置・半径
        const double xC = 2.0, rB = 0.75;     // 本体大円の中心 x・半径

        O(xC, 0, rB);                        // 本体大円
        foreach (var y in new[] { -1.0, 0.0, 1.0 })
        {
            M(0, y, xT - rT, y);                       // 入線（左境界→端子。Kind 経路のみ）
            O(xT, y, rT);                              // 端子 ○
            M(xT + rT, y - rT, xT - rT, y + rT);       // ⊘ 斜線
        }
        M(xT + rT, 0, xC - rB, 0);           // リード V→本体（水平直結）
        M(xT + rT, -1, 1.0, -1);             // リード U（水平）
        M(1.0, -1, 1.5, -0.5625);            // リード U（斜め）→本体左上
        M(xT + rT, 1, 1.0, 1);               // リード W（水平）
        M(1.0, 1, 1.4375, 0.5);              // リード W（斜め）→本体左下
    }

    // 三相モータ（縦向き・T-133増分8、殿裁定5）: 端子が横に3つ・下に本体大円。
    // 上の横向き（＝殿の新しい意匠）を時計回りに90度回し、端子を行中心へ揃えた形
    // ＝写像 (x, y) → (-y + 1, x - 0.25)。寸法は横向きから一切変えておらぬ。
    //
    // 【回す向きは殿裁定2026-08-06】主回路の流れは上から下（電源→ブレーカ→接触器→サーマル→モータ）
    // であり、モータは終端ゆえ外部配線を上から受けて本体が下に来る。3極記号の縦流れ（y∈[-1,+1] の
    // 上→下）とも揃う。反時計回り（大円が上）で一度実装したものを、この筋により改めた。
    //
    // 【この向きで初めて描画とポート定義が噛み合う】端子が列境界 0/1/2・行中心 y=0 に載るゆえ、
    // ElementCatalog.Ports(Motor) の U/V/W（RowOffset 0・BoundaryOffset 0/1/2）とちょうど重なる。
    // 横向きは端子を縦に描きながらポートは横並びで、両者が直交しておった（既知の食い違い）。
    // なお時計回りゆえ、横向きで上にあった U が右端（x=2）へ来る。端子の絵は3つとも同一で
    // 名前を描き分けておらぬゆえ見た目に差は出ぬが、U/W の並びが横向きとは逆順になる。
    //
    // 【縦の広がりが器を超える】y は -0.25（入線上端）から +2.5（大円下端）＝約2.75セル分に及び、
    // ElementCatalog.DefaultCellHeight(Motor) = 2 に収まらぬ（旧意匠では2.97セル分であった）。
    // 縮めれば収まるが、それは「殿の意匠を90度回した形」ではなくなる。
    private static void MotorV(IRenderer r, StrokeStyle s, double width)
    {
        double k = width / 3.0;
        void M(double x1, double y1, double x2, double y2) => r.DrawLine(new(x1 * k, y1 * k), new(x2 * k, y2 * k), s);
        void O(double x, double y, double rad) => r.DrawCircle(new(x * k, y * k), rad * k, s);

        const double rT = 0.125;                       // 端子 ⊘ の半径（端子の中心は行中心 y=0）
        const double xC = 1.0, yC = 1.75, rB = 0.75;   // 本体大円の中心・半径

        O(xC, yC, rB);                       // 本体大円
        foreach (var x in new[] { 0.0, 1.0, 2.0 })
        {
            M(x, -0.25, x, -rT);             // 入線（上＝外部配線側→端子）
            O(x, 0, rT);                     // 端子 ○
            M(x + rT, rT, x - rT, -rT);      // ⊘ 斜線
        }
        M(xC, rT, xC, yC - rB);              // リード V→本体（垂直直結）
        M(2.0, rT, 2.0, 0.75);               // リード U（垂直）
        M(2.0, 0.75, 1.5625, 1.25);          // リード U（斜め）→本体右上
        M(0.0, rT, 0.0, 0.75);               // リード W（垂直）
        M(0.0, 0.75, 0.5, 1.1875);           // リード W（斜め）→本体左上
    }

    // ===== 主回路（三相動力）用 3極記号（sample.png 準拠・2×2セル）=====
    // 原点=左境界・行中心 y0、+x右・+y下。1 単位 = k = width/2（CellWidth=2）＝1セル。
    // 極の間隔はグリッドピッチ（1セル）に一致させ、母線（自由直線）がグリッド線上で極に重なるようにする。
    // 向きは配置時に確定（縦流れ V / 横流れ H・切替不可）。「流れ軸 f」と「極の並び軸」を入れ替える。
    //   縦(V): 極は x=0/1/2（列境界＝縦グリッド線上）、流れは y∈[-1,1]（上→下）。
    //   横(H): 極は y=-1/0/1（1セル間隔・アンカー行中心）、流れは x∈[0,2]（左→右、実図面準拠）。
    private static readonly double[] PoleAcrossV = { 0.0, 1.0, 2.0 };
    private static readonly double[] PoleAcrossH = { -1.0, 0.0, 1.0 };

    // 1極ぶんの座標マッパ。(f=流れ位置, g=流れに直交する極中心からのオフセット) を実 mm へ写す。
    private readonly struct Pole
    {
        private readonly IRenderer _r; private readonly StrokeStyle _s;
        private readonly bool _h; private readonly double _k; private readonly double _c;
        public Pole(IRenderer r, StrokeStyle s, bool h, double k, double c)
        { _r = r; _s = s; _h = h; _k = k; _c = c; }
        private Point2D P(double f, double g) => _h ? new(f * _k, (_c + g) * _k) : new((_c + g) * _k, f * _k);
        public void L(double f1, double g1, double f2, double g2) => _r.DrawLine(P(f1, g1), P(f2, g2), _s);
        public void Circ(double f, double g, double rad) => _r.DrawCircle(P(f, g), rad * _k, _s);
        public void Rect(double f1, double g1, double f2, double g2)
        {
            var a = P(f1, g1); var b = P(f2, g2);
            _r.DrawRectangle(new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y),
                                 Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)), _s);
        }
        // 上に膨らむ弧（端子間をまたぐ ∩）をポリラインで近似。頂点は g = -ha。
        public void Arc(double fL, double fR, double ha, int n = 10)
        {
            double fc = (fL + fR) / 2, d = (fR - fL) / 2;
            double pf = fL, pg = 0;
            for (int i = 1; i <= n; i++)
            {
                double th = Math.PI * i / n;
                double f = fc - d * Math.Cos(th), g = -ha * Math.Sin(th);
                L(pf, pg, f, g); pf = f; pg = g;
            }
        }
    }

    // 連動（ガング）線を3極の across 軸方向に1本引く（f 固定・極中心+gのライン）。
    private static void Gang(IRenderer r, StrokeStyle s, bool h, double k, double[] poles, double f, double g)
    {
        Point2D Pt(double c) => h ? new(f * k, (c + g) * k) : new((c + g) * k, f * k);
        r.DrawLine(Pt(poles[0]), Pt(poles[2]), s);
    }

    // 配線用遮断器（NFB/MCCB/ELB）: 各極=左右の端子○＋上をまたぐ弧、3極の弧頂点を縦線で連動。
    // ELB はテストボタン印付き。
    private static void Breaker3P(IRenderer r, StrokeStyle s, double width, string? variant, bool h)
    {
        double k = width / 2.0;
        var poles = h ? PoleAcrossH : PoleAcrossV;
        double fMin = h ? 0 : -1, fMax = h ? 2 : 1, fMid = (fMin + fMax) / 2;
        const double d = 0.40, rc = 0.12, ha = 0.42;

        foreach (var c in poles)
        {
            var p = new Pole(r, s, h, k, c);
            p.L(fMin, 0, fMid - d - rc, 0);   // 入線
            p.Circ(fMid - d, 0, rc);          // 端子○（左）
            p.Circ(fMid + d, 0, rc);          // 端子○（右）
            p.L(fMid + d + rc, 0, fMax, 0);   // 出線
            p.Arc(fMid - d, fMid + d, ha);    // 端子上をまたぐ弧 ∩
        }
        // 連動（実線）：3極の弧頂点（f=fMid, g=-ha）を横断
        Gang(r, s, h, k, poles, fMid, -ha);

        if (variant == "ELB")
            // 漏電遮断器：最外極の外側にテストボタン（小四角）
            new Pole(r, s, h, k, poles[2] + 0.55).Rect(fMid - 0.13, -0.13, fMid + 0.13, 0.13);
    }

    // 電磁接触器 主接点(3P): 各極=線のギャップ＋向かい合う2本の縦バー（─┤ ├─）。連動線なし。
    private static void ContactorMain3P(IRenderer r, StrokeStyle s, double width, bool h)
    {
        double k = width / 2.0;
        var poles = h ? PoleAcrossH : PoleAcrossV;
        double fMin = h ? 0 : -1, fMax = h ? 2 : 1, fMid = (fMin + fMax) / 2;
        const double gap = 0.16, tb = 0.26;

        foreach (var c in poles)
        {
            var p = new Pole(r, s, h, k, c);
            p.L(fMin, 0, fMid - gap, 0);            // 入線
            p.L(fMid - gap, -tb, fMid - gap, tb);   // 左バー │
            p.L(fMid + gap, -tb, fMid + gap, tb);   // 右バー │
            p.L(fMid + gap, 0, fMax, 0);            // 出線
        }
    }

    // サーマルリレー(OL): 2極（外側2線）に Z字段のヒータ素子、中央(S)は素通り。連動線なし。
    private static void ThermalOverload3P(IRenderer r, StrokeStyle s, double width, bool h)
    {
        double k = width / 2.0;
        var poles = h ? PoleAcrossH : PoleAcrossV;
        double fMin = h ? 0 : -1, fMax = h ? 2 : 1, fMid = (fMin + fMax) / 2;
        const double W = 0.22, hh = 0.18;

        for (int i = 0; i < 3; i++)
        {
            var p = new Pole(r, s, h, k, poles[i]);
            if (i == 1) { p.L(fMin, 0, fMax, 0); continue; }   // 中央(S)は素通り
            p.L(fMin, 0, fMid - W, 0);          // 入線
            p.L(fMid - W, 0, fMid - W, -hh);    // 上へ
            p.L(fMid - W, -hh, fMid, -hh);      // 上段
            p.L(fMid, -hh, fMid, hh);           // 基線をまたいで下へ
            p.L(fMid, hh, fMid + W, hh);        // 下段
            p.L(fMid + W, hh, fMid + W, 0);     // 基線へ戻る
            p.L(fMid + W, 0, fMax, 0);          // 出線
        }
    }
}
