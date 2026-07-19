# T-099要件(1)(2) 「AnchorablePaneTitle」二重ヘッダー疑義 調査（隠密）

調査日: 2026-07-19
調査者: 隠密
委任元: 家老（忍者のUIA実測所見を受けた優先度高の緊急依頼、task_id=T-099）

## 発端（忍者所見の再掲、家老経由）

配置ツールバーのタイトル帯を構成する要素のAutomationId（`MenuDropDownButton`・`PART_AutoHidePin`・`PART_HidePin`）がAvalonDock標準テンプレート命名と一致し、単一ペイン専用の別コントロール由来の可能性が高い。事実ならT-099要件(1)(2)実装（`PlacementToolBarPaneControlStyle`、対象=`LayoutAnchorablePaneControl`のTabItem）は、配置ツールバーが単一ペイン状態の間は効いていないことになり、既存の「要件(1)(2)実機確認OK」という記録と矛盾する。

## 依頼内容

一次ソース確認：(1) AvalonDockで該当コントロールがいつ使われるか（単一ペイン限定か） (2) ecad2側に当該コントロール向けのスタイルカスタマイズが存在するか (3) 存在するならその内容。

## 結論

**忍者の指摘は事実と確定する。T-099要件(1)(2)は、実際に画面へ表示される2種類の「タイトル表示要素」のうち片方（下段のTabItem）にのみ適用されており、もう片方（上段の`AnchorablePaneTitle`、AvalonDock標準の常設タイトルバー）には一切及んでいない。**

## 根拠（一次ソース、AvalonDock v4.74.1 GitHub本体）

### (1) 対象コントロールは`AnchorablePaneTitle`（`LayoutAnchorableControl`のHeader部分）

`MenuDropDownButton`・`PART_AutoHidePin`・`PART_HidePin`はいずれも`AnchorablePaneTitle`のControlTemplate内の要素（VS2013テーマ版`source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml:562-800`付近、本体版`source/Components/AvalonDock/Themes/generic.xaml`にも同型定義あり）。

`AnchorablePaneTitle.cs`のクラスコメント（19-23行）：
> This control defines the Title area of a `LayoutAnchorableControl`. It is used to show a title bar with docking window buttons to let users interact with a `LayoutAnchorable` via drop down menu click or drag & drop.

＝`LayoutAnchorablePaneControl`（TabControl、ecad2の`PlacementToolBarPaneControlStyle`がTargetTypeとする対象）とは完全に別のコントロール。

### (2) `AnchorablePaneTitle`はドッキング時、単一・複数ペイン問わず常時表示される

`LayoutAnchorableControl`のControlTemplate（`source/Components/AvalonDock/Themes/generic.xaml:863-927`）：
```xml
<Border x:Name="Header">
    <avalonDockControls:AnchorablePaneTitle Model="{Binding Model, RelativeSource={RelativeSource TemplatedParent}}" />
</Border>
...
<ControlTemplate.Triggers>
    <MultiDataTrigger>
        <!-- Hide the title if the control is directly hosted in floating window -->
        <MultiDataTrigger.Conditions>
            <Condition Binding="{Binding ..., Path=Model.IsFloating}" Value="True" />
            <Condition Binding="{Binding ..., Path=Model.Parent.IsDirectlyHostedInFloatingWindow}" Value="True" />
        </MultiDataTrigger.Conditions>
        <Setter TargetName="Header" Property="Visibility" Value="Collapsed" />
    </MultiDataTrigger>
    ...
</ControlTemplate.Triggers>
```
Header（`AnchorablePaneTitle`）がCollapsedになる条件は「フロート化していて、かつ親が直接フロートウィンドウにホストされている」場合のみ。**ドッキング時は常にVisible**（単一ペイン・複数ペインの区別なし）。この`LayoutAnchorableControl`自体は`PlacementToolBarPaneControlStyle`の`ContentTemplate`（`MainWindow.xaml:288-293`）で明示的に選択中コンテンツの表示に使われている：
```xml
<Setter Property="ContentTemplate">
    <Setter.Value>
        <DataTemplate>
            <avalonDock:LayoutAnchorableControl Model="{Binding}"/>
        </DataTemplate>
    </Setter.Value>
</Setter>
```

