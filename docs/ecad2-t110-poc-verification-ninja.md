# T-110 増分0（PoC）実機確認（忍者）

検証日: 2026-07-21〜22　検証担当: 忍者　委任元: 隠密（静的レビュー完了後の引き継ぎ）
対象: コミット`b584cc0`（`poc/t110-single-dockingmanager-poc/T110SingleDockingManagerPoc/`、
隠密静的レビュー`docs/ecad2-t110-poc-review-onmitsu.md`の要修正2件反映済み）
検証手順: 同レビュー§5の10項目

## 0. 環境トラブルと対応（検証開始前に発覚・解決済み）

検証着手直後、PoC・本実装Ecad2.App双方でスクリーンショットが白紙になる現象に遭遇（PrintWindow・
CopyFromScreenいずれの方式でも）。UIA探索では要素・座標とも正常でレイアウトは成立しており、
ワークステーションロック・GPUドライバクラッシュイベントも否定。診断の結果、
`HKCU\Software\Microsoft\Avalon.Graphics\DisableHWAcceleration=1`（WPFソフトウェアレンダリング
強制）で正常描画を確認——**環境上、GPUハードウェアアクセラレーション経由のWPF描画が機能して
いなかった**ことが原因と判明。家老経由で殿裁可を得て「検証中のみ一時適用」とし、他役のdotnet
プロセス不使用を確認の上で適用、**検証終了後は直ちに復元済み**。この環境異常自体はT-110に
限らない重大な発見のため、別途家老が記録を検討する。

## 1. 検証結果（10項目）

