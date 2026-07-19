# メインツールバー(1段目)のタブストリップ統合可能性調査（隠密）

調査日: 2026-07-20　調査者: 隠密　委任元: 家老（殿発案の実装可能性調査、着手前の技術見立て段階）
手法: XAML構造調査（Explore委任＋自己確認）＋一次ソース確認（AvalonDock VS2013テーマ）。実機検証なし。

---

## 結論（先出し）

**前提となる「配置ツールバーの下に隣接するAvalonDockペイン」は現状の構造には存在しない**
（配置ツールバー直下はキャンバス領域、AvalonDock管轄外）。おそらく殿の意図は「配置ツールバー
**自体**の下段タブストリップ」を指すと解釈するのが構造上唯一辻褄が合う——**この解釈で正しいか
の確認を先に家老経由で殿へ仰ぐことを推奨する**。

その解釈の下では技術的に**不可能ではない**が、(1)タブストリップは`IsItemsHost`専用パネルで
直接ボタン追加は不可、ControlTemplate改修が必要 (2)現状Height=5pxの帯にメインツールバー
10ボタンを収めるには帯自体の大幅拡大が要り、「高さがまるごと浮く」という期待とは食い違う
（実質的な削減効果は限定的、相殺されうる） (3)T-099(c)で3周のモグラ叩きを経験した最重要警戒
領域（`AnchorablePaneControlStyle`系）への再突入となる——という3点から、**着手には慎重な
検討を要する**、というのが技術的な見立て。

---

## 調査観点(1): メインツールバー(1段目)の実装

- **行範囲**: `MainWindow.xaml` 934〜1031行。
- **要素**: 標準WPF `ToolBarTray`(`x:Name="ToolBarArea"`, `IsLocked="True"`) → 単一の`ToolBar`
  （AvalonDock要素ではない、固定配置）。
- **内容（10ボタン）**: 新規作成・開く・上書き保存・元に戻す・やり直し・PDF出力・行を追加・
  行を削除・テストモード(ToggleButton)・TimerPause(ToggleButton)。
- コメント（923-926・933行）に明記：「T-058増分5(殿裁定=2段目のみAvalonDock管轄内で可動化、
  1段目は現行ToolBarTray固定維持)」——**1段目は当初からAvalonDock非対応と決めた経緯がある**。

## 調査観点(2): 「配置ツールバーの下に隣接するAvalonDockペイン」の実態

`MainContentArea`の行構成（Explore調査結果より）:

| Row | 内容 |
|---|---|
| 0 | `MenuBarArea`（メニューバー） |
| 1 | ツールバー2段ラッパー：内0=`ToolBarArea`(1段目)／内1=`PlacementToolBarDockingManager`(2段目) |
| 2 | `MainWorkAreaGrid`（左パレット／キャンバス／右パネル、5列） |
| 3 | `GridSplitter` |
| 4 | `OutputPanelDockingManager` |

配置ツールバー（Row1）のGrid上の直下はRow2＝`MainWorkAreaGrid`。この5列構成のうち中央列
（キャンバス、`CanvasArea`）は**素のWPF Grid列**（`ScrollViewer`+`LadderCanvas`）であり、
`LayoutAnchorablePane`/`LayoutDocumentPane`のいずれでもない。左列・右列にはそれぞれ独立の
`LeftPaletteDockingManager`／`RightPanelDockingManager`があるが、これらは「配置ツールバーの
真下」ではなく横に並ぶキャンバスのさらに左右にある。

**結論: 「配置ツールバーの下に隣接するタブストリップ持ちAvalonDockペイン」に該当するものは
存在しない**。DockingManagerは全部で4個（配置ツール／左パレット／右パネル／出力）、いずれも
独立インスタンスで配置ツールバーの直下にあるものはない。

## 調査観点(3): タブストリップへのボタン群統合の技術的可能性

配置ツールバー自体（`PlacementToolBarDockingManager`）は、`AnchorablePaneControlStyle`として
ecad2独自の`PlacementToolBarPaneControlStyle`（`MainWindow.xaml:171-295`、T-099(c)で確立済みの
「既定コピー+標的差し替え」カスタムテンプレート）を適用済み。構造は2行Grid：

- Row0（`*`）＝`ContentPanel`（`PART_SelectedContentHost`、ツール本体=a接点/b接点等のボタン群）
- Row1（`Auto`）＝`HeaderPanel`（`avalonDock:AnchorablePaneTabPanel`、**`IsItemsHost="true"`**）

**`IsItemsHost="true"`のパネルはTabControl（この場合`LayoutAnchorablePaneControl`）の
アイテムジェネレータ専用領域であり、XAML側で直接任意の子要素（ボタン等）を追加することは
WPFの仕組み上できない**（一次ソース確認: AvalonDock既定`AvalonDockThemeVs2013AnchorablePane
ControlStyle`も同型構造、`HeaderPanel`は常に単独でGrid行を占有）。

