# T-121回帰疑い（タブヘッダー復活）独立調査（隠密）

家老依頼：忍者所見（`docs/ecad2-t122-verification-ninja.md`「範囲外の重大な所見」）——メニュー経由
フロート化→実ドラッグでの再ドッキング後、常時非表示のはずのタブヘッダー（「基本機能」「配置ツール」
見出し行）が表示されたまま残る、の根本原因を一次ソース（AvalonDockの`Dock()`実装・DataTriggerの
再評価タイミング）で確認。侍への同時依頼（Wチェック方式）につき、独立性を保つため侍側の結論は
参照せず本調査を行った。

## 前提：2種類の異なる「タイトルバー的要素」の区別

ecad2の配置ツールバーペイン（`MainToolBar`＋`PlacementToolBar`の2タブ構成）には、名前が紛らわしいが
**構造的に別物の2要素**が存在する。事実の切り分けが本調査の核心。

| 要素 | 実体 | 表示範囲 | 制御スタイル |
|---|---|---|---|
| ①`Header`（`AnchorablePaneTitle`） | 選択中の1タブの内容部分（`ContentPresenter`側）の上部バー | **選択中の1つのみ**（複数同時表示は構造上不可能） | `TitleBarHiddenAnchorableControlStyle`（`MainWindow.xaml:758`、`LayoutAnchorableControl`用、T-121新設DataTrigger`838-844`行はここ） |
| ②タブストリップ（`HeaderPanel`/`AnchorablePaneTabPanel`） | ペイン下部の複数タブ見出し行（各`LayoutAnchorableTabItem`が並ぶ） | **ペイン内の全タブ分、同時表示可能** | `UnifiedAnchorablePaneControlStyle`の`ItemContainerStyle`（`MainWindow.xaml:398-401`、`Items.Count==1`でCollapse） |

忍者所見は「『基本機能』『配置ツール』の見出し行」＝**両方同時に表示された**と明記している。①は選択中
1タブのみのため両方同時表示は構造上ありえず、**忍者が見たのは②タブストリップである可能性が極めて高い**
（事実からの推論）。

## 一次ソース確認結果

### `Dock()`の実装（`LayoutContent.cs:598-634`、AvalonDock v4.74.1）

```csharp
public void Dock()
{
    if (PreviousContainer != null)
    {
        ...
        previousContainerAsLayoutGroup.InsertChildAt(PreviousContainerIndex, this);
        ...
        IsSelected = true;
        IsActive = true;
    }
    else { InternalDock(); }
    Root.CollectGarbage();
}
```

**事実**：`Dock()`は**モデルツリー操作のみ**（同一`LayoutAnchorable`インスタンスを元の`LayoutAnchorablePane`
（`ILayoutGroup`）の`Children`へ`InsertChildAt`で再挿入するだけ）。Visual（`LayoutAnchorableControl`等）の
生成・破棄はここには一切含まれず、`ItemsSource`バインディング経由のWPF標準コンテナ生成機構に委ねられる
設計。

### タブストリップの`Items.Count==1`判定（`MainWindow.xaml:398-401`、ecad2独自スタイル）

```xml
<DataTrigger Binding="{Binding RelativeSource={RelativeSource Mode=FindAncestor, AncestorType={x:Type TabControl}}, Path=Items.Count}" Value="1">
    <Setter Property="Visibility" Value="Collapsed"/>
</DataTrigger>
```

**事実**：`UnifiedAnchorablePaneControlStyle`の`ItemContainerStyle`（`TabItem`用）に存在。AvalonDock標準
`generic.xaml`にも同型のトリガーが存在する（`Items.Count==1`ならタブ見出し自体をCollapse、単一ペインで
冗長なタブ行を隠す設計、コメント`393-397`行に経緯記載）。

### 事実から導ける仮説（確度：高、ただし未実証）

