using System.Windows.Threading;

namespace Ecad2.App.ViewModels;

/// <summary>WPFの<c>Dispatcher.BeginInvoke</c>を抽象化する(T-045 P-016対応)。
/// <see cref="SheetNavigationViewModel"/>がWPF <c>Application</c>へ直接依存していると、
/// WPF Applicationが起動していないテストプロセスで<see cref="NullReferenceException"/>になる
/// (T-034で発覚、テスト対象外にせざるを得なかった)。テストでは<c>Action</c>を即時同期実行する
/// フェイクを注入し、「BeginInvokeで渡したActionが実際に実行されるか」まで検証できるようにする。</summary>
public interface IDispatcherService
{
    void BeginInvoke(DispatcherPriority priority, Action action);
}
