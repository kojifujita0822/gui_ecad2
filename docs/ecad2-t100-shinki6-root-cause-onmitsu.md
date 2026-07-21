# T-100新規発見6 根本原因調査（隠密、再調査）

日付: 2026-07-21　調査者: 隠密（key=1784601476926）
契機: 忍者による実機再現確定（`docs/ecad2-t100-shinki6-verify2-ninja.md`、2026-07-21）。「セルクリック後・OK確定前(インラインバー表示中)、シートパネル・部品選択パネルが持続的に純白化、両パネル完全同期、機器表・出力パネルは終始正常、OK確定で即復帰」という確定情報を受けての再調査。

## 結論：根本原因を確定した

**`MainContentArea`（メニュー・ツールバー・左パレット・キャンバス・右パネル・出力パネルを含む巨大なGrid）のIsEnabledが、配置バー(インラインバー)表示中`false`になる設計と、WPF既定Aero2テーマのListBoxコントロールテンプレートが持つ「IsEnabled=false時にBackgroundを白固定色へ強制上書きするControlTemplate.Trigger」の組み合わせが原因**。前回調査（`docs/ecad2-t083-shinki-hakken6-theme-flicker-survey-onmitsu.md`、2026-07-17）の仮説「`IsPartSelectionVisible`切替時のDynamicResource解決のちらつき」は誤りと判明した（下記「前回仮説との関係」参照）。観察された全事実（両パネル同時発生・持続性・対照パネルの無事・OK確定での即復帰）を矛盾なく説明できる。

## 発火経路（一次ソース確認済み）

1. `MainWindowViewModel.cs`202行: `public bool IsMainContentEnabled => !IsPlacementBarVisible && !IsRungCommentEditorVisible && !IsFrameLabelEditorVisible;`
2. `MainWindow.xaml`889-890行: `<Grid x:Name="MainContentArea" Grid.Row="0" Grid.RowSpan="4" IsEnabled="{Binding IsMainContentEnabled}">`——メニューバー・ツールバー・左パレット(`LeftPaletteDockingManager`、`SheetNavList`含む)・キャンバス・右パネル(`RightPanelDockingManager`、`PartSelectionList`・`DeviceTableGrid`含む)・出力パネル(`OutputPanelDockingManager`)を全て内包する。
3. `MainWindow.xaml.cs`3334行`TryPlaceElement`: `_viewModel.IsPlacementBarVisible = true;`→`IsMainContentEnabled`が`false`化→`MainContentArea.IsEnabled=false`。
4. WPFの`IsEnabled`は明示的にローカル値を持たない限り親の無効化状態を実効的に継承する（`FrameworkElement`の仕様）。`SheetNavList`（`MainWindow.xaml`1353行）・`PartSelectionList`（同1574行）とも、XAML定義にIsEnabledのローカル設定は存在しない（確認済み）ため、両者ともMainContentArea無効化の影響を直接受ける。
5. `App.xaml`23-26行: `ListBox`型への暗黙的スタイル（`TargetType="{x:Type ListBox}"`、x:Keyなし）。`Background`/`Foreground`のみ`DynamicResource`でオーバーライドし、**`Template`は指定していない**（WPF既定Aero2テンプレートをそのまま継承）。
6. WPF本体一次ソース `dotnet/wpf` `src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Aero2/Themes/Aero2.NormalColor.xaml`（2026-07-21取得、scratchpadへcurl保存し直読）2524-2565行: `ListBox`既定`ControlTemplate`。2549-2553行:
   ```xml
   <ControlTemplate.Triggers>
       <Trigger Property="IsEnabled" Value="false">
           <Setter TargetName="Bd" Property="Background" Value="{StaticResource &#327;}" />
           <Setter TargetName="Bd" Property="BorderBrush" Value="{StaticResource &#328;}" />
       </Trigger>
   ```
   `&#327;`は同ファイル2521行で`<SolidColorBrush x:Key="&#327;" Color="#FFFFFFFF" />`と定義された**白固定色**（`&#328;`は`#FFD9D9D9`）。`StaticResource`参照であり、アプリ側の`DynamicResource PanelContentBackgroundBrush`オーバーライドは原理的に届かない（`onmitsu.md`記載PR-20パターン1「StaticResourceの固定解決」と一致）。
7. 結果：インラインバー表示中は`SheetNavList`・`PartSelectionList`の`Bd`（既定テンプレート内のBorder）のBackgroundが白へ強制上書きされ、両パネルが同時に`#FFFFFFFF`化する。OK確定で`IsPlacementBarVisible=false`に戻ると`IsEnabled=true`に復帰しTriggerが解除、`TemplateBinding Background`経由の`DynamicResource PanelContentBackgroundBrush`（ダーク色）へ即座に戻る。

## 「機器表・出力パネルは正常」の説明

