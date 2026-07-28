using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-125増分β-1の回帰テスト。対象は2件——
/// (1) 左Down系統(LadderCanvasHost_PreviewMouseLeftButtonDown)の要素ドラッグ分岐だけが成功時に
///     returnせず、続く枠ドラッグ判定へ落ちていた件(MainWindow.xaml.cs、他7分岐はいずれもreturnする)。
/// (2) OpenFrameLabelEditorだけが枠選択の排他規約を破っていた件。規約は「SelectedFrameを設定する側が
///     先にSelectedCell=nullを呼ぶ」であり、その意図はMainWindowViewModel.cs:1441-1443および
///     :1620-1621に明記されている。SelectedCellのsetterは一方向にSelectedFrameをクリアするが、
///     逆方向(SelectedFrameを設定してもSelectedCellは消えない)は保証されない。
///
/// 【2026-07-28 重要な追記】上記(2)の修正は撤回された(殿裁定=案A')。SelectedCell=nullは値が
/// 変わらずとも5種のドラフトを常時クリアし、うち合流先確認ドラフトの取消が配置済み要素と機器表
/// エントリを削除するため、記入中に枠境界をダブルクリックすると要素が失われる実害を生んだ
/// (機序と回帰テストはT125Beta1DraftDestructivenessTestsを参照)。
/// <b>ゆえにOpenFrameLabelEditorは現在も排他規約を満たさない。</b>そこから生じる両立状態
/// (SelectedFrame≠null かつ SelectedElement≠null)はP-142として起票済みで、γ(MainWindowViewModelの
/// 責務分割)で判ずる。
///
/// <b>この撤回により、本クラスの各テストの位置づけが変わった</b>——
/// ・排他規約を対照する2件（違反の順／規約どおりの順）は「β-1の修正を守る網」ではなく
///   <b>両立状態が実在することの記録</b>として残る。P-142を判ずる際の一次資料になる。
/// ・(1)のreturn補填は撤回していないため、<b>二重ドラッグを扱う2件は引き続き回帰の網として働く</b>。
///
/// 【RED先行証明が成り立たぬ理由】上記2件はいずれもMainWindow.xaml.csのコードビハインドであり、
/// マウス配線・ヒットテスト・ラベル編集オーバーレイにはテスト基盤が無い(T067GroupFrameTestsの
/// クラスコメントに既述、T-088/T-070 A-7/T-087と同事情)。ゆえに修正の前後でRED→GREENの遷移を
/// 実測する形の証明は原理的に成り立たない。家老裁定(2026-07-27)により「根本原因自体の再現可否」へ
/// 観点を切り替え、ViewModel層で【規約違反の順】と【規約どおりの順】を対照し、修正が何を防ぐのかを
/// 実行可能な形で残す(memory: feedback_red_proof_new_api_limitation、T-102の前例に同じ)。
/// </summary>
public class T125BetaSelectionExclusivityTests : ViewModelTestBase
{
    // 行と列に異なる値を選ぶ(samurai.md「テスト入力の対称性・退化性チェック」——行と列を取り違える
    // 型のバグを、対称な値が偶然打ち消してしまうのを避けるため)。要素は列>行、枠は行>列と大小関係も
    // 逆に取り、移動先も両者で別方向へずらす。既定グリッドはRows=22/Columns=40(GridSpec)。
    private static readonly GridPos ElementPos = new(3, 7);
    private static readonly GridPos FrameTopLeft = new(5, 2);
    private static readonly GridPos ElementDragTarget = new(9, 14);
    private static readonly GridPos FrameDragTarget = new(12, 6);

    /// <summary>枠を1つ作る。ConfirmFrameDraftは規約準拠の経路(SelectedCell=null→SelectedFrame=frame)
    /// のため、戻った時点でSelectedCellはnullになっている。</summary>
    private static GroupFrame CreateFrame(MainWindowViewModel vm)
    {
        vm.BeginFrameDraft(FrameTopLeft);
        vm.ConfirmFrameDraft();
        return vm.SelectedFrame!;
    }

    /// <summary>要素を1つ配置し、選択済みにして返す。</summary>
    private static ElementInstance PlaceAndSelectElement(MainWindowViewModel vm)
    {
        vm.SelectedCell = ElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = ElementPos;
        return vm.SelectedElement!;
    }

