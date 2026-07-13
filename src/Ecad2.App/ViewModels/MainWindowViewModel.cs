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
        set
        {
            if (SetProperty(ref _isPlacementBarVisible, value))
                OnPropertyChanged(nameof(IsMainContentEnabled));
        }
    }

    private bool _isRungCommentEditorVisible;

    /// <summary>T-080往復1周目指摘F: 行コメントエディタの表示状態(IsPlacementBarVisibleと同じ
    /// 「ViewModel単一の真実源」パターン)。表示中はIsMainContentEnabled経由でメインコンテンツを
    /// 無効化し、マウス経由の素通し(編集中にメニュー「新規」等が即実行される)を塞ぐ。キーボード
    /// 経路はWindow_PreviewKeyDown冒頭のガード(_rungCommentEditingRow)が対で塞ぐ。</summary>
    public bool IsRungCommentEditorVisible
    {
        get => _isRungCommentEditorVisible;
        set
        {
            if (SetProperty(ref _isRungCommentEditorVisible, value))
                OnPropertyChanged(nameof(IsMainContentEnabled));
        }
    }

    /// <summary>メインコンテンツ(メニュー・ツールバー・メイン作業域・出力パネル)が操作可能か
    /// (MainWindow.xaml MainContentAreaのIsEnabledバインド先)。配置バー(T-033)・行コメント
    /// エディタ(T-080往復1周目指摘F)のどちらかが表示中はfalse(現行モーダル同等の使用感)。</summary>
    public bool IsMainContentEnabled => !IsPlacementBarVisible && !IsRungCommentEditorVisible;

    private double _canvasScale = 1.0;

    /// <summary>キャンバスの表示倍率（Ctrl+マウスホイールで変更）。0.25〜4.0の範囲にクランプする。</summary>
    public double CanvasScale
    {
        get => _canvasScale;
        set => SetProperty(ref _canvasScale, Math.Clamp(value, 0.25, 4.0));
    }

    private bool _isGridVisible = true;

    /// <summary>作図ガイドのグリッド線を画面表示するか(T-056)。既定=表示(殿裁定2026-07-11、
    /// T-030以来の常時表示運用を踏襲)。「表示」メニューのグリッド表示項目・Ctrl+Gでトグルする。
    /// 非永続(殿裁定2026-07-11)のためアプリ再起動で既定値へ戻る。</summary>
    public bool IsGridVisible
    {
        get => _isGridVisible;
        set => SetProperty(ref _isGridVisible, value);
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

    /// <summary>ドキュメント情報(T-065)を一括反映しMarkDirty()する。Revisions(改定履歴)は
    /// 編集対象外(殿裁定2026-07-12)のため変更しない。</summary>
    public void ApplyDocumentInfo(DocumentInfo info)
    {
        Document.Info.CompanyName = info.CompanyName;
        Document.Info.Title = info.Title;
        Document.Info.DrawingNo = info.DrawingNo;
        Document.Info.Customer = info.Customer;
        Document.Info.Designer = info.Designer;
        Document.Info.Drafter = info.Drafter;
        Document.Info.Checker = info.Checker;
        Document.Info.Date = info.Date;
        MarkDirty();
    }

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

    /// <summary>
    /// T-082往復3周目(隠密review2提案「実体不変の原則」): CurrentSheetの実体(オブジェクト参照)は
    /// 変わらないが表示上の添字だけが変化するケース(MoveSheetCommandでのシート並び替え=削除・追加
    /// ではなく位置入替のみ)専用の経路。<see cref="SetCurrentSheetIndexCore"/>と異なり、クロスカット
    /// 処理(CurrentSheet依存プロパティの再通知・SelectedCellクリア)を一切行わない——実体が不変で
    /// ある以上これらのクリア処理は不要かつ有害で、選択中セル・記入中ドラフトを理由なく破棄する
    /// 「所見L」型再発(往復1〜2周目で対症療法に留まっていた欠陥、docs/ecad2-t082-sheet-reorder-
    /// review2-onmitsu.md)の根本原因だった。
    /// </summary>
    internal void SetCurrentSheetIndexWithoutCrossCut(int value) => SetProperty(ref _currentSheetIndex, value);

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
            // T-064: 画像も同様に扱う。
            SelectedImage = null;
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
            // T-064: 画像挿入の記入中状態(_imageInsertDraft)も同型でクリアする(シート切替等の外部
            // 要因での残留防止)。CancelImageInsertDraftは「記入中でなければ何もしない」構造のため
            // ClearConnectorDraftIfAny等と同様にそのまま呼べる。
            CancelImageInsertDraft();
            if (SetProperty(ref _selectedCell, value))
            {
                OnPropertyChanged(nameof(SelectedCellDisplay));
                OnPropertyChanged(nameof(SelectedElement));
                OnPropertyChanged(nameof(HasSelectedElement));
                OnPropertyChanged(nameof(SelectedElementKindDisplay));
                OnPropertyChanged(nameof(SelectedElementDeviceName));
                OnPropertyChanged(nameof(HasNoPropertySelection));
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

    private ImageInsert? _selectedImage;

    /// <summary>
    /// 現在選択中の画像(T-064: グリッド非依存の自由配置要素、SelectedFreeLine等と同型の排他制御——
    /// SelectedCellのsetterが常時クリアする)。単一選択のみ。
    /// </summary>
    public ImageInsert? SelectedImage
    {
        get => _selectedImage;
        set
        {
            ForceCancelDragImageIfAny();
            ForceCancelResizeImageIfAny();
            SetProperty(ref _selectedImage, value);
            OnPropertyChanged(nameof(HasSelectedImage));
            OnPropertyChanged(nameof(HasNoPropertySelection));
        }
    }

    /// <summary>右パネル下段のプロパティ表示切替に使う(選択中の画像があるか)。</summary>
    public bool HasSelectedImage => SelectedImage is not null;

    /// <summary>右パネルのプロパティ領域で「要素を選択してください」プレースホルダを表示するか
    /// (T-064: 画像選択の追加に伴い、要素・画像いずれも無選択の場合のみプレースホルダを表示する)。</summary>
    public bool HasNoPropertySelection => !HasSelectedElement && !HasSelectedImage;

    /// <summary>SelectedImageのトレース用下絵トグル(T-064、プロパティパネルのCheckBox用)。殿裁定
    /// (画像操作は全てUndo対象、他要素との非対称は許容)によりRecordSnapshotを実行直前に呼ぶ。</summary>
    public bool SelectedImageIsTracingOnly
    {
        get => SelectedImage?.IsTracingOnly ?? false;
        set
        {
            if (SelectedImage is not ImageInsert image || image.IsTracingOnly == value) return;
            UndoManager.RecordSnapshot(Document);
            image.IsTracingOnly = value;
            MarkDirty();
            OnPropertyChanged(nameof(SelectedImageIsTracingOnly));
        }
    }

    /// <summary>SelectedImageを削除する(T-064、案A=DeleteSelectedFreeLineと同型)。殿裁定により
    /// 画像操作はUndo対象(他要素との非対称は許容)のため、削除直前にRecordSnapshotを呼ぶ。
    /// 戻り値は実際に削除したか。</summary>
    public bool DeleteSelectedImage()
    {
        if (CurrentSheet is not Sheet sheet || SelectedImage is not ImageInsert image || !sheet.Images.Contains(image))
            return false;
        UndoManager.RecordSnapshot(Document);
        sheet.Images.Remove(image);
        MarkDirty();
        SelectedImage = null;
        return true;
    }

    // T-064: 画像挿入(メニュー→ファイル選択→キャンバス上で配置待機、殿裁定「案A」=2段階操作)の
    // 作業中データ。ファイルパス・初期サイズ(View側で計算済み、長辺120mm上限・アスペクト比維持)は
    // 固定、現在位置(mm実座標)はマウスホバーに追従して更新される(記入中ドラフトの一種だが、
    // キーボードではなくマウス位置を直接受け取る点が_connectorDraft/_freeLineDraftと異なる)。
    private (string FilePath, double WidthMm, double HeightMm, double XMm, double YMm)? _imageInsertDraft;

    /// <summary>記入中の画像挿入のプレビュー形状(LadderCanvasの半透明描画用)。記入中でなければnull。</summary>
    public ImageInsert? ImageInsertDraftPreview
    {
        get
        {
            if (_imageInsertDraft is not { } d) return null;
            return new ImageInsert { FilePath = d.FilePath, XMm = d.XMm, YMm = d.YMm, WidthMm = d.WidthMm, HeightMm = d.HeightMm };
        }
    }

    /// <summary>画像挿入メニュー選択・ファイル選択完了後、配置待機モードを開始する(T-064、殿裁定
    /// 「案A」)。widthMm/heightMmはView側で計算済みの初期サイズ(長辺120mm上限、アスペクト比維持)。
    /// xMm/yMmは開始時点のマウス位置(mm実座標、View側が変換して渡す)。</summary>
    public void BeginImageInsertDraft(string filePath, double widthMm, double heightMm, double xMm, double yMm)
    {
        _imageInsertDraft = (filePath, widthMm, heightMm, xMm, yMm);
        Tool = new ToolState(ToolMode.PlaceImage);
        OnPropertyChanged(nameof(ImageInsertDraftPreview));
    }

    /// <summary>記入中の画像挿入位置をマウスホバーに追従させる(T-064)。ページ境界(0〜maxXMm/maxYMm、
    /// 呼び出し元がsheet.Grid.Columns/Rows×CellMmを計算して渡す)へクランプする。</summary>
    public void UpdateImageInsertDraftPosition(double xMm, double yMm, double maxXMm, double maxYMm)
    {
        if (_imageInsertDraft is not { } d) return;
        double clampedX = Math.Clamp(xMm, 0, Math.Max(0, maxXMm - d.WidthMm));
        double clampedY = Math.Clamp(yMm, 0, Math.Max(0, maxYMm - d.HeightMm));
        _imageInsertDraft = d with { XMm = clampedX, YMm = clampedY };
        OnPropertyChanged(nameof(ImageInsertDraftPreview));
    }

    /// <summary>記入中の画像挿入を確定する(クリック、T-064)。殿裁定により画像操作はUndo対象のため
    /// RecordSnapshotを実行直前に呼ぶ。確定後は挿入した画像を選択状態にする(GuiEcad同様)。
    /// 戻り値は実際に確定したか。</summary>
    public bool ConfirmImageInsertDraft()
    {
        if (_imageInsertDraft is not { } d || CurrentSheet is not Sheet sheet) return false;
        UndoManager.RecordSnapshot(Document);
        var image = new ImageInsert { FilePath = d.FilePath, XMm = d.XMm, YMm = d.YMm, WidthMm = d.WidthMm, HeightMm = d.HeightMm };
        sheet.Images.Add(image);
        MarkDirty();
        CancelImageInsertDraft();
        // T-064往復1周目修正4(隠密レビュー指摘、実害大): SelectedCellをnullにせずSelectedImageだけを
        // 設定すると、既存要素選択(SelectedCell)を残したまま画像挿入した場合にHasSelectedElement/
        // HasSelectedImageが両方trueになり、Deleteキーが対象とするOR連鎖の先頭(要素削除)を誤って
        // 先にヒットさせる(挿入したばかりの画像ではなく旧選択要素が消える)。SelectedCell=null→
        // SelectedImage=imageの順で呼び、排他選択を成立させる(縦コネクタ選択等の既存パターンと同順)。
        SelectedCell = null;
        SelectedImage = image;
        return true;
    }

    /// <summary>記入中の画像挿入を取消す(Esc、T-064)。何も生成せず選択モードへ戻す。</summary>
    public void CancelImageInsertDraft()
    {
        if (_imageInsertDraft is null) return;
        _imageInsertDraft = null;
        Tool = ToolState.SelectDefault;
        OnPropertyChanged(nameof(ImageInsertDraftPreview));
    }

    // T-064: 画像のドラッグ移動(本体移動のみ、リサイズは別途BeginResizeImage系)。ConnectionDotと
    // 同型(相対差分方式)だが、Width/HeightがあるためFreeLine同様「画像がページ境界からはみ出さない」
    // 制約で境界クランプする。
    private ImageInsert? _draggingImage;
    private double _dragImageOrigXMm, _dragImageOrigYMm;
    private double _dragImageStartXMm, _dragImageStartYMm;
    private double _dragImageMaxXMm, _dragImageMaxYMm;

    /// <summary>画像をドラッグ中か。</summary>
    public bool IsDraggingImage => _draggingImage is not null;

    /// <summary>ドラッグ中の画像を外部要因により強制的にキャンセルする(ForceCancelDragConnectionDot
    /// IfAnyと同型)。</summary>
    private void ForceCancelDragImageIfAny()
        => ForceCancelIfAny(
            () => _draggingImage is not null,
            CancelDragImage,
            () => OnPropertyChanged(nameof(IsDraggingImage)));

    /// <summary>画像のドラッグ(本体移動)を開始する(T-064)。startXMm/startYMmはドラッグ開始時の
    /// マウス位置(mm実座標)。maxXMm/maxYMmはページ境界(呼び出し元がsheet.Grid.Columns/Rows×CellMm
    /// を計算して渡す)。</summary>
    public void BeginDragImage(ImageInsert image, double startXMm, double startYMm, double maxXMm, double maxYMm)
    {
        _draggingImage = image;
        _dragImageOrigXMm = image.XMm;
        _dragImageOrigYMm = image.YMm;
        _dragImageStartXMm = startXMm;
        _dragImageStartYMm = startYMm;
        _dragImageMaxXMm = maxXMm;
        _dragImageMaxYMm = maxYMm;
    }

    /// <summary>ドラッグ中のマウス位置(mm実座標)に応じて画像の位置を更新する(T-064)。画像がページ
    /// 境界からはみ出さないよう(0〜max-Width/Height)クランプする。</summary>
    public void UpdateDragImage(double currentXMm, double currentYMm)
    {
        if (_draggingImage is not ImageInsert image) return;
        double newX = Math.Clamp(_dragImageOrigXMm + (currentXMm - _dragImageStartXMm), 0, Math.Max(0, _dragImageMaxXMm - image.WidthMm));
        double newY = Math.Clamp(_dragImageOrigYMm + (currentYMm - _dragImageStartYMm), 0, Math.Max(0, _dragImageMaxYMm - image.HeightMm));
        image.XMm = newX;
        image.YMm = newY;
    }

    /// <summary>画像のドラッグを確定する(T-064)。殿裁定により画像操作はUndo対象のため、開始時から
    /// 実際に値が変化していれば、一旦開始時位置へ戻してRecordSnapshotを呼んでから確定値へ戻す
    /// (RecordSnapshotは「操作実行の直前」の状態を記録する契約のため、確定後の値をそのまま記録すると
    /// Undoしても変化前の状態に戻らなくなる)。</summary>
    public void ConfirmDragImage()
    {
        if (_draggingImage is not ImageInsert image) { _draggingImage = null; return; }
        if (image.XMm != _dragImageOrigXMm || image.YMm != _dragImageOrigYMm)
        {
            double confirmedX = image.XMm, confirmedY = image.YMm;
            image.XMm = _dragImageOrigXMm;
            image.YMm = _dragImageOrigYMm;
            UndoManager.RecordSnapshot(Document);
            image.XMm = confirmedX;
            image.YMm = confirmedY;
            MarkDirty();
        }
        _draggingImage = null;
    }

    /// <summary>画像のドラッグをキャンセルし、開始時の位置へ復元する(Esc、T-064)。</summary>
    public void CancelDragImage()
        => CancelDrag(ref _draggingImage,
            image => { image.XMm = _dragImageOrigXMm; image.YMm = _dragImageOrigYMm; });

    // T-064: 画像のリサイズ(ドラッグハンドル、殿裁定=数値入力のみのGuiEcad踏襲は不採用)の一時状態。
    // ドラッグ中のハンドルの対角コーナーを固定点(アンカー)とし、幅・高さを独立して変更する。
    private ImageInsert? _resizingImage;
    private ImageResizeHandle _resizingImageHandle;
    private double _resizeImageAnchorXMm, _resizeImageAnchorYMm;
    private double _resizeImageOrigXMm, _resizeImageOrigYMm, _resizeImageOrigWidthMm, _resizeImageOrigHeightMm;
    private double _resizeImageMaxXMm, _resizeImageMaxYMm;

    private const double ImageMinSizeMm = 5.0;

    /// <summary>画像をリサイズ中か。</summary>
    public bool IsResizingImage => _resizingImage is not null;

    /// <summary>リサイズ中の画像を外部要因により強制的にキャンセルする。</summary>
    private void ForceCancelResizeImageIfAny()
        => ForceCancelIfAny(
            () => _resizingImage is not null,
            CancelResizeImage,
            () => OnPropertyChanged(nameof(IsResizingImage)));

    /// <summary>画像のリサイズを開始する(T-064、ドラッグハンドル方式、殿裁定)。handleは掴んだ隅、
    /// startXMm/startYMmはドラッグ開始時のマウス位置(mm実座標、未使用だが他Begin*との対称性のため
    /// 引数に残す)。maxXMm/maxYMmはページ境界。</summary>
    public void BeginResizeImage(ImageInsert image, ImageResizeHandle handle, double startXMm, double startYMm, double maxXMm, double maxYMm)
    {
        _resizingImage = image;
        _resizingImageHandle = handle;
        _resizeImageOrigXMm = image.XMm;
        _resizeImageOrigYMm = image.YMm;
        _resizeImageOrigWidthMm = image.WidthMm;
        _resizeImageOrigHeightMm = image.HeightMm;
        _resizeImageMaxXMm = maxXMm;
        _resizeImageMaxYMm = maxYMm;
        // 掴んだ隅の対角(固定点)を計算する。
        (_resizeImageAnchorXMm, _resizeImageAnchorYMm) = handle switch
        {
            ImageResizeHandle.TopLeft => (image.XMm + image.WidthMm, image.YMm + image.HeightMm),
            ImageResizeHandle.TopRight => (image.XMm, image.YMm + image.HeightMm),
            ImageResizeHandle.BottomLeft => (image.XMm + image.WidthMm, image.YMm),
            ImageResizeHandle.BottomRight => (image.XMm, image.YMm),
            _ => (image.XMm, image.YMm),
        };
    }

    /// <summary>ドラッグ中のマウス位置(mm実座標)に応じて画像のサイズを更新する(T-064)。固定点
    /// (対角コーナー)からドラッグ中の隅までの距離を新しい幅・高さとする。
    /// T-064往復1周目修正1(隠密レビュー最重要指摘、4系統独立検出): 旧実装はページ境界クランプ→
    /// 最小サイズ適用の順で、最小サイズ確保がページ境界を再び破りうる不具合があった(アンカーが
    /// 境界近くの状態で対角ハンドルを大きく逆方向へ動かすと画像が境界外へはみ出す)ため、
    /// <see cref="ClampResizeTarget"/>で「最小サイズ制約」と「ページ境界制約」を同時に満たす範囲へ
    /// 1軸ずつクランプする方式へ改めた。
    /// T-064往復2周目修正2(隠密再レビュー指摘、殿裁定=退行として復活): 上記改修時、伸びる方向
    /// (growsPositive)を掴んだハンドル種別のみで固定してしまい、ハンドルをアンカーの反対側へ大きく
    /// ドラッグした際に軸が反転してマウスへ追従する旧来の挙動が失われていた。掴んだハンドル種別
    /// (defaultGrowsRight/Down)を「反転が起きない通常時の伸びる方向」として渡し、
    /// <see cref="ClampResizeTarget"/>内でドラッグ位置基準の反転を優先しつつ、反転先で最小サイズを
    /// 確保できない異常ケース(アンカーが境界近く)ではハンドル本来の方向へフォールバックすることで、
    /// 反転追従とページ境界クランプ・最小サイズ確保の両立を実現する。</summary>
    public void UpdateResizeImage(double currentXMm, double currentYMm)
    {
        if (_resizingImage is not ImageInsert image) return;

        bool defaultGrowsRight = _resizingImageHandle is ImageResizeHandle.TopRight or ImageResizeHandle.BottomRight;
        bool defaultGrowsDown = _resizingImageHandle is ImageResizeHandle.BottomLeft or ImageResizeHandle.BottomRight;
        double targetX = ClampResizeTarget(currentXMm, _resizeImageAnchorXMm, defaultGrowsRight, _resizeImageMaxXMm);
        double targetY = ClampResizeTarget(currentYMm, _resizeImageAnchorYMm, defaultGrowsDown, _resizeImageMaxYMm);

        image.XMm = Math.Min(_resizeImageAnchorXMm, targetX);
        image.YMm = Math.Min(_resizeImageAnchorYMm, targetY);
        image.WidthMm = Math.Abs(targetX - _resizeImageAnchorXMm);
        image.HeightMm = Math.Abs(targetY - _resizeImageAnchorYMm);
    }

    /// <summary>T-064往復1周目修正1・往復2周目修正2: リサイズ対象座標を、最小サイズ制約
    /// (ImageMinSizeMm)とページ境界制約(0〜maxMm)の両方を満たす範囲へクランプする。伸びる方向は
    /// まずドラッグ位置(currentMm)がアンカーのどちら側にあるかで判定する(反転追従)。ただし、その
    /// 方向では最小サイズを確保できない場合(アンカーがページ境界に極めて近い異常ケース)は、掴んだ
    /// ハンドル本来の伸びる方向(defaultGrowsPositive)へフォールバックし、往復1周目と同じ境界優先
    /// クランプ(安全側、min>maxでのMath.Clamp例外を避ける)を適用する。</summary>
    private static double ClampResizeTarget(double currentMm, double anchorMm, bool defaultGrowsPositive, double maxMm)
    {
        bool growsPositive = currentMm >= anchorMm;
        if (growsPositive && anchorMm + ImageMinSizeMm > maxMm) growsPositive = defaultGrowsPositive;
        if (!growsPositive && anchorMm - ImageMinSizeMm < 0) growsPositive = defaultGrowsPositive;

        if (growsPositive)
        {
            double lowerBound = anchorMm + ImageMinSizeMm;
            return lowerBound > maxMm ? maxMm : Math.Clamp(currentMm, lowerBound, maxMm);
        }
        double upperBound = anchorMm - ImageMinSizeMm;
        return upperBound < 0 ? 0 : Math.Clamp(currentMm, 0, upperBound);
    }

    /// <summary>画像のリサイズを確定する(T-064)。殿裁定により画像操作はUndo対象のため、開始時から
    /// 実際に値が変化していれば、一旦開始時のサイズへ戻してRecordSnapshotを呼んでから確定値へ戻す
    /// (ConfirmDragImageと同じ理由)。</summary>
    public void ConfirmResizeImage()
    {
        if (_resizingImage is not ImageInsert image) { _resizingImage = null; return; }
        bool changed = image.XMm != _resizeImageOrigXMm || image.YMm != _resizeImageOrigYMm
            || image.WidthMm != _resizeImageOrigWidthMm || image.HeightMm != _resizeImageOrigHeightMm;
        if (changed)
        {
            double confirmedX = image.XMm, confirmedY = image.YMm, confirmedWidth = image.WidthMm, confirmedHeight = image.HeightMm;
            image.XMm = _resizeImageOrigXMm; image.YMm = _resizeImageOrigYMm;
            image.WidthMm = _resizeImageOrigWidthMm; image.HeightMm = _resizeImageOrigHeightMm;
            UndoManager.RecordSnapshot(Document);
            image.XMm = confirmedX; image.YMm = confirmedY;
            image.WidthMm = confirmedWidth; image.HeightMm = confirmedHeight;
            MarkDirty();
        }
        _resizingImage = null;
    }

    /// <summary>画像のリサイズをキャンセルし、開始時のサイズへ復元する(Esc、T-064)。</summary>
    public void CancelResizeImage()
        => CancelDrag(ref _resizingImage, image =>
        {
            image.XMm = _resizeImageOrigXMm; image.YMm = _resizeImageOrigYMm;
            image.WidthMm = _resizeImageOrigWidthMm; image.HeightMm = _resizeImageOrigHeightMm;
        });

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

    /// <summary>現在いずれかの記入中ドラフト(縦コネクタ・自由線・画像挿入)を実際に保持しているか
    /// (T-069往復3周目修正2、隠密テスト設計書「Tool.Modeガード粒度」)。右クリック時の記入中
    /// ドラフト保護ガードを、静的なツールモード(Tool.Mode!=Select全般)ではなく実際にドラフトを
    /// 持つ状態のみへ絞り込むために使う。往復2周目のTool.Modeガードは、ドラフトを一切持たない
    /// PlaceElement(連続配置、T-021分岐A)等まで一律ブロックする過剰な副作用があった。</summary>
    public bool HasAnyDraft => _connectorDraft is not null || _freeLineDraft is not null || _imageInsertDraft is not null;

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
                    RemoveDeviceIfUnreferenced(oldName);
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

        if (deviceName is not null) RemoveDeviceIfUnreferenced(deviceName);

        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
        DeviceTable.Refresh();
        return true;
    }

    /// <summary>指定デバイス名が、Document.Sheets全体のどの要素からも参照されなくなっていれば
    /// Document.Devices.ByNameから該当エントリを除去する(T-055増分3往復1周目、隠密レビュー指摘c=
    /// SelectedElementDeviceNameセッター・DeleteSelectedElement・CleanupRemovedDeviceNamesの
    /// 3箇所で複製されていた「参照有無判定→除去」ロジックを一本化)。DeviceTable.Refresh()は
    /// 呼び出し元の責務(呼び出し元が複数回連続で呼ぶ場合があるため、ここでは呼ばない)。</summary>
    private void RemoveDeviceIfUnreferenced(string deviceName)
    {
        bool stillReferenced = Document.Sheets.Any(s =>
            s.Elements.Any(e => string.Equals(e.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase)));
        if (stillReferenced) return;
        var key = Document.Devices.ByName.Keys
            .FirstOrDefault(k => string.Equals(k, deviceName, StringComparison.OrdinalIgnoreCase));
        if (key is not null) Document.Devices.ByName.Remove(key);
    }

    /// <summary>行削除で「要素ごと削除」(T-055増分3、RowOps.DeleteRow)された複数のElementInstanceに対し、
    /// DeleteSelectedElement(単一削除)と同じ規則で機器表(Document.Devices)クリーンアップを行う。
    /// 削除された要素群のDeviceNameのうち、他のどのシートのどの要素からも参照されなくなったものだけ
    /// Document.Devices.ByNameから除去する(重複DeviceNameはDistinctで1回のみ判定)。</summary>
    private void CleanupRemovedDeviceNames(IReadOnlyList<ElementInstance> removed)
    {
        if (removed.Count == 0) return;
        foreach (var deviceName in removed.Select(e => e.DeviceName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            RemoveDeviceIfUnreferenced(deviceName);
        DeviceTable.Refresh();
    }

    /// <summary>SelectedElement系4プロパティ(SelectedElement/HasSelectedElement/
    /// SelectedElementKindDisplay/SelectedElementDeviceName)のPropertyChangedを無条件で発火する
    /// (T-055増分3往復1周目、隠密レビュー指摘a・bの恒久対応)。DeleteRowAtCommandは行削除で
    /// SelectedCellの座標が指す実データ(要素の有無・内容)が変わりうるが、SelectedCell自体の
    /// setterは「値が変化した場合のみ」通知する(削除対象行そのものを選択中=座標は不変というケースで
    /// 通知が抜け落ちる、指摘a)ため、削除完了後に明示的に呼ぶ。</summary>
    private void NotifySelectedElementChanged()
    {
        OnPropertyChanged(nameof(SelectedElement));
        OnPropertyChanged(nameof(HasSelectedElement));
        OnPropertyChanged(nameof(SelectedElementKindDisplay));
        OnPropertyChanged(nameof(SelectedElementDeviceName));
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

    /// <summary>指定行の行コメント(T-080)を取得する。エディタの初期表示に使う。</summary>
    public string GetRungComment(int row)
        => CurrentSheet?.RungComments.FirstOrDefault(rc => rc.Row == row)?.Text ?? "";

    /// <summary>行コメント(T-080)を設定する。空文字列は削除扱い(殿裁定2026-07-12、GuiEcadの
    /// 空エントリ残留癖は踏襲しない)。値が実際に変化した場合のみMarkDirty()する(T-065/T-066往復の
    /// 教訓、同値ガード規約)。</summary>
    public void SetRungComment(int row, string text)
    {
        if (CurrentSheet is not Sheet sheet) return;
        string trimmed = text.Trim();
        var existing = sheet.RungComments.FirstOrDefault(rc => rc.Row == row);
        string oldText = existing?.Text ?? "";
        if (oldText == trimmed) return;

        if (trimmed.Length == 0)
        {
            if (existing is not null) sheet.RungComments.Remove(existing);
        }
        else if (existing is not null)
        {
            existing.Text = trimmed;
        }
        else
        {
            sheet.RungComments.Add(new RungComment { Row = row, Text = trimmed });
        }
        MarkDirty();
    }

    /// <summary>指定行に要素(広義5種: ElementInstance/VerticalConnector/WireBreak/GroupFrame/
    /// RungComment、殿裁定2026-07-10)が存在するかを判定する(T-055増分1、削除拒否の判定に使う)。
    /// internalはIVT経由のテスト用。</summary>
    internal static bool IsRowOccupied(Sheet sheet, int row)
        => sheet.Elements.Any(e => e.Pos.Row == row)
            || sheet.Connectors.Any(c => Math.Min(c.TopRow, c.BottomRow) <= row && row <= Math.Max(c.TopRow, c.BottomRow))
            || sheet.WireBreaks.Any(w => w.Row == row)
            || sheet.Frames.Any(f => row >= f.TopLeft.Row && row < f.TopLeft.Row + f.Height)
            || sheet.RungComments.Any(rc => rc.Row == row);

    /// <summary>指定行が占有されていれば拒否メッセージ(message)をStatusMessageへ設定してtrueを返す
    /// (T-055増分3往復1周目、隠密レビュー指摘c=DeleteRowCommand/UpdateSheetSettingsCommand/
    /// DeleteRowAtCommandの3箇所到達によるrule of three解消)。呼び出し元はtrueが返ればreturnすること。
    /// 文言は呼び出し元ごとに異なる(DeleteRowCommandは「最終行に」固定、他は実際の行番号)ためそのまま維持し、
    /// 判定→設定のロジックのみ共通化する。</summary>
    private bool TryRejectOccupiedRow(Sheet sheet, int row, string message)
    {
        if (!IsRowOccupied(sheet, row)) return false;
        StatusMessage = message;
        return true;
    }

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

    /// <summary>右クリックコンテキストメニューから、指定行(int、CommandParameter)の前に1行挿入する
    /// (T-055増分3)。上限(GridSpec.MaxRows)到達時は無効化。RowOps.InsertRowで広義5種をシフトする。</summary>
    public ICommand InsertRowBeforeCommand { get; }

    /// <summary>右クリックコンテキストメニューから、指定行(int、CommandParameter)を削除する
    /// (T-055増分3)。下限(GridSpec.MinRows)到達時は無効化。対象行に要素(広義5種)が存在する場合は
    /// 拒否せず「要素ごと削除」する(GuiEcad同型、殿裁定2026-07-11。DeleteRowCommand/
    /// UpdateSheetSettingsCommandは現状の拒否のまま、本コマンド限定の適用)。</summary>
    public ICommand DeleteRowAtCommand { get; }

    /// <summary>直前の操作を取り消す(T-051、MVP対象範囲=シート追加/削除のみ)。履歴が無ければ無効化
    /// (IsEnabledはCommandManager.RequerySuggested経由で自動連動、既存コマンドと同じ流儀)。</summary>
    public ICommand UndoCommand { get; }

    /// <summary>Undoで取り消した操作をやり直す(T-051)。履歴が無ければ無効化。</summary>
    public ICommand RedoCommand { get; }

    /// <summary>左パレット（シートナビゲーション）の子ViewModel。</summary>
    public SheetNavigationViewModel SheetNavigation { get; }

    /// <summary>部品選択の子ViewModel（自作パーツ含む）。T-026段階4で右パネルへ配置予定。</summary>
    public PartPaletteViewModel PartPalette { get; }

    /// <summary>右パネル上段（機器表）の子ViewModel。</summary>
    public DeviceTableViewModel DeviceTable { get; }

    /// <summary>下部出力パネル（DesignRuleCheck結果表示）の子ViewModel。</summary>
    public OutputPanelViewModel OutputPanel { get; }

    /// <summary>Undo/Redo基盤(T-051)。MVP対象範囲はSheetNavigationViewModelのシート追加/削除のみ
    /// (設計出典: docs/ecad2-t051-implementation-plan-samurai.md)。</summary>
    public UndoManager UndoManager { get; } = new();

    /// <summary>
    /// 現在使用可能な自作パーツライブラリ。PartPalette.Library(T-015隠密レビュー指摘#2で
    /// PartPaletteViewModel側に構築を一本化)をそのまま参照する。DiagramRenderer.Render /
    /// LadderCanvas.Draw に渡し、要素配置時（T-016）の PartResolver 解決にも使う。
    /// </summary>
    public PartLibrary PartLibrary { get; }

    /// <summary>SelectedCellの行に既に要素が置かれているか判定する(T-026段階4: 配置行は空き行限定、行挿入はしない)。
    /// cellWidth>1(Motor等)の場合、占有列範囲[pos.Column, pos.Column+cellWidth-1]と既存要素の占有列範囲
    /// [el.Pos.Column, el.Pos.Column+el.CellWidth-1]の交差判定に拡張する(T-071バグ修正、隠密テスト設計
    /// docs/ecad2-t071-bugfix-test-design-onmitsu.md 表1)。既定cellWidth=1は従来の単一セル一致判定と
    /// 数学的に等価(区間[c,c]同士の一致比較になるため回帰なし)。</summary>
    public bool IsSelectedCellOccupied(int cellWidth = 1)
        => SelectedCell is { } pos && CurrentSheet is Sheet sheet && IsOccupied(pos, cellWidth, sheet);

    /// <summary>SelectedCellが現在のグリッド範囲内(行0〜Rows-1・列0〜Columns-1)か判定する
    /// (T-045増分C、View層のTryPlaceElementが配置バー表示前に境界外を弾くために使う。所見B=
    /// 境界チェック未追随でのサイレント失敗の解消)。選択(SelectedCell)自体の仕様範囲(行-1・
    /// 列-2まで選択可、殿教示2026-07-07・docs/proposed.md P-022/P-024)には触れず、配置前の
    /// フィードバック用の判定に留める(殿裁定2026-07-09=下限0、選択の仕様は不変)。cellWidthの
    /// 意味はIsSelectedCellOccupiedと同じ(T-071バグ修正)。</summary>
    public bool IsSelectedCellWithinGrid(int cellWidth = 1)
        => SelectedCell is { } pos && CurrentSheet is Sheet sheet && IsWithinGridBounds(pos, cellWidth, sheet);

    private static bool IsWithinGridBounds(GridPos pos, int cellWidth, Sheet sheet)
        => pos.Row >= 0 && pos.Row < sheet.Grid.Rows
        && pos.Column >= 0 && pos.Column + cellWidth - 1 < sheet.Grid.Columns;

    private static bool IsOccupied(GridPos pos, int cellWidth, Sheet sheet)
    {
        int left = pos.Column, right = pos.Column + cellWidth - 1;
        return sheet.Elements.Any(el =>
            el.Pos.Row == pos.Row && el.Pos.Column <= right && left <= el.Pos.Column + el.CellWidth - 1);
    }

    /// <summary>指定セル位置にヒットする要素を返す(T-069往復2周目修正1、右クリックメニューの
    /// 要素判定用)。CellWidth>1(Motor等)の要素は左上アンカーセル以外の占有列もヒット対象に含める
    /// (IsOccupiedと同じ区間交差判定、T-071バグ修正の教訓をヒットテストへ再適用)。</summary>
    public ElementInstance? HitTestElement(GridPos pos)
    {
        if (CurrentSheet is not Sheet sheet) return null;
        return sheet.Elements.FirstOrDefault(el =>
            el.Pos.Row == pos.Row && el.Pos.Column <= pos.Column && pos.Column <= el.Pos.Column + el.CellWidth - 1);
    }

    /// <summary>posへの配置可否を判定する(T-045 P-025、P-021占有再チェック+P-022/P-024境界ガードの
    /// 統合)。境界外、または既に要素があればfalse。IsSelectedCellWithinGridと境界判定ロジックを
    /// 共有する(IsWithinGridBounds)。cellWidthの意味はIsSelectedCellOccupiedと同じ(T-071バグ修正)。</summary>
    private bool ValidatePlacement(GridPos pos, int cellWidth, Sheet sheet)
        => IsWithinGridBounds(pos, cellWidth, sheet) && !IsOccupied(pos, cellWidth, sheet);

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
        // T-071バグ修正: Motor(WidthCells=3)等の複数セル幅パーツに対応するため、配置するパーツの
        // WidthCellsをPartLibraryから取得する(既定1、基本図形の大半は1セル幅のまま)。
        int cellWidth = PartLibrary.Get(partId)?.WidthCells ?? 1;
        if (!ValidatePlacement(pos, cellWidth, sheet)) return;

        var newElement = new ElementInstance
        {
            Pos = pos,
            PartId = partId,
            CellWidth = cellWidth,
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

        // T-079(P-058)バグ修正: SelectedCell自体は配置前後で値が変わらないため、SelectedCellの
        // setter経由のSelectedElement系4プロパティ通知が発火されない。放置するとプロパティパネル
        // (DeviceNameBox)の表示が配置前の古い値のまま残り、配置直後にCtrl+S等でCommitDeviceNameEdit
        // (T-049)が走ると、古い表示値が誤って新要素のデバイス名としてコミットされ、機器表エントリが
        // 消失する(侍実測で確定、隠密の当初仮説=CommitEdit内部メカニズム説は棄却)。要素追加が
        // 確定した直後に必ず通知することで、isOr分岐(以降の処理)に関わらず解消する。
        NotifySelectedElementChanged();

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
        // T-051バグ修正#1(隠密レビューCONFIRMED重大): 無関係な旧文書のUndo/Redo履歴を持ち越すと、
        // 別ファイルへの切替後にUndoで旧文書の状態が復元され、それを保存すると新ファイルパスへ
        // 誤って上書きされるデータ破損事故になる。文書差し替えの入口で必ず履歴を破棄する。
        UndoManager.Clear();
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
                if (TryRejectOccupiedRow(sheet, lastRow, "最終行に要素があるため削除できません")) return;
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
                // T-055増分2往復2周目(隠密レビュー指摘、CONFIRMED): DeleteRowCommand由来の
                // 「最終行に」固定文言をそのまま流用すると、縮小範囲内の先頭・中間行で拒否された
                // 場合にユーザーが実際の元凶ではなく旧最終行付近を確認しに行き誤誘導する。
                // 拒否した実際の行番号(表示は1始まり、SelectedCellDisplayと同じ規約)を含める。
                if (TryRejectOccupiedRow(sheet, row, $"行{row + 1}に要素があるため削除できません")) return;
            }
            sheet.Grid.Rows = settings.Rows;
            sheet.Bus.LeftName = settings.LeftName;
            sheet.Bus.RightName = settings.RightName;
            FinishRowCountChange(sheet);
        },
        _ => CurrentSheet is not null);

        // T-055増分3: 右クリックコンテキストメニュー経由の任意位置挿入・削除。
        // CanExecuteはGrid.Rows上限/下限のみ判定(要素占有はAddRow/DeleteRowCommandと同じくExecute内で
        // 判定してStatusMessageへ案内する既存流儀に揃える)。
        InsertRowBeforeCommand = new RelayCommand(param =>
        {
            if (CurrentSheet is not Sheet sheet || param is not int row) return;
            if (sheet.Grid.Rows >= GridSpec.MaxRows) return;
            // 隠密レビュー指摘a(往復1周目、CONFIRMED): SelectedCellもRowOpsと同じ規則(挿入点以降は+1)で
            // 追随させる。据え置くと選択カーソルが指す座標と実要素の対応がずれ、直後の操作で誤操作を招く。
            if (SelectedCell is GridPos sc && sc.Row >= row)
                SelectedCell = sc with { Row = sc.Row + 1 };
            RowOps.InsertRow(sheet, row);
            sheet.Grid.Rows++;
            FinishRowCountChange(sheet);
        },
        param => CurrentSheet is Sheet sheet && sheet.Grid.Rows < GridSpec.MaxRows && param is int);

        DeleteRowAtCommand = new RelayCommand(param =>
        {
            if (CurrentSheet is not Sheet sheet || param is not int row) return;
            if (sheet.Grid.Rows <= GridSpec.MinRows) return;
            // T-055増分3差し込み(殿裁定2026-07-11): 占有拒否(TryRejectOccupiedRow)を撤廃し、
            // 対象行の要素(広義5種)はRowOps.DeleteRowが「要素ごと削除」する(GuiEcad同型)。
            // SelectedCellが削除対象行そのものを指す場合の据え置き(sc.Row > rowのみ-1)は現状維持
            // (家老裁定、要素ごと削除方式でも選択位置は動かさない。忍者実機確認で違和感があれば再検討)。
            //
            // T-055増分3往復1周目(隠密レビュー指摘a・b、恒久対応): RowOps.DeleteRowを先に実行して
            // sheet.Elementsを確定させてから、SelectedCellのシフト代入・SelectedElement系通知を行う
            // 順序へ変更した(指摘b=旧実装は削除前にシフト代入していたため、未シフトのsheet.Elementsで
            // SelectedElementが一時的に誤評価されていた)。加えて、削除対象行そのものを選択中
            // (sc.Row == row、SelectedCellの値自体は不変)でもNotifySelectedElementChanged()を
            // 無条件で呼ぶことで、setterの「値が変化した場合のみ通知」という性質による通知漏れ
            // (指摘a)を解消する。
            var removed = RowOps.DeleteRow(sheet, row);
            sheet.Grid.Rows--;
            CleanupRemovedDeviceNames(removed);
            if (SelectedCell is GridPos sc && sc.Row > row)
                SelectedCell = sc with { Row = sc.Row - 1 };
            NotifySelectedElementChanged();
            FinishRowCountChange(sheet);
        },
        param => CurrentSheet is Sheet sheet && sheet.Grid.Rows > GridSpec.MinRows && param is int);

        // T-051: Undo/Redo基盤(案C、殿裁定)。MVP対象範囲はSheetNavigationViewModelのシート追加/削除のみ
        // (RecordSnapshotの呼び出しはSheetNavigationViewModel.AddCommand/DeleteCommand側で行う)。
        UndoCommand = new RelayCommand(
            () =>
            {
                if (UndoManager.Undo(Document) is not LadderDocument restored) return;
                ApplyUndoRedoSnapshot(restored);
            },
            () => UndoManager.CanUndo);

        RedoCommand = new RelayCommand(
            () =>
            {
                if (UndoManager.Redo(Document) is not LadderDocument restored) return;
                ApplyUndoRedoSnapshot(restored);
            },
            () => UndoManager.CanRedo);
    }

    /// <summary>T-051往復3周目(隠密再々レビューPLAUSIBLE、docs/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md):
    /// ApplyUndoRedoSnapshotでのSelectedCell復元専用のRowクランプ。AddRowCommand等が課す
    /// FinishRowCountChangeと同じ意味論(Row>=Grid.Rowsなら末尾行へ)だが、StatusMessageクリア等の
    /// 副作用は複製しない(Undo/Redoはそれらを巻き戻さず現状維持、殿裁定)。Columnは対象外
    /// (Grid.Columnsを変更するコマンドが現状存在しないため、設計書§0.2)。</summary>
    private static GridPos? ClampSelectedCellToSheetRows(GridPos? cell, Sheet? sheet)
        => cell is GridPos pos && sheet is Sheet s && pos.Row >= s.Grid.Rows
            ? pos with { Row = s.Grid.Rows - 1 }
            : cell;

    /// <summary>Undo/Redoで復元したDocumentを反映する(T-051)。新規/開く専用のReplaceDocumentとは
    /// 意味論が異なる(SelectedCell/Tool状態/StatusMessageは巻き戻さず現状維持、殿裁定2026-07-11=
    /// シート構成のみ復元)ため、ReplaceDocumentを流用せず専用メソッドとする。Undo/Redo自体も
    /// 「変更」の一種として扱いMarkDirtyする(戻した内容が未保存である事実は変わらないため、殿裁定)。</summary>
    private void ApplyUndoRedoSnapshot(LadderDocument restored)
    {
        var oldDocument = Document;
        // T-051バグ修正#2(隠密レビューCONFIRMED): Document差し替え前=旧文書の選択状態が残る
        // この時点で捕捉する(ReplaceDocumentと同じ理由、SetCurrentSheetIndexCore後に読むと
        // 既に新しいindex・ミラーの組から誤った値になる)。
        var oldSelectedSheet = SheetNavigation.SelectedSheet;
        // T-051往復2周目(隠密再レビューCONFIRMED、docs/ecad2-t051-selectedcell-bugfix-test-design-onmitsu.md):
        // SetCurrentSheetIndexCoreは「常時無条件」でSelectedCell=nullを実行する既存仕様(T-041由来、
        // AddCommand/DeleteCommand等の複数呼び出し元が依存)のため、そのままではUndo/Redoでも
        // SelectedCellが強制クリアされ、殿裁定(SelectedCellは巻き戻さず現状維持)に反する。
        // SetCurrentSheetIndexCore本体は変えず、呼び出し前後で局所的に退避・復元する(座標値はそのまま
        // 据え置く=DeleteRowAtCommandのクランプ意味論と整合、T-055増分3)。
        var oldSelectedCell = SelectedCell;
        Document = restored;
        OnPropertyChanged(nameof(Document), oldDocument);
        SheetNavigation.ResetSheets();
        // シート数が変化しうるため、CurrentSheetIndexを新しい範囲へクランプする。
        int clampedIndex = Math.Clamp(_currentSheetIndex, 0, Math.Max(0, restored.Sheets.Count - 1));
        SetCurrentSheetIndexCore(clampedIndex);
        // 復元先シート(復元後CurrentSheet、CurrentSheetIndexクランプ確定後の値)のGrid.Rowsを
        // 基準にクランプする(T-055増分3のRowクランプ意味論と同型、設計書§0.3)。
        SelectedCell = ClampSelectedCellToSheetRows(oldSelectedCell, CurrentSheet);
        NotifyCurrentSheetChanged();
        // T-051バグ修正#2: AddCommand/DeleteCommand/RenameCommand等の既存コマンド群が律儀に発火
        // させているSelectedSheet変更通知(T-050で確立済みの不変条件)をUndo/Redoでも発火させ、
        // 左パレットのシート選択ハイライト崩れを防ぐ。意味論はクランプ位置維持(元のシートへ戻す
        // のではない、docs/ecad2-t051-bugfix-test-design-onmitsu.md §2.1)。
        SheetNavigation.RefreshSelectedSheet(oldSelectedSheet);
        // Undo/Redoが「シート0枚⇔1枚以上」の境界を跨ぐ可能性がある(最後の1枚のAdd/Deleteを取り消す場合)。
        NotifyHasProjectChanged();
        DeviceTable.Rebind(restored.Devices);
        // T-051バグ修正#4(隠密レビューCONFIRMED): ReplaceDocumentと同じ理由(T-019の教訓)で、
        // シート構成が変わった以上、旧文書に紐づくDRC結果は破棄する。放置すると存在しないページ
        // 番号を指す診断が残留し、クリック時にJumpToが無言returnする「沈黙」不整合が再発する。
        OutputPanel.ClearResults();
        MarkDirty();
    }
}
