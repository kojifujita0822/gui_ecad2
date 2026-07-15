# T-058増分3（右パネル AvalonDock化）設計叩き台

侍記す。2026-07-15。家老の采配（T-058増分3着手命）を受け、実装着手前の設計案として提示する。
増分1・2で確立したパターンをそのまま踏襲する方針とし、新規の技術的分岐は最小限に留めた。

## 0. 前提（着手前必読事項の再確認）

- `docs-notes/handover-next-session.md`§3を原文Read済み：AvalonDockの`LayoutAnchorable`/
  `LayoutContent`系は`DependencyObject`直継承ゆえ通常のBinding（DataContext継承前提）が機能
  しない。Title等の状況依存表示は**コードビハインドでPropertyChanged購読→直接更新**方式
  （増分2・コミット79a60b2で確立）を用いる。
- `docs/todo.md` T-058節（L369-386「ドッキング位置制約調査完了」）で、右パネルの状況依存切替
  （プロパティ⇔部品選択）は既存Visibilityバインディングそのまま持ち込み可、隠密の「最難関」
  評価は実装コスト（移植手間）の見立てであり実現不可能性ではない、と確認済み。

## 1. 現状構造（`MainWindow.xaml` L551-631）

`Grid.Column="4"`の`Border`（`RightPanelArea`）内、Grid上下2分割（`GridSplitter`）：
- 上段：機器表（`DataGrid`、固定タイトル「機器表」）
- 下段：プロパティ（既定）⇔部品選択（`Tool.Mode==PlaceElement`中）の状況依存切替。
  `MainWindowViewModel.IsPartSelectionVisible`（算出プロパティ、`Tool`のsetterから
  `OnPropertyChanged`明示発火）にVisibilityバインドした2つの`DockPanel`を重畳する現行方式。
  タイトルも「プロパティ」/「部品選択」とDockPanel内TextBlockで手動出し分け。

## 2. 設計方針

### 2.1 DockingManager構成：3つ目の独立DockingManager新設（案C踏襲）

`LeftPaletteDockingManager`・`OutputPanelDockingManager`と同様、`RightPanelDockingManager`を
新設し`Grid.Column="4"`に配置する。左右GridSplitter（`Grid.Column="3"`、外側境界リサイズ用）は
維持、内部の上下`GridSplitter`（現行L575）はAvalonDock内蔵リサイザーに置き換わるため削除する
（増分1・2で確立済みの「DockingManager配下は自動的にドラッグリサイズ可能」パターン踏襲）。

### 2.2 XAML構造

```xml
<avalonDock:DockingManager x:Name="RightPanelDockingManager" Grid.Column="4">
    <avalonDock:LayoutRoot>
        <avalonDock:LayoutPanel Orientation="Vertical">
            <avalonDock:LayoutAnchorablePane>
                <avalonDock:LayoutAnchorable Title="機器表" ContentId="DeviceTable" CanClose="False">
                    <!-- 既存DataGrid(DeviceTableGrid)をそのまま移植 -->
                </avalonDock:LayoutAnchorable>
            </avalonDock:LayoutAnchorablePane>
            <avalonDock:LayoutAnchorablePane>
                <avalonDock:LayoutAnchorable Title="プロパティ" ContentId="RightPanelBottom" CanClose="False">
                    <!-- 既存の状況依存Grid(プロパティ⇔部品選択のVisibility切替)をそのまま移植。
                         Content内部のBindingはビジュアルツリーに実接続されるため機能する
                         (増分2の教訓＝Content内は正常、Titleのみオフツリーで機能せず)。 -->
                </avalonDock:LayoutAnchorable>
            </avalonDock:LayoutAnchorablePane>
        </avalonDock:LayoutPanel>
    </avalonDock:LayoutRoot>
</avalonDock:DockingManager>
```

- 単一`Orientation="Vertical"`の`LayoutPanel`一つのみ＝Orientation混在ネストに該当せず
  （隠密指摘(3)の既知バグ、v4.72.0まで・本件はネスト自体が発生しないため非該当と判断）。

### 2.3 ContentId命名（隠密指摘(1)：一意性確認）

`"DeviceTable"` / `"RightPanelBottom"`。既存`"LeftPalette"` / `"OutputPanel"`と非衝突。
（`"PropertyPanel"`案も検討したが、部品選択中はプロパティではなくなるため状況依存の実態と
名称がずれる懸念があり、位置ベースの`"RightPanelBottom"`を採用案とした——ここは異論あれば
差し替え可）

### 2.4 タイトル状況依存切替（増分2パターンの横展開）

下段`LayoutAnchorable.Title`は「プロパティ」⇔「部品選択」と状況に応じて変わるため、増分2の
`Find_PropertyChanged`→`UpdateOutputPanelTitle`と同型で対処する：

- `_viewModel.PropertyChanged`（既存の`ViewModel_PropertyChanged`、L222）に
  `e.PropertyName == nameof(MainWindowViewModel.IsPartSelectionVisible)`の分岐を追加し、
  `UpdateRightPanelBottomTitle()`を呼ぶ。
- `UpdateRightPanelBottomTitle()`は`UpdateOutputPanelTitle()`と同型（`ContentId`一致で
  `LayoutAnchorable`を探索し`Title`を直接更新）。
- 上段「機器表」は状況に依らない固定文字列のため、静的`Title`指定のままで問題ない
  （Bindingを使わないため罠に該当しない）。

### 2.5 CanFloat/CanAutoHide・レイアウトリセット・ContentIdレジストリ

- `CanFloat`/`CanAutoHide`は明示設定しない（増分2で隠密REFUTED済み＝Ctrl+Alt+Rによる事後救済
  方針が既定、対処漏れではないとの結論を踏襲）。
- `AllDockingManagers`（L107）に`RightPanelDockingManager`を追加するのみで、
  `RegisterDockingContents`/`SerializeDefaultDockingLayouts`/`ResetDockingLayoutToDefault`は
  既存の汎用実装がそのまま対応する（改修不要）。

### 2.6 隠密指摘(2)：CollectGarbage()による空LayoutDocumentPane自動挿入リスク

本設計は`LayoutDocumentPane`を使わない（`LayoutAnchorablePane`のみ、増分1・2と同一状況）ため、
リスクの発生条件は増分1・2から変化なし。既存方針（実機確認での経過観察）を踏襲し、新規の
対処は行わない。増分3の忍者実機確認でドラッグ移動・フローティング切り離しを伴う操作を行った
際に空パネル出現の有無を確認事項に加える。

## 3. スコープ確認

- 見た目・操作感（上下同時参照可能・タブ排他式にしない等）は現行のまま維持し、AvalonDock化に
  よる実現方式の置き換えのみを行う。UI/UX上の新規分岐は無いと判断しているが、家老検分で異論
  あれば差し替える。
- 部品選択リスト（`PartSelectionList`）・プロパティ入力欄（`DeviceNameBox`等）の中身・
  イベントハンドラは無改変で移植する。

## 4. 未確定・要検分事項

1. ContentId命名`"RightPanelBottom"`の妥当性（位置ベース vs 状態ベース、上記2.3参照）
2. `CollectGarbage()`空パネルリスクは「対処せず経過観察」で良いか（2.6）
3. 他に見落としている横展開チェック項目がないか

以上、家老の検分を仰ぐ。
