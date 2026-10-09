using System.Globalization;
using Ecad2.Model;

namespace Ecad2.Simulation;

/// <summary>機器番号の順送りの結果。</summary>
/// <param name="Renames">行った（行う）改名。適用する順に並ぶ。空なら何も送っておらぬ。</param>
/// <param name="Conflict">送れなんだ理由。送れた・送る対象が無い場合は null。</param>
public sealed record DeviceNumberShiftResult(IReadOnlyList<(string From, string To)> Renames, string? Conflict);

/// <summary>
/// 機器番号の挿入と詰め（殿ご下命2026-10-09）。指定した機器名と<b>同じ接頭辞</b>で<b>番号がそれ以上</b>の
/// 機器名を、すべて一つずつ送る。
/// <list type="bullet">
/// <item><b>挿入</b>（<see cref="InsertAt"/>）＝後ろへ送る。CR1〜CR7 が在るとき CR4 を指定すれば
/// CR4〜CR7 が CR5〜CR8 になり、CR4 が空く。</item>
/// <item><b>詰め</b>（<see cref="RemoveAt"/>）＝前へ送る。CR1 を消した後に CR2 を指定すれば
/// CR2 以降が CR1 以降になる。</item>
/// </list>
/// <para>
/// <b>【欠番でも止めぬ】</b>途中に欠番が在っても末尾まで送る（殿ご裁可）。CR4, CR5, CR10 へ CR4 を
/// 挿入すれば CR5, CR6, CR11 になる。
/// </para>
/// <para>
/// <b>【対象の名】</b>末尾が数字で、その前に一文字以上の接頭辞を持つ名（CR4・T1・MC12 等）。
/// 接頭辞の照合は <see cref="DeviceRenamer"/> に合わせて大文字小文字を区別せぬ。
/// 図面上の要素の名と、機器表にのみ在る名の双方を送る——機器表だけに残る CR8 を見落とせば、
/// CR7 を CR8 へ送った折に <see cref="DeviceRenamer.Rename"/> が CR7 の型式・コメントを捨てる。
/// </para>
/// <para>
/// <b>【送る順】</b>挿入は大きい番号から、詰めは小さい番号から。逆にすると、CR4 を CR5 にした時点で
/// 元の CR5 と見分けがつかなくなる。
/// </para>
/// <para>
/// <b>【送り先が塞がっておれば送らぬ】</b>送り先の名を、送る対象でない機器が既に使っておる場合
/// （詰めようとした先の CR1 がまだ残っておる等）は、黙って同じ名へ潰さず理由を返す。
/// </para>
/// </summary>
public static class DeviceNumberShifter
{
    /// <summary>機器名を接頭辞と末尾の番号へ分ける。末尾が数字でない・接頭辞が無い・番号が
    /// <see cref="int"/> に収まらぬ名は false。</summary>
    public static bool TryParse(string? name, out string prefix, out int number)
    {
        prefix = "";
        number = 0;
        if (string.IsNullOrEmpty(name)) return false;

        int digitStart = name.Length;
        while (digitStart > 0 && name[digitStart - 1] is >= '0' and <= '9') digitStart--;
        if (digitStart == 0 || digitStart == name.Length) return false;
        if (!int.TryParse(name.AsSpan(digitStart), NumberStyles.None, CultureInfo.InvariantCulture, out number))
            return false;

        prefix = name[..digitStart];
        return true;
    }

    /// <summary>挿入の段取りだけを立てる（図面は書き換えぬ）。</summary>
    public static DeviceNumberShiftResult Plan(LadderDocument doc, string insertAt) => PlanShift(doc, insertAt, +1);

    /// <summary>詰めの段取りだけを立てる（図面は書き換えぬ）。</summary>
    public static DeviceNumberShiftResult PlanRemoval(LadderDocument doc, string closeFrom) => PlanShift(doc, closeFrom, -1);

    /// <summary>挿入を行う。<paramref name="insertAt"/> 以降を後ろへ送り、その名を空ける。
    /// 送れなんだ場合（<see cref="DeviceNumberShiftResult.Conflict"/> が非 null）と
    /// 送る対象が無い場合は図面に触れぬ。</summary>
    public static DeviceNumberShiftResult InsertAt(LadderDocument doc, string insertAt) => Apply(doc, Plan(doc, insertAt));

    /// <summary>詰めを行う。<paramref name="closeFrom"/> 以降を前へ送り、一つ前の空き番号を埋める。
    /// 図面に触れぬ条件は <see cref="InsertAt"/> と同じ。</summary>
    public static DeviceNumberShiftResult RemoveAt(LadderDocument doc, string closeFrom) => Apply(doc, PlanRemoval(doc, closeFrom));

    private static DeviceNumberShiftResult Apply(LadderDocument doc, DeviceNumberShiftResult plan)
    {
        foreach (var (from, to) in plan.Renames) DeviceRenamer.Rename(doc, from, to);
        return plan;
    }

    private static DeviceNumberShiftResult PlanShift(LadderDocument doc, string origin, int delta)
    {
        var none = Array.Empty<(string From, string To)>();
        if (!TryParse(origin, out string prefix, out int originNumber)) return new(none, null);

        // 大文字小文字違いの同名は DeviceRenamer が一度の改名でまとめて書き換えるゆえ、一つに畳む。
        var names = doc.Sheets.SelectMany(s => s.Elements).Select(e => e.DeviceName)
            .Concat(doc.Devices.ByName.Keys)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var renames = new List<(string From, string To, int Number)>();
        foreach (string name in names)
        {
            if (!TryParse(name, out string p, out int n)) continue;
            if (!string.Equals(p, prefix, StringComparison.OrdinalIgnoreCase) || n < originNumber) continue;
            if (delta > 0 ? n == int.MaxValue : n == 0)
                return new(none, $"{name} はこれ以上番号を送れません。");
            renames.Add((name, p + FormatNumber(n + delta, name.Length - p.Length, name[p.Length] == '0'), n));
        }

        // 桁数の書き方が混じると送り先が重なりうる（CR9 と CR09 はどちらも CR10 になる）。
        var collision = renames.GroupBy(r => r.To, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (collision is not null)
            return new(none, $"{string.Join(" と ", collision.Select(r => r.From))} の送り先がどちらも {collision.Key} になります。");

        // 送り先を、送る対象でない機器が使っておる（詰める先の番号がまだ残っておる等）。
        var staying = names.Except(renames.Select(r => r.From), StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blocked = renames.FirstOrDefault(r => staying.Contains(r.To));
        if (blocked.To is not null)
            return new(none, $"{blocked.From} の送り先 {blocked.To} がまだ使われています。");

        var ordered = delta > 0 ? renames.OrderByDescending(r => r.Number) : renames.OrderBy(r => r.Number);
        return new(ordered.Select(r => (r.From, r.To)).ToList(), null);
    }

    /// <summary>番号を名へ戻す。<b>先頭が 0 の名だけ桁数を保つ</b>（CR09→CR10・CR010→CR009）。
    /// 先頭が 0 でない名まで桁数を保てば、CR10 を詰めた折に CR09 になってしまう。</summary>
    private static string FormatNumber(int number, int originalWidth, bool wasZeroPadded) =>
        number.ToString(wasZeroPadded ? "D" + originalWidth : "D", CultureInfo.InvariantCulture);
}
