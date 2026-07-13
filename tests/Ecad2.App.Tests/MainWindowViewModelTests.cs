using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ecad2.App.ViewModels;
using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Tests;

/// <summary>
/// T-034: MainWindowViewModelのDirty追跡(MarkDirty呼び忘れ検出)・HasProject切替の回帰テスト。
/// 明示MarkDirty方式は変更操作の入口ごとに呼び忘れる構造的リスクがあるため(docs/todo.md T-034備考)、
/// 各入口で最低限の検証を行う。
/// </summary>
public class MainWindowViewModelTests : ViewModelTestBase
{
    [Fact]
    public void Constructor_InitialState_HasProjectIsFalseAndNotDirty()
    {
        var vm = CreateViewModel();

        Assert.False(vm.HasProject);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void NewDocument_SetsHasProjectTrueAndNotDirty()
    {
        var vm = CreateViewModel();

        vm.NewDocument();

        Assert.True(vm.HasProject);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void PlaceElementAtSelectedCell_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);

        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void DeleteSelectedElement_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        ResetDirtyViaSave(vm);

        bool deleted = vm.DeleteSelectedElement();

        Assert.True(deleted);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void SelectedElementDeviceName_Set_MarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        ResetDirtyViaSave(vm);

        vm.SelectedElementDeviceName = "X002";

        Assert.True(vm.IsDirty);
    }

