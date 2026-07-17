# T-083新規発見6調査: 要素配置時のパネル一瞬ライトモード化(隠密、静的調査)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 家老（殿直接指摘・忍者経由、T-099操作不能のため静的調査先行）

## 依頼内容(DoD)

現象「ダークモードで要素配置時、シートパネル・部品選択パネルが一瞬ライトモードに戻る」について、要素配置コマンド実行経路でテーマリソース（`Theme.Light/Dark.xaml`のMergedDictionaries）が一時的に再解決・再マージ・デフォルト値フォールバックしうる箇所の有無を調査。原因の特定または有力仮説の提示。

## 結論(先出し)

**コード調査だけでは確定的な発火源を特定できなかった**。要素配置コマンド経路（`TryPlaceElement`→`PlacementOkButton_Click`→`PlaceElementAtSelectedCell`→`RedrawCanvas`→`ClosePlacementBar`）を一次コードで全行程精読したが、`Application.Current.Resources.MergedDictionaries`や`DockingManager.Theme`を直接操作するコードは一切含まれていない——テーマ再適用ロジック（`ApplyUiChromeTheme`/`ApplyDockingManagerThemes`）は`IsDarkMode`プロパティ変更イベントでのみトリガーされ、要素配置経路とは実装上独立していることを確認した（**確定事実**）。

最も疑わしい候補として、**部品選択パネル（`PartSelectionList`）のVisibility切替（`IsPartSelectionVisible`、要素配置ツール起動と連動）**を提示するが、これは殿観察の「シートパネル」側の現象を説明できない（`SheetNavList`は常時Visibleでこの仕組みの対象外）ため、**仮説として不完全**。実機での画素採取・タイミング計測による検証が必要（**推測、机上調査の限界**）。

---

## 1. 要素配置コマンド経路の全行程確認：テーマ操作コードなし

出典: `src/Ecad2.App/MainWindow.xaml.cs`（2026-07-17実測）、`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（同）。

要素配置の一連の流れを追跡した：

1. `TryPlaceElement`（2831-2872行）：配置バー（`ElementPlacementBar`）を表示、`PositionPlacementBar`で位置決め、`Dispatcher.BeginInvoke(..., DispatcherPriority.Loaded)`でフォーカス移動を予約。
2. `PositionPlacementBar`（2890-2911行）：`ElementPlacementBar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity))`という**明示的なMeasure呼び出し**を行う（配置バーの実サイズ取得のため）。
3. `PlacementOkButton_Click`（3052-3066行）：`_viewModel.PlaceElementAtSelectedCell(...)`呼び出し→`RedrawCanvas()`→`ClosePlacementBar()`。
4. `PlaceElementAtSelectedCell`（`MainWindowViewModel.cs`2542-2616行）：`Sheet.Elements`へのモデル追加、`DeviceTable.Refresh()`、`NotifySelectedElementChanged()`のみ。`IsDarkMode`・テーマ・リソース関連への言及は皆無。
5. `RedrawCanvas`（549-558行）：`LadderCanvasHost.Draw(...)`呼び出しのみ（層A＝キャンバス描画専用、層C＝UIクロームとは無関係）。
6. `ClosePlacementBar`（3079-3083行）：`IsPlacementBarVisible = false`、`FocusCanvas()`。

**この一連の経路に`MergedDictionaries`・`DockingManager.Theme`・`FindResource`/`Application.Current.Resources`への操作は一切現れない**（`grep MergedDictionaries src/Ecad2.App/MainWindow.xaml.cs`で全該当箇所を確認済み、`IsDarkMode`のPropertyChangedハンドラ内=426-440行のみが該当し、要素配置経路からは呼ばれない）。

## 2. テーマ再適用ロジックの発火条件（既存実装の再確認）

`MainWindow.xaml.cs`395-440行のPropertyChangedハンドラ：`ApplyUiChromeTheme`・`ApplyDockingManagerThemes`は`e.PropertyName == nameof(MainWindowViewModel.IsDarkMode)`の分岐内でのみ呼ばれる（426行）。要素配置操作が`IsDarkMode`プロパティを変更する経路は、`PlaceElementAtSelectedCell`はじめ調査した範囲には存在しない。

`ApplyUiChromeTheme`自体（581-591行）は「既存Theme.*.xamlをRemove→新Theme.*.xamlをAdd」という2段階操作であり、構造上は一瞬リソースが空白になりうる弱点を持つ（T-100調査で把握済みの一般知識）——ただしこれが**要素配置操作によって呼び出される経路がない**以上、直接の引き金にはならないと判断する。

## 3. 有力仮説（不完全）：`IsPartSelectionVisible`切替によるVisibility遷移

`MainWindow.xaml`658-661行（増分5静的レビュー時に確認済みの構造）：

```xml
<DockPanel Visibility="{Binding IsPartSelectionVisible, Converter={StaticResource InverseBoolToVisibility}}">
    <!-- プロパティパネル -->
