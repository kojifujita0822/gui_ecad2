# T-110 4分割DockingManagerの単一統合 プラン（隠密）

作成日: 2026-07-21　作成者: 隠密　委任元: 家老（殿裁定2026-07-21「将来的なリスクも考慮すると単一統合がいい」、`docs/todo.md` T-110節）
依頼内容(1)〜(6)の全てに対応する。本書はプランのみで実装には着手しない。

---

## 0. 要旨（先出し）

- **統合はキャンバス（LadderCanvas）をDockingManagerの内側へ取り込むことが幾何学的に必須**。現状キャンバスはどのDockingManagerにも属さず、4管理者は上・左・右・下の4矩形を別々に覆っている。単一DockingManagerは1つの矩形領域しか覆えないため、「キャンバスを中心に据えた全域1管理者」構成（Visual Studio型）への転換になる。
- 統合方式は**案1（キャンバス=LayoutDocumentPane）を推奨**。AvalonDockの寸法管理（`OnFixChildrenDockLengths`）はLayoutDocumentPaneの存在を前提に設計されており、現行の「Auto列Gridで包む」回避策（T-099要件(3)）が不要になる方向に働く（§2）。
- 影響範囲は広いが、大半は**単純化方向**（4要素ループ→単一、ファイル4つ→1つ）。真の難所は3つ：(a) `AnchorablePaneControlStyle`系スタイルのManager単位スコープ分離が使えなくなる、(b) T-099(c)案Yの「配置ツールバーだけ部分リセット」が構造的に不可能になる、(c) T-103独自ドロップ枠のイベント購読が全ペインのフロート化を拾うようになる（§3・§4）。
- **PoC先行を強く推奨**（§5）。特に (b) の代替方式と、統合トポロジでのフロート化/ドッキング往復の健全性は、T-099(c)で3周のモグラ叩きを経た領域そのものであり、本実装前に`poc/`での実証が必須と考える。
- 依頼内容(6)（単一ペインタイトルバー非表示）は**技術的に実現可能**。VS2013テーマ自身が「フロートウィンドウ直接ホスト時にタイトルバーをCollapse化する」パターンを内蔵しており（テーマ公認の非表示手法）、T-099(c)案Eの前例（ラベルのみ非表示）もある。ただし失われる機能の代替と、**「タイトルバーを消すとアクティブ色表示も消える」というT-110の発端との相互作用**があり、殿裁定事項が多い（§6）。

### 殿裁定を要する事項（一覧）

| # | 論点 | 選択肢 | 隠密の推奨 |
|---|---|---|---|
| 裁1 | 統合トポロジ | 案1=キャンバスをLayoutDocumentPaneに / 案2=キャンバスもLayoutAnchorableに | 案1 |
| 裁2 | キャンバス上部のドキュメントタブ表示 | 表示のまま / 非表示化（スタイル追加） | 非表示化（§2.4） |
| 裁3 | 既存保存レイアウト（%AppData%旧4ファイル）の扱い | 移行ロジック新設 / 既定へフォールバック（喪失許容） | フォールバック（§3.1） |
| 裁4 | シート/機器表/プロパティ/出力ペインのフロート化可否 | 現状維持（可） / CanFloat="False"で封じる | CanFloat="False"（§4.3） |
| 裁5 | タイトルバー非表示の方式 | 案A=完全非表示 / 案B=ラベルのみ非表示（案E踏襲） / 案C=現状維持 | §6.4参照（機能要否の裁定とセット） |
| 裁6 | タイトルバー非表示時のアクティブ表示喪失の許容 | 許容 / 別の視覚表現を検討 | §6.5参照 |

---

## 1. 現状の詳細構造（依頼内容(1)）

### 1.1 外側レイアウト（`MainWindow.xaml`、全1868行）

```
RootLayoutGrid (866行)  Row: Auto/Auto/*/Auto/Auto
├─ MainContentArea (890行, RowSpan=4, IsEnabled=IsMainContentEnabled)
│    Row: Auto / Auto / *(Min200) / Auto / 160(Min80)
│    ├─ Row0: MenuBarArea (908行)
│    ├─ Row1: Grid列Auto/* (974行)
│    │    ├─ [1] PlacementToolBarDockingManager (991行)
│    │    └─ PlacementToolBarDropZoneOverlay (1305行, T-103独自ドロップ枠)
│    ├─ Row2: MainWorkAreaGrid (1315行)  列: 220/Auto/*/Auto/280
│    │    ├─ [2] LeftPaletteDockingManager (1332行)
│    │    ├─ GridSplitter (1373行)
│    │    ├─ CanvasArea: ScrollViewer+LadderCanvas (1379行)  ※どのManagerにも属さない
│    │    ├─ FindBarオーバーレイ (1413行, ZIndex=100)
│    │    ├─ GridSplitter (1447行)
│    │    └─ [3] RightPanelDockingManager (1459行)
│    ├─ Row3: GridSplitter (1604行)
│    └─ Row4: [4] OutputPanelDockingManager (1615行)
└─ StatusBarArea (1691行)
```

