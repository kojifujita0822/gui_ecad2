using System.Windows.Threading;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>T-045(P-016対応)のテスト用<see cref="IDispatcherService"/>実装。WPF Applicationが
/// 起動していないテストプロセスでもNullReferenceExceptionにならず、かつ「BeginInvokeで渡した
/// Actionが実際に実行されるか」まで即時同期実行で検証できるようにする(優先度は無視する)。</summary>
public sealed class ImmediateDispatcherService : IDispatcherService
{
    public void BeginInvoke(DispatcherPriority priority, Action action) => action();
}