### (3) ecad2側の既存カスタマイズ（T-100）はラベルテキストを対象にしていない

`MainWindow.xaml:318-`の`AnchorablePaneTitleNoDragHandleStyle`（T-100、家老采配2026-07-17）は`AnchorablePaneTitle`のControlTemplateをコピーしたもの。しかしその変更点は`DragHandleTexture`（ハッチング模様、356-364行）を`Visibility="Collapsed"`にすることのみ。**ラベルテキスト自体を表示する`Header`という名の`ContentPresenter`（348-353行）は一切手を加えられておらず、常時表示のまま**：
```xml
<ContentPresenter
    x:Name="Header"
    Content="{Binding Model, RelativeSource={RelativeSource TemplatedParent}}"
    ContentTemplate="{Binding Model.Root.Manager.AnchorableTitleTemplate, ...}"
    ...
```

## 評価

T-099要件(1)「ドッキング時タブ(ラベル)を非表示、帯のみ表示」は、`PlacementToolBarPaneControlStyle`の`ItemContainerStyle`（`MainWindow.xaml:209-278`、TabItemのControlTemplate内DataTrigger）にのみ実装されている。これは`LayoutAnchorablePaneControl`が管理する**下段のタブストリップ**（`TabStripPlacement="Bottom"`指定）を対象とした変更である。

一方、`AnchorablePaneTitle`は`LayoutAnchorableControl`の**上段**（Grid.Row="0"、コンテンツ本体の上）に位置する別要素であり、T-099はこちらには一切触れていない。T-100スタイルはこの上段のハッチング模様のみを消したに過ぎず、ラベルテキスト自体の非表示化はスコープ外だった。

**結論として、配置ツールバーがドッキングされている通常状態では、上段に「配置ツール」ラベル付きの標準タイトルバー（`AnchorablePaneTitle`）が今も表示されたままである可能性が非常に高い。** 過去の「要件(1)(2)完全決着」の実機確認（忍者、2026-07-18）は下段のTabItem（帯のみ表示、4倍拡大画像で確認）のみを見ており、上段の`AnchorablePaneTitle`の存在自体を見落としていた疑いが濃厚である。

## 不明点（机上調査の限界）

- 上段（`AnchorablePaneTitle`）と下段（TabItem）が実際に画面上で**両方同時に**表示されているか（二重表示になっているか）は未確認。理論上はドッキング時どちらもVisibleのはずだが、他の要因（例えば`TabStripPlacement="Bottom"`かつ単一アイテムでAvalonDockが何らかの理由でタブストリップ自体の高さを潰す等）で見えにくくなっている可能性も否定できない。
- 実機確認・スクリーンショット（特にパネル全体を撮影し、上段・下段の両方を視野に入れる）が必要。忍者への確認依頼が妥当。

## 対処案の方向性（参考、未検証）

方向性としては前向きと考える：`AnchorablePaneTitleNoDragHandleStyle`へ、T-099要件(1)と同様の「ドッキング時はHeader（ラベル）を非表示にし細い帯のみ表示する」DataTriggerを追加すれば、設計としては筋が通る（既存のDataTrigger条件`Model.IsDirectlyHostedInFloatingWindow`をそのまま流用できる可能性が高い）。

ただし1点、設計上の考慮が必要：`AnchorablePaneTitleNoDragHandleStyle`は現状、`MainWindow.xaml.cs`の`ApplyDockingManagerThemes`から**全DockingManager共通**で適用されている（T-100のスコープ＝全パネル共通のハッチング模様除去）。T-099要件(1)(2)は**配置ツールバーのみを対象**とする方針（殿確認済みの対象範囲、他パネルには影響させない）だったため、もしHeader非表示化を追加するなら、配置ツールバー専用の別スタイル（またはDockingManager単位の条件分岐）へ切り分ける設計変更を要する。

## 対処案の設計イメージ（追記、家老依頼2026-07-19、未検証）

