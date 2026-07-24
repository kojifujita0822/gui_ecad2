# T-068増分1 重大バグ「自作パーツ」サブメニューが開かない — 独立調査（隠密）

家老依頼：忍者実機確認（`docs/ecad2-t068-increment1-verification-ninja.md`）で「自作パーツ」
サブメニューが一切開かない（6操作パターン全て不成立）と判明、根本原因を一次ソース（WPF MenuItemの
HasItems判定ロジック・SubmenuOpenedイベントの発火条件）で確認。侍への同時依頼（Wチェック方式）
につき、独立性を保つため侍側の結論は参照せず本調査を行った。

## 結論：確度高（一次ソース+実コード比較で確定）

**原因はWPF`MenuItem.Role`が`HasItems`プロパティに完全依存して決まる仕様と、`CustomPartsMenu`が
XAML上で子要素0件の自己終了タグとして定義されていることの組み合わせ**。忍者理論（HasItems判定に
よりExpandCollapsePattern相当の機能が提供されない疑い）の方向性は正しい。

## 一次ソース確認（dotnet/wpf、`PresentationFramework/System/Windows/Controls/MenuItem.cs`）

### 1. `Role`は`HasItems`のみで決まる（`MenuItem.cs:764-792`、`UpdateRole()`）

```csharp
private void UpdateRole()
{
    MenuItemRole type;
    if (!IsCheckable && HasItems)
    {
        type = LogicalParent is Menu ? MenuItemRole.TopLevelHeader : MenuItemRole.SubmenuHeader;
    }
    else
    {
        type = LogicalParent is Menu ? MenuItemRole.TopLevelItem : MenuItemRole.SubmenuItem;
    }
    SetValue(RolePropertyKey, type);
}
```

`HasItems=false`の場合、`Role`は`SubmenuHeader`（サブメニューを持つヘッダー）ではなく
`SubmenuItem`（通常のクリック実行アイテム）になる。`UpdateRole()`は`OnItemsChanged`
（`MenuItem.cs:2044-2049`）からも呼ばれ、Itemsコレクションが変化するたびに再評価される仕組み自体は
存在するが、**マウスクリックの瞬間にはまだItemsが空のまま**という点が次項で致命的に効いてくる。

### 2. マウスクリック処理は`Role`で分岐する（`MenuItem.cs:1465-1514`）

```csharp
private void HandleMouseDown(MouseButtonEventArgs e)
{
    ...
    MenuItemRole role = Role;
    if (role == MenuItemRole.TopLevelHeader || role == MenuItemRole.SubmenuHeader)
    {
        ClickHeader();   // サブメニューを開く(IsSubmenuOpen=true)処理はここのみ
    }
    ...
}

private void HandleMouseUp(MouseButtonEventArgs e)
{
    ...
    MenuItemRole role = Role;
    if (role == MenuItemRole.TopLevelItem || role == MenuItemRole.SubmenuItem)
    {
        ClickItem(e.UserInitiated);   // 通常のクリック実行(Clickイベント相当)
    }
    ...
}
```

`SubmenuOpened`イベント（`MenuItem.cs:279-300`）は`IsSubmenuOpen`プロパティが変化した時にのみ発火する
（`OnIsSubmenuOpenChanged`経由、`MenuItem.cs:631`）。`IsSubmenuOpen`をtrueにする経路は`ClickHeader()`
（マウス）・`OpenSubmenuWithKeyboard()`（キーボード）のみで、いずれも`Role==Header`系のときにしか
呼ばれない。

### 3. `CustomPartsMenu`はXAML上でHasItems=falseとして初期化される（`MainWindow.xaml:969`）

```xml
<MenuItem x:Name="CustomPartsMenu" Header="自作パーツ(_C)" SubmenuOpened="CustomPartsMenu_SubmenuOpened"/>
```

自己終了タグで子要素0件。起動直後から`HasItems=false`→`Role=SubmenuItem`が確定する。マウスクリック
すると`HandleMouseUp`側の`role==SubmenuItem`分岐に入り`ClickItem()`が呼ばれるのみ（`Click`ハンドラ
未登録のため見た目上は反応なし）、`HandleMouseDown`側の`ClickHeader()`（サブメニュー展開）には
到達しない。**`IsSubmenuOpen`が一度もtrueにならないため、`SubmenuOpened`ハンドラ内での動的構築
（`Items.Clear()`→`Items.Add(...)`）自体に到達できない「鶏と卵」の構造的欠陥**。

## 決定的な裏付け：侍が「同型」と参照した既存パターンとの実装差異

侍のコメント（`MainWindow.xaml:958-961`）は「自作パーツ(_C)サブメニューは...
`AutoHideSubmenu_SubmenuOpenedと同型パターン`」としていたが、実際のXAML定義を比較すると
**決定的な差異**がある。

既存の`AutoHideSubmenu`（`MainWindow.xaml:936-940`）：
```xml
<MenuItem Header="パネルを自動的に隠す(_A)" SubmenuOpened="AutoHideSubmenu_SubmenuOpened">
    <MenuItem x:Name="AutoHideLeftPaletteMenuItem" Header="シート(_S)" IsCheckable="True" Tag="LeftPalette" Click="AutoHideMenuItem_Click"/>
    <!-- 他3件も同様に静的定義 -->
</MenuItem>
```
これは開始・終了タグ形式で、**4件の子`MenuItem`がXAML上に静的に定義済み**。起動直後から
`HasItems=true`→`Role=SubmenuHeader`が確定しており、`SubmenuOpened`ハンドラ（`MainWindow.xaml.cs`の
`AutoHideSubmenu_SubmenuOpened`）は既存4項目の`IsChecked`プロパティを最新化するだけで、項目の
追加・削除は一切行わない。

対して`CustomPartsMenu`は自己終了タグで子要素0件。「`SubmenuOpened`イベントを使う」という表層は
同じでも、**「子要素が最初から静的に1件以上存在するか、それとも動的にゼロから構築するか」という
決定的な設計差異**があり、この差異がまさに`HasItems`初期値の違い→`Role`判定の違い→サブメニュー
展開可否の違いを生んでいる。侍はこの差異を見落として「同型パターン」と誤認した可能性が高い。

## 対処の方向性（参考、実装判断は侍・家老に委ねる）

`CustomPartsMenu`のXAML定義にダミーの子要素を最低1つ静的に持たせれば（例：
`<MenuItem Header="(読み込み中)" IsEnabled="False"/>`をプレースホルダとして1件置く）、起動時から
`HasItems=true`が保証され`SubmenuOpened`は正常に発火するはずである。`SubmenuOpened`ハンドラ内の
既存ロジック（`Items.Clear()`後に実際の項目を追加、0件なら`(なし)`表示）自体は正しく動く設計と
見られ、初期XAML側の子要素0件という一点のみが問題と考えられる（推測、実装確認は侍側で要検証）。

## 確度・不明点

一次ソース（`MenuItem.cs`）の`UpdateRole()`・`HandleMouseDown`/`HandleMouseUp`の実装、および
自プロジェクトのXAML実装（`CustomPartsMenu`のタグ形式と既存`AutoHideSubmenu`の差異）は実際に
コードを読んで確認した事実。「対処案でこのバグが完全に解消するか」は実機検証を経ていないため
推測にとどめる（侍の修正後、忍者の再実機確認で最終確認されたい）。
