# T-110 増分0（PoC）静的レビュー（隠密）

レビュー日: 2026-07-21　レビュー担当: 隠密　委任元: 家老
対象: コミット`96d3164`（`poc/t110-single-dockingmanager-poc/T110SingleDockingManagerPoc/`、7ファイル709行）
レビュー深度: 軽量（家老采配どおり、「実証すべき論点を正しく検証できているか」を主観点とする）

## 結論（先出し）

**要修正2件（修1・修2）を侍へ差し戻し推奨。反映後に忍者実機確認へ進んでよい**。
検証設計の骨格（(a)〜(h)+追加2件のカバレッジ、(c)前提条件の忠実性、VS2013テーマ設定）は良好。
侍の気づき（`ShowHeader`）は**一次ソース裏取りの結果、正しい**——増分1の裁2実装は
テンプレートコピー不要の`ShowHeader="False"`方式へ変更を推奨する（後述、本レビュー最大の収穫）。

## 1. 合格点（確認済み事項）

- **パッケージ・TFM**: `Dirkster.AvalonDock`+`Themes.VS2013`両方4.74.1、`net10.0-windows`で
  本実装と一致（csproj:5,15-16。第2回検証指摘Aの反映確認）。
- **(c)前提条件の忠実性**: 2タブ同居（MainToolBar=`CanFloat="False"`／PlacementToolBar=フロート可）・
  両タブ`CanClose="False"`・`SelectedContentIndex="1"`（MainWindow.xaml:459-473）・**ContentDocking系
  ハンドラ無し**（MainWindow.xaml.cs全読で購読ゼロ確認）。`Float()`/`Dock()`ボタン（82-104行）は
  メニューコマンドと同じ`LayoutContent.Dock()`本体経路を通るため再現手段として妥当。
- **トポロジ(a)**: 隠密プラン§2.2案1の骨子どおり（縦: ツールバー123／横: 220+DocumentPane+280縦2分割／
  出力160、MainWindow.xaml:452-539）。
- **(e)統合タイトルスタイル**: Style本体Setter2本（Background/Foreground）の転記あり（PR-21観点OK、
  24-25行）・T-100ハッチングCollapse維持（68行）・VS2013既定トリガー群（IsAutoHidden回転／CanClose
  コマンド差し替え／IsActive色3種+グリフ）の写し完全（161-182行、一次ソース突き合わせ）。ContentId
  分岐トリガー（188-190行）の設計は検証目的に適合。
- **(h)**: テーマ公認パターン（`LayoutAnchorableControl`テンプレートのHeader Border層Collapse、
  Generic.xaml:1749-1761と同型）の踏襲を確認。T-099の罠（コントロール自体のCollapse）とは階層が
  異なり安全。
- **(d)(g)・AutoHide**: 中身クリック指示のステータス文言（543行）・フォーカス確認用TextBox3本
  （495-497行）・AutoHideピンは統合スタイル内に維持（113-135行）で観察可能。

## 2. 要修正2件（実機確認前に反映推奨）

### 修1: ダークモード時のアプリ側テーマ辞書が差し替わらない

`ApplyTheme()`（MainWindow.xaml.cs:24-30）は`MainDockingManager.Theme`の切替のみで、
**`Application.Current.Resources.MergedDictionaries`のTheme.Light.xaml⇔Theme.Dark.xaml差し替えが
無い**（App.xaml:9はLight固定、Theme.Dark.xamlはどこからも参照されず死蔵）。このため
`PanelHeaderBackgroundBrush`等（UnifiedPaneControlStyleのタブ色、247-248行）がダーク時もLight値の
まま残り、**(e)両テーマ確認と全ペインPaneControl適用検証で「ダークで色が浮く」という偽の副作用を
観察してしまう**。本実装`ApplyUiChromeTheme`（`MainWindow.xaml.cs:796-807`）のミラーとして
Light/Darkハンドラで辞書差し替えを追加されたい。

### 修2: `Items.Count==1`のタブCollapseトリガー欠落（設計ギャップの静的検出1件目）

UnifiedPaneControlStyleのItemContainerStyle（243-294行）に、VS2013テーマ標準の
「`Items.Count==1`でTabItem自体をCollapse」Style.Trigger（Generic.xaml:536-540）が**無い**。
このままでは**単一ペイン4領域（シート/機器表/プロパティ/出力）全てに冗長な下タブが常時表示**され、
(h)のスクリーンショット等、全観察を汚染する。

**侍の写し誤りではない**——本実装の`PlacementToolBarPaneControlStyle`自体が同トリガーを持たない
ことをgrepで確認済み（`src/Ecad2.App/MainWindow.xaml`に"Items.Count"出現ゼロ）。2タブ固定ペイン
専用スタイルでは死文だったため省かれており、**全ペインへ昇格して初めて顕在化する設計ギャップ**。
これは漏れ2（全ペイン適用の副作用確認）の狙いどおりの検出であり、静的段階で1件目を拾えたことに
なる。ItemContainerStyleのStyle.Triggersへ同トリガー（5行）を追加されたい。
**増分1の統合スタイル設計にも同トリガー必須**と申し送る。

## 3. 侍の気づき（`ShowHeader`）への所見——**裏取り完了、侍説が正しい**

一次ソース（AvalonDock v4.74.1、scratchpad取得済み）で以下を確認した:

1. `LayoutDocumentPane.ShowHeader`は**公開bool プロパティ**（`LayoutDocumentPane.cs:49-58`、
   変更通知`RaisePropertyChanged`付き）。
2. VS2013テーマ既定テンプレートは、ヘッダー領域Grid（タブストリップ+メニューボタン）の
   `Visibility`を`Model.ShowHeader`へBinding済み（`Generic.xaml:212`、BoolToVisibilityConverter）。
