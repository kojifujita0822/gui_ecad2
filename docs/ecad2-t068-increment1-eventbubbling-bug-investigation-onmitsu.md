# T-068増分1 重大バグ、真因はイベントバブリングの発火源チェック漏れ — 独立確認（隠密）

家老依頼：忍者診断ログから新たな手がかり（`sub.SubmenuOpened`発火の3ms後、親`CustomPartsMenu`側の
`SubmenuOpened`ハンドラが再度呼ばれた痕跡）。忍者仮説＝`SubmenuOpened`がRoutedEvent(Bubble方向)の
ため子のイベントが親へバブリングし親ハンドラを誤発火、`Items.Clear()`で全階層が巻き込まれて消える、
という筋の当否を一次ソースで確認。侍への同時依頼につき独立性を保ち侍側の結論は参照せず調査。

## 結論：忍者仮説は正しい（確度：高）。従来の「Popupタイミング問題」仮説は訂正を要する

**真因はWPFの`SubmenuOpened`RoutedEventのBubbleルーティングを考慮せず、
`CustomPartsMenu_SubmenuOpened`ハンドラが発火源チェックを一切行っていないという、単純な実装バグ**
と判断する。前回・前々回の調査（`ecad2-t068-increment1-submenu-bug2-investigation-onmitsu.md`・
`ecad2-t068-increment1-updatelayout-scope-investigation-onmitsu.md`）で示した「Popupの
ApplyTemplate/Measureタイミング問題」という仮説は、実際にはこの発火源チェック漏れという、より
単純で直接的な原因の陰に隠れていた可能性が高い。一次情報（診断ログ・一次ソース）が示す結論を
優先し、率直に見解を更新する。

## 一次ソース確認

### 1. `SubmenuOpenedEvent`は`RoutingStrategy.Bubble`（`MenuItem.cs:279-280`、既確認事項の再確認）

```csharp
public static readonly RoutedEvent SubmenuOpenedEvent =
    EventManager.RegisterRoutedEvent("SubmenuOpened", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MenuItem));
```

`sub`（子孫、`CustomPartsMenu`の子）で発火した`SubmenuOpened`イベントは、Bubble戦略により
論理ツリーの祖先方向へ伝播し、`CustomPartsMenu`に登録されたハンドラも呼び出される。

### 2. WPF自身の内部実装が、まさに同種のバブリングイベントで発火源チェックを行っている
（`MenuItem.cs:1037-1046`、`OnIsSelectedChanged`）

```csharp
private static void OnIsSelectedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e)
{
    // If IsSelected changed on a child of the MenuItem, change CurrentSelection
    // to the element that sent the event and handle the event.
    if (sender != e.OriginalSource)   // ← 発火源が自分自身か子孫かを明示的に区別
    {
        MenuItem menuItem = (MenuItem)sender;
        MenuItem source = e.OriginalSource as MenuItem;
        ...
    }
}
```

これは`IsSelectedChangedEvent`（同じくBubble方向のRoutedEvent）を処理する際、WPF自身が
「`sender`（ハンドラが登録された要素）と`e.OriginalSource`（実際の発火元）が異なる＝子孫からの
バブリングイベント」というケースを明示的に区別する実装パターンを採用している決定的な参考資料。
バブリングイベントを親でハンドルする際は、この発火源チェックが標準的な作法であることを裏付ける。

### 3. 対象コード（`CustomPartsMenu_SubmenuOpened`）にはこのチェックが無い

```csharp
private void CustomPartsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
{
    CustomPartsMenu.Items.Clear();   // ← e.OriginalSource / e.Sourceのチェックなし、無条件実行
    var customs = _viewModel.PartPalette.Entries...
    ...
}
```

`sub.IsSubmenuOpen`が`true`になった瞬間、`sub`自身の`SubmenuOpened`が発火し、Bubbleルーティング
により`CustomPartsMenu`まで伝播、このハンドラが（`sender=CustomPartsMenu`だが`e.OriginalSource=sub`
という状態で）**誤って再度実行される**。ハンドラ内の`CustomPartsMenu.Items.Clear()`により、
たった今開こうとしていた`sub`自身（とその子孫Popup）を含む既存の全項目がツリーから除去され、
その後新しいインスタンス群へ再構築される——これが「個別パーツ項目を開こうとした瞬間に全階層が
閉じる」現象の直接的な引き金と判断する。忍者の診断ログ所見（3ms後に親ハンドラ再発火の痕跡）と
完全に整合する。

## モグラ叩き俯瞰評価の訂正

前回・前々回、「動的構築+開いた直後の操作というアプローチがPopup階層の境界（ApplyTemplate/
Measureタイミング）で繰り返し衝突する構造的リスク」という見立てを提示したが、**今回の決定的な
原因（イベントバブリングの発火源チェック漏れ）を踏まえると、この見立ては修正を要する**。

- 1件目のバグ（`CustomPartsMenu`自体がHasItems=falseで開かない）は、本件とは独立した別の真因
  （XAML上の静的子要素0件）であり、これは解消済みの事実として変わらない
- 2件目・3件目の「子メニューを開こうとすると全階層が閉じる」現象は、Popupタイミング問題という
  仮説よりも、**「`sub`自身が`SubmenuOpened`を発火しうる（＝2階層の動的入れ子構造を持つ）ことを
  考慮せず、親ハンドラが発火源チェックを怠っていた」という単純な実装バグの方が、より直接的で
  決定的な説明**と判断する

ただし、**この問題が起こりうる前提条件自体（`sub`が自分の子として`editItem`/`deleteItem`を持つ
＝2階層の動的入れ子構造を持つこと）は、依然として「動的に入れ子のMenuItemツリーを構築する」という
設計アプローチに起因する**。「モグラ叩き」という表現自体は取り下げないが、その中身は「Popupの
レイアウトタイミング」ではなく「RoutedEventのルーティングを考慮した実装の作法」という、より
基本的なWPF知識の適用漏れに近いと訂正する。

## 対処の方向性（参考）

`CustomPartsMenu_SubmenuOpened`の冒頭に発火源チェックを追加する、1行規模の修正で解決できる可能性が
高いと考える：

```csharp
private void CustomPartsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
{
    if (e.OriginalSource != CustomPartsMenu) return;   // 子孫からのバブリングは無視
    CustomPartsMenu.Items.Clear();
    ...
}
```

これが正しく機能すれば、前回提示した「対症療法(UpdateLayoutの追加呼び出しを階層ごとに重ねる) vs
設計変更」という重い分岐は不要になり、**対症療法ですらない、単純な既知バグ修正**で解決する見込みが
高い。ただし実機検証（忍者）は必須（本調査は一次ソース+ログ解釈のみ、実装・実機確認は侍・忍者の
役割）。

## 出典

- `MenuItem.cs`（dotnet/wpf main、`SubmenuOpenedEvent`定義279-280行・`OnIsSelectedChanged`
  1037-1046行）
- `src/Ecad2.App/MainWindow.xaml.cs`（`CustomPartsMenu_SubmenuOpened`、コミット`9855a36`時点）
- 忍者診断ログ所見（家老経由の共有、3ms後の親ハンドラ再発火痕跡）
