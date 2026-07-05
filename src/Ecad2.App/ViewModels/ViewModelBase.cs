using System.ComponentModel;
using System.Runtime.CompilerServices;
using Ecad2.App.Diagnostics;

namespace Ecad2.App.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // T-039: SetPropertyが書き換える直前の値を一時保持し、直後のOnPropertyChangedへ受け渡す
    // (旧値をTraceLogへ安価に渡すための一時変数、殿裁定「安くできる範囲」)。
    private (string Name, object? Value)? _pendingOldValue;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        _pendingOldValue = (propertyName ?? "", field);
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        object? oldValue = _pendingOldValue is { } pending && pending.Name == propertyName ? pending.Value : null;
        _pendingOldValue = null;
        TraceLog.LogPropertyChanged(this, propertyName, oldValue);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
