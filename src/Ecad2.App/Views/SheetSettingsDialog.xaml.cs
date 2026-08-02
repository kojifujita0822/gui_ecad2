using System.Windows;
using Ecad2.Model;

namespace Ecad2.App.Views;

/// <summary>シート設定(行数・列数・母線名・電源ラベル)変更用のモーダルダイアログ(T-055増分2、
/// RenameDialog/AddSheetDialogと同型、design-brief 4節#4: 非ネスト方針、単一階層のみ)。
/// <para>
/// <b>【T-132増分3・後の者へ：この増分だけでは列数・電源ラベルは反映されない】</b>
/// 本増分でダイアログに欄は現れ、現在値も入り、範囲検証も効く。<b>だが OK を押しても
/// シートは変わらない</b>——受け取り側の <c>UpdateSheetSettingsCommand</c> がまだ
/// <c>Columns</c>／<c>PowerLabel</c> を受け取らぬためである。<b>これは意図した中間状態であり、
/// 増分4で繋ぎ込む。</b>「列数欄が効かぬ」と見て慌てられぬよう。
/// </para>
/// </summary>
public partial class SheetSettingsDialog : Window
{
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public string LeftName { get; private set; } = "";
    public string RightName { get; private set; } = "";
    /// <summary>電源ラベル(母線間電圧など、任意)。空欄は <c>null</c> へ落とす(原本準拠、下記 TryApply の注記)。</summary>
    public string? PowerLabel { get; private set; }

    public SheetSettingsDialog(int currentRows, string currentLeftName, string currentRightName,
        int currentColumns, string? currentPowerLabel)
    {
        InitializeComponent();
        RowsBox.Text = currentRows.ToString();
        ColumnsBox.Text = currentColumns.ToString();
        LeftNameBox.Text = currentLeftName;
        RightNameBox.Text = currentRightName;
        PowerLabelBox.Text = currentPowerLabel ?? "";
        Loaded += (_, _) =>
        {
            // 初期フォーカスは行数のまま(殿ご裁定の案ア=既存の操作感を壊さない)。
            RowsBox.Focus();
            RowsBox.SelectAll();
        };
    }

    /// <summary>
    /// 入力を検証し、通れば各プロパティへ確定させる。通らなければエラー表示を出して <c>false</c>。
    /// <para>
    /// <b>【OkButton_Click から切り出した理由】</b> <c>DialogResult</c> は <c>ShowDialog()</c> で
    /// 開かれた窓にしか代入できず、代入すると <c>InvalidOperationException</c> になる。
    /// ゆえに検証と確定を <c>DialogResult</c> の代入から分けておかねば、<b>正常系をテストから測れない</b>。
    /// 分けた結果、正常系・異常系の双方をダイアログ単体で測れる(<c>SheetSettingsDialogTests</c>)。
    /// </para>
    /// <para>
    /// <b>【行数・列数の両方を毎回検証する理由】</b> 片方が不正なら即打ち切る形にすると、
    /// 使い手は行数を直してから改めて列数のエラーを見ることになり二度手間になる。
    /// <b>毎回両方の表示を更新する</b>ので、直した側のエラーは自動的に消える。
    /// </para>
    /// </summary>
    internal bool TryApply()
    {
        bool rowsOk = int.TryParse(RowsBox.Text, out int rows)
            && rows >= GridSpec.MinRows && rows <= GridSpec.MaxRows;
        bool columnsOk = int.TryParse(ColumnsBox.Text, out int columns)
            && columns >= GridSpec.MinColumns && columns <= GridSpec.MaxColumns;

        RowsErrorText.Visibility = rowsOk ? Visibility.Collapsed : Visibility.Visible;
        ColumnsErrorText.Visibility = columnsOk ? Visibility.Collapsed : Visibility.Visible;
        if (!rowsOk || !columnsOk) return false;

        Rows = rows;
        Columns = columns;
        // Bus名は空文字を許容する(殿裁定、GuiEcad踏襲)ためバリデーション不要。
        LeftName = LeftNameBox.Text;
        RightName = RightNameBox.Text;
        // 電源ラベルのみ空欄を null へ落とす(原本 MainPage.Dialogs.cs:216 準拠、Trim 済みの値を入れる)。
        // 母線名が空文字をそのまま保持するのとは別の扱いである点に注意——
        // BusConfig.PowerLabel が string? であり「未設定」を null で表すため。
        PowerLabel = PowerLabelBox.Text.Trim().Length > 0 ? PowerLabelBox.Text.Trim() : null;
        return true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (TryApply()) DialogResult = true;
    }
}
