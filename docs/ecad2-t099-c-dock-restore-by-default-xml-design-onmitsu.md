# T-099(c) 案Y詳細設計: 既定レイアウトXML Deserializeによるドッキング復帰（隠密設計書）

設計日: 2026-07-19　設計者: 隠密　委任元: 家老（殿裁定=案Y正式採用）
実装担当: 侍（本書は設計のみ）

## 0. 方式の骨子

`ContentDocking`イベントハンドラから**モデル手術（再接続・Children.Add・CollectGarbage等）を
全廃**し、`e.Cancel=true`の後は**ハードコード既定レイアウトXMLのDeserialize（単一Manager版）を
1回呼ぶだけ**にする。3周の教訓（AvalonDockの隠れた不変条件との衝突）への根本回答として、
「モデルの整合はAvalonDock自身の正規機構（Layout差し替え）に全部任せる」方針。

有効性の実証: 殿実測「Ctrl+Alt+R（同方式の全Manager版）で復活した」。

## 1. 単一Manager版リセットの構成（家老観点1）

現行`ResetDockingLayoutToDefault()`は「全Managerループ＋saved優先2段＋Title同期2種＋
ステータスメッセージ」の複合。ここから配置ツールバー向けに必要な芯だけを抜き出す。

**新規メソッド（private）**:

```csharp
// T-099(c)案Y: 配置ツールバーのレイアウトをハードコード既定(XAML初期状態の自己Serialize)へ
// 戻す。ContentDockingハンドラからの「確実なドッキング復帰」専用。保存済みファイル
// (TryReadSavedDockingLayoutXml)は意図的に参照しない——ユーザー保存レイアウトがフロート状態
// だった場合、「ドッキングせよ」という操作意図と矛盾する状態を復元してしまうため、
// 本経路は常にドッキング済みの既定状態へ戻す。
private void ResetPlacementToolBarLayoutToDefault()
{
    if (_defaultDockingLayoutXmlByManager.TryGetValue(PlacementToolBarDockingManager, out var defaultXml)
        && TryDeserializeDockingLayout(PlacementToolBarDockingManager, defaultXml))
        return;
    // defaultXmlは起動時の自己Serialize産のため実質失敗しないが、万一失敗しても
    // 何もしない(現状維持=フロートのまま)。モデルを中途半端に触らないことが3周の教訓。
    _viewModel.StatusMessage = "配置ツールバーのドッキングに失敗しました";
}
```

- `TryDeserializeDockingLayout`（既存、HasExpectedContent検証・RebindDockingContent
  コールバック配線済み）を**そのまま再利用**する。Content実体の再バインドも既存機構が担う。
- **saved XMLを使わない理由**（設計判断）: `ResetDockingLayoutToDefault`のsaved優先2段は
  「ユーザーの好みレイアウトの復元」が目的。本経路は「ドッキング操作の実行」が目的であり、
  saved XMLがフロート状態で保存されていた場合に矛盾する。既定XML固定が正しい。
- Title同期（UpdateOutputPanelTitle等）は他パネル向けで配置ツールバーに該当機能なし、不要。

## 2. ContentDockingハンドラの書き換え（家老観点2）

```csharp
private void PlacementToolBarDockingManager_ContentDocking(object? sender, ContentDockingEventArgs e)
{
    if (e.Content is not LayoutAnchorable anchorable || anchorable.ContentId != "PlacementToolBar") return;
    e.Cancel = true;
    ResetPlacementToolBarLayoutToDefault();
}
```

これで全部である。IsSelected/IsActive設定も不要（Deserializeで新規構築されるモデルは
既定状態で表示され、単一コンテンツゆえ選択状態の概念が実質無い）。

**同期呼び出しの安全性**（3周の教訓を踏まえた検証済み事項）:
- `ContentDocking`は`ExecuteDockCommand`内から同期発火し、`e.Cancel=true`なら
  `anchorable.Dock()`を呼ばず即returnする（`DockingManager.cs:2334-2337`）。
  発火元スタックに破棄済みモデルへの後続アクセスは無い（`LayoutAnchorableItem.
  ExecuteDockCommand`も式1行で後続なし）。ゆえにハンドラ内での同期Layout差し替えは安全。
- MenuItemのClickはメニュークローズ後に発火するWPF既定挙動のため、「開いたメニューの
  親ウィンドウをクローズする」衝突も実用上起きない。
