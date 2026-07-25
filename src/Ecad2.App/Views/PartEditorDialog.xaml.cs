using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Ecad2.Model;

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
    private readonly ObservableCollection<PortRow> _portRows = new();

    /// <summary>OK確定後の結果。DialogResult==trueの場合のみ有効。</summary>
    public PartDefinition Result { get; private set; } = null!;

    /// <summary>新規作成の場合はeditにnullを渡す。編集の場合は対象のPartDefinitionを渡す
    /// (Id・IsOrEligibleは編集対象からそのまま引き継ぐ。Ports・Primitivesは本ダイアログで編集可能)。</summary>
    public PartEditorDialog(PartDefinition? edit)
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
            foreach (var port in edit.Ports)
                _portRows.Add(new PortRow { Name = port.Name, RowOffset = port.RowOffset, BoundaryOffset = port.BoundaryOffset });
        }
        else
        {
            Title = "自作パーツ新規作成";
            WidthBox.Text = "1";
            HeightBox.Text = "1";
            RoleCombo.SelectedIndex = 0;
        }

        PortsGrid.ItemsSource = _portRows;

        // T-068増分3-b2: Undo/RedoのスナップショットにPorts/W/H/Roleも含める(GuiEcad原本の
        // EditorSnapshotと同じ5項目)。端子・幅高さ・役割は本ダイアログが管理しているため、
        // キャンバスからは取得・復元を委譲してもらう形にする。
        ShapeCanvas.CaptureExternalState = CaptureExternalState;
        ShapeCanvas.RestoreExternalState = RestoreExternalState;

        // T-068増分3-b2(家老采配DoD4): 編集対象のPrimitivesはコピーして渡す。素通しにすると
        // キャンバス上の編集が呼び出し元のPartDefinition(PartLibrary内の実体と同一参照)を直接
        // 書き換えてしまい、キャンセルしても元へ戻らなくなる。
        ShapeCanvas.LoadPrimitives(edit?.Primitives ?? Enumerable.Empty<PartPrimitive>());
        ShapeCanvas.WidthCells = ParseCells(WidthBox.Text, MinCells);
        ShapeCanvas.HeightCells = ParseCells(HeightBox.Text, MinCells);
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
        DeleteShapeButton.IsEnabled = ShapeCanvas.SelectedIndex >= 0;

        StatusText.Text = $"図形: {ShapeCanvas.Primitives.Count}個 / 表示倍率: {ShapeCanvas.Zoom:0.00}倍 "
            + "/ Ctrl+ホイールで拡大縮小、中ボタンのドラッグで移動";
    }

    private PartEditorExternalState CaptureExternalState() => new(
        _portRows.Select(r => new PortDef(r.Name, r.RowOffset, r.BoundaryOffset)).ToList(),
        ParseCells(WidthBox.Text, MinCells),
        ParseCells(HeightBox.Text, MinCells),
        SelectedRole());

    private void RestoreExternalState(PartEditorExternalState state)
    {
        _portRows.Clear();
        foreach (var port in state.Ports)
            _portRows.Add(new PortRow { Name = port.Name, RowOffset = port.RowOffset, BoundaryOffset = port.BoundaryOffset });
        WidthBox.Text = state.WidthCells.ToString();
        HeightBox.Text = state.HeightCells.ToString();
        SelectRole(state.Role);
    }

    // ===== 端子(T-068増分2、増分3-cでキャンバス上の接続点ツールへ統合予定) =====

    // T-068増分2: GuiEcad原本AddPort(自動命名"P{count+1}")踏襲。RowOffset/BoundaryOffsetは0から
    // 開始、DataGrid上でそのままインライン編集する想定。
    private void AddPortButton_Click(object sender, RoutedEventArgs e)
    {
        _portRows.Add(new PortRow { Name = $"P{_portRows.Count + 1}", RowOffset = 0, BoundaryOffset = 0 });
    }

    private void DeletePortButton_Click(object sender, RoutedEventArgs e)
    {
        if (PortsGrid.SelectedItem is PortRow row) _portRows.Remove(row);
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

        // T-068増分2: DataGridのインライン編集中セルは、他コントロール(OKボタン)へフォーカスが
        // 移るまでItemsSource側へコミットされない場合があるため、バリデーション・保存前に明示的に
        // 確定させる(WPF DataGridの既知の罠)。
        PortsGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        PortsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        // T-068増分2(GuiEcad原本OnSave 925-928行踏襲): ポート2点未満はNonSimulated以外拒否。
        if (_portRows.Count < 2 && role != PartRole.NonSimulated)
        {
            ShowError("接続点(ポート)を2つ以上配置してください。");
            return;
        }

        // T-068増分2(GuiEcad原本OnSave 939行踏襲): 先頭=NetA・末尾=NetBの規約でBoundaryOffset昇順に
        // 並べ替えてから保存する。
        var ports = _portRows
            .Select(r => new PortDef(r.Name, r.RowOffset, r.BoundaryOffset))
            .OrderBy(p => p.BoundaryOffset)
            .ToList();

        // T-068増分3-b2: 形状はキャンバスの編集結果を新しいリストとして取り出す(キャンバス内部の
        // リストとも切り離す)。MergeCollinearLinesの適用は増分3-b3で扱う。
        var primitives = ShapeCanvas.Primitives.ToList();

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

    // T-068増分2: DataGrid行の編集用DTO。PortDef自体はreadonly record structのためDataGridの
    // 直接バインディング(セル編集の書き戻し)には使えず、可変プロパティを持つラッパーを介する。
    private sealed class PortRow
    {
        public string Name { get; set; } = "";
        public int RowOffset { get; set; }
        public int BoundaryOffset { get; set; }
    }
}