### 1.2 各DockingManagerの管理ペイン

| Manager | ペイン（Title / ContentId） | 中身 | 特記 |
|---|---|---|---|
| PlacementToolBar (991行) | 基本機能/MainToolBar・配置ツール/PlacementToolBar | ToolBar直接XAML | 2タブ同居（T-104）、SelectedContentIndex=1、MainToolBarのみCanFloat="False"。唯一`AnchorablePaneControlStyle`ローカル指定（993行） |
| LeftPalette (1332行) | シート/LeftPalette | ListBox(SheetNavList) | 単一ペイン |
| RightPanel (1459行) | 機器表/DeviceTable・プロパティ/RightPanelBottom | DataGrid・プロパティ群+部品選択ListBox | 縦2分割、DockMinHeight=80、下段TitleはUpdateRightPanelBottomTitleでコード更新 |
| OutputPanel (1615行) | 出力/OutputPanel | DataGrid2枚(Visibility切替) | TitleはUpdateOutputPanelTitleでコード更新 |

全ペインCanClose="False"。ペイン中身は全て直接XAML（UserControl不使用）。

### 1.3 テーマ適用処理（`MainWindow.xaml.cs:771-791` `ApplyDockingManagerThemes`）

`AllDockingManagers`（124行、4要素配列）をループし、(a) `manager.Theme`へ`Vs2013LightTheme`/`Vs2013DarkTheme`を動的設定（XAMLにTheme属性なし）、(b) `manager.Resources[typeof(AnchorablePaneTitle)]`へタイトルスタイルを登録——PlacementToolBarのみ`PlacementToolBarAnchorablePaneTitleStyle`（618行、ドッキング時ラベル非表示=案E）、他3管理者は`AnchorablePaneTitleNoDragHandleStyle`（354行、ハッチング除去=T-100）と**Manager identityで出し分け**。呼出はコンストラクタ（189行）とダークモード切替（641行）。

### 1.4 レイアウト永続化・リセット（`MainWindow.xaml.cs`）

