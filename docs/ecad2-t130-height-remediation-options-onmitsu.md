# T-130「高さ」対処可否調査（隠密）

> 2026-07-27 隠密調査。家老采配「AutoHideフライアウトがツールバーを覆う件、対処の選択肢を洗い出す」
> を受けての一次ソース調査。**調査と案の提示のみ、実装はしていない。**

---

## 総括

**機序を完全に特定した。** `PART_AutoHideArea`は`DockingManager`のControlTemplateで
`LayoutRootPanel`と**全く同じGridセル（中央セル）に重ねて配置**される設計であり、
`AnchorSide.Left`（シートパネルの辺）の場合`VerticalAlignment=Stretch`が`LayoutAutoHideWindowControl`
の**private実装内にコード直書き**されている。これはAvalonDock標準の設計そのもので、**XAML
スタイル上書きでは変更できない**（家老の当初見立てどおり）。

引き金は**T-110増分1でツールバー自体が単一DockingManagerの内部（`LayoutAnchorable`）に
統合されたこと**——統合前はツールバー用DockingManagerがシートパネル用DockingManagerと
完全に別領域にあったため、AutoHideフライアウトの「中央セル全体」がツールバー領域まで
含んでいなかった。

対処案は4つに整理できる。**いずれもAvalonDock本体の改造は非現実的**（P-103のIssue #337同様、
フォーク・独自ビルドはコスト過大）。**現実的な選択肢は「発生源の遮断」（案3）か「巻き戻し」
（案2）の二択**、あるいは現状維持（案4）。

---

## 1. 機序の特定（一次ソース）

### 1.1 `PART_AutoHideArea`はDockingManagerの中央セル全体を占める

`DockingManager`のControlTemplate（`AvalonDock/Themes/generic.xaml:692-742`）：

```
Grid（Row: Auto/*/Auto、Column: Auto/*/Auto）
├─ ContentPresenter Grid.Row=1 Grid.Column=1  Content={TemplateBinding LayoutRootPanel}   ← 通常のドッキングツリー
├─ ContentPresenter Grid.Row=0 RowSpan=3 Grid.Column=2  Content={TemplateBinding RightSidePanel}
├─ ContentPresenter Grid.Row=0 RowSpan=3 Grid.Column=0  Content={TemplateBinding LeftSidePanel}
├─ ContentPresenter Grid.Row=0 Grid.Column=0 ColumnSpan=3  Content={TemplateBinding TopSidePanel}
├─ ContentPresenter Grid.Row=2 Grid.Column=0 ColumnSpan=3  Content={TemplateBinding BottomSidePanel}
└─ ContentPresenter x:Name="PART_AutoHideArea" Grid.Row=1 Grid.Column=1
     HorizontalAlignment={TemplateBinding HorizontalAlignment}
     VerticalAlignment={TemplateBinding VerticalAlignment}
     Content={TemplateBinding AutoHideWindow}                                              ← AutoHideフライアウト
```

**`LayoutRootPanel`と`PART_AutoHideArea`は同一セル（Row=1, Column=1）に重ねて配置される。**
`LeftSidePanel`/`RightSidePanel`等（AutoHideタブの帯自体）は別セルだが、**フライアウト本体は
中央セル全体**を占める領域として設計されている。これはAvalonDock標準のControlTemplateであり、
`ControlTemplate`自体を丸ごと差し替えない限り変更できない（XAML Setterレベルでの部分上書き不可）。

### 1.2 `LayoutAutoHideWindowControl`の高さがStretchになる理由

`CreateInternalGrid()`（`AvalonDock/Controls/LayoutAutoHideWindowControl.cs:277-339`）：

```csharp
case AnchorSide.Left:
    _internalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = ... AutoHideWidth ... });  // 幅は明示制限
    ...
    VerticalAlignment = VerticalAlignment.Stretch;   // ← 高さは常にStretch（コード直書き）
    break;
```

Left/Right側は`ColumnDefinition`で幅だけ明示制限し、`VerticalAlignment=Stretch`で高さを
制限しない（Top/Bottom側はこの逆＝高さを制限し幅はStretch）。**この非対称はAvalonDock標準の
設計そのもの**であり、T-110固有の欠陥ではない（家老の当初見立てを一次ソースで確認・追認）。

