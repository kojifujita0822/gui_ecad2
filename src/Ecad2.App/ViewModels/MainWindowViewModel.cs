using System.Linq;
using System.Windows.Input;
using Ecad2.App.Commands;
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
            // T-050修正(P-044): RefreshSelectedSheetの2引数化に伴い、変更前の選択シートを渡す。
            // SetPropertyで_currentSheetIndexを更新する前に読み取るのみ(挙動を変える代入・分岐は
            // 追加しない=P-030のsetter粒度モグラ叩き再発防止、家老厳守事項2026-07-10)。
            // T-050往復2周目(隠密CONFIRMED二重発火の解消): クロスカット処理はSetCurrentSheetIndexCoreへ
            // 切り出し、公開セッタは従来どおり「コア + SelectedSheet通知(RefreshSelectedSheet)」の組で
            // 構成する(公開挙動は不変)。DRC出力パネルのジャンプ等、SelectedSheet通知を自前で持たない
            // 外部からの直接代入経路はこの公開セッタを使い、ちょうど1回の通知を得る。一方、コレクション
            // 変更を伴い自前で正しい旧値の通知を撃つAddCommand/DeleteCommandはコアを直接呼び、公開
            // セッタ経由のネスト通知(誤った旧値・二重発火)を避ける。
            var oldSelectedSheet = SheetNavigation.SelectedSheet;
            SetCurrentSheetIndexCore(value);
            SheetNavigation.RefreshSelectedSheet(oldSelectedSheet);
        }
    }

    /// <summary>CurrentSheetIndexの変更に伴うクロスカット処理(CurrentSheet依存プロパティの通知・
    /// SelectedCellクリア)のみを行い、SelectedSheetの変更通知は呼び出し元に委ねる。SelectedSheet通知を
    /// 自前で1回だけ発火する経路(SheetNavigationViewModelのAddCommand/DeleteCommand=コレクション変更後に
    /// index確定するため公開セッタ経由だと二重発火・旧値不整合を起こす、T-050往復2周目/隠密CONFIRMED)
    /// から呼ぶ。公開セッタはこのコアの直後にRefreshSelectedSheetを続けることで従来挙動を保つ。
    ///
    /// T-041増分5隠密レビュー指摘(観点3 CONFIRMED重大、増分1由来の構造的な穴): シート削除
    /// (SheetNavigationViewModel.DeleteCommand)で「非末尾シートを削除、かつそれが現在表示中」
    /// の場合、削除後のindex数値がたまたま削除前と一致するケースがある(Sheets[index]の実体は
    /// 差し替わっているのに、int値としてのCurrentSheetIndexは変化しない)。CurrentSheetIndex
    /// は「値そのもの」ではなく「Document.Sheetsへの添字(キー)」に過ぎず、キーの数値が同じ
    /// でも参照先の実体(Sheets[index])が入れ替わりうる非対称性を持つ。よってプロパティ
    /// 自身の変更通知(OnPropertyChanged(nameof(CurrentSheet)))・クロスカット的クリア
    /// (前シートのSelectedCell・全選択状態/記入中状態の連鎖クリア、左パレット選択ハイライト
    /// 同期)は共に値変化の有無に関わらず常時実行する(SetPropertyの戻り値でガードしない)。
    /// T-041増分5往復3周目: 改名の遅延再選択がCurrentSheetIndexへの代入を経由し記入中ドラフトを
    /// 破棄する副作用(所見L)は、RenameCommand側(RefreshSelectedSheetのみ呼ぶ形)で対処し、本処理は
    /// 「常時無条件」のままとする(二重のモグラ叩きを避ける)。</summary>
    internal void SetCurrentSheetIndexCore(int value)
    {
        SetProperty(ref _currentSheetIndex, value);
        NotifyCurrentSheetDependentPropertiesChanged();
        SelectedCell = null;
    }

    /// <summary>現在表示中のシート。Document.Sheets[CurrentSheetIndex] の読み取り専用ビュー。
    /// Document.Sheets.Count==0(起動直後の濃紺スタート、殿裁定2026-07-05)の間はnull。</summary>
    public Sheet? CurrentSheet
        => CurrentSheetIndex >= 0 && CurrentSheetIndex < Document.Sheets.Count ? Document.Sheets[CurrentSheetIndex] : null;

    /// <summary>現在シートが主回路(動力回路)か(T-047、手動配線F9/sF9/F10系ボタンの活性制御に使う)。
    /// CurrentSheetがnull(HasProject=false)の間は常にfalse(ボタン非活性のデフォルトに倒す)。</summary>
    public bool IsMainCircuitSheet => CurrentSheet?.MainCircuit == true;

    /// <summary>現在シートが制御回路(ラダー)か(T-047、<see cref="IsMainCircuitSheet"/>の対)。
    /// CurrentSheetがnullの間は常にfalse。</summary>
    public bool IsControlCircuitSheet => CurrentSheet is Sheet sheet && !sheet.MainCircuit;

    /// <summary>CurrentSheet・およびそれに連動する活性制御プロパティ(T-047)の変更通知をまとめて
    /// 発火する。CurrentSheetIndexのsetter・NotifyCurrentSheetChanged・ReplaceDocumentの3箇所が
    /// CurrentSheetの実体を変えうる経路であり(T-041増分5隠密レビュー指摘、CurrentSheetIndexの
    /// SetProperty早期return再発トラップ)、いずれも無条件でこのメソッドを呼ぶ。</summary>
    private void NotifyCurrentSheetDependentPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentSheet));
        OnPropertyChanged(nameof(IsMainCircuitSheet));
        OnPropertyChanged(nameof(IsControlCircuitSheet));
    }

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
    public void NotifyCurrentSheetChanged() => NotifyCurrentSheetDependentPropertiesChanged();

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

    private bool _selectedEndpointIsStart = true;

    /// <summary>選択中の線分プリミティブ(VerticalConnector/FreeLine)に対し、Tab+Shift+矢印キーで
    /// 操作する対象端点(T-041増分7、殿裁定P-033=案2)。true=始点(VerticalConnectorの
    /// TopRow/FreeLineの(X1Mm,Y1Mm))、false=終点(BottomRow/(X2Mm,Y2Mm))。新しい線分プリミティブが
    /// 選択されるたび(SelectedConnector/SelectedFreeLineのsetter)に既定値(始点)へリセットされる。
    /// 点系(WireBreak/ConnectionDot)には端点概念が無いため関与しない。</summary>
    public bool SelectedEndpointIsStart
    {
        get => _selectedEndpointIsStart;
        set
        {
            // T-041増分7実機確認で発覚(往復1周目): SetPropertyは自身(SelectedEndpointIsStart)の
            // 変更通知のみ発火し、派生表示プロパティ(SelectedEndpointDisplay)へは伝播しない
            // (WPFのCallerMemberName既定の仕組み上当然だが、ここでは明示発火が必要だった)。
            // 忘れるとToggleSelectedEndpoint()は内部的に正しく動作するのにステータスバー表示だけ
            // 更新されない、という気づきにくい表示バグになる(実機で「Tabが効かない」ように見えた)。
            if (SetProperty(ref _selectedEndpointIsStart, value))
                OnPropertyChanged(nameof(SelectedEndpointDisplay));
        }
    }

    /// <summary>ステータスバー表示用(T-041増分7)。線分プリミティブ選択中のみ意味を持つ。</summary>
    public string SelectedEndpointDisplay => SelectedEndpointIsStart ? "始点" : "終点";

    /// <summary>ステータスバーの操作対象端点表示の可視性判定用(T-041増分7)。線分プリミティブ
    /// (VerticalConnector/FreeLine)選択中のみtrue。SelectedConnector/SelectedFreeLineの
    /// setterが変更を明示通知する。</summary>
    public bool HasSelectedLinePrimitive => SelectedConnector is not null || SelectedFreeLine is not null;

    /// <summary>Tabキーで操作対象端点をトグルする(T-041増分7、殿裁定P-033=案2)。選択中の線分
    /// プリミティブが無ければ何もしない(呼び出し元がTool.Mode/選択有無を判定済みの前提でも、
    /// 二重防御としてここでも確認する)。</summary>
    public void ToggleSelectedEndpoint()
    {
        if (SelectedConnector is null && SelectedFreeLine is null) return;
        SelectedEndpointIsStart = !SelectedEndpointIsStart;
    }

    private VerticalConnector? _selectedConnector;

    /// <summary>
    /// 現在選択中の縦コネクタ(T-041増分1: 配線プリミティブの選択、GridPos単位のSelectedCellとは
    /// 別枠で保持する)。SelectedCellとは排他——SelectedCellのsetterが値変化の有無に関わらず常時
    /// このプロパティをクリアするため(隠密レビュー指摘、上記SelectedCell参照)、縦コネクタを
    /// クリック選択する経路はSelectedCell=null→SelectedConnector=connectorの順で呼ぶ必要がある
    /// (逆順だとSelectedCellのクリアが直後に打ち消してしまう)。単一選択のみ
    /// (殿裁定2026-07-07)。
    /// </summary>
    public VerticalConnector? SelectedConnector
    {
        get => _selectedConnector;
        set
        {
            // T-041増分7: 新規選択のたびに操作対象端点を既定(始点)へリセットする(値変化の有無に
            // 関わらず無条件、SelectedCellのsetterと同じ設計原則)。
            SelectedEndpointIsStart = true;
            // T-041増分7隠密レビュー所見A対応: SelectedCellのsetter・CurrentSheetIndexのsetter
            // (SelectedCell=null経由)・ReplaceDocumentはいずれも最終的にこのsetterを通じて
            // SelectedConnector=nullを代入する(Delete経由のDeleteSelectedConnectorも同様)。
            // ドラッグ中の一時状態(_draggingConnector)はこれら3経路の外に置かれていたため、削除
            // 済み/切替後/破棄済みの実体を後続のMouseMove/MouseUpが無意味に書き換え続ける穴が
            // あった。このsetter自体を「ドラッグ状態をも強制クリアする唯一の入口」にすることで、
            // 個別経路への後追い対応(モグラ叩き)を避ける(_connectorDraftのクリア集約と同じ設計)。
            ForceCancelDragConnectorIfAny();
            SetProperty(ref _selectedConnector, value);
            OnPropertyChanged(nameof(HasSelectedLinePrimitive));
        }
    }

    // T-041増分7: 縦コネクタのドラッグ(本体移動/端点リサイズ)の一時状態。記入中ドラフト
    // (_connectorDraft)と同様、確定前はモデル(sheet.Connectors内の実体)を直接書き換えつつ
    // 開始時スナップショットを保持し、Escでスナップショットへ復元できるようにする。
    // ドラッグ確定(ConfirmDragConnector)・キーボード平行移動(MoveSelectedConnector)の両経路とも
    // 「モデルへ実際に反映し、値が変化した場合のみMarkDirty()する」という同じ結果に収束する。
    private VerticalConnector? _draggingConnector;
    private bool _draggingConnectorIsEndpoint;
    private bool _draggingConnectorIsTop;
    private int _dragConnectorOrigTopRow;
    private int _dragConnectorOrigBottomRow;
    private int _dragConnectorStartRow;
    // P-039(殿裁定): 本体移動時に列位置(Column)も動かせるようにする。WireBreakのBoundaryと同型
    // (0.5刻み境界、単一値のためmin>maxガードは不要)。端点リサイズ時は列を変更しない(殿裁定
    // 「VerticalConnectorは常に縦線のためLeft/Rightの端点伸縮は意味を持たず未対応」と同じ理由)。
    private double _dragConnectorOrigColumn;
    private double _dragConnectorStartColumn;

    /// <summary>縦コネクタをドラッグ中か(View側がMouseMove/Escの処理要否を判定するのに使う)。</summary>
    public bool IsDraggingConnector => _draggingConnector is not null;

    /// <summary>ドラッグ中のいずれかの型(Connector/WireBreak/FreeLine/ConnectionDot)を外部要因
    /// (Delete・シート切替・ドキュメント差し替え等、各SelectedXxxのsetterを経由する全経路)により
    /// 強制的にキャンセルする骨格(T-045増分D、隠密所見「ForceCancelDrag*IfAny4箇所(3行同一)の
    /// 共通化」対応、T-041増分7隠密レビュー所見A対応の一般化)。CancelDragXxx()と同じ開始時位置への
    /// 復元を必ず行う(所見Y対応: 復元せず_draggingXxxをnullにするだけだと、シート切替のように
    /// 対象が生きたまま残る経路でUpdateDragXxx適用済みの半端な位置がMarkDirty()もされず黙って
    /// 確定してしまう——本ヘルパーはisActive/cancel/notifyを型ごとに明示的に渡す構造にすることで、
    /// 新しい型を追加する際にcancel呼び出しの書き忘れがレビューで見えやすくなる)。notifyで
    /// IsDraggingXxxの変更をView側へ明示通知する(MainWindow.xaml.csのViewModel_PropertyChangedが
    /// これを受けてキャプチャ解放・Viewローカル一時フラグのリセット等の後始末を行う)。</summary>
    private void ForceCancelIfAny(Func<bool> isActive, Action cancel, Action notify)
    {
        if (!isActive()) return;
        cancel();
        notify();
    }

    private void ForceCancelDragConnectorIfAny()
        => ForceCancelIfAny(
            () => _draggingConnector is not null,
            CancelDragConnector,
            () => OnPropertyChanged(nameof(IsDraggingConnector)));

    /// <summary>縦コネクタのドラッグを開始する(T-041増分7)。isEndpoint=falseなら本体移動、
    /// trueならisTopで指定した端点のみのリサイズ。startRowはドラッグ開始時のマウス位置(行)。
    /// startColumnはドラッグ開始時のマウス位置(列境界0.5刻み、P-039対応)。</summary>
    public void BeginDragConnector(VerticalConnector connector, bool isEndpoint, bool isTop, int startRow, double startColumn)
    {
        _draggingConnector = connector;
        _draggingConnectorIsEndpoint = isEndpoint;
        _draggingConnectorIsTop = isTop;
        _dragConnectorOrigTopRow = connector.TopRow;
        _dragConnectorOrigBottomRow = connector.BottomRow;
        _dragConnectorStartRow = startRow;
        _dragConnectorOrigColumn = connector.Column;
        _dragConnectorStartColumn = startColumn;
    }

    /// <summary>ドラッグ中のマウス位置(現在行・列境界0.5刻み)に応じて縦コネクタの位置を更新する
    /// (T-041増分7、列位置はP-039対応)。本体移動はTop/BottomRowの間隔(span)を保ったままGrid範囲内に
    /// クランプする(独立にクランプすると片方が先に端へ達した際に間隔が歪むため、delta自体を範囲内に
    /// 丸めてから両方へ同時適用する)。列位置はWireBreak.Boundaryと同型の単一値のため独立クランプで
    /// よい(min>maxは起こりえない)。端点リサイズはTop&lt;Bottom(ゼロ長禁止、ConfirmConnectorDraftの
    /// 記入時不変条件と同じ)を保ち、列位置は変更しない(端点は上下伸縮のみ)。</summary>
    public void UpdateDragConnector(int currentRow, double currentColumn)
    {
        if (_draggingConnector is not VerticalConnector c || CurrentSheet is not Sheet sheet) return;
        int deltaRow = currentRow - _dragConnectorStartRow;
        if (_draggingConnectorIsEndpoint)
        {
            // T-041増分7隠密レビュー所見B対応: 外部データ(手編集ファイル・旧バージョン由来等)で
            // TopRow>=BottomRowという不変条件違反のコネクタが読み込まれていた場合、Math.Clampの
            // 上限・下限が逆転しArgumentExceptionが未処理のまま投げられクラッシュする
            // (min>maxをここで先に検出し、直せない状態なら何もせず抜ける)。ConfirmConnectorDraftの
            // topRow==bottomRowならreturn falseと同じ趣旨の二重防御。
            if (_draggingConnectorIsTop)
            {
                int maxTop = c.BottomRow - 1;
                if (maxTop < 0) return;
                c.TopRow = Math.Clamp(_dragConnectorOrigTopRow + deltaRow, 0, maxTop);
            }
            else
            {
                int minBottom = c.TopRow + 1;
                if (minBottom > sheet.Grid.Rows - 1) return;
                c.BottomRow = Math.Clamp(_dragConnectorOrigBottomRow + deltaRow, minBottom, sheet.Grid.Rows - 1);
            }
        }
        else
        {
            int minDelta = -_dragConnectorOrigTopRow;
            int maxDelta = sheet.Grid.Rows - 1 - _dragConnectorOrigBottomRow;
            if (minDelta > maxDelta) return;
            int clamped = Math.Clamp(deltaRow, minDelta, maxDelta);
            c.TopRow = _dragConnectorOrigTopRow + clamped;
            c.BottomRow = _dragConnectorOrigBottomRow + clamped;

            // P-039(殿裁定): 本体移動時は列位置(Column)もマウス追従させる。WireBreak.Boundaryと
            // 同型の単一値のためmin>maxガードは不要(0<=sheet.Grid.Columnsは常に真)。
            double deltaColumn = currentColumn - _dragConnectorStartColumn;
            c.Column = Math.Clamp(_dragConnectorOrigColumn + deltaColumn, 0, sheet.Grid.Columns);
        }
    }

    /// <summary>縦コネクタのドラッグを確定する(T-041増分7)。開始時から実際に値が変化していれば
    /// MarkDirty()する(ドラッグ中は毎フレーム呼ばず、確定時の1回に集約)。</summary>
    public void ConfirmDragConnector()
        => ConfirmDrag(ref _draggingConnector,
            c => c.TopRow != _dragConnectorOrigTopRow || c.BottomRow != _dragConnectorOrigBottomRow
                || c.Column != _dragConnectorOrigColumn);

    /// <summary>縦コネクタのドラッグをキャンセルし、開始時の位置へ復元する(Esc、T-041増分7)。</summary>
    public void CancelDragConnector()
        => CancelDrag(ref _draggingConnector, c =>
        {
            c.TopRow = _dragConnectorOrigTopRow;
            c.BottomRow = _dragConnectorOrigBottomRow;
            c.Column = _dragConnectorOrigColumn;
        });

    /// <summary>選択中の縦コネクタを行方向に矢印キー1回分(delta=±1)平行移動する(T-041増分7、
    /// キーボード等価操作)。UpdateDragConnectorの本体移動と同じ「間隔を保ったままクランプ」方式。
    /// 実際に動けた場合のみMarkDirty()し、ドラッグ確定と同じ結果に収束する。</summary>
    public bool MoveSelectedConnector(int deltaRow)
    {
        if (CurrentSheet is not Sheet sheet || SelectedConnector is not VerticalConnector c) return false;
        int minDelta = -c.TopRow;
        int maxDelta = sheet.Grid.Rows - 1 - c.BottomRow;
        int clamped = Math.Clamp(deltaRow, minDelta, maxDelta);
        if (clamped == 0) return false;
        c.TopRow += clamped;
        c.BottomRow += clamped;
        MarkDirty();
        return true;
    }

    /// <summary>選択中の縦コネクタを列方向に矢印キー1回分(delta=±1)平行移動する(T-041増分7、
    /// キーボード等価操作、Key.Left/Right)。MoveSelectedConnectorと対の列方向版。</summary>
    public bool MoveSelectedConnectorColumn(double delta)
    {
        if (CurrentSheet is not Sheet sheet || SelectedConnector is not VerticalConnector c) return false;
        double newColumn = Math.Clamp(c.Column + delta, 0, sheet.Grid.Columns);
        if (newColumn == c.Column) return false;
        c.Column = newColumn;
        MarkDirty();
        return true;
    }

    /// <summary>選択中の縦コネクタの操作対象端点(SelectedEndpointIsStart、Tab+Shift+矢印、
    /// T-041増分7殿裁定P-033=案2)を矢印キー1回分(delta=±1)伸縮する。始点=TopRow、終点=BottomRow
    /// とみなす(VerticalConnectorは常に縦線のためShift+Up/Downのみ呼ばれる想定、Left/Rightは
    /// 呼び出し元(MainWindow.xaml.cs)で無視される)。Top&lt;Bottom(ゼロ長禁止、ConfirmConnectorDraftの
    /// 記入時不変条件と同じ)・Grid範囲でクランプする。</summary>
    public bool ResizeSelectedConnectorEndpoint(int delta)
    {
        if (CurrentSheet is not Sheet sheet || SelectedConnector is not VerticalConnector c) return false;
        // T-041増分7隠密レビュー所見B対応: UpdateDragConnectorと同じ理由でmin>maxの事前ガードが要る。
        if (SelectedEndpointIsStart)
        {
            int maxTop = c.BottomRow - 1;
            if (maxTop < 0) return false;
            int newTop = Math.Clamp(c.TopRow + delta, 0, maxTop);
            if (newTop == c.TopRow) return false;
            c.TopRow = newTop;
        }
        else
        {
            int minBottom = c.TopRow + 1;
            if (minBottom > sheet.Grid.Rows - 1) return false;
            int newBottom = Math.Clamp(c.BottomRow + delta, minBottom, sheet.Grid.Rows - 1);
            if (newBottom == c.BottomRow) return false;
            c.BottomRow = newBottom;
        }
        MarkDirty();
        return true;
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
        set
        {
            // T-041増分7隠密レビュー所見A対応(SelectedConnectorと同型): Delete・シート切替・
            // ドキュメント差し替えいずれも最終的にこのsetterを経由してSelectedWireBreak=nullを
            // 代入するため、ドラッグ中の一時状態(_draggingWireBreak)もここで強制クリアする。
            ForceCancelDragWireBreakIfAny();
            SetProperty(ref _selectedWireBreak, value);
        }
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

    // T-041増分7横展開: 配線分断(WireBreak)のドラッグ移動の一時状態。点系は本体移動のみ(端点概念が
    // 無いためVerticalConnectorのisEndpoint/isTopに相当する分岐は不要)。行・列(Boundary)とも
    // ドラッグ開始位置からの相対差分で動かす(点をつまんだ位置がいきなり点の中心へワープしないよう、
    // VerticalConnectorの本体移動と同じ相対差分方式に揃える)。
    private WireBreak? _draggingWireBreak;
    private int _dragWireBreakOrigRow;
    private double _dragWireBreakOrigBoundary;
    private int _dragWireBreakStartRow;
    private double _dragWireBreakStartBoundary;

    /// <summary>配線分断をドラッグ中か。</summary>
    public bool IsDraggingWireBreak => _draggingWireBreak is not null;

    /// <summary>ドラッグ中の配線分断を外部要因により強制的にキャンセルする(T-041増分7隠密レビュー
    /// 所見A対応、ForceCancelDragConnectorIfAnyと同型)。所見Y対応でCancelDragWireBreak()と同じ
    /// 開始時位置への復元を行う。</summary>
    private void ForceCancelDragWireBreakIfAny()
        => ForceCancelIfAny(
            () => _draggingWireBreak is not null,
            CancelDragWireBreak,
            () => OnPropertyChanged(nameof(IsDraggingWireBreak)));

    /// <summary>配線分断のドラッグを開始する(T-041増分7)。startRow/startBoundaryはドラッグ開始時の
    /// マウス位置(行・列境界0.5刻み)。</summary>
    public void BeginDragWireBreak(WireBreak wireBreak, int startRow, double startBoundary)
    {
        _draggingWireBreak = wireBreak;
        _dragWireBreakOrigRow = wireBreak.Row;
        _dragWireBreakOrigBoundary = wireBreak.Boundary;
        _dragWireBreakStartRow = startRow;
        _dragWireBreakStartBoundary = startBoundary;
    }

    /// <summary>ドラッグ中のマウス位置(現在行・列境界)に応じて配線分断の位置を更新する(T-041増分7)。
    /// Grid範囲内にクランプする。</summary>
    public void UpdateDragWireBreak(int currentRow, double currentBoundary)
    {
        if (_draggingWireBreak is not WireBreak b || CurrentSheet is not Sheet sheet) return;
        int deltaRow = currentRow - _dragWireBreakStartRow;
        double deltaBoundary = currentBoundary - _dragWireBreakStartBoundary;
        b.Row = Math.Clamp(_dragWireBreakOrigRow + deltaRow, 0, sheet.Grid.Rows - 1);
        b.Boundary = Math.Clamp(_dragWireBreakOrigBoundary + deltaBoundary, 0, sheet.Grid.Columns);
    }

    /// <summary>配線分断のドラッグを確定する(T-041増分7)。開始時から実際に値が変化していれば
    /// MarkDirty()する。</summary>
    public void ConfirmDragWireBreak()
        => ConfirmDrag(ref _draggingWireBreak,
            b => b.Row != _dragWireBreakOrigRow || b.Boundary != _dragWireBreakOrigBoundary);

    /// <summary>配線分断のドラッグをキャンセルし、開始時の位置へ復元する(Esc、T-041増分7)。</summary>
    public void CancelDragWireBreak()
        => CancelDrag(ref _draggingWireBreak, b =>
        {
            b.Row = _dragWireBreakOrigRow;
            b.Boundary = _dragWireBreakOrigBoundary;
        });

    /// <summary>選択中の配線分断を矢印キー1回分(deltaRow/deltaBoundary、いずれか一方は0)平行移動する
    /// (T-041増分7、キーボード等価操作)。点系は本体移動のみ。実際に動けた場合のみMarkDirty()する。</summary>
    public bool MoveSelectedWireBreak(int deltaRow, double deltaBoundary)
    {
        if (CurrentSheet is not Sheet sheet || SelectedWireBreak is not WireBreak b) return false;
        int newRow = Math.Clamp(b.Row + deltaRow, 0, sheet.Grid.Rows - 1);
        double newBoundary = Math.Clamp(b.Boundary + deltaBoundary, 0, sheet.Grid.Columns);
        if (newRow == b.Row && newBoundary == b.Boundary) return false;
        b.Row = newRow;
        b.Boundary = newBoundary;
        MarkDirty();
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
        set
        {
            // T-041増分7: SelectedConnectorのsetterと同じ理由で操作対象端点を既定(始点)へリセットする。
            SelectedEndpointIsStart = true;
            // T-041増分7隠密レビュー所見A対応(SelectedConnectorと同型): ドラッグ中の一時状態
            // (_draggingFreeLine)もこのsetter経由で強制クリアする。
            ForceCancelDragFreeLineIfAny();
            SetProperty(ref _selectedFreeLine, value);
            OnPropertyChanged(nameof(HasSelectedLinePrimitive));
        }
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

    // T-041増分7横展開: 自由線(FreeLine)のドラッグ(本体移動/端点リサイズ)の一時状態。mm実座標系の
    // ためVerticalConnectorと異なりdouble精度で扱う。記入時(AdjustFreeLineDraft、原案4節「水平・
    // 垂直のみ」)と同じ制約を端点リサイズにも適用する: 水平線の端点はX方向のみ、垂直線の端点は
    // Y方向のみ動かし、線が斜めに崩れないようにする(本体移動は両端点を同量シフトするため制約不要)。
    private FreeLine? _draggingFreeLine;
    private bool _draggingFreeLineIsEndpoint;
    private bool _draggingFreeLineIsStart;
    private double _dragFreeLineOrigX1, _dragFreeLineOrigY1, _dragFreeLineOrigX2, _dragFreeLineOrigY2;
    private double _dragFreeLineStartXMm, _dragFreeLineStartYMm;
    // T-041増分7隠密レビュー所見AA対応: ページ境界(mm)。ViewModelは幾何を知らない設計のため
    // View側(呼び出し元)がsheet.Grid.Columns/Rows×CellMmを計算して渡す。
    private double _dragFreeLineMaxXMm, _dragFreeLineMaxYMm;

    // ConfirmFreeLineDraftのStepCount==0禁止(ゼロ長禁止)と同じ趣旨。記入時と異なり連続値のため
    // 「0との比較」ではなく最小長さで判定する。
    private const double FreeLineMinLengthMm = 1.0;

    /// <summary>自由線をドラッグ中か。</summary>
    public bool IsDraggingFreeLine => _draggingFreeLine is not null;

    /// <summary>ドラッグ中の自由線を外部要因により強制的にキャンセルする(T-041増分7隠密レビュー
    /// 所見A対応、ForceCancelDragConnectorIfAnyと同型)。所見Y対応でCancelDragFreeLine()と同じ
    /// 開始時位置への復元を行う。</summary>
    private void ForceCancelDragFreeLineIfAny()
        => ForceCancelIfAny(
            () => _draggingFreeLine is not null,
            CancelDragFreeLine,
            () => OnPropertyChanged(nameof(IsDraggingFreeLine)));

    /// <summary>自由線のドラッグを開始する(T-041増分7)。isEndpoint=falseなら本体移動、trueなら
    /// isStartで指定した端点(始点/終点)のみのリサイズ。startXMm/startYMmはドラッグ開始時のマウス
    /// 位置(mm実座標)。maxXMm/maxYMmはページ境界(T-041増分7隠密レビュー所見AA対応、呼び出し元が
    /// sheet.Grid.Columns/Rows×CellMmを計算して渡す)。</summary>
    public void BeginDragFreeLine(FreeLine line, bool isEndpoint, bool isStart, double startXMm, double startYMm, double maxXMm, double maxYMm)
    {
        _draggingFreeLine = line;
        _draggingFreeLineIsEndpoint = isEndpoint;
        _draggingFreeLineIsStart = isStart;
        _dragFreeLineOrigX1 = line.X1Mm; _dragFreeLineOrigY1 = line.Y1Mm;
        _dragFreeLineOrigX2 = line.X2Mm; _dragFreeLineOrigY2 = line.Y2Mm;
        _dragFreeLineStartXMm = startXMm; _dragFreeLineStartYMm = startYMm;
        _dragFreeLineMaxXMm = maxXMm; _dragFreeLineMaxYMm = maxYMm;
    }

    /// <summary>ドラッグ中のマウス位置(mm実座標)に応じて自由線の位置を更新する(T-041増分7)。
    /// 端点リサイズは水平・垂直の向きを保ちゼロ長を禁止する(クラス冒頭コメント参照)。T-041増分7
    /// 隠密レビュー所見AA対応: グリッド・ページ境界(0〜_dragFreeLineMax*Mm)へクランプする
    /// (Undo機能が無いため、境界外へ飛んで見失うことを防ぐ)。T-041増分7隠密レビュー所見AB対応:
    /// 線の長さがページ境界を超える場合(min>max)はMath.Clampが例外を投げるため、
    /// VerticalConnectorのUpdateDragConnectorと同様に先にガードしその方向は動かさない。</summary>
    public void UpdateDragFreeLine(double currentXMm, double currentYMm)
    {
        if (_draggingFreeLine is not FreeLine line) return;
        double deltaX = currentXMm - _dragFreeLineStartXMm;
        double deltaY = currentYMm - _dragFreeLineStartYMm;

        if (!_draggingFreeLineIsEndpoint)
        {
            double minDeltaX = -Math.Min(_dragFreeLineOrigX1, _dragFreeLineOrigX2);
            double maxDeltaX = _dragFreeLineMaxXMm - Math.Max(_dragFreeLineOrigX1, _dragFreeLineOrigX2);
            double clampedDeltaX = minDeltaX > maxDeltaX ? 0 : Math.Clamp(deltaX, minDeltaX, maxDeltaX);
            double minDeltaY = -Math.Min(_dragFreeLineOrigY1, _dragFreeLineOrigY2);
            double maxDeltaY = _dragFreeLineMaxYMm - Math.Max(_dragFreeLineOrigY1, _dragFreeLineOrigY2);
            double clampedDeltaY = minDeltaY > maxDeltaY ? 0 : Math.Clamp(deltaY, minDeltaY, maxDeltaY);
            line.X1Mm = _dragFreeLineOrigX1 + clampedDeltaX;
            line.Y1Mm = _dragFreeLineOrigY1 + clampedDeltaY;
            line.X2Mm = _dragFreeLineOrigX2 + clampedDeltaX;
            line.Y2Mm = _dragFreeLineOrigY2 + clampedDeltaY;
            return;
        }

        bool isHorizontal = Math.Abs(_dragFreeLineOrigY1 - _dragFreeLineOrigY2) < 0.001;
        if (_draggingFreeLineIsStart)
        {
            if (isHorizontal)
            {
                double newX1 = Math.Clamp(_dragFreeLineOrigX1 + deltaX, 0, _dragFreeLineMaxXMm);
                if (Math.Abs(line.X2Mm - newX1) >= FreeLineMinLengthMm) line.X1Mm = newX1;
            }
            else
            {
                double newY1 = Math.Clamp(_dragFreeLineOrigY1 + deltaY, 0, _dragFreeLineMaxYMm);
                if (Math.Abs(line.Y2Mm - newY1) >= FreeLineMinLengthMm) line.Y1Mm = newY1;
            }
        }
        else
        {
            if (isHorizontal)
            {
                double newX2 = Math.Clamp(_dragFreeLineOrigX2 + deltaX, 0, _dragFreeLineMaxXMm);
                if (Math.Abs(newX2 - line.X1Mm) >= FreeLineMinLengthMm) line.X2Mm = newX2;
            }
            else
            {
                double newY2 = Math.Clamp(_dragFreeLineOrigY2 + deltaY, 0, _dragFreeLineMaxYMm);
                if (Math.Abs(newY2 - line.Y1Mm) >= FreeLineMinLengthMm) line.Y2Mm = newY2;
            }
        }
    }

    /// <summary>自由線のドラッグを確定する(T-041増分7)。開始時から実際に値が変化していれば
    /// MarkDirty()する。</summary>
    public void ConfirmDragFreeLine()
        => ConfirmDrag(ref _draggingFreeLine,
            line => line.X1Mm != _dragFreeLineOrigX1 || line.Y1Mm != _dragFreeLineOrigY1 ||
                    line.X2Mm != _dragFreeLineOrigX2 || line.Y2Mm != _dragFreeLineOrigY2);

    /// <summary>自由線のドラッグをキャンセルし、開始時の位置へ復元する(Esc、T-041増分7)。</summary>
    public void CancelDragFreeLine()
        => CancelDrag(ref _draggingFreeLine, line =>
        {
            line.X1Mm = _dragFreeLineOrigX1; line.Y1Mm = _dragFreeLineOrigY1;
            line.X2Mm = _dragFreeLineOrigX2; line.Y2Mm = _dragFreeLineOrigY2;
        });

    /// <summary>選択中の自由線を矢印キー1回分(deltaXMm/deltaYMm、いずれか一方は0)平行移動する
    /// (T-041増分7、キーボード等価操作)。maxXMm/maxYMmはページ境界(T-041増分7隠密レビュー所見AA
    /// 対応、呼び出し元がsheet.Grid.Columns/Rows×CellMmを計算して渡す)。T-041増分7隠密レビュー
    /// 所見AB対応: 線の長さがページ境界を超える場合(min>max)はその方向を動かさない
    /// (UpdateDragFreeLineと同じガード)。実際に動けた場合のみMarkDirty()する。</summary>
    public bool MoveSelectedFreeLine(double deltaXMm, double deltaYMm, double maxXMm, double maxYMm)
    {
        if (SelectedFreeLine is not FreeLine line) return false;
        double minDeltaX = -Math.Min(line.X1Mm, line.X2Mm);
        double maxDeltaX = maxXMm - Math.Max(line.X1Mm, line.X2Mm);
        double clampedDeltaX = minDeltaX > maxDeltaX ? 0 : Math.Clamp(deltaXMm, minDeltaX, maxDeltaX);
        double minDeltaY = -Math.Min(line.Y1Mm, line.Y2Mm);
        double maxDeltaY = maxYMm - Math.Max(line.Y1Mm, line.Y2Mm);
        double clampedDeltaY = minDeltaY > maxDeltaY ? 0 : Math.Clamp(deltaYMm, minDeltaY, maxDeltaY);
        if (clampedDeltaX == 0 && clampedDeltaY == 0) return false;
        line.X1Mm += clampedDeltaX; line.Y1Mm += clampedDeltaY;
        line.X2Mm += clampedDeltaX; line.Y2Mm += clampedDeltaY;
        MarkDirty();
        return true;
    }

    /// <summary>選択中の自由線の操作対象端点(SelectedEndpointIsStart、Tab+Shift+矢印、
    /// T-041増分7殿裁定P-033=案2)を矢印キー1回分伸縮する。水平線はdeltaXMmのみ、垂直線は
    /// deltaYMmのみ意味を持つ(呼び出し元(MainWindow.xaml.cs)が線の向きに応じたキーのみ渡す)。
    /// maxXMm/maxYMmはページ境界(T-041増分7隠密レビュー所見AC対応、ドラッグ版UpdateDragFreeLine
    /// と対称にMath.Clamp(…, 0, maxMm)でクランプする。0が下限のためmin>maxにはならず所見ABの
    /// ガードは不要)。ゼロ長になる場合は変更しない。T-041増分7隠密レビュー所見Z対応: 呼び出し元は
    /// 線の向きを問わずKey.Up/Down/Left/Rightすべてを無条件で渡すため、線の向きと逆軸のキー
    /// (常にdelta=0)や境界に達し変化しないキーでは実際には座標が変化しない。この場合はゼロ長
    /// ガードを素通りして偽陽性のMarkDirty()に至る前に「変化なし」を検出してfalseを返す
    /// (MoveSelectedWireBreakと同じ趣旨)。</summary>
    public bool ResizeSelectedFreeLineEndpoint(double deltaXMm, double deltaYMm, double maxXMm, double maxYMm)
    {
        if (SelectedFreeLine is not FreeLine line) return false;
        bool isHorizontal = Math.Abs(line.Y1Mm - line.Y2Mm) < 0.001;
        if (SelectedEndpointIsStart)
        {
            if (isHorizontal)
            {
                double newX1 = Math.Clamp(line.X1Mm + deltaXMm, 0, maxXMm);
                if (newX1 == line.X1Mm) return false;
                if (Math.Abs(line.X2Mm - newX1) < FreeLineMinLengthMm) return false;
                line.X1Mm = newX1;
            }
            else
            {
                double newY1 = Math.Clamp(line.Y1Mm + deltaYMm, 0, maxYMm);
                if (newY1 == line.Y1Mm) return false;
                if (Math.Abs(line.Y2Mm - newY1) < FreeLineMinLengthMm) return false;
                line.Y1Mm = newY1;
            }
        }
        else
        {
            if (isHorizontal)
            {
                double newX2 = Math.Clamp(line.X2Mm + deltaXMm, 0, maxXMm);
                if (newX2 == line.X2Mm) return false;
                if (Math.Abs(newX2 - line.X1Mm) < FreeLineMinLengthMm) return false;
                line.X2Mm = newX2;
            }
            else
            {
                double newY2 = Math.Clamp(line.Y2Mm + deltaYMm, 0, maxYMm);
                if (newY2 == line.Y2Mm) return false;
                if (Math.Abs(newY2 - line.Y1Mm) < FreeLineMinLengthMm) return false;
                line.Y2Mm = newY2;
            }
        }
        MarkDirty();
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
        set
        {
            // T-041増分7横展開(所見A対応、SelectedWireBreakと同型): Delete・シート切替・
            // 文書差し替えいずれも最終的にこのsetterを経由してSelectedConnectionDot=nullを
            // 代入するため、ドラッグ中の一時状態(_draggingConnectionDot)もここで強制クリアする。
            ForceCancelDragConnectionDotIfAny();
            SetProperty(ref _selectedConnectionDot, value);
        }
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

    // T-041増分7横展開: 接続点(ConnectionDot)のドラッグ移動の一時状態。WireBreakと同型(点系・
    // 本体移動のみ・相対差分方式)だが、mm実座標系のためFreeLineと同様double精度で扱う。
    private ConnectionDot? _draggingConnectionDot;
    private double _dragConnectionDotOrigXMm, _dragConnectionDotOrigYMm;
    private double _dragConnectionDotStartXMm, _dragConnectionDotStartYMm;
    // T-041増分7隠密レビュー所見AD対応: ページ境界(mm)。FreeLineと同じmm実座標系・Undo機能無しの
    // 条件を共有するため境界クランプを拡大適用する(呼び出し元がsheet.Grid.Columns/Rows×CellMmを
    // 計算して渡す)。単一点のためFreeLineの本体移動と異なりmin>maxは起こりえない(0<=maxは常に真)。
    private double _dragConnectionDotMaxXMm, _dragConnectionDotMaxYMm;

    /// <summary>接続点をドラッグ中か。</summary>
    public bool IsDraggingConnectionDot => _draggingConnectionDot is not null;

    /// <summary>ドラッグ中の接続点を外部要因により強制的にキャンセルする(T-041増分7所見A対応、
    /// ForceCancelDragConnectorIfAnyと同型)。所見Y対応でCancelDragConnectionDot()と同じ開始時
    /// 位置への復元を行う。</summary>
    private void ForceCancelDragConnectionDotIfAny()
        => ForceCancelIfAny(
            () => _draggingConnectionDot is not null,
            CancelDragConnectionDot,
            () => OnPropertyChanged(nameof(IsDraggingConnectionDot)));

    /// <summary>接続点のドラッグを開始する(T-041増分7)。startXMm/startYMmはドラッグ開始時の
    /// マウス位置(mm実座標)。maxXMm/maxYMmはページ境界(T-041増分7隠密レビュー所見AD対応、
    /// 呼び出し元がsheet.Grid.Columns/Rows×CellMmを計算して渡す)。</summary>
    public void BeginDragConnectionDot(ConnectionDot dot, double startXMm, double startYMm, double maxXMm, double maxYMm)
    {
        _draggingConnectionDot = dot;
        _dragConnectionDotOrigXMm = dot.XMm;
        _dragConnectionDotOrigYMm = dot.YMm;
        _dragConnectionDotStartXMm = startXMm;
        _dragConnectionDotStartYMm = startYMm;
        _dragConnectionDotMaxXMm = maxXMm;
        _dragConnectionDotMaxYMm = maxYMm;
    }

    /// <summary>ドラッグ中のマウス位置(mm実座標)に応じて接続点の位置を更新する(T-041増分7)。
    /// T-041増分7隠密レビュー所見AD対応: グリッド・ページ境界(0〜_dragConnectionDotMax*Mm)へ
    /// クランプする(Undo機能が無いため、境界外へ飛んで見失うことを防ぐ)。</summary>
    public void UpdateDragConnectionDot(double currentXMm, double currentYMm)
    {
        if (_draggingConnectionDot is not ConnectionDot dot) return;
        dot.XMm = Math.Clamp(_dragConnectionDotOrigXMm + (currentXMm - _dragConnectionDotStartXMm), 0, _dragConnectionDotMaxXMm);
        dot.YMm = Math.Clamp(_dragConnectionDotOrigYMm + (currentYMm - _dragConnectionDotStartYMm), 0, _dragConnectionDotMaxYMm);
    }

    /// <summary>ドラッグ確定の骨格(T-045増分D、当初ConnectionDotのみのPoCだったが、隠密レビュー
    /// (ecad2-t045-increment-d-review-onmitsu.md DoD(6))でConfirmDragConnector/ConfirmDragFreeLine
    /// にも分岐が無く同型と判明したためConnector/WireBreak/FreeLineへも展開し4種全てに適用)。
    /// draggingが非nullかつhasChangedがtrueならMarkDirty()し、draggingをnullへ戻す。Confirm/Cancel
    /// とも「型固有の判定・復元ロジックのみdelegateとして渡し、if文の構造とnull化はここへ集約する」
    /// 設計(隠密所見3.4節2.「スナップショット構造体」に相当)。</summary>
    private void ConfirmDrag<T>(ref T? dragging, Func<T, bool> hasChanged) where T : class
    {
        if (dragging is not null && hasChanged(dragging)) MarkDirty();
        dragging = null;
    }

    /// <summary>ドラッグキャンセルの骨格(T-045増分D、4種(Connector/WireBreak/FreeLine/
    /// ConnectionDot)全てに適用)。draggingが非nullならrestoreで開始時位置へ復元してからnullへ
    /// 戻す。</summary>
    private void CancelDrag<T>(ref T? dragging, Action<T> restore) where T : class
    {
        if (dragging is not null) restore(dragging);
        dragging = null;
    }

    /// <summary>接続点のドラッグを確定する(T-041増分7)。開始時から実際に値が変化していれば
    /// MarkDirty()する。</summary>
    public void ConfirmDragConnectionDot()
        => ConfirmDrag(ref _draggingConnectionDot,
            dot => dot.XMm != _dragConnectionDotOrigXMm || dot.YMm != _dragConnectionDotOrigYMm);

    /// <summary>接続点のドラッグをキャンセルし、開始時の位置へ復元する(Esc、T-041増分7)。</summary>
    public void CancelDragConnectionDot()
        => CancelDrag(ref _draggingConnectionDot,
            dot => { dot.XMm = _dragConnectionDotOrigXMm; dot.YMm = _dragConnectionDotOrigYMm; });

    /// <summary>選択中の接続点を矢印キー1回分(Shift無し)平行移動する(T-041増分7、キーボード
    /// 等価操作、1ステップ=CellMm)。maxXMm/maxYMmはページ境界(T-041増分7隠密レビュー所見AD対応、
    /// 呼び出し元がsheet.Grid.Columns/Rows×CellMmを計算して渡す)。実際に動けた場合のみ
    /// MarkDirty()する。</summary>
    public bool MoveSelectedConnectionDot(double deltaXMm, double deltaYMm, double maxXMm, double maxYMm)
    {
        if (SelectedConnectionDot is not ConnectionDot dot) return false;
        double newX = Math.Clamp(dot.XMm + deltaXMm, 0, maxXMm);
        double newY = Math.Clamp(dot.YMm + deltaYMm, 0, maxYMm);
        if (newX == dot.XMm && newY == dot.YMm) return false;
        dot.XMm = newX;
        dot.YMm = newY;
        MarkDirty();
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
    /// 名(Document.Devicesに未登録)の場合は要素種別から解決したDeviceClass(T-045 P-020対応、
    /// ResolveDeviceClass参照)で新規登録する(忍者実機検証で発覚: 単純代入だけでは機器表に反映
    /// されないバグの修正)。
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
                        Document.Devices.ByName[newName] = new Device { Name = newName, Class = ResolveDeviceClass(el) };
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

    /// <summary>末尾行を1行追加する(T-055増分1)。上限(GridSpec.MaxRows)到達時は無効化。</summary>
    public ICommand AddRowCommand { get; }

    /// <summary>末尾行を1行削除する(T-055増分1)。下限(GridSpec.MinRows)到達時は無効化。
    /// 最終行に要素(広義5種、殿裁定)が存在する場合は削除を拒否しStatusMessageへ警告を出す。</summary>
    public ICommand DeleteRowCommand { get; }

    /// <summary>指定行に要素(広義5種: ElementInstance/VerticalConnector/WireBreak/GroupFrame/
    /// RungComment、殿裁定2026-07-10)が存在するかを判定する(T-055増分1、削除拒否の判定に使う)。
    /// internalはIVT経由のテスト用。</summary>
    internal static bool IsRowOccupied(Sheet sheet, int row)
        => sheet.Elements.Any(e => e.Pos.Row == row)
            || sheet.Connectors.Any(c => Math.Min(c.TopRow, c.BottomRow) <= row && row <= Math.Max(c.TopRow, c.BottomRow))
            || sheet.WireBreaks.Any(w => w.Row == row)
            || sheet.Frames.Any(f => row >= f.TopLeft.Row && row < f.TopLeft.Row + f.Height)
            || sheet.RungComments.Any(rc => rc.Row == row);

    /// <summary>Grid.Rowsを変更する操作(Add/Delete/UpdateSheetSettings)共通の後処理(T-055増分2
    /// 隠密レビュー指摘、rule of three超えの重複解消)。SelectedCellが新しい範囲を超えていれば
    /// 選択解除ではなく新しい末尾行へクランプし(殿裁定)、StatusMessageクリア・MarkDirty・
    /// CurrentSheet変更通知を行う。呼び出し元がsheet.Grid.Rowsへの代入を終えた後に呼ぶこと。</summary>
    private void FinishRowCountChange(Sheet sheet)
    {
        if (SelectedCell is GridPos selectedCell && selectedCell.Row >= sheet.Grid.Rows)
            SelectedCell = selectedCell with { Row = sheet.Grid.Rows - 1 };
        StatusMessage = "";
        MarkDirty();
        NotifyCurrentSheetChanged();
    }

    /// <summary>シート設定ダイアログ(T-055増分2)からUpdateSheetSettingsCommandへ渡すパラメータ。
    /// Grid.RowsとBus.LeftName/RightNameをまとめて変更する。</summary>
    public sealed record SheetSettings(int Rows, string LeftName, string RightName);

    /// <summary>シート設定(Grid.Rows・Bus.LeftName/RightName)をまとめて更新する(T-055増分2)。
    /// Rowsが上限/下限(GridSpec.MaxRows/MinRows)外の場合は何も変更せず拒否する(ダイアログ側の
    /// 入力制約をすり抜けた場合の安全弁、AddRowCommand/DeleteRowCommandと同じ二重化方針)。
    /// Bus名の空文字は許容する(殿裁定、GuiEcad踏襲)。</summary>
    public ICommand UpdateSheetSettingsCommand { get; }

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

    /// <summary>SelectedCellが現在のグリッド範囲内(行0〜Rows-1・列0〜Columns-1)か判定する
    /// (T-045増分C、View層のTryPlaceElementが配置バー表示前に境界外を弾くために使う。所見B=
    /// 境界チェック未追随でのサイレント失敗の解消)。選択(SelectedCell)自体の仕様範囲(行-1・
    /// 列-2まで選択可、殿教示2026-07-07・docs/proposed.md P-022/P-024)には触れず、配置前の
    /// フィードバック用の判定に留める(殿裁定2026-07-09=下限0、選択の仕様は不変)。</summary>
    public bool IsSelectedCellWithinGrid()
        => SelectedCell is { } pos && CurrentSheet is Sheet sheet && IsWithinGridBounds(pos, sheet);

    private static bool IsWithinGridBounds(GridPos pos, Sheet sheet)
        => pos.Row >= 0 && pos.Row < sheet.Grid.Rows
        && pos.Column >= 0 && pos.Column < sheet.Grid.Columns;

    /// <summary>posへの配置可否を判定する(T-045 P-025、P-021占有再チェック+P-022/P-024境界ガードの
    /// 統合)。境界外、または既に要素があればfalse。IsSelectedCellWithinGridと境界判定ロジックを
    /// 共有する(IsWithinGridBounds)。</summary>
    private bool ValidatePlacement(GridPos pos, Sheet sheet)
        => IsWithinGridBounds(pos, sheet) && !sheet.Elements.Any(el => el.Pos == pos);

    /// <summary>ElementKindから機器表のDeviceClass分類を導出する(T-045 P-020対応、殿裁可済み案A)。
    /// ContactNO/NC・Coil・ContactorMain3P→Relay(MCコイルと同一機器名参照ゆえ配置順による種別揺れ防止)、
    /// Lamp→Lamp、PushButtonNO/NC・EmergencyStop→PushButton、SelectSwitch→SelectSwitch、
    /// Terminal→Terminal、Timer系(限時・瞬時とも)→Timer、Counter→Counter、
    /// ThermalOverload/ThermalOverload3P・Motor・Breaker3P→Other(該当クラス無し)。</summary>
    private static DeviceClass MapToDeviceClass(ElementKind kind) => kind switch
    {
        ElementKind.ContactNO or ElementKind.ContactNC or ElementKind.Coil or ElementKind.ContactorMain3P
            => DeviceClass.Relay,
        ElementKind.Lamp => DeviceClass.Lamp,
        ElementKind.PushButtonNO or ElementKind.PushButtonNC or ElementKind.EmergencyStop
            => DeviceClass.PushButton,
        ElementKind.SelectSwitch => DeviceClass.SelectSwitch,
        ElementKind.Terminal => DeviceClass.Terminal,
        ElementKind.Timer or ElementKind.TimerContactNO or ElementKind.TimerContactNC
            or ElementKind.TimerInstantContactNO or ElementKind.TimerInstantContactNC => DeviceClass.Timer,
        ElementKind.Counter => DeviceClass.Counter,
        _ => DeviceClass.Other,
    };

    /// <summary>要素からDeviceClassを解決する(T-045 P-020対応)。PartResolver.ComponentKindは
    /// CreatesComponent=false(自作パーツRole=NonSimulated等)の場合に例外を投げるため、事前に
    /// ガードしOtherへフォールバックする。セレクトSWはRole=ContactNO(電気的にはa接点と同一、
    /// T-037往復2周目の既知制約)のためComponentKind経由では区別できない。T-045増分B修正
    /// (隠密レビューCONFIRMED、ecad2-t045-increment-b-review-onmitsu.md): 固定Id完全一致判定は
    /// Explorerコピー由来のId再採番(PartFolderStore.cs:94-110、T-035)に耐性が無く誤分類する
    /// ため廃止し、PartEntryToGlyphGeometryConverter.cs:53-63と同型のCategory/Role/IsOrEligible
    /// 判定へ置換する(element.PartIdでPartPalette.Entriesを動的検索、Idの新旧に依存しない)。
    /// Category==""(基本図形フォルダ直下)をゲートにするのは、自作パーツ(Category="自作")が
    /// Role=ContactNO・IsOrEligible=falseを偶然持つ場合の誤判定を防ぐため
    /// (ecad2-t045-increment-b-fix-test-design-onmitsu.md 分類D)。</summary>
    private DeviceClass ResolveDeviceClass(ElementInstance element)
    {
        var entry = PartPalette.Entries.FirstOrDefault(e => e.Definition.Id == element.PartId);
        if (entry is { Category: "", Definition.Role: PartRole.ContactNO, Definition.IsOrEligible: false })
            return DeviceClass.SelectSwitch;

        return PartResolver.CreatesComponent(element, PartLibrary)
            ? MapToDeviceClass(PartResolver.ComponentKind(element, PartLibrary))
            : DeviceClass.Other;
    }

    /// <summary>
    /// SelectedCellへ要素を配置する(T-026段階4新配置フロー)。isOr=trueの場合、基準行
    /// (SelectedCellより上にある直近の既存要素行、殿裁定で上方向限定)との間に縦コネクタを
    /// 2本自動生成しOR(並列)接続にする。基準行内では新要素に列位置が最も近い要素を対応先とする。
    /// </summary>
    public void PlaceElementAtSelectedCell(string partId, string deviceName, bool isOr)
    {
        if (SelectedCell is not { } pos || CurrentSheet is not Sheet sheet) return;
        if (!ValidatePlacement(pos, sheet)) return;

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
        // setterと同じ流儀: 新規デバイス名(未登録)のみ要素種別から解決したDeviceClass(T-045
        // P-020対応)で追加し、既存デバイス名なら既存エントリを維持(上書きしない)。デバイス名
        // 空欄の場合は機器表を一切操作しない。
        if (deviceName.Length > 0 && !Document.Devices.ByName.ContainsKey(deviceName))
            Document.Devices.ByName[deviceName] = new Device { Name = deviceName, Class = ResolveDeviceClass(newElement) };
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
        // T-050往復2周目(隠密CONFIRMEDバグ2): SelectedSheetの旧値は、_currentSheetIndex=0の先行代入や
        // Sheetsミラーの再構築より前=旧Documentの選択状態が残るこの時点で捕捉する。ResetSheets内部で
        // 捕捉すると、_currentSheetIndex=0(新Document用)と未クリアの旧Sheetsミラーの組から旧Document先頭
        // シートを誤った旧値として返す(参照型ゆえTraceLogのボクシング懸念はなく無条件捕捉でよい)。
        var oldSelectedSheet = SheetNavigation.SelectedSheet;

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
        NotifyCurrentSheetDependentPropertiesChanged();
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(SelectedCell), oldSelectedCell);
        OnPropertyChanged(nameof(SelectedCellDisplay));
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
        SheetNavigation.ResetSheets();
        // T-050往復2周目(隠密CONFIRMEDバグ2): ResetSheets自体はSelectedSheet通知を撃たない。ミラー
        // 再同期(Sheets.Clear+再追加)を終えた後、Document差し替え前に捕捉した正しい旧値でここから
        // ちょうど1回だけ通知する(RefreshSelectedSheetはOnPropertyChanged(SelectedSheet, oldValue)の
        // 薄いラッパ。getterは新Document先頭=Sheets[0]を返すため new値も正しい)。
        SheetNavigation.RefreshSelectedSheet(oldSelectedSheet);
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
    /// コンストラクタ(P-019=App層テストが実MyDocumentsを叩く副作用の解消)。IDispatcherServiceは
    /// 本番既定(WpfDispatcherService)を使う。</summary>
    public MainWindowViewModel(PartFolderStore partFolderStore) : this(partFolderStore, new WpfDispatcherService()) { }

    /// <summary>T-045(P-016対応): テスト等からIDispatcherServiceも注入できるようにするための
    /// コンストラクタ(SheetNavigationViewModelのDispatcher直接依存分離)。PartFolderStoreの
    /// 2本立てパターン(T-042)と同型。</summary>
    public MainWindowViewModel(PartFolderStore partFolderStore, IDispatcherService dispatcherService)
    {
        SheetNavigation = new SheetNavigationViewModel(this, dispatcherService);
        PartPalette = new PartPaletteViewModel(partFolderStore);
        // T-015隠密レビュー指摘#2: PartPaletteViewModel.Libraryと同一ロジックの重複構築だったため、
        // 構築元(PartPaletteViewModel)へ一本化し、ここでは公開済みのLibraryをそのまま使う。
        PartLibrary = PartPalette.Library;
        DeviceTable = new DeviceTableViewModel(Document.Devices);
        OutputPanel = new OutputPanelViewModel(this);

        // T-055増分1: 末尾行の追加・削除。CanExecuteはボタンのIsEnabled連動用、Execute内部の
        // ガードはキーボードショートカット等CanExecuteを経由しない呼び出しに対する安全弁
        // (二重化だが役割が異なるため許容、家老裁可済み)。
        AddRowCommand = new RelayCommand(
            () =>
            {
                if (CurrentSheet is not Sheet sheet || sheet.Grid.Rows >= GridSpec.MaxRows) return;
                sheet.Grid.Rows++;
                FinishRowCountChange(sheet);
            },
            () => CurrentSheet is Sheet sheet && sheet.Grid.Rows < GridSpec.MaxRows);

        DeleteRowCommand = new RelayCommand(
            () =>
            {
                if (CurrentSheet is not Sheet sheet || sheet.Grid.Rows <= GridSpec.MinRows) return;
                int lastRow = sheet.Grid.Rows - 1;
                if (IsRowOccupied(sheet, lastRow))
                {
                    StatusMessage = "最終行に要素があるため削除できません";
                    return;
                }
                sheet.Grid.Rows--;
                FinishRowCountChange(sheet);
            },
            () => CurrentSheet is Sheet sheet && sheet.Grid.Rows > GridSpec.MinRows);

        // T-055増分2: シート設定ダイアログ経由でGrid.Rows・Bus.LeftName/RightNameをまとめて更新。
        UpdateSheetSettingsCommand = new RelayCommand(param =>
        {
            if (CurrentSheet is not Sheet sheet || param is not SheetSettings settings) return;
            if (settings.Rows < GridSpec.MinRows || settings.Rows > GridSpec.MaxRows) return;
            // T-055増分2往復1周目(隠密レビュー指摘、殿裁定): DeleteRowCommandは最終行のみ判定するが、
            // UpdateSheetSettingsCommandはダイアログ経由で一気に大きく縮小できるため、縮小される
            // 全行(新Rows〜旧Rows-1)のいずれかに要素があれば拒否する(キーボードのみ到達不能・
            // マウス経由のみ到達可という非対称の解消)。
            for (int row = settings.Rows; row < sheet.Grid.Rows; row++)
            {
                if (IsRowOccupied(sheet, row))
                {
                    StatusMessage = "最終行に要素があるため削除できません";
                    return;
                }
            }
            sheet.Grid.Rows = settings.Rows;
            sheet.Bus.LeftName = settings.LeftName;
            sheet.Bus.RightName = settings.RightName;
            FinishRowCountChange(sheet);
        },
        _ => CurrentSheet is not null);
    }
}
