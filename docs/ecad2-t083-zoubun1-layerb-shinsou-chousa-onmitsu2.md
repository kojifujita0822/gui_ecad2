# T-083増分1・層B(AvalonDockドッキングクローム)「テーマ切替不発」の真相調査(隠密2)

調査日: 2026-07-16　調査者: 隠密2(key=1784191978287)　依頼元: 家老(T-083増分1、層B再挑戦)

## 依頼内容(DoD)

実際にタブ・タイトルバー(青い帯)を描画している要素・スタイルを特定し、テーマ切替に
追従しない根本原因、または有効な対処法を明らかにすること。

## 結論(先出し)

1. **青い帯の描画実体は`AnchorablePaneTitle`**(`LayoutAnchorableControl`のControlTemplate内)。
   その青は`Model.IsActive=True`のDataTriggerで適用される**アクティブキャプション色
   `ToolWindowCaptionActiveBackground`=#007ACCで、VS2013テーマのLight/Dark両方で同一値=仕様**。
   ダークに切り替えても青いままなのが正しい挙動(本家Visual Studioも明暗共通のアクセント青)。
2. **層B(非アクティブキャプションの切替)は、忍者検証(2026-07-16、コミット21d58fb時点)の
   スクリーンショットにおいて既に正しく機能していた**。忍者撮影PNGの画素実測で、ダークON時に
   非アクティブキャプション5箇所全てが#2D2D30(VS2013 Dark正解値)、OFF時に#EEEEF2(Light正解値)へ
   切り替わっていることを確認した。**観点2・観点4のNG判定は誤読**であり、バグは存在しない。
3. 誤読の成因は3点の重なりと分析する:
   (a) 青い帯(アクティブキャプション)が両テーマ共通色である仕様を検証者・依頼側の誰も知らなかった
   (侍・旧隠密の先行調査も「切り替わらないはずがない機構」を追うだけで、この仕様に到達していなかった)。
   (b) 増分1の範囲外である層C(メニューバー・ツールバー・白いアプリコンテンツ=シート一覧・機器表
   DataGrid等)が画面の大半を占め、これらは当然明るいままのため「全体としてほぼ変わっていない」
   という印象が支配した。
   (c) キャプションバーは高さ十数pxと細く、目視では変化を見落としやすい(本調査でも当初、
   同じPNGを目視して「変わっていない」と誤読しかけ、画素実測で覆った)。
4. 現行ビルド(2026-07-16 18:19、増分2込み)でも、(i)ミニマル構成、(ii)実アプリin-process、
   (iii)メニューPopup経由、(iv)**実プロセス+忍者と同一のUIA経路+同一のPrintWindow撮影**、の
   全4経路で切替が正常動作することを実測確認した(ツリー真値と画面実態の両面)。
5. **侍が増分2で観測した「MenuBarAreaのプロパティ値は新色だが画面に反映されない」症状も、
   現行ビルドの実プロセス実測では再現しなかった**(メニューバー領域 #F0F0F0→#2D2D30 が画面
   ピクセルとして成立)。要再確認(不明点の節を参照)。

## 証拠1: 忍者撮影PNGの画素実測(最重要)

忍者の検証記録`docs-notes/verification-ninja-t083-zoubun1.md`が参照するスクリーンショット
(別セッションscratchpad、1280x800)を画素採取した。座標の妥当性はLight時に#EEEEF2
(VS2013 Light非アクティブキャプションの正確な値)を示すことで裏付けている。

| 採取点(座標) | initial(Light) | darkon | darkon-retry | sheet-added-clean-darkon | sheet-added-clean-darkoff |
|---|---|---|---|---|---|
| 配置ツール(300,114) | #EEEEF2 | **#2D2D30** | **#2D2D30** | **#2D2D30** | #EEEEF2 |
| シート(120,192) | #EEEEF2 | **#2D2D30** | **#2D2D30** | #007ACC(アクティブ) | #007ACC(アクティブ) |
| 機器表(1120,192) | #EEEEF2 | **#2D2D30** | **#2D2D30** | **#2D2D30** | #EEEEF2 |
| プロパティ(1120,404) | #EEEEF2 | **#2D2D30** | **#2D2D30** | **#2D2D30** | #EEEEF2 |
| 出力(300,627) | #EEEEF2 | **#2D2D30** | **#2D2D30** | **#2D2D30** | #EEEEF2 |
| メニューバー(640,40) | #F0F0F0 | #F0F0F0 | #F0F0F0 | #F0F0F0 | #F0F0F0 |

