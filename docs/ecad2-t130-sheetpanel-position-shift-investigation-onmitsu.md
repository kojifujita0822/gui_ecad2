# T-130 シートパネルのドッキング位置ずれ 原因調査（隠密）

> 2026-07-27 隠密調査。家老采配「T-130 シートパネル位置ずれの原因調査」（P-122起票、殿裁可
> 2026-07-27）を受けての静的調査。共有main上での一時注入は行わず、静的読解のみで実施した。
> DoD＝(1)再現条件の特定 (2)原因の層の切り分け (3)P-121との同根性判定 (4)対処方針の案。

---

## 総括

**結論（推測、CONFIRMEDではない・机上調査のみ）**：AvalonDock標準の`LayoutAnchorable.
ToggleAutoHide()`には、**AutoHide解除時に「元のドッキング先ペイン（`PreviousContainer`）が
何らかの理由でnullになっていた場合、位置情報を一切参照せず、サイド方向だけを頼りに
RootPanelの端へ新規の小さいペインを自動生成する」という一次ソース上明確な分岐**が存在する
（`LayoutAnchorable.cs:442-555`）。この`PreviousContainer`は、**`LayoutRoot.CollectGarbage()`が
「参照先ペインの親がnull・または別Root」と判定した場合に強制的にnullクリアする**（`LayoutRoot.
cs:361-365`）——AutoHide中のパネルを包む`LayoutAnchorGroup`自身もこのクリア対象に含まれる
一次ソース構造になっている。**シートパネルをAutoHide化した状態で、CollectGarbage()を伴う
何らかのモデル操作（Dock()/Float()呼び出し、レイアウトのDeserialize等）が挟まると、AutoHide
解除時に元の位置（左パレット190px幅の専用スロット）を見失い、RootPanel端へ誤配置される**、
という機序が最も筋が通ると判断する。「左上に『シート▼』」という忍者の観測（本来のタイトル
位置とは異なる場所に、タイトル+ドロップダウン矢印だけの狭いペインが現れる）とも整合する。

**ただし本調査は一次ソースの静的読解のみに基づく仮説であり、実機での再現確認を経ていない**。
確度を上げるには忍者の実機実験（4節「再現条件」の手順）が必要。

---

## 1. 初動：main-layout.xmlの現状確認

`%AppData%\Ecad2\docking-layout\main-layout.xml`（現時点、最終更新2026-07-27 10:59:32＝
本日のT-127/T-128検証で書き換わった後の状態）を確認したところ、シートパネルは以下のとおり
**正常な構造**で記録されている：

```xml
<LayoutPanel Orientation="Horizontal">
  <LayoutAnchorablePane DockWidth="190">
    <LayoutAnchorable ... Title="シート" IsSelected="True" ContentId="LeftPalette" CanFloat="False" .../>
  </LayoutAnchorablePane>
  ...
```

**このファイル単体からは位置ずれの兆候は見えない**。ただし、これは(a)P-122発生時点
（2026-07-22、T-110増分3実機確認中）とは異なる、その後複数回上書きされた後の状態であり、
(b)問題が「保存ファイルの恒久的な破損」ではなく「実行時のセッション内でのみ起きる一過性の
モデル異常」である可能性を示唆する——後者の場合、正常終了すれば正常な状態が保存され、
ファイル単体を見ても再現しない。**永続化ファイルの恒常的破損という層は今回は該当しない
可能性が高い**（2節で詳述）。

---

## 2. 前段調査からの継承知見

家老指定の前段調査書4件を確認した。

- **T-099(c)「保存済みレイアウトの読込に失敗」調査**：`HasExpectedContent`機構（ContentId
  集合の不一致検出）の話であり、本件とは直接の関係は薄いが、「Deserialize前後でモデルの
  整合性が壊れうる」という一般的な教訓は共通する。
- **T-104 DoD(4)キーボードナビゲーション調査**：AutoHideサイド領域（`LeftSidePanel`等）が
  未使用でも常に生成される構造的特性を確認済み。本件のAutoHide関連調査の背景知識として有用
  だが、直接の原因ではない。
- **T-110増分2 sheet-loss調査**：MouseAssistant競合によるUndo連打が真因、実装回帰なし。
  ただし副産物として**「開始時にセットした状態フィールドの対のクリア漏れ」というecad2自前
  コードのバグ（`_sheetDragSource`のMouseUpクリア漏れ）** を検出しており、**「ペア処理の
  片側だけが漏れる」という不具合の型自体は、本件の疑い（AutoHide発動時にPreviousContainerが
  正しく設定されても、その後の操作で消失する）とパターンとして類似する**。
- **T-110 Ctrl+Alt+S保存調査**：合成キー入力の配送問題（Alt絡みショートカットの限界）が真因。
  本件とは無関係。