    /// <summary>IsDirtyのsetterはprivateのため、公開APIであるSaveToFileの副作用を借りてfalseへ戻す。</summary>
    private static void ResetDirtyViaSave(MainWindowViewModel vm)
    {
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");
        try
        {
            vm.SaveToFile(path);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void SaveToFile_ClearsDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");

        try
        {
            vm.SaveToFile(path);

            Assert.False(vm.IsDirty);
            Assert.Equal(path, vm.CurrentFilePath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_ReplacesDocumentAndClearsDirty()
    {
        var source = CreateViewModel();
        source.NewDocument();
        source.SelectedCell = new GridPos(0, 0);
        source.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);
        string path = Path.Combine(Path.GetTempPath(), $"ecad2-test-{Guid.NewGuid():N}.gcad");
        source.SaveToFile(path);

        try
        {
            var vm = CreateViewModel();
            vm.LoadFromFile(path);

            Assert.True(vm.HasProject);
            Assert.False(vm.IsDirty);
            Assert.Equal(path, vm.CurrentFilePath);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>
    /// T-045増分B(P-022/P-024境界ガード欠如の解消)の回帰テスト。NewDocument()の既定グリッドは
    /// Rows=10・Columns=20。行-1・列-2はSelectedCellとしては選択可能な仕様範囲(殿教示2026-07-07、
    /// docs/proposed.md P-022/P-024)だが、配置経路のみをガードする(殿裁定2026-07-09=下限0)。
    /// </summary>
    [Theory]
    [InlineData(-1, 5, false)]   // 行-1: 仕様上選択可能だが配置は範囲外
    [InlineData(0, 5, true)]     // 行0: 下限、配置可
    [InlineData(9, 5, true)]     // 行Rows-1=9: 上限直下、配置可
    [InlineData(10, 5, false)]   // 行Rows=10: 範囲外
    [InlineData(0, -2, false)]   // 列-2: 仕様上選択可能な下限だが配置は範囲外
    [InlineData(0, 0, true)]     // 列0: 下限、配置可
    [InlineData(0, 19, true)]    // 列Columns-1=19: 上限直下、配置可
    [InlineData(0, 20, false)]   // 列Columns=20: 範囲外
    public void PlaceElementAtSelectedCell_BoundaryRowAndColumn_PlacesOnlyWithinGridRange(int row, int column, bool expectPlaced)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(row, column);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        Assert.Equal(expectPlaced, vm.CurrentSheet!.Elements.Any(el => el.Pos == new GridPos(row, column)));
    }

    /// <summary>
    /// T-071バグ修正(隠密テスト設計 docs/ecad2-t071-bugfix-test-design-onmitsu.md 表1)の回帰テスト。
    /// WidthCells>1(Motor=3セル)配置時の境界外・重複配置の検出漏れを検証する。occupiedColumn>=0の
    /// ケースは先に1セル部品(ContactNO)をその列へ配置してから、目的列cへMotor等を配置する。
    /// </summary>
    [Theory]
    [InlineData(BasicPartTemplates.ContactNOId, 19, -1, true)]   // 1: 1セル部品、上限(既存回帰)
    [InlineData(BasicPartTemplates.ContactNOId, 20, -1, false)]  // 2: 1セル部品、上限+1(既存回帰)
    [InlineData(BasicPartTemplates.MotorId, 0, -1, true)]        // 3: 3セル部品、下限
    [InlineData(BasicPartTemplates.MotorId, 17, -1, true)]       // 4: 3セル部品、[17,18,19]ちょうど収まる
    [InlineData(BasicPartTemplates.MotorId, 18, -1, false)]      // 5: [18,19,20]、20が範囲外
    [InlineData(BasicPartTemplates.MotorId, 19, -1, false)]      // 6: [19,20,21]、2列はみ出す
    [InlineData(BasicPartTemplates.MotorId, 5, 5, false)]        // 7: アンカー自体に既存要素(従来の重複)
    [InlineData(BasicPartTemplates.MotorId, 5, 6, false)]        // 8: +1列目に既存要素(新規重複ケース)
    [InlineData(BasicPartTemplates.MotorId, 5, 7, false)]        // 9: +2列目に既存要素(新規重複ケース)
    [InlineData(BasicPartTemplates.MotorId, 5, -1, true)]        // 10: 周辺すべて空き(正常系対称ケース)
    public void PlaceElementAtSelectedCell_MultiCellWidthBoundaryAndOverlap_PlacesOnlyWhenValid(
        string partId, int column, int occupiedColumn, bool expectPlaced)
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        if (occupiedColumn >= 0)
        {
            vm.SelectedCell = new GridPos(0, occupiedColumn);
            vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "", isOr: false);
        }

        vm.SelectedCell = new GridPos(0, column);
        vm.PlaceElementAtSelectedCell(partId, "X001", isOr: false);

        Assert.Equal(expectPlaced, vm.CurrentSheet!.Elements.Any(el => el.PartId == partId && el.Pos == new GridPos(0, column)));
    }

    /// <summary>
    /// T-045増分C(所見B=TryPlaceElementの境界チェック未追随の解消)の回帰テスト。IsSelectedCellWithinGrid
    /// はValidatePlacementと境界判定ロジックを共有する(IsWithinGridBounds)。境界は
    /// PlaceElementAtSelectedCell_BoundaryRowAndColumnと同じ8ケース。
    /// </summary>
    [Theory]
    [InlineData(-1, 5, false)]
    [InlineData(0, 5, true)]
    [InlineData(9, 5, true)]
    [InlineData(10, 5, false)]
    [InlineData(0, -2, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 19, true)]
    [InlineData(0, 20, false)]
    public void IsSelectedCellWithinGrid_BoundaryRowAndColumn_ReturnsExpected(int row, int column, bool expected)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(row, column);

        Assert.Equal(expected, vm.IsSelectedCellWithinGrid());
    }

    /// <summary>SelectedCell未選択(null)の場合はfalseを返す(TryPlaceElementは占有チェック同様、
    /// null選択を別ガードで先に弾くため到達しない想定だが、単体としての境界値を確認する)。</summary>
    [Fact]
    public void IsSelectedCellWithinGrid_WhenNoCellSelected_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.IsSelectedCellWithinGrid());
    }

    /// <summary>T-045増分B(P-021占有再チェック欠如の解消)の回帰テスト。既に要素があるセルへの
    /// 再配置は無視され、先着の要素・機器名がそのまま残ることを検証する。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WhenCellAlreadyOccupied_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.ContactNOId, "X001", isOr: false);

        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.CoilId, "X002", isOr: false);

        Assert.Single(vm.CurrentSheet!.Elements);
        Assert.Equal("X001", vm.CurrentSheet!.Elements[0].DeviceName);
        Assert.False(vm.Document.Devices.ByName.ContainsKey("X002"));
    }

    /// <summary>端子台(Role=Terminal)はTerminalへ分類される。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithTerminalPart_SetsDeviceClassTerminal()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell(BasicPartTemplates.TerminalId, "TB1", isOr: false);

        Assert.Equal(DeviceClass.Terminal, vm.Document.Devices.ByName["TB1"].Class);
    }

    /// <summary>
    /// T-045増分B修正(隠密レビューCONFIRMED、ecad2-t045-increment-b-review-onmitsu.md=セレクトSW
    /// 誤分類バグ)の回帰テスト。テスト設計書(ecad2-t045-increment-b-fix-test-design-onmitsu.md)の
    /// 同値分割4分類(A/B/C/D)。既存の`PlaceElementAtSelectedCell_With{Contact,SelectSwitch}Part_
    /// SetsDeviceClass{Relay,SelectSwitch}`をケースA/Cとして統合した(設計書6節、侍判断で統合可)。
    /// A/Bは「Idの違いのみ」(固定Id判定の脆さを直接突く対)、C/Dは「IsOrEligibleの違いのみ」
    /// (IsOrEligible単独判定の弁別力を試す対)、B/Dは「Role=ContactNO・IsOrEligible=falseで一致
    /// するが期待結果が異なる」最も厳しい対。
    /// </summary>
    public static IEnumerable<object[]> SelectSwitchClassificationCases()
    {
        // A: セレクトSW・元Id(既存テスト相当)。
        yield return new object[] { BasicPartTemplates.SelectSwitchId, null!, false, DeviceClass.SelectSwitch };

        // B: セレクトSW・再採番Id相当(T-035Explorerコピー後、基本図形フォルダ直下、RED対象)。
        // Role/IsOrEligible/PortsはSelectSwitchと同一、Idのみ異なる。
        // T-061 A-1構造対処(家老裁定): Role=SelectSwitchが新仕様下の正しい表現。Idは意図的に
        // 再採番値のまま据え置く(検証意図=Id固定ではなくデータフィールドで分類できること、は
        // Role値をこちらへ揃えても保たれる)。
        yield return new object[]
        {
            "reassigned-select-switch-guid",
            new PartDefinition
            {
                Id = "reassigned-select-switch-guid", Name = "セレクトSW", Role = PartRole.SelectSwitch,
                IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
            },
            false, // 基本図形フォルダ直下(Category=="")
            DeviceClass.SelectSwitch,
        };

        // C: 純正ContactNO(a接点、既存テスト相当、退行防止)。
        yield return new object[] { BasicPartTemplates.ContactNOId, null!, false, DeviceClass.Relay };

        // D: 自作パーツ(自作フォルダ、Role=ContactNO・IsOrEligible=false、対応案の弁別力を試す。
        // Category="自作"のためCategory==""ゲートで弾かれ、IsOrEligible単独判定だと誤ってSelectSwitch
        // へ分類されるところをこのケースが検出する)。
        yield return new object[]
        {
            "custom-contact-no-guid",
            new PartDefinition
            {
                Id = "custom-contact-no-guid", Name = "自作接点", Role = PartRole.ContactNO,
                IsOrEligible = false, Ports = new() { new PortDef("L", 0, 0), new PortDef("R", 0, 1) },
            },
            true, // 自作フォルダ(Category=="自作")
            DeviceClass.Relay,
        };
    }

    /// <summary>customDefinitionが指定されたケース(B/D)は、PartFolderStoreの一時フォルダへ実際に
    /// .gcadpartファイルを書き出し、store.Enumerate()経由でPartPalette.Entriesへ反映させる
    /// (vm.PartLibrary.ById直接追加ではEntriesに載らずCategory判定を再現できないため、設計書
    /// ecad2-t045-increment-b-fix-test-design-onmitsu.md 5節注記に従い実装方式に追随)。</summary>
    [Theory]
    [MemberData(nameof(SelectSwitchClassificationCases))]
    public void PlaceElementAtSelectedCell_ClassifiesSelectSwitchByDataFieldsNotFixedId(
        string partId, PartDefinition? customDefinition, bool isCustomFolder, DeviceClass expected)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"ecad2-apptest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new PartFolderStore(tempDir);
            if (customDefinition is not null)
            {
                store.EnsureFolders();
                string dir = isCustomFolder ? store.CustomDir : store.RootDir;
                PartLibrarySerializer.SaveOne(customDefinition, Path.Combine(dir, $"{partId}.gcadpart"));
            }

            var vm = new MainWindowViewModel(store, new ImmediateDispatcherService());
            vm.NewDocument();
            vm.SelectedCell = new GridPos(0, 0);

            vm.PlaceElementAtSelectedCell(partId, "X001", isOr: false);

            Assert.Equal(expected, vm.Document.Devices.ByName["X001"].Class);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// T-045補遺2(Stryker棚卸し、docs/ecad2-t046-stryker-t045-close-survey-onmitsu.md=MapToDeviceClass
    /// 生存ミュータント4件対応)。裁可済み案A対応表(handover-next-session.md §2)の全20 ElementKindを
    /// 直接検証する。PlaceElementAtSelectedCell経由(PartResolver.ComponentKind→Role起点)では
    /// ContactNO/ContactNC/Coil/Lamp/Terminal/PushButtonNO/PushButtonNCの7値にしか実際には到達
    /// できない(Timer/Counter/EmergencyStop/SelectSwitch等13値に対応するPartRoleが存在しないため)。
    /// MapToDeviceClassは将来のKind直接設定に備え全20値を網羅する設計のため、経路上到達できない
    /// 残りをリフレクション経由で直接検証する。
    /// </summary>
    [Theory]
    [InlineData(ElementKind.ContactNO, DeviceClass.Relay)]
    [InlineData(ElementKind.ContactNC, DeviceClass.Relay)]
    [InlineData(ElementKind.Coil, DeviceClass.Relay)]
    [InlineData(ElementKind.ContactorMain3P, DeviceClass.Relay)]
    [InlineData(ElementKind.Lamp, DeviceClass.Lamp)]
    [InlineData(ElementKind.PushButtonNO, DeviceClass.PushButton)]
    [InlineData(ElementKind.PushButtonNC, DeviceClass.PushButton)]
    [InlineData(ElementKind.EmergencyStop, DeviceClass.PushButton)]
    [InlineData(ElementKind.SelectSwitch, DeviceClass.SelectSwitch)]
    [InlineData(ElementKind.Terminal, DeviceClass.Terminal)]
    [InlineData(ElementKind.Timer, DeviceClass.Timer)]
    [InlineData(ElementKind.TimerContactNO, DeviceClass.Timer)]
    [InlineData(ElementKind.TimerContactNC, DeviceClass.Timer)]
    [InlineData(ElementKind.TimerInstantContactNO, DeviceClass.Timer)]
    [InlineData(ElementKind.TimerInstantContactNC, DeviceClass.Timer)]
    [InlineData(ElementKind.Counter, DeviceClass.Counter)]
    [InlineData(ElementKind.ThermalOverload, DeviceClass.Other)]
    [InlineData(ElementKind.ThermalOverload3P, DeviceClass.Other)]
    [InlineData(ElementKind.Motor, DeviceClass.Other)]
    [InlineData(ElementKind.Breaker3P, DeviceClass.Other)]
    public void MapToDeviceClass_AllApprovedMappingTableAElementKinds_MatchesExpected(ElementKind kind, DeviceClass expected)
    {
        var method = typeof(MainWindowViewModel).GetMethod(
            "MapToDeviceClass", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (DeviceClass)method!.Invoke(null, new object[] { kind })!;

        Assert.Equal(expected, actual);
    }

    /// <summary>自作パーツRole=NonSimulated(主回路記号等)はComponentKindが呼べない
    /// (CreatesComponent=false)ため、Otherへフォールバックする。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_WithNonSimulatedCustomPart_SetsDeviceClassOther()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.PartLibrary.ById["custom-nonsim"] = new PartDefinition
        {
            Id = "custom-nonsim",
            Name = "テスト非シミュレート",
            Role = PartRole.NonSimulated,
        };
        vm.SelectedCell = new GridPos(0, 0);

        vm.PlaceElementAtSelectedCell("custom-nonsim", "M1", isOr: false);

        Assert.Equal(DeviceClass.Other, vm.Document.Devices.ByName["M1"].Class);
    }

    /// <summary>SelectedElementDeviceNameセッター経由でも同じマッピングが効くことを確認する
    /// (配置時にデバイス名を空欄のままにし、後から命名する経路)。</summary>
    [Fact]
    public void SelectedElementDeviceName_Set_WithNewDeviceName_UsesResolvedDeviceClass()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.PlaceElementAtSelectedCell(BasicPartTemplates.TerminalId, "", isOr: false);

        vm.SelectedElementDeviceName = "TB2";

        Assert.Equal(DeviceClass.Terminal, vm.Document.Devices.ByName["TB2"].Class);
    }

    /// <summary>T-065: ApplyDocumentInfoが8フィールドをDocument.Infoへ反映しMarkDirty()すること。</summary>
    [Fact]
    public void ApplyDocumentInfo_UpdatesFieldsAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        ResetDirtyViaSave(vm);
        var info = new DocumentInfo
        {
            CompanyName = "テスト社",
            Title = "テスト図面",
            DrawingNo = "D-001",
            Customer = "テスト客先",
            Designer = "設計太郎",
            Drafter = "製図次郎",
            Checker = "検図三郎",
            Date = "2026-07-12",
        };

        vm.ApplyDocumentInfo(info);

        Assert.Equal("テスト社", vm.Document.Info.CompanyName);
        Assert.Equal("テスト図面", vm.Document.Info.Title);
        Assert.Equal("D-001", vm.Document.Info.DrawingNo);
        Assert.Equal("テスト客先", vm.Document.Info.Customer);
        Assert.Equal("設計太郎", vm.Document.Info.Designer);
        Assert.Equal("製図次郎", vm.Document.Info.Drafter);
        Assert.Equal("検図三郎", vm.Document.Info.Checker);
        Assert.Equal("2026-07-12", vm.Document.Info.Date);
        Assert.True(vm.IsDirty);
    }

    /// <summary>T-065: Revisions(改定履歴)はApplyDocumentInfoの編集対象外(殿裁定2026-07-12)で
    /// あり、既存のリスト参照・内容とも変化しないこと。</summary>
    [Fact]
    public void ApplyDocumentInfo_DoesNotChangeRevisions()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var existingRevisions = vm.Document.Info.Revisions;
        existingRevisions.Add(new RevisionEntry { Rev = "A", Date = "2026-01-01", Description = "初版", By = "太郎" });

        vm.ApplyDocumentInfo(new DocumentInfo { CompanyName = "新社名" });

        Assert.Same(existingRevisions, vm.Document.Info.Revisions);
        Assert.Single(vm.Document.Info.Revisions);
        Assert.Equal("A", vm.Document.Info.Revisions[0].Rev);
    }

    /// <summary>T-079(P-058): PlaceElementAtSelectedCellはSelectedCell自体を変更しないため
    /// SelectedCellのsetter経由の通知連鎖が起きない。要素追加後にSelectedElement系4プロパティの
    /// 変更通知が発火することを確認する(修正前は発火せずRED、侍実測で確認済み)。</summary>
    [Fact]
    public void PlaceElementAtSelectedCell_RaisesSelectedElementDeviceNameChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.PlaceElementAtSelectedCell("contact-no", "X001", isOr: false);

        Assert.Contains(nameof(vm.SelectedElementDeviceName), raised);
        Assert.Contains(nameof(vm.HasSelectedElement), raised);
    }

    /// <summary>T-080: SetRungCommentが新規コメントを追加しMarkDirty()すること。</summary>
    [Fact]
    public void SetRungComment_NewText_AddsEntryAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        ResetDirtyViaSave(vm);

        vm.SetRungComment(3, "テストコメント");

        Assert.Equal("テストコメント", vm.GetRungComment(3));
        Assert.True(vm.IsDirty);
    }

    /// <summary>T-080: 前後で空白をTrimして保存すること。</summary>
    [Fact]
    public void SetRungComment_TrimsWhitespace()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.SetRungComment(0, "  余白付き  ");

        Assert.Equal("余白付き", vm.GetRungComment(0));
    }

    /// <summary>T-080殿裁定: 空文字列で確定した場合、RungCommentエントリを残さない(削除扱い)。
    /// GuiEcadの空エントリ残留癖は踏襲しない。</summary>
    [Fact]
    public void SetRungComment_EmptyText_RemovesEntry()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SetRungComment(2, "いったん記入");

        vm.SetRungComment(2, "");

        Assert.Equal("", vm.GetRungComment(2));
        Assert.Empty(vm.CurrentSheet!.RungComments);
    }

    /// <summary>T-080殿裁定: 元々空の行を空のまま確定(取消相当)した場合もエントリを作らない。</summary>
    [Fact]
    public void SetRungComment_EmptyOnEmptyRow_DoesNotCreateEntryOrMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        ResetDirtyViaSave(vm);

        vm.SetRungComment(5, "");

        Assert.Empty(vm.CurrentSheet!.RungComments);
        Assert.False(vm.IsDirty);
    }

    /// <summary>T-080: 既存コメントを同じテキストで確定してもMarkDirty()しない(同値ガード規約、
    /// T-065/T-066往復の教訓)。</summary>
    [Fact]
    public void SetRungComment_SameText_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SetRungComment(1, "既存コメント");
        ResetDirtyViaSave(vm);

        vm.SetRungComment(1, "既存コメント");

        Assert.False(vm.IsDirty);
    }

    /// <summary>T-080: 既存コメントのテキストを変更できること。</summary>
    [Fact]
    public void SetRungComment_ExistingRow_UpdatesText()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SetRungComment(4, "旧テキスト");

        vm.SetRungComment(4, "新テキスト");

        Assert.Equal("新テキスト", vm.GetRungComment(4));
        Assert.Single(vm.CurrentSheet!.RungComments);
    }

    // --- T-069往復2周目(隠密レビュー指摘・修正1): HitTestElementのCellWidth>1境界値 ---

    /// <summary>
    /// 隠密レビュー指摘・要修正1のRED先行証明用回帰テスト。CellWidth>1(Motor等)の要素は左上
    /// アンカーセル以外の占有列も右クリックメニューのヒット対象に含める必要がある(IsOccupiedと
    /// 同じ区間交差判定、T-071バグ修正の教訓)。旧実装は単純Pos一致のみだったため、アンカー以外の
    /// セルでnullを返していた。
    /// </summary>
    [Fact]
    public void HitTestElement_HitsNonAnchorCell_WhenCellWidthGreaterThanOne()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var motor = new ElementInstance
        {
            Kind = ElementKind.Motor,
            Pos = new GridPos(0, 1),
            CellWidth = 3,
            DeviceName = "M1",
        };
        vm.CurrentSheet!.Elements.Add(motor);

        // Motor(左上アンカー列1、CellWidth=3)は列1-3を占有。アンカー以外の列2でもヒットするはず。
        var hit = vm.HitTestElement(new GridPos(0, 2));

        Assert.Same(motor, hit);
    }

    [Fact]
    public void HitTestElement_HitsAnchorCell()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var motor = new ElementInstance { Kind = ElementKind.Motor, Pos = new GridPos(0, 1), CellWidth = 3 };
        vm.CurrentSheet!.Elements.Add(motor);

        Assert.Same(motor, vm.HitTestElement(new GridPos(0, 1)));
    }

    [Fact]
    public void HitTestElement_ReturnsNull_OutsideOccupiedRange()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var motor = new ElementInstance { Kind = ElementKind.Motor, Pos = new GridPos(0, 1), CellWidth = 3 };
        vm.CurrentSheet!.Elements.Add(motor);

        Assert.Null(vm.HitTestElement(new GridPos(0, 4)));
        Assert.Null(vm.HitTestElement(new GridPos(0, 0)));
    }

    [Fact]
    public void HitTestElement_ReturnsNull_DifferentRow()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var motor = new ElementInstance { Kind = ElementKind.Motor, Pos = new GridPos(0, 1), CellWidth = 3 };
        vm.CurrentSheet!.Elements.Add(motor);

        Assert.Null(vm.HitTestElement(new GridPos(1, 2)));
    }

    // --- T-069往復3周目(隠密テスト設計書、docs/ecad2-t069-fix2-test-design-onmitsu.md):
    //     修正1「表示と実行の整合原則」——CellWidth>1要素の占有範囲内のどのセルで右クリックしても、
    //     削除・機器名取得/設定が同一要素に正しく作用すること。右クリックハンドラの正規化ロジック
    //     (HitTestElementで検出した要素のアンカー位置=hitElement.PosをSelectedCellへ設定する)を
    //     ここでも模擬し、実行側(SelectedElementDeviceName/DeleteSelectedElement)の結果を検証する。

    [Theory]
    [InlineData(2, 0)] // CellWidth=2, アンカー
    [InlineData(2, 1)] // CellWidth=2, 非アンカー(上限)
    [InlineData(3, 0)] // CellWidth=3, アンカー
    [InlineData(3, 1)] // CellWidth=3, 中間
    [InlineData(3, 2)] // CellWidth=3, 非アンカー(上限)
    public void RightClickElementSelection_OnAnyOccupiedCell_ResolvesDeviceNameToSameElement(int cellWidth, int columnOffset)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var element = new ElementInstance { Kind = ElementKind.Motor, Pos = new GridPos(0, 1), CellWidth = cellWidth, DeviceName = "M1" };
        vm.CurrentSheet!.Elements.Add(element);
        var clickedPos = new GridPos(0, 1 + columnOffset);

        // 右クリックハンドラの正規化ロジック(MainWindow.xaml.cs)を模擬: HitTestElement→アンカー位置。
        var hit = vm.HitTestElement(clickedPos);
        Assert.NotNull(hit);
        vm.SelectedCell = hit!.Pos;

        Assert.Equal("M1", vm.SelectedElementDeviceName);

        vm.SelectedElementDeviceName = "M2";
        Assert.Equal("M2", element.DeviceName);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    public void RightClickElementSelection_OnAnyOccupiedCell_DeletesCorrectElement(int cellWidth, int columnOffset)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var element = new ElementInstance { Kind = ElementKind.Motor, Pos = new GridPos(0, 1), CellWidth = cellWidth, DeviceName = "M1" };
        vm.CurrentSheet!.Elements.Add(element);
        var clickedPos = new GridPos(0, 1 + columnOffset);

        var hit = vm.HitTestElement(clickedPos);
        vm.SelectedCell = hit!.Pos;

        bool deleted = vm.DeleteSelectedElement();

        Assert.True(deleted);
        Assert.Empty(vm.CurrentSheet!.Elements);
    }

    // --- T-069往復3周目修正2: HasAnyDraft(記入中ドラフト保持判定、右クリックガードの絞り込み) ---

    [Fact]
    public void HasAnyDraft_InitiallyFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterBeginConnectorDraft_True()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);

        vm.BeginConnectorDraft();

        Assert.True(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterCancelConnectorDraft_False()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(0, 0);
        vm.BeginConnectorDraft();

        vm.CancelConnectorDraft();

        Assert.False(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterBeginFreeLineDraft_True()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;

        vm.BeginFreeLineDraft(horizontal: true, startXMm: 10, startYMm: 10, stepMm: 5);

        Assert.True(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterCancelFreeLineDraft_False()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.MainCircuit = true;
        vm.BeginFreeLineDraft(horizontal: true, startXMm: 10, startYMm: 10, stepMm: 5);

        vm.CancelFreeLineDraft();

        Assert.False(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterBeginImageInsertDraft_True()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 20, heightMm: 10, xMm: 0, yMm: 0);

        Assert.True(vm.HasAnyDraft);
    }

    [Fact]
    public void HasAnyDraft_AfterCancelImageInsertDraft_False()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 20, heightMm: 10, xMm: 0, yMm: 0);

        vm.CancelImageInsertDraft();

        Assert.False(vm.HasAnyDraft);
    }

    /// <summary>往復2周目で誤ってブロックされていた核心のケース(隠密テスト設計書3.2節)。
    /// PlaceElement(部品配置準備中、T-021分岐Aの連続配置)は記入中ドラフトを一切持たない
    /// 静的な状態であり、右クリックメニュー(削除・行操作等)は引き続き使えるべき。</summary>
    [Fact]
    public void HasAnyDraft_WhenPlaceElementModeWithoutDraft_False()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.Tool = new ToolState(ToolMode.PlaceElement, PartId: "contact-no");

        Assert.False(vm.HasAnyDraft);
    }

    // --- T-069往復4周目(隠密テスト設計書、docs/ecad2-t069-fix4-test-design-onmitsu.md):
    //     検証観点1「ツールバーボタンのドラフトクリア漏れ」——ActivateBuiltinTool/
    //     ActivateOpenPartSelection(MainWindow.xaml.cs)の本体ロジック(CancelResidualDraftForToolSwitch
    //     呼び出し+Tool代入)をここで模擬し、記入中ドラフト3種いずれからでもクリアされることを検証する。

    /// <summary>状態遷移表・無効域(バグ域)3行: PlaceConnector/PlaceLine/PlaceImageいずれの記入中でも、
    /// ツールバーボタン相当の操作でドラフトが確実にクリアされ、実体(Connectors/FreeLines/Images)も
    /// 生成されずに終わること。T-064で画像挿入ドラフトだけ横展開漏れが起きた前例と同型の見落としを
    /// 防ぐため3種とも対称に確認する(隠密テスト設計書)。</summary>
    [Theory]
    [InlineData("Connector")]
    [InlineData("FreeLine")]
    [InlineData("Image")]
    public void ToolbarButtonEquivalent_ClearsResidualDraft_BeforeSwitchingMode(string draftKind)
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 3);

        switch (draftKind)
        {
            case "Connector":
                vm.BeginConnectorDraft();
                break;
            case "FreeLine":
                vm.CurrentSheet!.MainCircuit = true;
                vm.BeginFreeLineDraft(horizontal: true, startXMm: 10, startYMm: 10, stepMm: 9.0);
                break;
            case "Image":
                vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 20, heightMm: 10, xMm: 5, yMm: 5);
                break;
        }
        Assert.True(vm.HasAnyDraft);

        // ツールバーボタン相当の操作(ActivateBuiltinToolの本体ロジック)。
        vm.CancelResidualDraftForToolSwitch();
        vm.Tool = new ToolState(ToolMode.PlaceElement, PartId: "contact-no");

        Assert.False(vm.HasAnyDraft);
        Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
        Assert.Null(vm.ConnectorDraftPreview);
        Assert.Null(vm.FreeLineDraftPreview);
        Assert.Null(vm.ImageInsertDraftPreview);
        Assert.Empty(vm.CurrentSheet!.Connectors);
        Assert.Empty(vm.CurrentSheet!.FreeLines);
        Assert.Empty(vm.CurrentSheet!.Images);
    }

    /// <summary>状態遷移表・有効域(対照ケース): ドラフトを持たない状態からの遷移は従来どおり
    /// 影響を受けないこと(回帰確認)。</summary>
    [Fact]
    public void ToolbarButtonEquivalent_WithoutDraft_SwitchesModeNormally()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.CancelResidualDraftForToolSwitch();
        vm.Tool = new ToolState(ToolMode.PlaceElement, PartId: "contact-no");

        Assert.False(vm.HasAnyDraft);
        Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
    }

    /// <summary>状態遷移表・最終行(ペア構成の対称性): ActivateOpenPartSelection側の入口でも同じく
    /// ドラフトがクリアされること(隠密テスト設計書「少なくとも1組は両方の入口で確認する」)。</summary>
    [Fact]
    public void OpenPartSelectionEquivalent_ClearsResidualConnectorDraft_BeforeSwitchingMode()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.SelectedCell = new GridPos(2, 3);
        vm.BeginConnectorDraft();
        Assert.True(vm.HasAnyDraft);

        // ツールバーボタン相当の操作(ActivateOpenPartSelectionの本体ロジック)。
        vm.CancelResidualDraftForToolSwitch();
        vm.Tool = new ToolState(ToolMode.PlaceElement);

        Assert.False(vm.HasAnyDraft);
        Assert.Equal(ToolMode.PlaceElement, vm.Tool.Mode);
        Assert.Empty(vm.CurrentSheet!.Connectors);
    }
}