- 万一実機で例外・描画不全が出た場合の代替手: `Dispatcher.BeginInvoke(DispatcherPriority.
  Normal)`で`ResetPlacementToolBarLayoutToDefault()`を1テンポ遅延させる（第2手として温存、
  初手では入れない——不要な遅延はタイミング依存の新種バグの温床、これも過去の教訓）。

## 3. 削除してよい範囲・残す範囲（家老観点3）

**削除する（ContentDockingハンドラ内の積み上げ全部）**:
- `PlacementToolBarPane.Parent == null`判定とツリー再接続処理（RootPanel再構築含む）
- `PlacementToolBarPane.Children.Add(anchorable)`
- `anchorable.IsSelected/IsActive`設定
- `Layout.CollectGarbage()`
- `InvalidateArrange()`
- 遅延`UpdateLayout()`・`InvalidateVisual()`（第2段対処で入れたもの）

**併せて削除推奨（ボーイスカウト）**:
- XAMLの`x:Name="PlacementToolBarPane"`と付随コメント——新方式ではコード参照が無くなる上、
  **Deserializeのたびにモデルは新規インスタンスへ置き換わるため、x:Name参照は古い孤立
  インスタンスを指し続ける罠フィールドになる**（残すと将来の誤用リスク）。削除が筋。

**残す（変更しない）**:
- `ContentFloating`ハンドラ（FloatingLeft/TopのDPI補正付き設定）——フロート位置対処は
  モデル手術ではなく独立した正当な機能。無変更で残す。
  - 注意点1つ: Deserializeで生成された**新しい**LayoutAnchorableインスタンスにも
    ContentFloatingは正しく効く（ハンドラはe.Content経由で対象を受け取るため、
    インスタンス固定参照を持たない。現実装のままで整合）。
- `ContentDocking`/`ContentFloating`のイベント購読登録（コンストラクタ）
- 再発防止策（HasExpectedContent・読込/保存側検証）——本設計と独立に有効

## 4. DockAsDocumentの統一（家老観点4）

`ExecuteDockAsDocumentCommand`も同じ`RaiseContentDocking`を通る（`DockingManager.cs:
2340-2348`、一次ソース確認済み）ため、「タブ付きドキュメントとしてドッキング」も本ハンドラで
同一の既定復帰動作になる。前回殿裁定（両項目統一）通り、追加実装は不要で自然に維持される。

## 5. フロートウィンドウの後始末（家老観点5）——抜け漏れなしを一次ソースで確認済み

`TryDeserializeDockingLayout`→`XmlLayoutSerializer.Deserialize`→`Manager.Layout`への
新LayoutRoot設定→**`DockingManager.OnLayoutChanged`（`DockingManager.cs:434-448`）が発動**:

```csharp
foreach (var fwc in _fwList.ToArray())
{
    fwc.KeepContentVisibleOnClose = true;
    fwc.InternalClose();       // 既存フロートウィンドウControlを全てクローズ
}
_fwList.Clear();
// …新Layoutの FloatingWindows から必要分のみ再生成(L479-482)
```

- 既存フロートウィンドウ（実ウィンドウ）は**AvalonDock自身の正規機構で全てクローズ**される。
  既定XMLは`<FloatingWindows />`（空）のため再生成も無い——**幽霊ウィンドウは構造上残らない**。
- `KeepContentVisibleOnClose = true`によりコンテンツ実体（ToolBar）はクローズ時に破棄されず、
  `RebindDockingContent`コールバックが新モデルへ再バインドする。調査5で問題になった
  「ビジュアル親の解放漏れ」もこの正規クローズ経路で解消される。

## 6. 実装規模

- 新規メソッド1個（約10行）＋ハンドラ本体を3行へ縮小＋積み上げコード削除（純減）。
- XAML: x:Name削除（コメント整理含む）。
- テスト: 既存テストへの影響なし見込み（View層のみ）。build/test後、静的レビューへ。

## 7. 忍者実機確認項目（実装後・次回セッション）

1. メニュー「フローティング」→ツールバー近傍にフロート表示（ContentFloating維持確認）。
2. メニュー「ドッキング」→**元の横長位置（569×81相当）へ復帰**、縦長44px・タブ複製・
   空白のいずれも発生しない。フロートウィンドウが画面から消える（幽霊なし）。
3. 「タブ付きドキュメントとしてドッキング」→2と同一動作。
4. Float→Dock反復2〜3周で劣化なし。
5. Dock後にDockingManager配下へDocumentPane残骸が無い（UIAツリー確認）。
6. 終了→再起動でレイアウト正常（再発防止策との複合確認）。
