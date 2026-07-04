using Ecad2.Model;
using Ecad2.Simulation;

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
            OnPropertyChanged(nameof(IsPartSelectionVisible));
        }
    }

    /// <summary>
    /// 右パネル下段の状況依存切替(T-026段階4-7、案B)。Tool.Mode==PlaceElementの間は
    /// 自作パーツ選択を表示し、それ以外はプロパティを表示する。自作パーツボタン押下で
    /// Tool.Mode=PlaceElement(PartId未確定)にすることでパネルを開く(鶏卵問題の回避)。
    /// </summary>
    public bool IsPartSelectionVisible => Tool.Mode == ToolMode.PlaceElement;

    private double _canvasScale = 1.0;

    /// <summary>キャンバスの表示倍率（Ctrl+マウスホイールで変更）。0.25〜4.0の範囲にクランプする。</summary>
    public double CanvasScale
    {
        get => _canvasScale;
        set => SetProperty(ref _canvasScale, Math.Clamp(value, 0.25, 4.0));
    }

    /// <summary>
    /// 開いているドキュメント全体（複数シートを保持）。起動時はダミーデータ(3シート)。
    /// 新規/開く(T-019)ではReplaceDocumentで丸ごと差し替える。setterは外部非公開とし、
    /// 差し替え経路をReplaceDocumentへ一本化する(GuiEcadの反省: 文書破棄操作が複数入口に
    /// 分散し確認漏れが生じた、docs/ecad2-guiecad-code-survey-onmitsu.md T-024節)。
    /// </summary>
    public LadderDocument Document { get; private set; } = CreateDummyDocument();

    /// <summary>現在開いている.GCADファイルのパス(T-019)。新規作成/未保存はnull。
    /// 上書き保存が可能か(パスがあるか)の判定に使う。</summary>
    public string? CurrentFilePath { get; private set; }

    private int _currentSheetIndex;

    /// <summary>現在表示中のシートのインデックス（Document.Sheets への添字）。左パレットのシート
    /// ナビゲーション(T-026)からの選択、およびDRC出力パネルの行選択によるジャンプ(T-018)で
    /// 変更される。</summary>
    public int CurrentSheetIndex
    {
        get => _currentSheetIndex;
        set
        {
            if (!SetProperty(ref _currentSheetIndex, value)) return;
            OnPropertyChanged(nameof(CurrentSheet));
            // シート切替時、前シートのSelectedCell(ハイライト・プロパティパネル)を持ち越さない
            // (T-018忍者実機検証で発見: 空シートへ切替えてもハイライト・プロパティ内容が残存するバグ)。
            SelectedCell = null;
            // SheetNavigation.SelectedSheetはCurrentSheetIndexを読むだけの導出プロパティのため、
            // DRC出力パネルのジャンプ(T-018)等、シートナビ経由以外でCurrentSheetIndexが変わった際は
            // 左パレットの選択ハイライトが追従しない(T-018忍者実機検証で発見)。ここで明示的に通知する。
            SheetNavigation.RefreshSelectedSheet();
        }
    }

    /// <summary>現在表示中のシート。Document.Sheets[CurrentSheetIndex] の読み取り専用ビュー。</summary>
    public Sheet CurrentSheet => Document.Sheets[CurrentSheetIndex];

    /// <summary>
    /// プロジェクト(ドキュメント)が実在するか(T-020)。GX Works3踏襲の空状態(濃紺)⇔作業領域(白＋黒
    /// グリッド)の状態依存配色(App.xaml の EmptyStateBackgroundBrush/WorkAreaBackgroundBrush、
    /// 殿裁定)を切替えるための状態。GuiEcadには「未作成の空状態」という概念自体が無かったため、
    /// ecad2で新規導入する(docs/ecad2-preimplementation-survey-onmitsu.md T-020節)。ドキュメント
    /// 管理(新規/開く/保存、T-019)が未実装のため、現状はDocumentが常時ダミーの3シートを持ち、
    /// シート削除も最後の1枚を残すガードがあるため実際には常にtrueのまま(現行UIから空状態には
    /// 到達しない、暫定固定)。T-019で「新規」フローがSheets.Countを0にできるようになった時点で
    /// 自然に機能する。
    /// </summary>
    public bool HasProject => Document.Sheets.Count > 0;

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
            {
                OnPropertyChanged(nameof(SelectedCellDisplay));
                OnPropertyChanged(nameof(SelectedElement));
                OnPropertyChanged(nameof(HasSelectedElement));
                OnPropertyChanged(nameof(SelectedElementKindDisplay));
                OnPropertyChanged(nameof(SelectedElementDeviceName));
            }
        }
    }

    /// <summary>SelectedCellのステータスバー表示用文字列。</summary>
    public string SelectedCellDisplay => SelectedCell is { } pos ? $"行{pos.Row + 1}/列{pos.Column}" : "未選択";

    /// <summary>SelectedCellの位置にある既存要素(T-017)。null=要素なし、または未選択。</summary>
    public ElementInstance? SelectedElement
        => SelectedCell is { } pos ? CurrentSheet.Elements.FirstOrDefault(el => el.Pos == pos) : null;

    /// <summary>右パネル下段のプロパティ表示切替に使う(選択中セルに要素があるか)。</summary>
    public bool HasSelectedElement => SelectedElement is not null;

    /// <summary>SelectedElementの種別表示名。PartIdがあれば図形定義名、なければKindの日本語名
    /// (ダミーデータ等、PartId無しでKindのみ設定された要素向け)。</summary>
    public string SelectedElementKindDisplay
    {
        get
        {
            var el = SelectedElement;
            if (el is null) return "";
            if (el.PartId is string partId)
            {
                var entry = PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == partId);
                if (entry is not null) return entry.Definition.Name;
            }
            return KindDisplayName(el.Kind);
        }
    }

    private static string KindDisplayName(ElementKind kind) => kind switch
    {
        ElementKind.ContactNO => "a接点",
        ElementKind.ContactNC => "b接点",
        ElementKind.Coil => "コイル",
        ElementKind.Lamp => "ランプ",
        ElementKind.PushButtonNO => "押しボタン(NO)",
        ElementKind.PushButtonNC => "押しボタン(NC)",
        ElementKind.SelectSwitch => "セレクトSW",
        ElementKind.Terminal => "端子台",
        ElementKind.Timer => "タイマ",
        ElementKind.Counter => "カウンタ",
        _ => kind.ToString(),
    };

    /// <summary>
    /// SelectedElementのデバイス名(T-017、プロパティパネルで編集可能)。ElementInstance.DeviceName
    /// を書き換えるだけでなく、Document.Devices(機器表)にも反映する。同名デバイスを参照する他要素
    /// も含めた一括リネームはSimulation.DeviceRenamerに委譲する(既存デバイスの改名時)。新規デバイス
    /// 名(Document.Devicesに未登録)の場合はDeviceClass.Otherで新規登録する(忍者実機検証で発覚:
    /// 単純代入だけでは機器表に反映されないバグの修正)。
    /// </summary>
    public string SelectedElementDeviceName
    {
        get => SelectedElement?.DeviceName ?? "";
        set
        {
            if (SelectedElement is not ElementInstance el) return;
            string oldName = el.DeviceName ?? "";
            string newName = value.Trim();
            if (oldName == newName) return;

            if (oldName.Length > 0 && newName.Length > 0)
            {
                DeviceRenamer.Rename(Document, oldName, newName);
            }
            else
            {
                el.DeviceName = newName.Length > 0 ? newName : null;
                if (newName.Length > 0 && !Document.Devices.ByName.ContainsKey(newName))
                    Document.Devices.ByName[newName] = new Device { Name = newName, Class = DeviceClass.Other };
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedElementKindDisplay));
            DeviceTable.Refresh();
        }
    }

    /// <summary>
    /// SelectedElement(選択中の要素)を削除する(T-017追加スコープ、Deleteキー)。SelectedCell自体は
    /// 維持する(削除後もハイライト位置・矢印キー操作の起点を保つ、GX Works3等の一般的な挙動)。
    /// 削除した要素のDeviceNameを、Document.Sheets全体で他のどの要素も参照しなくなった場合は
    /// 機器表(Document.Devices)からも該当エントリを削除する(既存のDeviceRenamerに削除系メソッドは
    /// 無かったため新規実装)。戻り値は実際に削除したか。
    /// </summary>
    public bool DeleteSelectedElement()
    {
        if (SelectedElement is not ElementInstance el) return false;

        string? deviceName = el.DeviceName;
        CurrentSheet.Elements.Remove(el);

        if (deviceName is not null)
        {
            bool stillReferenced = Document.Sheets.Any(s =>
                s.Elements.Any(e => string.Equals(e.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)));
            if (!stillReferenced)
            {
                var key = Document.Devices.ByName.Keys
                    .FirstOrDefault(k => string.Equals(k, deviceName, StringComparison.OrdinalIgnoreCase));
                if (key is not null) Document.Devices.ByName.Remove(key);
            }
        }

        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
        DeviceTable.Refresh();
        return true;
    }

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

    /// <summary>下部出力パネル（DesignRuleCheck結果表示）の子ViewModel。</summary>
    public OutputPanelViewModel OutputPanel { get; }

    /// <summary>
    /// 現在使用可能な自作パーツライブラリ。PartPalette.Entries（PartFolderStoreの列挙結果、
    /// 基本図形もPartIdを持つ.gcadpartとして含む）から構築する。DiagramRenderer.Render /
    /// LadderCanvas.Draw に渡し、要素配置時（T-016）の PartResolver 解決にも使う。
    /// </summary>
    public PartLibrary PartLibrary { get; }

    /// <summary>SelectedCellの行に既に要素が置かれているか判定する(T-026段階4: 配置行は空き行限定、行挿入はしない)。</summary>
    public bool IsSelectedCellOccupied()
        => SelectedCell is { } pos && CurrentSheet.Elements.Any(el => el.Pos == pos);

    /// <summary>
    /// SelectedCellへ要素を配置する(T-026段階4新配置フロー)。isOr=trueの場合、基準行
    /// (SelectedCellより上にある直近の既存要素行、殿裁定で上方向限定)との間に縦コネクタを
    /// 2本自動生成しOR(並列)接続にする。基準行内では新要素に列位置が最も近い要素を対応先とする。
    /// </summary>
    public void PlaceElementAtSelectedCell(string partId, string deviceName, bool isOr)
    {
        if (SelectedCell is not { } pos) return;

        const int cellWidth = 1; // 基本図形(BasicPartTemplates)は全て1セル幅
        var newElement = new ElementInstance
        {
            Pos = pos,
            PartId = partId,
            DeviceName = deviceName.Length > 0 ? deviceName : null,
        };
        CurrentSheet.Elements.Add(newElement);

        if (!isOr) return;

        int? baseRow = CurrentSheet.Elements
            .Where(el => el != newElement && el.Pos.Row < pos.Row)
            .Select(el => (int?)el.Pos.Row)
            .DefaultIfEmpty(null)
            .Max();
        if (baseRow is not int br) return;

        var baseElement = CurrentSheet.Elements
            .Where(el => el.Pos.Row == br)
            .OrderBy(el => Math.Abs(el.Pos.Column - pos.Column))
            .FirstOrDefault();
        if (baseElement is null) return;

        int leftColumn = Math.Min(baseElement.Pos.Column, pos.Column);
        int rightColumn = Math.Max(baseElement.Pos.Column, pos.Column) + cellWidth;
        CurrentSheet.Connectors.Add(new VerticalConnector { Column = leftColumn, TopRow = br, BottomRow = pos.Row });
        CurrentSheet.Connectors.Add(new VerticalConnector { Column = rightColumn, TopRow = br, BottomRow = pos.Row });
    }

    /// <summary>現在のDocumentを指定パスへ.GCAD形式で保存する(T-019)。CurrentFilePathを更新する。
    /// I/O例外はそのまま呼び出し元(View層)へ伝播させ、技術的例外文面をユーザーへ出す変換は
    /// View側の責務とする(隠密調査 docs/ecad2-guiecad-code-survey-onmitsu.md T-024節推奨)。</summary>
    public void SaveToFile(string path)
    {
        Persistence.GcadSerializer.Save(Document, path);
        if (CurrentFilePath != path)
        {
            CurrentFilePath = path;
            OnPropertyChanged(nameof(CurrentFilePath));
        }
    }

    /// <summary>指定パスの.GCADファイルを読み込み、現在のDocumentを丸ごと差し替える(T-019)。
    /// I/O・スキーマ不一致例外はそのままView層へ伝播させる(SaveToFileと同方針)。</summary>
    public void LoadFromFile(string path)
    {
        var document = Persistence.GcadSerializer.Load(path);
        ReplaceDocument(document, path);
    }

    /// <summary>Documentを丸ごと差し替え、関連する子ViewModel・選択状態を再同期する
    /// (T-019: 新規/開く共通の単一ゲートウェイ。文書破棄操作の入口を分散させない、
    /// GuiEcadの反省 docs/ecad2-guiecad-code-survey-onmitsu.md T-024節を踏まえる)。</summary>
    private void ReplaceDocument(LadderDocument newDocument, string? filePath)
    {
        Document = newDocument;
        CurrentFilePath = filePath;
        _currentSheetIndex = 0;
        _selectedCell = null;
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(CurrentSheetIndex));
        OnPropertyChanged(nameof(CurrentSheet));
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(SelectedCell));
        OnPropertyChanged(nameof(SelectedCellDisplay));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
        SheetNavigation.ResetSheets();
        DeviceTable.Rebind(newDocument.Devices);
    }

    public MainWindowViewModel()
    {
        SheetNavigation = new SheetNavigationViewModel(this);
        PartPalette = new PartPaletteViewModel();
        PartLibrary = BuildPartLibrary(PartPalette.Entries);
        DeviceTable = new DeviceTableViewModel(Document.Devices);
        OutputPanel = new OutputPanelViewModel(this);
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
