# T-100根本原因調査: ドッキング済みタブヘッダーのハッチング模様(隠密)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 殿直接指示（密命、家老経由でなく直接下命）

## 経緯

T-100は2026-07-17に「一旦保留」（殿裁定）となっていた。侍の先行試行（コミット`8650a66`、`AnchorablePaneTitleStyle`のGrid列0を`Width="*"`から`Auto`へ変更）は侍の自己目視では「改善を確認」と報告されたが、殿の実機直接観察では「治っていない」との食い違いがあり、保留のまま据え置かれていた（`docs/todo.md` T-100節325-342行）。今回、殿より隠密へ直接「これは仕様なのか、対処できるものか空いた時間で調査して」とのご下命があり、一次ソースから仕切り直して調査した。

## 依頼内容(DoD)

1. ハッチング模様は仕様か、対処可能なバグか
2. 対処方法の提示

## 結論(先出し)

1. **仕様である（バグではない）**。AvalonDockのVS2013テーマパッケージ（`Dirkster.AvalonDock.Themes.VS2013`）の`AnchorablePaneTitleStyle`（ドッキング済みパネルのタイトル表示部）に、`x:Name="DragHandleTexture"`という名の`Rectangle`要素が明示的に実装されている。これはVisual Studio系IDEでお馴染みの「ここをドラッグしてパネルを移動できる」ことを示す視覚的グリップ（点描テクスチャ）であり、意図的なデザイン要素と確認した。
2. **対処は可能**。この`DragHandleTexture`要素を直接標的にした派生スタイルをアプリ側で定義すれば、模様を完全に消せる見込み。
3. **侍の先行試行（列0のAuto化）が効かなかった理由も技術的に説明できる**：模様の原因は列0の余白そのものではなく、`DockPanel`の`LastChildFill`機構により最後の子要素（`Rectangle`）が残りスペースを自動的に占有する構造にあるため。

---

## 1. 技術的根拠：VS2013テーマ一次ソース解析

