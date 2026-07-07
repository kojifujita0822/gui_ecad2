using System.Windows;

namespace Ecad2.App.Views;

/// <summary>シート追加時の名前・種別(制御回路/主回路)選択ダイアログ(T-041、殿裁定「案1」、
/// 忍者範囲外検出=UIから主回路シートを作る手段が無かった問題への対処)。RenameDialogと同型の
/// 最小モーダル(design-brief 4節#4: 非ネスト方針、単一階層のみ)。</summary>
public partial class AddSheetDialog : Window
{
    public string SheetName { get; private set; } = "";
    public bool IsMainCircuit { get; private set; }

    public AddSheetDialog(string defaultName)
    {
        InitializeComponent();
        NameBox.Text = defaultName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        SheetName = NameBox.Text;
        IsMainCircuit = MainCircuitRadio.IsChecked == true;
        DialogResult = true;
    }
}