- 保存先: `%AppData%\Ecad2\docking-layout\` にManager単位の4ファイル（left-palette.xml等、`GetDockingLayoutFileName` 138-145行）。
- 起動時: `RegisterDockingContents`（409行、ContentIdレジストリ+期待集合構築）→`SerializeDefaultDockingLayouts`（443行、XAML初期状態を自己Serializeしハードコード既定としてメモリ保持）→`LoadDockingLayoutFromFileIfExists`（506行、保存ファイル→既定XMLの2段フォールバック）。
- 保存時: `SaveDockingLayoutAsDefault`（472行、終了時+Ctrl+Alt+S）。`HasExpectedContent`（433行）でContentId欠落時は保存スキップ（T-099(c)復旧作業由来の二重防御）。
- Ctrl+Alt+R: `ResetDockingLayoutToDefault`（538行、全Managerループ、保存済み優先2段）。クラスハンドラ登録（155-175行）はManager数非依存。
- T-099(c)案Y: `ContentDocking`をCancelし`ResetPlacementToolBarLayoutToDefault`（281-289行）で**当該Managerのみ**既定XMLへDeserialize（標準`Dock()`のタブ自己複製バグ回避）。

---

## 2. 統合方式の検討（依頼内容(2)）

### 2.1 幾何学的制約（統合の前提）

DockingManagerは1つの矩形領域を覆うコントロールである。現行4管理者は上・左・右・下の4矩形を占め、中央のキャンバスはWPFのGridで挟み込まれているだけで**どのManagerにも属さない**（1.1参照）。ゆえに「4管理者を1つへ」は、必然的に**キャンバスを内包する全域1管理者**への転換を意味する。キャンバスを外に残したままの単一統合は幾何学的に不可能。

### 2.2 案1（推奨）: キャンバス=LayoutDocumentPane

Visual Studio型の標準構成。中央にLayoutDocumentPane（文書領域）、周囲にLayoutAnchorablePane（ツールペイン）。

**推奨根拠（一次ソース）**: `LayoutPanelControl.OnFixChildrenDockLengths`（AvalonDock v4.74.1 `LayoutPanelControl.cs:31-110`、scratchpad取得済み）は、
- 水平LayoutPanelに**LayoutDocumentPane系の子孫が存在する場合**: 文書側をStar化し、非文書側の固定DockWidthを尊重する（37-58行）——ツールペイン固定幅+キャンバス可変という現行UIの寸法思想と一致。
- **存在しない場合**: 全子のDockWidthをStar強制上書き（60-71行）——これが現行T-099要件(3)の「Auto列Gridで包む」回避策（`MainWindow.xaml:963-993`コメント）を要した原因。統合+案1ではこの回避策自体が不要になる。

**初期レイアウトの骨子（概念図）**:

```xml
<avalonDock:DockingManager x:Name="MainDockingManager">
  <avalonDock:LayoutRoot>
    <avalonDock:LayoutPanel Orientation="Vertical">
      <avalonDock:LayoutAnchorablePane DockHeight="123">   <!-- 実測調整、§7 -->
        <LayoutAnchorable 基本機能/MainToolBar />
        <LayoutAnchorable 配置ツール/PlacementToolBar />
      </avalonDock:LayoutAnchorablePane>
      <avalonDock:LayoutPanel Orientation="Horizontal">
        <avalonDock:LayoutAnchorablePane DockWidth="220">
          <LayoutAnchorable シート/LeftPalette />
        </avalonDock:LayoutAnchorablePane>
        <avalonDock:LayoutDocumentPane>
          <LayoutDocument キャンバス（ScrollViewer+LadderCanvas+FindBar） CanClose="False" />
        </avalonDock:LayoutDocumentPane>
        <avalonDock:LayoutPanel Orientation="Vertical" DockWidth="280">
          <avalonDock:LayoutAnchorablePane DockMinHeight="80">
            <LayoutAnchorable 機器表/DeviceTable />
          </avalonDock:LayoutAnchorablePane>
          <avalonDock:LayoutAnchorablePane DockMinHeight="80">
            <LayoutAnchorable プロパティ/RightPanelBottom />
          </avalonDock:LayoutAnchorablePane>
        </avalonDock:LayoutPanel>
      </avalonDock:LayoutPanel>
      <avalonDock:LayoutAnchorablePane DockHeight="160">
        <LayoutAnchorable 出力/OutputPanel />
      </avalonDock:LayoutAnchorablePane>
    </avalonDock:LayoutPanel>
  </avalonDock:LayoutRoot>
