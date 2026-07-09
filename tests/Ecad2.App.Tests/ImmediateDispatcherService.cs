using System.Windows.Threading;
using Ecad2.App.ViewModels;

namespace Ecad2.App.Tests;

/// <summary>T-045(P-016対応)のテスト用<see cref="IDispatcherService"/>実装。WPF Applicationが
/// 起動していないテストプロセスでもNullReferenceExceptionにならず、かつ「BeginInvokeで渡した
/// Actionが実際に実行されるか」まで即時同期実行で検証できるようにする。<see cref="LastPriority"/>で
/// 呼び出し時のpriorityを、<see cref="BeforeInvoke"/>でaction実行直前の状態(同期処理が既に
/// 完了しているか)をそれぞれ検証可能にする(増分A補遺、隠密所見1・2対応)。</summary>
public sealed class ImmediateDispatcherService : IDispatcherService
{
    public DispatcherPriority? LastPriority { get; private set; }

    public Action? BeforeInvoke { get; set; }

    public void BeginInvoke(DispatcherPriority priority, Action action)
    {
        LastPriority = priority;
        BeforeInvoke?.Invoke();
        action();
    }
}
