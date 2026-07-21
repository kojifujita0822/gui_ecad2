# T-110 増分1（骨格統合）着手前チェック（隠密）

作成日: 2026-07-22　作成者: 隠密　用途: 増分1実装（侍）の参照・隠密静的レビュー時の1対1突き合わせ台
（`onmitsu.md`「着手前チェックとの1対1突き合わせ」制度に基づく。各項目は増分0までに確定した
事項・検出済みの罠のみを列挙し、新規の設計論点は含まない）

## A. 確定方式の反映（裁定・増分0確定事項）

- A-1: トポロジ=案1（キャンバス=`LayoutDocumentPane`+`LayoutDocument`、`CanClose="False"`）。
  隠密プラン§2.2の骨子どおり（縦: ツールバー/横: 220+Doc+280縦2分割/出力160）
- A-2: ドキュメントタブ非表示=**`ShowHeader="False"`方式**（`LayoutDocumentPane`の公開プロパティ。
  `DocumentPaneControlStyle`のテンプレートコピーは**使わない**——PR-21 3例目(f)バグの教訓、
  `docs/ecad2-t110-poc-review-onmitsu.md`追1）
- A-3: T-099(c)案Y代替=**候補a確定**（標準`Dock()`に任せる）。`ContentDocking`の
  `e.Cancel`+`ResetPlacementToolBarLayoutToDefault`機構は撤去。撤去時は案Y設計書の逆適用として
  孤立参照（`_defaultDockingLayoutXmlByManager`の旧用途等）の残骸が無いか確認
- A-4: 裁4=シート/機器表/プロパティ/出力の4アンカラブルに`CanFloat="False"`。配置ツールバー
  （PlacementToolBar）のみフロート可を維持、MainToolBarタブは従来どおり`CanFloat="False"`
- A-5: 統合PaneControlスタイルに**`Items.Count==1`のTabItem Collapseトリガー必須**
  （増分0修2、一次ソースGeneric.xaml:536-540）
- A-6: 統合タイトルスタイル=`Model.ContentId`分岐方式（増分0(e)実証済み）。ただし忍者申し送り
  2.5（分岐の発火場面がPoC操作範囲で皆無だった）を踏まえ、**本実装で「配置ツールバーが単独
  ドッキングペインになる場面」が実在するかを特定し、分岐の要否自体を実装前に再判断**すること
  （不要と判明すれば削るのが筋、要なら案E忠実形=ドッキング時のみ条件のMultiDataTriggerを推奨）

## B. 永続化・リセット機構の作り直し

- B-1: 保存ファイル単一化（`GetDockingLayoutFileName`4分岐の廃止/縮退、新ファイル名）。
  旧4ファイルは放置（削除しない、rm禁止・裁3=移行ロジック無し）
- B-2: `HasExpectedContent`の期待集合を単一Manager用に再構築、**キャンバスDocumentの
  ContentId（"Canvas"等）を期待集合へ含める**こと（含め忘れると保存スキップ/フォールバックの
  誤発動）
- B-3: `AllDockingManagers`ループ8メソッドの単一化（RegisterDockingContents/
  SerializeDefaultDockingLayouts/ResetDockingLayoutToDefault/LoadDockingLayoutFromFileIfExists/
  SaveDockingLayoutAsDefault/ApplyDockingManagerThemes等）
- B-4: Ctrl+Alt+R/Sクラスハンドラ（155-175行）は変更不要のはず——変更が入っていたら理由を確認
- B-5: タイトル動的更新（`UpdateOutputPanelTitle`/`UpdateRightPanelBottomTitle`）のContentId
  検索先が統合Managerへ正しく切替わっていること・リセット後の再呼出維持

## C. 既存機構の再配線

- C-1: T-103ドロップ枠——`LayoutFloatingWindowControlCreated`が統合Managerでは**全ペインの
  フロートで発火**するため、ContentIdフィルタ必須（裁4でフロート可は配置ツールバーのみに
  なるが、防御としてのフィルタ有無を確認）。オーバーレイの座標取得元の再配線
- C-2: T-104タブナビ——`LayoutAnchorSideControl`暗黙スタイル・
  `DisableFocusOnAutoHideSideItemsControl`の統合Manager全体への適用
- C-3: テーマ適用——`ApplyDockingManagerThemes`単一化、`manager.Resources[typeof(
  AnchorablePaneTitle)]`への統合スタイル登録（出し分け分岐の撤去）
- C-4: GridSplitter4本の撤去とMainContentArea行構成の単純化。**ElementPlacementBar座標変換
  （`PositionPlacementBar`、RootLayoutGrid基準）への影響確認**（コメント875-889行の既知ズレ
  対策が前提とする行構成が変わる）
- C-5: FindBarオーバーレイのDocument内コンテンツへの内包化
- C-6: 統合Managerが`MainContentArea`（`IsMainContentEnabled`）配下に置かれ、無効化継承の
  挙動が維持されること（PR-20 4例目の文脈）

## D. PR-21全数確認（レビュー最重点）

- D-1: 増分1で新設/移設される**全てのテンプレートコピー系スタイル**につき、一次ソースの
  Style本体**全Setter**（既定値Setter+ItemTemplate/ContentTemplate/ItemContainerStyle）と
  トリガー全数の突き合わせを行う（`onmitsu.md`73行改訂版）
- D-2: 特に統合タイトルスタイル（2種→1種へ統合）は、旧2スタイルが持っていたSetter・
  トリガーの和集合が漏れなく引き継がれているか

## E. 忍者申し送りの取り込み（`docs/ecad2-t110-poc-verification-ninja.md` 2.3-2.5）

- E-1: `SelectedContentIndex="1"`が起動時に反映されない件——初期選択タブ（殿裁定=配置ツール）
  の実現手段（XAML任せにせずLoaded後設定等）と、忍者確認項目「起動直後の選択タブ」の明示
- E-2: (d)相当の実機確認基準は「セル/行クリックでのアクティブ化」（列ヘッダ単体クリックは
  アクティブ化しない既知挙動）
- E-3: A-6と同じ（ContentId分岐の要否再判断）

## F. テスト・後始末

- F-1: `T058Increment4LayoutFileNameTests`等レイアウト関連既存テストの追随
- F-2: 旧`ResetPlacementToolBarLayoutToDefault`関連テスト（あれば）の整理
- F-3: DockAsDocument経路の封止検討（レビュー§3残課題——アンカラブルの「タブ付きドキュメント
  としてドッキング」で新規`LayoutDocumentPane`（既定`ShowHeader=true`）が生成されうる。
  `CanDockAsTabbedDocument`等の封止可否は一次ソース確認要、実装困難なら増分1の忍者確認項目へ
  「DockAsDocument操作時の挙動」を含めて実測で判断）

## 出典

- `docs/ecad2-t110-implementation-plan-karo.md`§2増分1・`docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`§2-§4
- `docs/ecad2-t110-poc-review-onmitsu.md`（本文§2-§4・追補追1/追2）・`docs/ecad2-t110-poc-verification-ninja.md`
- `docs-notes/roles/onmitsu.md`73行（2026-07-22改訂版）・`docs-notes/pattern-recurrence-log.md`PR-21