- #2D2D30 = DarkBrushs.xamlの`ToolWindowCaptionInactiveBackground`の正解値。**切替は成功していた**。
- メニューバーが#F0F0F0のままなのは増分1当時、層C(増分2)未実装ゆえ正しい。
- フロート検証PNG(t083-1-float-clean-darkon.png)のタイトルバーは#007ACC+グリップ#59A8DE=
  `ToolWindowCaptionActiveBackground`+`ActiveGrip`のDark正解値(両テーマ共通)。フロート直後は
  当該コンテンツがアクティブになるため青が正しい。観点4のNGも同じ仕様誤解。

## 証拠2: テーマパッケージの実配布物・一次ソースの構造確認

NuGetキャッシュのDLL(4.74.1、BAML)をWPFとして実ロードして検分し、GitHub一次ソース
(タグv4.74.1)との一致を確認した。

- 辞書構造: DarkTheme.xaml=[DarkBrushs.xaml → Themes/Generic.xaml]、LightTheme.xaml=
  [LightBrushs.xaml → 同一のGeneric.xaml]。**両テーマの差はブラシ辞書のみ**で、コントロール
  スタイル本体(Generic.xaml)は完全共通。Generic.xaml系列にブラシの隠れ定義(影武者)は無い。
- `AnchorablePaneTitle`のスタイル・ControlTemplate内Trigger群・タブ(ItemContainerStyle)の
  色参照は**全てComponentResourceKey経由のDynamicResource**。StaticResource凍結の構造は無い
  (Generic.xaml内のブラシCRKへのStaticResource参照は0件、一次ソースgrepでも確認)。
- 主要ブラシの実値(Light/Dark):

| キー | Light | Dark | 備考 |
|---|---|---|---|
| ToolWindowCaptionActiveBackground(青い帯) | #FF007ACC | #FF007ACC | **同値=仕様** |
| ToolWindowCaptionActiveGrip | #FF59A8DE | #FF59A8DE | 同値=仕様 |
| ToolWindowCaptionActiveText | #FFFFFFFF | #FFFFFFFF | 同値=仕様 |
| ToolWindowCaptionInactiveBackground | #FFEEEEF2 | #FF2D2D30 | 切替で変わる |
| ToolWindowCaptionInactiveText | #FF444444 | #FFD0D0D0 | 切替で変わる |
| ToolWindowTabUnselectedBackground | #FFEEEEF2 | #FF2D2D30 | 切替で変わる |
| ToolWindowTabSelectedActiveBackground | #FFF5F5F5 | #FF252526 | 切替で変わる |
| TabBackground(ペイン背景) | #FFF5F5F5 | #FF252526 | 切替で変わる(忍者観測の「プロパティ中身の暗色化」の正体) |
| DocumentWellTabSelectedActiveBackground | #FF007ACC | #FF007ACC | 同値=仕様 |

- クラス素性: `Vs2013DarkTheme`/`Vs2013LightTheme`は`Theme`直系(DictionaryThemeではない)、
  GetResourceUriは`/AvalonDock.Themes.VS2013;component/DarkTheme.xaml`等を返す(旧隠密調査と一致)。

## 証拠3: 切替動作の多段実測(全て正常)

いずれもscratchpad内で実施(共有main・リポジトリファイル無変更、ビルド成果物は複製を使用し
ロックも回避)。実行ランタイムは.NET 10.0.9(アプリの共有ランタイムと同一版)。

1. **ミニマル構成**(DockingManager2個をコードで構築、Vs2013Light→Dark→Light):
   キャプション#EEEEF2→#2D2D30→#EEEEF2、ペイン#F5F5F5→#252526→#F5F5F5。
   切替時にAnchorablePaneTitleインスタンスは再生成される(テンプレート再適用)ことも確認。
2. **実アプリin-process**(実ビルドのMainWindowを生成、VM.IsDarkMode直接切替):
   5キャプション+MenuBarArea全て正常切替(RenderTargetBitmap実測)。
3. **実アプリin-process+メニュー経由**(表示メニューPopupを実際に開きAutomationPeerでInvoke):
   ツリー真値(RTB)と画面実態(PrintWindow PW_RENDERFULLCONTENT)の両方で全キャプション正常切替。
4. **実プロセス**(複製exeを起動、忍者と同一のUIA経路=ExpandCollapse→Invoke、同一の
   PrintWindow撮影): キャプション5箇所#EEEEF2→#2D2D30、メニューバー#F0F0F0→#2D2D30、
   切替5秒後も安定。

