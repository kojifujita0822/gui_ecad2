using System.Windows;
using Ecad2.Persistence;

namespace Ecad2.App.Views;

/// <summary>
/// 要素配置(T-026段階4)の浮動インライン入力ダイアログ。種別(基本図形)とデバイス名を入力する。
/// GX Works3の「回路入力ダイアログ」相当(design-brief 10節、SH-081214マニュアル調査に基づく)。
/// 「拡張表示」ボタンは要件が確定していないため今回は未実装(将来タスク、家老へ確認済み)。
/// </summary>
public partial class ElementPlacementDialog : Window
{
    public string? SelectedPartId { get; private set; }
    public string DeviceName { get; private set; } = "";

    public ElementPlacementDialog(IReadOnlyList<PartFolderEntry> basicEntries, string initialPartId)
    {
        InitializeComponent();
        PartComboBox.ItemsSource = basicEntries;
        PartComboBox.SelectedItem = basicEntries.FirstOrDefault(e => e.Definition.Id == initialPartId)
            ?? basicEntries.FirstOrDefault();
        Loaded += (_, _) =>
        {
            DeviceNameBox.Focus();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (PartComboBox.SelectedItem is not PartFolderEntry entry) return;
        SelectedPartId = entry.Definition.Id;
        DeviceName = DeviceNameBox.Text.Trim();
        DialogResult = true;
    }
}
