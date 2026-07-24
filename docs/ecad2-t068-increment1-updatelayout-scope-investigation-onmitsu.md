# T-068増分1 UpdateLayoutの効果範囲、一次ソース確認（隠密）

家老依頼：忍者3周目確認でも観点3（個別パーツ項目の子メニュー展開）が未解消。忍者所見（未確認・
推測）＝`CustomPartsMenu.UpdateLayout()`の呼び出し対象が2階層目（`sub`自体）止まりで3階層目
（`sub`自身の子メニュー、編集/削除項目）に及んでいない疑い。これを一次ソース
（`UpdateLayout`/`ApplyTemplate`の効果範囲が階層をまたいでどこまで及ぶか）で確認。

## 結論：忍者所見は正確（確度：高）

**`UpdateLayout()`は呼び出し対象に限定されないグローバル処理だが、Popupという「開かれるまで
Visual Treeが存在しないコンテナ」を挟む階層構造である以上、まだ開かれていないPopup内部（3階層目）
には原理的に到達できない。**忍者の「2階層目止まりで3階層目に及んでいない」という推測は、
一次ソースの構造から見て正確と判断する。

## 一次ソース確認（dotnet/wpf）

### 1. `UpdateLayout()`自体はDispatcher全体を処理するグローバル操作（`UIElement.cs:1630-1633`）

```csharp
public void UpdateLayout()
{
    ...
    ContextLayoutManager.From(Dispatcher).UpdateLayout();
}
```

呼び出し元（`this`＝`CustomPartsMenu`）に処理対象を限定する実装ではなく、**同一`Dispatcher`
（同一UIスレッド）が管理する`MeasureQueue`/`ArrangeQueue`全体を同期的に処理する**、という
グローバルな仕組みであることを確認した。侍の1回目修正（`CustomPartsMenu.UpdateLayout()`）自体は、
この意味で「`CustomPartsMenu`自身とその直接の子（`sub`群）」のMeasure/ApplyTemplateを確定させる
効果はあったと考えられる（1件目の階層＝`sub`自体の展開失敗は解消したため）。

### 2. Popupの子コンテンツは`IsOpen=true`になって初めてVisual Treeが構築される（`Popup.cs`）

```csharp
private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    ...
    popup.CreateWindow(false /*asyncCall*/);   // 336-359行目
    ...
}

private void CreateWindow(bool asyncCall)
{
    ...
    BuildWindow(targetVisual);   // 1480-1568行目、実際のウィンドウ生成・子コンテンツ接続はここ
    ...
    OnOpened(EventArgs.Empty);
}
```

`Popup`（`MenuItem`のControlTemplate内`PART_Popup`、`_submenuPopup`フィールド、
`MenuItem.cs:2155-2161 OnApplyTemplate`で取得）は、**`IsOpen`プロパティがtrueに変化した時点で
初めて`CreateWindow`/`BuildWindow`が呼ばれ、子コンテンツ（`ItemsPresenter`とその配下）が
Visual Treeへ接続される**設計であることを確認した。`IsOpen=false`の間、Popup内部は別ウィンドウ・
別ビジュアルツリールートとしてすら存在しない。

### 3. 組み合わせた結論

`CustomPartsMenu_SubmenuOpened`ハンドラ内で`CustomPartsMenu.UpdateLayout()`を呼んだ時点では：

- **存在するもの**：`CustomPartsMenu`自身、および`CustomPartsMenu.Items`へ追加済みの`sub`群
  （2階層目）——これらは`UpdateLayout()`によりMeasure/ApplyTemplateが確定しうる
- **まだ存在しないもの**：`sub`自身の`PART_Popup`内部（`editItem`/`deleteItem`、3階層目）——
  `sub.IsSubmenuOpen`はこの時点でまだ`false`のため、`sub`の`_submenuPopup`の`IsOpen`も`false`で
  あり、`CreateWindow`/`BuildWindow`が一度も呼ばれていない。ゆえにこの内部要素はまだ
  `MeasureQueue`/`ArrangeQueue`に入りようがなく、**`CustomPartsMenu.UpdateLayout()`をいくら呼んでも
  原理的に到達不可能**

**忍者所見「2階層目止まりで3階層目に及んでいない」は、この構造から見て正確な指摘と判断する**
（確度：高、一次ソースの`OnIsOpenChanged`/`CreateWindow`/`BuildWindow`の呼び出し条件から論理的に
導かれる）。

## モグラ叩き俯瞰評価の更新（家老依頼、前回報告の追認）

前回報告（`docs/ecad2-t068-increment1-submenu-bug2-investigation-onmitsu.md`）で示した懸念が、
今回さらに具体的な形で的中しつつあると考える。侍の`UpdateLayout()`修正は「2階層目の境界」で
発生した問題には効いたが、**同じ種類のタイミング問題（動的生成直後・ApplyTemplate/レイアウト
未完了状態での即座操作）が、Popupの階層が1段深くなるたびに、その階層の境界で再現する**という
構造が一次ソースで裏付けられた。

理論的には「`sub.IsSubmenuOpen`がtrueになった直後（3階層目のPopupが開いた瞬間）に、
`sub`（またはそのPopup内`ItemsPresenter`）へ`UpdateLayout()`を追加で呼ぶ」という対症療法を
さらに重ねれば、今回の3階層構造（パーツ(_P)→自作パーツ(_C)→個別パーツ名→編集/削除）は
解消できる可能性が高い。しかし、これは**階層が1段増えるたびに同種の対症療法を追加し続ける**
ことに等しく、モグラ叩きそのものである。将来さらに深い階層を追加する設計変更があれば、
同じ問題がまた別の境界で再発するリスクが構造的に残る。

## 対処の方向性（参考、決定は家老・侍・殿に委ねる）

- **(a) 対症療法の継続**：`sub`の`IsSubmenuOpen`変化（`SubmenuOpened`イベント等）を検知し、
  その都度`UpdateLayout()`を追加で呼ぶ。今回の3階層構造なら解消しうるが、モグラ叩きの性質は残る
- **(b) 設計変更**：動的な入れ子メニュー構造自体を避け、自作パーツの一覧・編集・削除を
  `ListBox`+`ItemsSource`バインディング等、WPFの動的コンテンツ表示として実績のある形（ダイアログ内
  リスト等）に切り替える。Popupの開閉タイミングに依存する階層構造を持たないため、今回のような
  境界問題が原理的に発生しない

前回報告と同じく、これはUI/UX分岐（画面構成の変更）になりうる論点であり、殿確認が必要と考える。

## 出典

- `UIElement.cs`（dotnet/wpf main、`UpdateLayout`メソッド本体、1630-1633行）
- `Popup.cs`（dotnet/wpf main、`OnIsOpenChanged`/`CreateWindow`/`BuildWindow`、336-359行・
  1480-1610行）
- `MenuItem.cs`（`OnApplyTemplate`・`_submenuPopup`フィールド、2155-2161行・2682-2685行）
- 侍コミット`9855a36`（`CustomPartsMenu.UpdateLayout()`追加）
- `docs/ecad2-t068-increment1-submenu-bug2-investigation-onmitsu.md`（前回の隠密調査、本報告は
  その追認・具体化）
