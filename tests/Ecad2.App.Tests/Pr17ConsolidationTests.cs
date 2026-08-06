using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// PR-17段1: `SelectedElement` 系通知の四重複製を <c>NotifySelectedElementChanged()</c> へ統合した際の、
/// <b>振る舞い不変</b>を測る（隠密のテスト設計書 <c>docs/ecad2-pr17-consolidation-test-design-onmitsu.md</c> §2）。
/// <para>
/// <b>【件数ではなく集合で見る】</b>件数一致は、例えば <c>SelectedElementLabelDy</c> が漏れて
/// 代わりに <c>SelectedElementComment</c> が二重に飛んでも15件のままゆえ検出できぬ
/// ——<b>PR-27 が警告する「対称な入れ替わりを件数が隠す」形</b>にござる。
/// </para>
/// <para>
/// <b>【測っておらぬこと】</b>本テスト群が見るのは <c>PropertyChanged</c> が飛んだか否かの一点にて、
/// <b>通知の順序も、同一プロパティが何回飛んだかも見ておらぬ</b>（集合ゆえ重複は潰れる）。
/// 回数は段2のテストが受け持つ（設計書§3-4）。
/// </para>
/// </summary>
public class Pr17ConsolidationTests : ViewModelTestBase
{
    /// <summary>
    /// 基準 <c>NotifySelectedElementChanged()</c> が通知する15件。
    /// <b>並びの定義をここ1箇所に持ち、他の期待集合はここからの合成で組み立てる</b>
    /// （<c>MenuPlacementToolTests</c> が <c>SymbolIndices()</c> を派生させたのと同じ作法）。
    /// </summary>
    private static readonly string[] BasisProperties =
    {
        nameof(MainWindowViewModel.SelectedElement),
        nameof(MainWindowViewModel.HasSelectedElement),
        nameof(MainWindowViewModel.SelectedElementKindDisplay),
        nameof(MainWindowViewModel.SelectedElementDeviceName),
        nameof(MainWindowViewModel.IsSelectedElementSelectSwitch),
        nameof(MainWindowViewModel.SelectedElementNotchPosition),
        nameof(MainWindowViewModel.IsSelectedElementBreaker3P),
        nameof(MainWindowViewModel.SelectedElementBreakerType),
        nameof(MainWindowViewModel.IsSelectedElementLamp),
        nameof(MainWindowViewModel.SelectedElementLampColor),
        nameof(MainWindowViewModel.IsSelectedElementTimerRelated),
        nameof(MainWindowViewModel.SelectedElementSetpoint),
        nameof(MainWindowViewModel.SelectedElementSetpointSliderValue),
        nameof(MainWindowViewModel.SelectedElementLabelDy),
        nameof(MainWindowViewModel.SelectedElementComment),
    };

    /// <summary>基準に含まれぬが本統合の対象に隣接する2件。<c>SelectedCellDisplay</c> は
    /// <c>SelectedCell</c> setter 固有、<c>HasNoPropertySelection</c> は段2で基準へ移す予定のもの。</summary>
    private static readonly string[] AdjacentProperties =
    {
        nameof(MainWindowViewModel.SelectedCellDisplay),
        nameof(MainWindowViewModel.HasNoPropertySelection),
    };

    /// <summary>
    /// 捕捉フィルタ（設計書§2-3）。<c>ReplaceDocument</c> は <c>Document</c>・<c>CurrentFilePath</c>・
    /// <c>CanEditDiagram</c> 等、本統合と無関係な通知も同時に出す。
    /// <b>無フィルタで集合比較すれば、無関係な通知が増減しただけで本テストが無関係な理由で壊れる。</b>
    /// </summary>
    private static readonly HashSet<string> Watched =
        new(BasisProperties.Concat(AdjacentProperties));

    private static HashSet<string> Expected(params string[] extras)
        => new(BasisProperties.Concat(extras));

    /// <summary><paramref name="act"/> の間に飛んだ通知のうち、監視対象の名だけを集める。</summary>
    private static HashSet<string> Capture(MainWindowViewModel vm, Action act)
    {
        var raised = new HashSet<string>();
        void Handler(object? _, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is string name && Watched.Contains(name)) raised.Add(name);
        }