機器表（`DeviceTableGrid`）・出力パネルとも`DataGrid`型（`ListBox`ではない）。同じくAero2既定テンプレート（同ファイル973-1200行付近、DataGrid用ControlTemplate）を確認したが、**`IsEnabled`に言及する`ControlTemplate.Trigger`は存在しない**（`grep -n IsEnabled`で該当範囲に0件）。よってDataGrid系パネルはIsEnabled=false化してもBackgroundの強制書き換えを受けず、正常な配色を保つ。この型による差異が、殿観察「両パネル(ListBox系)のみ発生・対照パネル(DataGrid系)は無事」を完全に説明する。

## 前回仮説との関係

前回（`docs/ecad2-t083-shinki-hakken6-theme-flicker-survey-onmitsu.md`）は「要素配置コマンド経路にテーマ操作コードが無い」ことは正しく確認していたが、**トリガーが「配置確定操作」ではなく「インラインバー表示中というIsEnabled状態そのもの」であることに気づけなかった**（当時は「一瞬の現象」という前提で捜査しており、`IsPartSelectionVisible`というVisibility切替に着目していた）。今回、忍者の実機確認で「セルクリック後・OK確定前の持続的な状態」と時間軸が確定したことで、`IsPlacementBarVisible`→`IsMainContentEnabled`→`MainContentArea.IsEnabled`という別の経路に気づけた。「シートパネルは常時Visibleでこの仕組みの対象外」という前回の壁は、Visibility切替ではなくIsEnabled継承という別軸で両パネルに共通することが分かり解消した。

## 対策候補（実装要否・方式は家老・侍判断、隠密は選択肢の提示のみ）

1. **ControlTemplate差し替え方式**（T-106で確立済みの既定パターンを踏襲）：`App.xaml`のListBox暗黙的スタイルに`Template`を明示指定し、既定Aero2テンプレートをコピーした上で、`ControlTemplate.Triggers`内の`IsEnabled=false`時の`Background`/`BorderBrush`を`{StaticResource &#327;/&#328;}`から`{DynamicResource PanelContentBackgroundBrush}`等へ差し替える。影響範囲はアプリ全体の`ListBox`（`SheetNavList`・`PartSelectionList`以外にも暗黙適用される箇所がないか要棚卸し）。
2. **無効化方式の変更**：`IsMainContentEnabled`によるツリー全体の`IsEnabled=false`化をやめ、キャンバス操作ブロックという本来の目的（配置バー表示中の誤操作防止）に絞った代替手段（例：キャンバス個別の`IsHitTestVisible=false`、または`PreviewKeyDown`側でのガード）に変更する。既存コメント（MainWindow.xaml 1731行等）によれば「MainContentArea全体ごと無効化されると選択中の視認性」に関する既存の設計意図があるため、変更时はUundo/Redo・フォーカス制御等への副作用範囲の洗い出しが必要。設計変更の規模がより大きい。

いずれもUI/UX上の見え方に関わる選択であり、着手する場合は`onmitsu.md`既定どおり家老経由で殿確認を挟む必要があると考える（隠密の推奨は特に無し、対策1が既存パターン踏襲で影響範囲が読みやすいという程度の所見に留める）。

## 派生提案（範囲外の気づき）

`IsMainContentEnabled`は`IsPlacementBarVisible`だけでなく`IsRungCommentEditorVisible`・`IsFrameLabelEditorVisible`にも連動している（`MainWindowViewModel.cs`202行）。行コメントエディタ・枠ラベルエディタの表示中も同一メカニズムで同様の白化が起きる可能性が高い（未検証、推測）。対応方針を検討する際はこの2箇所も併せて確認範囲に含めるべきと考える。`docs/proposed.md`行きの派生提案として家老へ申し送る。

## 不明点

- 対策1採用時、アプリ全体で`ListBox`の暗黙的スタイルが適用される他の箇所（本調査ではSheetNavList/PartSelectionList以外を網羅的には棚卸ししていない）に意図しない影響が及ばないかは、対策実装時に別途棚卸しが必要（本調査のスコープ外）。

## 出典

- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`IsMainContentEnabled`202行、`IsPlacementBarVisible`164-180行付近）
- `src/Ecad2.App/MainWindow.xaml`（`MainContentArea`889-890行、`SheetNavList`1353-1360行、`PartSelectionList`1574-1575行、`DeviceTableGrid`1465行、`OutputPanelDockingManager`1612行）
- `src/Ecad2.App/MainWindow.xaml.cs`（`TryPlaceElement`3300-3341行）
- `src/Ecad2.App/App.xaml`（ListBox暗黙的スタイル23-26行）
- WPF本体一次ソース `dotnet/wpf`（GitHub、2026-07-21 curl取得）`src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Aero2/Themes/Aero2.NormalColor.xaml`（ListBoxスタイル2524-2565行、DataGridスタイル973-1200行付近）
- `docs/ecad2-t100-shinki6-verify2-ninja.md`（忍者実機確認、2026-07-21）
- `docs/ecad2-t083-shinki-hakken6-theme-flicker-survey-onmitsu.md`（前回調査、2026-07-17）
- `docs-notes/roles/onmitsu.md`（PR-20「値は正しいが反映されない」系パターン節）