**最も直結する知見はこの4件の外にあった**——`docs/ecad2-t099-c-overlaywindow-droptarget-and-
attachdrag-survey-onmitsu.md`の「調査6：縦長44pxタブとして左端に別追加」（441-511行）が、
まさに**RootPanel自動補完によるDocumentPane侵入**という、本件と同型（「元の位置を見失い
RootPanel端へ異常配置される」）の機序を扱っていた（家老が「まず精読せよ」と指示した
CollectGarbage/RootPanel setterのnull補完/OnLayoutChangedの3点は、この調査6の核心部分）。
本調査はこの知見を**AutoHide解除の経路**へ適用したものである。

---

## 3. 一次ソース調査（本調査の中心）

### 3.1 AutoHide解除時の異常分岐（`LayoutAnchorable.ToggleAutoHide()`）

`LayoutAnchorable.cs:434-607`（AvalonDock v4.74.1）を全文精読した。AutoHide解除処理
（`IsAutoHidden`がtrueの場合の分岐、436-585行）は以下の構造を持つ：

```csharp
var parentGroup = Parent as LayoutAnchorGroup;
var parentSide = parentGroup.Parent as LayoutAnchorSide;
var previousContainer = ((ILayoutPreviousContainer)parentGroup).PreviousContainer as LayoutAnchorablePane;

if (previousContainer == null)
{
    // サイド方向(Left/Right/Top/Bottom)だけを見て、RootPanelの先頭/末尾へ
    // 新規LayoutAnchorablePaneを挿入する（元の位置・DockWidth等の情報は一切使わない）
    switch (side) { case AnchorSide.Left: ... RootPanel.Children.Insert(0, previousContainer); ... }
}
```

**`previousContainer`（AutoHide化前にいた元のペイン）がnullの場合、AvalonDockは元の位置情報を
一切参照せず、単にサイド方向に応じてRootPanelの先頭または末尾へ新規の`LayoutAnchorablePane`
（DockWidth等は既定値のまま）を挿入する**（442-555行）。この新規ペインの幅は指定が無く、
既定の自動採寸（コンテンツ要求幅）になる——「本来190px幅の左パレット専用スロットにあるべき
ものが、DockWidth未指定の細いペインとしてRootPanel端に現れる」という位置・見た目両面の異常を
説明できる。

### 3.2 PreviousContainerが失われる経路（`LayoutRoot.CollectGarbage()`）

`LayoutRoot.cs:352-465`（`CollectGarbage()`全文）361-365行：

```csharp
foreach (var content in this.Descendents().OfType<ILayoutPreviousContainer>()
    .Where(c => c.PreviousContainer != null &&
        (c.PreviousContainer.Parent == null || c.PreviousContainer.Parent.Root != this)))
{
    content.PreviousContainer = null;
}
```

`ILayoutPreviousContainer`を実装する**全ての**要素が対象——`LayoutAnchorable`（個別コンテンツ）
だけでなく**`LayoutAnchorGroup`（AutoHideの入れ物）自身も対象に含まれる**（`LayoutAnchorGroup.
cs:21`で`ILayoutPreviousContainer`を実装済み、一次ソース確認済み）。つまり、**AutoHide中の
パネル群を包む`LayoutAnchorGroup`が持つ「元のドッキング先ペインへの参照」（3.1節の
`previousContainer`の実体）は、CollectGarbage()実行時に元のペインが一時的に「親を持たない」
または「Rootが異なる」状態になっていれば、無条件でnullへクリアされる**。

`Dock()`（`LayoutContent.cs:598-634`）・`Float()`（同541行）はいずれも処理の最後に
`Root.CollectGarbage()`を呼ぶ。つまり、**シートパネルがAutoHide中の間に、他パネルへのDock()/
Float()操作やレイアウトのDeserializeなど、CollectGarbage()を誘発する何らかの操作が挟まると**、
その瞬間のモデルツリーの過渡状態次第で、AutoHide中のシートパネルの`PreviousContainer`が
巻き添えでクリアされる可能性がある（構造的に成立する経路として確認、実際に発火する具体的な
操作シーケンスは未特定）。

### 3.3 PreviousContainerのシリアライズは「参照」でなく「ID」

`LayoutContent.cs`・`LayoutAnchorGroup.cs`とも、`PreviousContainer`自体は`[XmlIgnore]`
（オブジェクト参照はシリアライズされない）。代わりに`WriteXml`/`ReadXml`で`PreviousContainerId`
（GUID文字列）としてIDのみをやり取りする設計（`LayoutContent.cs:509-513`・`LayoutAnchorGroup.
cs:29-30`）。**IDから実オブジェクト参照への解決処理（Fixup相当）が`LayoutRoot.ReadXml`
（541-577行）自体には存在しないことを確認した**——解決ロジックの所在は`XmlLayoutSerializer`側
（本調査では未追跡）にあると推測されるが、**Deserializeを経由した場合にPreviousContainerが
確実に復元される保証は一次ソースからは確認できていない**。T-099(c)が「モデル手術でなく
Layout差し replaceに任せるのが正解」という方針を確立した背景（`docs/ecad2-t099-c-dock-restore-
by-default-xml-design-onmitsu.md`）とも符合し、**AutoHide状態はDeserializeとの相性が本質的に
悪い可能性がある**（推測、確度中）。