</avalonDock:DockingManager>
```

メニューバー・ステータスバー・ElementPlacementBar等はDockingManagerの外（現行どおりRootLayoutGrid直下）に残る。

**付随する変化**:
- ペイン間リサイズは現行のWPF GridSplitter（4本）から**AvalonDock内蔵のLayoutGridResizerControl**へ置き換わる（GridSplitterは全廃）。操作感の差は忍者実機確認項目。
- キャンバスのDocumentはCanClose="False"必須。フロート化・移動も封じる（CanFloat="False"、LayoutDocumentのCanMove等は要PoC確認）。

### 2.3 案2（非推奨）: キャンバスもLayoutAnchorableに

文書領域を作らず全てツールペインで構成する案。`OnFixChildrenDockLengths`の「文書ペイン無し→全子Star強制」（60-71行）と正面衝突し、固定幅220/280の維持に現行同様の回避策をペインごとに再発明する必要が生じる。またキャンバスが「閉じる/フロート化できるツールウィンドウ」という誤ったセマンティクスを持つ。採用理由が見当たらない。

### 2.4 キャンバス上部のドキュメントタブ（裁2）

LayoutDocumentPaneは上部にドキュメントタブ（題名タブ）を表示する。単一文書のecad2では冗長の可能性が高く、非表示化するにはDocumentPaneControlStyleのテンプレートコピー+タブ領域Collapse（T-100/案Eと同型の既定コピー+標的差し替え手法）が必要。**推奨=非表示化**だが視覚確認を経たいためPoC観察項目とし、最終判断は殿裁定（UI/UX分岐）。

### 2.5 その他の再配置

- **FindBarオーバーレイ**（1413行、現行はキャンバス列にZIndex重ね）: Document内コンテンツ（ScrollViewerを包むGrid）へ内包させる。座標系はDocument内で完結するため単純化方向。
- **ElementPlacementBar座標変換**（`PositionPlacementBar`、RootLayoutGrid基準のTranslatePoint、875-889行コメントの既知の二重Gridズレ対策）: DockingManager統合で参照アンカーが変わるため再検証必須（§7）。

---

## 3. 影響範囲の洗い出し（依頼内容(3)）

### 3.1 レイアウト永続化（T-058増分4）

| 項目 | 現状 | 統合後 | 影響度 |
|---|---|---|---|
| 保存ファイル | Manager単位4ファイル | 単一ファイル（例: main-layout.xml） | 中（単純化） |
| `GetDockingLayoutFileName` | 4分岐switch | 1値（またはメソッド廃止） | 小 |
| `HasExpectedContent` | `Dictionary<DockingManager, HashSet<string>>` | 単一集合へ作り直し。**期待ContentId集合に新設のキャンバスDocument分を追加** | 中 |
| `AllDockingManagers`ループ群 | 8メソッドが横展開ループ | 単一インスタンス直接操作へ書き換え | 中（単純化） |
| 既存ユーザーの保存済みレイアウト | 旧4ファイル | **新ファイル名には存在しない→全員が既定フォールバック経路を通る**（保存カスタムレイアウト喪失）。T-104増分1でContentId構成変更時に同種事象が実際に発生済み（`docs/ecad2-t099-c-dock-layout-load-failure-investigation-onmitsu.md`） | 裁3 |

裁3の推奨=移行ロジックは作らない。旧4ファイルのXMLは新トポロジ（LayoutDocumentPane内包）と構造非互換であり、機械変換は歪んだレイアウトを生むリスクの方が大きい。既存の3層フォールバック（読込失敗→既定XML→XAML初期状態、クラッシュしない設計）がそのまま安全網になる。旧ファイルはアプリからは参照されなくなるだけで実害なし（削除はしない——rm禁止原則、放置で無害）。

### 3.2 Ctrl+Alt+R リセット機構（T-058増分1〜5）

- クラスハンドラ登録（155-175行）はManager数非依存で**変更不要**。
- `ResetDockingLayoutToDefault`は4要素ループ→単一化（単純化）。「保存済み優先→既定XML」の2段構成はそのまま維持可能。
- タイトル再同期（`UpdateOutputPanelTitle`/`UpdateRightPanelBottomTitle`の明示呼出、553-554行）は統合後もContentId検索ベースのため軽微な修正で維持可能。

### 3.3 T-099(c)案Y（配置ツールバーのドッキング復帰）——**最重要の再設計点**

現行`ResetPlacementToolBarLayoutToDefault`（281-289行）は「PlacementToolBarDockingManagerだけを既定XMLへDeserialize」する部分リセット。統合後は**Layout差し替え=ウィンドウ全域のリセット**になり、「ツールバーをドッキングし直しただけで他ペインの配置まで全て既定へ戻る」という許容し難い副作用を持つ。そのままの移植は不可。

代替候補（PoCで検証、§5）:
- **候補a: 標準`Dock()`の再評価**。案Yが回避した「タブ自己複製バグ」は`InternalDock`のフォールバック探索の欠陥（`docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`調査5）に起因する。統合トポロジではPreviousContainer解決の文脈が変わるため、**バグが再現しない可能性がある**（推測、PoC必須）。再現しなければ`e.Cancel`自体を撤去でき、最も素直。
- **候補b: 現状Serialize→部分修正→Deserialize**。ドッキング操作時に現在レイアウトをSerializeし、フロート部分だけを既定位置へ書き換えてDeserialize。「モデル手術禁止・Layout差し替えに任せる」原則（3周の教訓）は守られるが、XML操作の複雑さが増す。
- **候補c: 全域リセットを許容**（保存済みレイアウト優先で復元するなら実害は限定的）。UX上の妥協案として温存。

### 3.4 T-103（独自ドロップ枠）

- `PlacementToolBarDropZoneOverlay`（1305行）はManager隣接のGrid兄弟要素。統合でツールバーペインの位置がLayout内在化するため、**オーバーレイの表示位置決定ロジック（スクリーン矩形ヒットテスト、349-366行）の座標取得元を再配線**する必要がある。
- `LayoutFloatingWindowControlCreated`購読（224行）は現行「配置ツールバー専用Manager」への購読ゆえ無条件でツールバーのフロートと判断できた。統合後は**全ペインのフロート化で発火**するため、e.Content/ContentIdでのフィルタ追加が必須。（裁4でCanFloat="False"を採るなら、フィルタは単純化される——フロート化しうるのが配置ツールバーだけに戻るため。）

### 3.5 T-104（基本機能/配置ツールのタブ切替）

- 2タブ同居のLayoutAnchorablePane構造はそのまま統合レイアウトの上段ペインとして移設可能（構造自体はManager非依存）。SelectedContentIndex=1（案A裁定）も維持。
- `DockingManager.Resources`の`LayoutAnchorSideControl`暗黙スタイル（994-1006行、Tabナビ対策）は統合Manager全体へ適用範囲が広がるが、これは**望ましい方向**（AutoHideサイド領域のフォーカス除外は全ペインに有益）。`DisableFocusOnAutoHideSideItemsControl`（234-249行）も単一Manager化で1回適用に単純化。
- ただし統合ManagerのAutoHideサイド領域はウィンドウ全域を囲むため、**Tabキー巡回の再検証は必須**（T-104増分1 DoD(4)の回帰確認）。

### 3.6 T-100・テーマ適用（`ApplyDockingManagerThemes`）

- Theme設定は1回になる（単純化）。
- `manager.Resources[typeof(AnchorablePaneTitle)]`による**Manager単位のスタイル出し分けが不可能になる**——§4.2で詳述。

### 3.7 T-106（タブ文字色ダークモード対応）

`PlacementToolBarPaneControlStyle`内TabItemの`PanelHeaderBackgroundBrush`/`PanelHeaderForegroundBrush`参照（252-253行）はスタイル資産の一部として§4.2の統合スタイル再設計に内包される。App.xaml側のComboBox/ScrollBar対応はDockingManager数と無関係で影響なし。

### 3.8 その他

- **GridSplitter 4本**（1373/1447/1604行+T-059由来の行構成）: 全廃、AvalonDock内蔵リサイザーへ。`MainContentArea`の5行Grid構成も大幅に単純化。
- **`Window_Closing`の保存**（1207-1216行）: 単一化のみ。
- **`IsMainContentEnabled`によるIsEnabled継承**（890行、T-100新規発見6のPR-20 4例目の文脈）: 統合Managerも同様にMainContentArea配下に置けば挙動維持（要回帰確認）。
- **tests**: `T058Increment4LayoutFileNameTests`等、ファイル名導出・レイアウト関連の既存テストは書き換えが必要。

---

## 4. 高リスク領域（`AnchorablePaneControlStyle`系）への影響評価（依頼内容(4)）

### 4.1 リスクの本質

T-099(c)で3周のモグラ叩きを経た領域の教訓は「AvalonDockの隠れた不変条件（CollectGarbage・RootPanel setterのnull補完・OnLayoutChanged）と衝突するモデル手術は必ず失敗する。正規機構（Layout差し替え）に任せる」だった（`docs-notes/`台帳・memory参照）。統合はこの領域の**前提そのもの（Manager単位のスコープ分離）を崩す**ため、同じ轍を踏まない設計が要る。

### 4.2 スタイルのスコープ分離が使えなくなる問題

現状の分離は2機構とも**Manager identity依存**:
1. `AnchorablePaneControlStyle`プロパティ: PlacementToolBar管理者のみローカル指定（993行）。**DockingManager単位のプロパティであり、統合後は全ペインに同一スタイルが適用される**。
2. `manager.Resources[typeof(AnchorablePaneTitle)]`: 2種のタイトルスタイルをManagerで出し分け（789行）。統合後は1種しか登録できない。

**再設計方針（推奨）**: それぞれ「1つの統合スタイル+Model情報によるDataTrigger分岐」へ集約する。
- タイトルスタイル: `AnchorablePaneTitleNoDragHandleStyle`をベースに、案Eのラベル非表示トリガーを`Model.ContentId`条件（DataTrigger、対象=PlacementToolBar）付きで統合。AnchorablePaneTitleは`Model`（LayoutAnchorable）プロパティを持つため、`Binding Model.ContentId`での分岐は標準機構で成立する見込み（推測、PoC確認）。
- PaneControlスタイル: `PlacementToolBarPaneControlStyle`（TabStripPlacement="Bottom"+T-106色+T-099トリガー群）を全ペイン共通スタイルへ昇格できるかが論点。単一子ペインではTabItem自体がCollapse（VS2013テーマ標準のItems.Count==1トリガー、`vs2013-generic.xaml:537-539`）のため実害が出にくい見込みだが、機器表/プロパティ等が**将来タブ合流した場合**の見え方が変わる。全ペイン昇格で問題が出る場合の逃げ道（`SelectedContent.ContentId`によるトリガー分岐）も併設検討する。
- ※依頼内容(6)で案A（タイトルバー完全非表示）が採用された場合、タイトルスタイルの統合は大幅に単純化する（出し分け対象が減るため）。実装順序への示唆: **(6)の裁定を先に得てからスタイル統合を設計するのが効率的**。

### 4.3 フロート化・ドッキング往復の再評価

- 案Yのガードは「PlacementToolBarのみ保護」であり他ContentIdの安全を意味しない（T-104設計書§3で既指摘）。統合後は全ペインが同一Manager上でフロート化可能になり、`InternalDock`フォールバック探索の構造的弱点に**シート/機器表/プロパティ/出力も晒される**。
- **裁4の推奨=全アンカラブルペインにCanFloat="False"**。フロート運用の実需が確認されていない現状では、リスク面を構造的に閉じるのが得策（配置ツールバーのみ既存裁定どおりフロート可を維持）。キーボードファースト方針とも整合。
- なお`PlacementToolBarAnchorablePaneTitleStyle`のラベル非表示トリガー（`Model.Parent.IsDirectlyHostedInFloatingWindow`、856-858行）はManager非依存のバインドパスであり統合後も動作する見込み（推測、PoC確認）。

### 4.4 高リスク領域まわりの検証必須項目（PoC/忍者確認へ引き継ぐ）

1. 統合トポロジでの配置ツールバーFloat→Dock往復（タブ自己複製・縦長化・空白化の再現有無）
2. AutoHide（ピン留め）操作の挙動（統合Managerのサイド領域はウィンドウ全域）
3. タイトルスタイルのContentId分岐が全テーマ（Light/Dark）で正しく効くこと
4. `PlacementToolBarPaneControlStyle`全ペイン適用時の副作用（特にタブ2枚の上段ペイン以外での見え方）
5. アクティブペイン色の一元化（T-110の発端）が実際に達成されること

---

## 5. PoC先行の増分計画（依頼内容(5)）

規模が大きく、`AnchorablePaneControlStyle`系に深く関わるため、「プラン→PoC→増分実装+各増分で忍者検証」（memory: 高リスク領域は検証優先）を厳守する。**検証パイプライン（侍実装→隠密静的レビュー→忍者実機）を全増分で必ず挟む**。

### 増分0: PoC（`poc/`配下、本実装非接触）

単一DockingManager+5領域（ダミー中身）の最小WPFアプリを`poc/`に作成し、以下を実証:
- (a) §2.2トポロジの初期表示（固定幅220/280・上段/下段の高さがXAML指定どおり出るか。特に上段ツールバーペインの高さ——現行はGrid Autoによる内容フィットだが、DockHeightは固定値指定になるため見え方の差を確認）
- (b) AvalonDock内蔵リサイザーの操作感
- (c) 配置ツールバー相当ペインのFloat→Dock往復（**候補a: 標準Dock()でタブ自己複製バグが再現するか**——再現しなければ案Y代替の本命）
- (d) アクティブペイン色の一元化の実証（複数ペインを順にクリックし青帯が常に1つであること）
- (e) タイトルスタイルのContentId分岐（§4.2）の成立確認
- (f) ドキュメントタブの見え方（裁2の判断材料スクリーンショット）
- (g) LayoutDocumentPane内ダミーコンテンツへのキーボードフォーカス移動・Tab巡回

DoD: (a)〜(g)の結果を隠密がまとめ、家老経由で殿へ増分1以降の最終形（裁1〜裁4の確定）を提示できる状態。**PoCで(c)が否なら候補b/cの追加検証を挟む**（モグラ叩き検知: 2周以内に方式確定しなければ設計再考）。

### 増分1: 骨格統合（本実装）

- MainWindow.xamlのトポロジ統合（4Manager撤去→単一Manager+LayoutDocumentPane、GridSplitter撤去、FindBar内包化）
- 永続化の単一ファイル化（`GetDockingLayoutFileName`・`HasExpectedContent`・各ループの単純化）
- `ApplyDockingManagerThemes`の単一化+統合タイトルスタイル
- T-099(c)案Y代替（PoC確定方式）・T-103再配線・T-104タブナビ再確認
- 既存テストの追随修正
- 忍者確認: 起動・全ペイン表示・リサイズ・保存/復元・Ctrl+Alt+R・ダークモード切替・Float/Dock往復・Tab巡回・ElementPlacementBar位置

※骨格統合は性質上ビッグバン的（トポロジは半分だけ移行できない）。増分1が最大の塊になるのは避けられないが、PoCで方式リスクを先に潰しておくことで「実装の不確実性」は最小化する構え。

### 増分2: 回帰総点検

- T-100（ハッチング）・T-101（選択中ツールハイライト）・T-103（ドロップ枠）・T-104（タブ切替+TimerPause等コマンド到達性）・T-106（ダーク色）・T-107/T-108（ダークモード全域）の回帰を忍者が一括実機確認
- 画素採取による色検証（目視断定禁止の教訓適用）

### 増分3: 単一ペインタイトルバー非表示（依頼内容(6)、裁5・裁6の殿裁定後）

- §6の裁定結果に基づき実装。統合完了後に着手する独立増分とする（統合自体の検証と混ぜない——切り分け可能性の維持）。

---

## 6. 単一ペインタイトルバー非表示の検討（依頼内容(6)）

### 6.1 タイトルバーが担う機能の棚卸し（一次ソース確認済み）

`AnchorablePaneTitle`（v4.74.1 `AnchorablePaneTitle.cs`全読）とVS2013テーマテンプレート（`vs2013-generic.xaml:562-`）より:

| # | 機能 | 実装箇所 | 代替可能性 |
|---|---|---|---|
| 1 | ペイン名表示 | Header(ContentPresenter) | ペイン中身で自明なら不要とも言える（裁定事項） |
| 2 | ドラッグでフロート化/再ドック | AnchorablePaneTitle自身のマウス処理（OnMouseLeftButtonDown/OnMouseLeave、83-135行） | 裁4でCanFloat="False"なら機能自体が不要に |
| 3 | クリックでアクティブ化 | OnMouseLeftButtonUp（142行、Model.IsActive=true） | ペイン中身クリックでのフォーカス移動で代替される（実機確認要） |
| 4 | 標準メニュー（Float/Dock/AutoHide/Hide） | MenuDropDownButton | メニューバー項目やショートカットからLayoutItemのICommand（AutoHideCommand等）を呼べば代替可 |
| 5 | AutoHideピン留め | PART_AutoHidePin（LayoutItem.AutoHideCommand） | 同上。**そもそもAutoHide機能を残すか自体が裁定事項** |
| 6 | 閉じる | PART_HidePin | 全ペインCanClose="False"のため現状でも非表示（実質失うものなし） |
| 7 | 右クリックメニュー | DropDownControlArea | #4と同一メニュー |

### 6.2 実現手法（技術的裏付け）

- **VS2013テーマ自身に公認パターンがある**: `LayoutAnchorableControl`テンプレートは「フロートウィンドウ直接ホスト時」に`Header`（AnchorablePaneTitleを包むBorder）を`Visibility=Collapsed`にするトリガーを内蔵する（`vs2013-generic.xaml:1749-1761`）。つまり**LayoutAnchorableControlテンプレート層でのHeader Collapse化はテーマ設計上想定内の操作**。
- 実装は`LayoutAnchorableControl`スタイル（1712-1790行、約80行）のコピー+「ドッキング時かつ対象ContentId」条件のDataTriggerでHeaderをCollapse——T-100/案Eで確立済みの「既定コピー+標的差し替え」手法の3例目に相当。
- **T-099の罠は回避できる**: 過去の事故は`AnchorablePaneTitle`コントロール自体のCollapse化でマウス処理ごと殺した件（memory: wpf_collapse_visibility_hides_interaction）。今回は(a)一段上の階層（Header Border）で消す=テーマ公認パターンと同型、(b)裁4でCanFloat="False"ならドラッグ機能自体が不要、の2点で構図が異なる。

### 6.3 発端との相互作用（重要、裁6）

T-110の発端は「アクティブ色（青帯）の非対称性」であり、青帯は**タイトルバー上に表示される**。単一統合で「常に1ペインのみ青」を達成しても、タイトルバーを消せば**青帯表示そのものが消える**。すなわち:
- 統合の成果（1アクティブの正しい挙動）は、タイトルバー非表示時には**視覚上ほぼ見えなくなる**（キーボードフォーカスの所在はキャレット・選択表示等の中身側表現に委ねられる）。
- これを許容するか（すっきり優先・キーボードファースト的にはフォーカス所在は中身で分かれば足りる、という立場もありうる）、アクティブ表示の代替（ペイン境界線色等）を設けるかは殿裁定事項。
- なお統合自体の価値（標準挙動への回帰・将来リスク低減・レイアウト機構の一本化）はタイトルバー表示の有無と独立に成立する。

### 6.4 方式の選択肢（裁5）

| 案 | 内容 | 得られるもの | 失うもの | リスク |
|---|---|---|---|---|
| 案A | 完全非表示（ドッキング時、対象4ペイン） | 最大の省スペース・視覚シンプル化（UI/UX方針と整合） | 機能#1〜#5全て（#4・#5は代替UI設置で補完可） | 低〜中（テーマ公認パターン。ただし代替UI設計が必要） |
| 案B | 案E踏襲=ラベルのみ非表示（ボタン類は残す） | 実績ある低リスク手法の横展開 | 機能#1のみ | 低（前例あり。ただし高さはほぼ縮まない=省スペース効果薄、案E設計書§5の実測知見） |
| 案C | 現状維持（表示のまま） | アクティブ色の一元表示が活きる | 省スペース化なし | なし |

**隠密所見**: 省スペース・視覚シンプル化が目的なら案A一択（案Bは高さが縮まらないことが案E設計書で実証済み）。案Aを採る場合の付帯裁定は「AutoHide機能を残すか」（残すなら表示メニューへ「パネルを自動的に隠す」等の項目新設、捨てるなら`CanAutoHide="False"`で封止）。機能#2は裁4（CanFloat="False"）とセットで自然消滅する。

### 6.5 実装位置づけ

増分3（統合完了後の独立増分）を推奨（§5）。ただし§4.2のとおり**タイトルスタイル統合の設計は(6)の裁定に依存する**ため、裁5・裁6は増分1着手前に得ておくのが手戻り最小。

---

## 7. 不明点・実測が必要な事項（静的調査の限界の明示）

1. 上段ツールバーペインのDockHeight固定値化で、現行のAuto高さ（内容フィット）と同じ見え方にできるか——**実測必要**（PoC (a)）。
2. 統合トポロジで標準`Dock()`のタブ自己複製バグが再現するか——**実測必要**（PoC (c)、案Y代替の分岐点）。
3. `Model.ContentId`ベースのDataTrigger分岐がAnchorablePaneTitle/LayoutAnchorableControlの両層で期待どおり効くか——設計上は成立見込みだが**実機確認要**（PoC (e)）。
4. ペイン中身クリックでのアクティブ化（機能#3の代替）が全ペインで成立するか——**実測必要**。
5. ElementPlacementBar座標変換の統合後の挙動——**実測必要**（増分1忍者確認）。
6. LayoutDocumentPaneにドキュメントが1つだけの場合のタブ表示・フロート可否の細部——**実測必要**（PoC (f)）。

---

## 出典

- `src/Ecad2.App/MainWindow.xaml`（866-1868行の各定義、行番号は本文中に記載）
- `src/Ecad2.App/MainWindow.xaml.cs`（124/155-175/189/203-289/314-378/386-407/409-593/641/771-791/1207-1216行）
- `src/Ecad2.App/App.xaml`・`Themes/Theme.Light.xaml`・`Theme.Dark.xaml`（スタイル資産棚卸し）
- `%AppData%\Ecad2\docking-layout\`（保存済みレイアウト4ファイル実在確認）
- AvalonDock v4.74.1 一次ソース（GitHub Dirkster99/AvalonDock タグv4.74.1、2026-07-21 curl取得・scratchpad保存）:
  `LayoutPanelControl.cs:31-110`・`AnchorablePaneTitle.cs`全文・`vs2013-generic.xaml:404-560/562-660/537-539/1712-1790`
- 既往設計書・調査書: `docs/ecad2-panel-title-color-investigation-onmitsu.md`・`docs/ecad2-t104-toolbar-tabswitch-design-onmitsu.md`・`docs/ecad2-t099-c-paneltitle-label-only-hide-design-onmitsu.md`（案E）・`docs/ecad2-t099-c-dock-restore-by-default-xml-design-onmitsu.md`（案Y）・`docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`・`docs/ecad2-t099-c-dock-layout-load-failure-investigation-onmitsu.md`
- `docs/todo.md` T-110節・`docs/todo-archive.md` T-058節
