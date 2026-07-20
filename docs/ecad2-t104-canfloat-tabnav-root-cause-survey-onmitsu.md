# T-104増分1 DoD(2)(4) NG原因調査（緊急、家老委譲）

調査日: 2026-07-20　調査者: 隠密
対象: `docs/ecad2-t104-increment1-poc-verification-ninja.md`のNG項目(2)(4)

---

## 結論（先出し）

**(2)は「CanFloat="False"の実装不備」ではなく「検証対象の取り違え」と、一次ソース(Dirkster99/
AvalonDock `master`)の完全な裏付けをもって断定できる。** ただし副次的に、設計書§3が想定して
いなかった新しい副作用（下記「気づき」）があり、増分2着手前に要検討。

(4)は原因未確定。既存XAML設定（T-104非依存）から手がかりを示すに留め、確定にはさらなる調査
（AnchorablePaneTabPanel一次ソード、または侍への診断ログ計装依頼）を要する。

---

## (2) CanFloat="False"が効かない件

### 一次ソース調査結果

`https://raw.githubusercontent.com/Dirkster99/AvalonDock/master/source/Components/AvalonDock/`
配下、`DockingManager.cs`(3324行)・`Controls/LayoutItem.cs`(914行)・
`Controls/LayoutAnchorableTabItem.cs`(180行)を全文取得・精読。

**メニュー「フローティング」項目のCanExecute** (`LayoutItem.cs:365`):
```csharp
private bool CanExecuteFloatCommand(object anchorable) => LayoutElement != null && LayoutElement.CanFloat && LayoutElement.FindParent<LayoutFloatingWindow>() == null;
```
→ **そのタブ自身の`LayoutElement.CanFloat`のみ**をチェックする。「配置ツール」の
`LayoutElement.CanFloat`は未設定＝既定`True`のため`CanExecuteFloatCommand`は`true`を返し、
メニュー項目はIsEnabled=Trueになる。**これは正常な挙動**（CanFloat="False"は「基本機能」
タブのみに設定されており、「配置ツール」には一切適用されていない）。

**フロート化の実処理**には経路が2つある：

1. `DockingManager.cs:2011` `StartDraggingFloatingWindowForContent`（タブヘッダー単体の
   ドラッグ、コメント「Executes when the user starts to drag a LayoutAnchorable by dragging
   its TabItem Header」）→ 2015行`if (!contentModel.CanFloat) return;`（**対象コンテンツ単体
   のみ**のチェック）→ `CreateFloatingWindow`→（親がILayoutPaneの場合）`CreateFloatingWindowCore`
   （3228-3230行、ここでも`if (!contentModel.CanFloat) return null;`、単体チェック）
2. `DockingManager.cs:2070` `StartDraggingFloatingWindowForPane`（**ペインタイトル全体**の
   ドラッグ、コメント「Executes when the user starts to drag a docked LayoutAnchorable...by
   dragging its title bar」）→ `CreateFloatingWindowForLayoutAnchorableWithoutParent`
   （3152-3155行）→ `if (paneModel.Children.Any(c => !c.CanFloat)) return null;`
   （**ペイン内Children全員のOR判定、1つでもFalseなら全体拒否**）

侍が先に共有した仮説（ペイン単位=AND条件・コンテンツ単体=対象のみ判定、L2765-2843付近の
手がかり）は、正確な行番号・関数名まで含めて一次ソースで裏付けが取れた。

### 結論

忍者が(2)で再現した操作は「**配置ツールタブ**のヘッダー『メニュー』ボタン→『フローティング』」
（`docs/ecad2-t104-increment1-poc-verification-ninja.md` (2)節参照）。これは経路1（コンテンツ
単体チェック）を通り、「配置ツール」自身のCanFloat（既定True）のみが評価されるため、フロート化
成立は**仕様通り**であり、CanFloat="False"（基本機能タブに設定）とは無関係。侍の指摘（設計書
§4骨子・実装とも「配置ツール」側にはCanFloat設定を追加していない）とも整合する。

**「基本機能」タブ自身に対する検証（同じ経路でのメニュー操作、およびペインタイトル全体の
ドラッグ操作）が未実施**であり、これが本来のCanFloat="False"検証。

---

## 気づき（範囲外、増分2着手前に要検討）

CanFloat="False"の実装自体にバグは無いが、一次ソースの2経路の違いにより、**設計書§3が想定
していなかった副作用**が生じる：

