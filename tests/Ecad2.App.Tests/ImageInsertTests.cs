using System.Linq;
using Ecad2.App.ViewModels;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-064: 画像挿入機能(挿入ドラフト・選択・削除・ドラッグ移動・リサイズ)のViewModelロジックの
/// 回帰テスト。FreeLineDragAndResizeTests等と同じ方針(View操作を介さない)。殿裁定により画像操作は
/// 全てUndo対象(他要素との非対称は許容)のため、各確定操作でUndoCommand.CanExecuteがtrueになる
/// ことも併せて検証する。
/// </summary>
public class ImageInsertTests : ViewModelTestBase
{
    private static ImageInsert MakeImage(double x = 10, double y = 20, double w = 30, double h = 15)
        => new() { FilePath = @"C:\images\sample.png", XMm = x, YMm = y, WidthMm = w, HeightMm = h };

    // ---- 挿入ドラフト(BeginImageInsertDraft/UpdateImageInsertDraftPosition/ConfirmImageInsertDraft/CancelImageInsertDraft) ----

    [Fact]
    public void BeginImageInsertDraft_SetsToolModeAndPreview()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 0, yMm: 0);

        Assert.Equal(ToolMode.PlaceImage, vm.Tool.Mode);
        Assert.NotNull(vm.ImageInsertDraftPreview);
        Assert.Equal(40, vm.ImageInsertDraftPreview!.WidthMm);
    }

    [Fact]
    public void UpdateImageInsertDraftPosition_FollowsHoverAndClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 0, yMm: 0);

        vm.UpdateImageInsertDraftPosition(xMm: 500, yMm: 500, maxXMm: 100, maxYMm: 100);

        Assert.Equal(60, vm.ImageInsertDraftPreview!.XMm);  // 100-40
        Assert.Equal(80, vm.ImageInsertDraftPreview!.YMm);  // 100-20
    }

    [Fact]
    public void ConfirmImageInsertDraft_AddsImageSelectsItAndRecordsUndoSnapshot()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 5, yMm: 5);
        vm.UpdateImageInsertDraftPosition(xMm: 10, yMm: 12, maxXMm: 1000, maxYMm: 1000);

        bool confirmed = vm.ConfirmImageInsertDraft();

        Assert.True(confirmed);
        Assert.Single(vm.CurrentSheet!.Images);
        var image = vm.CurrentSheet!.Images[0];
        Assert.Equal(10, image.XMm);
        Assert.Equal(12, image.YMm);
        Assert.Same(image, vm.SelectedImage);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ImageInsertDraftPreview);
        Assert.True(vm.IsDirty);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void CancelImageInsertDraft_DiscardsDraftWithoutAddingImage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 0, yMm: 0);

        vm.CancelImageInsertDraft();

        Assert.Empty(vm.CurrentSheet!.Images);
        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ImageInsertDraftPreview);
        Assert.False(vm.IsDirty);
    }

    // ---- 選択・削除(SelectedImage/HasSelectedImage/DeleteSelectedImage) ----

    [Fact]
    public void SelectedImage_SetsHasSelectedImageAndClearsHasNoPropertySelection()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        vm.CurrentSheet!.Images.Add(image);

        vm.SelectedImage = image;

        Assert.True(vm.HasSelectedImage);
        Assert.False(vm.HasNoPropertySelection);
    }

    [Fact]
    public void SelectedCell_ClearsSelectedImage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.SelectedCell = new GridPos(0, 0);

        Assert.Null(vm.SelectedImage);
    }

    [Fact]
    public void DeleteSelectedImage_RemovesImageAndRecordsUndoSnapshot()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        bool deleted = vm.DeleteSelectedImage();

        Assert.True(deleted);
        Assert.Empty(vm.CurrentSheet!.Images);
        Assert.Null(vm.SelectedImage);
        Assert.True(vm.IsDirty);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void DeleteSelectedImage_WithoutSelection_ReturnsFalse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.DeleteSelectedImage());
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void SelectedImageIsTracingOnly_TogglesAndRecordsUndoSnapshot()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        image.IsTracingOnly = false;
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.SelectedImageIsTracingOnly = true;

        Assert.True(image.IsTracingOnly);
        Assert.True(vm.IsDirty);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    // ---- ドラッグ(BeginDragImage/UpdateDragImage/ConfirmDragImage/CancelDragImage) ----

    [Fact]
    public void DragImage_Move_UpdatesModelDuringDragAndRecordsUndoOnConfirm()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginDragImage(image, startXMm: 10, startYMm: 20, maxXMm: 1000, maxYMm: 1000);
        Assert.False(vm.IsDirty);

        vm.UpdateDragImage(currentXMm: 15, currentYMm: 25);   // +5,+5

        Assert.Equal(15, image.XMm);
        Assert.Equal(25, image.YMm);
        Assert.False(vm.IsDirty);   // 確定前はUndoスナップショット未記録

        vm.ConfirmDragImage();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsDraggingImage);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void DragImage_Move_ClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginDragImage(image, startXMm: 10, startYMm: 20, maxXMm: 50, maxYMm: 100);
        vm.UpdateDragImage(currentXMm: 200, currentYMm: 20);   // 大きく右へ動かそうとする

        Assert.Equal(20, image.XMm);   // 50 - WidthMm(30)
    }

    [Fact]
    public void DragImage_Cancel_RestoresOriginalPositionWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginDragImage(image, startXMm: 10, startYMm: 20, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateDragImage(currentXMm: 40, currentYMm: 50);
        Assert.Equal(40, image.XMm);

        vm.CancelDragImage();

        Assert.Equal(10, image.XMm);
        Assert.Equal(20, image.YMm);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsDraggingImage);
    }

    [Fact]
    public void ConfirmDragImage_WhenPositionUnchanged_DoesNotMarkDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginDragImage(image, startXMm: 10, startYMm: 20, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateDragImage(currentXMm: 10, currentYMm: 20);   // 移動量ゼロ
        vm.ConfirmDragImage();

        Assert.False(vm.IsDirty);
    }

    // ---- リサイズ(BeginResizeImage/UpdateResizeImage/ConfirmResizeImage/CancelResizeImage) ----

    [Fact]
    public void ResizeImage_BottomRightHandle_KeepsTopLeftAnchorFixed()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);   // 右下=(40,35)
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 40, startYMm: 35, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: 60, currentYMm: 45);   // 右下を(60,45)まで広げる

        Assert.Equal(10, image.XMm);   // 左上(アンカー)は不変
        Assert.Equal(20, image.YMm);
        Assert.Equal(50, image.WidthMm);
        Assert.Equal(25, image.HeightMm);
    }

    [Fact]
    public void ResizeImage_TopLeftHandle_KeepsBottomRightAnchorFixed()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);   // 右下=(40,35)固定点
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.TopLeft, startXMm: 10, startYMm: 20, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: 0, currentYMm: 10);   // 左上を外側へ広げる

        Assert.Equal(0, image.XMm);
        Assert.Equal(10, image.YMm);
        Assert.Equal(40, image.WidthMm);   // 右下(40)まで
        Assert.Equal(25, image.HeightMm);  // 右下(35)まで
    }

    [Fact]
    public void ResizeImage_EnforcesMinimumSize()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);   // 右下=(40,35)
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 40, startYMm: 35, maxXMm: 1000, maxYMm: 1000);
        // アンカー(10,20)のすぐそばまで縮めようとする(最小サイズ未満)。
        vm.UpdateResizeImage(currentXMm: 11, currentYMm: 21);

        Assert.True(image.WidthMm >= 5.0);
        Assert.True(image.HeightMm >= 5.0);
        Assert.Equal(10, image.XMm);   // アンカーは維持される
        Assert.Equal(20, image.YMm);
    }

    [Fact]
    public void ConfirmResizeImage_RecordsUndoSnapshotWhenChanged()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 40, startYMm: 35, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: 60, currentYMm: 45);
        vm.ConfirmResizeImage();

        Assert.True(vm.IsDirty);
        Assert.False(vm.IsResizingImage);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void CancelResizeImage_RestoresOriginalSizeWithoutMarkingDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 40, startYMm: 35, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: 60, currentYMm: 45);

        vm.CancelResizeImage();

        Assert.Equal(10, image.XMm);
        Assert.Equal(20, image.YMm);
        Assert.Equal(30, image.WidthMm);
        Assert.Equal(15, image.HeightMm);
        Assert.False(vm.IsDirty);
        Assert.False(vm.IsResizingImage);
    }

    // ---- Undo実行結果(DoD検証観点、殿裁定=画像操作はUndo対象) ----

    [Fact]
    public void UndoCommand_AfterConfirmImageInsertDraft_RemovesInsertedImage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 10, yMm: 10);
        vm.ConfirmImageInsertDraft();
        Assert.Single(vm.CurrentSheet!.Images);

        vm.UndoCommand.Execute(null);

        Assert.Empty(vm.CurrentSheet!.Images);
    }

    [Fact]
    public void UndoCommand_AfterDeleteSelectedImage_RestoresImage()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.DeleteSelectedImage();
        Assert.Empty(vm.CurrentSheet!.Images);

        vm.UndoCommand.Execute(null);

        Assert.Single(vm.CurrentSheet!.Images);
    }

    // ---- 保存/読込往復(DoD検証観点) ----

    [Fact]
    public void ImageInsert_OrderAndPropertiesArePreservedAcrossSaveAndLoad()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 12.5, y: 8, w: 40, h: 22);
        image.IsTracingOnly = true;
        vm.CurrentSheet!.Images.Add(image);

        string json = Ecad2.Persistence.GcadSerializer.Serialize(vm.Document);
        var restored = Ecad2.Persistence.GcadSerializer.Deserialize(json);

        var restoredImage = Assert.Single(restored.Sheets[0].Images);
        Assert.Equal(image.FilePath, restoredImage.FilePath);
        Assert.Equal(image.XMm, restoredImage.XMm);
        Assert.Equal(image.YMm, restoredImage.YMm);
        Assert.Equal(image.WidthMm, restoredImage.WidthMm);
        Assert.Equal(image.HeightMm, restoredImage.HeightMm);
        Assert.True(restoredImage.IsTracingOnly);
    }
}
