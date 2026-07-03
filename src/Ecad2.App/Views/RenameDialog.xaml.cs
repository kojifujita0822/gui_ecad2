using System.Windows;

namespace Ecad2.App.Views;

/// <summary>シート名変更用の最小モーダルダイアログ（design-brief 4節#4: 非ネスト方針、単一階層のみ）。</summary>
public partial class RenameDialog : Window
{
    public string NewName { get; private set; } = "";

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        NewName = NameBox.Text;
        DialogResult = true;
    }
}
