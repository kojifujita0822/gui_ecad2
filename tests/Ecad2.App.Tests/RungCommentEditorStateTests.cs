namespace Ecad2.App.Tests;

/// <summary>T-080往復1周目指摘F: 行コメントエディタ表示状態(IsRungCommentEditorVisible)と
/// メインコンテンツ有効状態(IsMainContentEnabled)の連動。配置バー(IsPlacementBarVisible)と同じ
/// 「ViewModel単一の真実源」パターンで、どちらかのオーバーレイが表示中はMainContentAreaの
/// IsEnabledバインドが無効化される。View層(XAMLバインド・マウス素通し遮断・エディタ開閉)の
/// 反映確認は実機確認(忍者マター)に委ねる。</summary>
public class RungCommentEditorStateTests : ViewModelTestBase
{
    [Fact]
    public void Constructor_初期状態_エディタ非表示でメインコンテンツ有効()
    {
        var vm = CreateViewModel();

        Assert.False(vm.IsRungCommentEditorVisible);
        Assert.True(vm.IsMainContentEnabled);
    }

    [Fact]
    public void IsRungCommentEditorVisible_表示中_メインコンテンツ無効()
    {
        var vm = CreateViewModel();

        vm.IsRungCommentEditorVisible = true;

        Assert.False(vm.IsMainContentEnabled);
    }

    [Fact]
    public void IsRungCommentEditorVisible_変更時_IsMainContentEnabledの変更通知も発火する()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsRungCommentEditorVisible = true;

        Assert.Contains(nameof(vm.IsRungCommentEditorVisible), raised);
        Assert.Contains(nameof(vm.IsMainContentEnabled), raised);
    }

    [Fact]
    public void IsPlacementBarVisible_変更時_IsMainContentEnabledの変更通知も発火する()
    {
        var vm = CreateViewModel();
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.IsPlacementBarVisible = true;

        Assert.False(vm.IsMainContentEnabled);
        Assert.Contains(nameof(vm.IsMainContentEnabled), raised);
    }

    [Fact]
    public void IsRungCommentEditorVisible_非表示に戻す_メインコンテンツ有効に復帰()
    {
        var vm = CreateViewModel();
        vm.IsRungCommentEditorVisible = true;

        vm.IsRungCommentEditorVisible = false;

        Assert.True(vm.IsMainContentEnabled);
    }
}