1. **「配置ツール」単独フロート化**: 「配置ツール」はCanFloat制限が無いため、経路1（コンテンツ
   単体）で単独フロート化が可能。これ自体は既存動作（T-099(c)以前から）だが、**2タブ構成に
   なったことで「配置ツールだけが抜け、基本機能だけが単独でペインに残る」という新しい状態が
   生まれる**——設計書§3は「2タブは常に一緒に存在する」前提でリスク評価しており、この片肺
   状態の想定が抜けている可能性がある。忍者(2)節が実際に観測した「基本機能パネルのタブ
   ヘッダー行自体が非表示になった」（単一タブ化に伴うAvalonDock標準動作とみられる）はこの
   帰結。
2. **ペイン全体ドラッグのブロック**: 経路2（ペインタイトル全体、3154行のOR判定）は「基本機能」
   のCanFloat=Falseにより、**ペイン全体のドラッグ操作自体がブロックされる**（未検証だが一次
   ソースからの論理的帰結）。これは「配置ツール」も巻き込まれて、ペインタイトル経由のフロート
   化操作ができなくなることを意味し、設計書は想定していなかった制約と見られる。

いずれも実装のバグではなく「一次ソースの挙動を踏まえた設計の再検討点」であり、増分2着手前に
要確認と考える（隠密は着手せず気づきとして報告のみ）。

## (2)の再検証が必要な項目（忍者への追加依頼を提案）

1. 「基本機能」タブ自身のヘッダーメニュー→「フローティング」を実行し、IsEnabled=False・
   実際にフロート化しないことを確認（これが本来のCanFloat="False"検証）
2. ペインタイトル（上段の帯、`DockedDragHandle`）をドラッグした場合の挙動確認（経路2、
   3154行のOR判定が効き、ペイン全体のドラッグ自体がブロックされるか）

---

## (4) Tab/Shift+Tabナビゲーション非対称性

### 確認した既存XAML設定（`MainWindow.xaml:171-295` `PlacementToolBarPaneControlStyle`）

- 最上位`Grid`（178行）: `KeyboardNavigation.TabNavigation="Local"`
- `ContentPanel`（185-196行、選択中コンテンツ表示領域）: `KeyboardNavigation.TabIndex="2"`
- `HeaderPanel`（197-199行、`AnchorablePaneTabPanel`、タブ項目群）: `KeyboardNavigation.TabIndex="1"`

これらは**T-104で新設された設定ではなく、既存のPlacementToolBarPaneControlStyleの一部**
（T-099要件対応等で以前から存在）。**これまで各AnchorablePaneは単一タブ構成だったため、
「複数TabItem間でのTab移動」という状況自体がT-104で初めて顕在化したケース**であり、既存の
`TabNavigation="Local"`設定が複数タブ構成を想定していなかった可能性がある。

`LayoutAnchorableTabItem.cs`（タブ項目自体のクラス）を全文精読したが、`Focusable`・
`IsTabStop`等のフォーカス関連プロパティのオーバーライドは無く、既定値（`Control`基底クラス
のFocusable=True）のままと見られる。ここには非対称性（Tab前進のみ欠落）を説明する手がかりは
見当たらなかった。

### 未確定

`Local`の正確な子孫探索範囲（直接の子のみか、孫要素以下も含むか）と、`AnchorablePaneTabPanel`
自体（`IsItemsHost="true"`のItemsControlホスト）が内部でTabIndex・KeyboardNavigationをどう
扱うかの一次ソースは未確認（時間配分によりここで一旦区切り）。原因の完全特定には
`AnchorablePaneTabPanel.cs`の一次ソース確認、または侍による診断ログ計装（`FocusManager`の
`GotFocus`/`LostFocus`イベント実測）が近道と考える。

---

## 出典

- `Controls/LayoutItem.cs`・`DockingManager.cs`・`Controls/LayoutAnchorableTabItem.cs`
  （[Dirkster99/AvalonDock](https://github.com/Dirkster99/AvalonDock) `master`ブランチ、
  2026-07-20取得、全文精読）
- `src/Ecad2.App/MainWindow.xaml:171-295`（`PlacementToolBarPaneControlStyle`、現状のXAML）
- `docs/ecad2-t104-increment1-poc-verification-ninja.md`（忍者実機確認記録）
