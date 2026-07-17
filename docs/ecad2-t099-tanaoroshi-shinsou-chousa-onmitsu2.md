# T-099棚卸し＋真相調査: 経緯の総括と「PoC DataTrigger自壊説」(隠密2)

調査日: 2026-07-17　調査者: 隠密2（key=1784285827032）　依頼元: 家老（経緯棚卸し・独立検分）＋殿直接指示「根本的な実装も疑って調査して」

## 依頼内容(DoD)

(a) T-099一連の経緯の要約・時系列整理（今後の参照用）
(b) これまでの調査・判断に見落とし・矛盾がないか独立視点で確認
(c) 対症療法復元という現在の着地の妥当性評価・代替案の有無
(＋殿追加指示) 根本的な実装（PoCテンプレート等）自体を疑って調査

## 結論(先出し)

**根本原因の有力仮説を新たに特定した——「特定不能」を覆せる見込み。**

潰れの真因は、外部環境やAvalonDock/WPFの謎挙動ではなく、**T-099 PoCが追加したDataTrigger
そのもの**（`MainWindow.xaml:151-153`、HeaderPanel=タブストリップを`Visibility="Collapsed"`
化する1トリガー）である公算が極めて高い。因果の鎖は次の5段で、全て一次ソースで裏取りした：

1. PoCのDataTriggerはドッキング時（＝起動時の常態）に`HeaderPanel`（`AnchorablePaneTabPanel`、
   `IsItemsHost="true"`）を`Collapsed`にする
2. WPFでは`Collapsed`要素の`Measure()`は**MeasureCoreを呼ばず早期return**し、子孫は一切
   測定されない（dotnet/wpf `UIElement.cs` Measure内、コメント「if Collapsed, we should not
   Measure」の分岐で確認）
3. ItemsHostパネルのアイテムコンテナ（TabItem）生成は、**パネルのMeasure/Arrange時の
   `InternalChildren`初回アクセスで初めて実行される**（dotnet/wpf `Panel.cs`：
   `InternalChildren` getter→`EnsureGenerator()`→`GenerateChildren()`）。パネルが測定され
   なければ**コンテナは永遠に生成されない**
4. `TabControl`の初期選択（`SelectedIndex=0`）とSelectedContent解決は、**コンテナ生成完了が
   前提**（dotnet/wpf `TabControl.cs`：`OnGeneratorStatusChanged`が`ContainersGenerated`時に
   初めて`SelectedIndex=0`を設定。`UpdateSelectedContent()`は`SelectedIndex<0`なら
   `SelectedContent=null`で即return。`GetSelectedTabItem()`はコンテナ実体を要求）。さらに
   AvalonDockの`LayoutAnchorablePaneControl`は**ItemsSourceしかバインドしない**
   （`LayoutAnchorablePaneControl.cs`コンストラクタ、SelectedIndex/SelectedItemのバインド
   なし）ため、選択を成立させる経路はTabItemコンテナの`IsSelected` TwoWayバインディング
   （ItemContainerStyle内）**のみ**＝コンテナ不在なら選択は永遠に確立しない
5. よって`SelectedContent=null`のまま→`PART_SelectedContentHost`は空を描画→**配置ツールバー
   2段目全体が潰れる**。外部トリガー（UIA FindAll・メニュー開閉）が何らかの経路でコンテナ生成を
   誘発した瞬間、`StatusChanged`→初期選択→SelectedContent解決が走り、以後恒久的に回復する
   （コンテナは一度生成されれば残るため「一度直れば直りっぱなし」という観測とも一致）

**本説はこれまでの全実測結果と無矛盾**（後述の突合表参照）。特に「WM_SIZE自然発生でも直らない」
「`IsVirtualizingAnchorable=False`でも直らない」「アプリ内AutomationPeer走査では直らない」
という3つの不発すべてを単一の原因で説明できる。

