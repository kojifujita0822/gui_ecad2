using System.Windows;
using System.Windows.Controls;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>
/// T-068増分1: 自作パーツのプロパティ編集ダイアログ(名前/幅高さ/役割)。GuiEcad原本
/// (PartEditorWindow)の上段プロパティ部分に相当、形状(Primitive)・端子(Port)編集は増分3・増分2
/// で別途扱う(本ダイアログのスコープ外)。RenameDialog/AddSheetDialogと同型の最小モーダル。
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

    /// <summary>OK確定後の結果。DialogResult==trueの場合のみ有効。</summary>
    public PartDefinition Result { get; private set; } = null!;

    /// <summary>新規作成の場合はeditにnullを渡す。編集の場合は対象のPartDefinitionを渡す
    /// (Id・Ports・Primitives・IsOrEligibleは編集対象からそのまま引き継ぎ、本ダイアログでは
    /// 名前・幅高さ・役割のみ変更可能)。</summary>
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
        }
        else
        {
            Title = "自作パーツ新規作成";
            WidthBox.Text = "1";
            HeightBox.Text = "1";
            RoleCombo.SelectedIndex = 0;
        }

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

        Result = _editing is { } original
            ? new PartDefinition
            {
                Id = original.Id, Name = name, WidthCells = width, HeightCells = height, Role = role,
                IsOrEligible = original.IsOrEligible, Ports = original.Ports, Primitives = original.Primitives,
            }
            : new PartDefinition { Name = name, WidthCells = width, HeightCells = height, Role = role };

        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
