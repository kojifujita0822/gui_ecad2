using System.Windows;
using System.Windows.Controls.Primitives;
using Ecad2.App.Views;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-132増分3: シート設定ダイアログへ列数・電源ラベルを追加した分の検証。
/// <para>
/// <b>【純粋関数へ切り出さず、ダイアログを STA で立てて測る形を採った理由】</b>
/// <c>samurai.md</c>「『テストしにくい』は設計の匂い——まず純粋関数にできぬかを問う」に従って先に問うたが、
/// 切り出せるのは <c>int.TryParse</c> ＋ 範囲比較の数行にすぎず、<b>本増分でもっとも起きやすい事故を捕らえられぬ</b>。
/// その事故とは<b>繋ぎ込みの取り違え</b>——行の欄を写して列の欄を作る作業ゆえ、
/// 「列数の欄に行数を入れる」「列数が不正なのに行数のエラーを出す」「<c>Rows</c> の値を <c>Columns</c> へ代入する」
/// といった型が本命である。<b>これらは検証ロジックを幾ら純粋にしても現れぬ。</b>
/// ゆえにダイアログ単体を立て、値の受け渡し・確定・エラー表示を通しで測る形とした。
/// <b>各観点で行と列に必ず別々の値を与えている</b>のは、取り違えを素通りさせぬためである。
/// </para>
/// <para>
/// <b>【<c>ShowDialog()</c> を呼ばずに測れる理由】</b> 確定処理は <c>TryApply()</c> として
/// <c>DialogResult</c> の代入から分けてある（<c>DialogResult</c> は <c>ShowDialog()</c> で開かれた窓にしか
/// 代入できず、代入すれば例外になる）。ゆえに正常系も測れる。
/// </para>
/// </summary>
public class SheetSettingsDialogTests
{
    /// <summary>各項目に相異なる値を与える。<b>どれか一つでも取り違えれば必ず落ちる</b>ようにするため、
    /// 行数と列数はいずれも有効範囲内でありながら別の値とする。</summary>
    private static SheetSettingsDialog NewDialog(
        int rows = 12, string left = "L1", string right = "R1", int columns = 25, string? power = "AC200V")
        => new(rows, left, right, columns, power);

    // ===== 観点A: 現在値がそれぞれ正しい欄へ入るか =====

    [Fact]
    public void 現在値がそれぞれの欄へ入る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();