**位置づけ**：一次ソース整合・全実測無矛盾だが、実機での直接確証（下記の5分診断）は未実施の
ため「有力仮説」に留める。確証手順は安価（後述）。

---

## (a) 経緯の時系列整理（2026-07-17、全て同日）

1. **起票**（殿直接指示）：ドッキング時ドックタグ非表示／フロート時表示＋幅動的調整。
   対象=配置ツールバー2段目のみ
2. **侍PoC**：AvalonDock既定`AnchorablePaneControlStyle`をコピーし、HeaderPanelを
   ドッキング時Collapsed化するDataTriggerを追加→**実機で2段目全体が幅ほぼゼロに潰れる**
   （殿確認）。Grid調整でも解消せず保留
3. **侍の対症療法発見**：起動時にファイルメニューを一瞬開閉すると表示回復（副作用=メニュー
   ハイライト残留）。診断でBackground=Magenta化により「潰れているのは
   `ContentPresenter(ContentSource="SelectedContent")`のみ」と特定
4. **隠密調査1**（selectedcontent-collapse調査書）：`IsVirtualizingAnchorable`既定trueで
   `TabControlEx`の独自初期化がスキップされる構造を一次ソース確認。コピー忠実性も突合し
   「構造完全一致・コピーに誤りなし」。本命案=`IsVirtualizingAnchorable="False"`
5. **侍検証**：False設定でも**クリーンな観測（Start-Process直叩き・UIA不使用）では潰れたまま**
   →従来の「直った」はUIA FindAll等が偶然直していた**観測者効果**と判明。アプリ内
   AutomationPeer明示走査（CreatePeerForElement→GetChildren再帰）も不発
6. **隠密調査2**（uiautomationcore-trigger調査書）：家老のPushFrame仮説を一次ソースで却下、
   代わりに`HwndSource.Process_WM_SIZE`の強制Measure理論を提示。対処案=自己MoveWindow/
   SetWindowPos
7. **侍検証**：自己MoveWindow（無変化）・SetWindowPos+SWP_FRAMECHANGED**とも不発**
8. **診断ログ実測**（侍、WndProcフック+LayoutUpdated計装）：起動626msに**WM_SIZE自然発生
   するもActualHeight=18のまま**。85782msの**UIA FindAll直後にActualHeight=84へ正常化、
   その間WM_SIZEは一度もなし**→WM_SIZE理論を実測で反証
9. **隠密調査3・最終ラウンド**（elementproxy-final調査書、殿裁定=これで決着なくば打ち切り）：
   `ElementProxy.cs`全549行精読、コールバック実装にレイアウト強制コードなし。手がかり=
   `ElementUtil.Invoke`の`DispatcherPriority.Send`同期マーシャリングのみ。
   **結論「文書化されておらず特定不能」**
10. **打ち切り確定**：対症療法（メニュー開閉）復元＋副作用対策（`Keyboard.Focus(LadderCanvasHost)`）
    で仕上げ（コミット53aab52、現HEAD）。T-099本来の要件実装へ進む方針
11. **本調査**（隠密2、殿指示「根本的な実装も疑え」）：**PoC DataTrigger自壊説を特定**（本書）

## 新説の技術的根拠（一次ソース引用）

### 根拠1: Collapsed要素はMeasureされない

dotnet/wpf `PresentationCore/System/Windows/UIElement.cs`、`Measure()`内：

```csharp
//if Collapsed, we should not Measure, keep dirty bit but remove request
if (this.Visibility == Visibility.Collapsed || ...)
{
    ...
    return;   // MeasureCoreへ到達しない＝子孫は測定されない
}
```

### 根拠2: コンテナ生成はItemsHostパネルの初回レイアウトで起きる

dotnet/wpf `PresentationFramework/System/Windows/Controls/Panel.cs`：