</DockPanel>
<ListBox x:Name="PartSelectionList" ItemsSource="{Binding PartPalette.SelectionEntries}" BorderThickness="0"
         Visibility="{Binding IsPartSelectionVisible, Converter={StaticResource BoolToVisibility}}">
```

`IsPartSelectionVisible`は`Tool.Mode==PlaceElement`の間trueとなり、要素配置ツール起動のたびに`PartSelectionList`（ListBox、増分5で暗黙的スタイル`TargetType="{x:Type ListBox}"`の対象）が`Collapsed`→`Visible`へ切り替わる。この切替タイミングで、WPFがそのListBoxのVisual Treeを（再）構築・測定する際に、暗黙的スタイル内の`DynamicResource`（`PanelContentBackgroundBrush`等）の解決に何らかのちらつきが生じている可能性を候補として挙げる（**推測、確証なし**）。

**この仮説の弱点**：`SheetNavList`（シートパネル、`MainWindow.xaml`523行）は`Visibility`のバインディングを持たず**常時Visible**であり、上記のメカニズムの対象外である。殿の観察は「シートパネル・部品選択パネル」両方が一瞬ライトモードに戻るというものであり、`IsPartSelectionVisible`切替だけでは**シートパネル側の現象を説明できない**。両パネルに共通するのはいずれも増分5の暗黙的ListBoxスタイルの対象という点のみであり、両者に同時に影響する共通トリガー（Application.Resources全体の一時的な不整合等）が存在する可能性の方が、観察事実（両パネル同時発生）とは整合的だが、その具体的なコードパスは今回の調査範囲では発見できなかった。

## 4. 不明点・調査の限界

- `SheetNavList`側の現象を説明する具体的なコードパスは発見できず（**未解明**）。
- `ElementPlacementBar.Measure()`（`PositionPlacementBar`内、明示的な強制Measure）が`RootLayoutGrid`全体の再レイアウトを誘発し、間接的に兄弟要素（AvalonDockペイン内の`SheetNavList`/`PartSelectionList`）のレイアウトパスに影響する可能性は理論上排除できないが、これが「テーマリソースの一時的フォールバック」を引き起こす技術的根拠は今回のコード調査では確認できなかった（**推測の域を出ない**）。
- PR-18（色対応調査の網羅性不足）・増分1/7の類似AvalonDock/WPF挙動との関連について、増分1・7はいずれも「値は正しいが静的に描画へ反映されない（StaticResource固定化等）」という**恒常的な**問題だったのに対し、本件は「一瞬だけ間違った値が見えてすぐ戻る」という**過渡的な**現象であり、性質が異なる。単純な類推は適用しにくいと判断する（**推測**）。
- 本件はコード調査のみでは限界があり、忍者による実機再現・画素採取（時系列でのフレーム単位撮影、あるいは前回T-099調査で有効だった診断ログ注入）が本命の次の一手と考える。ただしT-099のツールバー崩れで要素配置操作自体が現状できないため、**T-099の解決（対症療法復元含む）を待って本件の実機検証に着手するのが現実的な順序**と考える（家老裁定と同じ理解、確認のため記載）。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- アプリ側コード（2026-07-17実測）：`src/Ecad2.App/MainWindow.xaml.cs`（`TryPlaceElement`2831-2872行、`PositionPlacementBar`2890-2911行、`PlacementOkButton_Click`3052-3066行、`RedrawCanvas`549-558行、`ClosePlacementBar`3079-3083行、PropertyChangedハンドラ395-440行、`ApplyDockingManagerThemes`562-576行、`ApplyUiChromeTheme`581-591行）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`PlaceElementAtSelectedCell`2542-2616行）
- `src/Ecad2.App/MainWindow.xaml`（`PartSelectionList`/`SheetNavList`定義、658-661行・523行）
- `docs/todo.md` T-083節（新規発見6の経緯、459-462行）
- `docs-notes/pattern-recurrence-log.md`（PR-18関連、参照のみ・本件との直接一致なしと判断）
