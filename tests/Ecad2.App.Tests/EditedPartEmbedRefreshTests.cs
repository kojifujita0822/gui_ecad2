using System.IO;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.App.Tests;

/// <summary>
/// パーツエディタでの編集保存が、開いておる図面の埋め込み定義へ届くことのテスト
/// （殿ご裁可2026-10-09＝案A）。
/// <para>
/// 発端は殿ご指摘——<b>非シミュレートで一度置いた自作パーツは、後から種別をコイルへ直しても
/// 線番が付かなんだ</b>。T-151 以後、配置済みのパーツは図面に埋め込まれた写しで解決され、
/// その写しは既に在る Id を上書きせぬゆえ、編集がどこからも図面へ届いておらなんだ。
/// </para>
/// </summary>
public class EditedPartEmbedRefreshTests : ViewModelTestBase
{
    private const string ContactId = "edit-refresh-contact";
    private const string TargetId = "edit-refresh-target";

    private static PartDefinition CustomPart(string id, string name, PartRole role) =>
        new()
        {
            Id = id,
            Name = name,
            WidthCells = 1,
            HeightCells = 1,
            Role = role,
            Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
            Primitives = new() { new PartLine(0, 0, 1, 0) },
        };

    /// <summary>接点役の自作パーツ（列0）と、対象の自作パーツ（列3）を同じ行へ置いた状態を作る。
    /// 対象が結線に加われば、両者の間に母線へ繋がらぬ内部ネットが1つ生まれ、線番が付く。</summary>
    private MainWindowViewModel CreateViewModelWithPlacedParts(PartRole targetRole)
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(CustomPart(ContactId, "編集反映_接点", PartRole.ContactNO));
        vm.PartPalette.SaveNewPart(CustomPart(TargetId, "編集反映_対象", targetRole));
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(ContactId, "X1", isOr: false);
        vm.SelectedCell = new GridPos(0, 3);
        vm.PlaceElementAtSelectedCell(TargetId, "SOL1", isOr: false);
        return vm;
    }

    private static string PathOf(MainWindowViewModel vm, string id) =>
        vm.PartPalette.Entries.Single(e => e.Definition.Id == id).FilePath;

    private static int NumberedNetCount(MainWindowViewModel vm) =>
        NetlistBuilder.Build(vm.Document.Sheets[0], vm.PartLibrary).Nets.Count(n => n.WireNumber > 0);

    /// <summary>一時ファイルへ保存して変更ありを落とす（IsDirty は外から直に下ろせぬため）。</summary>
    private static void ClearDirtyBySaving(MainWindowViewModel vm)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-edit-refresh-{Guid.NewGuid():N}.gcad");
        try { vm.SaveToFile(path); }
        finally { File.Delete(path); }
        Assert.False(vm.IsDirty);
    }

    /// <summary>殿ご指摘の再現そのもの。非シミュレートで置いた後に種別をコイルへ直すと、線番が付く。</summary>
    [Fact]
    public void SaveEditedPart_RoleChangedFromNonSimulatedToCoil_WireNumberAppears()
    {
        var vm = CreateViewModelWithPlacedParts(PartRole.NonSimulated);
        // 前提: 対象が結線の外に在る間は、接点が左右とも母線へ繋がり、番号の付くネットが無い。
        Assert.Equal(0, NumberedNetCount(vm));

        bool refreshed = vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        Assert.True(refreshed);
        Assert.Equal(PartRole.Coil, vm.Document.Library!.Get(TargetId)!.Role);
        Assert.Equal(1, NumberedNetCount(vm));
    }

    /// <summary>図面側を改めたら変更ありになる（保存せねば編集が図面ファイルへ残らぬため）。</summary>
    [Fact]
    public void SaveEditedPart_EmbeddedDefinitionRefreshed_MarksDocumentDirty()
    {
        var vm = CreateViewModelWithPlacedParts(PartRole.NonSimulated);
        ClearDirtyBySaving(vm);

        vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        Assert.True(vm.IsDirty);
    }

    /// <summary>Undo 一回で図面側の定義が編集前へ戻る。</summary>
    [Fact]
    public void SaveEditedPart_ThenUndo_RestoresPreviousEmbeddedDefinition()
    {
        var vm = CreateViewModelWithPlacedParts(PartRole.NonSimulated);
        vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        vm.UndoCommand.Execute(null);

        Assert.Equal(PartRole.NonSimulated, vm.Document.Library!.Get(TargetId)!.Role);
        // 配置までは巻き戻っておらぬこと（Undo が一回分だけ積まれた証）。
        Assert.Equal(2, vm.Document.Sheets[0].Elements.Count);
    }

    /// <summary>中身を変えずに保存しただけなら、Undo も変更ありも生じぬ。</summary>
    [Fact]
    public void SaveEditedPart_DefinitionUnchanged_DoesNotRecordUndoNorMarkDirty()
    {
        var vm = CreateViewModelWithPlacedParts(PartRole.Coil);
        ClearDirtyBySaving(vm);
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        bool refreshed = vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        Assert.False(refreshed);
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        Assert.False(vm.IsDirty);
    }

    /// <summary>図面に置いておらぬパーツを編集しても、図面には何も足されぬ（埋め込みは配置の時のみ）。</summary>
    [Fact]
    public void SaveEditedPart_PartNotPlacedInDocument_DoesNotTouchDocument()
    {
        var vm = CreateViewModel();
        vm.PartPalette.SaveNewPart(CustomPart(TargetId, "編集反映_対象", PartRole.NonSimulated));
        vm.NewDocument();
        int undoDepthBefore = vm.UndoManager.UndoDepth;

        bool refreshed = vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        Assert.False(refreshed);
        Assert.Null(vm.Document.Library);
        Assert.Equal(undoDepthBefore, vm.UndoManager.UndoDepth);
        // ローカル側には編集が届いておること（保存自体は成立しておる）。
        Assert.Equal(PartRole.Coil, vm.PartPalette.Library.Get(TargetId)!.Role);
    }

    /// <summary>他のパーツの埋め込みは巻き込まぬ。</summary>
    [Fact]
    public void SaveEditedPart_OtherEmbeddedDefinitions_StayUntouched()
    {
        var vm = CreateViewModelWithPlacedParts(PartRole.NonSimulated);
        var contactBefore = vm.Document.Library!.Get(ContactId);

        vm.SaveEditedPart(CustomPart(TargetId, "編集反映_対象", PartRole.Coil), PathOf(vm, TargetId));

        Assert.Same(contactBefore, vm.Document.Library!.Get(ContactId));
    }
}
