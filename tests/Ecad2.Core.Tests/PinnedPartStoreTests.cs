using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// T-133増分9（殿裁定9）: <see cref="PinnedPartStore"/> の往復と退化入力。
/// <para>
/// <b>【本クラスは T-007 の移植以来、一度も測られておらなんだ】</b>参照0件のまま据え置かれ、
/// <b>原本 GuiEcad にもテストが無い</b>（侍の原本調査、2026-08-06）。<b>移植の拠り所が原本側にも
/// 無かった</b>ゆえ、配線する本増分で初めて網を張る。
/// </para>
/// <para>
/// <b>【「原本にも無い」は書かぬ理由にならぬ】</b>ecad2 は RED先行証明を制度として持ち、
/// 原本の水準に合わせる筋は無い——内側の作法は ecad2 自身の掟に従う（侍の分節、家老裁定2026-08-06）。
/// </para>
/// <para>
/// <b>【測っておらぬこと・侍が自ら区切る】</b>保存の失敗（ディスク満杯・権限不足等）は測っておらぬ
/// ——<see cref="PinnedPartStore.Save"/> が <c>catch { }</c> で握りつぶす形そのものが
/// <c>proposed.md</c> 起票済みの論点であり、本増分では振舞いを変えぬゆえ。
/// <b>「失敗しても例外が漏れぬ」ことだけは下の退化入力で押さえる。</b>
/// </para>
/// </summary>
public class PinnedPartStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public PinnedPartStoreTests()
    {
        // 実MyDocumentsを叩かぬよう一時フォルダを使う（P-019＝App層テストの副作用解消と同じ筋）。
        _dir = Path.Combine(Path.GetTempPath(), "ecad2-pinned-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "pinned-parts.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Load_ファイルが無ければ空を返す()
    {
        var store = new PinnedPartStore(_path);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void SaveしたIdがLoadで戻る()
    {
        var store = new PinnedPartStore(_path);

        store.Save(new[] { "part-a", "part-b" });

        Assert.Equal(new HashSet<string> { "part-a", "part-b" }, store.Load());
    }

    /// <summary><see cref="PinnedPartStore.Save"/> は保存先の親フォルダを自ら作る。
    /// <b>これが無ければ初回のピン留めが黙って失敗する</b>——<c>catch { }</c> ゆえ誰も気づかぬ形で。</summary>
    [Fact]
    public void Saveは保存先のフォルダを自ら作る()
    {
        Assert.False(Directory.Exists(_dir));

        new PinnedPartStore(_path).Save(new[] { "part-a" });

        Assert.True(File.Exists(_path));
    }

    /// <summary>空で保存すれば空が戻る。<b>最後の1件を解除した状態</b>にあたる。</summary>
    [Fact]
    public void 空で保存すれば空が戻る()
    {
        var store = new PinnedPartStore(_path);
        store.Save(new[] { "part-a" });

        store.Save(Array.Empty<string>());

        Assert.Empty(store.Load());
    }

    /// <summary>壊れたJSONを掴まされても例外を投げず空を返す（<c>catch</c> の側の振舞い）。
    /// <b>ピン留めの記録が壊れた程度でアプリが落ちてはならぬ</b>——原本と同じ握りつぶしを、
    /// ここでは<b>意図として固定する</b>（握りつぶし自体の是非は <c>proposed.md</c> の論点）。</summary>
    [Fact]
    public void 壊れたJSONでも例外を投げず空を返す()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ これはJSONではない");

        Assert.Empty(new PinnedPartStore(_path).Load());
    }

    /// <summary>重複を渡しても集合として畳まれる（<c>HashSet</c> で返るゆえ）。
    /// <b>呼び手が同じIdを二度足しても壊れぬ</b>ことを押さえる。</summary>
    [Fact]
    public void 重複するIdは集合として畳まれる()
    {
        var store = new PinnedPartStore(_path);

        store.Save(new[] { "part-a", "part-a", "part-b" });

        Assert.Equal(2, store.Load().Count);
    }
}
