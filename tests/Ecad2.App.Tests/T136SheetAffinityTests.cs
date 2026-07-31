using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-136(A)増分1「シート種別の枷」の単体テスト（殿裁定2026-07-31＝3値・既定 <c>Any</c>・
/// 拒否はサイレント）。<c>ValidatePlacement</c> が private ゆえ、配置・移動の公開経路から測る。
///
/// <para><b>【入力値の選び方】</b>
/// (a) <b>グリッドを非対称に</b>（<c>Rows=5 / Columns=10</c>、位置も行と列で別の値）——
/// 行と列を取り違える改変が結果に現れるようにする。
/// (b) <b>枷の3値すべてを、2種のシート双方で測る</b>——片側だけでは真偽の反転が消える。
/// (c) <b>「置ける」側も必ず測る</b>——拒否がサイレントゆえ、枷が効きすぎて全部拒否になっても
/// 「拒否された」テストだけでは気づけぬ。</para>
/// </summary>
public class T136SheetAffinityTests : ViewModelTestBase
{
    private const int Rows = 5;
    private const int Columns = 10;
    private static readonly GridPos PlacePos = new(2, 6);   // 行と列で別の値

    /// <summary>非対称なグリッドを持つ文書を作る。<paramref name="mainCircuit"/> でシート種別を選ぶ。</summary>
    private MainWindowViewModel Arrange(bool mainCircuit)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid = new GridSpec { Rows = Rows, Columns = Columns };
        vm.CurrentSheet!.MainCircuit = mainCircuit;
        return vm;
    }

    private static void RegisterPart(MainWindowViewModel vm, string id, SheetAffinity affinity)
        => vm.PartLibrary.ById[id] = new PartDefinition
        {
            Id = id,
            Name = $"検体({affinity})",
            Role = PartRole.NonSimulated,
            SheetAffinity = affinity,
        };

    // ==================================================================
    // 配置（PartId 経路）
    // ==================================================================

    [Theory]
    [InlineData(SheetAffinity.Any, false, true)]
    [InlineData(SheetAffinity.Any, true, true)]
    [InlineData(SheetAffinity.ControlOnly, false, true)]        // 制御シートには置ける
    [InlineData(SheetAffinity.ControlOnly, true, false)]        // 主回路シートには置けぬ
    [InlineData(SheetAffinity.MainCircuitOnly, false, false)]   // 制御シートには置けぬ
    [InlineData(SheetAffinity.MainCircuitOnly, true, true)]
    public void 自作パーツは枷に合うシートにのみ置ける(
        SheetAffinity affinity, bool mainCircuit, bool expectPlaced)
    {
        var vm = Arrange(mainCircuit);
        RegisterPart(vm, "p1", affinity);
        vm.SelectedCell = PlacePos;

        vm.PlaceElementAtSelectedCell("p1", "", isOr: false);

        Assert.Equal(expectPlaced ? 1 : 0, vm.CurrentSheet!.Elements.Count);
    }

    [Fact]
    public void 枷に反した配置はUndo履歴を積まない()
    {
        // ValidatePlacement を通過した後に RecordSnapshot する既存の順序（T-134）が、
        // 新しい枷でも保たれることを固定する——拒否でも積めば「押しても何も変わらぬUndo」が挟まる。
        var vm = Arrange(mainCircuit: false);
        RegisterPart(vm, "main-only", SheetAffinity.MainCircuitOnly);
        vm.SelectedCell = PlacePos;
        int before = vm.UndoManager.UndoDepth;

        vm.PlaceElementAtSelectedCell("main-only", "", isOr: false);

        Assert.Empty(vm.CurrentSheet!.Elements);
        Assert.Equal(before, vm.UndoManager.UndoDepth);
    }

    // ==================================================================
    // 移動（矢印キー経路）
    //
    // 【この節が固定する仕様】枷は移動にも効く。ゆえに<b>枷に反する要素は動かせぬ</b>——
    // 既に置かれた要素の部品定義を後から「主回路専用」へ変えると、制御シート上のその要素は
    // 移動できなくなる（削除はできる）。侍が増分1の実装中に気づいた含意であり、計画書には
    // 書いていなかった。拒否がサイレントゆえ使い手には理由が見えぬ点も併せ、家老へ報告済み。
    // ==================================================================

    [Fact]
    public void 枷に合う要素は従来どおり移動できる()
    {
        // 「置ける」側の対照。これが無いと、枷が効きすぎて全部の移動を止めても気づけぬ。
        var vm = Arrange(mainCircuit: false);
        RegisterPart(vm, "control-only", SheetAffinity.ControlOnly);
        vm.SelectedCell = PlacePos;
        vm.PlaceElementAtSelectedCell("control-only", "", isOr: false);
        vm.SelectedCell = PlacePos;

        Assert.True(vm.MoveSelectedElement(deltaRow: 1, deltaColumn: 0));
        Assert.Equal(new GridPos(3, 6), vm.CurrentSheet!.Elements[0].Pos);
    }

    [Fact]
    public void 枷に反する要素は移動できぬ()
    {
        var vm = Arrange(mainCircuit: false);
        RegisterPart(vm, "p1", SheetAffinity.Any);
        vm.SelectedCell = PlacePos;
        vm.PlaceElementAtSelectedCell("p1", "", isOr: false);
        vm.SelectedCell = PlacePos;

        // 置いた後で部品定義の側を「主回路専用」へ変える（殿が後から枷を設定なさる運用）。
        vm.PartLibrary.ById["p1"].SheetAffinity = SheetAffinity.MainCircuitOnly;

        Assert.False(vm.MoveSelectedElement(deltaRow: 1, deltaColumn: 0));
        Assert.Equal(PlacePos, vm.CurrentSheet!.Elements[0].Pos);   // その場に留まる
    }

    [Fact]
    public void 組込み種別の要素は種別の側の枷で移動が決まる()
    {
        // PartId を持たぬ要素は ElementCatalog 側から解決される（PartResolver の二分岐）。
        // 3極記号は主回路専用ゆえ、制御シート上では動かせぬ。
        var vm = Arrange(mainCircuit: false);
        vm.CurrentSheet!.Elements.Add(new ElementInstance
        {
            Kind = ElementKind.Breaker3P,
            Pos = PlacePos,
            CellWidth = 1,
            CellHeight = 1,
        });
        vm.SelectedCell = PlacePos;

        Assert.False(vm.MoveSelectedElement(deltaRow: 1, deltaColumn: 0));

        // 対照＝同じ経路・同じ位置でも、枷の無い種別なら動く（枷以外の理由で止まっておらぬ証）。
        vm.CurrentSheet!.Elements[0].Kind = ElementKind.ContactNO;
        Assert.True(vm.MoveSelectedElement(deltaRow: 1, deltaColumn: 0));
    }
}
