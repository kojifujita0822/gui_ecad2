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
/// SelectedCellのsetterは値が変わらずとも5種のドラフトを常時クリアするが、5つは ClearXxxIfAny()
/// という同じ形で横一列に並びながら、5種目（ClearOrJoinTargetDraftIfAny）だけが破壊的で
/// 配置済み要素と機器表エントリを削除していた。
/// <b>並びの均質さが中身の非対称を隠す</b>——これが本件の根本原因であった。
///
/// <para><b>【T-135で仕様が変わった。2026-07-28】</b>
/// 上の段落は「非対称を記録に残す」ために書かれ、観点4のテストは<b>「このテストが落ちたら
/// 『バグが直った』ではなく『仕様が変わった』と読め」</b>と申し送っていた。
/// <b>その予言どおりになった。</b> T-102殿裁定＝解釈(i)「要素配置ごと取消」は<b>Escについての
/// 裁定</b>であり、SelectedCellのsetter経由は射程外であった（docs/proposed.md P-144）。
/// 殿裁定2026-07-28＝案(1)により、<b>巻き戻すのはEsc・文書差し替えの2経路に限り</b>、
/// 別セルクリック・AppMode切替はドラフトだけを畳む。<b>5種の非対称は解消し、横一列の並びと
/// 中身が一致した。</b>
/// ゆえに観点4のテストは期待値を反転させ、対としてEsc経路の回帰も併せて置いた。
/// T-135の網羅的な回帰は T135OrJoinDraftExitTests が受け持つ。</para>
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
    // 観点4【T4-5】合流先確認ドラフトも他4種と同じく非破壊であること（T-135で揃えた）
    // ------------------------------------------------------------------

    /// <summary>
    /// <b>【T-135で期待値を反転させた。以前は逆を表明していたテストである】</b>
    /// <para>
    /// 当初（T-125β-1）は「5種の中で合流先確認ドラフトだけが破壊的」という非対称を記録に残すために
    /// 置いた。T-102 殿裁定＝解釈(i)「要素配置ごと取消」がその根拠であった。
    /// </para>
    /// <para>
    /// <b>だが裁定はEscについてのものであり、SelectedCellのsetter経由は射程外であった</b>
    /// （docs/proposed.md P-144）。取消の意図が無いのに要素が消え、配置時のスナップショットは
    /// 「配置前」ゆえUndoでも戻らぬ。<b>殿裁定2026-07-28＝案(1)により、この経路は要素を残す</b>
    /// （OR接続されぬ要素が残ることも併せて裁可＝「残してよい」）。
    /// </para>
    /// <para>
    /// <b>検出力について</b>：本テストの眼目は「非対称の記録」から「5種が揃ったことの記録」へ移った。
    /// 巻き戻しが復活すれば要素・機器表の両方が落ちるゆえ、回帰の網としては以前より強い
    /// （以前は「消えること」を期待していたため、誤って消し過ぎても気づけなかった）。
    /// </para>
    /// </summary>
    [Fact]
    public void 合流先確認ドラフト保持中のSelectedCellクリアでも配置済み要素は残る()
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

        // T-135: 要素も機器表エントリも残る。畳まれるのはドラフトだけ（他4種と同じ挙動）。
        Assert.Equal(2, vm.CurrentSheet!.Elements.Count);
        Assert.Equal("X002", vm.CurrentSheet!.Elements[1].DeviceName);
        Assert.Equal(2, vm.Document.Devices.ByName.Count);
        Assert.True(vm.Document.Devices.ByName.ContainsKey("X002"));
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.False(vm.HasAnyDraft);
    }

    /// <summary>
    /// T-135: Esc経路は従来どおり要素配置ごと巻き戻す（殿裁定 T102裁4＝解釈(i)は健在）。
    /// 上のテストと対で置くことで、<b>「両経路とも非破壊にしてしまう」という行き過ぎ</b>を検出する。
    /// </summary>
    [Fact]
    public void 合流先確認ドラフトのEsc取消は従来どおり要素配置ごと巻き戻す()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = FirstElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);
        vm.SelectedCell = OrElementPos;
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X002", isOr: true);
        Assert.Equal(ToolMode.ConfirmOrJoinTarget, vm.Tool.Mode);

        vm.CancelOrJoinTarget();

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
    /// 「ツールモードを問わず」発火するが、選択状態に一切触れぬゆえ無害である（侍が全文を直読して確認。
    /// 同メソッドがViewModelに対して行うのは GetRungComment の読み取りと IsRungCommentEditorVisible の
    /// 点灯の2つのみ）。
    /// <b>「モードを問わぬ」方針それ自体は無害であり、無害だったのは選択状態に触れなかったからにござる。</b>
    ///
    /// 【本テストの射程——過大に読んではならぬ】OpenRungCommentEditor は View 層の private メソッドで
    /// 直接呼べぬため、本テストは<b>その中身を手で模倣しておる</b>。
    /// <b>ゆえに将来この経路へ SelectedCell=null が足されても、本テストは鳴らぬ。</b>
    /// 本テストが記録できるのは「現時点で、模倣した2操作は選択状態に触れぬ」という事実までである
    /// （設計書 観点5、onmitsu.md「修正の横展開確認」）。
    /// <b>将来の変更を捕まえる網が要るなら、View 層にテスト基盤を設けるか、判定を ViewModel 側の
    /// 純粋メソッドへ切り出す設計変更が要る</b>——本修正の範囲外ゆえ、ここでは射程を明示するに留める。
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
