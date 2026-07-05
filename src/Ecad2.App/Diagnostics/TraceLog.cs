using System.IO;

namespace Ecad2.App.Diagnostics;

/// <summary>
/// 操作トレースログ基盤(T-039、殿裁定=案B)。既定OFFで、起動時に明示的に有効化した場合のみ
/// %TEMP%\ecad2-trace.log へ追記する。T-038の一時診断ログ(%TEMP%\ecad2-diag.log、原因確定後に
/// 除去される調査用途)とは別名・別用途で常設共存する(docs/todo.md T-039備考)。
/// </summary>
internal static class TraceLog
{
    private const string EnvVarName = "ECAD2_TRACE_LOG";
    private const string ArgName = "--trace-log";

    // 高頻度イベント除外(忍者の現場意見「あっても困るもの」対応、家老指摘)。CanvasScaleは
    // Ctrl+マウスホイールの連続ズーム操作で刻み単位に頻発するが、1刻みごとの診断価値は低い
    // (マウス座標の連続変化に近い性質)。他の主要プロパティ(Tool/SelectedCell等)は離散的な
    // 状態遷移でありT-016/T-018等の実バグ調査に直結した実績があるため除外しない。
    private static readonly HashSet<string> HighFrequencyProperties = new() { "CanvasScale" };

    // 環境変数を無効化する意図の値の集合(隠密レビューfinding4: != "0"だけだと"false"/"off"/"no"を
    // 無効化として扱えない)。大小文字を区別しない。
    private static readonly HashSet<string> DisableEnvValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "0", "false", "off", "no",
    };

    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "ecad2-trace.log");

    private static bool _initialized;

    public static bool IsEnabled { get; private set; }

    /// <summary>App.OnStartupから一度だけ呼ぶ。忍者の現場要望(既定OFF+フラグ起動、セッション区切り
    /// 必須)に基づき、有効時のみ起動セッションの区切り行を書き出す。</summary>
    public static void Initialize(string[] args)
    {
        if (_initialized) return;
        _initialized = true;

        bool viaArg = args.Any(a => string.Equals(a, ArgName, StringComparison.OrdinalIgnoreCase));
        bool viaEnv = Environment.GetEnvironmentVariable(EnvVarName) is { Length: > 0 } env
            && !DisableEnvValues.Contains(env.Trim());
        IsEnabled = viaArg || viaEnv;

        if (IsEnabled) Write($"==== session start {DateTime.Now:O} ====");
    }

    /// <summary>ViewModelBase.OnPropertyChangedからの一括フック(案B (a))。oldValueはSetProperty経由の
    /// 変更のみ安価に捕捉できたもの(取得できなければnull=不明、殿裁定「安くできる範囲」に基づき
    /// 全setterの網羅は見送る)。newValueはリフレクションでプロパティ名から取得するため、setterの
    /// 実装パターンに依らず全プロパティを一律に捕捉できる。</summary>
    public static void LogPropertyChanged(object source, string? propertyName, object? oldValue)
    {
        if (!IsEnabled || propertyName is null || HighFrequencyProperties.Contains(propertyName)) return;
        try
        {
            object? newValue = source.GetType().GetProperty(propertyName)?.GetValue(source);
            Write($"event=PropertyChanged source={Quote(source.GetType().Name)} property={Quote(propertyName)} old={Quote(oldValue)} new={Quote(newValue)}");
        }
        catch
        {
            // ベストエフォート(隠密レビューfinding1): トレースログ自体の失敗が本来の処理
            // (PropertyChanged発火・T-036修正・Command実行)を道連れにしてはならない。
        }
    }

    /// <summary>App側のGotKeyboardFocus/LostKeyboardFocusクラスハンドラからのフック(案B (b))。</summary>
    public static void LogFocus(string eventName, string elementIdentity, string elementType)
    {
        if (!IsEnabled) return;
        Write($"event={eventName} element={Quote(elementIdentity)} type={Quote(elementType)}");
    }

    /// <summary>App側のButtonBase.Clickクラスハンドラからのフック(案B (c))。</summary>
    public static void LogClick(string elementIdentity, string elementType)
    {
        if (!IsEnabled) return;
        Write($"event=Click element={Quote(elementIdentity)} type={Quote(elementType)}");
    }

    // nullと空文字列を区別し(隠密レビューfinding5)、値中の"\/改行をエスケープしてkey=value形式の
    // 1行1イベント設計(Write参照)を壊さないようにする。
    private static string Quote(object? value)
    {
        if (value is null) return "null";
        string escaped = value.ToString()!
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return $"\"{escaped}\"";
    }

    private static void Write(string line)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff} {line}\n");
        }
        catch
        {
            // ベストエフォート(隠密レビューfinding1): ログ書込失敗(複数インスタンス同時起動時の
            // ファイル共有違反等)が本来の処理を道連れにしてはならない。
        }
    }
}