`MainWindow.xaml.cs:596-610`の`ApplyDockingManagerThemes`は`AllDockingManagers`全件へ同一の`AnchorablePaneTitleNoDragHandleStyle`を`manager.Resources[typeof(AnchorablePaneTitle)]`として一括登録している。これが「全パネル共通」になっている直接の原因。

設計案（机上のみ、未コンパイル・未実機確認）：

1. 新スタイル`PlacementToolBarAnchorablePaneTitleStyle`を新設、`AnchorablePaneTitleNoDragHandleStyle`をBasedOnし、`Style.Triggers`へ以下を追加：
```xml
<DataTrigger Binding="{Binding Model.Parent.IsDirectlyHostedInFloatingWindow, RelativeSource={RelativeSource Self}}" Value="False">
    <Setter Property="Visibility" Value="Collapsed"/>
</DataTrigger>
```
`AnchorablePaneTitle.Model`は`LayoutAnchorable`型（個別コンテンツ）のため、`Model.Parent`で`LayoutAnchorablePane`へ辿り`IsDirectlyHostedInFloatingWindow`を参照する経路になる。T-099要件(1)のバインドパス（`MainWindow.xaml:246`、`Model.IsDirectlyHostedInFloatingWindow`）は`LayoutAnchorablePaneControl.Model`＝`LayoutAnchorablePane`自体を起点としており、起点が1階層異なる点に注意。

2. `ApplyDockingManagerThemes`内で、対象が`PlacementToolBarDockingManager`か否かにより登録するスタイルを分岐する（三項演算子程度の変更で足りる見込み）。

この方式なら上段（`AnchorablePaneTitle`）はドッキング時Collapsedで完全に消え、下段のTabItem帯のみが見える構図になり、T-099要件(1)当初の意図（ラベル無し・帯のみ）に最も忠実になる。上段に代替の帯を新設する必要は無いと見立てる（下段の帯で既にドラッグ起点・視覚的一体感は足りているため）。

**未検証点**：(a) `Style.Triggers`によるVisibility制御が、Template自体のBasedOn継承と正しく協調するか（コンパイル・実機で要確認） (b) `LayoutAnchorableControl`のGrid行（`Height="Auto"`）がHeader Collapsed時に正しく0へ縮むか（通常のWPF挙動どおりのはずだが要実機確認）。侍への実装采配時、この2点を確認項目に含めることを推奨する。

## 実機確認結果（追記、2026-07-19、忍者）

「二重表示」ではなく、**「上段（AnchorablePaneTitle、未対応）が健在・下段（TabItem、T-099対応済み）は単一タブゆえ視認不能なほど縮小」**という実態が確定した（家老経由）。本調査の理論的推測（上段が常時Visible）は裏付けられたが、下段側についても新たな論点が生じた。

### 新たな懸念（下段TabItemの視認性）

下段の帯（`DockedDragHandle`、`MainWindow.xaml:237-241`）は`Height="5" MinWidth="20"`という指定のみ。単一タブ構成のTabStrip（`HeaderPanel`、Row1="Auto"）がこの1個のTabItemのみを含む場合、全体の高さが5px+余白程度まで縮み、実機では「視認不能なほど」小さく見える、というのが忍者所見の実態と推測される（推測、忍者所見の直接引用ではない）。

**この点は対処案（上段Collapse化）とは独立した論点**：上段をCollapsedにして下段のみを残す設計にしても、その下段自体が視認困難なほど小さいままでは、T-099要件(1)(2)の目的（GX Works3様式の細い帯によるドラッグ起点の視認性確保）が達成されない可能性がある。侍への実装采配時、上段Collapse化とあわせて、下段帯の高さ・視認性（必要なら`Height`値の見直し）も確認項目に加えることを推奨する。ただしこれもUI/UX判断（GX Works3実物でどの程度の太さだったか等）を伴いうるため、断定はしない。

## 家老への申し送り

事実確認（一次ソースでの構造解明）は完了。次段階は実機確認（上段・下段の同時表示有無）による裏付けが必須と考える——実機確認結果を受けて上記追記済み。対処方針の決定は殿・家老・侍の設計判断に委ねる。