3. さらに`ShowHeader=False`時に`ContentPanel`の`BorderThickness=1`へ補正するトリガーまで内蔵
   （`Generic.xaml:300-302`——ヘッダー消失時の枠線欠けを防ぐ配慮が既定で備わっている）。
4. **シリアライズ対応**（`LayoutDocumentPane.cs:166,174`、false時のみ属性書き出し+読み戻し）——
   レイアウト保存/復元と自然に整合する。

**結論**: 増分1の裁2（ドキュメントタブ非表示）実装は`<avalonDock:LayoutDocumentPane
ShowHeader="False">`の**属性1つで足り、DocumentPaneControlStyleのテンプレートコピー（約60行）は
不要**。ライブラリの正規機構ゆえ将来のAvalonDock更新にも強い。増分1ではShowHeader方式への変更を
推奨する。PoCの(f)コピー方式実装は「コピー方式でも可能」の実証として無駄にはならない（両方式の
比較材料になる）。

**残課題1件（増分1へ申し送り）**: 実行時にAvalonDockが新規`LayoutDocumentPane`を生成する経路
（アンカラブルの「タブ付きドキュメントとしてドッキング」等）では既定`ShowHeader=true`の新ペインが
生まれうる。裁4の`CanFloat="False"`はDockAsDocumentコマンドを封じない（別コマンド経路）ため、
増分1で`CanDockAsTabbedDocument`等による封止を併せて検討されたい（未検証、要一次ソース確認）。

## 4. 軽微（PoC修正不要、増分への申し送り）

- **軽1**: (e)のContentId分岐が無条件Collapse。案E原典は「ドッキング時のみ」条件付き
  （`Model.Parent.IsDirectlyHostedInFloatingWindow=False`とのAND）。フロート直接ホスト時は外側の
  LayoutAnchorableControl層でタイトルバーごと隠れるためPoCでは実害無しだが、**増分1では
  MultiDataTriggerの案E忠実形を推奨**。
- **軽2**: (h)コピーの枠線トリガーが簡略化されている（原典5本→2本、Generic.xaml:1749-1795対比。
  ドッキング時`BorderThickness`が原典の均一1でなく1,0,1,1のままとなり上辺が欠ける）。PoC目的には
  支障なし、**増分3では原典どおりの枠線トリガー移植を**。
- **軽3**: (h)(f)トグルの動的切替は、WPFの暗黙スタイル/スタイル再評価に依存する（実機で効かない
  可能性が理論上ある）。**忍者手順に「トグルで変化が無い場合はテーマボタンを一度押す、または
  (c)Float→Dockでコントロール再生成を促してから再観察」と明記**（下記手順に反映済み）。

## 5. 忍者実機確認手順（修1・修2反映後に実施）

前提: セカンドモニタ表示徹底・色判定は目視でなく画素採取【MUST】・PrintWindow撮影の色不正確性に注意。

1. **(a)** 起動直後の初期表示: 上段ツールバー領域の高さ（DockHeight=123の見え方、本実装の
   内容フィットとの差の所見）・左220/右280の固定幅・出力160。スクリーンショット保存。
2. **(b)** ペイン境界のAvalonDock内蔵リサイザーをドラッグし操作感を記録（GridSplitterとの差）。
3. **(d)** 各ペインの**中身**（リスト項目・TextBox・DataGrid）を順にクリックし、青帯（アクティブ色）が
   **常に1つだけ**移ることを画素採取で確認（T-110発端の実証、最重要）。
4. **(c)** 配置ツールのFloat()→Dock()をボタンで2〜3周、さらに**タイトルバーのMenuDropDownButton
   経由（フローティング/ドッキング項目）でも同様に**実施。タブ自己複製・縦長化・空白化の有無を
   各周で確認（再現すれば候補a棄却の確定根拠、再現せねば候補a有力の傍証）。
5. **(e)** Light/Dark両テーマで: 配置ツールペインのタイトルラベルのみ非表示・他ペインはラベル表示、
   の分岐が保たれること。テーマ切替直後の色も画素採取。
6. **(h)** トグルONで単一ペイン4領域のタイトルバーが完全に消えること（配置ツールは対象外のまま）。
   OFFで復帰。**効かない場合は軽3の再生成手順を試してから記録**。ON状態のスクリーンショットは
   裁5事後報告の添付材料とするため必ず保存。
7. **(f)** トグルONでドキュメントタブが消えること・OFFで復帰（軽3同様）。
8. **AutoHide** いずれかのペインのピンをクリックし、サイド領域への収納・復帰の挙動と、統合Manager
   のサイド領域がウィンドウ全域に及ぶ見え方を記録。
9. **(g)** Tabキー巡回: キャンバス相当のTextBox間・ペイン間の巡回が破綻しないこと。
10. **全ペインPaneControl適用の副作用**: 修2反映後、単一ペイン4領域に冗長タブが出ないこと・
    2タブペインのタブ表示が従来どおりであることを確認。

## 出典

- コミット`96d3164`全ファイル（`poc/t110-single-dockingmanager-poc/T110SingleDockingManagerPoc/`）
- `src/Ecad2.App/MainWindow.xaml`（"Items.Count"出現ゼロのgrep確認）・`MainWindow.xaml.cs:796-807`（ApplyUiChromeTheme）
- AvalonDock v4.74.1一次ソース（scratchpad取得済み）: `Generic.xaml:191-302（DocumentPaneControlStyle・ShowHeader Binding・補正トリガー）/536-540（Items.Count==1トリガー）/1712-1800（LayoutAnchorableControl既定スタイル）`・`LayoutDocumentPane.cs:49-58,166,174`
- `docs/ecad2-t099-c-paneltitle-label-only-hide-design-onmitsu.md`（案E原典トリガー条件）