`VerticalAlignment`はコンストラクタ的メソッド内でコードから直接代入されており、**`AnchorableStyle`
（唯一の公開スタイルフック、`LayoutAutoHideWindowControl.cs:69-80`）はこの`LayoutAutoHideWindowControl`
自身ではなく内部の`LayoutAnchorableControl`（`_internalHost`）にのみ適用される**——外部からの
XAML上書きでこの`VerticalAlignment`を変える経路は無い。

### 1.3 T-110増分1が「引き金」である理由

`src/Ecad2.App/MainWindow.xaml:1155`——配置ツールバー自体が単一`DockingManager`内の
`LayoutAnchorable`（`ContentId="PlacementToolBar"`）として統合されている。統合前（4分割
DockingManager時代）はツールバー専用の`PlacementToolBarDockingManager`が独立し、シートパネル
用`LeftPaletteDockingManager`とは別のGrid領域にあった。**統合後は両者が同一DockingManagerの
`LayoutRootPanel`内に同居するため、シートパネルのAutoHideフライアウト（中央セル全体を占める）
が必然的にツールバー領域とも重なる。**

統合の主目的は**「複数DockingManager間でのアクティブ色（`ActiveContent`）の一貫性」**
（`docs/todo.md`1240-1243行、殿裁定「将来的なリスクも考慮すると単一統合がいい」）——**高さ問題
とは無関係な目的で行われた設計変更**が、副作用として本症状を引き起こした形。

---

## 2. 対処の選択肢

### 案1：AvalonDock本体を改造し`LayoutAutoHideWindowControl`の高さを制限する

- **内容**：`CreateInternalGrid()`のLeft/Right分岐で`VerticalAlignment=Stretch`をやめ、
  `managerSize`からツールバー高さ分を差し引いた値で制限する等
- **規模**：**大**。AvalonDockをフォークし独自ビルド・保守する必要がある（NuGetパッケージの
  差し替え）。P-103（Issue #337）と同種の「コスト過大」判断が既に一度下っている前例あり
- **影響範囲**：AvalonDock全体（バージョンアップのたびに再パッチが必要）
- **T-110増分1の巻き戻し**：**不要**（構造はそのまま、AvalonDock側の挙動のみ変える）
- **評価**：非現実的。P-103と同じ理由で見送りが妥当と考える

### 案2：ツールバーを単一DockingManagerの外へ切り出す（部分巻き戻し）

- **内容**：配置ツールバー（`ContentId="PlacementToolBar"`の`LayoutAnchorable`）を
  `MainWindow.xaml`のルートGrid（現在「メニュー/単一DockingManager/ステータスバー」の3行
  構成、`todo.md`861行・1728行）へ独立した固定行として戻す
- **規模**：**中〜大**。(a) ルートGrid行構成の再拡張 (b) ツールバーの「基本機能/配置ツール」
  2タブUI・Aeroテーマ風タブ形状（T-119）をAvalonDock外で作り直すか、別の軽量DockingManager
  として維持するか要設計判断 (c) T-110増分1で追加した「配置ツールバーをフロート化」メニュー
  （ドッキング操作入口の代替提供、`todo.md`943-944行）の要否再検討
- **影響範囲**：ツールバー関連のスタイル・T-119（Aeroテーマタブ）・フロート化メニュー等、
  T-110増分1以降にツールバーへ加えられた対応群
- **T-110増分1の巻き戻し**：**部分的に巻き戻すことになる**。ただし統合の主目的（アクティブ色
  一元化）は**シート・機器表・プロパティ・出力の4パネル間**の話であり、ツールバー自体が
  この一元化の対象として重要だったかは`docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`
  等の再確認を要する（本調査では未確認、家老の采配スコープ外のため深追いせず）
- **評価**：現実的だが規模・影響とも中程度。「巻き戻しか否か」を問われれば**部分的にYes**

### 案3：`CanAutoHide="False"`でシートパネルのAutoHideを封じる（発生源の遮断）

- **内容**：`LayoutAnchorable`の公開プロパティ`CanAutoHide`（`LayoutAnchorable.cs:128`、
  既定`true`）をシートパネルにのみ`False`指定する
- **規模**：**極小**。属性1つ。T-110増分1(6)で採用した`CanDock="False"`と同型の「発生源の
  遮断」パターン（`memory: t110_increment1_moguratataki_overview_effect`）
