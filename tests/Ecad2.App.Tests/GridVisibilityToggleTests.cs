namespace Ecad2.App.Tests;

/// <summary>
/// T-056: グリッド表示切替(IsGridVisible)。既定値=表示(殿裁定2026-07-11)・setter経由の
/// PropertyChanged発火を検証する。View層(LadderCanvas.ShowGrid・Ctrl+G・メニュー結線)の
/// 反映確認は実機確認(忍者マター)に委ねる。
/// </summary>
public class GridVisibilityToggleTests : ViewModelTestBase
{
    [Fact]
    public void Constructor_InitialState_IsGridVisibleIsTrue()
    {
        var vm = CreateViewModel();

        Assert.True(vm.IsGridVisible);
    }

    [Fact]
    public void IsGridVisible_SetToFalse_UpdatesValueAndRaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsGridVisible)) raised = true; };

        vm.IsGridVisible = false;

        Assert.False(vm.IsGridVisible);
        Assert.True(raised);
    }

    [Fact]
    public void IsGridVisible_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        var vm = CreateViewModel();
        bool raised = false;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.IsGridVisible)) raised = true; };

        vm.IsGridVisible = true;

        Assert.False(raised);
    }
}
