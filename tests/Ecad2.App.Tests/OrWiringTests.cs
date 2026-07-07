using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-044: OR自動配線の左縦分岐省略ロジックの回帰テスト(殿直接要望、隠密事前調査
/// docs/ecad2-t044-presurvey-onmitsu.md)。配置行・基準行の両方でOR左接続点と左母線の間に既存要素が
/// 無い場合のみ左縦分岐を省略する(トポロジー等価保証ケース限定)。いずれかの行に既存要素があれば
/// 誤ったバイパス配線を防ぐため縦分岐を維持する。右(合流側)縦分岐は常に生成される(従来維持)。
/// </summary>
public class OrWiringTests : ViewModelTestBase
{
    private static VerticalConnector? FindConnector(MainWindowViewModel vm, int column, int topRow, int bottomRow)
        => vm.CurrentSheet!.Connectors.FirstOrDefault(
            c => c.Column == column && c.TopRow == topRow && c.BottomRow == bottomRow);

    [Fact]
    public void PlaceOr_AtColumnZero_OmitsLeftConnectorButKeepsRightConnector()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 基準行(行0)の要素、列0

        vm.SelectedCell = new GridPos(1, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X1", isOr: true);  // 配置行(行1)、列0

        Assert.Null(FindConnector(vm, column: 0, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 1, topRow: 0, bottomRow: 1));
    }

    [Fact]
    public void PlaceOr_AtMiddleColumnWithBothRowsClear_OmitsLeftConnectorButKeepsRightConnector()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 基準行(行0)の要素、列2(左に何も無い)

        vm.SelectedCell = new GridPos(1, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "X1", isOr: true);  // 配置行(行1)、列2(左に何も無い)

        Assert.Null(FindConnector(vm, column: 2, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 0, bottomRow: 1));
    }

    [Fact]
    public void PlaceOr_WithElementLeftOfBaseRow_KeepsLeftConnectorToAvoidBypass()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 基準行(行0)、列0の既存要素(遮る側)
        vm.SelectedCell = new GridPos(0, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 基準行(行0)、列2(OR対応先)

        vm.SelectedCell = new GridPos(1, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "X1", isOr: true);  // 配置行(行1)、列2

        // 基準行(列0)に既存要素があるため、これをバイパスしないよう左縦分岐は維持される。
        Assert.NotNull(FindConnector(vm, column: 2, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 0, bottomRow: 1));
    }

    [Fact]
    public void PlaceOr_WithElementLeftOfPlacementRow_KeepsLeftConnectorToAvoidBypass()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 基準行(行0)、列2(左に何も無い)

        vm.SelectedCell = new GridPos(1, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);   // 配置行(行1)、列0の既存要素(遮る側)
        vm.SelectedCell = new GridPos(1, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "X1", isOr: true);  // 配置行(行1)、列2

        // 配置行(列0)に既存要素があるため、これをバイパスしないよう左縦分岐は維持される。
        Assert.NotNull(FindConnector(vm, column: 2, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 0, bottomRow: 1));
    }
}