---

## 4. 再現条件の特定（DoD(1)）

**確実な再現条件は特定できていない**（実機実験が必要）。一次ソースから導ける最有力の仮説手順は
以下：

1. シートパネル（LeftPalette）をAutoHide化する（表示メニュー→「パネルを自動的に隠す」→
   「シート」、T-110増分3で新設されたUI）
2. AutoHide中に、**CollectGarbage()を誘発する操作**を行う——候補：他パネルのFloat化/Dock化、
   Ctrl+Alt+R（レイアウトリセット）、あるいはアプリ終了→再起動（保存/読込を経由）
3. シートパネルのAutoHideを解除する（フライアウトのピン留めボタン、またはメニューから再度
   トグル）
4. → 元の左パレット位置（190px幅）に戻らず、別位置（RootPanel端）へ誤配置されるか観察

**特にステップ2で「アプリ終了→再起動」を挟む場合が最有力**——T-110増分3実機確認時、忍者は
複数パネルのAutoHide/Dock操作を一通り試したと推測される（todo.md記述、`docs-notes/`に増分3
専用の忍者検証記録ファイルが見当たらないため詳細な操作列は未確認）ため、何らかの組み合わせで
本条件を踏んだ可能性が高いと考えるが、**断定はできない**（未確認、忍者の実機記憶・再実験に
委ねたい）。

---

## 5. 原因の層の切り分け（DoD(2)）

| 層 | 判定 | 根拠 |
|---|---|---|
| **表示（XAML/スタイル）** | 該当なし | タイトルバー非表示化（T-110増分3案A）等の表示層の変更は、ペインの配置先（RootPanel上のどこにあるか）には影響しない構造 |
| **レイアウトモデル（AvalonDock内部状態）** | **最有力** | 3節で確認した`ToggleAutoHide()`のnull分岐＋`CollectGarbage()`のPreviousContainer強制クリアという、一次ソース上明確な経路が存在する |
| **永続化ファイル（main-layout.xml）** | 直接の原因ではない可能性が高い | 現在のファイルは正常な構造（1節）。ただし、AutoHide状態のままアプリを終了した場合に保存されるXMLの内容、およびそれを読み込んだ際の`PreviousContainerId`解決が正しく行われるかは未検証（3.3節の不明点）——**永続化ファイルが「引き金の一つ」になる可能性は残るが、ファイル自体が恒常的に壊れているわけではない** |

**結論**：**レイアウトモデル層（AvalonDock標準`ToggleAutoHide()`とCollectGarbage()の相互作用）が
主因、永続化ファイルは(あるとしても)引き金の一つに留まる**、という切り分けが現時点の一次ソース
調査からの最有力の見立て。

---

## 6. P-121との同根性判定（DoD(3)）

**「AutoHide復帰処理系の不具合」という大枠では同根だが、具体的な機序は異なる別種の罠**と
判定する。

| | P-121（ツールバー機能不全） | P-122（本件、シートパネル位置ずれ） |
|---|---|---|
| 発生層 | ビジュアルツリー（`IsVirtualizingAnchorable`のコンテナ生成/破棄） | モデル層（`PreviousContainer`参照の消失） |
| トリガー | フライアウトのピン経由復帰、**合成マウスイベント特有のタイミング**（殿の物理操作では未発生） | AutoHide中の`CollectGarbage()`誘発操作（**タイミング非依存の構造的な罠の可能性**、合成入力固有ではないと見立てる） |
| 再現性 | 間欠的（レースコンディション） | 未確認だが、条件が揃えば決定論的に再現しうる（レースコンディションでなく状態の順序依存） |
| 対処の見立て | 根本対処の手札なし（`IsVirtualizingAnchorable="False"`は新害を生み不採用） | 3.1節の分岐を回避する防御コードが書ける可能性がある |

両者とも「T-110増分3で新設したAutoHide機構が、AvalonDock標準機構の想定していなかった使い方
（単一DockingManager統合後の複雑な状態遷移）に触れて顕在化した」という**発生の文脈**は共通する
が、**バグの実体（レースコンディション vs 状態参照の消失）は別物であり、片方の対処がもう片方に
波及することは期待できない**。「P-121と同じ一連の不具合」として一括りに扱うより、**別件として
個別に対処方針を立てるのが適切**と考える。

