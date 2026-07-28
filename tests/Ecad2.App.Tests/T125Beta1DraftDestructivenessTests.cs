using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-125増分β-1実害修正（殿裁定2026-07-28＝案A'＝OpenFrameLabelEditorのSelectedCell=nullを撤回）の
/// 回帰テスト。隠密のテスト設計書 docs/ecad2-t125-beta1-fix-test-design-onmitsu.md の
/// 観点4（5種ドラフトの破壊性の表明）・観点5（行コメント編集への横展開）を実装する。
///
/// 【本テストの性格】観点4は「バグを固定するテスト」ではなく<b>性質の記録</b>である。
/// SelectedCellのsetter（MainWindowViewModel.cs:428-463）は値が変わらずとも5種のドラフトを常時
/// クリアするが、5つは ClearXxxIfAny() という同じ形で横一列に並びながら、5種目
/// （ClearOrJoinTargetDraftIfAny）だけが破壊的で配置済み要素と機器表エントリを削除する。
/// <b>並びの均質さが中身の非対称を隠す</b>——これが本件の根本原因であった。
/// 将来また別の経路が SelectedCell=null を呼んだとき、この非対称が既知として残るよう表明しておく。
///
/// 【T4-5は仕様どおりの正しい振る舞いである】合流先確認ドラフトの取消が要素配置ごと巻き戻すのは
/// T-102殿裁定＝解釈(i)「要素配置ごと取消」のとおりで、Esc経路ではこれが正しい。
/// <b>問題は処理そのものではなく、意図せぬ経路から呼ばれたことにあった。</b>
/// ゆえにこのテストが落ちたら「バグが直った」ではなく「仕様が変わった」と読むこと。
///
/// 【観点1〜3（実機）は本テストの対象外】修正対象は MainWindow.xaml.cs＝View層コードビハインドで
/// テスト基盤が無く、RED先行証明は原理的に成り立たない（β-1と同じ理由、家老裁定2026-07-27）。
/// 忍者の再現手順が受け入れ条件となる（設計書§4-3）。
/// </summary>
public class T125Beta1DraftDestructivenessTests : ViewModelTestBase
{
    /// <summary>SelectedCellのsetterがクリアする5種ドラフトのうち、非破壊な4種（設計書 表2-1）。</summary>
    public enum NonDestructiveDraft { Connector, FreeLine, ImageInsert, Frame }

    // 行と列に異なる値を選び、対称・退化した入力を避ける（samurai.md「テスト入力の対称性・退化性
    // チェック」）。列0・行0を使わぬのは、「クリアされて0になった」のか「もともと0だった」のかを
    // 弁別できなくなるのを防ぐため。既定グリッドは Rows=22 / Columns=40（GridSpec）。
    private static readonly GridPos FirstElementPos = new(5, 10);
    private static readonly GridPos OrElementPos = new(7, 10);
    private static readonly GridPos FrameAnchor = new(2, 3);

