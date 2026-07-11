using System.Windows;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>ドキュメント情報(T-065)編集用のモーダルダイアログ(RenameDialog/SheetSettingsDialog
/// と同型、design-brief 4節#4: 非ネスト方針、単一階層のみ)。Revisions(改定履歴)は編集対象外
/// (殿裁定2026-07-12)のため本ダイアログには含めない。</summary>
public partial class DocumentInfoDialog : Window
{
    public DocumentInfo Result { get; } = new();

    public DocumentInfoDialog(DocumentInfo current)
    {
        InitializeComponent();
        CompanyNameBox.Text = current.CompanyName;
        TitleBox.Text = current.Title;
        DrawingNoBox.Text = current.DrawingNo;
        CustomerBox.Text = current.Customer;
        DesignerBox.Text = current.Designer;
        DrafterBox.Text = current.Drafter;
        CheckerBox.Text = current.Checker;
        DateBox.Text = current.Date ?? "";
        Loaded += (_, _) =>
        {
            CompanyNameBox.Focus();
            CompanyNameBox.SelectAll();
        };
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result.CompanyName = CompanyNameBox.Text;
        Result.Title = TitleBox.Text;
        Result.DrawingNo = DrawingNoBox.Text;
        Result.Customer = CustomerBox.Text;
        Result.Designer = DesignerBox.Text;
        Result.Drafter = DrafterBox.Text;
        Result.Checker = CheckerBox.Text;
        Result.Date = DateBox.Text;
        DialogResult = true;
    }
}
