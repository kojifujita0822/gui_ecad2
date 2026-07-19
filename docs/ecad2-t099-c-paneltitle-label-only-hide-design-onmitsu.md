# T-099(c) 上段タイトルバー「ラベルのみ非表示」テンプレート再設計（隠密設計書）

設計日: 2026-07-19　設計者: 隠密　委任元: 家老（殿裁定=選択肢E採用: ラベルのみ非表示、
MenuDropDownButton=標準メニュー[Float/Dock/DockAsDocument/AutoHide/Hide]は常時操作可能に残す）
実装担当: 侍（本書は設計のみ、書き込みは侍へ委譲）

## 1. 現行実装の撤回対象

`src/Ecad2.App/MainWindow.xaml` の `PlacementToolBarAnchorablePaneTitleStyle`
（現行: `BasedOn="{StaticResource AnchorablePaneTitleNoDragHandleStyle}"`＋Style.Triggersの
DataTriggerで`Opacity=0`・`Height=5`を全体適用）を**丸ごと差し替える**。
Opacity=0（全体透明化）とHeight=5（極小化）はいずれも撤回する。

## 2. ラベル要素の特定（一次ソース確認済み）

ecad2が既に保有する完全コピーテンプレート`AnchorablePaneTitleNoDragHandleStyle`
（`MainWindow.xaml:318-557`、VS2013テーマ暗黙スタイルの忠実コピー、T-100）の構造:

```
Border (Background=TemplateBinding)
└ Grid Margin="2,2,3,3"  列構成: */Auto/Auto/Auto
   ├ 列0: DockPanel
   │   ├ Border Padding="2,0,4,0"
   │   │   └ DropDownControlArea（右クリックでAnchorableContextMenuを出す機能持ち）
   │   │       └ ContentPresenter x:Name="Header"   ← ★ラベルの実体
   │   │           (Content=Model、ContentTemplate=AnchorableTitleTemplate。
   │   │            既定テンプレートがModel.Title="配置ツール"をTextBlock表示する)
   │   └ Rectangle x:Name="DragHandleTexture"（ハッチング模様、T-100で既にCollapsed）
   ├ 列1: DropDownButton x:Name="MenuDropDownButton"（Width/Height=15、標準メニュー）← 残す
   ├ 列2: Button x:Name="PART_AutoHidePin"（IsEnabled連動Visibility）← 無変更
   └ 列3: Button x:Name="PART_HidePin"（IsEnabled連動Visibility、CanClose=False時は非表示）← 無変更
```

**ラベル＝`ContentPresenter x:Name="Header"`**。TextBlock自体はContentTemplate
（`AnchorableTitleTemplate`既定）内で実行時生成されるため、テンプレートコピー内で直接
狙えるx:Name付き要素としては`Header`が正しい標的（一次ソース上、これより内側の要素に
x:Nameは無い）。

## 3. 新設計

### 3.1 方式

`PlacementToolBarAnchorablePaneTitleStyle`を、**`AnchorablePaneTitleNoDragHandleStyle`の
ControlTemplate完全コピー＋ControlTemplate.Triggersへの1トリガー追加**を持つ独立Styleへ
差し替える（T-100と同じ「既定コピー+標的差し替え」手法）。

重要な技術的制約: **Style.Triggersの`Setter`は`TargetName`を指定できない**（WPF仕様、
TargetNameはControlTemplate.Triggers内でのみ有効）。テンプレート内要素`Header`だけを
消すには、ControlTemplate.Triggers側へトリガーを置くしかない——ゆえにBasedOn＋
Style.Triggersの現行軽量方式では実現不可能で、Template全体を持つ独立Styleが必須になる。

### 3.2 追加するトリガー（ControlTemplate.Triggers末尾へ）

```xml
<!-- T-099(c)最終形(殿裁定=案E): ドッキング時はラベル(Header)のみ非表示。
     MenuDropDownButton(標準メニュー、Float項目でAttachDragガードを回避した確実な
     フロート化が可能)は常時表示のまま残す。 -->
<DataTrigger Binding="{Binding Model.Parent.IsDirectlyHostedInFloatingWindow, RelativeSource={RelativeSource Mode=Self}}" Value="False">
    <Setter TargetName="Header" Property="Visibility" Value="Collapsed"/>
</DataTrigger>
```

- バインドパス`Model.Parent.IsDirectlyHostedInFloatingWindow`は現行実装で動作実績あり
  （フロート時=True/ドッキング時=False、PropertyChanged通知あり動的追従）。
- `RelativeSource Mode=Self`は既存テンプレートトリガー群（`Model.IsActive`参照、L458等）と
  同じパターンで整合。

### 3.3 変更しないもの

- `MenuDropDownButton`（列1）: 無変更。ドッキング時も常時表示・操作可能。
- `PART_AutoHidePin`/`PART_HidePin`（列2/3）: 無変更。既存のIsEnabled連動Visibilityに任せる
  （配置ツールバーは`CanClose="False"`のためHidePinは既定で非表示のはず。AutoHidePinの
  表示有無は実機確認項目とする）。
