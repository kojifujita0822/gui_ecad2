using System.Text.RegularExpressions;

namespace NlToLadderPoc;

/// <summary>自己保持回路の仕様（中間表現）。「AIには論理構造の判定とパラメータ抽出のみを担わせ、
/// レイアウト計算はコードで行う」という役割分担のうち、AI側の出力を模したもの
/// （本PoCでは決め打ちパターン解析でスタブ化し、後日LLM呼び出しへ差し替える前提）。</summary>
public sealed record SelfHoldSpec(string Set, string Reset, string Coil);

/// <summary>「XXでON、YYでOFF、ZZ保持」という決め打ち文型のみを解析する。
/// 自由文解析はスコープ外（殿ご裁可2026-09-14＝まずは決め打ちパターンでパイプライン全体を実証する）。</summary>
public static partial class SelfHoldParser
{
    [GeneratedRegex(@"^\s*(?<set>[A-Za-z0-9]+)\s*で\s*ON\s*[、,]\s*(?<reset>[A-Za-z0-9]+)\s*で\s*OFF\s*[、,]\s*(?<coil>[A-Za-z0-9]+)\s*保持\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();

    /// <summary>解析に失敗したら null（呼び出し元がエラー表示を行う）。</summary>
    public static SelfHoldSpec? TryParse(string input)
    {
        var m = Pattern().Match(input);
        if (!m.Success) return null;
        return new SelfHoldSpec(m.Groups["set"].Value, m.Groups["reset"].Value, m.Groups["coil"].Value);
    }
}
