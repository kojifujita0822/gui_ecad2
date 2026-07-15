# T-058増分5（ツールバー2段目可動化）設計叩き台（侍）

作成日: 2026-07-15
対象: ツールバー2段目（F5〜F11・選択ツール、配置系）のみをAvalonDock管轄下へ移し、
フロート/再ドッキング可能にする。1段目（新規作成/開く/保存等）は現行`ToolBarTray`固定維持。

## 0. 前提（殿裁定の確認、todo.md T-058節429-432行）

- 1段目=新規作成/開く/保存等は現行`ToolBarTray`固定維持
- 2段目=F5-F10/部品(F11)のみAvalonDock管轄内でフロート/再ドッキング可能にする
- ツールバー管轄自体の技術的制約は無し（PoC実証済み）

## 1. 現状構造（MainWindow.xaml:176-431）

```xml
<ToolBarTray x:Name="ToolBarArea" Grid.Row="1" IsLocked="True">
    <ToolBar Band="0" BandIndex="0" ...>   <!-- 1段目: 新規/開く/保存/戻す/やり直し/PDF出力/行追加/行削除/テスト/一時停止 -->
    <ToolBar Band="1" BandIndex="0" ...>   <!-- 2段目: 選択ツール(Esc)/F5〜F10(8種)/自作パーツ(F11) -->
</ToolBarTray>
```
1つの`ToolBarTray`がBand順(0→1)で自動的に縦積みしている。

## 2. 設計方針: 増分1〜4のパターン踏襲＋既存Row構造への影響最小化