            Assert.Equal("12", dialog.RowsBox.Text);
            Assert.Equal("25", dialog.ColumnsBox.Text);
            Assert.Equal("L1", dialog.LeftNameBox.Text);
            Assert.Equal("R1", dialog.RightNameBox.Text);
            Assert.Equal("AC200V", dialog.PowerLabelBox.Text);
        });

    /// <summary>電源ラベルが未設定（<c>null</c>）のシートでも欄は空文字になる
    /// ——<c>null</c> をそのまま <c>TextBox.Text</c> へ入れると空文字へ暗黙変換されるが、
    /// <b>「意図して空文字にしている」ことを固定しておく</b>。</summary>
    [Fact]
    public void 電源ラベルがnullなら欄は空文字になる()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog(power: null);

            Assert.Equal("", dialog.PowerLabelBox.Text);
        });

    [Fact]
    public void 初期状態ではどちらのエラーも出ていない()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();

            Assert.Equal(Visibility.Collapsed, dialog.RowsErrorText.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.ColumnsErrorText.Visibility);
        });

    // ===== 観点B: TryApply の正常系（欄 → プロパティ） =====

    /// <summary>
    /// 各欄の値が対応するプロパティへ入る。<b>行数と列数に別の値を入れている</b>のが要点
    /// ——同じ値なら「<c>Rows</c> を <c>Columns</c> へ代入する」型の取り違えを素通りさせる。
    /// </summary>
    [Fact]
    public void TryApplyが通れば各欄の値がプロパティへ入る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.RowsBox.Text = "30";
            dialog.ColumnsBox.Text = "7";
            dialog.LeftNameBox.Text = "N24";
            dialog.RightNameBox.Text = "P24";
            dialog.PowerLabelBox.Text = "DC24V";

            Assert.True(dialog.TryApply());

            Assert.Equal(30, dialog.Rows);
            Assert.Equal(7, dialog.Columns);
            Assert.Equal("N24", dialog.LeftName);
            Assert.Equal("P24", dialog.RightName);
            Assert.Equal("DC24V", dialog.PowerLabel);
        });

    /// <summary>母線名は空文字をそのまま保持する（殿裁定・GuiEcad踏襲の既存方針）。
    /// <b>電源ラベルの <c>null</c> 変換と混同して母線名まで <c>null</c> 化する誤りへの網。</b></summary>
    [Fact]
    public void 母線名は空文字をそのまま保つ()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.LeftNameBox.Text = "";
            dialog.RightNameBox.Text = "";

            Assert.True(dialog.TryApply());

            Assert.Equal("", dialog.LeftName);
            Assert.Equal("", dialog.RightName);
        });

    /// <summary>電源ラベルは空欄・空白のみなら <c>null</c>、値があれば <c>Trim</c> 済みで入る
    /// （原本 <c>MainPage.Dialogs.cs:216</c> を一次ソースで確認）。</summary>
    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("AC100V", "AC100V")]
    [InlineData("  AC100V  ", "AC100V")]
    public void 電源ラベルは空欄をnullへ落とし値はTrimされる(string input, string? expected)
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.PowerLabelBox.Text = input;

            Assert.True(dialog.TryApply());

            Assert.Equal(expected, dialog.PowerLabel);
        });

    // ===== 観点C: 範囲検証（境界値） =====

    /// <summary>列数の範囲は <c>GridSpec.MinColumns</c>〜<c>MaxColumns</c>（2〜40）。
    /// <b>App 層へ数値を直書きせず定数を参照している</b>ことも、ここで併せて固定する（DoD2）。</summary>
    [Theory]
    [InlineData("1", false)]
    [InlineData("2", true)]
    [InlineData("40", true)]
    [InlineData("41", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("3.5", false)]
    public void 列数の範囲検証(string input, bool expected)
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.ColumnsBox.Text = input;

            Assert.Equal(expected, dialog.TryApply());
        });

    /// <summary>行数の範囲（1〜60）は既存の振る舞い。<b>列数を足したことで壊れていないか</b>を測る。</summary>
    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("60", true)]
    [InlineData("61", false)]
    [InlineData("abc", false)]
    public void 行数の範囲検証は変わっていない(string input, bool expected)
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.RowsBox.Text = input;

            Assert.Equal(expected, dialog.TryApply());
        });

    /// <summary>範囲の下限・上限が <c>GridSpec</c> の定数と結びついていること
    /// ——<b>定数を変えれば検証も追随する</b>（App 層に 2／40 を直書きしていない証）。</summary>
    [Fact]
    public void 列数の検証はGridSpecの定数に従う()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();

            dialog.ColumnsBox.Text = GridSpec.MinColumns.ToString();
            Assert.True(dialog.TryApply());
            dialog.ColumnsBox.Text = GridSpec.MaxColumns.ToString();
            Assert.True(dialog.TryApply());
            dialog.ColumnsBox.Text = (GridSpec.MinColumns - 1).ToString();
            Assert.False(dialog.TryApply());
            dialog.ColumnsBox.Text = (GridSpec.MaxColumns + 1).ToString();
            Assert.False(dialog.TryApply());
        });

    /// <summary>検証で弾かれたときはプロパティを書き換えない
    /// ——<b>半端に確定させてから false を返す</b>形になっていないこと。</summary>
    [Fact]
    public void 弾かれたときはプロパティを書き換えない()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.ColumnsBox.Text = "999";
            dialog.RowsBox.Text = "30";
            dialog.PowerLabelBox.Text = "DC24V";

            Assert.False(dialog.TryApply());

            Assert.Equal(0, dialog.Rows);
            Assert.Equal(0, dialog.Columns);
            Assert.Null(dialog.PowerLabel);
        });

    // ===== 観点D: エラー表示の出し分け =====
    // 「行の欄を写して列を作る」作業ゆえ、エラー表示の取り違えがもっとも起きやすい。

    [Fact]
    public void 行数のみ不正なら行数のエラーだけが出る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.RowsBox.Text = "99";

            Assert.False(dialog.TryApply());

            Assert.Equal(Visibility.Visible, dialog.RowsErrorText.Visibility);
            Assert.Equal(Visibility.Collapsed, dialog.ColumnsErrorText.Visibility);
        });

    [Fact]
    public void 列数のみ不正なら列数のエラーだけが出る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.ColumnsBox.Text = "99";

            Assert.False(dialog.TryApply());

            Assert.Equal(Visibility.Collapsed, dialog.RowsErrorText.Visibility);
            Assert.Equal(Visibility.Visible, dialog.ColumnsErrorText.Visibility);
        });

    [Fact]
    public void 両方不正なら両方のエラーが出る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.RowsBox.Text = "99";
            dialog.ColumnsBox.Text = "99";

            Assert.False(dialog.TryApply());

            Assert.Equal(Visibility.Visible, dialog.RowsErrorText.Visibility);
            Assert.Equal(Visibility.Visible, dialog.ColumnsErrorText.Visibility);
        });

    /// <summary>
    /// 一度出したエラーは、直せば消える。
    /// <b>行数を直したのに古いエラーが残ったまま列数のエラーも出る、という二重表示を防ぐ。</b>
    /// 行と列を別々に検証する形にした以上、「片方だけ直した中間状態」が必ず起きるため要る。
    /// </summary>
    [Fact]
    public void 直した側のエラーは次の検証で消える()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.RowsBox.Text = "99";
            dialog.ColumnsBox.Text = "99";
            Assert.False(dialog.TryApply());

            dialog.RowsBox.Text = "30";   // 行数だけ直す
            Assert.False(dialog.TryApply());

            Assert.Equal(Visibility.Collapsed, dialog.RowsErrorText.Visibility);
            Assert.Equal(Visibility.Visible, dialog.ColumnsErrorText.Visibility);
        });

    // ===== 観点E: OK ボタンと確定処理の繋ぎ込み =====

    /// <summary>
    /// OK ボタンを押せば <c>TryApply</c> が走ること。
    /// <para>
    /// <b>【なぜ不正値で測るのか】</b> 正常系では <c>DialogResult = true</c> に達し、
    /// <c>ShowDialog()</c> で開いていない窓ゆえ例外になる。ゆえに<b>弾かれる入力で押し</b>、
    /// エラー表示が現れることをもって「押下がハンドラへ届いた」ことの証とする。
    /// </para>
    /// <para>
    /// <b>これが要る理由</b>——T-144で「述語は正しいが繋ぎ込みが漏れており、実機まで露見しなかった」
    /// （<c>Notify()</c> の欠落）が起きている。<c>Click</c> の配線そのものを一度は測っておく。
    /// </para>
    /// </summary>
    [Fact]
    public void OKボタンを押せば検証が走る()
        => StaTestRunner.Run(() =>
        {
            var dialog = NewDialog();
            dialog.ColumnsBox.Text = "99";

            dialog.OkButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.Equal(Visibility.Visible, dialog.ColumnsErrorText.Visibility);
        });
}

// 【本テスト群が測れておらぬこと・侍が自ら区切る】
// 1. OK 押下の正常系（DialogResult = true まで）は測れていない。ShowDialog() を経ぬ窓へ
//    DialogResult を代入すると例外になるためで、そこは実機確認に委ねる。
//    ただし TryApply の正常系そのものは上で測れており、残るのは代入一行のみである。
// 2. 窓の高さ（Height=390）が両方のエラー行を出しても収まるかは測れていない。実測を要する寸法ゆえ、
//    増分4の後の実機確認に委ねる。
// 3. 本増分では「OK を押しても列数・電源ラベルはシートへ反映されない」——意図した中間状態であり、
//    反映は増分4で繋ぎ込む。ゆえに反映を測るテストはここには無い。
