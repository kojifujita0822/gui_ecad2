# T-099要件(2) 再ドック不成立 根本原因調査（隠密）

調査日: 2026-07-19
調査者: 隠密
委任元: 家老（殿の実機確認NG＋忍者実機再現を受けた独立調査、忍者はDragService/DockingIndicator周りを別途調査中・本調査は忍者所見と独立に進めたもの）

## 症状（家老報告の再掲）

「配置ツール」パネル（`PlacementToolBarDockingManager`）をフロート化後、メインウィンドウへの再ドッキングが2回試行とも不成立（殿実機操作・忍者実機再現とも一致）。

## 結論（推測、CONFIRMEDではない・机上調査のみ）

**T-099要件(3)実装（`docs/todo.md`記載、家老采配2026-07-18）で導入した「DockingManagerをAuto+Star列のGridで包み、Auto列の無限幅測定でマネージャ自身をコンテンツ要求幅まで縮める」構成が、フロート化でペインが空になった際にDockingManager自体のヒットテスト矩形をほぼ0まで縮小させ、AvalonDock DragServiceがドッキング候補ホストとして認識できなくなっている**、という仮説が最も筋が通ると判断する。

## 根拠（一次ソース、AvalonDock v4.74.1 GitHub本体）

### (1) ドッキング候補判定は完全に画面座標ヒットテストに依存する

`DragService.UpdateMouseLocation`（`source/Components/AvalonDock/Controls/DragService.cs:76-193`）は、`_overlayWindowHosts.FirstOrDefault(oh => oh.HitTestScreen(dragPosition))`（86行目）でマウス位置がいずれかの`IOverlayWindowHost`（＝`DockingManager`）内かを判定し、該当ホストが無ければ`_currentHost`は`null`のまま推移する（144-145行目で即return）。**ホストが1つも見つからなければオーバーレイウィンドウ（ドッキングインジケーター）自体が一度も表示されない**。

### (2) DockingManagerのHitTestScreenはActualSizeベースの矩形判定

`DockingManager.cs:1396-1414`：
```csharp
bool IOverlayWindowHost.HitTestScreen(Point dragPoint) => HitTest(this.TransformToDeviceDPI(dragPoint));

bool HitTest(Point dragPoint)
{
    try
    {
        var detectionRect = new Rect(this.PointToScreenDPIWithoutFlowDirection(new Point()), this.TransformActualSizeToAncestor());
        return detectionRect.Contains(dragPoint);
    }
    catch { /* DockingManager非表示時は握りつぶす */ }
    return false;
}
```
`detectionRect`のサイズは`TransformActualSizeToAncestor()`＝実際の画面上の実測サイズそのもの。**DockingManagerの`ActualWidth`/`ActualHeight`が0近くまで縮めば、この矩形も実質0になりヒットテストが恒常的に失敗する。**

### (3) ecad2側の該当構成

`src/Ecad2.App/MainWindow.xaml:776-782`（T-099要件3、未コミット）：
```xml
<Grid Grid.Row="1" Background="{DynamicResource ToolBarBackgroundBrush}">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
    </Grid.ColumnDefinitions>
<avalonDock:DockingManager x:Name="PlacementToolBarDockingManager" Grid.Column="0" ...>
    <avalonDock:LayoutRoot>
        <avalonDock:LayoutPanel Orientation="Horizontal">
            <avalonDock:LayoutAnchorablePane>
                <avalonDock:LayoutAnchorable Title="配置ツール" ContentId="PlacementToolBar" CanClose="False">
```
コメント（765-773行）に「DockingManagerをAuto+Star列のGridで包み、Auto列の無限幅測定によりマネージャ自身をコンテンツ要求幅まで縮める」と明記されている。**`PlacementToolBarDockingManager`は単一ペイン・単一コンテンツ構成**（T-058増分5由来、独立DockingManager方式）。フロート化するとその唯一のコンテンツがフロートウィンドウ側へ移り、元のペインは空になる。

### (4) 空ペインでもDockingManager側に最小サイズ保証が無い

`LayoutAnchorablePaneControl.cs`（AvalonDock標準）にMinWidth等の指定は無い（29-49行、コンストラクタでのバインド設定のみ）。`DockingManager.cs`側にもMeasureOverride独自実装・既定MinWidth指定は確認できず（`DockMinWidth`/`DockMinHeight`はモデル側の任意プロパティで、ecad2は明示指定していないと既存調査書`docs/ecad2-t058-increment5-redock-height-collapse-investigation-onmitsu.md`に記載済み）。

