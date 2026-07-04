using System.Windows;

namespace T021EnterPlacementPoc;

public partial class NameDialog : Window
{
    public string DeviceName { get; private set; } = "";

    public NameDialog()
    {
        InitializeComponent();
        // 本体 ElementPlacementDialog.xaml.cs:22-25 と同じく Loaded で入力欄へフォーカス。
        Loaded += (_, _) => DeviceNameBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DeviceName = DeviceNameBox.Text.Trim();
        DialogResult = true;
    }

    /// <summary>自動ループ(無人検証)用にOK確定を実行する。手動時はOKボタン/Enterで同じ結果になる。</summary>
    public void ConfirmForAutomation()
    {
        DeviceName = DeviceNameBox.Text.Trim();
        DialogResult = true;
    }
}