- Style本体の既定Setter（Background/Foreground、PR-21候補の観点）: NoDragHandleStyleと
  同一内容をコピー元から引き継ぐ（**BasedOnでなくコピーのため、Setter 2本
  [Background/Foreground、L319-320相当]の転記漏れに注意**——PR-21候補の型そのもの）。
- コントロール自身のフロート化ドラッグ機構（OnMouseLeftButtonDown/OnMouseLeave）: 無変更。
  タイトルバー全体のヒットテストは背景Border（`Background="{TemplateBinding Background}"`、
  非null維持）が担うため、Header Collapse後もドラッグ開始は機能する。

## 4. 設計上の検証（レビュー観点の先回り）

1. **Collapse化の副作用（T-099(c)発端の教訓）**: 今回Collapsedにする`Header`は
   ContentPresenter＝表示専用要素で、マウスイベント処理を内包しない（フロート化ドラッグは
   AnchorablePaneTitleコントロール自身のメソッド、ヒットテストは背景Borderが担う）。
   前回のCollapse化事故（コントロール全体を消しヒットテストごと殺した）とは対象の階層が
   異なり、同型の機能喪失は起きない——と設計上は判断するが、実機確認必須。
   なお副作用として、`Header`を包む`DropDownControlArea`（タイトルラベル右クリックで
   同メニューを出す機能）は中身が無くなり実質操作不能になるが、`MenuDropDownButton`経由で
   同一メニューへ到達できるため機能喪失にはならない（意図的に許容）。
2. **PR-20系統（優先順位競合）**: 既存テンプレートのControlTemplate.Triggers（L457-552相当）
   全数を精読済み——`Header`の`Visibility`を操作するSetterは皆無（Pin/ボタンの色・
   LayoutTransform関連のみ）。同一TargetName・同一Propertyの競合は無い。
   StaticResource固定解決（PR-20型1）・コンテナ生成前提（PR-20型2、HeaderはItemsHostでは
   ない）のいずれにも該当しない。
3. **DataTrigger vs 既定値の初期状態**: `Visibility`の既定はVisible、トリガー発火
   （ドッキング時=False）でCollapsed、フロート時はトリガー解除で自動的にVisibleへ復帰する
   （SetterはWPFのトリガー解除で元値へ戻る標準機構）。フロート時にタイトルラベルが
   表示されるのは望ましい挙動（フロートウィンドウの題名として機能）。

## 5. 高さへの影響見積もり（実装前報告用）

- ドッキング時の上段高さは**Height=5撤回により自然高さへ戻る**。自然高さの支配要素は
  `MenuDropDownButton`（Height=15＋Margin上1）＋Grid Margin（上2+下3）＋Border＝
  **約21px前後**（DPI・テーマ余白により前後、実測は忍者確認）。
- 比較: 現行（Height=5） → 新設計（約21px）で**約16px増**。ラベル付きフル表示時とは
  ほぼ同等（ラベルTextBlockの高さはボタン15pxより低く、高さの支配要素ではないため、
  ラベルを消しても高さはほぼ縮まない）。
- すなわち**T-099要件(1)の「省スペース化」のうち高さ面の効果はほぼ失われる**。得られるのは
  「ラベル文字の二重表示解消（視覚ノイズ低減）」と「標準メニューによる確実なフロート化
  経路の常時確保（本件の主目的）」。このトレードオフは殿裁定（案E採用）で織り込み済みと
  理解しているが、実装後の実機確認で高さの実測値を添えて改めて報告する。

## 6. 実装規模

- `MainWindow.xaml`のStyle 1個の差し替え（約240行のテンプレートコピー＋トリガー1個追加）。
  コード側（`ApplyDockingManagerThemes`のスタイル登録分岐）は変更不要（キー名不変のため）。
- MinWidth=100/MinHeight=93（DockingManager側、再ドック対策）は本設計と独立、無変更。

## 7. 忍者実機確認項目（実装後）

1. ドッキング時: ラベル「配置ツール」が消え、MenuDropDownButtonが見えて押せること。
2. MenuDropDownButton→「フローティング」で確実にフロート化されること（本件の主目的、
   物理マウスで確認）。
3. フロート化後: タイトルラベルが再表示されること（トリガー解除の自動復帰）。
4. 再ドッキング（ドラッグまたはメニューの「ドッキング」項目）が成立すること。
5. 上段の実測高さ（SizeChangedログまたはUIA Bounds）と、パネル全体の見た目のバランス。
6. PART_AutoHidePin（自動的に隠すピン）の表示有無と、押した場合の挙動が配置ツールバーとして
   許容範囲か（想定外の表示ならCanAutoHide=False設定の追加を検討、殿確認事項）。
7. 従来のタイトルバードラッグでのフロート化（物理マウス、これまで不成立だった経路）が
   本変更後どうなるかの再観察（ラベル消去は無関係のはずだが、Height=5→自然高さ復帰で
   ドラッグ可能領域が広がり改善する可能性がある——改善すれば副次的収穫）。
