namespace Ecad2.App;

/// <summary>
/// 起動引数の解釈(T-123)。ファイル関連付け(.gcad)経由の起動で開く文書を決める。
/// I/Oを行わない純粋関数として切り出してあり、単体テストで測れる。
/// </summary>
internal static class StartupArguments
{
    /// <summary>ecad2の文書ファイルの拡張子。MainWindowのファイルダイアログ(GcadFileFilter)と同一。</summary>
    internal const string DocumentExtension = ".gcad";

    /// <summary>
    /// 起動引数から開くべき文書のパスを取り出す。該当が無ければnull(＝従来どおり空で起動する)。
    ///
    /// 先頭固定ではなく全体を走査するのは、既存のTraceLogが`--trace-log`フラグを位置に依らず
    /// 探すため(TraceLog.Initialize)。`--trace-log foo.gcad`の順で渡された場合、先頭固定だと
    /// 文書を拾えない。
    ///
    /// フラグ(ハイフン始まり)を明示的に飛ばす分岐は置かない。拡張子の判定が既にフラグを弾く
    /// うえ、置くと`-sample.gcad`のようなハイフン始まりの正当なファイル名まで開けなくなる。
    /// (実装時に一度その分岐を書いた。RED証明の設計中に「外しても拡張子判定が弾くゆえ何も
    /// 変わらぬ」と読んで気づき、取り除いた。フラグを弾いているのが拡張子判定の側であることは、
    /// その判定を外す壊す実測で「フラグだけならnull」が落ちることにより裏づけている。)
    ///
    /// 存在確認は行わない——I/Oを持ち込むと純粋関数でなくなるため。存在しないパスもそのまま
    /// 返し、読み込み側のエラー処理(MainWindow.LoadDocumentOrShowError)へ委ねる。
    /// ダイアログ経由と同じ文面を出すのが狙い。
    ///
    /// 空白を含むパスは、Windowsが`"%1"`の引用を解いた後の1要素として渡ってくるため、
    /// ここでの分割・結合は不要(.issの関連付けは`"{app}\{exe}" "%1"`の形で書く)。
    /// </summary>
    internal static string? ResolveDocumentPath(IReadOnlyList<string>? args)
    {
        if (args is null) return null;

        foreach (string? arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg)) continue;
            if (!arg.EndsWith(DocumentExtension, StringComparison.OrdinalIgnoreCase)) continue;
            return arg;
        }
        return null;
    }
}
