using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// PR-17段4【試作】: 基準 <c>NotifySelectedElementChanged()</c> の通知漏れを、
/// <b>命名規約を唯一の登録先として</b>機械的に検める試み（侍の設計書§5）。
///
/// <para>
/// <b>【この仕掛けは穴を塞がぬ。移すだけである】</b>
/// 前セッションの到達点＝<c>memory: feedback_detection_relocates_not_eliminates_discipline_burden</c>。
/// A/B の fixture を持つ形では「fixture への追加漏れ」に穴が移る——本試作はそれを避けて
/// <b>命名規約</b>を登録先に据えたが、<b>規約の外にある名は例外として手で持つほかなく、
/// 穴は「例外リストへの追加漏れ」へ移る</b>。
/// <b>面積は減るが、消えはせぬ。</b>
/// </para>
///
/// <para>
/// <b>【ゆえに本試作が担うのは片側だけである】</b>
/// <list type="bullet">
/// <item>捕らえる＝<b>規約に合う名を新設し、基準へ足し忘れた</b>場合（<c>T-107</c>・<c>PR-17</c>の型）</item>
/// <item>捕らえぬ＝<b>規約の外の名</b>（<c>HasXxx</c> 等）<b>を新設し、基準へ足し忘れた</b>場合。
/// これは誰も捕らえぬ——<c>P-169</c>（<c>HasNoPropertySelection</c>）がまさにこの形であった</item>
/// </list>
/// <b>すなわち本試作は、我らが二度踏んだ穴のうち片方しか塞がぬ。</b>
/// 段1（複製そのものを断つ）が本命であり、本試作はその補助にすぎぬ。
/// </para>
///
/// <para>
/// <b>【もう一つの脆さ・タイポによる規約逸脱】</b>（隠密の指摘、2026-08-06）
/// 上の限界は<b>意図して規約の外へ出る</b>場合の話にござる。
/// これとは別に<b>規約に従うつもりでタイポにより外れる</b>形がある——例＝<c>SelectdElementXxx</c>。
/// <b>こちらの方が危うい</b>：意図的な規約外なら <see cref="KnownOutsideConvention"/> へ足す動機が働くが、
/// <b>タイポは書き手自身が逸脱に気づかぬ</b>ゆえ、基準へ足しても本試作は沈黙する。
/// </para>
/// </summary>
public class Pr17NotificationCoverageTests : ViewModelTestBase
{
    /// <summary>登録先とする命名規約。<b>ここに載る名を新設すれば、自動で検査の対象になる</b>
    /// ——手で足す一覧を持たぬのが本試作の狙いにござる。</summary>
    private static readonly string[] ConventionPrefixes = { "SelectedElement", "IsSelectedElement" };

    /// <summary>
    /// 規約の外にありながら基準が通知する名。<b>ここが本試作の穴である</b>
    /// ——新しい規約外の名を基準へ足せば、この一覧も手で直す要がある。
    /// <b>「fixture への追加漏れ」が「ここへの追加漏れ」に移っただけ</b>にござる。
    /// </summary>
    private static readonly string[] KnownOutsideConvention =
    {
        nameof(MainWindowViewModel.HasSelectedElement),
        nameof(MainWindowViewModel.HasNoPropertySelection),
    };

    private static HashSet<string> PropertiesByConvention()
        => typeof(MainWindowViewModel).GetProperties()
            .Select(p => p.Name)
            .Where(n => ConventionPrefixes.Any(prefix => n.StartsWith(prefix, StringComparison.Ordinal)))
            .ToHashSet();

    /// <summary>基準が実際に飛ばす通知を、削除経路で捕らえる（基準を単独で呼べる最短の経路）。
    /// <para>
    /// <b>【書かれておらぬ依存に支えられておる。ここに書き残す】</b>（隠密の指摘、2026-08-06）
    /// <c>DeleteSelectedElement()</c> は <c>MarkDirty()</c> を呼ぶが、<c>IsDirty</c> の通知は
    /// <b>ここへ混入せぬ</b>——<see cref="ArrangeWithSelectedElement"/> の要素配置で既に
    /// <c>IsDirty = true</c> になっており、<c>IsDirty</c> の setter が <c>SetProperty</c> ゆえ
    /// <b>二度目は同値ガードで弾かれる</b>から（<c>MainWindowViewModel.cs:263,267</c> で確認）。
    /// <b>Arrange が「文書を変更せぬ」形へ変われば、<c>IsDirty</c> が混入して
    /// 「規約の外は既知2件のみ」のテストが崩れる。</b>
    /// </para>
    /// <para>
    /// <b>本日、我らが繰り返し見た形にござる</b>——どこにも書かれておらぬ依存に正しさが支えられておる
    /// （<c>ReplaceDocument</c> の暗黙の通知・Undo時の無条件クリア・そしてこれ）。
    /// <b>ゆえに、せめて書き残す。</b>
    /// </para>
    /// </summary>
    private static HashSet<string> CaptureBasisNotifications(MainWindowViewModel vm)
    {
        var raised = new HashSet<string>();
        void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is string name) raised.Add(name);
        }

        vm.PropertyChanged += Handler;
        try { vm.DeleteSelectedElement(); }
        finally { vm.PropertyChanged -= Handler; }
        return raised;
    }

    private static MainWindowViewModel ArrangeWithSelectedElement(ViewModelTestBase _, MainWindowViewModel vm)
    {
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 3);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = new GridPos(2, 3);
        return vm;
    }

    /// <summary>
    /// <b>命名規約に合うプロパティは、基準がすべて通知すること。</b>
    /// <para>
    /// <b>これが本試作の主眼</b>——<c>SelectedElementXxx</c> を新設して基準へ足し忘れれば、
    /// <b>ここへ何も書き加えずとも鳴る</b>。<c>T-107</c>（<c>SelectedElementComment</c>）で
    /// 実害を出し、<c>PR-17</c> で二度目を踏んだ型にござる。
    /// </para>
    /// </summary>
    [Fact]
    public void 命名規約に合うプロパティは基準がすべて通知する()
    {
        var vm = ArrangeWithSelectedElement(this, CreateViewModel());

        var raised = CaptureBasisNotifications(vm);

        var missing = PropertiesByConvention().Except(raised).OrderBy(n => n).ToList();
        Assert.Empty(missing);
    }

    /// <summary>
    /// 基準が通知するもののうち<b>規約の外にある名は、既知の2件のみ</b>であること。
    /// <para>
    /// <b>本試作の穴を、穴と分かる形で固定する</b>のが狙いにござる。
    /// 3件目を基準へ足せばここが鳴り、<b>「規約の外へ足した」ことを書き手に気づかせる</b>。
    /// <b>されど基準へ足し忘れた規約外の名は、依然として誰も捕らえぬ</b>
    /// ——<c>P-169</c> がその形であった。
    /// </para>
    /// </summary>
    [Fact]
    public void 基準の通知のうち規約の外にあるものは既知の二件のみ()
    {
        var vm = ArrangeWithSelectedElement(this, CreateViewModel());

        var raised = CaptureBasisNotifications(vm);

        var outside = raised.Except(PropertiesByConvention()).OrderBy(n => n).ToHashSet();
        Assert.Equal(KnownOutsideConvention.OrderBy(n => n).ToHashSet(), outside);
    }
}