```csharp
protected internal UIElementCollection InternalChildren
{
    get
    {
        VerifyBoundState();
        if (IsItemsHost) { EnsureGenerator(); }   // ←ここで初めて生成
        ...
    }
}
internal void EnsureGenerator()
{
    if (_itemContainerGenerator == null)
    {
        ConnectToGenerator();
        EnsureEmptyChildren(null);
        GenerateChildren();   // TabItemコンテナを実際に生成
    }
}
```

`InternalChildren`の実質的なアクセス元はパネルのMeasureOverride/ArrangeOverride。
パネルがCollapsedで一度も測定されなければ、この経路は発火しない。

### 根拠3: TabControlの初期選択・SelectedContent解決はコンテナ生成が前提

dotnet/wpf `PresentationFramework/System/Windows/Controls/TabControl.cs`：

```csharp
private void OnGeneratorStatusChanged(object sender, EventArgs e)
{
    if (ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
    {
        if (HasItems && _selectedItems.Count == 0)
            SetCurrentValueInternal(SelectedIndexProperty, 0);   // 初期選択はここでのみ
        UpdateSelectedContent();
    }
}
private void UpdateSelectedContent()
{
    if (SelectedIndex < 0)
    {
        SelectedContent = null;   // 選択未確立なら中身は空
        ...
        return;
    }
    TabItem tabItem = GetSelectedTabItem();   // コンテナ実体を要求
    ...
}
```

### 根拠4: AvalonDock側に選択を救う別経路はない

`LayoutAnchorablePaneControl.cs`コンストラクタのバインドは`ItemsSource`（Model.Children）と
`FlowDirection`の2本のみ。SelectedIndex/SelectedItemはバインドされない。選択確立は
ItemContainerStyleの`<Setter Property="IsSelected" Value="{Binding IsSelected, Mode=TwoWay}"/>`
＝**TabItemコンテナ経由のみ**。また`TabControlEx.UpdateSelectedItem()`（仮想化OFF時の独自
経路）も`GetSelectedTabItem()`を呼ぶためコンテナ不在では同様に無力（`TabControlEx.cs`）。

### 根拠5: 事前状態（VS2013テーマ）との差分

VS2013テーマ（`AvalonDock.Themes.VS2013/Themes/Generic.xaml`）の
`AvalonDockThemeVs2013AnchorablePaneControlStyle`にはHeaderPanelを潰すトリガーは存在しない
（タブは常時表示——だからこそ殿の「タグを消したい」要望が生まれた）。PoC以前に2段目が正常
だったのは、HeaderPanelが可視でコンテナ生成が通常どおり走っていたため。他3つの
DockingManager（左パレット/出力/右パネル）が無事な理由も同じ（VS2013スタイルのまま）。

## 全実測結果との突合（本説の説明力）

| # | 実測事実 | 本説での説明 |
|---|---|---|
| 1 | PoC適用の起動直後から潰れる | DataTriggerがテンプレート適用時に即Collapsed→生成ブロック |
| 2 | `UpdateLayout()`・Theme再適用で直らない | Collapsedパネルはレイアウトパスから完全除外。保留中レイアウトなど最初から無い |
| 3 | WM_SIZE自然発生（626ms）でも直らない | ルート強制MeasureもCollapsed部分木はスキップ（根拠1） |
| 4 | 自己MoveWindow/SetWindowPos不発 | 同上（そもそも標的が違った） |
| 5 | `IsVirtualizingAnchorable=False`でも直らない | 独自経路`UpdateSelectedItem()`もコンテナ実体を要求（根拠4） |
| 6 | アプリ内AutomationPeer走査（GetChildren再帰）不発 | ビジュアルツリー走査は`InternalChildren`（生成トリガー）を経由しない。未生成コンテナはビジュアルツリーに存在しない |
| 7 | 外部UIA FindAllで回復・以後恒久 | UIAのアイテム系Peer機構がコンテナ実現(realization)を誘発（正確なチャネルは未解明=下記不明点）。生成後はStatusChanged→初期選択→解決。コンテナは残るため恒久回復 |
| 8 | メニュー開閉で回復・以後恒久 | 同上（チャネル未解明だが、回復＝生成完了の帰結という状態遷移は同一） |
| 9 | ElementProxy全549行にレイアウト強制コードなし | 本説と整合——回復機構はレイアウト強制ではなく**コンテナ生成**であり、探していた場所が違った |
| 10 | PoC以前は正常・他3パネルも正常 | 根拠5 |

