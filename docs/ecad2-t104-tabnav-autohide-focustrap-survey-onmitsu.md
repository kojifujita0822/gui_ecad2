# T-104 DoD(4) キーボードナビゲーション継続NG 原因調査（家老委譲）

調査日: 2026-07-20　調査者: 隠密
発端: `docs/ecad2-t104-increment1-poc-verification-ninja.md`末尾、侍のGotFocus/PreviewKeyDown
診断ログ実測（Tab前進が`LayoutAnchorSideControl`⇔`ItemsControl`を往復しタブヘッダー間へ到達しない）

---

## 結論（先出し）

**実装ミスではなく、AvalonDockのDockingManager既定ControlTemplateが持つ構造的特性
（AutoHideサイド領域が未使用でも常に生成される）に起因する可能性が高い。** T-104固有の
バグではなく、複数タブ構成になったことで初めて顕在化した既存の潜在的挙動と見立てる
（確証は一次ソース精読の範囲、実機での追加検証は未実施——6節参照）。

---

## 一次ソース調査

`https://raw.githubusercontent.com/Dirkster99/AvalonDock/master/source/Components/AvalonDock/`
配下、`DockingManager.cs`・`Themes/generic.xaml`（DockingManagerの既定ControlTemplate）・
`Controls/LayoutAnchorSideControl.cs`・`Controls/AnchorablePaneTabPanel.cs`を取得・精読。

### DockingManagerの既定ControlTemplate（`generic.xaml:808-858`）

```xml
<ContentPresenter Grid.Row="1" Grid.Column="1" Content="{TemplateBinding LayoutRootPanel}" />
<ContentPresenter Grid.Row="0" Grid.RowSpan="3" Grid.Column="2" Content="{TemplateBinding RightSidePanel}" />
<ContentPresenter Grid.Row="0" Grid.RowSpan="3" Grid.Column="0" Content="{TemplateBinding LeftSidePanel}" />
<ContentPresenter Grid.Row="0" Grid.Column="0" Grid.ColumnSpan="3" Content="{TemplateBinding TopSidePanel}" />
<ContentPresenter Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="3" Content="{TemplateBinding BottomSidePanel}" />
<ContentPresenter x:Name="PART_AutoHideArea" ... Content="{TemplateBinding AutoHideWindow}" />
```

DockingManagerには、実際のタブ・コンテンツ領域（`LayoutRootPanel`）に加え、**4方向の
AutoHideサイド領域（Left/Right/Top/Bottom）が常にレイアウトツリー上に存在する**。

### 常に生成されることの裏付け（`DockingManager.cs:474-477`）

```csharp
LeftSidePanel = CreateUIElementForModel(Layout.LeftSide) as LayoutAnchorSideControl;
TopSidePanel = CreateUIElementForModel(Layout.TopSide) as LayoutAnchorSideControl;
RightSidePanel = CreateUIElementForModel(Layout.RightSide) as LayoutAnchorSideControl;
BottomSidePanel = CreateUIElementForModel(Layout.BottomSide) as LayoutAnchorSideControl;
```

`Layout.LeftSide`等（`LayoutAnchorSide`モデル）の`Children`が空（AutoHide機能を一切使って
いない状態）であっても、この4行は無条件に実行され、`LayoutAnchorSideControl`インスタンスが
必ず生成・セットされる。ecad2の各DockingManager（左パレット・出力・右パネル・配置ツールバー）
は`AnchorablePaneControlStyle`（タブヘッダー＋コンテンツ部分）のみをカスタムしており、
DockingManager自体のControlTemplateはカスタムしていない（既定のまま）ため、この4つの
「中身が空のAutoHideサイド領域」は**全DockingManagerに共通して常に存在する**。

### `LayoutAnchorSideControl`自体にFocus制御なし

`LayoutAnchorSideControl.cs`（208行、全文精読）を確認したが、`Focusable`・`IsTabStop`・
`KeyboardNavigation`関連のオーバーライドは一切無い（`Control`基底クラスの既定のまま）。
`AnchorablePaneTabPanel.cs`（95行、全文精読）にも同様にKeyboardNavigation関連の記述は無い。
このため、空であってもコンテナ自体（`LayoutAnchorSideControl`とその内部の`ItemsControl`
相当要素、`AnchorSideTemplate`内の`StackPanel`が`_childViews`をホストする）はFocusable
（既定値）のままVisualTree上に存在し、標準のKeyboardNavigationによるTabキー巡回対象に
含まれうる。

### 忍者実測との整合

忍者のGotFocus計装ログ（`LayoutAnchorSideControl`⇔`ItemsControl`の間を往復するのみ、
タブヘッダーへは一度も到達せず）は、まさにこの「常設・空のAutoHideサイド領域」への
迷い込みと一致する。

---

## (4)結論の整理

- **実装ミスの可能性**: 低い。`PlacementToolBarPaneControlStyle`（MainWindow.xaml:171-295）
  側のTabIndex/TabNavigation設定自体に明白な誤りは見当たらない。
- **AvalonDock構造的制約の可能性**: 高い。DockingManagerの既定ControlTemplateが持つ
  「未使用でも常設のAutoHideサイド領域」が、Tabキーの巡回対象になってしまう構造。
- **T-104固有ではない**: これまで各DockingManagerが単一タブ構成だった間は「タブヘッダー
  間をTabキーで移動する」という操作自体が生じなかった（そもそも移動先が1つしかない）ため、
  この構造的挙動は顕在化しなかったと見られる。T-104で初めて複数タブ構成になったことで、
  既存の（ecad2の他のDockingManagerにも潜在する）挙動が可視化された可能性が高い。

## 未確証・限界

- `LayoutAnchorSideControl`・`ItemsControl`が実際にFocusable=True/IsTabStop=Trueで
  レンダリングされていることの直接確認（実機でのUIA `IsKeyboardFocusable`確認等）は
  未実施——一次ソースにFalse化する記述が無いことからの推論に留まる。
- Grid（`generic.xaml:813`)自体のKeyboardNavigation.TabNavigation設定は既定値（未指定）
  であり、5領域（LayoutRootPanel→RightSidePanel→LeftSidePanel→TopSidePanel→
  BottomSidePanel→AutoHideArea、XAML出現順）がどの順でTab移動対象になるかは、WPF標準の
  ロジカルツリー順に依存するはずだが、実機ログの詳細な突き合わせ（生ログ全文）までは
  行っていない。

## 対策案（実装可否は侍・家老判断、隠密は提案のみ）

- ecad2側でこの4つのAutoHideサイド領域を明示的に`Focusable="False"`または
  `KeyboardNavigation.IsTabStop="False"`にする（DockingManager用の`Style`を新設し
  `LeftSidePanel`等へTemplateレベルで設定する、あるいは各DockingManagerインスタンスの
  `LeftSidePanel`等プロパティに直接設定を試す）。
- ただし影響範囲は配置ツールバーに限らずecad2の全DockingManager（左パレット・出力・
  右パネルも同型の空サイド領域を持つ）に及ぶ可能性があり、他パネルのキーボード操作への
  影響を確認する必要がある。

---

## 出典

- `DockingManager.cs`（3324行、全文）・`Themes/generic.xaml`（1716行、DockingManager
  ControlTemplate部分）・`Controls/LayoutAnchorSideControl.cs`（208行、全文）・
  `Controls/AnchorablePaneTabPanel.cs`（95行、全文）
  （[Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ、
  2026-07-20取得）
- `docs/ecad2-t104-increment1-poc-verification-ninja.md`（侍診断ログ実測記録）
