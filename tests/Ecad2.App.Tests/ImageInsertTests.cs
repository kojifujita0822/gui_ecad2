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

    // --- T-064往復1周目(隠密レビュー、docs/ecad2-t064-image-insert-review-onmitsu.md):
    //     修正1(最重要)・修正4(実害大)の回帰テスト ---

    /// <summary>
    /// 隠密レビュー指摘・要修正1のRED先行証明用回帰テスト(指摘の具体例)。アンカーがページ境界
    /// (X=2mm)近くにある状態でBottomRightハンドルを大きく逆方向(左上)へ動かすと、旧実装は
    /// 最小サイズ制約(5mm)確保がページ境界クランプの後に効くため画像がXMm=-3等、境界外へ
    /// はみ出していた。
    /// </summary>
    [Fact]
    public void ResizeImage_BottomRightHandle_NearLeftBoundary_DoesNotExceedPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 2, y: 2, w: 30, h: 15);   // アンカー(左上、BottomRightハンドルの固定点)=(2,2)
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 32, startYMm: 17, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: -50, currentYMm: -50);   // アンカーを超えて大きく逆方向へ

        Assert.True(image.XMm >= 0, $"XMm={image.XMm}が0未満(境界外)");
        Assert.True(image.YMm >= 0, $"YMm={image.YMm}が0未満(境界外)");
        Assert.True(image.WidthMm >= 5.0);
        Assert.True(image.HeightMm >= 5.0);
    }

    /// <summary>上限側(ページ右下端近く)でも同様に境界外へはみ出さないことを検証する
    /// (TopLeftハンドルを大きく右下方向へ動かす逆方向ケース)。</summary>
    [Fact]
    public void ResizeImage_TopLeftHandle_NearRightBoundary_DoesNotExceedPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 468, y: 483, w: 30, h: 15);   // 右下(アンカー)=(498,498)、境界=500
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.TopLeft, startXMm: 468, startYMm: 483, maxXMm: 500, maxYMm: 500);
        vm.UpdateResizeImage(currentXMm: 600, currentYMm: 600);   // アンカーを超えて大きく逆方向へ

        Assert.True(image.XMm + image.WidthMm <= 500, $"右端={image.XMm + image.WidthMm}が境界500超");
        Assert.True(image.YMm + image.HeightMm <= 500, $"下端={image.YMm + image.HeightMm}が境界500超");
        Assert.True(image.WidthMm >= 5.0);
        Assert.True(image.HeightMm >= 5.0);
    }

    /// <summary>
    /// 隠密レビュー指摘・要修正4(実害大)のRED先行証明用回帰テスト。既存要素を選択した状態のまま
    /// 画像挿入を確定すると、旧実装はSelectedCellをnullにせずSelectedImageだけを設定していたため
    /// 両方non-nullになり、Deleteキーが対象とするOR連鎖の先頭(DeleteSelectedElement)が誤って
    /// 先にヒットし、挿入したばかりの画像ではなく旧選択要素が削除されていた。
    /// </summary>
    [Fact]
    public void ConfirmImageInsertDraft_ClearsSelectedCell_SoDeleteTargetsNewImageNotOldElement()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var oldElement = new ElementInstance { Kind = ElementKind.ContactNO, Pos = new GridPos(0, 0), DeviceName = "X1" };
        vm.CurrentSheet!.Elements.Add(oldElement);
        vm.SelectedCell = new GridPos(0, 0);   // 旧要素を選択中

        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 20, heightMm: 10, xMm: 10, yMm: 10);
        vm.ConfirmImageInsertDraft();

        Assert.Null(vm.SelectedCell);
        Assert.NotNull(vm.SelectedImage);

        bool deleted = vm.DeleteSelectedElement() || vm.DeleteSelectedConnector() || vm.DeleteSelectedWireBreak()
            || vm.DeleteSelectedFreeLine() || vm.DeleteSelectedConnectionDot() || vm.DeleteSelectedImage();

        Assert.True(deleted);
        Assert.Empty(vm.CurrentSheet!.Images);      // 挿入したばかりの画像が削除される
        Assert.Single(vm.CurrentSheet!.Elements);   // 旧要素は残ったまま
    }

    // --- T-064往復2周目(隠密再レビュー、docs/ecad2-t064-round1-review-onmitsu.md):
    //     修正2(殿裁定=反転追従の復活、境界クランプとの両立)の回帰テスト ---

    /// <summary>
    /// 隠密再レビュー指摘・新規要確認2の具体例(手計算で確認)のRED先行証明用回帰テスト。
    /// BottomRightハンドル(アンカー=左上、(10,20))を掴み、X方向だけアンカーを超えて左へ
    /// ドラッグすると、旧来の挙動(往復1周目修正1で失われた)ではX軸が反転してマウス位置へ
    /// 追従する。往復1周目のClampResizeTarget新設により伸びる方向がハンドル種別のみで固定され、
    /// この反転追従が失われていた(殿裁定=退行として復活)。
    /// </summary>
    [Fact]
    public void ResizeImage_DragPastAnchor_FlipsAxisAndFollowsMouse()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);   // アンカー(左上、BottomRightハンドルの固定点)=(10,20)
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.BottomRight, startXMm: 40, startYMm: 35, maxXMm: 1000, maxYMm: 1000);
        vm.UpdateResizeImage(currentXMm: 5, currentYMm: 25);   // X方向だけアンカー(10)を超えて左へ

        Assert.Equal(5, image.XMm);     // X軸が反転しマウス位置(5)へ追従
        Assert.Equal(5, image.WidthMm);
        Assert.Equal(20, image.YMm);    // Y方向は反転せず元のまま(アンカー=20)
        Assert.Equal(5, image.HeightMm);
    }

    /// <summary>反転追従とページ境界クランプ(往復1周目主題)の両立を検証する。TopLeftハンドル
    /// (通常は左上方向へ伸びる想定)を、アンカー(右下固定点)を超えて大きく逆方向(右下)へドラッグ
    /// しても、反転して伸びつつページ境界を超えないこと。</summary>
    [Fact]
    public void ResizeImage_DragPastAnchor_FlipsAxisButStillClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 2, y: 2, w: 10, h: 10);   // アンカー(右下、TopLeftハンドルの固定点)=(12,12)
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        vm.BeginResizeImage(image, ImageResizeHandle.TopLeft, startXMm: 2, startYMm: 2, maxXMm: 100, maxYMm: 100);
        vm.UpdateResizeImage(currentXMm: 150, currentYMm: 150);   // アンカーを超えて大きく右下へ(反転)

        Assert.Equal(12, image.XMm);       // アンカー(12)が左上として固定される(反転)
        Assert.Equal(12, image.YMm);
        Assert.Equal(88, image.WidthMm);   // 境界100まで(88=100-12)
        Assert.Equal(88, image.HeightMm);
    }

    // --- T-064追加往復(隠密フル観点レビュー指摘、殿裁定2026-07-13):
    //     ReplaceDocumentが画像挿入ドラフトをクリアしていなかった横展開漏れの回帰テスト。
    //     ConnectorDraftTests/FreeLineDraftTestsの同型テスト(ReplaceDocument_Clears*Draft_OnNewDocument)
    //     と対称に揃える。 ---

    /// <summary>画像挿入配置待機中(Tool.Mode=PlaceImage)に確定・キャンセルせず新規/開くを実行すると、
    /// 旧実装は_imageInsertDraftが残留しHasAnyDraftが真のまま残った(右クリック無反応の原因)。加えて
    /// ImageInsertDraftPreviewは旧文書座標系のImageInsertを返し続け、新文書上に幽霊プレビューが
    /// 表示される(RedrawCanvasがTool.Mode非依存で無条件描画するため)。</summary>
    [Fact]
    public void ReplaceDocument_ClearsImageInsertDraft_OnNewDocument()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 40, heightMm: 20, xMm: 10, yMm: 10);
        Assert.True(vm.HasAnyDraft);

        vm.NewDocument();

        Assert.Equal(ToolMode.Select, vm.Tool.Mode);
        Assert.Null(vm.ImageInsertDraftPreview);
        Assert.False(vm.HasAnyDraft);
    }

    /// <summary>T-064再追加往復(隠密フル観点レビュー指摘、殿裁定2026-07-13): 画像選択中に確定・
    /// キャンセルせず新規/開くを実行すると、旧実装はSelectedConnector等と異なりSelectedImageだけ
    /// クリアが漏れており、_selectedImageが旧Documentの実体を保持したまま残留していた
    /// (プロパティパネルに実体消失後の画像プロパティが表示・編集可能なまま残る恐れ)。</summary>
    [Fact]
    public void ReplaceDocument_ClearsSelectedImage_OnNewDocument()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage();
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;
        Assert.True(vm.HasSelectedImage);

        vm.NewDocument();

        Assert.Null(vm.SelectedImage);
        Assert.False(vm.HasSelectedImage);
    }

    // --- T-064矢印キー画像平行移動(隠密静的調査`docs/ecad2-t064-arrow-key-investigation-onmitsu.md`
    //     により原因特定、殿裁定2026-07-13): MoveSelectedImage(キーボード等価操作)の回帰テスト。
    //     ConnectionDotDragTests.MoveSelectedConnectionDot_*と対称に揃える。 ---

    [Fact]
    public void MoveSelectedImage_ShiftsPositionAndMarksDirty()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 20, y: 30, w: 30, h: 15);
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        bool moved = vm.MoveSelectedImage(5, -3, maxXMm: 1000, maxYMm: 1000);

        Assert.True(moved);
        Assert.Equal(25, image.XMm);
        Assert.Equal(27, image.YMm);
        Assert.True(vm.IsDirty);
        // T-064追加修正(隠密フル観点レビュー指摘、殿裁定2026-07-13): 画像操作は全てUndo対象
        // (他要素との非対称は許容)。移動もこの原則の対象であることを検証する。
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void MoveSelectedImage_WithoutSelection_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();

        Assert.False(vm.MoveSelectedImage(5, 0, maxXMm: 1000, maxYMm: 1000));
        Assert.False(vm.IsDirty);
    }

    /// <summary>UpdateDragImageと同じ境界式(0〜max-Width/Height)でクランプされることを検証する。</summary>
    [Fact]
    public void MoveSelectedImage_ClampsToPageBoundary()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 10, y: 20, w: 30, h: 15);   // 右端=40
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        bool moved = vm.MoveSelectedImage(100, 0, maxXMm: 50, maxYMm: 1000);

        Assert.True(moved);
        Assert.Equal(20, image.XMm);   // 50 - WidthMm(30)
        Assert.True(vm.IsDirty);
        Assert.True(vm.UndoCommand.CanExecute(null));
    }

    /// <summary>T-064追加修正(隠密フル観点レビュー指摘、殿裁定2026-07-13): 挿入(Undo記録1回目)
    /// →矢印キー移動(Undo記録が無いと挿入前まで一気に戻ってしまう、隠密指摘の失敗シナリオ)→Undoで、
    /// 移動1回分だけが戻り画像自体は残ること(挿入前まで巻き戻らないこと)を検証する。</summary>
    [Fact]
    public void UndoCommand_AfterMoveSelectedImage_RestoresPreviousPositionOnly()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        vm.BeginImageInsertDraft(@"C:\images\a.png", widthMm: 20, heightMm: 10, xMm: 10, yMm: 10);
        vm.ConfirmImageInsertDraft();

        vm.MoveSelectedImage(5, 5, maxXMm: 1000, maxYMm: 1000);
        Assert.Equal(15, vm.CurrentSheet!.Images[0].XMm);
        Assert.Equal(15, vm.CurrentSheet!.Images[0].YMm);

        vm.UndoCommand.Execute(null);

        // Undo/Redoはドキュメント全体を再デシリアライズして差し替えるため、Undo実行前に保持していた
        // ImageInsert参照は反映されない。CurrentSheet.Images[0]を都度再取得して検証する。
        Assert.Single(vm.CurrentSheet!.Images);   // 挿入前まで巻き戻らず画像は残る
        Assert.Equal(10, vm.CurrentSheet!.Images[0].XMm);
        Assert.Equal(10, vm.CurrentSheet!.Images[0].YMm);
    }

    /// <summary>既に境界に達している状態でさらに外側へ動かそうとしても変化なし・IsDirtyも立たない
    /// こと(MoveSelectedConnectionDotと同じ「変化なしならfalse」ガード)。</summary>
    [Fact]
    public void MoveSelectedImage_AlreadyAtBoundary_DoesNothing()
    {
        var vm = CreateViewModel();
        vm.NewDocument();
        var image = MakeImage(x: 20, y: 20, w: 30, h: 15);   // 右端=50=maxXMm、既に境界
        vm.CurrentSheet!.Images.Add(image);
        vm.SelectedImage = image;

        bool moved = vm.MoveSelectedImage(10, 0, maxXMm: 50, maxYMm: 1000);

        Assert.False(moved);
        Assert.False(vm.IsDirty);
    }
}