        vm.PropertyChanged += Handler;
        try { act(); }
        finally { vm.PropertyChanged -= Handler; }
        return raised;
    }

    /// <summary>要素を1つ置き、その位置を選択した状態にする（<c>T107CommentTests</c> と同じ作法）。</summary>
    private static void PlaceAt(MainWindowViewModel vm, int row, int col, string deviceName)
    {
        vm.SelectedCell = new GridPos(row, col);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, deviceName, isOr: false);
        vm.SelectedCell = new GridPos(row, col);
    }

    // ===== 2-4-a: DeleteSelectedElement =====

    /// <summary>削除経路の通知集合が基準15件と完全に一致すること。
    /// <b>統合前は同じ15件を書き写しておった</b>——統合で集合が変われば鳴る。</summary>
    [Fact]
    public void 削除経路の通知集合は基準と一致する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        var raised = Capture(vm, () => vm.DeleteSelectedElement());

        Assert.Equal(new HashSet<string>(BasisProperties), raised);
    }

    // ===== 2-4-b: SelectedCell setter（変化あり） =====

    /// <summary>選択セルを変えたときの通知集合が基準＋2件と一致すること。
    /// <b>行・列とも異なるセルへ移る</b>——同じ行や同じ列へ移る形では、
    /// 行と列を取り違える誤りが潰れて見えぬ（PR-27）。</summary>
    [Fact]
    public void 選択セル変更の通知集合は基準に二件を加えたものと一致する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        var raised = Capture(vm, () => vm.SelectedCell = new GridPos(5, 1));

        Assert.Equal(Expected(AdjacentProperties), raised);
    }

    // ===== 2-4-c: SelectedCell setter（退化入力・境界値） =====

    /// <summary>
    /// <b>同じセルを再び選んでも、基準15件と <c>SelectedCellDisplay</c> は飛ばぬこと</b>
    /// （設計書2-4-c、本章の要）。
    /// <para>
    /// 統合作業では3箇所を1つの呼び出しへ寄せる際、<b>うっかり <c>if (SetProperty(...))</c> の
    /// 外へ出しやすい</b>。外へ出れば集合は17件のまま「常に発火する」形へ崩れる
    /// ——<b>他の3件（集合を見るテスト）では、この壊れ方を一切検出できぬ。</b>
    /// </para>
    /// <para>
    /// <b>【設計書の「捕捉集合は空（0件）」は実測で誤りと判明した】</b>
    /// <c>HasNoPropertySelection</c> だけは<b>同値の再代入でも必ず1件飛ぶ</b>。
    /// 機序＝<c>SelectedCell</c> setter は <c>SetProperty</c> へ至る<b>手前</b>で
    /// <c>SelectedImage = null</c>・<c>SelectedFrame = null</c> を呼んでおり、
    /// <b>両 setter が <c>SetProperty</c> の戻り値を見ず無条件に通知する</b>ため
    /// （侍の設計書§5-2で挙げた「暗黙の依存」が、ここでも顔を出す）。
    /// <b>これは段1で生じたものではない</b>——統合が触れたのは <c>if</c> の内側のみにて、
    /// 統合前のコードでも同じ1件が飛ぶことを実測で確かめてある。
    /// </para>
    /// </summary>
    [Fact]
    public void 同じセルを選び直しても基準の通知は飛ばぬ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        var raised = Capture(vm, () => vm.SelectedCell = new GridPos(2, 3));

        Assert.Equal(new HashSet<string> { nameof(MainWindowViewModel.HasNoPropertySelection) }, raised);
    }

    // ===== 2-4-d: ReplaceDocument =====

    /// <summary>
    /// 文書差し替え（<c>NewDocument()</c> 経由）の通知集合。
    /// <c>ReplaceDocument</c> は <c>private</c> ゆえ、この経路で駆動する。
    /// <para>
    /// <b>【<c>HasNoPropertySelection</c> がここに入るのは段2の成果ではない】</b>
    /// <c>ReplaceDocument</c> は <c>SelectedImage = null</c>・<c>SelectedFrame = null</c> を必ず通り、
    /// <b>両 setter が <c>SetProperty</c> の戻り値を見ず無条件に通知する</b>ため、
    /// <b>段2の前から既に飛んでおる</b>（侍の設計書§5-2）。
    /// <b>集合では段2の寄与が見えぬ</b>——それを切り分けるのは段2の「回数」を見るテストである（同§3-4）。
    /// </para>
    /// </summary>
    [Fact]
    public void 文書差し替えの通知集合は基準に隣接二件を加えたものと一致する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        PlaceAt(vm, 2, 3, "X001");

        var raised = Capture(vm, () => vm.NewDocument());

        Assert.Equal(Expected(AdjacentProperties), raised);
    }
}
