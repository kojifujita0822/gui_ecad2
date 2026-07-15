namespace Ecad2.App.Tests;

/// <summary>
/// T-083 PoC(ダークモード搭載、家老采配2026-07-15=作図キャンバス色のテーマ切替の最小疎通)。
/// IsDarkModeの既定値・setter経由のPropertyChanged発火を検証する。View層(LadderCanvas.Theme・
/// メニュー結線・実際の描画色)の反映確認は実機確認(忍者マター)に委ねる(GridVisibilityToggleTests
/// と同型の構成)。
/// </summary>
public class DarkModeToggleTests : ViewModelTestBase
{
    [Fact]
    public void Constructor_InitialState_IsDarkModeIsFalse()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsDarkMode);
    }

    [Fact]
    public void IsDarkMode_SetToTrue_UpdatesValueAndRaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsDarkMode)) raised = true; };

        vm.IsDarkMode = true;

        Assert.True(vm.IsDarkMode);
        Assert.True(raised);
    }

    [Fact]
    public void IsDarkMode_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var vm = CreateViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsDarkMode)) raised = true; };

        vm.IsDarkMode = false;

        Assert.False(raised);
    }
}
