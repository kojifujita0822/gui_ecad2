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

    private const int MinCells = 1;
    private const int MaxCells = 12;

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

        if (edit is not null)
        {
            Title = "自作パーツ編集";
            NameBox.Text = edit.Name;
            WidthBox.Text = edit.WidthCells.ToString();
            HeightBox.Text = edit.HeightCells.ToString();
            SelectRole(edit.Role);
        }
        else
        {
            Title = "自作パーツ新規作成";
            WidthBox.Text = "1";
            HeightBox.Text = "1";
            RoleCombo.SelectedIndex = 0;
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
        SelectedRole());

    private void RestoreExternalState(PartEditorExternalState state)
    {
        WidthBox.Text = state.WidthCells.ToString();
        HeightBox.Text = state.HeightCells.ToString();
        SelectRole(state.Role);
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
        if (PartOptimizer.HasDuplicatePorts(clampedPorts))
        {
            ShowError("接続点が重なっています。基準枠を広げるか、接続点をずらしてください。");
            return;
        }
        if (PartOptimizer.AllPortsOnSameBoundary(clampedPorts))
        {
            ShowError("接続点がすべて同じ左右位置にあります。左右に分けて配置してください。");
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

        Result = _editing is { } original
            ? new PartDefinition
            {
                Id = original.Id, Name = name, WidthCells = width, HeightCells = height, Role = role,
                IsOrEligible = original.IsOrEligible, Ports = ports, Primitives = primitives,
            }
            : new PartDefinition
            {
                Name = name, WidthCells = width, HeightCells = height, Role = role,
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