補足の未解明点：潰れ時のActualHeight=18.00の内訳（空Presenter+境界要素の残高と推測、未確認）。

## (b) 既存調査・判断の検分結果

- **正しかった点**：調査書1のコピー忠実性突合（本調査で改めてAvalonDock本体generic.xamlを
  実取得し再確認、ItemContainerStyle・Items.Count=1トリガー含め完全一致）。調査書2の
  `Process_WM_SIZE`読解自体（コードの事実として正確）。侍の診断ログ設計と「WM_SIZE理論の
  反証」判断（測定系は健全、解釈も正当）。調査書3の「ElementProxyにレイアウト強制なし」
  （本説の傍証にすらなっている）
- **構造的な盲点（本件の教訓）**：3周の調査すべてが「**なぜ外部トリガーで直るのか**」
  （回復側の機構）を追い、「**なぜ最初から表示されないのか**」（発生側の機構）を
  一次ソースで追った周が一度もなかった。PoC差分の検分も「既定テンプレートと一致するか」
  （コピー忠実性）で終わり、**追加した唯一の差分＝DataTrigger自体の副作用**（ItemsHostを
  Collapsedにすることの意味）は検討されなかった。「差分が小さい＝無害」という暗黙の前提が
  調査の出発点をフレームワーク側へ固定してしまった形
- **矛盾の指摘**：調査書1の「Popupによる保留中レイアウトの強制フラッシュ」仮説と、実測2
  （UpdateLayoutで直らない）・実測3（WM_SIZEで直らない）は本来両立しない（保留中レイアウトが
  本質なら、これらでも直るはず）。当時この不整合は明示的に検討されなかった

## 確証手順の提案（安価、侍マター）

1. **5分診断**：`ContentRendered`時に配置ツールバー2段目のpane control（ビジュアルツリーから
   `LayoutAnchorablePaneControl`取得）の`ItemContainerGenerator.Status`・
   `ContainerFromIndex(0)`・`SelectedIndex`・`SelectedContent`をログ出力。
   **予測**＝潰れ中はStatus未完了・コンテナnull・SelectedIndex=-1・SelectedContent=null、
   対症療法発火後はすべて解決済みに転じる。この予測が外れれば本説は棄却
2. **即効テスト**：DataTrigger（`MainWindow.xaml:149-153`）を一時除去し、対症療法コードも
   無効化した状態でクリーン観測起動→潰れが消えれば確定

## (c) 現在の着地の評価と対処案

- **対症療法復元（現HEAD 53aab52）自体は実用上機能しており、当時の情報下では妥当な判断**。
  ただし本説が確証されれば、**より小さく綺麗な根本修正が可能**であり再検討の価値がある
- **根本修正案（本命）**：HeaderPanel（ItemsHost）を潰すのをやめ、**TabItemコンテナ側の
  Visibilityで制御する**。ItemContainerStyleの既存`Items.Count=1→Collapsed`トリガーを、
  「`{Binding Model.IsDirectlyHostedInFloatingWindow, RelativeSource={RelativeSource
  AncestorType={x:Type avalonDock:LayoutAnchorablePaneControl}}}`がFalse（ドッキング時）→
  TabItem Collapsed」のDataTriggerへ**置き換える**（両トリガー併存だとフロート時もCount=1で
  潰れたままになるため置換が必須）。コンテナ生成はパネルのレイアウトで走り、コンテナ自身の
  Collapsedは生成を妨げない（根拠2・3）ため、同型バグは再発しない。HeaderPanelの
  `Margin="2,0,2,2"`由来の残余2pxが気になる場合のみ、Marginを条件で0にするSetterを追加
  （MarginはVisibilityと違い測定を妨げない）
