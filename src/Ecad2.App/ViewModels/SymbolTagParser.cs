using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 配置メニューの項目に付したタグ文字列を、種別と向きへ解く（T-133増分4-A）。
/// タグの形は <c>"Breaker3P"</c>（向きなし）または <c>"Breaker3P#V"</c>／<c>"Breaker3P#H"</c>。
/// <para>
/// <b>【原本にも同じ役の関数がある】</b>GuiEcad の <c>ParseSymbolTag</c>（<c>MainPage.Tools.cs:63-72</c>）。
/// 原本はメニュー項目のタグに種別と向きを埋め込み、選択時にこれを解いて配置ツールの状態を決める
/// ——<b>向きは配置時に確定し、以後は変えられぬ</b>という設計であり（原本コメント「タグ "Kind#V/#H" で
/// 配置時に向きを確定（切替不可）」）、ecad2 もこれを踏襲する（殿裁定2026-07-28）。
/// </para>
/// <para>
/// <b>【なぜ純粋関数として切り出すか】</b>この解釈はメニューの <c>Click</c> ハンドラの中に書いても動くが、
/// そこへ書けば<b>不正入力・退化入力の振る舞いを測る術が無くなる</b>——View 層はテストから触りにくい。
/// 切り出せば「どこまでを受け入れ、どこから拒むか」を数で固定できる（<c>SymbolTagParserTests</c>）。
/// <b>呼び手は増分4-C で繋ぐ</b>——それまでは参照0件の意図した中間状態である。
/// </para>
/// <para>
/// <b>【責務の切り分け】本クラスが見るのは「文字列の形」だけである。</b>
/// 「その種別に向きを付けてよいか」（例：コイルに <c>#V</c> は無意味）までは検査せぬ。
/// <b>種別と向きの組み合わせが正しいことは、タグを書くメニュー定義の側が負う</b>——
/// タグを書くのは我々であり、外から任意の文字列が来る口ではないためである。
/// ここで種別ごとの可否まで抱えれば、主回路3極記号3種の列挙が
/// <see cref="ElementCatalog"/> 内の4箇所に続く5箇所目となり、増える側の理由が薄い。
/// </para>
/// </summary>
internal static class SymbolTagParser
{
    /// <summary>向きの区切り。</summary>
    private const char Separator = '#';

    /// <summary>
    /// タグを種別と向きへ解く。解けなければ <c>false</c> を返し、出力は既定値のまま。
    /// <para>
    /// <b>受け入れるもの</b>：<c>"Breaker3P"</c>（向き <c>null</c>）／<c>"Breaker3P#V"</c>／<c>"Breaker3P#H"</c>。
    /// <b>拒むもの</b>：<c>null</c>・空文字・区切りが2つ以上・種別名が未知・種別名が数値表記
    /// （<c>Enum.TryParse</c> は <c>"0"</c> を <c>ContactNO</c> として受けるゆえ、名前で照合する）・
    /// 向きが <c>V</c>／<c>H</c> 以外・大文字小文字の違い。
    /// </para>
    /// <para>
    /// <b>【向きを厳格に V／H のみとする理由】</b>綴りを誤ったタグを黙って通せば、
    /// <c>Params[Orient]</c> に意味を持たぬ値が入り、描画は既定の縦向きへ静かに倒れる
    /// ——<b>誤りが画面に現れず、気づく手立てが無い</b>。ここで拒めば、メニュー項目を追加した折の
    /// 綴り誤りがテストで露見する。
    /// </para>
    /// <para>
    /// <b>【値の綴りは <c>DiagramRenderer</c> と揃えること】</b>横向きの判定は
    /// <c>DiagramRenderer.cs:1047</c> の <c>orient == "H"</c> が行う。<b>同じ綴りが2箇所に在る</b>が、
    /// 3箇所目はメニューの XAML（<c>Tag="Breaker3P#V"</c>）であり定数にできぬゆえ、
    /// C# 側だけを定数へ括り出しても半端になる。<b>rule of three に達するまでは、この注記で結ぶ。</b>
    /// </para>
    /// </summary>
    public static bool TryParse(string? tag, out ElementKind kind, out string? orient)
    {
        kind = default;
        orient = null;
        if (string.IsNullOrEmpty(tag)) return false;

        var parts = tag.Split(Separator);
        if (parts.Length > 2) return false;

        // Enum.TryParse は数値表記も受け入れる（"0" → ContactNO、"999" → 未定義値）。
        // タグは種別名で書く約束ゆえ、定義済みの名前と一致するものだけを通す。
        if (Array.IndexOf(Enum.GetNames<ElementKind>(), parts[0]) < 0) return false;
        var parsedKind = Enum.Parse<ElementKind>(parts[0]);

        string? parsedOrient = null;
        if (parts.Length == 2)
        {
            if (parts[1] is not ("V" or "H")) return false;
            parsedOrient = parts[1];
        }

        // 【成功が確定してから out へ書く】途中で out へ書けば、後段で拒んだ折に
        // 半端な値が残る——実際、当初の実装は種別を先に代入しており、"Breaker3P#X"
        // （種別は通り向きで弾かれる）で kind が Breaker3P のまま返っておった。
        // RED先行証明の最中にテストを拒む段ごとへ増やしたところ、これが露見した。
        // この形にしておけば、将来どこへ検査を足しても同じ穴は空かぬ。
        kind = parsedKind;
        orient = parsedOrient;
        return true;
    }
}
