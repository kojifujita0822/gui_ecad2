using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ecad2.App.Diagnostics;

namespace Ecad2.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>テスト専用フック(T-050往復2周目、隠密テスト設計層3): OnPropertyChangedが受け渡す旧値を
    /// TraceLog.IsEnabledに依存せず観測するための内部イベント。標準のPropertyChangedEventArgsは
    /// PropertyNameしか持たず旧値を運ばないため、発火回数だけでなく「正しい旧値がちょうど1回通知される」
    /// ことの単体検証(ResetSheetsのタイミングバグ等)に使う。本番コードは購読しないため挙動・コストへの
    /// 影響はない(購読者ゼロなら?.Invokeはno-op)。引数は(propertyName, oldValue)。</summary>
    internal event Action<string?, object?>? PropertyChangedForTest;

    // T-039: SetPropertyが書き換える直前の値を一時保持し、直後のOnPropertyChangedへ受け渡す
    // (旧値をTraceLogへ安価に渡すための一時変数、殿裁定「安くできる範囲」)。
    private (string Name, object? Value)? _pendingOldValue;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        // TraceLog.IsEnabledで無効時のボクシングを回避する(隠密レビューfinding7: 値型fieldの
        // ボクシングコストがIsEnabledに関わらず常に発生していた)。
        if (TraceLog.IsEnabled) _pendingOldValue = (propertyName ?? "", field);
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        object? oldValue = _pendingOldValue is { } pending && pending.Name == propertyName ? pending.Value : null;
        _pendingOldValue = null;
        TraceLog.LogPropertyChanged(this, propertyName, oldValue);
        PropertyChangedForTest?.Invoke(propertyName, oldValue);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>SetPropertyを経由しないカスタムsetter(構造的等価性回避や複数プロパティ一括代入等)から、
    /// 旧値を明示的に渡すためのオーバーロード(隠密レビューfinding3: SelectedElementDeviceName/Tool/
    /// ReplaceDocumentで旧値が常にnullになっていた問題への対応。殿裁定「安くできる範囲」の範囲内)。</summary>
    protected void OnPropertyChanged(string propertyName, object? oldValue)
    {
        TraceLog.LogPropertyChanged(this, propertyName, oldValue);
        PropertyChangedForTest?.Invoke(propertyName, oldValue);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
