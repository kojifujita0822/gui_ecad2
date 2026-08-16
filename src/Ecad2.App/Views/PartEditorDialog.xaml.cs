using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ecad2.Model;
using Ecad2.Rendering;

namespace Ecad2.App.Views;

/// <summary>
/// T-068増分1: 自作パーツのプロパティ編集ダイアログ(名前/幅高さ/役割)。
/// T-068増分2: 端子(接続点)編集(リスト形式、殿裁定=案A・キャンバス上ドラッグは増分3-cで正式統合)。
/// T-068増分3-a: タブ構成を廃止し、GuiEcad原本(PartEditorWindow)と同じ単一画面構成へ再設計。
/// T-068増分3-b2: 形状編集キャンバス(PartEditorCanvas)を組み込み、7ツール・Undo/Redo・ズームを
/// 使えるようにした。文字ツールは増分3-b3、接続点ツールの統合は増分3-cで扱う
/// (画面構成と原本Row0-3との対応はPartEditorDialog.xaml冒頭のコメント参照)。
/// </summary>
public partial class PartEditorDialog : Window
{
    // GuiEcad原本(PartEditorWindow.xaml RoleBox)と同一の8種・同一の日本語ラベル。ecad2はT-071/
    // T-061でPartRoleを追加拡張済みだが、本増分は隠密プラン・家老采配どおりGuiEcad原本相当の8種に
    // 限定する(残り7種を自作パーツの役割として選べるようにする要否は別途相談)。
    private static readonly (PartRole Role, string Label)[] RoleChoices =
    {
        (PartRole.ContactNO, "a接点 (NO)"),
        (PartRole.ContactNC, "b接点 (NC)"),
        (PartRole.Coil, "コイル"),
        (PartRole.Lamp, "表示灯"),
        (PartRole.Terminal, "端子台"),
        (PartRole.NonSimulated, "非シミュレート"),
        (PartRole.InputNO, "外部入力 a接点 (NO)"),
        (PartRole.InputNC, "外部入力 b接点 (NC)"),
    };

    // T-136(A)増分2: 置けるシートの種別（殿裁定2026-07-31＝3値・既定はどちらでも）。
    // RoleChoices と同じ「enum→日本語ラベル」の形。原本GuiEcadには無い、ecad2独自の設定である。
    private static readonly (SheetAffinity Affinity, string Label)[] AffinityChoices =
    {
        (SheetAffinity.Any, "どちらでも"),
        (SheetAffinity.ControlOnly, "制御回路シート専用"),
        (SheetAffinity.MainCircuitOnly, "主回路シート専用"),
    };

    // T-136(B)増分5（殿裁定2026-08-02＝案イ）: 接続点の種類。RoleChoices・AffinityChoices と同じ
    // 「enum→日本語ラベル」の形。色は増分4で決まっており（電源=赤／DRC無効=青）、ラベルにも添える
    // ——選択欄の文字だけでは、キャンバス上のどの色に対応するか分からぬため。
    private static readonly (PortKind Kind, string Label)[] PortKindChoices =
    {
        (PortKind.Power, "電源に接続される点（赤）"),
        (PortKind.DrcExempt, "DRC無効な点（青）"),
    };

    private const int MinCells = 1;
    private const int MaxCells = 12;

    /// <summary>接続点の種類欄を「表示合わせ」で更新しておる間だけ真。
    /// <para>
    /// ComboBox は <c>SelectedItem</c> をプログラムから変えても <c>SelectionChanged</c> を発火する。
    /// 選択中の接続点が切り替わった際、欄の表示を新しい接続点の現在の種類へ合わせるのだが、
    /// ガードが無ければその発火が<b>「今選んでおる接続点の種類を書き換える」操作として扱われる</b>。
    /// </para>
    /// <para>
    /// <b>弧の縦半径欄にはこの罠が無い</b>——あちらは <c>KeyDown</c>（Enter）でしか反応せぬゆえ、
    /// 表示を差し替えても何も起きぬ。<b>案イが既存の型を踏襲できぬ唯一の点がここにある</b>
    /// （隠密の指摘、2026-08-02）。
    /// </para>
    /// <para>
    /// <b>二重の守りである</b>——本フラグに加え、<see cref="PartEditorPortKindRules.ShouldApply"/> が
    /// 同値の書き込みを弾く。<b>ただし両者は別の役目を持つ</b>：本フラグは<b>表示合わせの間だけ</b>
    /// 書き込みを止めるもの、述語の同値判定は<b>誰が呼んでも</b>無意味な履歴を積ませぬためのもの。
    /// 片方が他方の言い換えではないゆえ、二つ置いてある。
    /// </para></summary>
    private bool _syncingPortKind;

