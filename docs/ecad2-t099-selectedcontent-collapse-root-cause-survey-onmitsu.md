# T-099根本原因調査: 配置ツールバー2段目SelectedContent潰れ現象(隠密)

調査日: 2026-07-17　調査者: 隠密（key=1784276632853）　依頼元: 家老（殿直接指示、侍調査引き継ぎ）

## 依頼内容(DoD)

根本原因の特定(または有力仮説の絞り込み)、対処方針の提示。侍の対症療法(メニュー開閉トリック)を採用する場合の副作用解消策があれば併せて提案。

## 結論(先出し)

1. **確定事実**：`DockingManager.IsVirtualizingAnchorable`(既定`true`、ecad2は明示未設定)が`TabControlEx`のコンストラクタへそのまま伝播し、`_IsVirtualizing=true`となる。この状態では`TabControlEx.OnApplyTemplate()`が`ItemsHolderPanel`関連の初期化を全てスキップ(早期return)し、標準WPF `TabControl`の`SelectedContent`/`ContentPresenter(ContentSource="SelectedContent")`メカニズムのみに委ねられる（一次ソース`TabControlEx.cs`・`DockingManager.cs`で確認）。侍の仮説(3)の経路自体は事実だが、これは「バグ」ではなく設計上の意図的な分岐（仮想化ON時はTabControlEx独自ロジックを使わない）と解釈するのが正確。
2. **有力仮説**：本質的な原因は`TabControlEx`固有ではなく、標準WPFの`ContentPresenter(ContentSource="SelectedContent")`の初期レイアウトタイミング問題と推定する。`Popup`を開く操作（メニュー操作を含む）はWPFの`Dispatcher`にネストしたメッセージループを発生させ、保留中のレイアウトパスを強制フラッシュする副次効果を持つため、これが回復トリガーになったと考えられる。増分7のMenuItemテンプレート派生固有の問題である可能性は低い（既定Aero2メニューも同じ`Popup`機構を持つため）——ただしこの点は**未検証（推測）**であり、切り分け実験を提案する。
3. **対処方針(本命)**：`PlacementToolBarDockingManager`へ`IsVirtualizingAnchorable="False"`を明示設定する。侍のPoCカスタムテンプレートは既定`AnchorablePaneControlStyle`(AvalonDock本体`generic.xaml`)と完全に一致するコピーであり、`TabControlEx`側の仮想化ロジック(`CreateGrid()`によるContentPresenter差し替え)とも構造的に整合するため、この設定変更のみで根本解決が期待できる。

---

## 1. 一次ソース調査：`TabControlEx`の仮想化分岐