増分1〜4と同じ「独立DockingManager+LayoutAnchorable」パターンを踏襲するが、素朴に
新規DockingManagerを直接追加すると、既存の`MainContentArea`の`RowDefinitions`
(Auto/Auto/*/Auto/Auto)を1行増やす必要が生じ、以降の`Grid.Row`参照(メイン作業域=2、
GridSplitter=3、出力パネル=4)が軒並みずれる——変更範囲が不必要に広がる。

**対処**：既存の`Grid.Row="1"`(現在`ToolBarTray`が単独で占有している行)を、内側にAuto/Autoの
2行を持つ小さなネストGridへ差し替える(T-059のGridSplitter追加時と同型の「ラッパー内で行を
増やす」手法)。外側の`MainContentArea`のRow構成・以降の参照は一切変更不要。

```xml
<Grid Grid.Row="1">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>  <!-- 1段目(固定) -->
        <RowDefinition Height="Auto"/>  <!-- 2段目(可動化) -->
    </Grid.RowDefinitions>
    <ToolBarTray x:Name="ToolBarArea" Grid.Row="0" IsLocked="True">
        <ToolBar Band="0" BandIndex="0" ToolBarTray.IsLocked="True">
            <!-- 1段目ボタン群(現状のXAMLそのまま、変更なし) -->
        </ToolBar>
    </ToolBarTray>
    <avalonDock:DockingManager x:Name="PlacementToolBarDockingManager" Grid.Row="1">
        <avalonDock:LayoutRoot>
            <avalonDock:LayoutPanel Orientation="Horizontal">
                <avalonDock:LayoutAnchorablePane>
                    <avalonDock:LayoutAnchorable Title="配置ツール" ContentId="PlacementToolBar" CanClose="False">
                        <ToolBar Band="0" BandIndex="0">
                            <!-- 2段目ボタン群(現状のXAMLそのまま、Bandのみ0へ、変更なし) -->
                        </ToolBar>
                    </avalonDock:LayoutAnchorable>
                </avalonDock:LayoutAnchorablePane>
            </avalonDock:LayoutPanel>
        </avalonDock:LayoutRoot>
    </avalonDock:DockingManager>
</Grid>
```

2段目のボタン群自体(Style/ToolTip/AutomationProperties.Name/Command/IsEnabled/
PreviewKeyDown="ToolButtonPreviewKeyDown"等)は完全にそのまま移動するだけで、内容の変更は無い。

## 3. ツールバー特有の考慮点（洗い出し）

### 3-1. IsEnabledバインディングとの整合 → 問題なし

2段目各ボタンの`IsEnabled="{Binding CanEditDiagram}"`等は`ToolBar`(Content)内部の通常の
子要素バインディングであり、増分2/3で判明した§3のBinding罠（`LayoutAnchorable.Title`が
DataContext継承の対象外というオフツリー構造の話）とは無関係。`LayoutAnchorable`配下の
`Content`はAvalonDockが実ビジュアルツリー上へ配置するため通常のDataContext継承が効く
（増分2の調査で切り分け済み、`docs/todo.md`394-401行参照）。既存バインディングは無改修で機能する。

### 3-2. 既存イベントハンドラとの整合 → 問題なし

`Click="BuiltinPlaceButton_Click"`・`PreviewKeyDown="ToolButtonPreviewKeyDown"`等はボタン
自体に紐づくコードビハインドイベントで、ToolBarの配置場所(ToolBarTray配下かLayoutAnchorable
配下か)に依存しない。フロート化してもボタン単体の挙動は変わらない。

### 3-3. `ToolBarTray.IsLocked`の扱い

現行`IsLocked="True"`は「ユーザーによるツールバー位置変更・オーバーフロー折り畳み禁止」の
WPF標準機構だが、2段目は`ToolBarTray`配下から外れるため、この属性自体が不要になる
（可動性はAvalonDock側のドラッグ機構に完全に置き換わる）。1段目には引き続き付与する
（1段目は固定維持のため）。

### 3-4. フロート時の見た目（実機確認が必要、殿確認候補）

`ToolBar`はWrapPanelベースでOrientation既定Horizontal。フロート化するとAvalonDockの
`LayoutAnchorable`ヘッダー(タブ部分、Title="配置ツール")がボタン列の上に付いた小ウィンドウに
なる。GX Works3等の一般的なフローティングツールバーと比べてヘッダー分の視覚的な差異が出る
可能性があるが、これは増分1〜4のパネル群と統一感のある見た目でもあり、そのままでよいか、
装飾の要否は実機確認後に判断したい（PoC実証範囲内、致命的懸念ではない）。

### 3-5. ドッキング先の範囲

新規`PlacementToolBarDockingManager`は独立DockingManagerのため、既存の左パレット・出力
パネル・右パネルへドラッグドッキングすることはできない（各DockingManagerは完全に独立、
増分1〜4と同じ制約）。2段目は「フロート化⇔このDockingManager内へ再ドッキング」のみが可能。
これは殿裁定の「2段目のみAvalonDock管轄内でフロート/再ドッキング可能に」という範囲と一致する
と判断する（他パネルとの統合ドッキングは求められていない）。

### 3-6. T-058増分4（レイアウト永続化）・Ctrl+Alt+Rリセットとの統合

新規DockingManagerは既存の仕組みへそのまま組み込む（横展開のみ、新規ロジック不要）：
- `AllDockingManagers`配列へ`PlacementToolBarDockingManager`を追加（4要素目）
- `GetDockingLayoutFileName(string managerName)`のswitch式へ`nameof(PlacementToolBarDockingManager)
  => "placement-toolbar.xml"`を追加
- `RegisterDockingContents()`/`SerializeDefaultDockingLayouts()`/`ResetDockingLayoutToDefault()`/
  `LoadDockingLayoutFromFileIfExists()`は`AllDockingManagers`を汎用的に回っているため無改修で対応

### 3-7. `_toolButtonKeyboardClickSource`(キーボード操作の取り違え防止)への影響

`ToolButtonPreviewKeyDown`/クリックハンドラのsender一致判定(3ボタン限定、
`MainWindow.xaml.cs:2421-2433`)はボタンインスタンス自体の参照比較のため、配置場所移動の
影響を受けない。

## 4. タイトル同期メソッドの要否

増分2/3の`UpdateOutputPanelTitle`/`UpdateRightPanelBottomTitle`は「状況に応じてTitleを
動的に切り替える」ためのものだったが、2段目ツールバーのTitleは固定文言（「配置ツール」）でよく、
動的切替の要件はない（DoDにも言及なし）。**同型の同期メソッドは不要**と判断する。

## 5. スコープ境界

- 対象は`MainWindow.xaml`・`MainWindow.xaml.cs`のみ。2段目ボタン群自体の内容(Style/Command/
  ToolTip等)は無改修、配置場所のみ移動。
- 1段目(`ToolBarTray`)の内容・`ToolBarButtonStyle`等の既存スタイルは無改修。

## 6. 未確定・家老検分事項

- 新規`LayoutAnchorable`のTitle文言は「配置ツール」を仮案とした。他候補（「ツールパレット」等）
  があれば検分時に指摘願いたい。
- フロート時の見た目（3-4節）は実装後の実機確認で判断、致命的懸念があれば都度報告する。
