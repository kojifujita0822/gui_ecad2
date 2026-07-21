# ドッキングパネルタイトル色の非対称性 調査（隠密）

日付: 2026-07-21
契機: 殿指摘（スクリーンショット添付）「シート」「出力」タブのみ青色（アクティブ色）、「機器表」「プロパティ」は灰色（非アクティブ色）。「仕様か？」というご質問。

## 結論（先出し）

**バグではなく、複数`DockingManager`構成を採用したことの構造的な帰結（技術的には「仕様」）。** ただし、ユーザーが「1つのウィンドウ内では通常1つのペインだけがアクティブに見える」と直感的に期待する点を踏まえると、「改善余地がある違和感」に該当しうる。断定は避け、対応要否は殿裁定に委ねるべき論点と考える。

## 1. 複数DockingManager（4つ）構成の経緯・設計意図

`docs/todo-archive.md` T-058節を精読した。**4つの独立した`DockingManager`（`PlacementToolBarDockingManager`・`LeftPaletteDockingManager`・`RightPanelDockingManager`・`OutputPanelDockingManager`）は、「アクティブペイン管理をどうするか」という意図を持って設計されたものではなく、増分ごとの段階的実装の自然な結果**である。増分1（左パレット）→増分2（出力パネル、案C=独立した2つ目のDockingManager）→増分3（右パネル）→ツールバー2段目、という順で個別に`DockingManager`が追加されていった経緯が記録されている。「単一DockingManagerに統合する案」が検討され却下された、という明示的な記録は見当たらなかった（`docs/todo-archive.md`全文検索で該当なし）。

## 2. AvalonDockの「アクティブペインタイトル色」の一般的な仕組み（一次ソース確認）

### タイトル色を決めるXAML側のロジック

`Dirkster99/AvalonDock`本体（GitHub、2026-07-21 curl取得・scratchpad保存）`source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml`2277-2281行（`AnchorablePaneTitle`、タブ1個時のペインタイトル用ControlTemplate）：

```xml
<DataTrigger Binding="{Binding RelativeSource={RelativeSource Self}, Path=Model.SinglePane.SelectedContent.IsActive}" Value="True">
    <Setter TargetName="Header" Property="Background" Value="{DynamicResource ...ToolWindowCaptionActiveBackground}" />
    <Setter TargetName="Header" Property="TextElement.Foreground" Value="{DynamicResource ...ToolWindowCaptionActiveText}" />
    ...
</DataTrigger>
```

タイトルの色は、**そのペイン自身が属する`LayoutAnchorablePane`の`SelectedContent.IsActive`**（真偽値）で決まる。この`IsActive`はどこで管理されているかが本調査の核心。

### `IsActive`/`ActiveContent`はDockingManagerインスタンス単位で管理される

`source/Components/AvalonDock/DockingManager.cs`1232-1233行：

```csharp
public static readonly DependencyProperty ActiveContentProperty = DependencyProperty.Register(nameof(ActiveContent), typeof(object), typeof(DockingManager), ...);
```

`ActiveContent`は`DockingManager`クラスに登録された`DependencyProperty`——**WPFの依存関係プロパティはインスタンスごとに個別の値を持つ**ため、`DockingManager`のインスタンスが4つあれば、それぞれが独立した`ActiveContent`を持つ。2456-2470行付近では、内部の`LayoutRoot.ActiveContent`（各`DockingManager`が個別に持つレイアウトツリーのルート）が変化した際に、その`DockingManager`自身の`ActiveContent`プロパティへ反映する実装になっている。

**結論：単一`DockingManager`内では通常1つのペインのみアクティブ表示になるのがAvalonDockの標準的な挙動だが、複数`DockingManager`構成では、各インスタンスが完全に独立して自分自身のアクティブ状態を持つのが一次ソースから見て自然な（かつ回避不能な）帰結である。**

## 3. スクリーンショットの状態は初期状態特有か、常時発生しうるか

一次ソースの構造から判断する限り、**「初期状態特有」ではなく、複数DockingManager構成が続く限り常時発生しうる構造的な状態**と考えられる。

- 各`DockingManager`は「最後にその内部でフォーカス/操作されたコンテンツ」を自分の`ActiveContent`として保持し続ける。
- ユーザーが実際に触れた（クリック等でフォーカスした）`DockingManager`のペインは青色のまま留まり、一度も触れていない、あるいは過去に触れたが別の場所へフォーカスが移った`DockingManager`は灰色のままになる。
- 今回のスクリーンショットは、シート（左パレット）・出力パネルには操作が及んだが、機器表・プロパティ（右パネル）にはまだ操作が及んでいない、という自然な状態を反映している可能性が高い。もし殿が機器表またはプロパティ欄を実際にクリックすれば、そのDockingManagerが青くなり、代わりに直前にアクティブだった別のDockingManagerは灰色のままになる（**同時に最大4つのペインが青く見える状態が構造的に起こりうる**）、という挙動が推測される（**実機未確認、推測に留める**）。

## 4. 技術的所見（判断材料の提示、断定は避ける）

- 単一`DockingManager`構成であれば、AvalonDock標準の「アプリ全体で1つだけアクティブ」という直感的な挙動（Visual Studio等の一般的なドッキングIDEに近い体験）になったと考えられる。
- 現状の4分割構成は、バグではなく設計上の帰結（技術的には「仕様」と言える）だが、ユーザーが期待する「1つのウィンドウでは通常1つだけアクティブ」という直感とは乖離しており、**「改善余地がある違和感」に該当しうる**。
- 対処法の選択肢（隠密からは提案のみ、採否・優先度は判断しない）：
  1. 現状維持（技術的制約として説明・許容）
  2. 4つの`DockingManager`を単一の`DockingManager`へ統合（AvalonDock標準の1アクティブ挙動を得られるが、既存のペイン配置構造・レイアウトリセット機構（Ctrl+Alt+R）等への影響範囲が大きく、再設計コストは高いと推測される）
  3. 各`DockingManager`の`ActiveContent`変更イベントを監視し、あるDockingManagerがアクティブになった際に他のDockingManagerの`ActiveContent`を明示的にクリアする同期処理を追加（部分的な対処、実装コストは選択肢2より低いと推測されるが、AvalonDock標準の挙動から外れるカスタマイズになる）

## 不明点

- 実際にスクリーンショットの状態が「常時発生する」のか「特定操作直後にのみ見られる過渡的な状態」なのかは、本調査（静的解析）の範囲では実証できない。忍者による実機確認（複数パネルを順にクリックして色の変化を観察）で裏付けが可能と考える。

## 出典

- `docs/todo-archive.md` T-058節（1546-1770行付近、複数DockingManager導入の経緯）
- AvalonDock本体一次ソース（GitHub `Dirkster99/AvalonDock`、2026-07-21 curl取得）
  - `source/Components/AvalonDock.Themes.VS2013/Themes/Generic.xaml`2277-2281行
  - `source/Components/AvalonDock/DockingManager.cs`1232-1233行・2456-2470行
- `src/Ecad2.App/MainWindow.xaml`（4つのDockingManager定義箇所）