    /// <summary>要素を1つ配置し、機器表にも登録された状態を作る。合流先確認ドラフトは立てない。</summary>
    private MainWindowViewModel ArrangeWithOneElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = FirstElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        return vm;
    }

    /// <summary>指定の非破壊ドラフトを立てる。いずれも SelectedCell の setter を経由しないため、
    /// 直前に配置した要素・選択状態は保たれる。</summary>
    private static void BeginDraft(MainWindowViewModel vm, NonDestructiveDraft species)
    {
        switch (species)
        {
            case NonDestructiveDraft.Connector:
                // SelectedCell を読むだけで setter を経由しない（MainWindowViewModel.cs:448-449 に明記）。
                vm.BeginConnectorDraft();
                break;
            case NonDestructiveDraft.FreeLine:
                // 水平・非原点の始点を取る（軸上・原点という退化入力を避ける）。
                Assert.True(vm.BeginFreeLineDraft(horizontal: true, startXMm: 40.0, startYMm: 25.0, stepMm: 9.0));
                break;
            case NonDestructiveDraft.ImageInsert:
                // ファイルの実在は問われない（BeginImageInsertDraft はパスを保持するだけ）。
                vm.BeginImageInsertDraft("C:\\dummy\\trace.png", widthMm: 60.0, heightMm: 40.0, xMm: 30.0, yMm: 20.0);
                break;
            case NonDestructiveDraft.Frame:
                vm.BeginFrameDraft(FrameAnchor);
                break;
        }
    }

    // ------------------------------------------------------------------
    // 観点4【T4-1〜T4-4】非破壊4種——SelectedCell のクリアで要素・機器表は失われぬ
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(NonDestructiveDraft.Connector)]
    [InlineData(NonDestructiveDraft.FreeLine)]
    [InlineData(NonDestructiveDraft.ImageInsert)]
    [InlineData(NonDestructiveDraft.Frame)]
    public void 非破壊ドラフト保持中のSelectedCellクリアは配置済み要素も機器表も失わぬ(NonDestructiveDraft species)
    {
        var vm = ArrangeWithOneElement();
        BeginDraft(vm, species);
        // 前提の成立を確かめる（ドラフトが実際に立っていなければ、以降の検証は何も見ていない）。
        Assert.True(vm.HasAnyDraft);

        vm.SelectedCell = null;

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Single(vm.Document.Devices.ByName);
        Assert.Equal("X001", vm.CurrentSheet!.Elements[0].DeviceName);
        // ドラフト自体はクリアされる（これは4種とも同じ＝失うのは記入中プレビューのみ）。
        Assert.False(vm.HasAnyDraft);
    }

    // ------------------------------------------------------------------
    // 観点4【T4-5】合流先確認ドラフトだけが破壊的であることの明示的表明
    // ------------------------------------------------------------------

    /// <summary>
    /// <b>これはバグの固定ではなく仕様の表明である。</b>合流先確認ドラフトの取消が要素配置ごと
    /// 巻き戻すのは T-102 殿裁定＝解釈(i) のとおりで、Esc 経路では正しい。
    /// 本テストは「5種の中でこれだけが破壊的」という非対称を記録に残すために置く。
    /// </summary>
    [Fact]
    public void 合流先確認ドラフト保持中のSelectedCellクリアは配置済み要素を取り消す_仕様の表明()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = FirstElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = OrElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X002", isOr: true);

        // 前提＝OR配置が合流先確認モードへ遷移していること。ここが成立せねば以降は何も見ていない。
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Equal(2, vm.Document.Devices.ByName.Count);

        vm.SelectedCell = null;

        // 2件目の要素と、その配置で新規登録された機器表エントリが消える。
        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal("X001", vm.CurrentSheet!.Elements[0].DeviceName);
        Assert.Single(vm.Document.Devices.ByName);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X002"));
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
    }

    // ------------------------------------------------------------------
    // 観点5【T5-1】横展開——行コメント編集の経路には同型の穴が無い
    // ------------------------------------------------------------------

    /// <summary>
    /// 行コメント編集（OpenRungCommentEditor、MainWindow.xaml.cs:3776-3787）は枠ラベル編集と同じく
    /// 「ツールモードを問わず」発火するが、選択状態に一切触れぬゆえ無害である（侍が全文を直読して確認）。
    /// <b>「モードを問わぬ」方針それ自体は無害であり、無害だったのは選択状態に触れなかったからにござる。</b>
    /// 本テストは現状も無害だが、将来この経路へ SelectedCell=null が足された時に鳴る網として先に張る
    /// （設計書 観点5、onmitsu.md「修正の横展開確認」）。
    /// </summary>
    [Fact]
    public void 行コメント編集の経路は合流先確認ドラフト保持中でも配置済み要素を失わぬ()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = FirstElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = OrElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X002", isOr: true);
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);

        // OpenRungCommentEditor が ViewModel に対して行うことと同じ操作を、同じ順序で行う
        // （GetRungComment の読み取り → IsRungCommentEditorVisible の点灯）。
        _ = vm.GetRungComment(FirstElementPos.Row);
        vm.IsRungCommentEditorVisible = true;

        // 要素・機器表とも無傷で、ドラフトも生きたまま残る。
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Equal(2, vm.Document.Devices.ByName.Count);
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);
        Assert.True(vm.HasAnyDraft);
    }
}
