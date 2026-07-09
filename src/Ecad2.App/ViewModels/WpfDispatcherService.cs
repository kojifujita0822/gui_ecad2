using System.Windows;
using System.Windows.Threading;

namespace Ecad2.App.ViewModels;

/// <summary>本番用の<see cref="IDispatcherService"/>実装(T-045 P-016対応)。
/// 実際のWPF <see cref="Application.Current"/>.Dispatcherへ委譲する。</summary>
public sealed class WpfDispatcherService : IDispatcherService
{
    public void BeginInvoke(DispatcherPriority priority, Action action)
        => Application.Current.Dispatcher.BeginInvoke(priority, action);
}