| 項目 | 判定 | 所見 |
|---|---|---|
| (a) 初期表示 | OK | 左220/右280/出力160/ツールバー123、実測値は設計値とほぼ一致 |
| (b) 境界リサイズ | OK | AvalonDock内蔵Thumbドラッグで正常リサイズ、崩れなし |
| (d) アクティブ色一元化 | **OK（最重要）** | シート/機器表/プロパティ/出力の4パネルを順にクリックし、常に1つだけ青(#007ACC)・他3つは非アクティブ(#EEEEF2)を確認。**T-110発端の「複数DockingManager由来の非同期」問題が単一統合で解消されることを実証** |
| (c) Float/Dock往復 | OK | ボタン経由3周＋MenuDropDownButton経由(フローティング/ドッキングメニュー項目)2周、計5周。タブ自己複製・縦長化・空白化いずれも無し |
| (e) Light/Dark両テーマ | OK | タイトル帯色は正しく切替(Light時#EEEEF2/Dark時#2D2D30)、アクティブ色は両テーマ共通#007ACC。4パネルのラベル表示は両テーマとも維持 |
| (h) タイトルバー完全非表示 | OK（手法上の罠あり、下記2.2参照） | 物理クリックで検証した結果、ON/OFFとも正常動作。4パネル完全非表示・配置ツールは対象外、を確認 |
| (f) ドキュメントタブ非表示 | **NG（重大バグ検出）** | 下記2.1参照 |
| AutoHide | OK | 収納・展開(フライアウトがウィンドウ全域に及ぶ見え方)を確認 |
| (g) Tabキー巡回 | OK | フォーカス確認用1→2→3→(Window経由)→1と正しくCycle、破綻なし |
| 全ペイン副作用確認 | OK | 修2(Items.Count==1トリガー)により単一ペイン4領域とも冗長タブ無し、2タブペインは従来どおり |

## 2. 重大・重要所見

### 2.1 【重大バグ】(f)ドキュメントタブ非表示ON時、キャンバス内容が生のオブジェクト名で表示される

物理クリックで(f)をONにすると、「作図キャンバス」のタブ(ヘッダ)は正しく非表示になるが、
**コンテンツ領域の内容が実際のビュー(TextBlock/TextBox群)ではなく、"AvalonDock.Layout.LayoutDocument"
という生のオブジェクト名文字列に置き換わって表示される**。OFFに戻すと正常復帰。

推定原因（要侍/隠密のコード確認）: `DocumentTabHiddenPaneControlStyle`
(MainWindow.xaml:383-442)の`ControlTemplate`内`ContentPresenter`(`PART_SelectedContentHost`、
`ContentSource="SelectedContent"`)に、`ContentTemplate`の指定が無い。対照として、同ファイル内
`UnifiedPaneControlStyle`(アンカラブル側、204-323行)は同様の`ContentPresenter`に加えて
`<Setter Property="ContentTemplate"><DataTemplate><avalonDock:LayoutAnchorableControl
Model="{Binding}"/></DataTemplate></Setter>`を明示しており、これが`SelectedContent`
(`LayoutContent`モデル)を実ビューへ変換する役割を担っていると見られる。`DocumentTabHiddenPaneControlStyle`
にはこれに相当する`ContentTemplate`(`avalonDockControls:LayoutDocumentControl`でラップする設定)
が欠落しているため、`ContentPresenter`がモデルをそのまま`.ToString()`表示してしまっていると
推測される。増分1実装時は同様の見落としに注意されたい。

スクリーンショット: `t110-f2-real-click.png`（再現時点、忍者スクラッチパッドに保存、パスは
本報告末尾参照）

### 2.2 【手法上の重要な教訓】UI Automation TogglePattern.Toggle()がClickイベントを経由しない

(h)(f)とも、`Invoke-Ecad2Button`相当(UIA `TogglePattern.Toggle()`経由)で操作した直後は
`ToggleState`(押下表示)だけが変化し、ステータステキスト・視覚効果とも一切反映されなかった
（実装が壊れているように見えた）。物理クリック(実マウスイベント)で操作し直したところ、
両方とも正常に動作することを確認した。

**原因**: WPFの`ToggleButton`に対する UI Automation `TogglePattern.Toggle()`は、
`IsChecked`(→`ToggleState`)は反転させるが、対応する`Click`ルーテッドイベントを発火させない
模様（コードビハインドの`Click=`ハンドラで実処理する実装だと、一切処理が走らない）。
本スキル0節の既存の罠「UI Automation経由のInvoke/Selectがボタンの内部状態を不安定化させる」
と同系統だが、**ToggleButton特有の新規パターン**として確認できた。`ecad2-ui-automation`
スキルへの追記を推奨する——ToggleButtonの検証で「ToggleStateは変化するが効果が見えない」場合、
実装バグと即断せず物理クリックで再検証すること。

### 2.3 SelectedContentIndex="1"がXAML記述どおりに反映されない

`MainWindow.xaml:471`の`<avalonDock:LayoutAnchorablePane DockHeight="123" SelectedContentIndex="1">`
は、子要素の並び(index0=基本機能、index1=配置ツール)からすると起動時に「配置ツール」が選択
されるはずだが、**実機では起動直後「基本機能」(index0)が選択されている**ことをUIA
(`SelectionItemPattern.IsSelected`)で確認した。隠密静的レビューは「XAML記述の確認」に留まり
実行時挙動は未検証だった箇所（実機確認で初めて判明）。T-110増分1実装への影響は軽微と見られる
（初期選択タブの見た目だけの問題で、機能面への影響は無し）が、申し送りとして記録する。

### 2.4 (d)検証時の副次所見: DataGridColumnHeader単体クリックではアクティブ切替が起きない

機器表パネルの検証で、DataGridの列ヘッダ("機器名"等)を単体クリックしてもペインはアクティブ化
されず、グリッド本体(空白領域)をクリックして初めてアクティブ化された。(d)の核心基準
（クリックしたペインのみがアクティブになる）自体は満たしているため軽微な所見に留めるが、
実際の機器表(行データあり)ではセルクリックが主な操作になるため実害は無いと見られる。

### 2.5 (c)検証時の副次所見: フロートウィンドウの出現位置とタイトルバー実装

配置ツールをFloat()した際、フロートウィンドウは**プライマリモニタの(0,0)付近**に出現した
（セカンドモニタ表示中のメインウィンドウ近くではない）。AvalonDockの初回フロート時デフォルト
位置と見られ、PoC自体の不具合ではないが、実機検証時は殿の画面に一瞬映り込みうる点に注意
（本検証では即座にDock()で戻した）。

また、フロートウィンドウのタイトルバーは、ドッキング時の単独ペイン(シート等)で使われる
`UnifiedAnchorablePaneTitleStyle`(ContentId分岐を含む、MenuDropDownButton/PART_AutoHidePin/
PART_HidePin構成)とは**異なるボタン構成**(`SinglePaneContextMenu`/`PART_PinMaximize`/
`PART_PinClose`)を持つ別のチェコンテナが使われていた。このため、**(e)検証の核心である
`Model.ContentId=="PlacementToolBar"`時のラベル非表示トリガーは、本PoCで実施した操作範囲
(ドッキング時=タブ表示、フロート時=別チェコンテナ)では一度も発火する場面が無かった**。
配置ツールが「単独ペインとしてドッキングされた状態」になるシナリオが本PoCには無いため、
この分岐の実効性は今回未検証のまま残る。増分1実装・レビュー時に、この分岐が実際にどの場面で
発火することを想定しているか（本実装のPlacementToolBarDockingManagerが現状どういう状態を
取りうるか）を隠密・侍で確認されたい。

## 3. 総合判定

(d)（本タスクの発端かつ最重要検証項目）は明確にOK——単一DockingManager統合の核心的効果を
実機で実証できた。(c)(b)(e)(h)(g)・AutoHide・全ペイン副作用も問題なし。

一方、**(f)は重大バグを検出**(2.1)。修正が必要。加えて2.2〜2.5の所見は、増分1実装時に
考慮・確認されたい事項として申し送る。

## 4. 証跡ファイル（忍者スクラッチパッド、恒久保存ではない点に留意）

`C:\Users\kojif\AppData\Local\Temp\claude\C--ECAD2\d261558b-9426-4986-abed-e29eb75eda36\scratchpad\`
配下: `t110-01-initial-swrender.png`（初期表示）、`t110-d1〜d4-*-click.png`（アクティブ色一元化）、
`t110-c1〜c3,c-final.png`（Float/Dock往復）、`t110-e1,e2-dark*.png`（テーマ切替）、
`t110-h3-real-click.png`（タイトルバー完全非表示ON、裁5事後報告の添付材料候補）、
`t110-f1,f2-real-click.png`（ドキュメントタブ非表示バグの再現画像）、
`t110-autohide1〜3.png`（AutoHide）。恒久保存が必要な画像があれば家老の指示でdocs等へ移動する。