1. **フロート中**：`PlacementToolBar`が元のペインから離脱し、`MainToolBar`のみ残留（T-122レビューで
   確認済みの`FindVisualChild<LayoutAnchorablePaneControl>`探索コメントから、フロート後もペイン自体は
   Visual Treeに残ることは確定済み）。この時`Items.Count=1`となり、398-401行目のDataTriggerが発火、
   タブストリップ全体がCollapseされる。
2. **`Dock()`実行後**：`PlacementToolBar`が同一インスタンスのまま元のペインへ`InsertChildAt`で戻り、
   `Items.Count=2`に復帰。398-401行目のDataTrigger条件が偽になり、**Visibility Collapsedの指定が自動的に
   解除される＝タブストリップが表示される**。

この一連の流れは、忍者観察の時系列（フロート化→実ドラッグでの再ドッキング「後」に表示される）と
一次ソースの動作原理として完全に整合する。**これはAvalonDock/ecad2標準の「単一タブなら見出しを隠す」
機構が正常に動作した結果であり、`Dock()`自体にバグがあるわけではない**——むしろ`Items.Count`に連動する
設計上、2タブに戻れば見出しが出るのは机上では「意図した動作」に見える。

## 未解消の矛盾点（不明点、推測報告禁止のため明示）

`MainWindow.xaml:477`のT-100由来コメント（T-110増分1で追記）に以下の記述がある：

> 本実装のPlacementToolBarDockingManagerは**常時2タブ(基本機能/配置ツール)構成**のためドッキング中は
> タブUI(UnifiedAnchorablePaneControlStyle)がホストしAnchorablePaneTitle自体は使われず

この「常時2タブ構成」という設計前提が正しいなら、**起動直後（フロート化前）から`Items.Count=2`のはずで、
398-401行目のDataTriggerは発火せず、タブストリップは通常時から表示されているはず**である。

しかし忍者所見は「フロート化前（起動直後〜フロート化直後）：タブヘッダー非表示（スクリーンショット
確認済み）」としており、起動直後の状態も含めて非表示だったと明記している。**この2つの記述は一次ソース
読解だけでは整合しない**。

考えられる説明（いずれも未検証、优先順位なし）：
- (a) 忍者が「起動直後」の非表示として観察していたのは実際には①`Header`（`AnchorablePaneTitle`、T-121
  トリガーにより常時Collapseが正しい仕様）であり、②タブストリップの存在自体には気づいていなかった
  （再ドッキング後に初めて②の存在に気づいた）
- (b) 何らかの理由で起動直後は`Items.Count`が一時的に1（初期化順序の影響等）になっている
- (c) 上記(a)(b)以外の未特定要因

家老・侍の調査結果と突合のうえ、必要であれば忍者へ「起動直後のUIA階層でタブストリップ要素
（`HeaderPanel`/`AnchorablePaneTabPanel`配下の各TabItem）のVisibility・Bounds・Items.Count実値」を
直接確認する追加検証を推奨する。

## 結論（確度を明示）

- **確度高（一次ソースの動作原理と時系列が整合）**：再ドッキング後にタブストリップ（「基本機能」
  「配置ツール」両方の見出し）が表示される現象は、`Dock()`によるモデル再挿入→`Items.Count`が1から2へ
  復帰→ecad2独自の`Items.Count==1`判定トリガー（`MainWindow.xaml:398-401`）が自動的に解除される、という
  一連の標準機構の帰結として説明できる。これは`TitleBarHiddenAnchorableControlStyle`（T-121新設、
  `Header`/`AnchorablePaneTitle`制御）の管轄外であり、**T-121のコード自体が壊れているわけではない**
  可能性が高い。
- **不明点**：「常時2タブ構成」前提（`MainWindow.xaml:477`）と忍者の「起動直後も非表示」観察との矛盾は
  未解消。対処要否・対処方針（例：タブストリップ側にも`ContentId`名指しの常時Collapseトリガーを追加する
  等）は、この矛盾の解消（実機再確認）を経てから判断すべきと考える。

## 気づき（範囲外）

なし。
