using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>
/// T-068増分1: 自作パーツのプロパティ編集ダイアログ(名前/幅高さ/役割)。
/// T-068増分2: 端子(接続点)編集をタブ追加(リスト形式、殿裁定=案A・キャンバス上ドラッグは増分3で
/// 正式統合)。形状(Primitive)編集は増分3で別途扱う(本ダイアログのスコープ外)。
/// RenameDialog/AddSheetDialogと同型の最小モーダル。
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

    private readonly PartDefinition? _editing;
    private readonly ObservableCollection<PortRow> _portRows = new();

    /// <summary>OK確定後の結果。DialogResult==trueの場合のみ有効。</summary>
    public PartDefinition Result { get; private set; } = null!;

    /// <summary>新規作成の場合はeditにnullを渡す。編集の場合は対象のPartDefinitionを渡す
    /// (Id・IsOrEligible・Primitivesは編集対象からそのまま引き継ぐ。Portsは本ダイアログの
    /// 端子タブで編集可能)。</summary>
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

        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
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
        if (!int.TryParse(WidthBox.Text, out var width) || width < 1 || width > 12)
        {
            ShowError("幅は1〜12の整数で指定してください。");
            return;
        }
        if (!int.TryParse(HeightBox.Text, out var height) || height < 1 || height > 12)
        {
            ShowError("高さは1〜12の整数で指定してください。");
            return;
        }
        var role = RoleCombo.SelectedItem is ComboBoxItem { Tag: PartRole r } ? r : PartRole.ContactNO;

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

        Result = _editing is { } original
            ? new PartDefinition
            {
                Id = original.Id, Name = name, WidthCells = width, HeightCells = height, Role = role,
                IsOrEligible = original.IsOrEligible, Ports = ports, Primitives = original.Primitives,
            }
            : new PartDefinition { Name = name, WidthCells = width, HeightCells = height, Role = role, Ports = ports };

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