- **影響範囲**：シートパネルのAutoHide（ピン留め解除）機能そのものが失われる——ユーザーが
  シートパネルをAutoHideできなくなる（他のドッキング操作＝フロート化・移動等は影響なし）。
  **対象を「シートパネルのみ」に絞れる**のは、P-137の調査でAutoHideタブの辺が
  「シート＝左端、機器表・プロパティ＝上部、出力＝左下」と確定しており、**高さが問題になる
  Left/Right辺に現状来ているのはシートパネルのみ**と分かっているため
- **T-110増分1の巻き戻し**：**不要**
- **留意**：**根治ではなく回避**。将来レイアウト構造が変わり（P-137案a＝辺の再配置）他パネルが
  Left/Right辺に来た場合、同型の問題が再燃しうる。AutoHide機能自体を一部封じるというUI/UX上の
  トレードオフを伴うため、**殿裁可を要する**性質の判断と考える
- **評価**：規模最小・影響範囲も限定的。ただし「機能を封じて症状を消す」性質ゆえ、UI/UX判断
  としての裁可が要る

### 案4：現状維持（対応見送り、経過観察）

- **内容**：何もしない。一時計装（P-141）は本判断が済み次第、通常の除去フローに乗せる
- **規模**：ゼロ
- **影響範囲**：AutoHideフライアウト表示中の一時的な実害（ツールバーがクリック不能になる）は
  残るが、フライアウトを閉じれば（ピンを再度押す・別操作等）解消する恒久的な機能喪失ではない
- **T-110増分1の巻き戻し**：不要
- **評価**：P-121（AutoHide復帰時のツールバー高さ潰れ、重大バグだが根本対処の手札なしとして
  保留継続中）と同種の「既知リスクとして記録、実装対応は見送り」という前例がある

---

## 3. 家老DoDへの回答まとめ

1. **対処の選択肢**：上記4案（AvalonDock本体改造／ツールバー切り出し／AutoHide対象除外／
   現状維持）
2. **各案の影響範囲と規模**：案1=大・非現実的、案2=中〜大・ツールバー関連資産の再設計要、
   案3=極小・機能制限を伴う、案4=ゼロ
3. **T-110増分1の設計判断を巻き戻すことになるか**：**案2のみ部分的に巻き戻す**（ツールバーを
   統合対象から除外）。案1・3・4はいずれも統合構造自体には触れない

**隠密所見（判断は殿・家老に委ねる）**：案1は前例（P-103）に照らし現実的でないと考える。
案3は規模最小で殿裁可さえ得られれば早い一方、「症状を機能制限で回避する」性質を伴う。案2は
規模が中〜大だが恒久的な解決に近づく。**いずれを選ぶにせよUI/UX判断（機能を封じるか、構造を
組み直すか）を伴うため、殿裁可が必要な性質の決定**と考える。

---

## 未確認・留保

- 案2の「アクティブ色一元化にツールバーがどこまで寄与しているか」は本調査では未確認
  （`docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`等の再確認を要する）
- 実機での症状再現・各案の効果検証は行っていない（静的読解のみ、実機は忍者領分）
- `CanAutoHide=False`とした場合の既存保存レイアウト（`main-layout.xml`にAutoHide状態で
  永続化されたシートパネルがある場合）との相互作用は未確認

---

## 出典

- `docs-notes/vendor-reference/avalondock-v4.74.1/source/Components/AvalonDock/Themes/generic.xaml`
  （692-742行＝`DockingManager`ControlTemplate、733行＝`PART_AutoHideArea`）
- 同上`Controls/LayoutAutoHideWindowControl.cs`（277-339行＝`CreateInternalGrid()`、
  293-334行＝`AnchorSide`別分岐、69-80行＝`AnchorableStyle`）
- 同上`Layout/LayoutAnchorable.cs`（128-137行＝`CanAutoHide`プロパティ）
- 同上`DockingManager.cs`（47行＝`[TemplatePart(Name = "PART_AutoHideArea")]`、1698行＝
  `GetAutoHideAreaElement()`、2001行＝`GetTemplateChild("PART_AutoHideArea")`）
- `src/Ecad2.App/MainWindow.xaml`（1155行＝配置ツールバーの`LayoutAnchorable`定義、861行・
  1728行＝ルートGrid行構成、943-944行＝ツールバーのフロート化代替メニュー）
- `docs/todo.md`（1240-1306行＝T-110増分1統合の経緯・目的・実機確認結果）
