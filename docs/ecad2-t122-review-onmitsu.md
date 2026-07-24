# T-122 静的レビュー（隠密）

対象コミット：`2965fd4`（`src/Ecad2.App/MainWindow.xaml.cs`のみ、+62/-1）
effort=low（新規実装1周目、既定通り軽量）

## 結論：指摘事項なし

## レビュー観点

### 1. DoD整合

`docs/todo.md` T-122節DoD 2項目とも実装確認済み。

1. **表示開始トリガーをWM_MOVING受信へ変更**：`PlacementToolBarDockingManager_LayoutFloatingWindowControlCreated`
   から`PlacementToolBarDropZoneOverlay.Visibility = Visibility.Visible`の即時代入を削除（コメントで
   移設先を明記）。`PlacementToolBarFloatingWindowFilterMessage`にWM_MOVING(0x0216)分岐を新設し、
   未表示時のみ`UpdatePlacementToolBarDropZoneOverlayBounds()`→`Visibility.Visible`とする実装を確認
2. **ドロップ判定範囲の狭小化**：述語版`FindVisualChild<T>(DependencyObject, Func<T,bool>)`オーバーロード
   新設、`ContentId=="MainToolBar"`を含む`LayoutAnchorablePaneControl`をVisual Tree探索し、その実座標
   （`PointToScreen`→`PointFromScreen`）をオーバーレイのMargin/Width/Heightへ反映する実装を確認。
   ヒットテスト側（WM_EXITSIZEMOVE処理）は無改造でオーバーレイ自体の座標を参照する既存設計を維持して
   おり、狭小化はオーバーレイの見た目・ヒットテスト両方へ自動的に反映される設計として妥当

### 2. code-reviewスキル併用

既知の恒久事象（Claude Code v2.1.215以降、Skillツール経由の自動起動が塞がれている）につき、手動
レビューで代替。対象コミットが確定しているため`git show <commit> -- <path>`で範囲を明示して精読した。

### 3. RED先行証明「不可」申告の妥当性

侍申告＝「HwndSourceフック・VisualTreeHelper依存のSTAThread必須コードにつき既存単体テスト基盤で
検証不能」。`tests/Ecad2.App.Tests/`を確認したところ、`STAThread`/`HwndSource`/`VisualTreeHelper`への
言及は皆無、`MainWindow.xaml.cs`自体を対象とするテストも存在しない（`MainWindowViewModelTests.cs`のみ
＝ViewModel層限定）。**申告は妥当**と判断。

### 4. WM_MOVING切替の副作用（T-121メニュー経由Float()との整合）※本レビューの主眼

T-121のコード内コメント（`MainWindow.xaml.cs:498-501`）に「メニュー経由のFloat()は内部で
`StartDraggingFloatingWindowForContent(this, false)`(startDrag=false)を呼ぶため`AttachDrag()`が発火せず
WM_MOVING/WM_EXITSIZEMOVEが一切発生しない」との記述があり、T-122のWM_MOVING契機化がこの経路で機能
しなくなるおそれがないか、AvalonDock一次ソースで裏取りした。

- `DockingManager.cs:1706-1754`（`StartDraggingFloatingWindowForContent`）：`startDrag=false`の場合、
  `fwc.AttachDrag()`自体が呼ばれない（`if (startDrag) fwc.AttachDrag();`）ことを確認
- `LayoutFloatingWindowControl.cs:347-360`（`AttachDrag`）：`onActivated=false`時は
  `Win32Helper.SendMessage(windowHandle, WM_NCLBUTTONDOWN, HT_CAPTION, lParam)`を送信するのみ——これは
  「タイトルバーを掴んだ状態をシミュレートしOSドラッグへシームレスに引き継ぐ」ためのトリガーであり、
  WM_MOVING/WM_EXITSIZEMOVEを受け取るフック自体のセットアップとは無関係と判明
- 同ファイル`580-593`（`OnLoaded`）：`_hwndSrc.AddHook(_hwndSrcHook)`は`AttachDrag`呼び出しの有無に
  **関わらず、フロートウィンドウがLoadedされれば常に実行される**ことを確認

**結論**：T-121コメントの「WM_MOVING/WM_EXITSIZEMOVEが一切発生しない」は、Float()実行直後・ユーザーが
まだ何も操作していない瞬間を指すものであり、AvalonDock自身のフック（およびecad2独自の
`PlacementToolBarFloatingWindowFilterMessage`、同じ`fwc.Loaded`イベントで並行して登録）は`AttachDrag`
と無関係に常にセットアップされる。**メニュー経由でフロート化した場合でも、その後ユーザーが実際に
そのウィンドウを手動でドラッグすれば、WM_MOVING/WM_EXITSIZEMOVEは正常に発生し、T-122の実装は問題なく
機能する**。重大な副作用なしと判断。

### 5. 追加確認：`UpdatePlacementToolBarDropZoneOverlayBounds`のnullガード

`originPaneControl`（`MainToolBar`ペイン探索結果）がnullの場合、座標更新をスキップし無言でreturnする
設計だが、`MainWindow.xaml:1019`で`MainToolBar`は`CanClose="False" CanFloat="False"`と定義されており、
アプリケーション実行中は常に存在し続けることが構造的に保証されている。実質到達不能な防御コードであり、
残すこと自体は妥当（過剰ではない）。

### 6. ビルド・テスト

`dotnet build src/Ecad2.sln -c Debug`成功。`dotnet test`全GREEN（Core131件・App803件）、回帰なし。

## 気づき（範囲外）

なし。
