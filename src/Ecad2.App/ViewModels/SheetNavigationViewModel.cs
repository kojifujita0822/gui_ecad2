using System.Collections.ObjectModel;
using System.Windows.Input;
using Ecad2.App.Commands;
using Ecad2.Model;

namespace Ecad2.App.ViewModels;

/// <summary>
/// 左パレット（シートナビゲーション）用の ViewModel。GuiEcadのナビツリー調査（T-026）に基づき、
/// 実質フラットリスト（1シート=1ノード、階層なし）を踏襲する。MainWindowViewModel の子プロパティ
/// として持たせる（design-brief 3節#1: God Class化の再発防止）。
/// </summary>
public sealed class SheetNavigationViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _owner;

    /// <summary>
    /// Document.Sheets のミラー。ListBox.ItemsSource は素の List&lt;Sheet&gt; を直接バインドすると
    /// 同一参照のままでは OnPropertyChanged(nameof(Sheets)) を発してもWPFが変化なしと判定し
    /// 再列挙されないため、ObservableCollection で Add/Remove を個別に通知する。
    /// </summary>
    public ObservableCollection<Sheet> Sheets { get; }

    /// <summary>選択中のシート。MainWindowViewModel.CurrentSheetIndex と同期する。</summary>
    public Sheet? SelectedSheet
    {
        get
        {
            int index = _owner.CurrentSheetIndex;
            return index >= 0 && index < Sheets.Count ? Sheets[index] : null;
        }
        set
        {
            if (value is null) return;
            int index = Sheets.IndexOf(value);
            if (index >= 0) _owner.CurrentSheetIndex = index;
            OnPropertyChanged();
        }
    }

    /// <summary>CurrentSheetIndexが外部(DRC出力パネルのジャンプ等、T-018)から変更された際、
    /// SelectedSheetのバインディング(左パネルの選択ハイライト)を同期させるために呼ぶ。</summary>
    public void RefreshSelectedSheet() => OnPropertyChanged(nameof(SelectedSheet));

    /// <summary>Document丸ごと差し替え(T-019: 新規/開く)後、SheetsをDocument.Sheetsへ再同期する。</summary>
    public void ResetSheets()
    {
        Sheets.Clear();
        foreach (var sheet in _owner.Document.Sheets) Sheets.Add(sheet);
        OnPropertyChanged(nameof(SelectedSheet));
    }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }

    public SheetNavigationViewModel(MainWindowViewModel owner)
    {
        _owner = owner;
        Sheets = new ObservableCollection<Sheet>(_owner.Document.Sheets);

        AddCommand = new RelayCommand(() =>
        {
            int pageNumber = _owner.Document.Sheets.Count + 1;
            var sheet = new Sheet
            {
                PageNumber = pageNumber,
                Name = $"シート{pageNumber}",
                Grid = new GridSpec { Rows = 10, Columns = 20 },
            };
            _owner.Document.Sheets.Add(sheet);
            Sheets.Add(sheet);
            _owner.MarkDirty();
            // 殿実機検出の修正: HasProjectはReplaceDocument内でのみ明示通知されるため、
            // Sheets=0(濃紺)からここでシート追加してもHasProjectの変更がUIへ伝わらず、
            // 画面が濃紺のまま作業領域色へ切り替わらなかった。
            _owner.NotifyHasProjectChanged();

            // ObservableCollectionへのAdd直後は、ListBoxがまだ新しいアイテムのUI要素を生成し
            // 終えていないため、この場で同期的にSelectedSheetを設定すると視覚上の選択ハイライトが
            // 追従しない(IsSelectedはSelectionItemPattern上は正しく更新されるが表示は前の項目の
            // ままになる。T-026実機確認で発見)。UI要素生成後の次フレームへ遅延させる。
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => SelectedSheet = sheet));
        });

        // 最後の1枚は削除不可（ドキュメントにシートが0枚の状態を作らない）。
        DeleteCommand = new RelayCommand(
            () =>
            {
                if (SelectedSheet is not Sheet sheet) return;
                int index = Sheets.IndexOf(sheet);
                _owner.Document.Sheets.RemoveAt(index);
                Sheets.RemoveAt(index);
                _owner.CurrentSheetIndex = Math.Min(index, Sheets.Count - 1);
                _owner.MarkDirty();
                // 家老裁定: Sheets数を変える全経路で通知発火の不変条件を揃える(AddCommandと同型の
                // 欠陥、現状のガードにより到達不能でも将来ガード変更時の再発を防ぐため揃えておく)。
                _owner.NotifyHasProjectChanged();
                OnPropertyChanged(nameof(SelectedSheet));
            },
            () => Sheets.Count > 1);

        // パラメータに新しいシート名(string)を渡して呼び出す。ダイアログ表示自体はView側の責務。
        // Sheetモデルクラスは(永続化対象のため)INotifyPropertyChangedを実装していないので、
        // sheet.Name への直接代入だけではListBoxの表示に反映されない。また
        // ObservableCollection[index]=同一参照 の Replace も、ItemContainerGeneratorが
        // 「DataContext自体は変わっていない」と判定し再評価しないため反映されなかった(T-026実機
        // 確認で発見)。RemoveAt+Insertで確実にコンテナを再構築させる。
        RenameCommand = new RelayCommand(param =>
        {
            if (SelectedSheet is not Sheet sheet || param is not string newName) return;
            string trimmed = newName.Trim();
            if (trimmed.Length == 0) return;
            sheet.Name = trimmed;
            int index = Sheets.IndexOf(sheet);
            if (index < 0) return;

            _owner.MarkDirty();
            Sheets.RemoveAt(index);
            Sheets.Insert(index, sheet);
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => SelectedSheet = sheet));
        });
    }
}
