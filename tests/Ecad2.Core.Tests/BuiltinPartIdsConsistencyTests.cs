using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.Core.Tests;

/// <summary>
/// <see cref="BuiltinPartIds"/>（<c>Model</c>層に置いた綴りの本体）と、実際に同梱される
/// <see cref="BasicPartTemplates.All"/> の一致を固定する（T-151期）。
/// <para>
/// 【なぜ要るか】組込みか自作かの弁別は <see cref="BuiltinPartIds.Contains"/> が受け持つ。
/// <b>組込みパーツを増やして本集合への追加を忘れると、そのパーツは黙って自作扱いになる</b>
/// ——ラベルの既定オフセットが外れ、図面への埋め込み対象にもなる。いずれも例外を投げず、
/// テストも実機も「何も起きておらぬ」ように見える型の失敗にござる。
/// </para>
/// <para>
/// 【この網が守る対象】<c>BasicPartTemplates</c> の各 <c>const</c> は
/// <see cref="BuiltinPartIds"/> への転送ゆえ<b>綴り違いは起こり得ぬ</b>。
/// <b>本テストが捕らえるのは綴りではなく「員数の食い違い」</b>——
/// <c>All()</c> にだけ足して本集合へ足し忘れた場合にござる。
/// </para>
/// </summary>
public class BuiltinPartIdsConsistencyTests
{
    /// <summary>同梱テンプレートのIdは、すべて組込みId集合に含まれておらねばならぬ
    /// （足し忘れ＝黙って自作扱いになる側を捕らえる）。</summary>
    [Fact]
    public void 同梱テンプレートのIdはすべて組込みId集合に含まれる()
    {
        var missing = BasicPartTemplates.All()
            .Select(p => p.Id)
            .Where(id => !BuiltinPartIds.Contains(id))
            .ToArray();

        Assert.Empty(missing);
    }

    /// <summary>逆向き＝集合にあるのに同梱されておらぬIdが無いこと（消し忘れを捕らえる）。</summary>
    [Fact]
    public void 組込みId集合に同梱されておらぬIdは無い()
    {
        var actual = BasicPartTemplates.All().Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var stale = BuiltinPartIds.All.Where(id => !actual.Contains(id)).ToArray();

        Assert.Empty(stale);
    }

    /// <summary>員数の一致。上の二件は集合としての包含を測るゆえ、
    /// <b>同じIdが二度並んでおる</b>ような形は素通しする——員数を別に測って塞ぐ。</summary>
    [Fact]
    public void 組込みId集合の員数は同梱テンプレートと一致し重複を持たぬ()
    {
        Assert.Equal(BasicPartTemplates.All().Count, BuiltinPartIds.All.Count);
        Assert.Equal(BuiltinPartIds.All.Count, BuiltinPartIds.All.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>組込みでないIdを組込みと誤判定せぬこと。
    /// <c>null</c>・空は <c>Kind</c> 経路（組込み／自作の概念が無い）にて false が正しい。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("t151-label-custom-coil")]
    [InlineData("basic-")]
    [InlineData("basic-coil-2")]          // 組込みIdを接頭辞に持つが別物
    [InlineData("BASIC-COIL")]            // 大小が違えば別物（Ordinal比較であることの固定）
    public void 組込みでないIdは組込みと判定されぬ(string? id)
    {
        Assert.False(BuiltinPartIds.Contains(id));
    }

    /// <summary><see cref="ElementCatalog.MotorPartId"/> が同じ綴りを指しておること。
    /// <para>
    /// <b>【この網は T-151期に検出力を失った。記録として残す】</b>かつて両者は各々に綴りを持っており、
    /// 食い違えばモータのラベル迂回が黙って効かなくなるという罠を抱えておった（T-145）。
    /// 現在はいずれも <see cref="BuiltinPartIds.Motor"/> への転送ゆえ、<b>食い違いは原理的に起こり得ぬ</b>。
    /// すなわち本テストは常にGREENにて、<b>網としては働いておらぬ</b>——守るべき対象が構造から消えた形。
    /// 消さずに残すのは、転送を解いて綴りを戻す改変が入った折に、再び網として働くゆえにござる。
    /// </para></summary>
    [Fact]
    public void モータのPartIdは組込みId集合と同じ綴りを指す()
    {
        Assert.Equal(BuiltinPartIds.Motor, ElementCatalog.MotorPartId);
        Assert.Equal(BuiltinPartIds.Motor, BasicPartTemplates.MotorId);
    }
}
