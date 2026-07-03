using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// MainWindow のルート ViewModel。GuiEcad の MainPage（複数partialファイル合計約1500行超、
/// 実質単一クラスへの責務集中＝God Class化）の反省（design-brief 3節#1）を踏まえ、
/// 責務ごとに ViewModel を分割していく前提の最小骨格として開始する。段階4以降でキャンバス操作・
/// 部品パレット等の機能を専用 ViewModel（子プロパティ）として追加していく方針とし、
/// このクラス自体を肥大化させない。
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private ToolState _tool = ToolState.SelectDefault;

    /// <summary>
    /// 現在の配置ツール状態（単一の真実源）。ToolState は record struct のため、
    /// SetProperty の構造的等価性チェックだと「同じ図形を再選択」等で値が変わらないケースに
    /// PropertyChanged が発火せずUIが更新されないバグになる（忍者実機確認T-016で発見）。
    /// このプロパティは常に通知する。
    /// </summary>
    public ToolState Tool
    {
        get => _tool;
        set
        {
            _tool = value;
            OnPropertyChanged();
        }
    }

    private double _canvasScale = 1.0;

    /// <summary>キャンバスの表示倍率（Ctrl+マウスホイールで変更）。0.25〜4.0の範囲にクランプする。</summary>
    public double CanvasScale
    {
        get => _canvasScale;
        set => SetProperty(ref _canvasScale, Math.Clamp(value, 0.25, 4.0));
    }

    /// <summary>
    /// 開いているドキュメント全体（複数シートを保持）。T-026時点ではダミーデータ(3シート)を
    /// 起動時に設定する。GcadSerializer.Load によるドキュメント読込への置き換えは将来タスク(T-019)。
    /// </summary>
    public LadderDocument Document { get; } = CreateDummyDocument();

    private int _currentSheetIndex;

    /// <summary>現在表示中のシートのインデックス（Document.Sheets への添字）。左パレットのシート
    /// ナビゲーション(T-026)からの選択で変更される。</summary>
    public int CurrentSheetIndex
    {
        get => _currentSheetIndex;
        set
        {
            if (SetProperty(ref _currentSheetIndex, value))
                OnPropertyChanged(nameof(CurrentSheet));
        }
    }

    /// <summary>現在表示中のシート。Document.Sheets[CurrentSheetIndex] の読み取り専用ビュー。</summary>
    public Sheet CurrentSheet => Document.Sheets[CurrentSheetIndex];

    private GridPos? _selectedCell;

    /// <summary>
    /// 現在選択中のセル位置(T-026段階4新配置フロー: クリックでセル選択→キー/ボタンで設置)。
    /// 視覚的なハイライト表示は別タスク(T-027)。null=未選択。
    /// </summary>
    public GridPos? SelectedCell
    {
        get => _selectedCell;
        set
        {
            if (SetProperty(ref _selectedCell, value))
                OnPropertyChanged(nameof(SelectedCellDisplay));
        }
    }

    /// <summary>SelectedCellのステータスバー表示用文字列。</summary>
    public string SelectedCellDisplay => SelectedCell is { } pos ? $"行{pos.Row + 1}/列{pos.Column}" : "未選択";

    private string _statusMessage = "";

    /// <summary>ステータスバーへの一時的な案内メッセージ(例: セル未選択時の配置操作案内)。</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>左パレット（シートナビゲーション）の子ViewModel。</summary>
    public SheetNavigationViewModel SheetNavigation { get; }

    /// <summary>部品選択の子ViewModel（自作パーツ含む）。T-026段階4で右パネルへ配置予定。</summary>
    public PartPaletteViewModel PartPalette { get; }

    /// <summary>右パネル上段（機器表）の子ViewModel。</summary>
    public DeviceTableViewModel DeviceTable { get; }

    /// <summary>
    /// 現在使用可能な自作パーツライブラリ。PartPalette.Entries（PartFolderStoreの列挙結果、
    /// 基本図形もPartIdを持つ.gcadpartとして含む）から構築する。DiagramRenderer.Render /
    /// LadderCanvas.Draw に渡し、要素配置時（T-016）の PartResolver 解決にも使う。
    /// </summary>
    public PartLibrary PartLibrary { get; }

    /// <summary>
    /// ツールバー(T-026段階3)から組込み基本図形を配置ツールとして選択するコマンド。
    /// CommandParameterに図形名(PartFolderStoreの基本図形カテゴリ内でのDefinition.Name。
    /// 例:"a接点")を渡す。既存のPartPalette.SelectCommand(PartFolderEntry引数)と同じ
    /// PartId方式にそのまま乗るため、PartResolver解決(T-016)も無変更で動く。
    /// </summary>
    public ICommand SelectBuiltinToolCommand { get; }

    /// <summary>選択ツール(Esc相当)へ戻すコマンド。Window_PreviewKeyDownのEscケースと同じ操作。</summary>
    public ICommand SelectDefaultToolCommand { get; }

    public MainWindowViewModel()
    {
        SheetNavigation = new SheetNavigationViewModel(this);
        PartPalette = new PartPaletteViewModel(this);
        PartLibrary = BuildPartLibrary(PartPalette.Entries);
        DeviceTable = new DeviceTableViewModel(Document.Devices);

        SelectBuiltinToolCommand = new RelayCommand(param =>
        {
            if (param is not string partName) return;
            var entry = PartPalette.Entries.FirstOrDefault(e => e.Category == "" && e.Definition.Name == partName);
            if (entry is not null)
                Tool = new ToolState(ToolMode.PlaceElement, PartId: entry.Definition.Id);
        });

        SelectDefaultToolCommand = new RelayCommand(() => Tool = ToolState.SelectDefault);
    }

    private static PartLibrary BuildPartLibrary(IReadOnlyList<Persistence.PartFolderEntry> entries)
    {
        var library = new PartLibrary();
        foreach (var entry in entries)
            library.ById[entry.Definition.Id] = entry.Definition;
        return library;
    }

    // シート切替(T-026段階2)の動作確認がしやすいよう、シートごとに異なる要素構成にしている。
    private static LadderDocument CreateDummyDocument()
    {
        var document = new LadderDocument { Devices = CreateDummyDeviceTable() };
        document.Sheets.Add(new Sheet
        {
            PageNumber = 1,
            Name = "シート1",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
            Elements =
            {
                new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 2), DeviceName = "X0" },
                new ElementInstance { Kind = ElementKind.ContactNC, Pos = new GridPos(0, 5), DeviceName = "X1" },
                new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 8), DeviceName = "Y0" },
            },
        });
        document.Sheets.Add(new Sheet
        {
            PageNumber = 2,
            Name = "シート2",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
            Elements =
            {
                new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 4), DeviceName = "Y1" },
            },
        });
        document.Sheets.Add(new Sheet
        {
            PageNumber = 3,
            Name = "シート3",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        return document;
    }

    // 機器表の動作確認用ダミーデータ。CreateDummyDocument内の各シートのDeviceNameと対応させている。
    private static DeviceTable CreateDummyDeviceTable() => new()
    {
        ByName =
        {
            ["X0"] = new Device { Name = "X0", Class = DeviceClass.PushButton },
            ["X1"] = new Device { Name = "X1", Class = DeviceClass.PushButton },
            ["Y0"] = new Device { Name = "Y0", Class = DeviceClass.Relay },
            ["Y1"] = new Device { Name = "Y1", Class = DeviceClass.Relay },
        },
    };
}