出典: [Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ、`source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml`（2026-07-17取得、全2933行中の該当箇所を精読）。

### 1.1 AvalonDock本体(generic.xaml)には該当要素なし

まずAvalonDock本体（テーマ未指定時の既定、`source/Components/AvalonDock/Themes/generic.xaml`）の`AnchorablePaneTitleStyle`（297-378行）を確認したが、装飾パターンを描画する要素（`VisualBrush`/`DrawingBrush`等）は一切存在しない。ecad2はT-058/T-083でVS2013テーマパッケージを導入済みのため、実際に適用されているスタイルはAvalonDock本体ではなくVS2013テーマ側である。

### 1.2 VS2013テーマの`AnchorablePaneTitleStyle`に`DragHandleTexture`を発見

`AvalonDock.Themes.VS2013/Themes/Generic.xaml`551-620行、`AnchorablePaneTitle`用スタイルのControlTemplate内：

```xml
<DockPanel>
    <Border Padding="2,0,4,0" HorizontalAlignment="Left" Background="{TemplateBinding Background}">
        <avalonDockControls:DropDownControlArea ...>
            <ContentPresenter x:Name="Header" .../>  <!-- タイトルテキスト -->
        </avalonDockControls:DropDownControlArea>
    </Border>
    <Rectangle
        x:Name="DragHandleTexture"
        Height="5" Margin="4,0,2,0" VerticalAlignment="Center"
        UseLayoutRounding="True" RenderOptions.BitmapScalingMode="NearestNeighbor">
        <Rectangle.Fill>
            <DrawingBrush TileMode="Tile" Viewbox="0,0,4,4" ViewboxUnits="Absolute"
                          Viewport="0,0,4,4" ViewportUnits="Absolute">
                <DrawingBrush.Drawing>
                    <GeometryDrawing Brush="{Binding Fill, ElementName=DragHandleGeometryPlaceholder, ...}">
                        <GeometryDrawing.Geometry>
                            <GeometryGroup>
                                <RectangleGeometry Rect="0,0,1,1" />
                                <RectangleGeometry Rect="2,2,1,1" />
                            </GeometryGroup>
                        </GeometryDrawing.Geometry>
                    </GeometryDrawing>
                </DrawingBrush.Drawing>
            </DrawingBrush>
        </Rectangle.Fill>
    </Rectangle>
</DockPanel>
```

`DrawingBrush`が`TileMode="Tile"`で4×4ピクセル単位のパターンを繰り返し敷き詰め、その中に1×1ピクセルの矩形を2つ市松状（`Rect="0,0,1,1"`と`Rect="2,2,1,1"`）に配置することで、細かい点描のドット模様を生成している。色は`DragHandleGeometryPlaceholder`という非表示のRectangle（561-571行、`Fill="{DynamicResource ToolWindowCaptionInactiveGrip}"`）から`Binding`経由で取得しており、`Model.IsActive=True`時のTrigger（705-708行）で`ToolWindowCaptionActiveGrip`に切り替わる（増分1層B調査で確認済みの色キー系統、テーマ連動）。

**フローティングウィンドウ側にも同型実装あり**：`LayoutAnchorableFloatingWindowControl`用のヘッダー（2110-2153行）にも同じ`DragHandleGeometryPlaceholder`/`DragHandleTexture`のペアが存在し、ドッキング時・フローティング時いずれも一貫してこのグリップが表示される設計になっている。

**複数タブ時の`TabItem`側には存在しない**：`TabItem`用スタイル（298行・448行）を確認したが`DragHandleTexture`という要素は無い。ecad2の各パネル（シート/機器表/出力/プロパティ/配置ツール等）は基本的に単一タブ構成のため`AnchorablePaneTitle`が使われており、殿ご確認の「全ドック共通」という観察と一致する。

## 2. 侍の先行試行(8650a66)が効かなかった理由

侍は「Grid列0（タイトル表示部）が`Width="*"`で余白ができ、そこに装飾が表示される」という仮説のもと、列0を`Auto`化する対処を試みた。しかし実際の模様の発生源は**Grid列0の内部にある`DockPanel`の`LastChildFill`機構**にある——`DockPanel`は明示的な`DockPanel.Dock`指定のない最後の子要素（`Rectangle x:Name="DragHandleTexture"`）に残りスペースを自動的に割り当てる。Grid列0を`Auto`にしても、`DockPanel`内部の配分ロジック自体（Fill対象がRectangleである点）は変わらないため、模様は解消しなかったと説明できる。原因要素そのもの（`Rectangle`）を直接標的にしていなかった、という点が前回の空振りの技術的な理由と考える。

## 3. 殿ご指摘「スクリーンショットでは判別困難」との整合性

`DragHandleTexture`は1×1ピクセルの点を4×4単位でまばらに市松配置する、非常に微細なパターンである。`UseLayoutRounding="True"`・`RenderOptions.BitmapScalingMode="NearestNeighbor"`という設定も、この微細なパターンをディスプレイの物理ピクセルへ正確に合わせるための配慮であり、逆に言えば画素採取や静止画キャプチャの圧縮・縮小処理では潰れやすい・見落としやすい構造と言える。殿の「人間の目でしか判別できない可能性が高い」というご指摘は、この一次ソースの実装内容と技術的に整合する（**推測**、キャプチャ処理自体の検証は行っていない）。

## 4. 対処方針の提案

`AnchorablePaneTitle`型（ドッキング時）および必要なら`LayoutAnchorableFloatingWindowControl`（フローティング時）向けに、VS2013テーマの`AnchorablePaneTitleStyle`を土台にした派生スタイルをアプリ側（App.xaml等）へ定義し、`Rectangle x:Name="DragHandleTexture"`をControlTemplate内で直接ターゲットにして次のいずれかで無効化する：

- `Visibility="Collapsed"`（要素自体を非表示にする、最も確実）
- `Fill="Transparent"`（模様の描画のみ消し、レイアウト上の占有スペースは残す）

侍が増分1層B・増分7で確立した「既定ControlTemplateをコピーし、標的要素のみ差し替える派生テンプレート」手法（`docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`と同型のアプローチ）がそのまま適用できる。今回は色のDynamicResource化ではなく特定`Rectangle`のVisibility制御のみのため、増分7より対象範囲は小さく済む見込み（**推測**、正確な規模は侍の実装時精査が必要）。

**推奨は`Visibility="Collapsed"`**——`Fill="Transparent"`だと`Rectangle`自体の`Margin="4,0,2,0"`によるレイアウト上の占有スペース（最低限のドラッグハンドル領域）が残り、タイトルとアイコン群の間に不自然な空白が残る可能性があるため（**推測**、実機確認要）。ただし、ドラッグ操作自体への影響（`Rectangle`が占めていた領域でのドラッグ開始判定等）は無いと考えられる——`DragHandleTexture`はあくまで視覚的なテクスチャ描画のみを担い、ドラッグ操作自体は`AnchorablePaneTitle`コントロール自体（`DropDownControlArea`を含むタイトル領域全体）のマウスイベント処理で実現される設計と見るのが自然（**推測**、AvalonDockのドラッグ実装コード自体までは踏み込んで確認していない）。

## 5. 不明点

- `Fill="Transparent"`と`Visibility="Collapsed"`のどちらがレイアウト上より自然な見た目になるかは実機確認が必要。
- ドラッグ操作の当たり判定に`DragHandleTexture`領域が関与していないか（Collapsed化で誤ってドラッグ不能領域が生まれないか）は一次ソースのみでは確定できず、実装後の実機確認を推奨。
- `LayoutAnchorableFloatingWindowControl`側（2110-2153行）も同型のため同様の対処を要するが、今回はドッキング状態（殿ご指摘の主対象）を中心に調査した。フローティング時の対応要否は着手時に確認が必要。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- [Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ：
  - `source/Components/AvalonDock/Themes/generic.xaml`（AvalonDock本体、`AnchorablePaneTitleStyle`297-378行、装飾要素なしを確認）
  - `source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml`（VS2013テーマ、2026-07-17取得、全2933行。`AnchorablePaneTitleStyle`551-710行付近＝`DragHandleTexture`/`DragHandleGeometryPlaceholder`の実装・Active時Trigger、2080-2153行付近＝`LayoutAnchorableFloatingWindowControl`側の同型実装）
- `docs/todo.md` T-100節（316-342行、経緯・侍先行試行8650a66の内容）
- `git show 8650a66`（侍の先行試行コミット、App.xaml 91行追加の内容確認）
- `docs/ecad2-t083-zoubun1-layerb-shinsou-chousa-onmitsu2.md`（`ToolWindowCaptionActiveGrip`等の色キー系統、既存調査との関連確認）
