using System.IO;

namespace Ecad2.App.Tests;

/// <summary>
/// T-123: 起動引数から開く文書を決める純粋関数(StartupArguments.ResolveDocumentPath)の検証。
///
/// この関数はファイル関連付け(.gcad)経由の起動で唯一の判断点になる。App.OnStartupと
/// MainWindow.Loadedの繋ぎ込みは3行に抑えてあり、判断はすべてここに集めてある。
///
/// 存在確認を含まないのは意図的(docコメント参照)。存在しないパスもそのまま返し、
/// 読み込み側が既存のエラーダイアログで扱う。ゆえに「存在しないパス」の検証は
/// 「弾かれずに返る」ことの確認になる。
/// </summary>
public class T123StartupArgumentsTests
{
    // --- 基本 ---------------------------------------------------------------

    [Fact]
    public void 引数が無ければnull_従来どおり空で起動する()
    {
        Assert.Null(StartupArguments.ResolveDocumentPath(Array.Empty<string>()));
    }

    [Fact]
    public void 引数がnullならnull()
    {
        Assert.Null(StartupArguments.ResolveDocumentPath(null));
    }

    [Fact]
    public void gcadファイルが一つならそれを返す()
    {
        Assert.Equal(@"C:\work\sample.gcad",
            StartupArguments.ResolveDocumentPath(new[] { @"C:\work\sample.gcad" }));
    }

    // --- 拡張子 -------------------------------------------------------------

    [Theory]
    [InlineData(@"C:\work\sample.txt")]
    [InlineData(@"C:\work\sample.gcad.txt")]  // 途中に含むだけでは対象外
    [InlineData(@"C:\work\sample.notgcad")]   // 末尾が似ているだけでは対象外
    [InlineData(@"C:\work\sample")]           // 拡張子なし
    public void gcad以外の拡張子はnull(string arg)
    {
        Assert.Null(StartupArguments.ResolveDocumentPath(new[] { arg }));
    }

    [Theory]
    [InlineData(@"C:\work\SAMPLE.GCAD")]
    [InlineData(@"C:\work\sample.GcAd")]
    public void 拡張子の大小は問わない(string arg)
    {
        Assert.Equal(arg, StartupArguments.ResolveDocumentPath(new[] { arg }));
    }

    // --- 境界(家老のDoD 5) --------------------------------------------------

    [Fact]
    public void 存在しないパスも弾かずに返す_読み込み側のエラー処理へ委ねるため()
    {
        // 存在確認をここで行わない設計の裏返し。もしここでnullを返す実装に変えると、
        // 誤ったパスを渡されたとき何の反応も無いまま空で起動し、利用者が原因を掴めなくなる。
        const string missing = @"Z:\no\such\directory\missing.gcad";
        Assert.False(File.Exists(missing));
        Assert.Equal(missing, StartupArguments.ResolveDocumentPath(new[] { missing }));
    }

    [Fact]
    public void 空白を含むパスも一つの引数として扱う()
    {
        // Windowsは.iss側の "%1" の引用を解いた上で1要素として渡すため、
        // ここで分割・結合を行う必要はない。その前提を固定する。
        const string spaced = @"C:\My Documents\制御盤 A棟.gcad";
        Assert.Equal(spaced, StartupArguments.ResolveDocumentPath(new[] { spaced }));
    }

    [Theory]
    [InlineData(@"sample.gcad")]
    [InlineData(@".\sample.gcad")]
    [InlineData(@"..\sub\sample.gcad")]
    public void 相対パスもそのまま返す_絶対化は読み込み側に任せる(string arg)
    {
        Assert.Equal(arg, StartupArguments.ResolveDocumentPath(new[] { arg }));
    }

    [Fact]
    public void 複数のgcadがあれば最初の一つだけを採る()
    {
        Assert.Equal(@"C:\first.gcad", StartupArguments.ResolveDocumentPath(
            new[] { @"C:\first.gcad", @"C:\second.gcad" }));
    }

    // --- フラグとの併用 -----------------------------------------------------

    [Fact]
    public void トレースログのフラグが先頭にあってもgcadを拾う()
    {
        // 先頭固定の実装だとここで落ちる。TraceLog.Initializeが`--trace-log`を位置に依らず
        // 探す(args.Any)ため、この順序は実際に起こりうる。
        Assert.Equal(@"C:\work\sample.gcad", StartupArguments.ResolveDocumentPath(
            new[] { "--trace-log", @"C:\work\sample.gcad" }));
    }

    [Fact]
    public void フラグだけならnull()
    {
        Assert.Null(StartupArguments.ResolveDocumentPath(new[] { "--trace-log" }));
    }

    [Fact]
    public void ハイフンで始まるファイル名も開ける()
    {
        // 実装当初は「ハイフン始まりはフラグとみなして飛ばす」分岐を置いていた。拡張子の判定が
        // 既にフラグを弾いており何も守らぬうえ、この正当なファイル名を開けなくする害だけが
        // あったため取り除いた(気づいたのはRED証明の設計中の読解)。
        Assert.Equal("-sample.gcad", StartupArguments.ResolveDocumentPath(new[] { "-sample.gcad" }));
    }

    /// <summary>
    /// 挙動の固定であって、空白ガードの検出力を測るものではない——ガードを外す壊す実測で
    /// この2件は鳴らなかった(拡張子の判定が空文字・空白を先に弾くため)。
    /// ガードが実際に守っているのは下の「null要素」の方にござる。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void 空文字や空白のみの引数は飛ばす(string blank)
    {
        Assert.Equal(@"C:\work\sample.gcad", StartupArguments.ResolveDocumentPath(
            new[] { blank, @"C:\work\sample.gcad" }));
    }

    [Fact]
    public void 配列にnull要素が混じっていても落ちない()
    {
        // 配列は共変のため、実行時にはnull要素を含むstring[]がIReadOnlyList<string>として
        // 渡りうる。空文字・空白を飛ばすガードがこれも受け止めることを固定する
        // (このガードを外すとNullReferenceExceptionになる)。
        string?[] args = { null, @"C:\work\sample.gcad" };
        Assert.Equal(@"C:\work\sample.gcad", StartupArguments.ResolveDocumentPath(args!));
    }

    [Fact]
    public void 拡張子だけの引数も返す_ファイル名として妥当なため()
    {
        // 隠しファイル名として成立する(`.gcad`という名のファイルは作れる)。
        // ここで弾く根拠が無いので通す、という判断を固定しておく。
        Assert.Equal(".gcad", StartupArguments.ResolveDocumentPath(new[] { ".gcad" }));
    }
}
