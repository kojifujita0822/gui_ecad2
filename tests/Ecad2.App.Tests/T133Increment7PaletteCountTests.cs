using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-133増分7: サーマルリレーa/bの移植により<b>部品選択リストの表示件数が17→19件へ変わった</b>
/// ことの回帰テスト。
/// <para>
/// <b>【同じ「17」が二つの単位で現れる。ここで単位を固定する】</b>（隠密の検算、2026-08-06）
/// <list type="bullet">
/// <item><c>BasicPartTemplates.All()</c> の件数＝<b>15→17</b>（<c>T136Increment5PortKindAssignmentTests</c> が押さえる）</item>
/// <item><b>表示件数</b>＝<c>All()</c>＋OR論理2件＝<b>17→19</b>（本テスト群が押さえる。<b>忍者が実機で採る基準値はこちら</b>）</item>
/// </list>
/// <b>増分7の前後で「17」が別の意味を指すゆえ、数だけを見て突き合わせると必ず取り違える。</b>
/// </para>
/// <para>
/// <b>【なぜ実機任せにせず、ここでも測るか】</b>忍者の実機確認は表示件数を採るが、
/// <b>件数が合っていても内訳が入れ替わっておれば気づけぬ</b>——例えばサーマルリレーが
/// OR適格になって21件のうち2件が落ちた形でも、総数だけなら偶然19に見えうる。
/// <b>本テスト群は内訳（OR論理が a接点／b接点の2件のみ）まで固定する。</b>
/// </para>
/// <para>
/// <b>【環境に依らぬ】</b><see cref="ViewModelTestBase"/> がテストごとに空の一時フォルダを発行し、
/// <c>PartFolderStore.SeedBasics()</c> がそこへテンプレートを展開する（T-042、P-019の対処）。
/// 殿の実フォルダの中身には左右されぬ。
/// </para>
/// </summary>
public class T133Increment7PaletteCountTests : ViewModelTestBase
{
    /// <summary>
    /// 部品選択リストの表示件数は19件。<b>忍者の実機確認における基準値そのもの</b>
    /// （増分6までは17件であった）。
    /// </summary>
    [Fact]
    public void 部品選択リストは十九件である()
    {
        var vm = CreateViewModel();

        Assert.Equal(19, vm.PartPalette.SelectionEntries.Count);
    }

    /// <summary>
    /// 19件の内訳が「テンプレート17件＋OR論理2件」であること。
    /// <b>総数だけでは内訳の入れ替わりを捕らえられぬ</b>ゆえ、両側から押さえる。
    /// </summary>
    [Fact]
    public void 十九件の内訳はテンプレート十七件とOR論理二件である()
    {
        var vm = CreateViewModel();
        var entries = vm.PartPalette.SelectionEntries;

        Assert.Equal(17, entries.Count(e => !e.IsOr));
        Assert.Equal(2, entries.Count(e => e.IsOr));
    }

    /// <summary>
    /// <b>OR論理エントリは a接点・b接点の2件のみ</b>であること。
    /// <para>
    /// <c>BasicPartTemplates</c> 側の <c>IsOrEligible=false</c>（<c>T133Increment7ThermalRelayTests</c>）が
    /// <b>表示層まで届いておることの証</b>にござる——サーマルリレーは <c>Role=ContactNO/NC</c> を
    /// 持つゆえ、<b>もし判定が <c>Role</c> を見る形へ退化すれば、ここに「ORサーマルリレー」が現れる</b>。
    /// <b>T-037往復2周目で実際に起きた形</b>（当時は <c>Role</c> 判定でセレクトSWが巻き込まれ
    /// 「ORセレクトSW」が出現した、<c>PartPaletteViewModel.cs:69-74</c>）——
    /// <b>本増分でOR適格でない <c>ContactNO</c> 系が2件増えたことで、その退化を捕らえる力が増した。</b>
    /// </para>
    /// </summary>
    [Fact]
    public void OR論理はa接点とb接点の二件のみである()
    {
        var vm = CreateViewModel();

        var orIds = vm.PartPalette.SelectionEntries.Where(e => e.IsOr)
            .Select(e => e.Definition.Id).OrderBy(id => id).ToList();

        Assert.Equal(
            new[] { BasicPartTemplates.ContactNCId, BasicPartTemplates.ContactNOId }.OrderBy(id => id),
            orIds);
    }

    /// <summary>
    /// 移植した2件が実際に部品選択リストへ現れること。
    /// <b>テンプレートに足しただけで展開・列挙まで届いておらぬ形を捕らえる</b>
    /// ——<c>SeedBasics()</c> から <c>Enumerate()</c> を経て表示へ至る経路が繋がっておるかを見る。
    /// </summary>
    [Theory]
    [InlineData(BasicPartTemplates.ThermalRelayNOId)]
    [InlineData(BasicPartTemplates.ThermalRelayNCId)]
    public void サーマルリレーは部品選択リストに現れる(string partId)
    {
        var vm = CreateViewModel();

        Assert.Single(vm.PartPalette.SelectionEntries.Where(e => e.Definition.Id == partId && !e.IsOr));
    }
}