なお忍者のスキルの撮影実装(`helpers.ps1`)がPW_RENDERFULLCONTENT(=2)を使っていることを確認済み。
撮影系は健全であり、忍者のPNGは画面実態を正しく写している(だからこそPNG画素が#2D2D30を示した)。

## 再検証用の期待値表(忍者への引き継ぎ用)

ダークモードトグルON時に「変わるべき箇所」と「変わらないのが正しい箇所」:

| 観測対象 | Light時 | Dark時(期待) | 備考 |
|---|---|---|---|
| 非アクティブなパネルのタイトルバー | #EEEEF2 | #2D2D30 | 層Bの本丸。目視でなく画素採取で判定すべし |
| アクティブなパネルのタイトルバー | #007ACC | **#007ACC(不変)** | 仕様。変わらなくて正しい |
| タイトルバーの文字(非アクティブ) | #444444 | #D0D0D0 | |
| ペイン背景(透過コンテンツ越し) | #F5F5F5 | #252526 | プロパティパネル中身の暗色化として見える |
| フロートウィンドウのタイトルバー | 直後はアクティブ=#007ACC | **#007ACC(不変)** | 非アクティブ化させれば#EEEEF2→#2D2D30の差が出る |
| メニューバー・ツールバー | #F0F0F0系 | 増分2以降は#2D2D30系 | 増分1時点では変わらなくて正しい(層C) |
| シート一覧・機器表DataGrid等の白いコンテンツ | 白 | 増分2/3の対応範囲まで白のまま | 層Bではない |

## 青い帯もダークで暗くしたい場合(参考、UI/UX判断=殿マター)

仕様のまま(VS踏襲で青維持)が既定線だが、変えたい場合の公式レシピ(GitHub Issue #464/#12):
テーマ辞書より後にマージされる辞書または各DockingManagerのResourcesで
`AvalonDock.Themes.VS2013.Themes.ResourceKeys.ToolWindowCaptionActiveBackground`等の
ComponentResourceKeyを上書きする(動的変更には`options:Freeze="False"`が必要)。

## 不明点

- 忍者検証時のコミット(21d58fb)そのもののビルドでの再実測は未実施(侍のビルドと衝突しない
  隔離ビルド環境が必要なため見送り)。ただし証拠1(当時のPNG自体が切替成功を示す)により、
  当時のビルドで機能していたことは実測済みと言える。

## 追記(2026-07-16 同日): 侍のMenuBarArea症状も解明・完全決着

初稿の不明点だった「侍の診断ログ観測(Menu.Background実値=#FF2D2D30なのに撮影画素は白のまま)」
も、追加実測で矛盾なく解明された。

- 侍の再現手順に完全一致させた再実測(コミット1235898の同一ビルド、ExpandCollapse→
  **TogglePattern**、PrintWindow単体撮影)でも、メニュー行は正しく暗色化した
  (x=900縦走査でy=32〜80が全て#2D2D30)。
- **正体はサンプリング座標の取り違え**: ウィンドウ上部y≦31は**OSタイトルバー**
  (Windowsライトテーマの標準色#EEF4F9≒白)で、アプリ側テーマと無関係に不変。侍の申告座標
  (y≈18〜38)はこの帯と重なる。「直下で暗色化しているのはツールバー行」と見えたものが
  実は本物のメニュー行(y≈32〜52)だった。Light時はタイトルバー/メニュー/ツールバーが
  ほぼ同色のため行の取り違えが起きやすい。
- **UIAの罠(本調査で実際に踏んだ)**: `ControlType.MenuBar`をFindFirstで探すと、アプリの
  メニューバーではなく**タイトルバーのシステムメニュー(ウィンドウアイコン領域、
  約22x22px)を先に掴む**。ここから座標を導出すると必然的にタイトルバー帯を採取してしまい、
  「実値は正しいのに画素が白のまま」が完全に再現される。座標はWPF要素の`PointToScreen`から
  導出するか、y≧32を採取すべし(スキル追記候補として家老へ提案済み)。
- 環境固有差異説は棄却: 同一マシン5セッション体制ゆえ環境は構造上同一
  (OS=Windows 11 26200.0、DPI 100%、両モニタ1920x1080、Windowsライトテーマ、
  ハイコントラスト無効を実測記録)。
- 参考: OSタイトルバー自体をダーク化したい場合はアプリ側テーマ辞書では不可能で、
  `DwmSetWindowAttribute`の`DWMWA_USE_IMMERSIVE_DARK_MODE`等のOS API併用が必要
  (将来の磨き込み候補、T-083範囲外)。

家老裁定(2026-07-16): 層B・メニューバーとも「バグは存在しない」で完全決着。忍者の
画素採取ベース再検証でも全観点OKを確認済み。

## 追記2(2026-07-16、殿の問いへの回答): ダーク時に「プロパティ以外のドックが白背景」の正体

殿より「ダークテーマ反映時のプロパティ以外のdockが白背景の説明が見当たらない」との
ご指摘を受け、実XAML(現行main、増分3コミット3efca6b時点)で確認した。

**白いのはドッキングクロームではなく、ペインの中身(アプリ側コンテンツコントロール)**。
AvalonDockテーマ(層B)が塗るのはキャプション・タブ・ペイン背景までで、中に置いた
コントロールの色はアプリの責任範囲。各ドックの中身と挙動は次の通り。

| ドック | 中身(MainWindow.xaml) | ダーク時の見た目とその理由 |
|---|---|---|
| シート | `ListBox SheetNavList`(512行) | **白のまま**。WPF既定スタイルの背景=SystemColors.Window(白)。XAMLに明示色指定が無く既定値のため |
| 機器表 | `DataGrid DeviceTableGrid`(624行) | **白のまま**。DataGrid既定の背景・行背景=白。同上 |
| 出力 | `DataGrid OutputGrid`/`FindResultsGrid`(767,782行) | **白のまま**。同上 |
| プロパティ | Grid+DockPanel+StackPanel+TextBlock群(643行〜、背景指定なし) | **暗くなる**。コンテナが全て透明のため、層Bで正しく暗色化したペイン背景(TabBackground=#252526)が透けて見える |
| 部品選択(プロパティ下段の切替先) | `ListBox PartSelectionList`(709行) | 白のまま(表示時)。ListBox既定の白 |
| 配置ツール | ボタン群(ToolBar系ブラシは増分2で対応済み) | 暗色化する |

つまり「プロパティだけ暗い」非対称は、プロパティの中身だけが**たまたま透明コンテナ**で
あることによる。他ドックが白いのは既定白のListBox/DataGridが不透明に覆っているため。

**増分計画の隙間(派生提案として家老へ報告)**: これらListBox/DataGridは現行の増分計画の
どこにも明示的に割り当てられていない。増分2=メニュー・ツールバー、増分3=ダイアログ+
固定色パネル(`FindBar`等、XAMLに`Background="White"`と明示された箇所)、増分4=キャンバス
意味色。着手前調査の「App層直接色指定一覧」が**明示的な色指定のgrep**で作られたため、
色指定を持たない(既定値の)コントロールが棚卸しから漏れた構造。ペイン内コンテンツの
テーマ連動(ListBox/DataGridの背景・前景・行背景等のDynamicResource化、または暗黙スタイル
での一括指定)を増分3.5または増分5として追加することを提案する。

## 派生提案

1. **層Bは「完了扱い+忍者の再検証(期待値表つき)」で決着可能**。増分1の再実装・修正は不要。
2. テーマ・色に関する実機検証は目視でなく**画素採取判定を標準とする**ことをスキル/役儀への
   追記候補として提案(本件は検証者・調査者の双方が同じPNGを目視で誤読した実例)。
3. 「アクティブアクセント色は両テーマ共通」という仕様周知(本調査書の期待値表を正とする)。

## 出典

- 忍者検証記録: `docs-notes/verification-ninja-t083-zoubun1.md`とその撮影PNG群(画素実測)
- NuGetキャッシュ実配布物: `dirkster.avalondock.themes.vs2013/4.74.1`(BAML実ロード検分)
- GitHub一次ソース(タグv4.74.1): DarkTheme.xaml / LightTheme.xaml / DarkBrushs.xaml /
  LightBrushs.xaml / Themes/Generic.xaml / Themes/ResourceKeys.cs / Vs2013DarkTheme.cs /
  DockingManager.cs(OnThemeChanged)
- GitHub Issues: #514, #464, #12(アクセント色上書きレシピ)、#318/#402/#378(切替時の
  レイアウト系既知バグ、本件とは別symptom・未解決あり——切替本採用時の回帰確認推奨)
- 実験スクリプト一式: 本セッションscratchpad(inspect-vs2013-*.ps1, repro-*.ps1)
- アプリ側コード: `src/Ecad2.App/MainWindow.xaml.cs`(ApplyDockingManagerThemes等)、
  `src/Ecad2.App/App.xaml`、Exploreエージェントによる全XAML横断確認(マスク要因0件)