- 根本修正が効けば、**対症療法コード（メニュー開閉+Keyboard.Focus）は全撤去可能**。
  `IsVirtualizingAnchorable="False"`も不要になる見込み（既定trueへ戻して差分最小化を推奨、
  実害はないため侍・家老判断）。加えてT-099本来の要件（幅動的フィット）・T-100の2段目
  未検証分・新規発見6の検証も順次アンブロックされる
- **UI/UX留意（殿確認事項）**：本命案ではフロート時にタブが表示される（要件2どおり）。
  フロートウィンドウ自体のタイトルバーとタブが二重表示に見える可能性があるため、実機で
  見え方の確認を推奨

## 不明点

- 外部UIA FindAll・メニュー開閉が**コンテナ生成を誘発する正確なチャネル**は未解明のまま
  （UIAのItemsControl系Peer/realization機構またはメニューモードのKeyboardNavigation走査と
  推測。ただし本説の確証・修正には不要な副次論点であり、調査書3の打ち切り判断を維持してよい）
- 潰れ時ActualHeight=18.00の正確な内訳（未確認、実害なし）
- DataTrigger除去後にフロート⇔ドッキング遷移でVisibilityが正しく追従するか（実機確認要）

## 派生提案・気づき

1. **ItemContainerStyleの選択タブ`Background="White"`固定**（`MainWindow.xaml:183`、
   AvalonDock既定のコピー由来）：フロート時にタブが可視化されるとダークモードで白浮きする
   見込み。根本修正の実装時に併せてDynamicResource化を検討（軽微）
2. **台帳パターン候補**：「新規実装直後に発生した不具合の調査が、直近に追加した差分自体の
   副作用検討を飛ばし、フレームワーク・環境側の機構解明へ向かう」型。本件（3周）とT-100の
   標的取り違え（8650a66）に共通の匂いがある。記帳要否は家老の判断を仰ぐ

---

## 出典

- ecad2実装（2026-07-17時点HEAD=53aab52）：`src/Ecad2.App/MainWindow.xaml` 105-225行
  （PoCスタイル全体、DataTrigger=151-153行、Items.Count=1トリガー=204-206行、
  ItemTemplate/ContentTemplate=211-224行）、695-697行（DockingManager設定）、
  `src/Ecad2.App/MainWindow.xaml.cs` 189-209行（対症療法）
- [Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) master（package v4.74.1使用、
  2026-07-17取得）：`Themes/generic.xaml`（AnchorablePaneControlStyle、コピー忠実性の再確認）、
  `Controls/LayoutAnchorablePaneControl.cs`（コンストラクタのバインド2本のみ）、
  `Controls/TabControlEx.cs`（UpdateSelectedItem/CreateChildContentPresenter/
  OnSelectionChanged/OnItemsChanged）、
  `AvalonDock.Themes.VS2013/Themes/Generic.xaml`（AvalonDockThemeVs2013AnchorablePaneControlStyle）
- [dotnet/wpf](https://github.com/dotnet/wpf) main（2026-07-17取得）：
  `PresentationCore/System/Windows/UIElement.cs`（Measure内Collapsed早期return）、
  `PresentationFramework/System/Windows/Controls/Panel.cs`（InternalChildren/EnsureGenerator/
  GenerateChildren）、
  `PresentationFramework/System/Windows/Controls/TabControl.cs`（OnGeneratorStatusChanged/
  UpdateSelectedContent/GetSelectedTabItem）
- `docs/todo.md` T-099節（経緯全量）・T-100節（関連事実）
- 隠密の先行調査書3通：`docs/ecad2-t099-selectedcontent-collapse-root-cause-survey-onmitsu.md`・
  `docs/ecad2-t099-uiautomationcore-trigger-survey-onmitsu.md`・
  `docs/ecad2-t099-elementproxy-final-survey-onmitsu.md`