出典: [Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ、`source/Components/AvalonDock/Controls/TabControlEx.cs`（2026-07-17取得、全文241行精読）。

```csharp
[TemplatePart(Name = "PART_ItemsHolder", Type = typeof(Panel))]
public class TabControlEx : TabControl
{
    private Panel ItemsHolderPanel = null;
    private readonly bool _IsVirtualizing;

    public TabControlEx(bool isVirtualizing) : this()
    {
        _IsVirtualizing = isVirtualizing;
    }

    protected TabControlEx() : base()
    {
        _IsVirtualizing = true;
        // This is necessary so that we get the initial databound selected item
        ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // Code below is required only if virtualization is turned ON
        if (_IsVirtualizing)
            return;
        ItemsHolderPanel = CreateGrid();
        var topGrid = (Grid)GetVisualChild(0);
        if (topGrid?.Children?.Count > 2)
        {
            if (topGrid.Children[1] is Border border1) border1.Child = ItemsHolderPanel;
            else if (topGrid.Children[2] is Border border2) border2.Child = ItemsHolderPanel;
        }
        UpdateSelectedItem();
    }

    private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
    {
        if (this.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
        {
            this.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
            UpdateSelectedItem();
        }
    }

    private void UpdateSelectedItem()
    {
        if (ItemsHolderPanel == null)
            return;   // ← _IsVirtualizing=true の間は常にnullのまま、ここで無条件return
        ...
    }
}
```

C#のコンストラクタチェーン規則上、`TabControlEx(bool isVirtualizing) : this()`は**先に引数なしコンストラクタ`protected TabControlEx()`を完全実行**（`_IsVirtualizing=true`が一旦セットされ、`ItemContainerGenerator.StatusChanged`イベント購読も行われる）してから、`_IsVirtualizing = isVirtualizing;`で最終的な値に上書きする。

`LayoutAnchorablePaneControl`（`TabControlEx`を継承、`source/Components/AvalonDock/Controls/LayoutAnchorablePaneControl.cs`26-39行）は`internal LayoutAnchorablePaneControl(LayoutAnchorablePane model, bool IsVirtualizing) : base(IsVirtualizing)`という形でこの値を受け取る。呼び出し元は`DockingManager.cs`1896行:

```csharp
var templateModelView = new LayoutAnchorablePaneControl(model as LayoutAnchorablePane, IsVirtualizingAnchorable);
```

`DockingManager.IsVirtualizingAnchorable`は`DockingManager.cs`1603行で`public bool IsVirtualizingAnchorable { get; set; }`と定義され、コンストラクタ(271行)で`IsVirtualizingAnchorable = true;`と既定trueに初期化される。ecad2側の`MainWindow.xaml`をGrep確認したところ、`IsVirtualizingAnchorable`への言及は**皆無**（`PlacementToolBarDockingManager`含む全4 DockingManagerとも明示未設定）。よって既定`true`のまま動作している。

**結論**：`_IsVirtualizing=true`のため、`TabControlEx`独自の`ItemsHolderPanel`仮想化ロジック（コンテンツをContentPresenterとして事前生成しVisibility切替する方式）は完全にバイパスされ、標準WPF `TabControl`の`SelectedContent`公開プロパティ（`ContentPresenter x:Name="PART_SelectedContentHost" ContentSource="SelectedContent"`）に描画が委ねられる。

## 2. 侍PoCテンプレートと既定テンプレートの一致確認

出典: `source/Components/AvalonDock/Themes/generic.xaml`（2026-07-17取得）174-220行、`x:Key="AnchorablePaneControlStyle"`。

侍がPoCコメントで「既定`AnchorablePaneControlStyle`をコピー」と申告した内容を実際に突合したところ、`Grid`構造（`RowDefinitions`=`*`/`Auto`）・`Border x:Name="ContentPanel"`（`Grid.Row="0"`、`Children[1]`相当の位置）・`ContentPresenter x:Name="PART_SelectedContentHost"`・`AnchorablePaneTabPanel x:Name="HeaderPanel"`（`Grid.Row="1"`）・`IsEnabled=false`時のTrigger、いずれも**構造上完全に一致**（DataTrigger追加のみが差分）。コピー自体に誤りはない。

この一致は、`TabControlEx.OnApplyTemplate()`内の`topGrid.Children[1] is Border`という構造依存チェックとも整合する——侍のPoCテンプレートの`Children[1]`は実際に`Border x:Name="ContentPanel"`であり、もし`IsVirtualizingAnchorable=False`に切り替えても、`CreateGrid()`で生成された`ItemsHolderPanel`が正しく`ContentPanel.Child`へ差し込まれる構造になっている。

## 3. 有力仮説：Popup生成による保留中レイアウトの強制フラッシュ

侍の観測(5)「単純な`UpdateLayout()`・Theme再適用の複数パターンでは再現せず、`IsSubmenuOpen`のtrue→falseのみ効果があった」を踏まえると、これは`TabControlEx`固有のロジック不備というより、**WPFの`Popup`/`PresentationSource`生成に伴う既知の副次効果**である可能性が高いと推定する。

`Popup.IsOpen=true`は新しい`PresentationSource`（別ウィンドウ相当）の生成を伴い、この過程でWPFの`Dispatcher`は`PushFrame`によるネストしたメッセージループを発生させる。この種の操作は、保留中（`DispatcherPriority.Loaded`〜`Render`で溜まっていた）の測定・配置要求を副次的に強制実行させることがWPFでは知られている（`ComboBox`や`Popup`等、独自`PresentationSource`を生成するコントロールの開閉が、無関係に見える別要素のレイアウト崩れを回復させる、という報告は複数のWPFコミュニティで見られる一般的な経験則）。単純な`UpdateLayout()`は**現在のVisual Treeに対する測定・配置のみ**を実行するため、もし問題が「初期化がそもそも行われていない」（`ContentPresenter`が`Content`変更を検知できていない、または`InvalidateMeasure`要求自体が発行されていない）状態であれば、`UpdateLayout()`では再現しないという侍の観測と整合する。

**増分7との関連について**：増分7で新設した`TopLevelHeaderTemplateKey`派生テンプレートも既定Aero2テンプレートも、いずれも`<Popup x:Name="PART_Popup" ...>`を持つ点は共通しており（`docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`で確認済みの構造）、「メニュー操作＝Popup生成」という効果自体は増分7固有ではないと推定する（**推測**）。ただし、増分7のテンプレートは新規ブラシ7種の`DynamicResource`解決を伴うため、リソース解決負荷がわずかに増えている可能性はあり、完全な無関係とは言い切れない。**この点は未検証**——検証方法として、コミット`ec0707a`（増分7打ち切り時点、既定Aero2メニュー）のビルドで同じPoCカスタムテンプレートを試し、同じ現象が再現するかどうかを侍・忍者に確認いただくことを提案する。

## 4. 対処方針の提案

### 本命：`IsVirtualizingAnchorable="False"`の明示設定

```xml
<avalonDock:DockingManager x:Name="PlacementToolBarDockingManager" Grid.Row="1"
                           AnchorablePaneControlStyle="{StaticResource PlacementToolBarPaneControlStyle}"
                           IsVirtualizingAnchorable="False">
```

これにより`TabControlEx`独自の`ItemsHolderPanel`方式が有効化され、`ItemContainerGenerator.StatusChanged`（コンストラクタで購読済み、現状は`_IsVirtualizing=true`のため実質無効）が実際に機能するようになる。コンテナ生成完了（`GeneratorStatus.ContainersGenerated`）のタイミングで確実に`UpdateSelectedItem()`が呼ばれ、初期選択コンテンツが正しく`ItemsHolderPanel`へ追加・表示される設計であるため、Window初期表示時のタイミング問題そのものを回避できると期待する。

**副作用の見積もり**：`IsVirtualizingAnchorable=False`は「タブ切替時に非表示コンテンツを都度破棄せず、全タブ分のContentPresenterを保持し続ける」方式になるため、タブ数が多い場合はメモリ使用量が増える。ただし配置ツールバー2段目は現状「配置ツール」1タブのみのため、実害はまず考えられない（**推測**、実装後に侍が確認可能）。

**検証手順の提案**：(1)侍が上記1行を追加 (2)対症療法コード（`MainWindow.xaml.cs`のDispatcher.BeginInvoke部分）を一旦コメントアウトまたは削除した状態でビルド (3)忍者が起動直後の実機確認（Background=Magenta診断法の再利用、または通常確認）。もしこれで解消すれば、対症療法自体が不要になり、メニューハイライト残留の副作用問題も同時に解消する。

### 副次提案：対症療法を維持する場合の副作用解消策

仮に上記が効かず対症療法（メニュー開閉トリック）を維持する場合、`Keyboard.ClearFocus()`だけでは`MenuItem.IsHighlighted`（内部的にはマウスオーバーや`Menu`の`IsMenuMode`相当の状態で管理され、キーボードフォーカスとは別系統）が解除されない可能性が高い。対処案として、`firstMenuItem.IsSubmenuOpen = false;`の直後に、メニュー以外の既存コントロール（例：キャンバス`CanvasArea`）へ明示的に`Keyboard.Focus(CanvasArea)`することで、`Menu`自体のインタラクション状態を確実にリセットすることを提案する（**未検証、侍による実装・忍者確認が必要**）。ただしこの案はあくまで次善手段であり、本命の`IsVirtualizingAnchorable=False`案で対症療法自体を撤去できるなら、その方が望ましい。

## 5. 不明点

- 他の3つのDockingManager（LeftPalette/RightPanel/OutputPanel）でも同じ「起動直後のSelectedContent潰れ」現象が起きているかは未確認。これらは`AnchorablePaneControlStyle`をローカル値で上書きしておらず、増分1層B/T-058で導入したVS2013テーマ（`Dirkster.AvalonDock.Themes.VS2013`）側の`Theme`プロパティ経由でスタイルが決まる構造のため、今回問題が起きた`PlacementToolBarDockingManager`（唯一`AnchorablePaneControlStyle`をローカル値で明示設定）と初期化経路が異なる可能性がある（**推測**）。他の3パネルでも本当に問題が起きていないかどうかは、`IsVirtualizingAnchorable`の既定値（true）自体は共通のため、理論的には同じリスクを抱えているはずである。侍・忍者への確認を推奨する。
- Popup生成がレイアウトフラッシュを誘発する正確なWPF内部メカニズム（`Dispatcher.PushFrame`〜`ContextLayoutManager`の相互作用）は、一次ソース（`PresentationCore`/`PresentationFramework`内部実装）までは踏み込んでおらず、一般的な経験則からの推定に留まる（**推測**）。
- 増分7のMenuItemテンプレート派生との関連有無は、既定Aero2メニューでの再現テストによる切り分けを提案するのみで、実際の実験は行っていない（スコープ外、実装・実機確認は侍・忍者マター）。

## 派生提案の有無

範囲外の新規作業提案なし。

---

## 出典

- [Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ：
  - `source/Components/AvalonDock/Controls/TabControlEx.cs`（2026-07-17取得、全文241行）
  - `source/Components/AvalonDock/Controls/LayoutAnchorablePaneControl.cs`（2026-07-17取得、全文76行）
  - `source/Components/AvalonDock/DockingManager.cs`（2026-07-17取得、`IsVirtualizingAnchorable`定義1603行・既定値設定271行・使用箇所1896行を確認）
  - `source/Components/AvalonDock/Themes/generic.xaml`（2026-07-17取得、`AnchorablePaneControlStyle`定義174-220行）
- `docs/todo.md` T-099節（侍引き継ぎ材料5点、行57-75）
- アプリ側コード（2026-07-17実測）：作業ツリー未コミット差分`git diff -- src/Ecad2.App/MainWindow.xaml src/Ecad2.App/MainWindow.xaml.cs`（侍PoCカスタムテンプレート・対症療法コード）
- `docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`（増分7 MenuItemテンプレート構造、自己参照）