ecad2の`PlacementToolBarPaneControlStyle`（`MainWindow.xaml:171-295`、T-099根本修正で導入）も独自ControlTemplateで、`BorderThickness`は`TemplateBinding`（既定値未指定=0相当）、Row0=ContentPresenter（コンテンツ無しなら0×0）・Row1=HeaderPanel(ItemsHost、TabItem 0個なら0×0）という構造。**空状態で幅を主張する要素が無い**。

### (5) 空ペインが即座に消えるかは未確定だが、結論には影響しない

`LayoutRoot.CollectGarbage()`（`source/Components/AvalonDock/Layout/LayoutRoot.cs:352-419`）は、空ペインでも`PreviousContainer`から参照されている間は削除しない（389行目の条件）。つまりフロート化直後、元のペイン自体はツリーに残存している可能性が高い——**ただし残存していても「空」であることに変わりはなく、(4)により幅0近くまで縮む可能性は変わらない**ため、この論点は結論を左右しない。

## 追記（2026-07-19、家老からの事実関係整理を受けて仮説修正）

家老の申し送りにより時系列が判明：(1)殿の最初の実機確認＝NG (2)忍者のUIA操作2回とも不成立
(3)**その後、殿ご自身が再度操作を試み、成功を確認**（可視ウィンドウ1枚に復帰・パネル位置正常）。

「殿は最終的に成功させた」という事実は、当初の結論「矩形が完全に0で恒常的に失敗する」という
強い主張とは整合しない——矩形が真に0（存在しない）なら、原理的に人間の操作でも成功しえない
はずである。**より事実に近い仮説へ修正する**：矩形は0ではなく非常に狭い状態まで縮小しており、
「マウスを正確にその極小範囲へ通過させられるか」で成否が分かれる、という線が濃厚。

この修正仮説は、既存の忍者所見（要件(2)実機確認時、`docs/todo.md:305-307`「再ドックはUIA合成
マウス操作では不成立、AvalonDock DragServiceのインジケーター認識起因の技術的困難と推測」）とも
符合する。UIA合成操作（`SetCursorPos`+`mouse_event`等）は移動先座標を事前計算した離散的な
中間点移動になりやすく、極小矩形を「一瞬でも通過」させる精密なドラッグ軌跡の再現は、視覚
フィードバックを頼りに微調整できる人間の操作より不利な可能性が高い（`ninja.md`の既知の罠＝
UIA合成操作の限界と同根の可能性）。

**「矩形縮小」という骨子（要件3のAuto列包装が空ペイン時に矩形を圧迫する構造）自体は生きている
可能性が高いが、「完全消失」から「極小だが非ゼロ」へ確度を下げて報告する。** 正確な残存幅・
実際にゼロか否かは以下のUIA実測でのみ確定できる。

**さらに追記（2026-07-19、家老より再訂正）**：上記の「殿は成功させた」という反証情報自体が誤伝聞と
判明。殿へ確認した結果、殿操作での成功事例は「機器表パネル・部品選択パネル」の復帰であり、
**配置ツールバー自体の再ドック成功はまだ誰も確認できていない**とのこと。つまり「矩形が完全に0
という強い仮説への反証」は成立しておらず撤回された。**短時間で伝聞情報が二転三転した経緯を
そのまま記録に残す**（一次情報＝忍者のUIA実測を待つ以外に確定させる手段が無いことの裏付けでも
ある）。結論としては「完全に0」「極小・非ゼロ」いずれも依然として仮説段階のまま、対象を
配置ツールバーに明確化した上での忍者UIA実測結果が出るまでは確度を上げも下げもしない。

## CONFIRMED格上げ（2026-07-19、忍者UIA実測到着）

忍者の実測結果（家老経由）：フロート化直後、メインウィンドウ側の元ドッキング領域（`TabControl`＝
`PlacementToolBarPaneControlStyle`が適用される`LayoutAnchorablePaneControl`）のBoundsが
**`1974,144,569,81` → `25,6`（幅569→25px、高さ81→6px）まで縮小**、「ほぼ点」の状態と確認された。
フロートウィンドウ側は579px幅で一貫しており、縮むのはメインウィンドウ側の受け皿のみとの切り分け
も済んでいる。ドロップ後も再ドック不成立を確認。

**評価＝仮説の骨子（Auto列包装が空ペイン時に矩形を圧迫する構造）は実測で強く裏付けられた。
CONFIRMEDへ格上げして差し支えないと判断する。** ただし1点留保：今回の実測対象は
`LayoutAnchorablePaneControl`（TabControl）自体のBoundsであり、DragServiceのヒットテストが
直接参照する`DockingManager`自体のBounds（`DockingManager.cs:1396-1414`の`TransformActualSizeTo
Ancestor()`）ではない。両者はAuto列内で強く連動しており（子であるTabControlが25px相当まで縮めば
Auto列自体もほぼ同サイズまで縮小するのが自然）、**DockingManager自体も同程度まで縮小している
と強く推測されるが、これはTabControl実測からの論理的推論であり直接実測ではない**——完全な
CONFIRMEDとするにはDockingManager自体のBounds実測が望ましいが、現状の証拠だけでも原因帰属の
確度は十分高いと判断する。

「完全に0でなく25×6px」という非ゼロの結果は、以前の伝聞（殿操作で他パネルは成功）が示唆した
「精密な操作なら成功しうる余地」とも整合する——25×6pxという極小矩形でも、理論上は正確に
その範囲へマウスを通せば成功しうるが、通常の操作精度・UIA合成操作では現実的に困難、という
線で一貫性が取れる。

### 対処案の見立て（家老の問いへの回答）

3案のうち**案1（DockingManagerへMinWidth明示指定）が最有力**と判断する。

- **案3**（要件3の方式自体を見直し、ペイン側の制約で対応）は、要件3実装時点で既に
  `LayoutPanelControl.OnFixChildrenDockLengths`（水平LayoutPanel配下にLayoutDocumentPaneが
  無い構成でDockWidth類が強制Star上書きされる仕様）によりペイン側のDockWidth指定が機能しないと
  判明済み。同じ壁に再度当たる可能性が高く、有力候補から外れる。
- **案2**（フロート化中は親Grid列をAuto以外へ動的切替）は、フロート化イベント検知＋列幅切替の
  新規コードを要し複雑度が上がる。加えてタイミング依存の新規バグ（P-069のような理論的懸念と
  同種）を生むリスクがある。
- **案1**はDockingManagerへ`MinWidth`を1プロパティ追加するだけの最小実装。**MinWidthは下限保証
  のみに働くため、通常時（コンテンツあり、要求幅523px実測済み）のフィット動作には影響しない**
  ——Auto列の測定はコンテンツの要求幅とMinWidthの大きい方が採用されるため、MinWidth<523pxで
  ある限り通常時は無風。空状態（フロート化中）でのみMinWidth分の矩形が保証され、ヒットテストの
  成立余地が生まれる。

**MinWidthの具体的な値**は机上調査だけでは確定できない（AvalonDockのドロップターゲット
アイコン・ヒット領域の内部実装に依存）。通常時フィット幅（523px）より十分小さい値（目安として
100px前後）から侍が試作し、忍者が実測（フロート化→再ドック試行を段階的なMinWidth値で反復）
して最小成立値を詰めるのが現実的と考える。断定はせず、着手前の目安として提示する。

## 不明点（机上調査の限界）

- DockingManagerの`ActualWidth`が実際にどこまで縮むか（完全に0か、Grid既定の最小測定単位程度は
  残るか、あるいは何らかの残存要素で数px〜十数px程度残るか）はWPF Measure/Arrangeの実挙動次第で、
  UIA実測でしか確定できない。**「完全に0」という当初の強い主張は上記追記により確度を下げた。**
- フロート化直後・再ドック試行時点でのUIA実測（`PlacementToolBarDockingManager`の`BoundingRectangle`）が最有力の裏付け手段（家老采配により忍者が実測予定）。
- 忍者が別途進めているDragService/DockingIndicator調査の所見と、本仮説が一致するか要突き合わせ。
- 矩形が極小で非ゼロだとして、なぜ非ゼロなのか（Grid/Border/ContentPresenterのいずれかに残存する
  暗黙の最小サイズ）は本調査では特定できていない。実測値が判明次第、該当箇所の追跡調査が可能。

## 対処案（参考、判断は侍・家老・殿）

いずれも未検証の設計案、実装前に要検討：
1. **`PlacementToolBarDockingManager`へ`MinWidth`を明示指定**（フロート化で空になっても最低限のヒットテスト矩形を確保）。ただし通常時のコンテンツ幅フィット（要件3本題）を阻害しない値の選定が必要。
2. **フロート化中は親Grid列をAuto以外（固定値等）へ動的切替**——複雑度が上がる。
3. **要件3の「マネージャを縮める」方式自体を見直し**、ペイン側の制約（`DockMinWidth`等）で対応する方式へ転換——要件3実装時にDockWidth指定が効かないと判明した経緯（`LayoutPanelControl.OnFixChildrenDockLengths`）があるため、この方向は再度同じ壁に当たる可能性が高い。

## 家老への申し送り

本仮説はコード読解のみによる推測であり、UIA実測での裏付け（フロート化後・再ドック試行中の`PlacementToolBarDockingManager`実測サイズ）を経て初めてCONFIRMEDにできる。忍者の独立調査結果との突合を推奨する。対処は上記いずれも設計判断を伴うため、方針決定は殿・家老に委ねる。
