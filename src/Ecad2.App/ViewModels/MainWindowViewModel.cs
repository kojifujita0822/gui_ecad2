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
    /// 現在表示中のシート。段階4-a時点ではキャンバス描画の動作確認用ダミーデータを起動時に設定する。
    /// 本実装（GcadSerializer.Load によるドキュメント読込）への置き換えは将来タスク。
    /// </summary>
    public Sheet CurrentSheet { get; } = CreateDummySheet();

    /// <summary>左パレット（部品選択）の子ViewModel。</summary>
    public PartPaletteViewModel PartPalette { get; }

    /// <summary>右パネル上段（機器表）の子ViewModel。</summary>
    public DeviceTableViewModel DeviceTable { get; }

    /// <summary>
    /// 現在使用可能な自作パーツライブラリ。PartPalette.Entries（PartFolderStoreの列挙結果、
    /// 基本図形もPartIdを持つ.gcadpartとして含む）から構築する。DiagramRenderer.Render /
    /// LadderCanvas.Draw に渡し、要素配置時（T-016）の PartResolver 解決にも使う。
    /// </summary>
    public PartLibrary PartLibrary { get; }

    public MainWindowViewModel()
    {
        PartPalette = new PartPaletteViewModel(this);
        PartLibrary = BuildPartLibrary(PartPalette.Entries);
        DeviceTable = new DeviceTableViewModel(CreateDummyDeviceTable());
    }

    private static PartLibrary BuildPartLibrary(IReadOnlyList<Persistence.PartFolderEntry> entries)
    {
        var library = new PartLibrary();
        foreach (var entry in entries)
            library.ById[entry.Definition.Id] = entry.Definition;
        return library;
    }

    private static Sheet CreateDummySheet() => new()
    {
        Name = "シート1",
        Grid = new GridSpec { Rows = 10, Columns = 20 },
        Elements =
        {
            new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 2), DeviceName = "X0" },
            new ElementInstance { Kind = ElementKind.ContactNC, Pos = new GridPos(0, 5), DeviceName = "X1" },
            new ElementInstance { Kind = ElementKind.Coil, Pos = new GridPos(0, 8), DeviceName = "Y0" },
        },
    };

    // 機器表の動作確認用ダミーデータ。CreateDummySheet の DeviceName と対応させている。
    private static DeviceTable CreateDummyDeviceTable() => new()
    {
        ByName =
        {
            ["X0"] = new Device { Name = "X0", Class = DeviceClass.PushButton },
            ["X1"] = new Device { Name = "X1", Class = DeviceClass.PushButton },
            ["Y0"] = new Device { Name = "Y0", Class = DeviceClass.Relay },
        },
    };
}