    private readonly PartDefinition? _editing;

    /// <summary>OK確定後の結果。DialogResult==trueの場合のみ有効。</summary>
    public PartDefinition Result { get; private set; } = null!;

    /// <summary>新規作成の場合はeditにnullを渡す。編集の場合は対象のPartDefinitionを渡す
    /// (Id・IsOrEligibleは編集対象からそのまま引き継ぐ。Ports・Primitivesは本ダイアログで編集可能)。
    /// isDarkModeは形状編集キャンバスの配色に用いる(T-068増分3-b3、メインのラダーキャンバスと同じ
    /// テーマ追従を行う)。既定値は設けない——渡し忘れるとダークモードで白いキャンバスが出るという
    /// 実行時にしか気づけない不具合になるため、呼び出し元が増えた際にコンパイルで止める
    /// (T-068増分3-c、隠密の申し送りへの対応)。</summary>
    public PartEditorDialog(PartDefinition? edit, bool isDarkMode)
    {
        InitializeComponent();
        _editing = edit;

        foreach (var (role, label) in RoleChoices)
            RoleCombo.Items.Add(new ComboBoxItem { Content = label, Tag = role });
        foreach (var (affinity, label) in AffinityChoices)
            AffinityCombo.Items.Add(new ComboBoxItem { Content = label, Tag = affinity });
        foreach (var (kind, label) in PortKindChoices)
            PortKindCombo.Items.Add(new ComboBoxItem { Content = label, Tag = kind });

        if (edit is not null)
        {
            Title = "自作パーツ編集";
            NameBox.Text = edit.Name;
            WidthBox.Text = edit.WidthCells.ToString();
            HeightBox.Text = edit.HeightCells.ToString();
            SelectRole(edit.Role);
            SelectAffinity(edit.SheetAffinity);
            ExcludeFromCrossReferenceCheck.IsChecked = edit.IsExcludedFromCrossReference;   // T-152
        }
        else
        {
            Title = "自作パーツ新規作成";
            WidthBox.Text = "1";
            HeightBox.Text = "1";
            RoleCombo.SelectedIndex = 0;
            AffinityCombo.SelectedIndex = 0;   // 既定＝どちらでも（AffinityChoices の先頭）
            // T-152: 既定オフ。CheckBox の既定は未チェックゆえ明示は要らぬが、
            // 「既定オフが殿ご裁可の要件である」ことを読み手へ残すために書いておく。
            ExcludeFromCrossReferenceCheck.IsChecked = false;
        }

        // T-068増分3-b2: Undo/RedoのスナップショットはGuiEcad原本のEditorSnapshotと同じ5項目
        // (Prims/Ports/W/H/Role)。増分3-cで端子がキャンバスへ移ったため、キャンバスの外に残る
        // 幅・高さ・役割の3項目だけを取得・復元の委譲で扱う。
        ShapeCanvas.CaptureExternalState = CaptureExternalState;
        ShapeCanvas.RestoreExternalState = RestoreExternalState;

        // T-068増分3-b2(家老采配DoD4): 編集対象の内容はコピーして渡す。素通しにすると
        // キャンバス上の編集が呼び出し元のPartDefinition(PartLibrary内の実体と同一参照)を直接
        // 書き換えてしまい、キャンセルしても元へ戻らなくなる。
        ShapeCanvas.LoadContent(
            edit?.Primitives ?? Enumerable.Empty<PartPrimitive>(),
            edit?.Ports ?? Enumerable.Empty<PortDef>());
        ShapeCanvas.WidthCells = ParseCells(WidthBox.Text, MinCells);
        ShapeCanvas.HeightCells = ParseCells(HeightBox.Text, MinCells);
        ShapeCanvas.Theme = isDarkMode ? DrawingTheme.Dark : DrawingTheme.Default;
        ShapeCanvas.RequestText = AskShapeText;
        ShapeCanvas.StateChanged += (_, _) => UpdateShapeStatus();

        // T-144: 変更判定の基準を、コンストラクタで入力欄を組み終えた時点に据える。
        // ここより前は _lastRecordedExternal が null にて、項目構築中に発火する
        // SelectionChanged（RoleCombo.SelectedIndex への代入等）では履歴を積まぬ。
        _lastRecordedExternal = CaptureExternalState();

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
            ShapeCanvas.Draw();
            UpdateShapeStatus();
        };
    }

    private void SelectRole(PartRole role)
    {
        foreach (ComboBoxItem item in RoleCombo.Items)
        {
            if (item.Tag is PartRole r && r == role) { RoleCombo.SelectedItem = item; return; }
        }
        RoleCombo.SelectedIndex = 0;   // ecad2拡張Role(タイマ系等)で編集に入った場合のフォールバック
    }

    private PartRole SelectedRole()
        => RoleCombo.SelectedItem is ComboBoxItem { Tag: PartRole r } ? r : PartRole.ContactNO;

    private void SelectAffinity(SheetAffinity affinity)
    {
        foreach (ComboBoxItem item in AffinityCombo.Items)
        {
            if (item.Tag is SheetAffinity a && a == affinity) { AffinityCombo.SelectedItem = item; return; }
        }
        AffinityCombo.SelectedIndex = 0;   // 将来 SheetAffinity が増えた場合のフォールバック（SelectRoleと同じ流儀）
    }

    private SheetAffinity SelectedAffinity()
        => AffinityCombo.SelectedItem is ComboBoxItem { Tag: SheetAffinity a } ? a : SheetAffinity.Any;

    /// <summary>セル数の入力欄を読む。入力途中の空文字・不正値では既定値を返す(例外を投げない)。</summary>
    private static int ParseCells(string text, int fallback)
        => int.TryParse(text, out var v) && v >= MinCells && v <= MaxCells ? v : fallback;

    // ===== 形状編集キャンバス(T-068増分3-b2) =====

    /// <summary>基準枠(外形枠)を幅・高さの入力へ即時連動させる。</summary>
    private void SizeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ShapeCanvas.WidthCells = ParseCells(WidthBox.Text, ShapeCanvas.WidthCells);
        ShapeCanvas.HeightCells = ParseCells(HeightBox.Text, ShapeCanvas.HeightCells);
    }

    private void Tool_Checked(object sender, RoutedEventArgs e)
    {
        if (ShapeCanvas is null) return;   // InitializeComponent中のIsChecked="True"で先に発火しうる
        if (sender is not RadioButton { Tag: string tag }) return;
        ShapeCanvas.Tool = tag switch
        {
            "Line" => PartEditTool.Line,
            "Polyline" => PartEditTool.Polyline,
            "Rect" => PartEditTool.Rect,
            "Circle" => PartEditTool.Circle,
            "Arc" => PartEditTool.Arc,
            "Rotate" => PartEditTool.Rotate,
            "Text" => PartEditTool.Text,
            "Port" => PartEditTool.Port,
            _ => PartEditTool.Select,
        };
        UpdateShapeStatus();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        ShapeCanvas.Undo();
        ShapeCanvas.Focus();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        ShapeCanvas.Redo();
        ShapeCanvas.Focus();
    }

    private void DeleteShape_Click(object sender, RoutedEventArgs e)
    {
        ShapeCanvas.DeleteSelected();
        ShapeCanvas.Focus();
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        ShapeCanvas.Zoom = 1.0;
        ShapeCanvas.Focus();
    }

    /// <summary>文字ツールで配置する文字列を入力してもらう（キャンセル・空入力ならnull）。</summary>
    private string? AskShapeText()
    {
        var dialog = new PartTextInputDialog { Owner = this };
        return dialog.ShowDialog() == true ? dialog.InputText : null;
    }

    private void ArcRyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (double.TryParse(ArcRyBox.Text, out var ry)) ShapeCanvas.SetSelectedArcRy(ry);
        ShapeCanvas.Focus();
        e.Handled = true;   // IsDefault="True"のOKボタンが発火してダイアログが閉じるのを防ぐ
    }

    /// <summary>接続点の種類が選ばれたら即座に反映する（T-136(B)増分5、案イ）。
    /// 表示合わせ中の発火は <see cref="_syncingPortKind"/> で弾く。</summary>
    private void PortKindCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPortKind) return;
        if (PortKindCombo.SelectedItem is not ComboBoxItem { Tag: PortKind kind }) return;
        ShapeCanvas.SetSelectedPortKind(kind);
    }

    /// <summary>接続点の種類欄の表示を、選択中の接続点の現在の値へ合わせる。
    /// <b>この操作自体が <c>SelectionChanged</c> を呼ぶ</b>ゆえ、必ずガードで包む。</summary>
    private void SyncPortKindCombo(PortKind kind)
    {
        _syncingPortKind = true;
        try
        {
            foreach (ComboBoxItem item in PortKindCombo.Items)
            {
                if (item.Tag is PortKind k && k == kind) { PortKindCombo.SelectedItem = item; return; }
            }
            PortKindCombo.SelectedIndex = 0;   // 将来 PortKind が増えた場合のフォールバック（SelectRoleと同じ流儀）
        }
        finally
        {
            _syncingPortKind = false;   // 途中return・例外のいずれでも必ず戻す
        }
    }

    private void UpdateShapeStatus()
    {
        if (ShapeCanvas.SelectedArc is { } arc)
        {
            ArcRyLabel.Visibility = Visibility.Visible;
            ArcRyBox.Visibility = Visibility.Visible;
            if (!ArcRyBox.IsFocused) ArcRyBox.Text = arc.EffRy.ToString("0.###");
        }
        else
        {
            ArcRyLabel.Visibility = Visibility.Collapsed;
            ArcRyBox.Visibility = Visibility.Collapsed;
        }

        if (ShapeCanvas.SelectedPort is { } port)
        {
            PortKindLabel.Visibility = Visibility.Visible;
            PortKindCombo.Visibility = Visibility.Visible;
            SyncPortKindCombo(port.Kind);
        }
        else
        {
            PortKindLabel.Visibility = Visibility.Collapsed;
            PortKindCombo.Visibility = Visibility.Collapsed;
        }

        UndoButton.IsEnabled = ShapeCanvas.CanUndo;
        RedoButton.IsEnabled = ShapeCanvas.CanRedo;
        DeleteShapeButton.IsEnabled = ShapeCanvas.SelectedIndex >= 0 || ShapeCanvas.SelectedPortIndex >= 0;

        // 図形数・接続点数はGuiEcad原本のステータステキストが持っていた項目（接続点数は増分3-cで追加）。
        StatusText.Text = $"図形: {ShapeCanvas.Primitives.Count}個 / 接続点: {ShapeCanvas.Ports.Count}個"
            + $" / 表示倍率: {ShapeCanvas.Zoom:0.00}倍"
            + $" / ツール: {ToolLabel(ShapeCanvas.Tool)} - {ToolGuide(ShapeCanvas.Tool)}";
    }

    private static string ToolLabel(PartEditTool tool) => tool switch
    {
        PartEditTool.Line => "線",
        PartEditTool.Polyline => "折れ線",
        PartEditTool.Rect => "矩形",
        PartEditTool.Circle => "円",
        PartEditTool.Arc => "弧",
        PartEditTool.Rotate => "回転",
        PartEditTool.Text => "文字",
        PartEditTool.Port => "接続点",
        _ => "選択",
    };

    /// <summary>ツールごとの操作ガイド（GuiEcad原本のステータステキストが持っていた動的ガイダンス）。
    /// 原本がガイドを出していたのは折れ線・弧・回転の3つ。それ以外のツールでは代わりに
    /// ecad2独自のズーム・パン操作の案内を出す（案内が二重に並んで読みづらくなるのを避ける）。</summary>
    private static string ToolGuide(PartEditTool tool) => tool switch
    {
        PartEditTool.Polyline => "クリックで頂点を追加し、右クリックで確定します",
        PartEditTool.Arc => "外接する矩形をドラッグします。描いた後は下の欄で縦半径を変えられます",
        PartEditTool.Rotate => "図形をドラッグすると15度きざみで回ります",
        PartEditTool.Text => "クリックした位置に文字を置きます",
        PartEditTool.Port => "クリックで接続点を置きます。移動・削除は選択ツールで行います",
        _ => "Ctrl+ホイールで拡大縮小、中ボタンのドラッグで移動",
    };

    private PartEditorExternalState CaptureExternalState() => new(
        ParseCells(WidthBox.Text, MinCells),
        ParseCells(HeightBox.Text, MinCells),
        SelectedRole(),
        SelectedAffinity(),
        ExcludeFromCrossReferenceCheck.IsChecked == true);   // T-152

    private void RestoreExternalState(PartEditorExternalState state)
    {
        // T-144: Undo/Redo による復元中は、入力欄の書き換えが LostFocus/SelectionChanged を
        // 誘発しても履歴を積まぬ。これを怠ると Undo の最中に新しい履歴が生まれる（再入）。
        _restoringExternalState = true;
        try
        {
            WidthBox.Text = state.WidthCells.ToString();
            HeightBox.Text = state.HeightCells.ToString();
            SelectRole(state.Role);
            SelectAffinity(state.SheetAffinity);
            ExcludeFromCrossReferenceCheck.IsChecked = state.IsExcludedFromCrossReference;   // T-152
        }
        finally
        {
            _restoringExternalState = false;   // 途中return・例外のいずれでも必ず戻す
        }

        // 復元後の状態が、次の変更判定の基準になる。
        _lastRecordedExternal = state;
    }

    // ===== T-144: 入力欄（幅・高さ・役割・シート種別）の変更を Undo 対象にする =====
    // （殿ご裁可2026-08-02＝「undo対象に含めて」／積む契機は案A＝TextBox は LostFocus）
    //
    // 【原本からの意図的な逸脱である】原本 GuiEcad は入力欄の直接変更を Undo 対象にしていない
    // （PartEditorWindow.xaml.cs:795 のコメントより、サイズ・役割がスナップショットに入るのは
    // LoadTemplate がそれらを変えるからであって、入力欄の直接変更を対象にする意図は無い）。
    // 「配置先を変えたのに Undo で戻せぬのは使い勝手として違和感がある」という所見を添えて諮り、
    // 殿がご裁定なされたもの。後の者が「原本ではどうか」を根拠に差し戻さぬよう、ここに残す。
    // 判定そのものは PartEditorUndoRules.ShouldRecord（純粋関数・単体テスト済み）が持つ。

    /// <summary>Undo/Redo による外部状態の復元中か。復元が誘発する入力欄イベントで履歴を積まぬための門。
    /// <see cref="_syncingPortKind"/> と同じ役目を、外部状態（幅・高さ・役割・シート種別）に対して負う。
    /// <para>
    /// <b>【本フラグが守れる範囲は、イベントが同期発火することに依存する】</b>
    /// <c>WidthBox.Text</c> への代入や <c>ComboBox.SelectedItem</c> の差し替えが
    /// <c>LostFocus</c>／<c>SelectionChanged</c> を<b>その場で</b>呼ぶからこそ、<c>try/finally</c> の
    /// 内側で受け止められる。<b>もし将来これらが遅延発火する形（Dispatcher 経由等）へ変われば、
    /// フラグを戻した後に発火してこの守りは素通しになる。</b>
    /// そのときは下の <see cref="_lastRecordedExternal"/> による第二の守りだけが残る。
    /// </para></summary>
    private bool _restoringExternalState;

    /// <summary>直近に履歴へ積んだ（または復元した）時点の外部状態。次の変更を判ずる基準。
    /// <b>「フォーカスを得た時点」ではなくこれを基準にする理由</b>：ComboBox は
    /// <c>GotFocus</c> を経ずに値が変わりうる（ドロップダウンからの選択・キーボード操作）ゆえ、
    /// 契機ごとに基準を取り直す形では取りこぼす。
    /// <para>
    /// <b>【第二の守りでもある】</b><see cref="RestoreExternalState"/> の末尾でこれを復元後の値へ
    /// 更新するため、仮に上のフラグを抜けてイベントが発火しても、判定は同値となり履歴は積まれぬ。
    /// </para>
    /// <para>
    /// <b>【T-136(B)増分5 との違い——「同じ形」と読まぬこと】</b>
    /// あちら（<see cref="_syncingPortKind"/> ＋ <see cref="PartEditorPortKindRules.ShouldApply"/>）も
    /// 二層で守るが、<b>第二の守りの機序が別物である</b>。増分5の第二の守りは
    /// <b>WPF の ComboBox 標準挙動</b>（値が変わらねば <c>SelectionChanged</c> がそもそも発火せぬ）に
    /// 支えられているのに対し、こちらは<b>独自のステート管理</b>（本フィールドの更新）に支えられている。
    /// <b>4項目を一括で復元するという操作の性質上この形にならざるを得ぬ</b>が、
    /// 「同じ形ゆえ同じように直せる」と読めば誤る（隠密の静的レビューによる指摘、2026-08-02）。
    /// </para></summary>
    private PartEditorExternalState? _lastRecordedExternal;

    /// <summary>幅・高さの欄からフォーカスが外れたとき、変更を1段の履歴として積む。
    /// <para>
    /// <b>不正値（空文字・範囲外）で離れた場合は、表示を直前の有効値へ戻して打ち切る</b>——
    /// <see cref="ParseCells"/> は不正値で既定値へ落ちるため、放置すると「欄は空なのに内部値は1」という
    /// 食い違いが残り、その状態が履歴へ積まれる。
    /// <b>戻した後は判定へ進まず打ち切る</b>——戻した値は <c>before</c> そのものゆえ判定しても同値で
    /// 積まれぬが、判定を通す意味が無い。
    /// <b>OK 確定時の検証（範囲外を弾く）は従来どおり残る</b>——あちらは値の妥当性、こちらは表示と内部値の一致が目的。
    /// </para></summary>
    private void SizeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_restoringExternalState || _lastRecordedExternal is not { } before) return;
        if (sender is not TextBox box) return;

        // 不正値なら表示を直前の有効値へ戻す（内部値との食い違いを残さない）。
        bool valid = int.TryParse(box.Text, out var v) && v >= MinCells && v <= MaxCells;
        if (!valid)
        {
            box.Text = (ReferenceEquals(box, WidthBox) ? before.WidthCells : before.HeightCells).ToString();
            return;
        }

        RecordExternalStateChangeIfAny(before);
    }

    /// <summary>役割・配置先のコンボが変わったとき、変更を1段の履歴として積む。</summary>
    private void ExternalStateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // InitializeComponent 中・項目の構築中は _lastRecordedExternal がまだ無い。
        if (_restoringExternalState || _lastRecordedExternal is not { } before) return;
        RecordExternalStateChangeIfAny(before);
    }

    /// <summary>クロスリファレンス除外のチェックが変わったとき、変更を1段の履歴として積む（T-152）。
    /// <para>
    /// コンボと処理は同じにて、<c>Checked</c>／<c>Unchecked</c> の二つのイベントを一つの口で受ける。
    /// </para></summary>
    private void ExternalStateCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_restoringExternalState || _lastRecordedExternal is not { } before) return;
        RecordExternalStateChangeIfAny(before);
    }

    /// <summary>現在の外部状態が <paramref name="before"/> から変わっていれば、変更前を履歴へ積む。</summary>
    private void RecordExternalStateChangeIfAny(PartEditorExternalState before)
    {
        var now = CaptureExternalState();
        if (!PartEditorUndoRules.ShouldRecord(before, now)) return;

        ShapeCanvas.PushExternalStateSnapshot(before);
        _lastRecordedExternal = now;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError("名前は必須です。");
            return;
        }
        if (!int.TryParse(WidthBox.Text, out var width) || width < MinCells || width > MaxCells)
        {
            ShowError("幅は1〜12の整数で指定してください。");
            return;
        }
        if (!int.TryParse(HeightBox.Text, out var height) || height < MinCells || height > MaxCells)
        {
            ShowError("高さは1〜12の整数で指定してください。");
            return;
        }
        var role = SelectedRole();

        // T-068増分2(GuiEcad原本OnSave 925-928行踏襲): ポート2点未満はNonSimulated以外拒否。
        // 増分3-cで端子の編集UIはキャンバス上の接続点ツールへ移ったが、保存時のこの検査自体は
        // 変わらず要る(隠密設計書§3.6)。
        if (ShapeCanvas.Ports.Count < 2 && role != PartRole.NonSimulated)
        {
            ShowError("接続点を2つ以上配置してください。ツールバーの「接続点」で置けます。");
            return;
        }

        // T-068増分3-c(殿裁定2026-07-25): 並べ替えの前に基準枠の範囲内へ正規化する。編集中に枠を
        // 縮めても接続点は原本どおり動かさず、保存時にのみ正規化する方式(MergeCollinearLinesと同じ流儀)。
        var clampedPorts = PartOptimizer.ClampPortsToFrame(ShapeCanvas.Ports, width, height);

        // T-126(P-129、殿裁可2026-07-27): 上のクランプは複数の接続点を同一座標・同一境界へ潰しうる。
        // 潰れたまま保存すると誤結線を生むため、並べ替えより前に弾く。検証は二本立て——
        // (A)完全同一座標の重複、(B)全接続点が同一境界=左右の縮退。行が違えば(B)は(A)をすり抜けるが
        // 実害は同じ(要素の左右が1点へ潰れ、左右のネットが繋がる)ことを侍の実測で確かめている。
        // 文言が「基準枠に収めると」で始まるのは、判定の土俵が編集中の見た目とずれているため
        // (P-133、忍者の実機確認2026-07-27)。殿が見ておられるのはクランプ*前*の姿だが、判定は
        // クランプ*後*の座標で行う——枠外の接続点が枠内へ寄って初めて重なるので、「離れて見えるのに
        // 重なっていると出る」ことになる。判定の土俵自体は殿裁定(保存時のみクランプ)ゆえ動かせぬため、
        // 文言の側で隔たりを埋める。
        if (PartOptimizer.HasDuplicatePorts(clampedPorts))
        {
            ShowError("基準枠に収めると接続点が重なります。基準枠を広げるか、接続点をずらしてください。");
            return;
        }
        if (PartOptimizer.AllPortsOnSameBoundary(clampedPorts))
        {
            ShowError("基準枠に収めると接続点がすべて同じ左右位置になります。左右に分けて配置してください。");
            return;
        }

        // T-068増分2(GuiEcad原本OnSave 939行踏襲): 先頭=NetA・末尾=NetBの規約でBoundaryOffset昇順に
        // 並べ替えてから保存する。
        //
        // 【並べ替えはクランプより後に行うこと】クランプは複数の接続点を同一のBoundaryOffsetへ
        // 潰しうる(Math.Clampは単調非減少ゆえ大小関係自体は保たれるが、同値への収束は起きる)。
        // OrderByは安定ソートゆえ同値どうしの並びは入力順のまま残るが、その「入力順」がクランプの
        // 前か後かで変わってしまう——例えば[A(境界5), B(境界3)]を幅2でクランプすると両方が2になり、
        // この順序なら[A,B]、逆順に処理すると[B,A]となる。並べ替えは先頭=NetA・末尾=NetBの
        // 規約を作る処理ゆえ、この差は「どちらの接続点がNetAになるか」という電気的意味の差になる。
        // なお上のT-126検証Bにより「全点が同一境界」は保存前に弾かれるが、3点以上で一部だけが
        // 同値へ潰れる場合(残りが別の境界に載る場合)は保存が通るため、この順序は依然効いている。
        // 回帰テスト: PartOptimizerClampPortsTests.ClampBeforeOrderBy_PortsCollapsingToSameBoundary_KeepsCanvasOrder
        var ports = clampedPorts.OrderBy(p => p.BoundaryOffset).ToList();

        // T-068増分3-b2: 形状はキャンバスの編集結果を新しいリストとして取り出す(キャンバス内部の
        // リストとも切り離す)。
        // T-068増分3-b3(家老裁可7、GuiEcad原本OnSave 940行踏襲): 保存直前にのみ直線マージを掛ける。
        // 編集中のプリミティブ自体は変えない(MergeCollinearLinesは新しいリストを返すため、
        // キャンセルした場合はもちろん、OK後もキャンバス側の並びは影響を受けない)。
        var primitives = PartOptimizer.MergeCollinearLines(ShapeCanvas.Primitives);

        var affinity = SelectedAffinity();   // T-136(A)増分2
        bool excludedFromXref = ExcludeFromCrossReferenceCheck.IsChecked == true;   // T-152
        Result = _editing is { } original
            ? new PartDefinition
            {
                Id = original.Id, Name = name, WidthCells = width, HeightCells = height, Role = role,
                SheetAffinity = affinity, IsExcludedFromCrossReference = excludedFromXref,
                IsOrEligible = original.IsOrEligible, Ports = ports, Primitives = primitives,
            }
            : new PartDefinition
            {
                Name = name, WidthCells = width, HeightCells = height, Role = role,
                SheetAffinity = affinity, IsExcludedFromCrossReference = excludedFromXref,
                Ports = ports, Primitives = primitives,
            };

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
