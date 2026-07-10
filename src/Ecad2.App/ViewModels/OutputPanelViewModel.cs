using System.Collections.ObjectModel;
using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Model;
using Ecad2.Simulation;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 下部出力パネル用の ViewModel(T-018)。MainWindowViewModel の子プロパティとして持たせる
/// (design-brief 3節#1: God Class化の再発防止)。DesignRuleCheck(T-007移植済み)の実行結果を
/// 構造化DataGridへバインディングする(殿裁定: 構造化DataGrid＋重大度色分け、隠密調査
/// docs/ecad2-preimplementation-survey-onmitsu.mdのT-018節参照)。
/// </summary>
public sealed class OutputPanelViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;

    public ObservableCollection<Diagnostic> Diagnostics { get; } = new();

    private Diagnostic? _selectedDiagnostic;

    /// <summary>
    /// 選択中の診断。GuiEcadのCenterViewOnRow相当(選択変更で該当箇所へジャンプ)をecad2の
    /// SelectedCellハイライト機構(T-017/T-027)へ移植する形で実装する。
    /// </summary>
    public Diagnostic? SelectedDiagnostic
    {
        get => _selectedDiagnostic;
        set
        {
            if (!SetProperty(ref _selectedDiagnostic, value)) return;
            if (value is { Locations.Count: > 0 } diagnostic)
                JumpTo(diagnostic.Locations[0], diagnostic.DeviceName);
        }
    }

    public ICommand RunDrcCommand { get; }

    public OutputPanelViewModel(MainWindowViewModel owner)
    {
        _owner = owner;
        RunDrcCommand = new RelayCommand(RunDrc);
    }

    /// <summary>
    /// 診断行クリックから直接呼ぶジャンプ実行(T-018忍者実機検証で発見)。DataGridは既に選択中の
    /// 行を再クリックしても選択状態が変化しないと判定しSelectedItemバインディングが更新されない
    /// (T-016のPartSelectionItem_Clickedと同種、WPFの既知の仕様)。SelectedDiagnosticのsetterに
    /// 頼らず、行のPreviewMouseLeftButtonDownから確実にジャンプさせるための公開メソッド。
    /// </summary>
    public void JumpToDiagnostic(Diagnostic diagnostic)
    {
        if (diagnostic.Locations.Count == 0) return;
        JumpTo(diagnostic.Locations[0], diagnostic.DeviceName);
    }

    /// <summary>DRC結果をクリアする(T-019: 新規/開くでDocumentを差し替えた際、旧文書の結果が
    /// 出力パネルに残留し誤ジャンプ・沈黙する不整合を防ぐ、隠密レビュー指摘)。</summary>
    public void ClearResults()
    {
        Diagnostics.Clear();
        SelectedDiagnostic = null;
    }

    private void RunDrc()
    {
        var results = new List<Diagnostic>();
        results.AddRange(DesignRuleCheck.CheckCrossReference(_owner.Document, _owner.PartLibrary));
        results.AddRange(DesignRuleCheck.CheckDeviceTypeConsistency(_owner.Document, _owner.PartLibrary));
        results.AddRange(DesignRuleCheck.CheckUnresolvedPartId(_owner.Document, _owner.PartLibrary));

        foreach (var sheet in _owner.Document.Sheets)
        {
            var net = NetlistBuilder.Build(sheet, _owner.PartLibrary);
            results.AddRange(DesignRuleCheck.CheckVerticalCrossings(sheet, net));
            results.AddRange(DesignRuleCheck.CheckLoadReachability(sheet, net));
            results.AddRange(DesignRuleCheck.CheckSeriesCoils(sheet, net));
        }

        Diagnostics.Clear();
        foreach (var diagnostic in results) Diagnostics.Add(diagnostic);
    }

    // 該当箇所(ページ-行)のシート・セルへ選択を移す。同じ行に選択中診断のDeviceNameと一致する
    // 要素があればそれを、無ければ行内の先頭要素、それも無ければ列0を選択する(近似ジャンプ)。
    private void JumpTo(CircuitRef location, string deviceName)
    {
        int sheetIndex = _owner.Document.Sheets.FindIndex(s => s.PageNumber == location.PageNumber);
        if (sheetIndex < 0) return;
        _owner.CurrentSheetIndex = sheetIndex;

        var sheet = _owner.Document.Sheets[sheetIndex];
        int row = location.CircuitNumber - 1;

        var element = (string.IsNullOrEmpty(deviceName) ? null : sheet.Elements.FirstOrDefault(e =>
                e.Pos.Row == row && string.Equals(e.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)))
            ?? sheet.Elements.FirstOrDefault(e => e.Pos.Row == row);

        _owner.SelectedCell = element?.Pos ?? new GridPos(row, 0);
    }
}