---

## 7. 対処方針の案（規模見積つき、DoD(4)）

いずれも机上設計、実装前に要検討・要実機裏付け：

- **案1（本命候補、規模：小〜中）**：`AutoHideMenuItem_Click`（`MainWindow.xaml.cs:564`）または
  AutoHide解除操作の入口で、対象`LayoutAnchorable`が`IsAutoHidden`から復帰する直前に
  `PreviousContainer`の健全性（3.1節のnull分岐に落ちるかどうか）を確認し、nullであれば
  ecad2側で正しい復帰先（ContentIdベースで既定位置を特定）へ明示的に`Show()`/`Children.Add()`
  相当の処理を行う防御コード。**ただし「モデル手術」寄りの対処であり、T-099(c)の教訓
  （自前モデル操作は新たな不変条件との衝突を生みやすい）に照らすとリスクを伴う**——
  導入する場合は最小限（nullチェックとログ記録のみ、実際の復元は次善策）に留めるべきと考える。
- **案2（規模：中〜大）**：AutoHide状態そのものをecad2側の管理下に置き、AutoHide化・解除の
  両方を「対象ContentIdの記録」＋「解除時は既定位置（XAML初期構成）への復帰」という、
  T-099(c)案Y・T-110増分1（A-3、標準Dock()へ回帰した判断）と同じ設計思想（標準機構への信頼
  ＋失敗時は既定へ確実にフォールバック）で作り直す。規模は大きいが、re-entrant性・
  再現性の高さの両面でP-121より対処しやすい部類と見る。
- **案3（先行、規模：小）**：まず4節の再現手順を忍者に実機で試してもらい、再現するかどうか・
  再現する場合の正確なトリガー操作列を確定させる。再現すれば、侍の診断ログ注入
  （`PreviousContainer`の値をAutoHide解除の直前直後で記録）で3節の仮説をCONFIRMED級まで
  引き上げてから対処設計に入るのが、モグラ叩きを避ける定石（`memory:
  feedback_diagnostic_log_escalation`）と考える。

**現時点の規模見積は「不明」**——3節の仮説が正しいかどうかも含め、まず再現確認（案3）を
先行させるのが筋と考える。対処自体（案1）は小〜中規模で済む可能性が高いが、確定は次段階を
待ちたい。

---

## 不明点・忍者への申し送り

- 4節の再現手順が実際に症状を再現するかどうかは未検証。特に「AutoHide化→CollectGarbage誘発
  操作→AutoHide解除」のうち、どの具体的な操作がCollectGarbage()を誘発するに足るタイミングで
  挟まるかは、実機での試行錯誤が必要。
- T-110増分3実機確認時の忍者の詳細な操作列（どのパネルをどの順でAutoHide/Dock/Floatしたか）が
  記録として残っていれば、再現条件の絞り込みに直結する。ファイル化された記録が見当たらな
  かった（peerメッセージのみの可能性）ため、忍者の記憶・ログに委ねたい。
- 3.3節の「PreviousContainerId→実体解決処理の所在」は`XmlLayoutSerializer`側に残ると推測する
  が未追跡。再現確認が取れ、対処設計（案2）へ進む段階になれば追加調査の価値がある。

---

## 出典・参照

- `%AppData%\Ecad2\docking-layout\main-layout.xml`（現状、2026-07-27 10:59:32時点）
- `docs/proposed.md`（P-122記載、134行）
- `docs/todo.md`（T-130節53-71行、T-110節954-1189行、P-121関連記述133行）
- `docs/ecad2-t099-c-dock-layout-load-failure-investigation-onmitsu.md`
- `docs/ecad2-t104-tabnav-autohide-focustrap-survey-onmitsu.md`
- `docs/ecad2-t110-increment2-sheet-loss-investigation-onmitsu.md`
- `docs/ecad2-t110-ctrlalts-save-investigation-onmitsu.md`
- `docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-onmitsu.md`（調査6、441-511行）
- `docs/ecad2-t099-c-dock-restore-by-default-xml-design-onmitsu.md`（T-099(c)案Y設計思想）
- `src/Ecad2.App/MainWindow.xaml.cs`（160-419行＝コンストラクタ・AutoHide/Float関連ハンドラ、
  545-570行＝`AutoHideMenuItem_Click`、605-670行＝`RegisterDockingContents`/`HasExpectedContent`）
- AvalonDock v4.74.1一次ソース（`docs-notes/vendor-reference/avalondock-v4.74.1/`、本調査で新規
  取得＝`Layout/LayoutContent.cs`・`Layout/LayoutAnchorGroup.cs`・`Layout/LayoutRoot.cs`全文、
  既存＝`Layout/LayoutAnchorable.cs`全文）
