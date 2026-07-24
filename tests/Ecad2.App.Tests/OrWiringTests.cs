using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Simulation;

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
        vm.ConfirmOrJoinTarget();  // T-102: isOr配置は合流先確認モードへ遷移するため、既定候補(旧baseRow相当)で確定する

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
        vm.ConfirmOrJoinTarget();  // T-102: 既定候補(旧baseRow相当)で確定する

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
        vm.ConfirmOrJoinTarget();  // T-102: 既定候補(旧baseRow相当)で確定する

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
        vm.ConfirmOrJoinTarget();  // T-102: 既定候補(旧baseRow相当)で確定する

        // 配置行(列0)に既存要素があるため、これをバイパスしないよう左縦分岐は維持される。
        Assert.NotNull(FindConnector(vm, column: 2, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 0, bottomRow: 1));
    }

    [Fact]
    public void PlaceOr_ChainedThreeTierAtSameColumn_KeepsBothLeftConnectorsToAvoidRailShortcut()
    {
        // 隠密レビューCONFIRMED(重大、docs/ecad2-t044-review-onmitsu.md所見1)の回帰テスト。
        // 行0列0=A0・行0列2=A2 → 行1列2=BをOR(基準行0) → 行2列2=CをOR(基準行1、Bと同一列)。
        // 行1(B)は要素としては空(左に何も無い)が、既存の縦コネクタ(列2、行0-1)により母線と直結では
        // ない状態にある。この既存コネクタを見落とすと、Cの左縦分岐(列2、行1-2)が誤って省略され、
        // Cが母線へ直結されてしまう(BとCは本来同一ネットであるべき)。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);    // 行0列0=A0
        vm.SelectedCell = new GridPos(0, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "", isOr: false);    // 行0列2=A2

        vm.SelectedCell = new GridPos(1, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "B", isOr: true);    // 行1列2=B(OR、基準行0)
        vm.ConfirmOrJoinTarget();  // T-102: 既定候補(旧baseRow相当)で確定する

        vm.SelectedCell = new GridPos(2, 2);
        vm.PlaceElementAtSelectedCell("contact-no", "C", isOr: true);    // 行2列2=C(OR、基準行1)
        vm.ConfirmOrJoinTarget();  // T-102: 既定候補(旧baseRow相当)で確定する

        Assert.NotNull(FindConnector(vm, column: 2, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 2, topRow: 1, bottomRow: 2));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 0, bottomRow: 1));
        Assert.NotNull(FindConnector(vm, column: 3, topRow: 1, bottomRow: 2));
    }

    [Fact]
    public void PlaceOr_T044Scenario_SelectingOuterCandidate_JoinsAtOuterNetInsteadOfInnerBlock()
    {
        // T-102(T-044実例の回帰、docs/todo-archive.md:2899-2912原因確定記述+sample/T044-sample.gcadの
        // 実座標で裏付け済み): A(行0列5)/B(行3列5)がOR並列(共有ネット=線番1相当)、無名接点(行0列13)の
        // 右側が線番2相当(便宜上「コイル」役はcontact-noで代用、Kindは既定ContactNOのまま=既存全
        // テストと同じ流儀、本テストの主眼はKind/PartIdでなく合流先候補のネット到達性)。旧baseRow
        // 探索ではCは常にB(候補[0]、線番1側)にしか合流できなかった(T-044実バグ)。合流先候補[1]
        // (行0=無名接点の行)を明示選択すればCは無名接点と並列(線番1↔線番2間)になり、意図した
        // 線番2への到達が実現することを検証する。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 5);
        vm.PlaceElementAtSelectedCell("contact-no", "A", isOr: false);
        vm.SelectedCell = new GridPos(3, 5);
        vm.PlaceElementAtSelectedCell("contact-no", "B", isOr: true);
        vm.ConfirmOrJoinTarget();  // 候補1件(行0)のみ、既定候補で確定(sample実データと同じ{col6,top0,bot3}が生成される)
        vm.SelectedCell = new GridPos(0, 13);
        vm.PlaceElementAtSelectedCell("contact-no", "UNNAMED", isOr: false);   // 無名接点役
        vm.SelectedCell = new GridPos(0, 19);
        vm.PlaceElementAtSelectedCell("contact-no", "COIL", isOr: false);      // コイル役(線番2の存在確保用)

        vm.SelectedCell = new GridPos(6, 15);
        vm.PlaceElementAtSelectedCell("contact-no", "C", isOr: true);   // 合流先確認モードへ遷移(候補[0]=行3・候補[1]=行0)
        vm.MoveOrJoinTargetCandidate(1);   // 候補[1](行0、無名接点の行)へ切替
        vm.ConfirmOrJoinTarget();

        var netlist = NetlistBuilder.Build(vm.CurrentSheet!);
        var unnamedContact = netlist.Components.Single(comp => comp.DeviceName == "UNNAMED");
        var b = netlist.Components.Single(comp => comp.DeviceName == "B");
        var c = netlist.Components.Single(comp => comp.DeviceName == "C");

        // Cは無名接点と同じ左右ネット(線番1↔線番2)を持つ = 意図した合流先(線番2)に正しく到達している。
        Assert.Equal(unnamedContact.NetA, c.NetA);
        Assert.Equal(unnamedContact.NetB, c.NetB);
        // Cの右ネットはB(線番1)の右ネットとは異なる = 旧バグ(常にBの階層[線番1]にしか合流できない)は解消済み。
        Assert.NotEqual(b.NetB, c.NetB);
    }

    [Fact]
    public void PlaceOr_T044Scenario_DefaultCandidate_StillJoinsAtInnerBlock()
    {
        // T-102回帰防止(既存の単純ケースへの非破壊性): 合流先確認モードで何も操作せずEnter確定した
        // 場合(候補[0]=既定、旧baseRow探索と同じ結果)は、従来どおりB(線番1側)へ合流することを確認する。
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 5);
        vm.PlaceElementAtSelectedCell("contact-no", "A", isOr: false);
        vm.SelectedCell = new GridPos(3, 5);
        vm.PlaceElementAtSelectedCell("contact-no", "B", isOr: true);
        vm.ConfirmOrJoinTarget();
        vm.SelectedCell = new GridPos(0, 13);
        vm.PlaceElementAtSelectedCell("contact-no", "UNNAMED", isOr: false);
        vm.SelectedCell = new GridPos(0, 19);
        vm.PlaceElementAtSelectedCell("contact-no", "COIL", isOr: false);

        vm.SelectedCell = new GridPos(6, 15);
        vm.PlaceElementAtSelectedCell("contact-no", "C", isOr: true);
        vm.ConfirmOrJoinTarget();  // 候補切替なし、既定候補[0]=行3(B)のまま確定(旧baseRow挙動と同一)

        var netlist = NetlistBuilder.Build(vm.CurrentSheet!);
        var b = netlist.Components.Single(comp => comp.DeviceName == "B");
        var c = netlist.Components.Single(comp => comp.DeviceName == "C");

        Assert.Equal(b.NetA, c.NetA);
        Assert.Equal(b.NetB, c.NetB);
    }
}
