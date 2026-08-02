using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-132増分4: <c>UpdateSheetSettingsCommand</c> へ加えた列数・電源ラベルの繋ぎ込み。
/// <para>
/// <b>【増分2の述語をここで初めて呼ぶ】</b> 増分2で <c>IsColumnOccupied</c> を単体で測ったが、
/// <b>述語の単体テストだけでは「ガードを実装したのに一度も呼ばれておらぬ」状態を素通しする</b>
/// （T-125増分αで実証済み、<c>samurai.md</c>【MUST】）。本テスト群が呼び出し側を測る役を負う。
/// 同じ型の穴は本日T-144でも実機まで露見せず起きている（<c>Notify()</c> の欠落）。
/// </para>
/// <para>
/// <b>既存の <c>SheetSettingsCommandTests</c>（行数・母線名）とは別ファイルに置いた</b>
/// ——どちらが何を守っているかを混ぜないため。
/// </para>
/// </summary>
public class SheetSettingsColumnsCommandTests : ViewModelTestBase
{
    /// <summary>列数20・行数10のシートを用意する。<b>既定値に頼らず明示する</b>
    /// （行側テストが <c>Grid.Rows = 10</c> を明示するのと同じ作法。
    /// UI経由の新規シートと Core の既定値が食い違う件＝P-162 があるため、なおのこと明示が要る）。</summary>
    private MainWindowViewModel NewVm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.CurrentSheet!.Grid.Rows = 10;
        vm.CurrentSheet!.Grid.Columns = 20;
        return vm;
    }

    /// <summary>行数・母線名は現状維持で、列数と電源ラベルだけを動かす。</summary>
    private static MainWindowViewModel.SheetSettings Settings(MainWindowViewModel vm, int columns, string? power = null)
        => new(vm.CurrentSheet!.Grid.Rows, columns, vm.CurrentSheet!.Bus.LeftName, vm.CurrentSheet!.Bus.RightName, power);

    // ===== 観点A: 反映 =====

    [Fact]
    public void Execute_列数を反映する()
    {
        var vm = NewVm();

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 15));

        Assert.Equal(15, vm.CurrentSheet!.Grid.Columns);
    }

    [Fact]
    public void Execute_電源ラベルを反映する()
    {
        var vm = NewVm();

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 20, "AC200V"));

        Assert.Equal("AC200V", vm.CurrentSheet!.Bus.PowerLabel);
    }

    /// <summary>電源ラベルは <c>null</c>（未設定）へ戻せること
    /// ——ダイアログが空欄を <c>null</c> へ落とす以上、受け側もそれを保たねば「一度付けたら消せぬ」になる。</summary>
    [Fact]
    public void Execute_電源ラベルをnullへ戻せる()
    {
        var vm = NewVm();
        vm.CurrentSheet!.Bus.PowerLabel = "AC200V";

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 20, null));

        Assert.Null(vm.CurrentSheet!.Bus.PowerLabel);
    }

    [Fact]
    public void Execute_列数の変更でMarkDirtyされる()
    {
        var vm = NewVm();
        // NewVm はモデルを直に触るだけゆえ、この時点では未変更のまま（前提を明示して測る）。
        Assert.False(vm.IsDirty);

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 15));

        Assert.True(vm.IsDirty);
    }

    // ===== 観点B: 範囲検証（ダイアログをすり抜けた場合の安全弁） =====

    [Theory]
    [InlineData(2)]
    [InlineData(40)]
    public void Execute_境界値の列数は適用される(int columns)
    {
        var vm = NewVm();

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, columns));

        Assert.Equal(columns, vm.CurrentSheet!.Grid.Columns);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(41)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Execute_範囲外の列数は拒否され列数が変わらない(int columns)
    {
        var vm = NewVm();

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, columns));

        Assert.Equal(20, vm.CurrentSheet!.Grid.Columns);
    }

    /// <summary>列数が範囲外なら、行数・母線名・電源ラベルも含めて一切変更しない
    /// （行側の <c>Execute_OutOfRange_DoesNotChangeBusNames</c> と同型のアトミック拒否）。</summary>
    [Fact]
    public void Execute_列数が範囲外なら他の項目も変更しない()
    {
        var vm = NewVm();
        vm.CurrentSheet!.Bus.LeftName = "元L";
        vm.CurrentSheet!.Bus.PowerLabel = "元電源";

        vm.UpdateSheetSettingsCommand.Execute(
            new MainWindowViewModel.SheetSettings(5, 41, "新L", "新R", "新電源"));

        Assert.Equal(10, vm.CurrentSheet!.Grid.Rows);
        Assert.Equal("元L", vm.CurrentSheet!.Bus.LeftName);
        Assert.Equal("元電源", vm.CurrentSheet!.Bus.PowerLabel);
    }

    /// <summary>列数の範囲は <c>GridSpec</c> の定数に従うこと（App 層へ 2／40 を直書きしていない証）。</summary>
    [Fact]
    public void Execute_列数の範囲はGridSpecの定数に従う()
    {
        var vm = NewVm();

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, GridSpec.MaxColumns));
        Assert.Equal(GridSpec.MaxColumns, vm.CurrentSheet!.Grid.Columns);

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, GridSpec.MaxColumns + 1));
        Assert.Equal(GridSpec.MaxColumns, vm.CurrentSheet!.Grid.Columns);
    }

    // ===== 観点C: 縮小時の占有拒否（増分2の述語がここで初めて呼ばれる） =====

    /// <summary>縮小で消える列（新Columns〜旧Columns-1）のどこに要素があっても拒否する。
    /// 先頭・中間・末尾の三点を測るのは行側と同型（一箇所しか見ていない実装を捕らえる）。</summary>
    [Theory]
    [InlineData(10)]  // 縮小範囲の先頭列（新Columns）
    [InlineData(14)]  // 縮小範囲の中間列
    [InlineData(19)]  // 縮小範囲の末尾列（旧Columns-1）
    public void Execute_縮小範囲に要素があれば拒否する(int elementColumn)
    {
        var vm = NewVm();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, elementColumn) });

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(20, vm.CurrentSheet!.Grid.Columns);
    }

    /// <summary>
    /// 4種すべてが拒否理由になること——<b>増分2で述語が見ると定めた種別が、実際に呼び出し側へ効いているか</b>。
    /// 述語の単体テストは「述語が true を返す」までしか示さぬゆえ、ここで通しで確かめる。
    /// </summary>
    [Theory]
    [InlineData("ElementInstance")]
    [InlineData("VerticalConnector")]
    [InlineData("WireBreak")]
    [InlineData("GroupFrame")]
    public void Execute_四種いずれの要素でも拒否する(string elementType)
    {
        var vm = NewVm();
        var sheet = vm.CurrentSheet!;
        switch (elementType)
        {
            case "ElementInstance":
                sheet.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 14) });
                break;
            case "VerticalConnector":
                sheet.Connectors.Add(new VerticalConnector { Column = 14.5, TopRow = 1, BottomRow = 2 });
                break;
            case "WireBreak":
                sheet.WireBreaks.Add(new WireBreak { Boundary = 14.5, Row = 1 });
                break;
            case "GroupFrame":
                sheet.Frames.Add(new GroupFrame { Label = "枠", TopLeft = new GridPos(1, 14), Width = 1, Height = 1 });
                break;
        }

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(20, sheet.Grid.Columns);
    }

    /// <summary>対照: 縮小後も残る列にある要素は拒否理由にならない。</summary>
    [Fact]
    public void Execute_縮小範囲の外にある要素は拒否理由にならない()
    {
        var vm = NewVm();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 3) });

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(10, vm.CurrentSheet!.Grid.Columns);
    }

    /// <summary>
    /// <b>【述語の定義域と、呼び出し側の責務の対・その2】</b>
    /// 述語 <c>IsColumnOccupied</c> は範囲外の列でも <c>true</c> を返しうる（<c>ColumnOccupiedTests</c> で固定）。
    /// <b>呼び出し側が回すのは「縮小で消える列」だけである</b>ことを、ここで対にして固定する。
    /// 分断マークを境界 <c>0</c> に置くと述語は列 <c>-1</c> でも <c>true</c> を返すが、
    /// 縮小で消えるのは列10〜19ゆえ拒否されない。<b>呼び出し側が範囲を限っている証。</b>
    /// （隠密の指摘＋家老の裁可2026-08-02＝「述語側だけ定義域を凍らせず、呼び出し側と対で測る」）
    /// </summary>
    [Fact]
    public void Execute_負の列に掛かる要素があっても縮小できる()
    {
        var vm = NewVm();
        vm.CurrentSheet!.WireBreaks.Add(new WireBreak { Boundary = 0, Row = 1 });

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(10, vm.CurrentSheet!.Grid.Columns);
    }

    /// <summary>拒否されたときは列数以外も一切変えない（アトミック拒否）。</summary>
    [Fact]
    public void Execute_占有で拒否されたら他の項目も変更しない()
    {
        var vm = NewVm();
        vm.CurrentSheet!.Bus.PowerLabel = "元電源";
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, 14) });

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10, "新電源"));

        Assert.Equal(20, vm.CurrentSheet!.Grid.Columns);
        Assert.Equal("元電源", vm.CurrentSheet!.Bus.PowerLabel);
    }

    // ===== 観点D: 拒否メッセージ =====

    /// <summary>
    /// 拒否メッセージは実際に占有された列を名指しすること。
    /// <para>
    /// <b>【列番号に +1 しないことを固定する】</b><c>SelectedCellDisplay</c> は
    /// <c>"行{Row+1}/列{Column}"</c> と<b>行のみ1始まり</b>で表示する（列は負の値も取りうる仕様のため）。
    /// 行側の文言に倣って <c>+1</c> すれば、使い手は表示上の列番号と食い違う場所を見に行く
    /// ——T-055増分2往復2周目で正した「誤誘導」を列で再現することになる。
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(14)]
    [InlineData(19)]
    public void Execute_拒否メッセージは実際の列を名指しする(int elementColumn)
    {
        var vm = NewVm();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(1, elementColumn) });

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal($"列{elementColumn}に要素があるため削除できません", vm.StatusMessage);
    }

    /// <summary>行と列の双方が占有されているときは、行の拒否が先に出る（検査順の固定）。
    /// <b>順序そのものに意味はないが、定めておかねば使い手の見る先が実装のたびに揺れる。</b></summary>
    [Fact]
    public void Execute_行と列が共に占有なら行の拒否が先に出る()
    {
        var vm = NewVm();
        vm.CurrentSheet!.Elements.Add(new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(7, 14) });

        vm.UpdateSheetSettingsCommand.Execute(
            new MainWindowViewModel.SheetSettings(5, 10, "N24", "P24", null));

        Assert.Equal("行8に要素があるため削除できません", vm.StatusMessage);
    }

    [Fact]
    public void Execute_成功時はStatusMessageが消える()
    {
        var vm = NewVm();
        vm.StatusMessage = "列14に要素があるため削除できません";

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 15));

        Assert.Equal("", vm.StatusMessage);
    }

    // ===== 観点E: SelectedCell の列クランプ =====

    /// <summary>列数の縮小で選択セルが範囲外になったら、選択解除ではなく新しい末尾列へクランプする
    /// （行側 <c>FinishRowCountChange</c> と同じ作法＝殿裁定）。</summary>
    [Fact]
    public void Execute_選択セルの列が新しい列数を超えていればクランプする()
    {
        var vm = NewVm();
        vm.SelectedCell = new GridPos(3, 19);

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(new GridPos(3, 9), vm.SelectedCell);
    }

    /// <summary>範囲内の選択セルは動かさない（過剰なクランプへの網）。</summary>
    [Fact]
    public void Execute_範囲内の選択セルは動かさない()
    {
        var vm = NewVm();
        vm.SelectedCell = new GridPos(3, 5);

        vm.UpdateSheetSettingsCommand.Execute(Settings(vm, 10));

        Assert.Equal(new GridPos(3, 5), vm.SelectedCell);
    }

    /// <summary>行と列が共に範囲外なら両方クランプされること
    /// ——<b>列のクランプと行のクランプが別の場所に書かれている</b>ため、片方だけ効く形になっていないかを測る。</summary>
    [Fact]
    public void Execute_行と列が共に範囲外なら両方クランプする()
    {
        var vm = NewVm();
        vm.SelectedCell = new GridPos(9, 19);

        vm.UpdateSheetSettingsCommand.Execute(
            new MainWindowViewModel.SheetSettings(5, 10, "N24", "P24", null));

        Assert.Equal(new GridPos(4, 9), vm.SelectedCell);
    }
}
