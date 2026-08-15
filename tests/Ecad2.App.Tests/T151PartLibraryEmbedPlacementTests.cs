using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-151（自作パーツ定義を図面へ埋め込む・`P-183`対処）の保存側テスト。
/// 隠密テスト設計 docs/ecad2-t151-test-design-onmitsu.md 6-1節（2節の状態遷移表・3節の対称性チェック）。
/// <para>
/// 原本GuiEcadは配置のたびに <c>_document.Library.ById[id] = def;</c> で定義を図面へ写しており、
/// 「.gcad は自己完結（portable）」という設計思想を保っていた。ecad2 は器（<see cref="LadderDocument.Library"/>）
/// だけを移植し配線が落ちていたため、自作パーツを含む図面を別環境で開くと a接点へ黙って化けていた。
/// 本テスト群は、その配線が保存側で復活していることを固定する。
/// </para>
/// </summary>
public class T151PartLibraryEmbedPlacementTests : ViewModelTestBase
{
    private const string CustomXId = "t151-custom-x";
    private const string CustomYId = "t151-custom-y";

    /// <summary>識別しやすい自作パーツ定義を作る。既定値（WidthCells=1・Role=ContactNO）と
    /// 意図的にずらせるよう、幅と役割を呼び手から選べるようにしてある（設計書3節の対称性チェック
    /// ＝「たまたま組込み既定へのフォールバックと同じ絵になって見分けがつかない」迂回路を塞ぐ）。</summary>
    private static PartDefinition CustomPart(string id, string name, int widthCells = 1,
                                             PartRole role = PartRole.ContactNO) =>
        new()
        {
            Id = id,
            Name = name,
            WidthCells = widthCells,
            HeightCells = 1,
            Role = role,
            Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, widthCells) },
            Primitives = new() { new PartLine(0, 0, widthCells, 0) },
        };

    /// <summary>ローカルの「図形/自作」へ定義を置き、パレットへ反映した状態のViewModelを作る。
    /// <see cref="ViewModelTestBase"/> がテストごとに一時フォルダを発行するため、
    /// ここで置いたものだけがローカル定義になる。</summary>
    private MainWindowViewModel CreateViewModelWithLocalParts(params PartDefinition[] parts)
    {
        var vm = CreateViewModel();
        foreach (var part in parts) vm.PartPalette.SaveNewPart(part);
        vm.NewDocument();
        return vm;
    }

    /// <summary>2節1行目＝Library=null から非nullへの遷移そのもの（遅延初期化の境界）。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_NullLibrary_EmbedsDefinitionAndInitializesLibrary()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomXId, "T151自作X", widthCells: 5));
        // 前提の明示: NewDocument 直後は Library=null（Document.cs:12 の既定）。
        Assert.Null(vm.Document.Library);
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(CustomXId, "CR1", isOr: false);

        Assert.NotNull(vm.Document.Library);
        var embedded = vm.Document.Library!.Get(CustomXId);
        Assert.NotNull(embedded);
        Assert.Equal("T151自作X", embedded!.Name);
        // 幅5は組込み15種のいずれとも異なる。既定値1へ退化していないことを直接示す。
        Assert.Equal(5, embedded.WidthCells);
    }

    /// <summary>2節2行目＝同じパーツを複数回配置しても埋め込みは1件のまま（冪等性・重複キーの頑健性）。</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public void PlaceElementAtSelectedCell_EmbedsDefinitionIdempotently(int placementCount, int expectedCount)
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomXId, "T151自作X"));

        for (int i = 0; i < placementCount; i++)
        {
            // 行を変えて置く（同一セルは IsOccupied で拒否され、配置自体が成立しないため）。
            vm.SelectedCell = new GridPos(i, 0);
            vm.PlaceElementAtSelectedCell(CustomXId, $"CR{i + 1}", isOr: false);
        }

        Assert.NotNull(vm.Document.Library);
        Assert.Equal(expectedCount, vm.Document.Library!.ById.Count);
        // 配置回数そのものは成立していること（配置が拒否されていれば冪等性を測ったことにならぬ）。
        Assert.Equal(placementCount, vm.Document.Sheets[0].Elements.Count);
    }

    /// <summary>2節3行目＝別のパーツを置いても既存の埋め込みは変化しない（複数件の独立性）。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_MultipleDistinctParts_EmbedsIndependently()
    {
        var vm = CreateViewModelWithLocalParts(
            CustomPart(CustomXId, "T151自作X", widthCells: 5),
            CustomPart(CustomYId, "T151自作Y", widthCells: 3, role: PartRole.Coil));

        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomXId, "CR1", isOr: false);
        vm.SelectedCell = new GridPos(1, 0);
        vm.PlaceElementAtSelectedCell(CustomYId, "CR2", isOr: false);

        Assert.NotNull(vm.Document.Library);
        Assert.Equal(2, vm.Document.Library!.ById.Count);
        // Xの内容がYの配置で書き換わっていないこと。
        Assert.Equal(5, vm.Document.Library.Get(CustomXId)!.WidthCells);
        Assert.Equal(PartRole.ContactNO, vm.Document.Library.Get(CustomXId)!.Role);
        Assert.Equal(3, vm.Document.Library.Get(CustomYId)!.WidthCells);
        Assert.Equal(PartRole.Coil, vm.Document.Library.Get(CustomYId)!.Role);
    }

    /// <summary>2節4行目＝PartIdを伴わないKind経路（MainWindowViewModel.cs:3296）は Library に触れない。
    /// 対象外パスへの無干渉確認。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_BuiltinSymbolOnly_DoesNotTouchDocumentLibrary()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomXId, "T151自作X"));
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(ElementKind.ContactNO, orient: null);

        // 配置自体は成立していること（成立していなければ「触れていない」を測ったことにならぬ）。
        Assert.Single(vm.Document.Sheets[0].Elements);
        Assert.Null(vm.Document.Library);
    }

    /// <summary>
    /// 3節＝検出力の核心。配置後にローカル定義そのもの（同一インスタンス）を書き換え、
    /// 埋め込み側が配置時点の値のまま動かないことを測る。
    /// <para>
    /// <b>【なぜ同一インスタンスを直に書き換えるか】</b>設計書3節は <c>SaveEditedPart</c> 等を例示するが、
    /// あちらは <c>PartFolderStore</c> へ書き出して <c>Load()</c> でディスクから読み直す
    /// （<c>PartPaletteViewModel.cs:136-142</c>→<c>:74-88</c>）ゆえ、<b>ローカル側の
    /// <see cref="PartDefinition"/> が別インスタンスに入れ替わる</b>。すなわち実装が参照を共有していても
    /// 埋め込み側の値は動かず、<b>参照共有と複製を弁別できぬ</b>。同一インスタンスを直に書き換える形なら、
    /// 参照共有なら埋め込み側も一緒に変わる＝一撃で見分けがつく。
    /// </para>
    /// <para>使い手の実操作に近い <c>SaveEditedPart</c> 経由は
    /// <see cref="PlaceElementAtSelectedCell_ThenSaveEditedPart_EmbeddedValueStaysAtPlacementTime"/>
    /// が別に測る（こちらは検出力ではなく、実際の編集導線で仕様どおりに見えることの確認）。</para>
    /// </summary>
    [Fact]
    public void PlaceElementAtSelectedCell_ThenMutateLocalDefinition_EmbeddedValueStaysAtPlacementTime()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomXId, "T151自作X", widthCells: 5));
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomXId, "CR1", isOr: false);

        // ローカル側の定義インスタンスを直接書き換える。
        var local = vm.PartPalette.Library.Get(CustomXId);
        Assert.NotNull(local);
        local!.WidthCells = 99;
        local.Name = "書き換え後の名";

        var embedded = vm.Document.Library!.Get(CustomXId);
        Assert.NotNull(embedded);
        Assert.Equal(5, embedded!.WidthCells);
        Assert.Equal("T151自作X", embedded.Name);
    }

    /// <summary>3節の応用＝使い手の実際の編集導線（パーツエディタ＝<c>SaveEditedPart</c>）を通しても、
    /// 既存図面の埋め込み定義は配置時点のまま。台帳T-151節の検証観点
    /// 「ローカルでパーツを直しても既存図面が変わらぬこと（仕様どおりであることの確認）」に対応する。
    /// <para>上のテストと違い、こちらは参照共有を弁別できぬ（ディスク往復で別インスタンスになるため）。
    /// 検出力は上に譲り、本テストは導線の実地確認を担う。</para></summary>
    [Fact]
    public void PlaceElementAtSelectedCell_ThenSaveEditedPart_EmbeddedValueStaysAtPlacementTime()
    {
        var vm = CreateViewModelWithLocalParts(CustomPart(CustomXId, "T151自作X", widthCells: 5));
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(CustomXId, "CR1", isOr: false);

        string oldPath = vm.PartPalette.Entries.Single(e => e.Definition.Id == CustomXId).FilePath;
        var edited = CustomPart(CustomXId, "T151自作X", widthCells: 99);
        vm.PartPalette.SaveEditedPart(edited, oldPath);

        // ローカル側は編集が反映されている（前提の確認）。
        Assert.Equal(99, vm.PartPalette.Library.Get(CustomXId)!.WidthCells);
        // 図面側は配置時点の値のまま。
        Assert.Equal(5, vm.Document.Library!.Get(CustomXId)!.WidthCells);
    }
}
