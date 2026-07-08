using Ecad2.App.Diagnostics;
using Ecad2.Model;
using Ecad2.Persistence;
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
            // 隠密再レビュー指摘: IsEnabledガード無しだと無効時も_tool(値型)のボクシングが
            // 無条件発生する(finding7と同根)。短絡評価でfalse時は_toolへ触れないようにする。
            object? oldValue = TraceLog.IsEnabled ? _tool : null;
            _tool = value;
            OnPropertyChanged(nameof(Tool), oldValue);
            OnPropertyChanged(nameof(IsPartSelectionVisible));
        }
    }

    /// <summary>
    /// 右パネル下段の状況依存切替(T-026段階4-7、案B)。Tool.Mode==PlaceElementの間は
    /// 自作パーツ選択を表示し、それ以外はプロパティを表示する。自作パーツボタン押下で
    /// Tool.Mode=PlaceElement(PartId未確定)にすることでパネルを開く(鶏卵問題の回避)。
    /// </summary>
    public bool IsPartSelectionVisible => Tool.Mode == ToolMode.PlaceElement;

    private bool _isPlacementBarVisible;

    /// <summary>T-033増分1: 配置後入力の非モーダルバー表示状態(単一の真実源、隠密レビュー指摘)。
    /// バー表示中はメインコンテンツ(メニュー・ツールバー・メイン作業域・出力パネル)全体を
    /// IsEnabledバインドで無効化し、キャンバスクリック等マウス経由6系統の素通しを恒久的に塞ぐ
    /// (殿裁定=グレーアウト。個別ハンドラへの後追いガードはT-021の轍のため不採用)。</summary>
    public bool IsPlacementBarVisible
    {
        get => _isPlacementBarVisible;
        set => SetProperty(ref _isPlacementBarVisible, value);
    }

    private double _canvasScale = 1.0;

    /// <summary>キャンバスの表示倍率（Ctrl+マウスホイールで変更）。0.25〜4.0の範囲にクランプする。</summary>
    public double CanvasScale
    {
        get => _canvasScale;
        set => SetProperty(ref _canvasScale, Math.Clamp(value, 0.25, 4.0));
    }

    /// <summary>
    /// 開いているドキュメント全体（複数シートを保持）。起動直後は空(Sheets=0、HasProject=false=
    /// 濃紺スタート、殿裁定2026-07-05)。新規/開く(T-019)ではReplaceDocumentで丸ごと差し替える。
    /// setterは外部非公開とし、差し替え経路をReplaceDocumentへ一本化する(GuiEcadの反省: 文書破棄
    /// 操作が複数入口に分散し確認漏れが生じた、docs/ecad2-guiecad-code-survey-onmitsu.md T-024節)。
    /// </summary>
    public LadderDocument Document { get; private set; } = new();

    /// <summary>現在開いている.GCADファイルのパス(T-019)。新規作成/未保存はnull。
    /// 上書き保存が可能か(パスがあるか)の判定に使う。</summary>
    public string? CurrentFilePath { get; private set; }

    private bool _isDirty;

    /// <summary>未保存の変更があるか(T-019)。新規/開くでの上書き確認に使う。GuiEcadはUndo履歴depth
    /// との差分で判定し、Undo対象外の変更(シート追加/削除等)へのMarkDirty()呼び忘れが構造的欠陥
    /// だった(docs/ecad2-guiecad-code-survey-onmitsu.md T-024節)。ecad2はUndo機能自体が未実装
    /// なため、変更操作の入口(要素配置/削除/デバイス名変更、シート追加/削除/改名)で明示的に
    /// MarkDirty()を呼ぶ方式を採る。ReplaceDocument・保存成功時にfalseへ戻す。</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>ドキュメントに未保存の変更が生じたことを記録する(T-019)。</summary>
    public void MarkDirty() => IsDirty = true;

    private int _currentSheetIndex;

    /// <summary>現在表示中のシートのインデックス（Document.Sheets への添字）。左パレットのシート
    /// ナビゲーション(T-026)からの選択、およびDRC出力パネルの行選択によるジャンプ(T-018)で
    /// 変更される。</summary>
    public int CurrentSheetIndex
    {
        get => _currentSheetIndex;
        set
        {
            // T-041増分5隠密レビュー指摘(観点3 CONFIRMED重大、増分1由来の構造的な穴): シート削除
            // (SheetNavigationViewModel.DeleteCommand)で「非末尾シートを削除、かつそれが現在表示中」
            // の場合、削除後のindex数値がたまたま削除前と一致するケースがある(Sheets[index]の実体は
            // 差し替わっているのに、int値としてのCurrentSheetIndexは変化しない)。CurrentSheetIndex
            // は「値そのもの」ではなく「Document.Sheetsへの添字(キー)」に過ぎず、キーの数値が同じ
            // でも参照先の実体(Sheets[index])が入れ替わりうる非対称性を持つ。よってプロパティ
            // 自身の変更通知(OnPropertyChanged(nameof(CurrentSheet)))・クロスカット的クリア
            // (前シートのSelectedCell・全選択状態/記入中状態の連鎖クリア、左パレット選択ハイライト
            // 同期)は共に値変化の有無に関わらず常時実行する(SetPropertyの戻り値でガードしない)。
            //
            // T-041増分5隠密再レビュー往復2周目で、この無条件クロスカットクリアがシート改名
            // (SheetNavigationViewModel.RenameCommand、参照だけが入れ替わりindex数値は不変)にも
            // 波及し記入中ドラフトを警告なく破棄する副作用(所見L)を発見、一度はSelectedCellの
            // setterと同じ粒度(変更通知のみ値変化時限定)へ統一したが、往復3周目でそれが本節
            // 冒頭の症状1(削除時の再描画漏れ)を「削除直前にSelectedCellが既にnull」という別条件
            // 下で再発させることが判明した。真因は改名の遅延再選択がCurrentSheetIndexへの代入
            // 自体を経由すること側にあったため、対処はSheetNavigationViewModel.RenameCommand側
            // (RefreshSelectedSheet()のみ呼ぶ形)で行い、本setterは往復1周目の「常時無条件」の
            // ままとする(二重のモグラ叩きを避ける)。
            SetProperty(ref _currentSheetIndex, value);
            OnPropertyChanged(nameof(CurrentSheet));
            SelectedCell = null;
            SheetNavigation.RefreshSelectedSheet();
        }
    }

    /// <summary>現在表示中のシート。Document.Sheets[CurrentSheetIndex] の読み取り専用ビュー。
    /// Document.Sheets.Count==0(起動直後の濃紺スタート、殿裁定2026-07-05)の間はnull。</summary>
    public Sheet? CurrentSheet
        => CurrentSheetIndex >= 0 && CurrentSheetIndex < Document.Sheets.Count ? Document.Sheets[CurrentSheetIndex] : null;

    /// <summary>
    /// プロジェクト(ドキュメント)が実在するか(T-020)。GX Works3踏襲の空状態(濃紺)⇔作業領域(白＋黒
    /// グリッド)の状態依存配色(App.xaml の EmptyStateBackgroundBrush/WorkAreaBackgroundBrush、
    /// 殿裁定)を切替えるための状態。GuiEcadには「未作成の空状態」という概念自体が無かったため、
    /// ecad2で新規導入する(docs/ecad2-preimplementation-survey-onmitsu.md T-020節)。起動直後は
    /// Documentが空(Sheets=0)のためfalse=濃紺スタート(殿裁定2026-07-05)、新規(1シート生成)/開く
    /// でtrueへ切替わる。
    /// </summary>
    public bool HasProject => Document.Sheets.Count > 0;

    /// <summary>Document.Sheets.Countの増減を伴う操作(SheetNavigationViewModelのシート追加/削除等)
    /// の後に呼ぶ(T-019、殿実機検出の修正)。HasProjectはReplaceDocument内でのみ明示通知しており、
    /// シート追加/削除がDocument.Sheetsを直接操作するSheetNavigationViewModel側からは通知されず、
    /// Sheets=0(濃紺)からシート追加しても画面が作業領域色へ切り替わらない欠陥があった。</summary>
    public void NotifyHasProjectChanged() => OnPropertyChanged(nameof(HasProject));

    /// <summary>Sheets 0→1遷移(起動直後濃紺スタートから最初のシート追加、殿裁定2026-07-05)後に
    /// 呼ぶ(隠密レビュー指摘、往復2周目回帰)。CurrentSheetIndexの既定値が0のため、追加後に
    /// SelectedSheet=先頭シートを設定してもCurrentSheetIndexが0→0で「変化なし」と判定され
    /// CurrentSheetのPropertyChangedが発火せず、RedrawCanvasが呼ばれず画面が空白のままになる。
    /// CurrentSheetIndexの番兵値化(-1化、影響範囲が広い)は見送り、この経路でのみ明示発火する。</summary>
    public void NotifyCurrentSheetChanged() => OnPropertyChanged(nameof(CurrentSheet));

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
            // T-041増分1隠密レビュー指摘(観点2 CONFIRMED4件): SelectedCellとSelectedConnectorは
            // 排他のはずが、呼び出し元(矢印キー移動・選択解除ボタン等)がSelectedConnectorのクリアを
            // 個別に書き忘れると崩れる(実際に4箇所で発生)。呼び出し元の記憶に頼る方式をやめ、この
            // setter自身を「選択状態をクリアする唯一の入口」にする。値が変化しない場合も含め常時
            // クリアする(下のSetPropertyの早期returnより前に置くのはそのため、CurrentSheetIndex
            // 経由の同一シートジャンプで早期returnにより後続処理が丸ごと飛ばされた実例への対処)。
            // 縦コネクタをクリック選択する経路は、この副作用を踏まえてSelectedCell=null→
            // SelectedConnector=connectorの順で呼ぶ(MainWindow.xaml.cs)。
            SelectedConnector = null;
            // T-041増分3: 配線分断(WireBreak)も同じ排他対象として扱う(SelectedWireBreak参照)。
            SelectedWireBreak = null;
            // T-041増分5: 自由線・接続点(主回路シート)も同様に扱う。
            SelectedFreeLine = null;
            SelectedConnectionDot = null;
            // T-041増分2隠密レビュー指摘(観点3 CONFIRMED、所見E=増分1所見Aの反復): 記入中
            // (_connectorDraft)はSelectedCellとは別枠の状態だったため、CurrentSheetIndexの
            // シート切替経由でSelectedCellがクリアされてもTool/_connectorDraftが残留し、
            // 制御回路限定・グリッド範囲という不変条件を確定時に迂回しうるバグを生んだ。
            // BeginConnectorDraftはSelectedCellを読むだけでこのsetterを経由しないため記入開始
            // 自体は妨げない。PlaceElementのTool保持(T-021分岐A)には影響しないよう、記入中の
            // 場合のみ限定してクリアする。
            ClearConnectorDraftIfAny();
            // T-041増分5: 自由線の記入中状態(_freeLineDraft)も同型でクリアする。
            ClearFreeLineDraftIfAny();
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

    private VerticalConnector? _selectedConnector;

    /// <summary>
    /// 現在選択中の縦コネクタ(T-041増分1: 配線プリミティブの選択、GridPos単位のSelectedCellとは
    /// 別枠で保持する)。SelectedCellとは排他——SelectedCellのsetterが値変化の有無に関わらず常時
    /// このプロパティをクリアするため(隠密レビュー指摘、上記SelectedCell参照)、縦コネクタを
    /// クリック選択する経路はSelectedCell=null→SelectedConnector=connectorの順で呼ぶ必要がある
    /// (逆順だとSelectedCellのクリアが直後にこのプロパティを打ち消してしまう)。単一選択のみ
    /// (殿裁定2026-07-07)。
    /// </summary>
    public VerticalConnector? SelectedConnector
    {
        get => _selectedConnector;
        set => SetProperty(ref _selectedConnector, value);
    }

    /// <summary>
    /// SelectedConnectorを削除する(T-041増分1、案A=既存の部品削除(Deleteキー)と同型)。
    /// Undo機能は不採用のため直接List操作＋MarkDirty()の流儀に揃える(DeleteSelectedElementと同様)。
    /// 隠密レビュー指摘: Remove()の戻り値(実際に削除できたか)を見て、できなければMarkDirty()も
    /// falseもtrueにせず、削除できていない状態を偽って報告しない(DeleteSelectedElementとの一貫性)。
    /// 戻り値は実際に削除したか。
    /// </summary>
    public bool DeleteSelectedConnector()
    {
        if (CurrentSheet is not Sheet sheet || SelectedConnector is not VerticalConnector connector) return false;
        if (!sheet.Connectors.Remove(connector)) return false;
        MarkDirty();
        SelectedConnector = null;
        return true;
    }

    private WireBreak? _selectedWireBreak;

    /// <summary>
    /// 現在選択中の配線分断(T-041増分3: 制御回路シートの点系プリミティブ、SelectedConnectorと同型の
    /// 排他制御——SelectedCellのsetterが常時クリアする)。単一選択のみ。
    /// </summary>
    public WireBreak? SelectedWireBreak
    {
        get => _selectedWireBreak;
        set => SetProperty(ref _selectedWireBreak, value);
    }

    /// <summary>
    /// SelectedWireBreakを削除する(T-041増分3、案A=DeleteSelectedConnectorと同型)。
    /// 戻り値は実際に削除したか。
    /// </summary>
    public bool DeleteSelectedWireBreak()
    {
        if (CurrentSheet is not Sheet sheet || SelectedWireBreak is not WireBreak wireBreak) return false;
        if (!sheet.WireBreaks.Remove(wireBreak)) return false;
        MarkDirty();
        SelectedWireBreak = null;
        return true;
    }

    /// <summary>
    /// F10押下時、SelectedCellの位置へ配線分断を即時記入する(T-041増分3、点系は確認フェーズ無し
    /// で即時記入する原案3節の設計)。呼び出し元(MainWindow.xaml.cs)でHasProject/制御回路シート判定は
    /// 済ませてある前提。同一位置に既存の配線分断があれば重複記入を避けて何もしない(戻り値false)。
    /// </summary>
    public bool PlaceWireBreakAtSelectedCell()
    {
        if (SelectedCell is not { } pos || CurrentSheet is not Sheet sheet) return false;
        double boundary = pos.Column + 0.5;
        if (sheet.WireBreaks.Any(b => b.Row == pos.Row && b.Boundary == boundary)) return false;
        sheet.WireBreaks.Add(new WireBreak { Boundary = boundary, Row = pos.Row });
        MarkDirty();
        return true;
    }

    private FreeLine? _selectedFreeLine;

    /// <summary>
    /// 現在選択中の自由線(T-041増分5: 主回路シートの線系プリミティブ、SelectedConnectorと同型の
    /// 排他制御——SelectedCellのsetterが常時クリアする)。単一選択のみ。
    /// </summary>
    public FreeLine? SelectedFreeLine
    {
        get => _selectedFreeLine;
        set => SetProperty(ref _selectedFreeLine, value);
    }

    /// <summary>SelectedFreeLineを削除する(T-041増分5、案A=DeleteSelectedConnectorと同型)。
    /// 戻り値は実際に削除したか。</summary>
    public bool DeleteSelectedFreeLine()
    {
        if (CurrentSheet is not Sheet sheet || SelectedFreeLine is not FreeLine freeLine) return false;
        if (!sheet.FreeLines.Remove(freeLine)) return false;
        MarkDirty();
        SelectedFreeLine = null;
        return true;
    }

    private ConnectionDot? _selectedConnectionDot;

    /// <summary>
    /// 現在選択中の接続点(T-041増分5: 主回路シートの点系プリミティブ、SelectedWireBreakと同型の
    /// 排他制御——SelectedCellのsetterが常時クリアする)。単一選択のみ。
    /// </summary>
    public ConnectionDot? SelectedConnectionDot
    {
        get => _selectedConnectionDot;
        set => SetProperty(ref _selectedConnectionDot, value);
    }

    /// <summary>SelectedConnectionDotを削除する(T-041増分5、案A=DeleteSelectedWireBreakと同型)。
    /// 戻り値は実際に削除したか。</summary>
    public bool DeleteSelectedConnectionDot()
    {
        if (CurrentSheet is not Sheet sheet || SelectedConnectionDot is not ConnectionDot dot) return false;
        if (!sheet.ConnectionDots.Remove(dot)) return false;
        MarkDirty();
        SelectedConnectionDot = null;
        return true;
    }

    /// <summary>
    /// F10押下時、指定のmm座標へ接続点を即時記入する(T-041増分5、主回路シート限定・点系は確認
    /// フェーズ無しでWireBreakと同型)。mm座標への変換(SelectedCell→mm)はView側の責務
    /// (LadderCanvas.CellToMm)、ViewModelはmm座標を受け取るのみ(FreeLineの座標系がグリッドに
    /// 依存しないmm実座標のため、他の記入メソッドと異なりViewModelは幾何変換を行わない)。
    /// 同一位置に既存の接続点があれば重複記入を避けて何もしない(戻り値false)。
    /// </summary>
    public bool PlaceConnectionDot(double xMm, double yMm)
    {
        if (CurrentSheet is not Sheet sheet) return false;
        if (sheet.ConnectionDots.Any(d => d.XMm == xMm && d.YMm == yMm)) return false;
        sheet.ConnectionDots.Add(new ConnectionDot { XMm = xMm, YMm = yMm });
        MarkDirty();
        return true;
    }

    // T-041増分2: 縦コネクタ手動記入(sF9)の作業中データ。AnchorRowは記入開始行(固定)、CurrentRowは
    // 矢印キーで動く終点行。TopRow/BottomRowはこの2つのMin/Maxとして都度導出する(`ecad2-t041-key-flow
    // -proposal-samurai.md`3節の設計)。
    private (int AnchorRow, int CurrentRow, double Column)? _connectorDraft;

    /// <summary>記入中の縦コネクタのプレビュー形状(LadderCanvasの点線描画用)。記入中でなければnull。</summary>
    public VerticalConnector? ConnectorDraftPreview
    {
        get
        {
            if (_connectorDraft is not { } draft) return null;
            return new VerticalConnector
            {
                Column = draft.Column,
                TopRow = Math.Min(draft.AnchorRow, draft.CurrentRow),
                BottomRow = Math.Max(draft.AnchorRow, draft.CurrentRow),
            };
        }
    }

    /// <summary>
    /// sF9押下時、SelectedCellを起点に縦コネクタ記入を開始する(T-041増分2)。呼び出し元
    /// (MainWindow.xaml.cs)でHasProject/制御回路シート判定は済ませてある前提。始点列境界は
    /// SelectedCellのセル左端(整数境界)とする(原案3節)。
    /// </summary>
    public void BeginConnectorDraft()
    {
        if (SelectedCell is not { } pos) return;
        _connectorDraft = (pos.Row, pos.Row, pos.Column);
        Tool = new ToolState(ToolMode.PlaceConnector);
        OnPropertyChanged(nameof(ConnectorDraftPreview));
    }

    /// <summary>記入中の縦コネクタの終点行を矢印キー1回分(delta=±1)動かす。グリッド行範囲内にクランプする。</summary>
    public void MoveConnectorDraftRow(int delta)
    {
        if (_connectorDraft is not { } draft || CurrentSheet is not Sheet sheet) return;
        int newRow = Math.Clamp(draft.CurrentRow + delta, 0, sheet.Grid.Rows - 1);
        _connectorDraft = draft with { CurrentRow = newRow };
        OnPropertyChanged(nameof(ConnectorDraftPreview));
    }

    /// <summary>記入中の縦コネクタの列境界を矢印キー1回分動かす(step=±1.0は整数境界、±0.5は
    /// Shift+Left/Rightによるセル中央境界、原案3節)。グリッド列範囲内にクランプする。</summary>
    public void MoveConnectorDraftColumn(double step)
    {
        if (_connectorDraft is not { } draft || CurrentSheet is not Sheet sheet) return;
        double newColumn = Math.Clamp(draft.Column + step, 0, sheet.Grid.Columns);
        _connectorDraft = draft with { Column = newColumn };
        OnPropertyChanged(nameof(ConnectorDraftPreview));
    }

    /// <summary>
    /// 記入中の縦コネクタを確定する(Enter、T-041増分2)。TopRow==BottomRow(まだ伸縮していない)場合は
    /// 縦コネクタとして無意味なため確定せず記入モードに留める(戻り値false)。確定後は既存OR自動配線
    /// (PlaceElementAtSelectedCell)と同じ直接List操作＋MarkDirty()の流儀に揃える。
    /// 隠密レビュー指摘(観点3 CONFIRMED): 記入中にシートが切り替わってもSelectedCellのsetter経由の
    /// クリア(上記参照)で通常は起こり得なくなったが、防御的二重チェックとして制御回路シート限定・
    /// グリッド範囲のクランプをここでも再検証する(将来別経路が生まれても二重に守られる)。
    /// </summary>
    public bool ConfirmConnectorDraft()
    {
        if (_connectorDraft is not { } draft || CurrentSheet is not Sheet sheet || sheet.MainCircuit) return false;
        int topRow = Math.Clamp(Math.Min(draft.AnchorRow, draft.CurrentRow), 0, sheet.Grid.Rows - 1);
        int bottomRow = Math.Clamp(Math.Max(draft.AnchorRow, draft.CurrentRow), 0, sheet.Grid.Rows - 1);
        double column = Math.Clamp(draft.Column, 0, sheet.Grid.Columns);
        if (topRow == bottomRow) return false;
        sheet.Connectors.Add(new VerticalConnector { Column = column, TopRow = topRow, BottomRow = bottomRow });
        MarkDirty();
        CancelConnectorDraft();
        return true;
    }

    /// <summary>記入中の縦コネクタ記入を取消す(Esc、T-041増分2)。何も生成せず選択モードへ戻す。</summary>
    public void CancelConnectorDraft() => ClearConnectorDraftIfAny();

    /// <summary>
    /// 記入中(_connectorDraft)であれば取消してSelect状態へ戻す(T-041増分2隠密レビュー指摘、
    /// 観点3 CONFIRMED・所見E対応)。記入中でなければ何もしない(PlaceElementのTool保持、T-021
    /// 分岐Aの継続配置フローに影響を与えないため)。SelectedCellのsetter・CancelConnectorDraft・
    /// ReplaceDocumentの共通クリア入口として一本化する。
    /// </summary>
    private void ClearConnectorDraftIfAny()
    {
        if (_connectorDraft is null) return;
        _connectorDraft = null;
        Tool = ToolState.SelectDefault;
        OnPropertyChanged(nameof(ConnectorDraftPreview));
    }

    // T-041増分5: 自由線手動記入(F9/sF9、主回路シート)の作業中データ。AnchorXMm/AnchorYMmは記入
    // 開始点(固定、mm実座標)。StepMmは矢印キー1回分の移動量(View側でCellMmを渡す)。StepCountは
    // 矢印キーで動く符号付きステップ数(IsHorizontal=trueならX方向、falseならY方向にのみ動く)。
    // mm座標への変換はView側の責務(LadderCanvas.CellToMm)で完結させ、ViewModelは幾何を知らない
    // (VerticalConnectorがグリッド単位で完結するのと対称的に、FreeLineはmm単位で完結させる設計)。
    private (double AnchorXMm, double AnchorYMm, double StepMm, int StepCount, bool IsHorizontal)? _freeLineDraft;

    /// <summary>記入中の自由線が水平方向かを返す(View側のキー判定用)。記入中でなければfalse。</summary>
    public bool IsFreeLineDraftHorizontal => _freeLineDraft?.IsHorizontal ?? false;

    /// <summary>記入中の自由線のプレビュー形状(LadderCanvasの点線描画用)。記入中でなければnull。</summary>
    public FreeLine? FreeLineDraftPreview
    {
        get
        {
            if (_freeLineDraft is not { } d) return null;
            double currentX = d.IsHorizontal ? d.AnchorXMm + d.StepCount * d.StepMm : d.AnchorXMm;
            double currentY = d.IsHorizontal ? d.AnchorYMm : d.AnchorYMm + d.StepCount * d.StepMm;
            return new FreeLine
            {
                X1Mm = Math.Min(d.AnchorXMm, currentX),
                Y1Mm = Math.Min(d.AnchorYMm, currentY),
                X2Mm = Math.Max(d.AnchorXMm, currentX),
                Y2Mm = Math.Max(d.AnchorYMm, currentY),
            };
        }
    }

    /// <summary>
    /// F9(横線)/sF9(縦線)押下時、指定のmm座標を起点に自由線記入を開始する(T-041増分5)。呼び出し元
    /// (MainWindow.xaml.cs)でHasProject/主回路シート判定・SelectedCell存在は済ませてある前提。
    /// mm座標への変換(SelectedCell→mm)・矢印キー1回分の移動量(stepMm=CellMm)はView側が渡す。
    /// </summary>
    public void BeginFreeLineDraft(bool horizontal, double startXMm, double startYMm, double stepMm)
    {
        _freeLineDraft = (startXMm, startYMm, stepMm, 0, horizontal);
        Tool = new ToolState(ToolMode.PlaceLine);
        OnPropertyChanged(nameof(FreeLineDraftPreview));
    }

    /// <summary>記入中の自由線の終点を矢印キー1回分(delta=±1)動かす。方向(水平/垂直)に沿わない
    /// キーは呼び出し元(View)が弾く前提で、ここでは単純にStepCountを加算しグリッド範囲相当
    /// (Columns/Rows)内にクランプする。</summary>
    public void MoveFreeLineDraftEnd(int delta)
    {
        if (_freeLineDraft is not { } d || CurrentSheet is not Sheet sheet) return;
        int maxSteps = d.IsHorizontal ? sheet.Grid.Columns : sheet.Grid.Rows;
        int newStepCount = Math.Clamp(d.StepCount + delta, -maxSteps, maxSteps);
        _freeLineDraft = d with { StepCount = newStepCount };
        OnPropertyChanged(nameof(FreeLineDraftPreview));
    }

    /// <summary>
    /// 記入中の自由線を確定する(Enter、T-041増分5)。StepCount==0(まだ伸縮していない)場合は
    /// 自由線として無意味なため確定せず記入モードに留める(戻り値false)。ConfirmConnectorDraftと
    /// 同様、主回路シート限定を防御的に再検証する(二重の安全網)。
    /// </summary>
    public bool ConfirmFreeLineDraft()
    {
        if (_freeLineDraft is not { } d || CurrentSheet is not Sheet sheet || !sheet.MainCircuit) return false;
        if (d.StepCount == 0) return false;
        var preview = FreeLineDraftPreview!;
        sheet.FreeLines.Add(new FreeLine
        {
            X1Mm = preview.X1Mm, Y1Mm = preview.Y1Mm, X2Mm = preview.X2Mm, Y2Mm = preview.Y2Mm,
        });
        MarkDirty();
        CancelFreeLineDraft();
        return true;
    }

    /// <summary>記入中の自由線記入を取消す(Esc、T-041増分5)。何も生成せず選択モードへ戻す。</summary>
    public void CancelFreeLineDraft() => ClearFreeLineDraftIfAny();

    /// <summary>記入中(_freeLineDraft)であれば取消してSelect状態へ戻す(SelectedCellのsetter・
    /// CancelFreeLineDraft・ReplaceDocumentの共通クリア入口、ClearConnectorDraftIfAnyと同型)。</summary>
    private void ClearFreeLineDraftIfAny()
    {
        if (_freeLineDraft is null) return;
        _freeLineDraft = null;
        Tool = ToolState.SelectDefault;
        OnPropertyChanged(nameof(FreeLineDraftPreview));
    }

    /// <summary>SelectedCellの位置にある既存要素(T-017)。null=要素なし、または未選択。</summary>
    public ElementInstance? SelectedElement
        => SelectedCell is { } pos && CurrentSheet is Sheet sheet ? sheet.Elements.FirstOrDefault(el => el.Pos == pos) : null;

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
                if (newName.Length > 0)
                {
                    if (!Document.Devices.ByName.ContainsKey(newName))
                        Document.Devices.ByName[newName] = new Device { Name = newName, Class = DeviceClass.Other };
                }
                else if (oldName.Length > 0)
                {
                    // 空文字確定(旧名を手放す場合)。DeleteSelectedElementと同一ポリシー: 他要素から
                    // まだ参照されていれば機器表エントリを保持、参照が無ければ削除する(忍者実機検証で
                    // 発覚: 機器表への孤立残存バグの修正、T-036)。
                    bool stillReferenced = Document.Sheets.Any(s =>
                        s.Elements.Any(e => string.Equals(e.DeviceName, oldName, StringComparison.OrdinalIgnoreCase)));
                    if (!stillReferenced)
                    {
                        var key = Document.Devices.ByName.Keys
                            .FirstOrDefault(k => string.Equals(k, oldName, StringComparison.OrdinalIgnoreCase));
                        if (key is not null) Document.Devices.ByName.Remove(key);
                    }
                }
            }

            MarkDirty();
            OnPropertyChanged(nameof(SelectedElementDeviceName), oldName);
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
        if (CurrentSheet is not Sheet sheet || SelectedElement is not ElementInstance el) return false;

        string? deviceName = el.DeviceName;
        sheet.Elements.Remove(el);
        MarkDirty();

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
    /// 現在使用可能な自作パーツライブラリ。PartPalette.Library(T-015隠密レビュー指摘#2で
    /// PartPaletteViewModel側に構築を一本化)をそのまま参照する。DiagramRenderer.Render /
    /// LadderCanvas.Draw に渡し、要素配置時（T-016）の PartResolver 解決にも使う。
    /// </summary>
    public PartLibrary PartLibrary { get; }

    /// <summary>SelectedCellの行に既に要素が置かれているか判定する(T-026段階4: 配置行は空き行限定、行挿入はしない)。</summary>
    public bool IsSelectedCellOccupied()
        => SelectedCell is { } pos && CurrentSheet is Sheet sheet && sheet.Elements.Any(el => el.Pos == pos);

    /// <summary>
    /// SelectedCellへ要素を配置する(T-026段階4新配置フロー)。isOr=trueの場合、基準行
    /// (SelectedCellより上にある直近の既存要素行、殿裁定で上方向限定)との間に縦コネクタを
    /// 2本自動生成しOR(並列)接続にする。基準行内では新要素に列位置が最も近い要素を対応先とする。
    /// </summary>
    public void PlaceElementAtSelectedCell(string partId, string deviceName, bool isOr)
    {
        if (SelectedCell is not { } pos || CurrentSheet is not Sheet sheet) return;

        const int cellWidth = 1; // 基本図形(BasicPartTemplates)は全て1セル幅
        var newElement = new ElementInstance
        {
            Pos = pos,
            PartId = partId,
            DeviceName = deviceName.Length > 0 ? deviceName : null,
        };
        sheet.Elements.Add(newElement);
        MarkDirty();

        // 機器表(Document.Devices)への登録(T-036、殿承認2026-07-05)。SelectedElementDeviceNameの
        // setterと同じ流儀: 新規デバイス名(未登録)のみDeviceClass.Otherで追加し、既存デバイス名なら
        // 既存エントリを維持(上書きしない)。デバイス名空欄の場合は機器表を一切操作しない。
        if (deviceName.Length > 0 && !Document.Devices.ByName.ContainsKey(deviceName))
            Document.Devices.ByName[deviceName] = new Device { Name = deviceName, Class = DeviceClass.Other };
        DeviceTable.Refresh();

        if (!isOr) return;

        int? baseRow = sheet.Elements
            .Where(el => el != newElement && el.Pos.Row < pos.Row)
            .Select(el => (int?)el.Pos.Row)
            .DefaultIfEmpty(null)
            .Max();
        if (baseRow is not int br) return;

        var baseElement = sheet.Elements
            .Where(el => el.Pos.Row == br)
            .OrderBy(el => Math.Abs(el.Pos.Column - pos.Column))
            .FirstOrDefault();
        if (baseElement is null) return;

        int leftColumn = Math.Min(baseElement.Pos.Column, pos.Column);
        int rightColumn = Math.Max(baseElement.Pos.Column, pos.Column) + cellWidth;

        // T-044(殿直接要望、隠密事前調査docs/ecad2-t044-presurvey-onmitsu.md): OR自動配線の左縦分岐は、
        // 配置行・基準行の両方で「OR左接続点(leftColumn)と左母線(列0)の間に既存要素が無い」場合のみ
        // 省略する(殿最終裁定=トポロジー等価保証ケース限定)。母線への直結横線
        // (DiagramRenderer.LeftTerminator/NetlistBuilder.LeftRailReachedの既存c.Column>0除外条件)が
        // このケースを自然にカバーするため、省略しても電気的分断は起きない(列0はこの条件の自明な
        // 特殊ケースとして包含される)。いずれかの行に既存要素があれば縦分岐を維持し、その要素を
        // 誤ってバイパスする配線を防ぐ。右(合流側)縦分岐は従来どおり常時生成する。
        //
        // 隠密レビューCONFIRMED(重大、docs/ecad2-t044-review-onmitsu.md所見1): 既存要素(sheet.Elements)
        // だけでなく既存の縦コネクタ(sheet.Connectors)も見る必要がある。同一列で3階層以上のOR配置を
        // 重ねる連鎖ケースでは、基準行(br)が「要素としては空」でも、より上位のOR配置で生成済みの
        // 縦コネクタにより既に母線から分岐された状態(=直結ではない)になっている。この既存コネクタを
        // 見落とすと、末端行が誤って母線へ直結され電気的トポロジーが壊れる(B・Cが同一ネットであるべき
        // ところ、Cだけ母線ネットになってしまう)。判定対象の行がTopRow/BottomRowとして紐づく既存
        // コネクタのうちColumn<=leftColumnのものが無いかも確認する。
        bool NothingBetweenRailAndColumn(int row, int column)
            => !sheet.Elements.Any(el => el.Pos.Row == row && el.Pos.Column < column)
            && !sheet.Connectors.Any(c => (c.TopRow == row || c.BottomRow == row) && c.Column <= column);

        if (!NothingBetweenRailAndColumn(pos.Row, leftColumn) || !NothingBetweenRailAndColumn(br, leftColumn))
            sheet.Connectors.Add(new VerticalConnector { Column = leftColumn, TopRow = br, BottomRow = pos.Row });
        sheet.Connectors.Add(new VerticalConnector { Column = rightColumn, TopRow = br, BottomRow = pos.Row });
    }

    /// <summary>現在のDocumentを指定パスへ.GCAD形式で保存する(T-019)。CurrentFilePathを更新する。
    /// I/O例外はそのまま呼び出し元(View層)へ伝播させ、技術的例外文面をユーザーへ出す変換は
    /// View側の責務とする(隠密調査 docs/ecad2-guiecad-code-survey-onmitsu.md T-024節推奨)。</summary>
    public void SaveToFile(string path)
    {
        Persistence.GcadSerializer.Save(Document, path);
        if (CurrentFilePath != path)
        {
            // 隠密再レビュー指摘: 1引数版のままだと旧値null化が再発するため、他の直接代入経路と
            // 同様に旧値を明示的に渡す。
            string? oldFilePath = CurrentFilePath;
            CurrentFilePath = path;
            OnPropertyChanged(nameof(CurrentFilePath), oldFilePath);
        }
        IsDirty = false;
    }

    /// <summary>指定パスの.GCADファイルを読み込み、現在のDocumentを丸ごと差し替える(T-019)。
    /// I/O・スキーマ不一致例外はそのままView層へ伝播させる(SaveToFileと同方針)。</summary>
    public void LoadFromFile(string path)
    {
        var document = Persistence.GcadSerializer.Load(path);
        ReplaceDocument(document, path);
    }

    /// <summary>新規作成(T-019)。即1シート生成済みのドキュメントへ差し替える(殿裁定2026-07-05、
    /// GX Works3流儀。Sheets=0の空ドキュメントで開始する暫定実装から変更)。呼び出し元(View層)は
    /// 未保存確認(IsDirty)を先に行うこと。</summary>
    public void NewDocument()
    {
        var document = new LadderDocument();
        document.Sheets.Add(new Sheet
        {
            PageNumber = 1,
            Name = "シート1",
            Grid = new GridSpec { Rows = 10, Columns = 20 },
        });
        ReplaceDocument(document, filePath: null);
    }

    /// <summary>Documentを丸ごと差し替え、関連する子ViewModel・選択状態を再同期する
    /// (T-019: 新規/開く共通の単一ゲートウェイ。文書破棄操作の入口を分散させない、
    /// GuiEcadの反省 docs/ecad2-guiecad-code-survey-onmitsu.md T-024節を踏まえる)。</summary>
    private void ReplaceDocument(LadderDocument newDocument, string? filePath)
    {
        var oldDocument = Document;
        var oldFilePath = CurrentFilePath;
        // 隠密再レビュー指摘: _currentSheetIndex(int)/_selectedCell(GridPos?)は値型のため
        // IsEnabledガード無しだと無効時も無条件ボクシングが発生する(finding7と同根)。
        object? oldSheetIndex = TraceLog.IsEnabled ? _currentSheetIndex : null;
        object? oldSelectedCell = TraceLog.IsEnabled ? _selectedCell : null;

        Document = newDocument;
        CurrentFilePath = filePath;
        _currentSheetIndex = 0;
        _selectedCell = null;
        // T-041増分1隠密レビュー指摘(観点2 CONFIRMED#4): 上の_selectedCellはsetterをバイパスする
        // 直接代入のため、SelectedCellのsetterに集約した自動クリア(上記参照)が効かない。旧文書の
        // VerticalConnector参照を持ち越さないよう、ここでも明示的にクリアする。SelectedConnectorは
        // 参照型でTraceLogのボクシング懸念が無いため、setter経由(自己通知)でよい。
        SelectedConnector = null;
        // T-041増分3: 配線分断(WireBreak)も同様に旧文書の参照を持ち越さない。
        SelectedWireBreak = null;
        // T-041増分5: 自由線・接続点も同様に旧文書の参照を持ち越さない。
        SelectedFreeLine = null;
        SelectedConnectionDot = null;
        // T-041増分2隠密レビュー指摘(観点3 CONFIRMED): 記入中(_connectorDraft)も同様に、直接代入
        // (_selectedCell)経由ではSelectedCellのsetterの自動クリアが効かないため、ここでも明示する。
        ClearConnectorDraftIfAny();
        // T-041増分5: 自由線の記入中状態(_freeLineDraft)も同様。
        ClearFreeLineDraftIfAny();
        // 隠密レビューfinding3: 旧値をOnPropertyChangedへ明示的に渡す(SetPropertyバイパスの
        // 直接代入経路でも旧値がnullにならないようにする、殿裁定「安くできる範囲」の範囲内)。
        OnPropertyChanged(nameof(Document), oldDocument);
        OnPropertyChanged(nameof(CurrentFilePath), oldFilePath);
        OnPropertyChanged(nameof(CurrentSheetIndex), oldSheetIndex);
        OnPropertyChanged(nameof(CurrentSheet));
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(SelectedCell), oldSelectedCell);
        OnPropertyChanged(nameof(SelectedCellDisplay));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
        SheetNavigation.ResetSheets();
        DeviceTable.Rebind(newDocument.Devices);
        // 隠密レビュー指摘(#1 CONFIRMED/#2 CONFIRMED軽微/#3 PLAUSIBLE→格上げ): 旧文書に紐づく
        // 状態を明示的にリセットしないと、新規/開く後もDRC結果の誤ジャンプ・沈黙、ステータス
        // メッセージ残留、前文書のツール種別での配置ダイアログ開始が起こりうる。既存setter経由で
        // 設定し(#4の教訓: フィールド直接代入+手動通知の重複管理を増やさない)通知を一本化する。
        StatusMessage = "";
        Tool = ToolState.SelectDefault;
        OutputPanel.ClearResults();
        // 新規/開く直後は未保存の変更が無い状態(IsDirty=false)から始まる。
        IsDirty = false;
    }

    /// <summary>本番用。実MyDocuments配下(PartFolderStore.CreateDefault())を使う。</summary>
    public MainWindowViewModel() : this(PartFolderStore.CreateDefault()) { }

    /// <summary>T-042: テスト等から一時フォルダのPartFolderStoreを注入できるようにするための
    /// コンストラクタ(P-019=App層テストが実MyDocumentsを叩く副作用の解消)。</summary>
    public MainWindowViewModel(PartFolderStore partFolderStore)
    {
        SheetNavigation = new SheetNavigationViewModel(this);
        PartPalette = new PartPaletteViewModel(partFolderStore);
        // T-015隠密レビュー指摘#2: PartPaletteViewModel.Libraryと同一ロジックの重複構築だったため、
        // 構築元(PartPaletteViewModel)へ一本化し、ここでは公開済みのLibraryをそのまま使う。
        PartLibrary = PartPalette.Library;
        DeviceTable = new DeviceTableViewModel(Document.Devices);
        OutputPanel = new OutputPanelViewModel(this);
    }
}