技術的に実現するには、**`PlacementToolBarPaneControlStyle`のControlTemplate（177-200行）を
さらに改修し、Row1をHeaderPanel単独でなく「HeaderPanel(Auto幅)＋追加ツールバー領域(Star幅)」の
横並びGridへ組み替える**必要がある。ecad2は既にこの領域を一度カスタムコピー済みのため、
ゼロから既定コピーする必要はなく、既存改修で対応できる（T-099(c)/T-100/T-089で確立済みの
手法の延長、実装規模は中程度と見立てる）。

## 調査観点(4): 高さ削減の見積もりと懸念点

### 高さの実態

- 配置ツールバー全体は`MinHeight="103"`（`MainWindow.xaml:1064`、実測ベースの下限保証値）。
  大半は上段`ContentPanel`（選択/a接点/OR a接点/b接点/…の配置系ボタン群、GX Works3様式）が
  占め、下段`HeaderPanel`（タブ帯）は**現状Height=5px**（`DockedDragHandle`、T-099(c)で
  「省スペース化」の到達点として確立した値）。
- メインツールバーの10ボタン（うちToggleButton2個）は通常のWPF ToolBarボタン（アイコン+
  パディングで20〜30px角相当）であり、**現状5pxの帯には到底収まらない**。

### 「高さがまるごと浮く」への疑義

統合を実現するには、帯（Row1）の高さをメインツールバーボタンが収まるサイズまで拡大する
必要がある。これは**T-099要件3で達成した「配置ツールバー2段目の省スペース化」の一部を
打ち消す**ことを意味する。結果として：

- 削減効果＝「メインツールバー(1段目)の高さ」－「拡大後の帯（Row1）の高さ増分」
- 単純に「メインツールバー分がまるごと浮く」わけではなく、**実質的な削減幅は限定的、
  場合によってはほぼ相殺される**可能性が高い（帯を10ボタン+タブ1個分の幅・高さまで拡大すると、
  結局「小さいツールバーが1段減って、別の場所に少し大きいツールバーが1つ増える」に近い結果に
  なりうる）。

### 懸念点・リスク

1. **モグラ叩き高リスク領域への再突入**：`AnchorablePaneControlStyle`/`AnchorablePaneTitle`
   系はT-099(c)で3周のモグラ叩きを経験した最重要警戒領域（`karo.md`「モグラ叩き検知」制度の
   直接の契機）。この領域へのさらなる構造改修は、過去の教訓に照らし高い慎重さを要する
   （実装前にPoC→検証優先のプロセスを踏むべき、[[feedback_verification_over_speed_risky_areas]]）。
2. **キーボードナビゲーションの再設計**：既存の`HeaderPanel`(TabIndex=1)/`ContentPanel`
   (TabIndex=2)構造に、新規ボタン群のTabIndexをどう位置づけるか要検討。ecad2はキーボード
   ファースト方針のため軽視できない。
3. **UI/UX上の妥当性**（技術問題とは別軸、殿確認が必須の分岐）：タブストリップは本来「パネル
   状態表示・ドラッグハンドル」の役割であり、そこへ無関係な10個のコマンドボタン（新規/開く/
   保存等）を同居させる意匠が、GX Works3等の参考UIパターンと整合するかは別途検討を要する。
4. **コマンドバインディング自体への影響は小さいと見立て**：`Command`/`CommandBinding`は
   `Window`スコープで機能するため、ボタンの物理配置場所を変えても、Undo/Redo等の動作自体には
   影響しないと考えられる（WPFの一般的仕組み）。ただしAccessKey（Altアクセスキー）の配置・
   視認性の再検証は要る。
5. **他パネルへの影響は無い設計**：`PlacementToolBarPaneControlStyle`は`x:Key`付きで
   `PlacementToolBarDockingManager`にのみ明示適用されており、左パレット/右パネル/出力パネルの
   3つには影響しない（隔離は取れている、確認済み）。

## 家老への申し送り

- **前提の食い違い（要・殿確認）**：「配置ツールバーの下に隣接するAvalonDockペイン」は現状
  存在しない。「配置ツールバー自体の下段タブストリップ」への統合という解釈で進めてよいか、
  殿へ確認いただきたい。
- 技術的には可能だが、期待される「高さがまるごと浮く」効果は限定的である可能性が高い、という
  見積りのズレも併せて殿へお伝えいただくのがよいと考える。
- 着手判断となった場合は、T-099(c)の教訓（モグラ叩き検知・PoC先行・検証優先）を踏まえた
  進め方を強く推奨する。
