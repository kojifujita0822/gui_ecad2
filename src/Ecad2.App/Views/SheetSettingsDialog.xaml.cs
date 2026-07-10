using System.Windows;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>シート設定(行数・母線名)変更用のモーダルダイアログ(T-055増分2、RenameDialog/
/// AddSheetDialogと同型、design-brief 4節#4: 非ネスト方針、単一階層のみ)。</summary>
public partial class SheetSettingsDialog : Window
{
    public int Rows { get; private set; }
    public string LeftName { get; private set; } = "";
    public string RightName { get; private set; } = "";

    public SheetSettingsDialog(int currentRows, string currentLeftName, string currentRightName)
    {
        InitializeComponent();
        RowsBox.Text = currentRows.ToString();
        LeftNameBox.Text = currentLeftName;
        RightNameBox.Text = currentRightName;
        Loaded += (_, _) =>
        {
            RowsBox.Focus();
            RowsBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RowsBox.Text, out int rows) || rows < GridSpec.MinRows || rows > GridSpec.MaxRows)
        {
            RowsErrorText.Visibility = Visibility.Visible;
            return;
        }
        Rows = rows;
        // Bus名は空文字を許容する(殿裁定、GuiEcad踏襲)ためバリデーション不要。
        LeftName = LeftNameBox.Text;
        RightName = RightNameBox.Text;
        DialogResult = true;
    }
}
