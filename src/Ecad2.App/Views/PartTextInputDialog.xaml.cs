using System.Windows;

namespace Ecad2.App.Views;

/// <summary>T-068増分3-b3: 形状編集キャンバスの文字ツールで配置する文字列を入力する最小モーダル
/// （RenameDialog/AddSheetDialogと同型）。詳細はPartTextInputDialog.xaml冒頭のコメント参照。</summary>
public partial class PartTextInputDialog : Window
{
    /// <summary>OK確定後の入力文字列。DialogResult==trueの場合のみ有効。</summary>
    public string InputText { get; private set; } = "";

    public PartTextInputDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TextBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        // 空文字のまま確定しても意味のある図形にならないため、そのまま閉じさせない
        // （キャンセルしたい場合はキャンセルボタン・Escを使う）。
        if (TextBox.Text.Length == 0) return;

        InputText = TextBox.Text;
        DialogResult = true;
    }
}