    /// <summary>修正前のOpenFrameLabelEditorが作っていた状態を再現する——要素を選択したまま、
    /// SelectedCellへ触れずSelectedFrameだけを設定する「規約違反の順」。</summary>
    private (MainWindowViewModel Vm, ElementInstance Element, GroupFrame Frame) ArrangeCoexisting()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm);
        var element = PlaceAndSelectElement(vm);
        vm.SelectedFrame = frame;
        return (vm, element, frame);
    }

    [Fact]
    public void 排他規約違反の順_SelectedCellを残したままSelectedFrameを設定すると要素選択と両立する()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm);
        var element = PlaceAndSelectElement(vm);
        // 要素を選ぶとSelectedCellのsetterが枠選択を一方向にクリアする(MainWindowViewModel.cs:437)。
        Assert.Null(vm.SelectedFrame);

        vm.SelectedFrame = frame;

        // 逆方向の排他は無いため、要素と枠が同時に選択された状態が成立してしまう。これが
        // 修正前のOpenFrameLabelEditorが枠ラベル編集の起動時に作っていた状態そのものである。
        Assert.Same(element, vm.SelectedElement);
        Assert.Same(frame, vm.SelectedFrame);
    }

    [Fact]
    public void 排他規約どおりの順_SelectedCellをnullにしてからSelectedFrameを設定すれば両立しない()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm);
        PlaceAndSelectElement(vm);

        // 既存3箇所(MainWindow.xaml.cs 左Up枠選択・右Down枠メニュー、MainWindowViewModel.cs
        // ConfirmFrameDraft)と同じ順序。β-1でOpenFrameLabelEditorもこの形へ揃える。
        vm.SelectedCell = null;
        vm.SelectedFrame = frame;

        // SelectedElementはSelectedCellからの算出プロパティのため、両立そのものが成立しない。
        Assert.Null(vm.SelectedElement);
        Assert.Same(frame, vm.SelectedFrame);
    }

    [Fact]
    public void 両立状態では要素と枠を続けて掴め二重ドラッグになる()
    {
        var (vm, element, frame) = ArrangeCoexisting();

        // 左Down系統で要素分岐がreturnしないと、同じ押下で枠分岐も評価される。両分岐の入口条件
        // (SelectedElementが非null／SelectedFrameが非null)が同時に満たされるため、両方のドラッグが
        // 開始されうる。
        vm.BeginDragElement(element);
        vm.BeginDragFrame(frame);

        Assert.True(vm.IsDraggingElement);
        Assert.True(vm.IsDraggingFrame);
    }

    [Fact]
    public void 二重掴みで要素を先に確定すると枠のドラッグは巻き戻される()
    {
        // 左Up系統は要素の確定(IsDraggingElement分岐)を枠より先に評価しreturnする。その
        // ConfirmDragElementがSelectedCellを移動先へ追随更新するため(T-088)、SelectedCellの
        // setter経由でSelectedFrame=null→ForceCancelDragFrameIfAnyが連鎖し、枠のドラッグは
        // 開始時位置へ巻き戻される。すなわち現状ではreturn欠落の実害がこの連鎖に吸収されている。
        // 本テストはその「吸収」自体の実在を固定する——連鎖が失われれば枠が動いたまま残る。
        //
        // 【この吸収は条件付きである。「常に吸収される」と読んではならない】(隠密静的レビュー
        // 指摘2、2026-07-27) ConfirmDragElementがSelectedCellを更新するのは
        // element.Pos != _dragElementOrigPos のとき、すなわち実際に位置が変わった場合のみで、
        // 要素を掴んだが動かさずに離した場合は代入自体が起きず吸収は働かない。本テストは
        // UpdateDragElementで位置を変えた検体ゆえ吸収が働く経路を固定しているが、吸収は
        // T-088の副次効果であって意図された防御ではなく、その射程もここまでである。
        var (vm, element, frame) = ArrangeCoexisting();
        vm.BeginDragElement(element);
        vm.BeginDragFrame(frame);
        vm.UpdateDragElement(ElementDragTarget);
        vm.UpdateDragFrame(FrameDragTarget);

        vm.ConfirmDragElement();

        Assert.Equal(ElementDragTarget, element.Pos);
        Assert.False(vm.IsDraggingFrame);
        Assert.Equal(FrameTopLeft, frame.TopLeft);
        Assert.Null(vm.SelectedFrame);
    }

    [Fact]
    public void 規約どおりの順で枠を選び直してもラベル変更は従来どおり働く()
    {
        // 【2026-07-28】元はβ-1でOpenFrameLabelEditorへSelectedCell=nullを足すことへの回帰として
        // 置いた。その追加は案A'で撤回されたが、本テストが見ているのは「規約どおりの順
        // (SelectedCell=null→SelectedFrame)で枠を選び直しても確定処理(RenameSelectedFrame)が働く」
        // ことであり、既存3経路(左Up枠選択・右Down枠メニュー・ConfirmFrameDraft)がいずれもこの順序を
        // 採るため、回帰の網として有効なまま残る。
        var vm = CreateViewModel();
        vm.NewDocument();
        var frame = CreateFrame(vm);
        PlaceAndSelectElement(vm);

        vm.SelectedCell = null;
        vm.SelectedFrame = frame;
        vm.RenameSelectedFrame("枠A");

        Assert.Equal("枠A", frame.Label);
        Assert.Same(frame, vm.SelectedFrame);
    }
}
