# タスク台帳（家老が采配してよい根拠）

家老が采配してよいのは **Approved** または **In-progress** の行だけ。采配には必ずタスクIDを添える。
台帳に無い作業は、家老の裁量では着手せず `docs/proposed.md` へ記録して殿の承認を待つ
（詳細は `docs-notes/roles/karo.md` の「采配の権限線引き」）。

- 状態: Proposed → Approved → In-progress → Done / Rejected（+ Blocked＝外部要因待ち）
- 種別: auto-OK（家老の裁量で采配可） / gated（殿の承認を経たもの）
- **完了・取り止めタスクの詳細経緯は `docs/todo-archive.md` を参照**（2026-07-09軽量化＝殿指示。
  本ファイルは「生きているタスクの詳細」＋「完了・取り止めタスクの1行索引」のみを置く。
  タスクが完了したら家老が詳細をアーカイブ末尾へ移し、ここは1行索引に縮める運用）

## 【最優先・進行中インシデント】Ecad2.App起動不能（2026-07-18）

忍者実機確認でEcad2.App起動時にStack overflowが発生し即クラッシュ（2回連続再現）。`dotnet build`
自体は0警告0エラーで成功するが実行不能。スタックトレースはGrid→Border→Control→
FrameworkElement.MeasureCoreの再帰パターン。未コミットのApp.xaml/MainWindow.xaml（T-099要件1・
T-089 ControlTemplate自作・T-099要件2の帯・ツールバー1段目ラベル色、計4件が同居）のいずれかに
起因すると推定、T-089のControlTemplate自作（Border>Grid>ContentPresenter+Rectangle）が最有力候補
（忍者所見、未確定）。侍・隠密へ並行調査を委譲中、他の全作業は一時中断。
**真因判明（2026-07-18、忍者、crash.log解析、`docs-notes/ecad2-stackoverflow-crashlog-excerpt-ninja.md`）**：
表面症状のStack overflowは見かけ上のもので、真の一次例外は`System.InvalidOperationException
「'{DependencyProperty.UnsetValue}'はプロパティ'BorderBrush'の有効な値ではありません」`
（`Border.ArrangeOverride`内）。約1秒間に同一例外が3285回連続発生——
`OnDispatcherUnhandledException`→`MessageBox.Show`→新たな`UpdateLayout`→同じ例外再発、という
無限ループでスタック領域を食い潰した。忍者所見（推測）＝T-089新設テンプレートの
`BorderBrush="{TemplateBinding BorderBrush}"`が、Button/ToggleButton暗黙的スタイルにBorderBrush
既定値Setterが無いままUnsetValueを評価している疑い。侍へ修正采配済み、隠密は修正の副作用有無の
レビュー準備中。
**解消・実測確認済み（2026-07-18、侍）**：真因は忍者のcrash.log特定と隠密のキー未定義説の**両方が
正しい2段構えの複合**——(1)Button.*7キーはWPF Aero2テーマDLL内部リソースでアプリ側から解決不能
（ControlTemplate.Triggers内のStaticResourceは遅延解決のためビルド時エラーにならずトリガー発火時
にUnsetValueとして現れる） (2)暗黙的StyleがテーマStyleを置き換えたことでBackground/BorderBrush等
の既定値Setterも失われた。**特定のトリガーに依らず**、Style本体の既定値欠落によりTemplateBinding
経由で通常のArrangeパスでUnsetValueが渡り例外→MessageBox→UpdateLayout→再発の無限ループでStack
overflow（1秒で3285回、忍者実測）に至ったと見られる。**訂正（2026-07-18、隠密の事実確認・侍の
自己申告）**＝当初「起動直後IsEnabled=falseトリガー発火が発火源」と記録したが、侍いわく**これは
一次情報でなく状況証拠からの推測**（デバッガ等での直接確認なし）。加えて対処は2要素（10キー定義・
Setter群追加）を同時投入したため、**どちらが決定打かの単独切り分けも未実施**（両方に欠陥が
あったことは確実だが、クラッシュの必要条件がどちらかは未確定）。
対処＝Button.*10キーをecad2側で明示定義（dotnet/wpf Aero2既定値を転記）+Button/ToggleButton両
Styleへ既定値Setter群を追加。検証＝クリーン起動2回連続正常・crash.logに新規例外なし・build/test
全合格（App.Tests716/Core.Tests120）。隠密静的レビュー完了（副作用＝ツールバーボタン群の枠線が
見えなくなる視覚変化の可能性、実害なし、実機確認課題として申し送り）。忍者へ実機検証再開を依頼
——押下フィードバック(PressedOverlay)・テストモードON色・T-099帯・T-083の1段目ラベル色、計4件。

## 現在の要望スコープ

- REQ-01: 技術スタック選定（`docs/ecad2-stack-decision-brief.md` 参照）

## 生きているタスク

### T-099 配置ツールバー2段目のドックタグ表示制御＋幅動的調整 — Approved（gated、殿直接指示2026-07-17）

**起票=殿直接指示2026-07-17**「avalondockの仕様でドッキング時はdockタグ非表示はできるか？フロート時は
表示する」「2段目ツールパネルに実装したい。またツールパネルの幅も動的に幅調整して。余白が多い」。
対象範囲＝**配置ツールバー2段目のみ**（殿確認済み、他パネルへは広げない）。実装イメージ＝
殿提示のGX Works3スクリーンショット（`c:\Users\kojif\Desktop\claude_TEMP\Dockimage.png`）の
「接続先」タブ——ドッキング時はコンテンツ幅にフィットした細身のタブ（ピン留め・閉じるアイコンの
みで文字ラベル+アイコン分だけの幅、余白なし）。

**要件**：
1. ドッキング状態の時、ドックタグ（タブヘッダー）を非表示にする
2. フローティング状態の時はタブ／タイトルを表示する
3. パネル幅をコンテンツに動的フィットさせ、現状の余白（既定の固定幅による空白）を解消する

**家老の事前調査（2026-07-17、Web一次情報）**：AvalonDock（Dirkster99/AvalonDock）の
`LayoutAnchorablePane`に`IsDirectlyHostedInFloatingWindow`／`IsHostedInFloatingWindow`という
状態判定プロパティが存在。`AnchorablePaneControlStyle`内でこれをDataTrigger条件に使い、
タブストリップ（`TabPanel`）の`Visibility`を切り替える構成が技術的な筋道と見立てる。ドッキング時
（`LayoutAnchorablePaneControl`）とフローティング時（`LayoutAnchorableFloatingWindowControl`、
`LayoutFloatingWindowControl`継承・独立ウィンドウ）は元々描画クラスが別なため、フローティング側は
既存のタイトル表示（`DropDownControlArea`）がそのまま使える見込み。出典＝
[LayoutAnchorablePane Wiki](https://github.com/Dirkster99/AvalonDock/wiki/LayoutAnchorablePane)・
[LayoutAnchorableFloatingWindowControl Wiki](https://github.com/Dirkster99/AvalonDock/wiki/LayoutAnchorableFloatingWindowControl)。

**リスク注記【重要】**：AvalonDockの`AnchorablePaneControlStyle`／`ControlTemplate`領域は、
T-083増分1（層B）で「値は正しく設定されるが実描画に反映されない」事象に遭遇した経緯がある
既知の要注意領域（最終的には検証ミスと判明したが、調査に一往復分を要した）。高リスク領域ゆえ
実装速度より検証優先＝PoC（ドックタグ表示制御のみ、最小構成）→忍者実機確認→幅調整の増分実装、
の順で進めること。

**着手（2026-07-17）**：侍へPoCから着手を采配。
**PoC難航・一旦保留（2026-07-17）**：AnchorablePaneControlStyleのHeaderPanelへDataTrigger追加
という設計自体は理論上妥当なはずだが、実機では配置ツールバー2段目全体（タブだけでなくボタン
本体まで）が幅ほぼゼロに潰れる実害が発生（殿確認済み）。Grid RowDefinitions調整でも解消せず、
原因未特定。T-100（ハッチング模様除去、同じAnchorablePaneControlStyle領域を調査）の知見を先に
得てから再挑戦する方針で、侍が新規発見3件を優先し一旦保留。**リスク注記【重要】節で予告した
「高リスク領域」の懸念が的中した形**——AvalonDockのAnchorablePaneControlStyleカスタマイズは
これで3件目の想定外挙動（層B・増分7メニュー・本件）、今後も同領域は検証優先を徹底する。
**根本原因調査、殿直接指示（2026-07-17）で隠密へ委譲**：侍が対症療法（起動時にファイルメニューの
ドロップダウンを一瞬開閉するコードを仕込む）で表示崩れ自体は解消できたが、メニューのハイライト
残留という副作用が`Keyboard.ClearFocus()`でも解消できず不完全。侍の調査引き継ぎ材料：
(1) 現象＝配置ツールバー2段目の`ContentPresenter(ContentSource="SelectedContent")`のみサイズが
潰れる（実機でBackground=Magenta化して確認、他要素=TextBlock等は正常にAuto計算される）
(2) 殿の実機操作で「メニューのドロップダウンを一度開く」ことが引き金になり表示が回復すると判明
（ダークモード切替ではなくメニュー操作が本質、殿ご指摘）
(3) 一次ソース調査＝`LayoutAnchorablePaneControl`は`TabControlEx`（独自TabControl拡張）を継承。
`TabControlEx.OnApplyTemplate`は`IsVirtualizingAnchorable`（既定true）がtrueの場合、コンストラクタの
`ItemContainerGenerator.StatusChanged`による初期選択反映ロジックが`ItemsHolderPanel==null`で早期
returnし実質無効化される、という疑わしい経路を発見（未確定）
(4) 増分7（ComponentResourceKeyでのMenuItemテンプレート派生）との競合を殿が疑っておられる。メニュー
操作（Popup生成）が何らかの形でDockingManager側のリソース解決/レイアウトパスを誘発している可能性
(5) 単純な`UpdateLayout()`・Theme再適用の複数パターンでは再現せず、「メニューの`IsSubmenuOpen`を
実際にtrue→(次のDispatcherサイクル)→false」という特殊な操作のみ効果があった。
侍が示した調査観点＝(a)なぜメニュー操作がDockingManager側のレイアウトに影響するか (b)TabControlEx/
LayoutAnchorablePaneControlのOnApplyTemplate〜SelectedContent解決の正確な処理順序 (c)増分7の
MenuItemテンプレート派生との実際の関連有無。
**着手（2026-07-17）**：隠密へ根本原因調査を委譲。侍は対症療法止まりで一旦区切り、作業ツリー整理中。
**根本原因調査完了（2026-07-17、隠密、一次ソース裏取り済み、
`docs/ecad2-t099-selectedcontent-collapse-root-cause-survey-onmitsu.md`）**：`DockingManager
.IsVirtualizingAnchorable`（既定true、ecad2は明示未設定）が`TabControlEx`のコンストラクタへ伝播、
`_IsVirtualizing=true`時は`OnApplyTemplate()`が`ItemsHolderPanel`関連初期化を全て早期returnで
スキップし標準WPFの`SelectedContent`メカニズムのみに委ねる構造と確認——**これはバグでなく設計上
意図的な分岐**。侍PoCテンプレートはAvalonDock既定`AnchorablePaneControlStyle`と構造完全一致で
コピー自体に誤りなし。**有力仮説**＝本質は標準WPFの`ContentPresenter(SelectedContent)`初期レイアウト
タイミング問題、メニュー操作のPopup生成がDispatcherのネストしたメッセージループを誘発し保留中
レイアウトを強制フラッシュする副次効果（WPF一般の経験則、完全な一次証明までは未到達）。増分7
固有の問題である可能性は低いと見立てる（既定Aero2メニューも同じPopup機構）が未検証。
**対処方針（本命、提案）**：`PlacementToolBarDockingManager`へ`IsVirtualizingAnchorable="False"`を
1行追加——TabControlEx独自のItemsHolderPanel方式（確実な初期化）が有効化され根本解決が期待でき、
これが効けば対症療法自体が不要になり副作用（メニューハイライト残留）も同時解消の見込み。タブ1つ
のみゆえ副作用（メモリ増）は実害なしと推測。**次善策**（対症療法維持の場合）＝`IsSubmenuOpen=false`
直後にキャンバス等へ`Keyboard.Focus()`を明示（`MenuItem.IsHighlighted`はClearFocusと別管理の
ため、未検証）。**不明点**＝他3つのDockingManager（左パレット/出力/右パネル）で同現象が起きるかは
未確認（理論上同じリスクを抱えるはず）、増分7との関連は既定Aero2メニューでの再現テストが必要
（侍・忍者マター）。
**着手（2026-07-17）**：本命案（`IsVirtualizingAnchorable="False"`）の検証を侍へ采配。
**本命案は根治せず、「観測者効果」と判明（2026-07-17、侍実機検証）**：`IsVirtualizingAnchorable
="False"`実装後も、外部トリガー（UIA `FindAll`・`Start-Ecad2App`内部の`MoveWindow`）に一切依存
しない起動シーケンス（`Start-Process`直叩き→`MainWindowHandle`確定待ちのみ）では起動直後・3秒後
とも配置ツールバー2段目は依然潰れたまま。従来「解決した」ように見えていたのは、外部UIAクライアント
の`FindAll`等プロセス境界を越えた問い合わせが偶然レイアウトをフラッシュしていた**観測者効果**
だった。**アプリ内でAutomationPeerツリーを明示的に構築・走査（`UIElementAutomationPeer
.CreatePeerForElement`→再帰`GetChildren()`、`ContentRendered`イベントで実行）する打開策も
効果なし**（侍実機検証、2026-07-17）——マネージド層のみで完結するAutomationPeer構築と、
外部UIAクライアントが経由する`UIAutomationCore.dll`のプロセス境界越えプロバイダコールバックは
別経路である可能性が高いと侍推定。この方向は打ち切り、作業ツリーは基準状態
（対症療法削除+`IsVirtualizingAnchorable="False"`のみ）へ復元済み、build/test再確認済み。
**殿裁定（2026-07-17）＝さらに調査を続ける**。隠密へUIAutomationCore発火源の追加調査を委譲。
侍はT-100実装へ先行して着手。
**真の発火源、特定（2026-07-17、隠密、一次ソース確認、
`docs/ecad2-t099-uiautomationcore-trigger-survey-onmitsu.md`）**：家老仮説（`DispatcherFrame
.PushFrame`によるメッセージポンプ早回し）は一次ソースで裏付けられず率直に却下——`Dispatcher
.PushFrameImpl`は既存メインループと処理内容が同一のWin32 `GetMessageW`ループに過ぎず、それ自体に
「保留中レイアウトを早回しする」特別な機構は無いと確認。**代わりに真の発火源を発見**＝
`HwndSource.cs`の`Process_WM_SIZE`（1401行）が、`WM_SIZE`受信時にルート`UIElement`へ**同期的な
`Measure()`呼び出しを無条件で強制実行**する実装と確認（コメント「Invalidating layout here ensures
that we do layout」）。侍が確認した「Win32 `MoveWindow`が効く」という実験結果と技術的に完全に
一致する一次証拠。外部UIA `FindAll`が効いた理由は完全解明には至らず推測に留まるが、侍の
「`GetChildren()`再帰では効果なし」という実験結果は本結論（本質は`WM_SIZE`経由の強制`Measure`、
managed層完結のAutomationPeer操作はWin32メッセージを一切発行しない）と論理的に整合。
**対処案（本命、提案）**：アプリ内からP/Invokeで自己の`MoveWindow`（現在の座標・サイズをそのまま
指定=実質無変化リサイズ）または`SetWindowPos`+`SWP_FRAMECHANGED`を呼び出し、意図的に`WM_SIZE`を
発火させる。外部UIAクライアントに頼らずアプリ内で完結する形。呼び出しタイミングは`ContentRendered`
後が妥当と推定（実機比較検証推奨、隠密の調査範囲では実機未検証）。
**着手（2026-07-17）**：本命案（自己`MoveWindow`/`SetWindowPos`呼び出し）の実装・実機検証を侍へ
采配。
**本命案2バリエーションとも不発（2026-07-17、侍実機検証）**：(1)自己`MoveWindow`（無変化）
(2)`SetWindowPos`+`SWP_FRAMECHANGED`、いずれも`ContentRendered`後実行・外部トリガー非依存の
起動シーケンスで確認したが潰れたまま。侍推定＝座標・サイズが実質無変化のため`WM_SIZE`自体が
OS側で送出されない（冗長操作として最適化スキップ）、または送出されても`Measure()`結果が既存
レイアウトと同一で見た目上変化がない、のいずれかの可能性（`Measure()`実際の呼び出し有無は
WndProcフック等の追加計装なしには確認できず）。同一アプローチ（`WM_SIZE`系）2バリエーション
不発につき打ち切り、作業ツリーはT-100コミット状態へ復元済み（実験コードはgit stash退避）。
**家老裁量で診断ログ方式へ切替（2026-07-17）**：確立済みの打開策
（`feedback_diagnostic_log_escalation`メモリ＝コード推論ベースの修正が複数周失敗したら診断ログ
注入で実測）を適用。殿裁定「調査続行」の範囲内の技術的手段選択のため、殿への再確認は行わず着手。
侍へ`HwndSourceHook`によるWndProc計装（`WM_SIZE`受信有無・`Measure()`呼び出しタイミング/結果の
ログ記録）を采配、外部トリガー成功時と自己トリガー失敗時のログを比較する。
**実測結果、WM_SIZE理論を覆す（2026-07-17、侍、WndProcフック+LayoutUpdated計装）**：
`[626ms] WM_SIZE自然発生（起動シーケンス中）→[763ms] ActualHeight=18.00のまま変化なし
（潰れ状態継続）`と実測——**起動シーケンス中に実際のWM_SIZEが自然発生したにもかかわらず修正
されない**ことが判明。さらに`[85782ms] UIA FindAll実行直後にActualHeight=84.00へ正常化、この間
WM_SIZEは一度も記録されず`——**FindAllによる修正はWM_SIZE経由ではないと実測で確定**。隠密の
`HwndSource.Process_WM_SIZE`理論は一次ソースとしては正確だが、本現象の実際の発火源ではなかった
と判明（侍の自己`MoveWindow`/`SetWindowPos`策2案が効かなかった理由も、標的が違っていたと裏付け）。
真の発火源は未特定のまま、推定候補＝UIAutomationCore.dllのプロバイダコールバックがWin32
メッセージを介さずAutomationPeer/UIElementのMeasureを直接誘発している等（侍提案、未検証）。
診断ログコードは作業ツリーに残置（除去せず維持）。
**殿裁定を再度仰ぐ（2026-07-17）**：理論が2周とも実測で覆り、真の発火源はUIAutomationCore.dll
という文書化されていない領域に絞られつつある。継続要否を家老から再度伺う。
**殿裁定（2026-07-17）＝もう1回だけ隠密へ追加調査、これで決着なくば打ち切り**。隠密へ
UIAutomationCoreプロバイダコールバック機構の深掘りを最終ラウンドとして委譲。
**最終ラウンド完了・結論「文書化されておらず特定不能」（2026-07-17、隠密、
`docs/ecad2-t099-elementproxy-final-survey-onmitsu.md`）**：UI Automationコールバックの
COM実装本体`ElementProxy.cs`（`MS/internal/Automation`配下、549行全文精読）にMeasure/Arrange/
UpdateLayout/InvalidateMeasure/InvalidateArrangeへの呼び出しは一切存在せず、コールバック自体に
レイアウト強制コードは無いと一次ソースで確認。**手がかり（確証には至らず）**＝外部UIAクライアント
のコールバックをUIスレッドへマーシャリングする`ElementUtil.Invoke`が
`Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.FromMinutes(3), ...)`という最高優先度の
同期呼び出しを使用——保留中レイアウトの処理順序への副次影響の可能性は排除できぬが、`ElementProxy`
/`ElementUtil`レベルでは因果関係の一次証拠は見つからず。ネイティブ`UIAutomationCore.dll`自体・
Dispatcher内部キューのさらに深い層は「深追いしすぎない」の指示どおり未調査のまま区切り。
**対症療法復元の方針に異論なし**（隠密所見＝対症療法も「UIスレッドへ高優先度処理を挟む」という
点で`DispatcherPriority.Send`経路と間接的に類似し、効果の整合性という意味では筋が通る、推測）。
**打ち切り確定（2026-07-17）**：対症療法（メニュー開閉トリック）を復元し、隠密提案の副作用対策
（`IsSubmenuOpen=false`直後にキャンバス等へ`Keyboard.Focus()`明示）を追加して仕上げる方針で
確定。真因の完全解明は打ち切り、実用上動く状態を確保してT-099本来の要件（ドックタグ表示制御・
幅動的調整）の実装へ進む。侍へ采配。
**対症療法仕上げ完了（2026-07-17、侍、コミット53aab52）**：診断ログ計装コード除去、対症療法
（ファイルメニュー時間差開閉）+副作用対策（`IsSubmenuOpen=false`直後に`Keyboard.Focus
(LadderCanvasHost)`、`ClearFocus()`から変更）実装。起動直後から配置ツールバー2段目正常表示・
複数回起動で再現性確認・ビルド/テスト回帰なし。メニューハイライト残留は「即座」でなく約2秒で
自然消滅（`ClearFocus()`単体だった前回の「残留し続ける」からは改善）。**殿裁定（2026-07-17）＝
この程度の残存挙動は許容、本題へ進む**。T-099のブロッカーはこれにて解消。
**着手（2026-07-17）**：侍へT-099本来の要件（1）ドックタグ表示制御（2）フローティング時タブ表示
（3）幅動的フィット、の実装を采配。
**忍者の「再発」報告は誤診断と判明・撤回（2026-07-17）**：忍者がダークモード切替時に配置ツール
バー2段目が潰れると観測したが、殿が実機画面（人間の目）のスクリーンショットを提供したところ
a接点等のボタン群は正常表示と確認。真因＝**忍者のPrintWindow撮影・UIA探索(FindAll)の両方が
`PlacementToolBarDockingManager`の内容を正しく捕捉できていなかった**（UIA探索でもボタン0件、
PrintWindow画像でも選択ツールのみ表示——両手法が一致して「見えない」と誤示したため手法の限界と
気づきにくかった）。T-080のPDFプレビュー撮影不正確の教訓と同型の事例。T-099対症療法(53aab52)
自体は正常に機能している可能性が高い。**家老裁量で対応**：ecad2-ui-automationスキルへ
「`PlacementToolBarDockingManager`はPrintWindow・UIA双方で正しく捕捉できないことがある」罠を
追記（技術情報のため殿確認不要）。**【報告】**T-100残り観点（配置ツールバー2段目の
DragHandleTexture確認）は、自動検証手法が本パネルで信頼できないと判明したため、殿ご自身の
実機目視での確認待ちとし、忍者にはこれ以上の自動検証手法の深追いをさせない方針とした。新規発見6
（シートパネル・部品選択パネルの一瞬ライトモード化）は別のDockingManager（左パレット側）が
対象で本問題の影響を受けないため、通常どおり検証継続してよいと判断。
**【重大】隠密2が真の根本原因（有力仮説）を特定（2026-07-17、殿直接指示「根本的な実装も疑って
調査せよ」による棚卸し調査、`docs/ecad2-t099-tanaoroshi-shinsou-chousa-onmitsu2.md`）**：終日
追ってきた「外部トリガーで直る謎」は的外れで、真因は**T-099 PoC自身が追加したDataTrigger**
（`MainWindow.xaml:151-153`、HeaderPanelをドッキング時`Visibility="Collapsed"`化）だった公算が
極めて高い。5段の因果を一次ソース（dotnet/wpf `UIElement.cs`/`Panel.cs`/`TabControl.cs`、
AvalonDock`LayoutAnchorablePaneControl.cs`）で裏取り：(1)Collapsed要素はMeasure()が早期return
(2)ItemsHostパネルのTabItemコンテナ生成はパネル初回Measure時の`InternalChildren`アクセスでのみ
発火 (3)パネルが測定されなければコンテナは永遠に生成されない (4)`TabControl`の初期選択・
`SelectedContent`解決はコンテナ生成完了が前提、AvalonDock側にも選択を救う別バインドなし
(5)よって`SelectedContent=null`のまま2段目全体が潰れる。外部UIA FindAll・メニュー開閉が偶然
コンテナ生成を誘発し「一度直れば恒久回復」という観測とも整合。**本日の実測10件全てと無矛盾**
（WM_SIZE不発・`IsVirtualizingAnchorable=False`不発・Peer走査不発の3不発も単一原因で説明）。
**確証は安価**（5分診断＝ContentRendered時にコンテナ生成状態をログ、または即効テスト＝
DataTrigger一時除去でクリーン観測起動）。**確証できれば根本修正＝HeaderPanel自体でなく
TabItemコンテナ側のVisibilityで制御する形へ置き換え**（既存Items.Count=1トリガーを
`Model.IsDirectlyHostedInFloatingWindow`条件のDataTriggerへ置換）——対症療法・
`IsVirtualizingAnchorable="False"`は全撤去可能、T-099要件(1)もこの形で直接実現される見込み。
**着手（2026-07-17、家老裁量・技術検証の範疇のため殿確認なしで進行）**：侍へ確証・根本修正の
実装を最優先割込みで采配。
**【報告・殿お戻り後の確認事項】**：根本修正案ではフロート時にタブが表示される（要件2どおり）が、
フロートウィンドウ自体のタイトルバーとタブが二重表示に見える可能性があり実機での見え方確認を
推奨（隠密2所見）。また選択タブの`Background="White"`固定（AvalonDock既定コピー由来、
`MainWindow.xaml:183`）はフロート時ダークモードで白浮きの見込み、根本修正時に併せて
DynamicResource化を検討中。
**隠密2仮説、実測で確定（2026-07-17、侍）**：DataTrigger（151-153行）コメントアウト+対症療法
コード無効化のみ（`IsVirtualizingAnchorable="False"`等は変更せず）で、外部トリガー完全なし
（`Start-Process`直叩き）のクリーン起動を2回実施、**2回とも起動直後から配置ツールバー2段目が
正常表示**（潰れなし）。「PoC DataTriggerがHeaderPanel(ItemsHost)をCollapsedにしたことが
コンテナ生成をブロックし潰れの真因」と確定。今日一日の対症療法・WM_SIZE系施策は一切不要だった
ことが実証された。
**着手（2026-07-17）**：根本修正（TabItemコンテナ側のVisibility制御へ置換）の実装を侍へ采配。
**要件(1)実装完了（2026-07-17、侍）**：DataTrigger置換・対症療法/`IsVirtualizingAnchorable`
完全撤去・選択タブ背景DynamicResource化、いずれも実装しビルド成功。実機確認＝Light/Dark双方・
複数回起動とも起動直後から正常表示、「配置ツール」タブヘッダーがドッキング時に正しく非表示化。
他パネル（T-100対象のAnchorablePaneTitle）に回帰なし。
**【重要・要殿判断】要件(2)実現に構造的矛盾を発見（2026-07-17、侍）**：フローティング化を検証
すべく試みたところ、**ユーザーがフローティング化する唯一の手段（タブをドラッグする操作）自体が、
要件(1)でタブを完全Collapsed化したことにより失われている**と判明。AvalonDockのドラッグ機構
（DragService）はタブヘッダー限定とみられ、ツールバー本体の余白領域ドラッグではフロート化しない。
DataTrigger自体（`IsDirectlyHostedInFloatingWindow=True`時は通常表示）は理論上正しいが、**そこへ
到達する経路がユーザーに存在しない**（保存済みレイアウトで既にフロート状態なら見た目上は再現
できるが、新規フロート化の手段がない）循環構造。
**【報告・殿お戻り後の判断事項】**：これは実装ミスでなく真のUI/UX設計分岐、家老裁量では決めず
殿のご判断を仰ぐ。候補案（検討材料、いずれも未実装）：(a) ドラッグ起点として機能する最小限の
ハンドル（細い帯等）をドッキング時も残す (b) T-100で扱ったAnchorablePaneTitle領域自体にドラッグ
手段を追加する (c) ドラッグ以外の代替操作（右クリックメニュー「フロート化」・キーボード
ショートカット等）を新設する (d) 完全非表示という要件(1)自体を再検討し、最小化された常時表示
要素（例=殿提示のGX Works3参考画像どおりピン留め+閉じるアイコンのみ残す形）に落ち着ける。
**現状**：作業ツリーは要件(1)実装済みの状態で維持、要件(2)(3)は保留。侍は判断待ちの間、他の
待機列（T-089再開・T-083残り3件=TextBox色/コメント追随/部品アイコン再生成）へ回す。
**副産物＝対症療法コードの取りこぼし発見・裏取り強化（2026-07-17、侍）**：T-089叩き台をstash
から復元しT-099要件(1)の未コミット変更と統合する過程で、対症療法コード（メニュー開閉+
`Keyboard.Focus`）が実は完全に削除されていなかったと判明（`git apply --3way`失敗時のロール
バックが影響と推測）。改めて完全削除し、対症療法なしのクリーン起動2回とも正常表示を再確認——
**根本修正単体での動作が改めて裏取りされ、DoD(2)の確証が強化された**。
**要件(2)実装完了（2026-07-18、侍、未コミット・殿裁可済み案a）**：TabItem全体をドッキング時
Collapsed化する従来方式から、Content(ラベル)のみCollapsed+`DockedDragHandle`(帯、Rectangle、
Height5/MinWidth20)を表示する方式へ置換。帯はT-100で除去した`DragHandleTexture`(ハッチング模様、
意匠のみ)とは別物で当たり判定は残す設計、色は既存`AnchorablePaneTitleNoDragHandleStyle`と同じ
`ToolWindowCaptionInactiveGrip`キーを流用（新規UI/UX分岐でなく既存意匠の踏襲と侍判断・家老確認
済み）。build/test全合格（App.Tests716/Core.Tests120、回帰なし）。**要実機確認**＝(a)ドッキング時
ラベル非表示+帯のみ表示になっているか(要件1再検証) (b)帯をドラッグしてフロート化できるか
(AvalonDock DragServiceがこの帯要素を拾うか未検証、本題)。検証パイプライン既定順どおり隠密静的
レビューへ采配済み（T-067(1)レビューの後）、完了後忍者へ実機確認を回す。要件(3)は要件(2)の実機
結果を見てから着手。
**要件(3)実装完了（2026-07-18、侍）**：ToolBar実幅1244px→523px(UIA実測)、ライト/ダーク両モードで
確認済み。実現方式＝DockingManagerをAuto+Star列のGridで包む（マネージャ自体を無限測定でコンテンツ
幅へ縮める）。**当初のDockWidth="Auto"案は不成立**——AvalonDock仕様（`LayoutPanelControl
.OnFixChildrenDockLengths`、v4.74.1一次ソース確認）で「水平LayoutPanel配下にLayoutDocumentPaneが
無い構成では初回描画時に全子DockWidthをStarへ強制上書き」される既知問題、診断ログ+UIA実測で
DockWidth指定・強制コードとも無意味と証明され除去（KISS）。空き領域の白浮き対策としてラッパー
Gridへ`ToolBarBackgroundBrush`（テーマ連動）設定。
**T-089回帰(2)完治（2026-07-18、侍）**：先のTransparent修正は有効ボタンのみで、**無効ボタン（起動
直後の大半）が明灰色のまま残存**していたと自己検分で発見——真因はControlTemplate内`IsEnabled`
トリガー（優先度4位）の`Button.Disabled.Background`がStyle.Triggers（優先度6位）のTransparentに
勝つ構図（PR-20系統と同型のパターン）。無効時の背景/枠線をControlTemplate.Triggersから
Style.Triggersへ移設し、派生スタイル（ToolBarButtonStyle等）が上書き可能な構成へ変更。画素採取で
無効ボタン=`#2D2D30`（地色と同化）を確認、ダイアログ等の通常ボタンは標準無効表示`#F4F4F4`を維持。
build/test exit0（716/120）、家老検分済み。**忍者へ最終確認4点を依頼**＝(1)幅フィットの見た目
(GX様式か) (2)帯ドラッグ→フロート→再ドックの一連動作+再ドック後の幅維持 (3)無効ボタンのフラット
表示(ライト/ダーク) (4)押下フィードバック(PressedOverlay)とテストモードON色。
**忍者最終確認結果（2026-07-18）**：(1)幅フィットOK（コンテンツ幅に正しくフィット、GX様式）
(3)無効ボタンフラット表示OK（ライト`#F0F0F0`/ダーク`#2D2D30`とも地色と完全一致、画素採取確認）
(2)帯ドラッグ→フロート化は成功（幅541px、移動後も維持確認）、**再ドックはUIA合成マウス操作では
不成立**（AvalonDock DragServiceのインジケーター認識起因の技術的困難と推測、殿実機操作での確認を
推奨・保留）。
**(4)テストモードON色、NG・自己訂正あり（2026-07-18、忍者）**：前回「回帰なし」と報告した判定が
目視のみで期待値との数値比較をしておらず不十分だったと自ら発見・訂正。期待値`TestModeActiveBrush`
=`#E63C00`（オレンジ、Light/Dark共通固定値）のはずが、実測はダーク/ライト両モードとも`#BCDDEE`系
（水色、支配色）——これはStack overflow修正で追加した暗黙的ToggleButtonスタイルの
`Button.Checked.Background`（Aero2既定の水色）と一致。**家老仮説**＝無効ボタン対応時と同型の
罠——ControlTemplate.Triggers内`IsChecked`トリガー（優先度4位）が、`TestModeToolBarButtonStyle`
独自のStyle.Triggers（優先度6位、`IsChecked=True→TestModeActiveBrush`）を握り潰している（侍が
T-099要件2作業時に事前警告していた範囲外懸念が的中）。証拠画像=
`docs-notes/screenshots/t089-testmode-wrong-color-5x.png`。侍へ最優先修正を采配——無効ボタン対応
（IsEnabledトリガーをControlTemplateからStyle.Triggersへ移設）と同型の対処をIsCheckedトリガーにも
適用する方向で検討されたし。
**押下フィードバック、参考所見（2026-07-18、忍者、検証一区切り）**：「新規」ボタン押下中の色を
実測（`#9DB7C5`系、青系）——通常時`#F0F0F0`からは変化しているが水色寄りでIsMouseOver色に近い
印象、テストモード色バグと同根（`IsMouseOver`トリガーが他トリガーより優先される疑い）の可能性を
指摘（参考情報、断定せず）。瞬間的視覚状態ゆえ確証には限界あり、最終確認は従前どおり殿ご自身の
実機操作に委ねる方針。忍者の一連の検証はこれにて一区切り。
**(4)テストモード色バグ、修正完了・自己確認済み（2026-07-18、侍、家老検分済み）**：真因は家老仮説
どおり（テンプレート内`IsChecked`トリガー優先度4位が`TestModeActiveBrush`[6位]を握り潰し）。対処＝
(1)`IsChecked`をStyle.Triggersへ移設（IsEnabled対応と同型） (2)**同じ罠の再発を構造的に防止**する
ためMouseOver/Pressed/IsDefaultedも同時にStyle.Triggersへ移設（テンプレート側は構造のみ=
PressedOverlay表示/無効文字色に統一。忍者所見「押下中が水色寄り」も以後は派生側で調整可能に）
(3)`TestModeToolBarButtonStyle`のIsChecked/MultiTriggerへ`BorderBrush=Transparent`追加。自己確認＝
実機で新規作成→テストモードON→支配色集計、`#E63C00`が1105px(圧倒的支配色)=`TestModeActiveBrush`
と完全一致。build/test exit0（716/120）。正規binにT-044診断ログ計装も反映済み、忍者へログ取得と
テストモード色再確認を采配。
**隠密静的レビュー完了（2026-07-18）**：ControlTemplate.Triggers内4件の重複なし、`ToolWindow
CaptionInactiveGripキー`は単色SolidColorBrush（ハッチング模様は別要素`DragHandleTexture`が担う、
T-100調査書で一次ソース確定済み）ゆえ模様混入の懸念なし、PR-20系統（値は正しいが反映されない罠）
にも該当せず（ecad2独自ControlTemplateの直接改変ゆえ）。code-reviewスキルも指摘なし。静的観点では
問題なし、忍者の実機確認（帯のドラッグでフロート化できるか、本題）をキュー追加済み。
**実機確認・本題成功（2026-07-18、忍者）**：帯（相対X=22,Y=110）を350px/50ステップでドラッグした
ところ、独立フロートウィンドウとして正しく切り離された（EnumWindowsで新規ウィンドウ検出・
キャプチャで「配置ツール」タイトル+ツールボタン群を確認）。AvalonDock DragServiceが帯要素を
正しく拾うことを実証。残る(a)ドッキング時ラベル非表示+帯のみ表示、確認中。
**要件(2)、完全決着（2026-07-18、忍者）**：(a)も確認OK——当初「配置ツールの上部青タイトルバー」を
対象と誤解していたが、これはAvalonDock標準ペインタイトル（変更対象外）と自ら気づき訂正。真の対象
はパネル下端の小さいタブ（UIA発見、rel X16,Y175,W40,H10）——4倍拡大画像で灰色の細い帯のみ・ラベル
テキストなしと確認、正しく機能。フロート化後は同位置に「配置ツール」ラベル付きタブへ変わることも
確認済み。**T-099要件(2)、本題・(a)とも全OK、完全決着**。要件(3)（パネル幅の動的フィット）へ着手
可能な段階。

### T-089 ボタン押下状態の視覚的明示化 — Approved（gated、殿直接指示2026-07-14、P-091起票）

**起票=P-091（殿直接要望2026-07-14）を殿裁定でタスク化**。ツールバー・メニュー等アプリ全体の
ボタンについて、押したときに押されている（押下中）ことが視覚的にわかるようにしたい。**対象範囲
＝全ボタン共通（殿裁定2026-07-14）**。家老grep確認＝`IsPressed`の言及がXAML側のButtonスタイル
定義に見当たらず、WPF標準Buttonの既定描画（プラットフォーム依存の薄いフィードバック）に任せて
いる状態。
**着手前調査要**：現状の全ボタン（ツールバー配置系F5-F10・「自作パーツ」・OK/キャンセル系・
テストモードトグル等）のスタイル定義箇所を洗い出し、共通スタイルへ`IsPressed`トリガーを
追加する方式が妥当か、個別スタイルが必要な箇所がないか調査する。UI/UX分岐（押下時の具体的な
視覚表現＝色変化・枠線・影・スケール等）は着手時に殿確認【MUST】。

**着手前調査完了（2026-07-14、隠密、`docs/ecad2-t089-button-style-survey-onmitsu.md`）**：
「共通スタイル1箇所への`IsPressed`追加で全ボタンに反映」は不可（名前付き共通スタイルは
`MainWindow.xaml`の`Window.Resources`にスコープが閉じており23個のみ対象、ダイアログ6ファイル
の30個超はスタイル指定なし＝スコープ外、ButtonとToggleButtonは型制約でBasedOn不可）。
**構造変更が必要**：`App.xaml`の`Application.Resources`へ`TargetType="Button"`/
`TargetType="ToggleButton"`の暗黙スタイルを新設し、既存3スタイルをBasedOnさせる構成に
変更する必要あり（規模は「Style 1箇所追加」より大きくなる）。ToggleButton側は
`TestModeToolBarButtonStyle`の既存`IsChecked`トリガーとの競合に`MultiTrigger`等の個別対応要。
**殿裁定（2026-07-14）＝スコープ・進め方確定**：(1) 未配線のプレースホルダボタン（未定アイコン
2個・拡張表示ボタン、Click未配線・TabStop無効）は**対象外**（「押せそうに見えて無反応」の
誤解助長を避けるため。将来Click配線時に併せて対応） (2) 押下時の具体的な視覚表現は**侍が
叩き台を実装しプレビューを殿へ提示、殿が選ぶ形で決定**（T-048グリフ変更と同型の進め方）。
**叩き台実装完了・殿プレビュー待ち（2026-07-17、侍）**：案A（半透明オーバーレイ型、
`IsPressed=True`時`Background=#33000000`）を実装、ビルド成功・目視でコード上の誤りなしを確認。
**実機での視覚効果確認は限界あり**——`SetCursorPos+mouse_event`による押下状態の静止画キャプチャを
試みたが、既存`IsMouseOver`等のフィードバックと視覚的に紛れる・タイミング制御の粗さから確実な
確証は得られず。**押下時の実際の見え方は殿ご自身の実機操作でのご確認が必要**。作業ツリーは
T-099要件(1)の未コミット変更と同一ファイル（MainWindow.xaml）で統合された状態のまま維持
（【報告】、殿お戻り後にT-099要件(2)方針とあわせてご確認いただく）。
**案A、原理的に対処不能と判明・ControlTemplate自作へ転換（2026-07-18、隠密一次ソース調査、
`docs/ecad2-t089-button-pressed-feedback-investigation-onmitsu.md`）**：Style.Triggersでの
Background上書きは既定Aero2 ControlTemplate内のControlTemplate.Triggers（優先度4位）に握り潰される
と確定（PR-20新規パターン3例目）。対処＝増分7と同型のControlTemplate部分自作+PressedOverlay
(Rectangle)追加、既存の色変化ロジック(MouseOver/Pressed/Checked/Disabled)は温存。
**実装時にStack overflowインシデント発生・解消（2026-07-18、詳細は本ファイル冒頭「進行中
インシデント」節参照）**：既定テーマスタイル置き換えでStyle本体の既定値Setterが失われUnsetValue
例外の無限ループに陥ったが、10キー明示定義+既定値Setter群追加で解消・実測確認済み。
**忍者実機検証で重大な回帰2件、新規発見（2026-07-18）**：(2)ツールバーのボタン背景がダークモードで
明るいグレーのまま変化せず浮いて見える——新設Styleの`Background`が`{StaticResource
Button.Static.Background}`（Aero2固定のライトグレー#FFDDDDDD、テーマ非依存）になっており、以前は
透明で下地(ToolBar黒背景)が透けていたのが覆い隠される回帰と推測（家老仮説、要検証）。(3)右パネル
（機器表・プロパティ、AvalonDock）がダークモードに一切追従せず白背景のまま(3回再現)、原因未特定。
(4)気づき＝無効化ボタン(戻す/再実行/一時停止)のダークモード時アイコン視認性が低い。BorderBrush
副作用懸念(枠線消失)はNG無し確認。侍・隠密へ並行調査を委譲、他作業は一時中断。
**中間報告（2026-07-18、隠密）**：(2)確定——Stack overflow修正時に追加したStyle本体の
`Background="{StaticResource Button.Static.Background}"`（Aero2固定薄グレー）が常時効いており、
以前はBackground未指定＝実質透明だったため下地(黒)が透けていたのが浮いて見える回帰と確定。
(3)は仮説段階——**T-089との直接関連は見出せず**、DataGrid/ListBoxの`DynamicResource
PanelContentBackgroundBrush`等は無変更のまま健在（コード上は正しくダーク追従するはず）。むしろ
**T-083増分5当時の「対応済み」判定自体が誤解だった可能性**（プロパティパネルが暗く見えたのは
中身対応でなく、背後の層B=AvalonDockペイン背景`#252526`がたまたま暗色で透けていただけ、という
構造）を指摘。忍者へ画素採取での裏取りを依頼中——確定すれば増分5の完了記録の訂正が必要になる
重大な話。
**続報（2026-07-18、隠密）**：増分5・7の唯一の実機検証記録
（`docs-notes/ecad2-t083-zoubun5-zoubun7-verify-ninja.md`）を精読——対象3件は「Ctrl+Tabナビゲータ
波及確認」「DataGridCell選択色回帰確認」「メニューダーク対応」のみで、**DataGrid/ListBox本体の
通常時（非選択）の地の背景色そのものを画素採取で確認した記録が、この検証書にもそれ以前の記録にも
見当たらない**。仮説を訂正——(3)の真因はT-089/Stack overflowの回帰ではなく、**増分5「完全決着」
判定自体が当時から検証範囲の漏れ（選択色等の派生観点のみ確認し、最も基本的な地色を見落とした）
だった可能性が高い**。層Bの固定ダーク色(#252526)により画面全体が「それらしく暗く見えた」ことが
見落としを助長した可能性（推測）。コード自体（App.xaml DataGrid/ListBox暗黙的スタイル）は変更
不要かもしれず、確定すれば台帳の完了記録訂正のみで済む見込み。忍者へ「データがある状態」での
地色の画素採取確認を依頼中（現状シート・機器表は空のため、部品配置後に確認要）。
**訂正・(3)は目視誤読だったと確定（2026-07-18、忍者、画素採取実測）**：SheetNavList空白部・
DeviceTableGrid本体とも`#2D2D30`で期待値`PanelContentBackgroundBrush`と一致、プロパティパネルは
`#252526`（層B地色）——いずれもダーク配色として問題なし。**新たな小さな発見**＝
DeviceTableGridの**ColumnHeader行**（「機器名 種別 型式」ラベル部分）のみ白いまま残存（PR-18
同型の対応表漏れの可能性、隠密調査中だが優先度低=視認性のみ）。
**データ行確認完了（2026-07-18、忍者）**：部品(a接点、X001)配置後実測——DeviceTableGridデータ行
余白部は`#2D2D30`で期待値`PanelContentBackgroundBrush`と完全一致・OK。**新たな軽微発見**＝
SheetNavList選択中項目行が`#565659`（中間グレー）で、期待される選択色
`PanelContentSelectedBackgroundBrush`(#0E639C)と不一致——ListBoxがフォーカスを持たぬ際にWPF既定の
「非アクティブ選択色」にフォールバックしている可能性（`Selector.IsSelectionActive`をトリガーに
含めていない疑い、忍者所見・断定せず）。実害は視認性の微妙な差異のみ、優先度低。これにて忍者の
検証依頼は一区切り、Ecad2.App停止・侍の(2)修正ビルド待ち。
**ColumnHeader白残存、統合仮説（2026-07-18、隠密、区切り）**：侍の時系列調査（旧スクショでは
ColumnHeaderもダーク、忍者の10:25再検証=Stack overflow修正後ビルドで初めて白と観測）を受け、
App.xaml/MainWindow.xamlともDataGrid/ColumnHeader関連の差分は0件（静的経路なし）と確認した上で、
**Stack overflow修正で削除されたT-099対症療法コード（起動後にファイルメニューを一瞬開閉する
処理）が、副次的にDataGridColumnHeaderの初回描画タイミングにも恩恵を与えていた可能性**を提示
（T-099調査サーガの「メニュー操作がAvalonDockコンテナ生成を偶然誘発」と同型のロジック）。対症療法
削除でその偶然の恩恵も消え、増分5当時から潜在していた初期化タイミング問題が露呈した、という解釈。
確証は実機のみとし調査は一旦区切り、優先度低のため他作業を優先。

### T-101 配置ツール選択中ツールの恒久的ハイライト表示 — Approved（gated、殿直接指示2026-07-19）

**起票=殿直接指示2026-07-19**。T-089実機確認で殿より「選択されていた場合に水色に変化したものが
元にもどってしまっている」との回帰報告があり調査した結果、T-089由来の回帰ではなく**元々の仕様
欠落**と判明（隠密調査、`git show`によるT-089着手前・T-040導入時点双方の確認）。配置ツール
ボタン群（a接点配置等、`PlacementToolBarButtonStyle`）は単純Button（ToggleButtonでない）のため
`IsChecked`概念自体が無く、現在有効なツール（`ViewModel.Tool`）を恒久的に示す視覚的インジケーター
がT-040導入時点から存在しない。現状の色変化はMouseOver/Pressedにのみ依存する一時的なもので、
カーソルを離すと選択中でも通常色（#F0F0F0）に戻る。

**内容**：現在有効なツール（`ViewModel.Tool`）に対応する配置ツールボタンを、カーソル位置に
依存せず恒久的にハイライト表示する機能を新設する。

**UI/UX分岐は着手時に殿確認【MUST】**（`karo.md`「UI/UX・使用感に関わる分岐は必ず殿へ」）：
具体的な視覚表現（背景色・枠線・アイコン等）、対象範囲（配置ツールボタン群のみか他ツール系も
含むか）は未確定。

### T-083 ダークモード搭載（AvalonDock連動、T-058と同時実装） — Approved（gated、殿直接指示2026-07-12）

**起票=殿直接指示2026-07-12**「AvalonDock VS2013はダーク/ライトテーマが選択できそうだが、ダークモードも
搭載できるか」→「T-058と同時にAvalonDock連動で実装をタスク起草して」。棚卸し39項目の後日判断バケット
（殿裁定2026-07-11）にあった「ダークモード」のタスク化。
**家老の事前検分（2026-07-12）＝3層構成**：
(1) **作図キャンバス色＝Core下地完備**：`src/Ecad2.Core/Rendering/DrawingTheme.cs`に`Default`（白地・黒線）/
`Dark`（暗地・明線）の2パレット定義済み。意味色（通電・接続済み・手動強制）はテーマ間固定の設計済み、
設計コメントに「画面はメニューの『ダークモード(作図色)』で切替、**PDFは常にDefault**（提出図面は白地黒線）」
の運用構想あり。**App層の切替メニュー・結線は皆無**（Core完備・App結線待ちパターン）。App層の直接色指定
15件（LadderCanvas.cs 7件ほか計6ファイル）のテーマ整合確認要。
(2) **UIクローム（メニュー・ツールバー・ダイアログ等）**＝.NET 10のWPF標準Fluentテーマ+ThemeMode
（Light/Dark/System）がネイティブ候補（新規依存なし）。成熟度・既存UIとの相性は**着手時に隠密調査で
裏取り必須**（.NET 9導入・10で改善継続の経緯ゆえ評価中機能の可能性あり）。
(3) **AvalonDockドッキングクローム**＝VS2013 Dark/Lightテーマ（T-058のテーマ7種必須構成の内）と
(1)(2)を連動切替。
**着手=T-058と同時（殿指示）**：T-058のPoC→本実装の増分計画へT-083を組み込む。PoC時に隠密へ
Fluent ThemeMode成熟度確認を併せて振る。UI/UX分岐（切替UIの形・メニュー配置・起動時既定・OSテーマ
追従の有無・切替の即時反映方式等）は着手時に殿確認【MUST】。
**メニュー配置、殿直接指示で確定（2026-07-15）**：既存「表示(_V)」メニュー
（`MainWindow.xaml:157`）へダークモードのトグル項目を追加する形とする。起動時既定・OSテーマ
追従の有無・切替の即時反映方式等、残る論点は引き続き着手時に殿確認【MUST】。
**PoC完了（2026-07-15、侍、コミット8a24318）**：作図キャンバス色のテーマ切替、忍者実機確認OK
（往復含め正常）。**範囲外の気づき（忍者2026-07-15）**：シートが0件の時のみトグルOn/Offに関わ
らずキャンバスが固定の濃紺色になる（シート追加後は正しく追従）。本実装（増分計画）着手時に
解消すべき項目として記録、PoCスコープ外のため今回は対応不要。
**本実装UI/UX論点、殿裁定（2026-07-16）＝確定**：(1) 起動時既定＝**ライトモード** (2) OSテーマ
追従＝**なし**（アプリ内トグルのみで手動切替） (3) 切替反映方式＝**即時反映**。
**本実装着手前調査完了（2026-07-16、隠密、`docs/ecad2-t083-honjissou-3layer-design-survey-onmitsu.md`）**：
(1) Fluent ThemeMode＝不採用維持（既存調査どおり、.NET 10でも`[Experimental]`のまま）、UIクローム
（層C）は自前`ResourceDictionary`（`Theme.Light/Dark.xaml`）切替方式で実現 (2) 3層連動設計＝
層A(作図キャンバス、PoC結線済み)／層B(AvalonDock、`Dirkster.AvalonDock.Themes.VS2013`新規導入、
T-058節で採用決定済み)／層C(UIクローム、自前方式) (3) 増分4分割案＝増分1:AvalonDock連動(層B)／
増分2:UIクローム基盤構築+シート0件時キャンバス色固定バグ解消(層C基盤)／増分3:UIクローム残箇所
(層C残り)／増分4:選択色系(暗背景視認性)の実機確認・調整。
**永続化要否、殿裁定（2026-07-16）＝不要**（起動時は常にライトモードへ戻す、前回設定の記憶機構は
実装しない）。
**着手（2026-07-16）**：侍へ増分1（AvalonDockテーマ連動）から着手を采配。
**増分1、層B（AvalonDockドッキングクローム）を保留（殿裁定2026-07-16）**：忍者実機確認でタブ・
タイトルバーがテーマトグルに無反応と判明（コミット0f63245+5020811+21d58fb）。侍・隠密のWチェック
で技術トライアル7回相当（Theme設定の反映機構調査、暗黙的スタイル自動再解決の既知制約検証、
ItemContainerStyle追跡等）を尽くすも根本原因未特定——ただしテーマのリソース機構自体は正常と
実証済み（`AnchorablePaneControlStyle`/`TabItem`のBackgroundともにDynamicResource解決は正しく
動作、パッケージ・AvalonDock本体・アプリ側ロジックいずれも問題なしと除外済み）。新たな手がかり
（実際に見えるタブ/タイトルバーはTabItemではなく別の描画要素＝推定`LayoutAnchorableControl`等の
可能性）はあるが、この先の調査は長期化リスクありと判断。**殿裁定＝層Bは保留し増分2・3（UI
クローム）を先に進める、後日体制を立て直して再挑戦可能**。現状のコード（Theme設定自体は機能上
害なし、視覚反映のみ未達）はそのまま残置。層A（作図キャンバス色）・観点1（起動時Light既定）・
観点3・5は確認済みOKにつき増分1はこれにて区切り。
**増分1層B、隠密2へ再調査を委譲（殿直接指示2026-07-16）**：旧隠密・侍の既存調査結果を踏まえ、
`LayoutAnchorableControl`等の実際の描画要素特定に焦点を当て独立調査中。
**増分2、メニューバー背景色も保留（殿裁定2026-07-16）**：ツールバー本体・シート0件時キャンバス
色固定バグは解消確認済み（`Theme.Light/Dark.xaml`新設・`App.xaml`統合・`MergedDictionaries`
差替え実装）。ただしメニューバー（MenuBarArea）の背景色のみダークモードに無反応と判明——値は
正しく更新される（診断ログで`#FF2D2D30`確認）が画面描画に反映されない、**増分1層B
（AvalonDock）と同型の「値は正しいが描画されない」現象**。Menu.Template書き換え・TemplateBinding
/通常Binding双方で切り分けたが解消せず（直接値=Purple等は反映される、Style/Template適用自体は
生きている）。**殿裁定＝メニューバーも保留、増分3へ進む**。層B調査（隠密2）が先に解決すれば
共通原因の可能性が高く、その知見を踏まえ再挑戦する。
**増分1層B・増分2メニュー背景色、「バグは存在しなかった」と判明（隠密2・忍者、2026-07-16）**：
隠密2が忍者撮影PNGの画素実測・GitHub一次ソース確認・4経路実測により、層B（AvalonDockドッキング
クローム）は増分1時点で既に正しく機能していたと特定（観点2・4のNG判定は目視誤読、青い帯＝
アクティブキャプション色はLight/Dark両テーマ共通値＝仕様のため変わらないのが正しい）。忍者が
画素採取で再検証し観点2・3・4を全てOKへ訂正（前回の目視誤読を撤回、`docs-notes/verification-
ninja-t083-zoubun1-recheck.md`）。侍のみ「メニューバー効果なし」を診断ログ・クリーンビルド後も
再現していたが、**真因は環境差異ではなくUIA座標取り違え**（`ControlType.MenuBar`をFindFirstすると
アプリのメニューでなくOSタイトルバーのシステムメニュー領域＝y≦31を誤って掴む罠、侍の申告座標
y≈18〜38がこの領域と重なっていた）と隠密2が特定（同一マシン5セッション体制ゆえ環境固有差異説は
構造上成立せず）。侍の最終確認（正しい座標での再測定）を待って完全決着とする。詳細・再検証用
期待値表は`docs/ecad2-t083-zoubun1-layerb-shinsou-chousa-onmitsu2.md`（追記含む）参照。
**教訓＝テーマ・配色の実機検証は目視でなく画素採取を標準とする**（ecad2-ui-automationスキルへの
追記候補）。
**増分3、TextBox入力欄の白浮き完全解消（2026-07-16）**：忍者検証でNG確定（隠密の事前指摘的中）
→侍が新規`InputBackgroundBrush`/`InputForegroundBrush`を追加し全箇所へ適用（コミット5867a51）
→忍者の往復2周目再検証で全箇所#3C3C3Cへ正しく修正・回帰なしを確認。増分3完全完了。
**増分5（新規）、ペイン内コンテンツのテーマ未対応（殿直接指摘・隠密2調査2026-07-16）**：シート
一覧（ListBox）・機器表/出力パネル（DataGrid）の中身が明示色指定なしでWPF既定の白のまま。
プロパティパネルのみ暗く見えるのは中身が透明コンテナで層Bのペイン背景(#252526)が透けているため
（忍者が「プロパティ中身の暗色化」として観測していた現象の正体）。着手前調査の色指定一覧が
「明示的な色指定のgrep」で作られたため、既定値のコントロールが増分計画から漏れていた構造的な
穴と判明。**殿裁定＝T-083の一環として増分5で対応**。詳細・対応表は同調査書「追記2」参照。
**増分6（新規）、配置ツールバー(2段目)のダークモード未対応（殿直接指摘2026-07-16）**：侍が増分1
時点で「2段目はAvalonDock管轄(層B)のため対象外」とコメント（MainWindow.xaml:195）した判断が
誤りだったと判明。層B（AvalonDock独自クローム＝タブ・タイトルバー）とパネル中身（通常WPF
コンテンツ＝`PlacementToolBarButtonStyle`/`PlacementToolBarIconStyle`等）は別物で、パネル中身は
1段目`ToolBarArea`同様に層C扱いで対応可能（侍の技術的整理、家老裁可）。**家老裁量にて対応承認**
（範囲内の見落とし修正、UI/UX新規分岐ではない）。**完了**（コミットec0707a、実機確認済み）。
**増分7（新規）、ドロップダウンメニュー背景のダーク対応 — 打ち切り（2026-07-16）**：緊急対応
（メニュー全体Light固定）を殿が実機確認、「上メニューもダーク対応にしたい」とのご指示を受け
本格対応に再挑戦。侍の調査で、サブメニューPopup内の実背景要素`SubMenuBorder`(Border)が
TemplateBinding経由で親MenuItem自身のBackgroundを参照する構造と特定（`DependencyPropertyHelper
.GetValueSource`で実測）。しかし`SystemColors.MenuBrushKey`オーバーライド・暗黙的Style適用の
いずれも試すも値操作が実描画に反映されず、**増分1層Bと同種の構造的制約**に直面。次善手段は
MenuItem用ControlTemplate（Role別=TopLevelHeader/SubmenuItem等）の完全自作だが、相当な作業量・
回帰リスクを伴う見込みのため、時間対効果を鑑みここで打ち切り。一時調査コードは除去し、コミット
ec0707a（メニュー全体Light固定、実害である白地白文字は解消済み、ダーク非連動のみ残存）の状態を
維持。**再挑戦は次セッション以降、要検討**（ControlTemplate完全自作の要否・規模を含め）。
**増分7再挑戦・方針再調査完了（2026-07-17、隠密、`docs/ecad2-t083-zoubun7-menu-dark-redesign-survey-onmitsu.md`）**：
一次ソース（dotnet/wpf本家`MenuItem.xaml`/`Menu.xaml`のAero2セクション）を精読し根本原因を特定
——`SubMenuBorder`（ドロップダウン全体の背景）は`StaticResource Menu.Static.Background`参照ゆえ
DynamicResourceオーバーライドが原理的に届かない**真の構造的制約**（増分1層Bの検証ミスとは異なる）。
侍実測の「TemplateBinding経由」要素は別役割（`SubmenuItemTemplateKey`側の`templateRoot`＝各項目
自体の背景、これは健全）を指していたと判明。**推奨案＝完全自作でなく、WPF既定Aero2の
ControlTemplateをコピーし色参照のみDynamicResource化する「派生テンプレート」**（対象2種＝
`TopLevelHeaderTemplateKey`・`SubmenuItemTemplateKey`、新規ブラシキー7〜8種、規模中）。実装順序
＝「ドロップダウン側→メニューバー本体復活」の順必須（逆順だと白地白文字再発）。代替アプローチ
（サードパーティテーマライブラリ・Fluent ThemeMode・DWM API）はいずれも非推奨で一致。
**家老裁量にて範囲確定（2026-07-17）**：現状未使用の`TopLevelItemTemplateKey`・
`SubmenuHeaderTemplateKey`（サブサブメニュー用）は今回省略——現行メニューは2階層のみで
サブサブメニューが存在せず、UI/UX上の見え方に影響しない純粋な実装スコープ判断のため
（品質哲学KISS・過剰実装回避）。将来サブサブメニュー追加時に併せて対応する。
**着手（2026-07-17）**：侍へ実装を采配。
**増分4（選択色系の暗背景視認性確認）完了（2026-07-17、忍者、画素採取＋WCAGコントラスト比、
`docs-notes/ecad2-t083-zoubun4-verify-ninja.md`）**：観点1（ダーク背景での選択ハイライト判別）・
観点3（ライトモード回帰なし）は画素採取で実測OK（背景`#202226`/選択枠OrangeRed`#FF4500`とも
理論値と完全一致）。観点2（3色の暗背景コントラスト）はOrangeRed=実測OK（コントラスト比約
4.63:1）、**DodgerBlue（約4.92:1）・White（約15.93:1）は理論値のみ**——記入中プレビュー・
画像リサイズハンドルの実機動的再現がUIA単発クリックで不安定（ツール状態が意図せずSelectへ
遷移／ボタンInvoke自体が例外）なため打ち切り。3色とも`LadderCanvas.cs`で同型の
`Pen`/`Brush`描画・テーマ非依存の固定色ゆえ、未実測分もOrangeRedと同様に理論値どおりと
推定される（実測ではない旨を明記）。NG無し、リスク低と判断し家老裁量にて増分4を決着とする。
**増分5、隠密静的レビューでCONFIRMED指摘（2026-07-17）**：DataGridRowへのIsSelectedトリガー
追加のみでは選択色が反映されぬ懸念——WPF既定DataGridCell(全テーマ共通の暗黙的Style)が
`IsSelected=True`時にSystemColors.HighlightBrushKeyでBackground/Foreground/BorderBrushを
上書きする既定Triggerを持つ(dotnet/wpf `DataGrid.xaml`一次ソース裏取り済み)。DataGridCellにも
同様の暗黙的スタイル追加が必要と判定、家老裁量で範囲内欠陥修正として侍へ差し戻し。
**要確認指摘（同、実機）**：追加したListBox暗黙的スタイルがAvalonDock既定Ctrl+Tabナビゲータ
内部の生ListBox(`PART_AnchorableListBox`等)へ意図せず波及する可能性、忍者実機確認へ回付。
軽微指摘(`PanelContentBackgroundBrush`等が`DialogBackgroundBrush`等と値完全一致=二重管理)は
修正必須とせず経過観察扱い。
**増分7、隠密静的レビュー完了（2026-07-17）**：狙い撃ち観点3点(ComponentResourceKey派生構造・
各Trigger欠落なし・実装順序遵守)はいずれも良好。新規発見(軽微〜中)＝既定Aero2
`TopLevelHeaderTemplateKey`にある7個目のTrigger(`SubMenuScrollViewer`のCanContentScroll、
サブメニューにスクロールバーが実際に表示される場合のみ影響)が派生に含まれず——現行メニューは
各6〜7項目程度でスクロールバー非表示につき顕在化しないと見立て、家老裁量で**経過観察**
（低解像度環境・将来の項目追加時に再検討）。要実機確認＝`MenuItem.SeparatorStyleKey`
オーバーライドの解決可否（9箇所実在、視覚影響あり得るためWPF内部実装依存で一次ソースからは
確定不可）、忍者の実機確認へ回付。チェックマークStroke代替は技術的制約として妥当と確認済み。
**増分5・増分7、忍者実機検証完了・完全決着（2026-07-17、画素採取実測、
`docs-notes/ecad2-t083-zoubun5-zoubun7-verify-ninja.md`）**：3件（Ctrl+Tabナビゲータ波及確認・
DataGridCell選択色回帰確認・メニューダーク対応全観点）とも全てNG無し、実測値がTheme.*.xaml
理論値と高精度で一致（半透明ブラシは背景合成計算値で照合、Dark側の差1はPNG圧縮丸め誤差の範囲）。
セパレータ色・スクロールTrigger漏れの懸念点も実害なしと確認。増分5・7ともこれにて完了。
**新規発見1（殿直接指摘2026-07-17）：プロパティパネルのダークモード時、ラベル文字色が黒固定の
バグ**：`種別`/`デバイス名`/`ラベル高さ調整`等のラベルテキストが既定黒のまま残存し、層Bペイン
背景(#252526)が透ける暗背景と衝突し可読性低下。増分5の着手前調査（対応表）は「シート一覧
(ListBox)・機器表/出力パネル(DataGrid)」の5コントロールが対象で、プロパティパネルの背景透過は
把握済みだったが中身のラベルテキスト色までは対応表に含まれていなかった見落とし。範囲内欠陥
修正として侍へ差し戻し。
**新規発見2（殿直接指摘2026-07-17）：ドッキング済みタブのヘッダー領域に常時ハッチング模様
（斜線パターン）が表示される**：タブラベルと固定アイコン(ピン留め/閉じるX)の間に余白がある際、
その余白部分にAvalonDock/VS2013テーマ既定の装飾的背景パターンが表示される。**殿確認＝全ドック
共通の事象**（特定パネル限定ではない）。T-099の要件3（幅動的フィット）とは別パネルでの観測だが
同根（タブストリップの余白部分の表示）の可能性があり、共有スタイル側の対応が妥当と見立てる。
新規タスクとして起票し侍へ調査・対応を依頼（→T-100）。
**新規発見3（殿直接指摘2026-07-17）：配置ツールバー1段目（新規/開く/保存/戻す/再実行/PDF出力/
行追加/行削除/テスト/一時停止）のアイコンがダークモードでネガポジ反転されておらず視認困難**：
2段目（配置ツール、F5/F6/F7等）は増分6で`PlacementToolBarIconStyle`によりアイコン色までダーク
対応済みだったが、1段目（`ToolBarArea`）は増分2で背景色のみ対応し、アイコンのGeometry Fill色は
theme非対応のまま据え置かれていた見落としと推定（着手時に要実測確認）。範囲内欠陥修正として
侍へ差し戻し。
**新規発見1・3、隠密静的レビュー完了（2026-07-17、コミット1648d2b）**：狙い撃ち観点（1段目全10
アイコンが`ToolBarIconStyle`経由で統一、個別Fill/Stroke指定なし）は良好、見落としなし。
**cleanup指摘2件（低severity、修正不要）**：TextBlock/CheckBox 8箇所への`Foreground`個別付与が
冗長（継承で代替可）／`PlacementToolBarIconStyle`のStroke再指定が本コミットでデッドコード化し
コメント（MainWindow.xaml:93-94「2段目専用キーのため1段目には影響しない」）が事実と矛盾——実害
なしだが保守性のため追随修正が望ましい。
**完了（2026-07-17、侍、コミット3165d49）**：コメントを事実に追随させ修正。
**T-083待機列3件、全完了（2026-07-17）**：新規発見4（TextBox5箇所色対応、8cf7e87）・新規発見5
（部品アイコンダーク対応、399019d）・cleanup（コメント追随、3165d49）、いずれも実装・実機確認・
build/test回帰なし・コミット済み。**残る未コミット分はMainWindow.xaml/App.xaml/
MainWindow.xaml.cs（T-099要件1+T-089叩き台、殿確認待ちのため意図的に留保中）のみ**。
**新規発見4（隠密発見2026-07-17、範囲内欠陥・PR-18確認）：プロパティパネルのTextBox5箇所
（DeviceNameBox/NotchPositionBox/LampColorBox/SetpointBox/LabelDyBox）がInputBackgroundBrush/
ForegroundBrush対象外のまま残存**——増分3で確立したはずの入力欄対応表からも漏れていた。殿提示の
実機画像（X1/0入力欄の白浮き）と符合する実害と確認。新規発見1と同根、`docs-notes/
pattern-recurrence-log.md` PR-18（色対応調査の網羅性不足、3例判明で正式パターン確定）として記帳
済み。範囲内欠陥修正として侍へ差し戻し。
**完了（2026-07-17、侍、コミット8cf7e87）**：`InputBackgroundBrush`/`ForegroundBrush`適用。実機
確認＝DeviceNameBox/LabelDyBoxの2箇所（暗背景+白文字で正常表示、ライト復帰も回帰なし）、残り3箇所
（NotchPositionBox/LampColorBox/SetpointBox、セレクトSW/表示灯/タイマ接点選択時限定表示）は同一
パターンでの機械的追加のためコード目視で担保。build/test回帰なし。混在していたMainWindow.xamlから
該当12行のみを`git apply --cached --recount`で的確に抽出しコミット、範囲外混入なし。
**新規発見5（殿直接指摘2026-07-17、範囲内欠陥）：部品選択パネルの部品アイコンがダークモードで
黒色のまま**：家老の一次調査で原因特定——`PartThumbnailRenderer.Render()`
（`src/Ecad2.Rendering.Wpf/PartThumbnailRenderer.cs:56`）がサムネイル描画時に固定の
`DrawingTheme.Black`を使用しており、アプリのライト/ダーク切替に反応しない構造。生成された
サムネイルはビットマップ（`ImageSource`）としてキャッシュされる設計のため、単純なブラシ差替え
では対応できず、**テーマ切替時にサムネイルの再生成が必要になる見込み**（着手時に要精査）。
PR-18とは異なるメカニズム（ビットマップ事前レンダリング由来）ゆえ別記帳。範囲内欠陥修正として
侍へ差し戻し。
**完了（2026-07-17、侍、コミット399019d）**：`PartThumbnailRenderer.Render()`/`RenderGlyph`へ
foreground引数追加（既定=従来どおり黒、後方互換維持）、`PartSelectionEntryViewModel.Thumbnail`を
可変プロパティ化、`PartPaletteViewModel.RefreshThumbnails(Color)`で全件再生成する仕組みを追加。
実機確認＝a接点/b接点/コイル等のアイコンがダーク切替で黒→明色（白系）へ正しく変化、ライト復帰も
回帰なし。build/test回帰なし。**配線待ちの注記**：呼び出し配線（`MainWindow.xaml.cs`の
`IsDarkMode`変更ハンドラ内、`RefreshThumbnails(...)`の1行）はT-099要件(1)+T-089叩き台の未コミット
分と同一ファイルのため意図的に未コミットのまま——現HEAD単体では新規発見5は「配線未接続」（実害
なし、T-099/T-089コミット時に一括で有効化される）。
**範囲外の気づき（隠密、実機確認推奨）**：1段目`ToolBarKeyLabelStyle`（ラベル用）にForeground
明示指定がなく`ToolBar.Foreground`継承依存、2段目`PlacementToolBarKeyLabelStyle`は増分6で
「継承に頼らず明示指定」と判断済みという非対称性あり——1段目でも継承だけで正しく機能するか
忍者の実機確認を推奨。
**セッション終了時点のT-083状態まとめ（2026-07-16、当時の記録。現在は増分5・7も完了し全7増分
完了、上記2件の新規発見を除く）**：増分1(層B)・増分2(メニュー含む層C基盤)・増分3(ダイアログ/
固定色パネル)・増分6(配置ツールバー2段目)＝**完了**。増分5(ペイン内コンテンツ=ListBox/DataGrid)
＝**未着手**（着手前調査済み、対応表あり）。増分7(ドロップダウンメニュー背景)＝**打ち切り**
（Light固定のまま残置、次セッション要判断）。増分4(選択色系暗背景視認性)＝**未着手**。
v0.4リリースは全増分完了後の予定ゆえ、今回は見送り。
**リリース予定、殿直接指示（2026-07-16）**：T-083全増分（4分割→層B/メニュー決着・増分5/6/7追加で
実質7分割）の検証パイプライン完了後、v0.4仮リリースビルドを作成する（T-057確立の手順＝Version
設定→publish2箇所→起動終了実測→csprojのみコミット→家老がタグ打ち・push、に準拠。侍が実行、
家老が采配時に手順詳細を伝達）。**2026-07-17時点＝全7増分の実装・検証は完了したが、上記新規
発見2件（プロパティ文字色・ハッチング模様）の対応完了後にリリースへ進む**。

### T-100 ドッキング済みタブのハッチング模様除去 — Approved（再開、殿裁定2026-07-17）

**起票=殿直接指摘2026-07-17**「ドックタブの前の後ろにハッチング模様がでているが消せないか？」
「常に表示されている」→対象範囲確認「全ドック共通」。タブヘッダーのラベルと固定アイコン
(ピン留め/閉じるX)の間の余白部分に、AvalonDock/VS2013テーマ既定の斜線ハッチング背景パターンが
常時表示される。**対象=全ドッキング可能パネルのタブストリップ共通**（T-099のスコープ=配置
ツールバー2段目限定とは別、こちらは全パネル共通の事象と殿確認済み）。着手時に原因箇所
（`AnchorablePaneControlStyle`/VS2013テーマの`TabPanel`背景装飾）を特定してから対応する。
**着手（2026-07-17）**：侍へ調査・対応を采配。
**調査進捗（2026-07-17）**：原因はAvalonDock既定`AnchorablePaneTitleStyle`（タブ1個時のタイトル
バー表示、generic.xaml 297行〜）のGrid列0が`Width="*"`（残り幅全部）になっており、ラベルが短いと
余白ができその部分に装飾が表示される構造と判明（要素自体の特定は継続調査中だった）。殿ご教示
「タブ自体の幅設定はないの？」を受け列0を`Auto`化する案を試すも、**殿確認＝解消せず**。
**殿裁定（2026-07-17）＝一旦保留**。侍は着手前に戻し、他タスクへ。
**侍完了報告との齟齬（2026-07-17）**：侍は列0`Auto`化を実装しコミット（`8650a66`、`AnchorablePaneTitle`
型への暗黙的スタイル、全ドック共通適用）、build/test回帰なし・**侍自身の目視ではLight/Dark双方
「改善を確認」と報告**。しかし**殿の実機での直接観察は「治っていない」**——自己目視確認と殿の
実観察が食い違う結果となった。侍の報告到着と家老の保留裁定が交錯したタイミングの問題であり、
コードは`8650a66`のまま残置（実害なしのため撤回は不要、保留中は据え置き）。
**【重要・殿ご指摘2026-07-17】本件のハッチング模様は、スクリーンショット（PrintWindow等の静止画
キャプチャ）では視認の限界があり、人間の目でしか判別できない種類の視覚アーティファクトである
可能性が高いとのこと**。既存の「画素採取が目視に勝る」原則（色の誤読対策として確立、
ecad2-ui-automationスキル参照）は依然として色・配色の判定には有効だが、**本件のような微細な
テクスチャ・レンダリングパターンには通用しない可能性がある**——次回T-100再挑戦時は、忍者の
画素採取・侍の自己目視のいずれにも過度に依存せず、殿ご自身の実機目視での最終確認を要する
点に留意する。原因技術（PrintWindowのキャプチャ方式がWPFの特定描画効果を再現しない可能性等）は
未確定・推測の域を出ない。
**根本原因、完全特定（2026-07-17、隠密、殿直接密命による再調査・事後家老共有、
`docs/ecad2-t100-drag-handle-texture-root-cause-survey-onmitsu.md`）**：**結論＝仕様（バグに
あらず）、対処は可能**。VS2013テーマパッケージ一次ソース（`Themes/Generic.xaml`）確認により、
`AnchorablePaneTitleStyle`内に`x:Name="DragHandleTexture"`という`Rectangle`要素が明示実装されて
おり、`DrawingBrush`（TileMode=Tile、4x4単位、1x1px点を市松状）で描く**Visual Studio系IDE定番の
「ドラッグハンドル」意匠**（意図的な装飾）と判明。複数タブ時のTabItem側には無く単一タブ構成の
パネルタイトル部のみに常時表示——ecad2は各パネル単一タブ構成のため常時表示となり、殿確認の
「全ドック共通」と符合。**侍の先行試行（8650a66、Grid列0のAuto化）が効かなかった理由も判明**：
模様の発生源は列0の余白ではなく、内部`DockPanel`の`LastChildFill`機構が最後の子要素（この
`Rectangle`）へ残りスペースを自動割当てる構造のため、列0を縮めても解消しない（標的が違って
いた）。**スクリーンショット限界の技術的裏付け**＝1x1pxの点が4x4単位でまばらな極めて微細な
パターンゆえ、画素採取・圧縮縮小では潰れやすい構造と一次ソースからも符合。
**対処方針（提案）**：`AnchorablePaneTitleStyle`の派生スタイルを定義し、`Rectangle
x:Name="DragHandleTexture"`を直接標的に`Visibility="Collapsed"`（推奨）または`Fill="Transparent"`
で無効化。増分7で確立済みの「既定ControlTemplateコピー+標的要素のみ差し替え」手法を流用でき、
対象範囲は増分7より小さい見込み（色のDynamicResource化でなく特定Rectangle 1個のVisibility制御
のみ）。**不明点**＝Visibility=CollapsedかFill=Transparentかは実機確認要（前者はレイアウト占有
スペースも消える分、より自然な見た目になる可能性が高いと隠密推測）。DragHandleTexture領域が
ドラッグ当たり判定に関与していないかも実装後の実機確認を推奨。フローティング側
（`LayoutAnchorableFloatingWindowControl`）も同型のため対応要否は着手時要確認。
**殿裁定（2026-07-17）＝T-100再開**。侍へ実装を采配。
**完了（2026-07-17、侍、コミット62b993f）**：VS2013テーマ`AnchorablePaneTitle`暗黙的スタイルを
一次ソースから完全コピーし`Rectangle x:Name="DragHandleTexture"`のみ`Visibility="Collapsed"`化。
`ApplyDockingManagerThemes`でTheme適用直後に各DockingManagerの`Resources`へ直接登録しVS2013
テーマのMergedDictionaries経由スタイルより優先させる方式（先行実装8650a66はAvalonDock本体既定
テンプレートを標的にしており実行時適用のVS2013テーマ側スタイルには無効だったため旧コード削除・
本実装に一本化）。build/test回帰なし。**ライトモードは殿ご自身の実機目視で「正しく消えている」と
確認済み**（スクリーンショット限界を踏まえ殿の目視を一次情報として採用）。**ダークモードは未確認、
忍者検証待ち**。
**ダークモード検証、部分進捗（2026-07-17、忍者）**：`DragHandleTexture`要素はWPF既定で
AutomationPeer非対応（`Rectangle`型）と判明、UIA直接探索は不可（`RawViewWalker`でも219要素中
0件検出）。**代替の機械的検証法を考案**——旧ハッチング表示域を画素採取し「ユニーク色数」で判定
（模様が残っていれば複数色、完全に消えていれば単色のはず）。シートパネルのタイトル余白領域
（118x11px）で実測、ユニーク色数=1（`#2D2D30`のみ）を確認、模様消失を確定。機器表・出力パネルは
未実施（下記新規発見の対応で中断）。**配置ツールバー2段目はT-099未修正症状で領域自体が潰れており
現ビルドでは確認不能、T-099修正待ち**。
**ダークモード検証完了・T-100完全決着（2026-07-17、忍者、`docs-notes/ecad2-t100-verify-ninja.md`）**：
ドッキング済み3パネル（シート/機器表/出力）いずれもタイトル余白領域のユニーク色数=1（模様なし）を
確認、出力パネルは選択（アクティブ）状態でも同様にOK。**教訓＝ユニーク色数判定は、文字・アイコン
境界に近い領域だとアンチエイリアシングで誤爆する**（出力パネルで実際に遭遇、境界から離して再測定
し解消）——ecad2-ui-automationスキルへ追記推奨。配置ツールバー2段目のみT-099修正待ちで未確認、
残置。新規発見6の検証もT-099完全解決まで見送り。
**新規発見6（殿直接指摘・忍者経由2026-07-17、範囲内欠陥・未検証）：ダークモードで要素配置時、
シートパネル・部品選択パネルが一瞬ライトモードに戻る**：忍者が実機確認を試みるも、配置ツールバー
2段目がT-099症状で操作不能（a接点等のボタン非表示）のため要素配置自体ができず検証未了。**T-099
修正待ち**（検証・原因調査とも）。
**静的調査完了・確定的発火源は特定できず（2026-07-17、隠密、
`docs/ecad2-t083-shinki-hakken6-theme-flicker-survey-onmitsu.md`）**：要素配置コマンド経路
（`TryPlaceElement`→`PlacementOkButton_Click`→`PlaceElementAtSelectedCell`→`RedrawCanvas`→
`ClosePlacementBar`）を一次コード全行程精読、`MergedDictionaries`・`DockingManager.Theme`・
`Application.Resources`への操作は一切含まれず（確定事実）。テーマ再適用ロジックは`IsDarkMode`の
`PropertyChanged`でのみ発火し要素配置経路とは実装上完全に独立と確認。**有力仮説（不完全）**＝
部品選択パネルの`IsPartSelectionVisible`切替（Collapsed⇔Visible）時のDynamicResource解決の
ちらつきを候補視するが、シートパネルは常時Visibleでこの仕組みの対象外のため「両パネル同時
発生」を説明しきれず。PR-18・増分1/7とは性質が異なる（それらは「値は正しいが静的に反映されない
恒常的問題」、本件は「一瞬だけ間違った値が見えて戻る過渡的現象」）ため類推は適用しにくいと判断。
**隠密所見＝机上調査の限界、忍者の実機再現（画素採取・時系列撮影）が本命**。T-099解決により
検証の障害は解消済み、忍者へ実機検証を回付。
**忍者の実機検証、断定に至らず・両論併記（2026-07-18、
`docs-notes/ecad2-t083-shinki6-flicker-verify-ninja.md`）**：要素配置確定操作直後を高速連写
（PrintWindow+GetPixel）したところ、7回中3回でシート/部品選択パネルが約35〜65ms時点でRGB
(255,255,255)を検出、以降は正常なダーク色に復帰——殿ご指摘の現象と一見符合。**しかし重大な矛盾**：
白検出フレームのBitmapをそのまま画像保存し目視すると実際には白くなっておらず通常のダーク配色の
まま（同一Bitmap内で反復GetPixelしても一貫して255を返しノイズではないにもかかわらずSave画像には
反映されない）。(a)実UI現象を機械的にのみ捕捉できている可能性 (b)PrintWindow+GetPixel経由の
検証手法自体に未解明のタイミング上の罠がある可能性、いずれも残り確定的判定に至らず。要素配置
操作自体は3回とも正常動作・回帰なし。忍者は深追いせず検証終了・アプリ停止。
**家老裁定（2026-07-18）＝診断ログ注入へ切替、優先度は低（現行T-099/T-067/T-089完了後）**：
GetPixel/Save矛盾は検証手法側の未解明な罠である公算が高いと判断、これ以上のスクリーンショット系
検証の反復は水掛け論になりやすい。実害は一瞬のちらつきで機能的損害なしのため、侍の現行タスク
キュー（T-067次段階・T-099要件2/3・T-089バグ調査・T-067基盤修正2件）完了後に、
`IsPartSelectionVisible`切替経路等へ診断ログを仕込み一次実測する方針で改めて着手する。

### T-098 シート追加時のPageNumber採番方式見直し — Approved（gated、殿裁定2026-07-15、P-105起票）

**起票=P-105（侍、T-084差し戻し調査2026-07-15で発覚）を殿裁定でタスク化**。`AddCommand`
（`SheetNavigationViewModel.cs`91-101行）のPageNumber採番が`Sheets.Count+1`固定で既存シートの
最大PageNumberを見ない。削除で欠番が生じた状態で新規追加すると、歯抜けを埋める小さい番号が
新シート（表示順序上は末尾）に付き、表示順序とPageNumber数値の対応が崩れる。T-084の
DeleteCommand欠番警告ロジック自体は論理的に正しいと確認済みだが（真に新規追加分のシートを
削除すると新たな欠番が生じるため警告は妥当）、ユーザー体験としては「末尾シートを消しただけ
なのに欠番警告が出る」という直感に反する挙動に見える。
**修正方針（案、着手時に精査）**：既存シートの最大PageNumber+1を採番する方式へ変更。
**優先順位**：殿裁定2026-07-15＝新規タスク化するが優先度は別途検討（実装順ロードマップへの
組み込みは後日判断）。

### T-077 「ヘルプ」→「使い方」画面新設（docs/spec/転用） — Approved（gated、殿直接指示2026-07-11）

**起票=殿直接指示2026-07-11**「最終的にdocs/spec/の情報を編集して『ヘルプ』→『使い方』に転用。
ユーザー向けの為、詳しい詳細までは求めない」。

**背景**：T-075（主要機能の仕様書整備、隠密担当・進行中）で`docs/spec/ecad2-spec-{領域}.md`として
作成している技術仕様書（開発者向け、出典・コード根拠つきで詳細）を素材とし、将来的にアプリ内
「ヘルプ」メニューへ新設する「使い方」画面へ、**ユーザー向けに編集・転用**する。開発者向けの
実装詳細（コード内部・出典明記等）は求めず、平易な操作説明へ書き換える。

**前提条件**：T-075（仕様書整備）が一定領域完了してから着手（素材が無いと転用できない）。
内容（見込み、着手時に精査）：
1. 「ヘルプ」メニューへ「使い方」項目を新設
2. `docs/spec/`の各領域仕様書からユーザー向け平易版を作成・編集
3. アプリ内での表示形式＝**非モーダル別ウィンドウに確定（殿裁定2026-07-17）**——独立した
   ウィンドウとして開き、閉じずにmain windowと並べて参照しながら作業できる方式（GX Works3等の
   ヘルプウィンドウに近い形）。モーダルダイアログ・AvalonDockドッキングパネルは不採用。
規模中、T-075完了後に着手順序を検討。着手時要精査：複数領域の仕様書をまたぐナビゲーション
（目次/トピック切替のUI）は表示形式の決定とは別に、実装着手時に具体案を詰める。

### 実装順ロードマップ（2026-07-14家老改訂、T-087/T-088完了・T-058/T-083前倒しを反映）

T-058/T-083前倒し（殿指示2026-07-14） → 小粒タスクの消化 → 大物の順（.NET 10移行=T-062は完了済み）。

直近の完了実績（T-052〜T-088、v0.2/v0.3仮リリース含む多数）は`docs/todo.md`末尾の
「完了・取り止めタスク索引」および`docs/todo-archive.md`を参照——本表は**生きているタスクのみ**
を扱う（肥大化防止のため完了行は都度アーカイブへ移し、ここには残さない）。

| 順 | task | 内容 | 状態・根拠 |
|---|---|---|---|
| 1' | T-083 | ダークモード搭載（AvalonDock連動） | **PoC完了（2026-07-15）**、忍者実機確認OK。本実装（増分計画）待ち |
| 2 | T-089 | ボタン押下状態の視覚的明示化 | 殿直接要望（2026-07-14）、規模中・着手前調査要 |
| 3 | T-067 | GroupFrame作成・編集UI（中〜大） | UI/UX論点殿裁定済み(2026-07-12)、侍下ごしらえ済み・実装未着手。着手時要確認事項はT-067節参照 |
| 4 | T-068 | 自作パーツ管理・編集UI | 規模大ゆえ最後尾 |
| - | T-077 | 「ヘルプ」→「使い方」画面新設(docs/spec/転用) | T-075完了済み・素材あり、優先度低のため着手順序は後日検討 |
| - | T-098 | シート追加時のPageNumber採番方式見直し | Approved（殿裁定2026-07-15）、優先度は別途検討 |

- 順2以降の細かな先後は着手時に殿と調整（本表は家老見立て）
- 棚卸し39項目の残り（クリップボード・テンプレート・オートセーブ・ショートカットキー設定は
  →**T-087として一部起票済み**・ダークモードは→**T-083として起票済み**・可動パレット[T-058と
  表裏]・不明点5件）は**後日判断（殿裁定2026-07-11）**、
  詳細=`docs/ecad2-guiecad-unwired-features-survey-onmitsu2.md`
- T-046（CI化）はStrykerスコア改善待ちの残置、T-044・T-028等の保留・Proposedは順位付け対象外

### T-068 自作パーツ管理・編集UI — Approved（gated、殿裁定2026-07-11、棚卸し起票）

**起票=棚卸しB区分**。`PartFolderStore`等Core層は完備だが、自作パーツの作成・編集UI（GuiEcadの
パーツエディタ相当）が皆無。**規模大**——ロードマップ最後尾（家老見立て）。着手時に増分計画から。

### T-067 GroupFrame（グループ枠）作成・編集UI — Approved（gated、殿裁定2026-07-11、P-054起票）

**起票=P-054（殿の直接指摘2026-07-11）を殿裁定でタスク化（B区分一括）**。`new GroupFrame`・
`Frames.Add`ともsrc全体0件＝生成経路がアプリに一切存在せぬ。Core層は完備（モデル・描画・RowOps
シフト・Height--詰め処理）。GuiEcad側には枠追加/削除/リネーム/移動/枠線スタイルのコマンド群が
存在。規模中〜大。関連=P-050（Visual*Mm座標未追随、枠ドラッグ実装時の潜在バグ——本タスク着手時に
併せて対処を検討）。UI/UX分岐（枠の作成操作方法等）は着手時に殿確認【MUST】。
**着手前調査完了（2026-07-11、隠密2、`docs/ecad2-t067-groupframe-design-onmitsu2.md`）**：
GuiEcad実物操作体系＝作成(「枠」ツール選択→**マウスドラッグ、mm連続座標のグリッド非依存自由配置**
→リリース確定、半セル未満は無視。GroupFrameは唯一グリッドに縛られない自由配置機能)・ラベル編集
(ダブルクリック→インラインテキストボックスのキャンバス上オーバーレイ)・移動(ドラッグ)・線種変更/
削除(右クリックメニュー、T-069調査で確認済み)。ecad2側Core層は完全同型で実装済み
（`DiagramRenderer.DrawFrames`のVisual*Mm優先ロジックまで）、App層は生成・編集・移動・削除いずれも
皆無。
**重要な発見**：P-050（RowOps未追随）の対処要否は独立したバグ修正ではなく、**「枠の配置単位を
グリッドセル単位にするかGuiEcad同様mm自由配置にするか」という設計分岐そのものに直結**——グリッド
セル単位限定ならP-050は構造的に非該当（そもそも追随すべきVisual座標が無い）、mm自由配置を踏襲する
ならP-050対処が必須。この分岐は作成操作方法（マウスドラッグ vs 既存`FreeLine`同型のキーボード
ステップ方式、`BeginFreeLineDraft`等が直接参考になる）とも自然に対応。Undo対応はT-064同様、既存
`RecordSnapshot`パターン流用で新規コマンドクラス不要。
**殿裁定（2026-07-12、プレビュー提示で選択）**：①作成操作方法＝**両方対応**（マウスドラッグ・
キーボードステップいずれでも作成可能） ②配置単位＝**グリッドセル単位**（P-050は構造的に非該当と
確定、マウスドラッグ時もグリッドへスナップする実装とする） ③ラベル編集UI＝**ダブルクリック→
インライン入力（GuiEcad踏襲）** ④移動操作方式＝**ドラッグ（GuiEcad踏襲）**。
**着手時要確認（2026-07-13家老追記）**：proposed.mdの経過観察3件がいずれもGroupFrame追加を
発火条件の筆頭候補として明記している——P-071（未確定編集確定処理の呼び忘れ、新規選択状態変更
経路の追加で4回目再発なら構造格上げの殿条件つき）・P-077（左右クリックヒットテスト優先順位
ロジックの重複、別要素種別追加で2件目が出れば正式パターン化検討）・P-080（3種ドラフト全クリア
責務の分散、4種目のドラフト追加で同型のクリア漏れ再発の恐れ）。着手時は横展開チェックリスト
7項目の自己点検に加え、この3件への該当有無を隠密へ確認させること。
**着手前チェック完了（2026-07-17、隠密、`docs/ecad2-t067-pretask-check-onmitsu.md`）**：
**3件とも該当**。P-071＝該当（名指し予告そのもの、既存チェックリスト6項目目で対応済みだが
GroupFrameラベル編集[ダブルクリックインライン]は`UpdateSourceTrigger=Explicit`入力欄になる
可能性が高く要重点確認、4回目再発なら構造格上げの殿条件は維持）。P-077＝該当（左右クリック
ヒットテスト重複、**T-067実装がP-077の言う「2件目」になる可能性が高く正式パターン化格上げの
検討タイミング**）。P-080＝該当の可能性高い（GroupFrame作成がFreeLine同型の4種目ドラフト
プレビュー機構を要する設計と推測されるため）。**横展開チェックリスト7項目、7/7該当**——ただし
**項目7（矢印キーでの選択状態平行移動）のみ、殿裁定④「移動=ドラッグ」が矢印キー対応を含むか
未明記でUI/UX判断を要する**。
**【報告・殿お戻り後の確認事項】**：GroupFrameの移動操作に、ドラッグに加えて矢印キーでの平行
移動も対応させるか（他の選択可能要素との一貫性・キーボードファースト理念との整合 vs
殿裁定「移動=ドラッグ」の明示範囲）。**着手（2026-07-17）**：侍は矢印キー対応を除く範囲（ドラッグ
移動・P-071/P-077/P-080の実装時注意）で先行実装、矢印キー対応は殿裁定待ちで後日追加可能な形に
しておく。
**基盤区切り完了（2026-07-17、侍、コミット837b407）**：ViewModel層——`SelectedFrame`新設
（`SelectedCell`セッタへ排他クリア組込み、P-080対応=ドラフトクリア4種目として追加）、
`DeleteSelectedFrame`/`RenameSelectedFrame`（P-071対応、確定処理の受け皿）、
`BeginDragFrame`〜`ConfirmDragFrame`/`CancelDragFrame`（GridPos単位、Undo対応）、
`BeginFrameDraft`〜`ConfirmFrameDraft`/`CancelFrameDraft`（キーボードステップ方式）、
`IsFrameWithinGridBounds`（他要素との重複は占有判定対象外）。回帰テスト`T067GroupFrameTests.cs`
新設、build/test回帰なし（App.Tests 695→712件+17、Core.Tests 120件）。**次段階**：
(1)ヒットテスト+選択ハイライト描画 (2)キーボード/マウス配線 (3)マウスドラッグ新規作成
(4)ラベル編集UI（P-071確定処理の実配線） (5)右クリックメニュー。P-077は(1)(5)実装時に意識する
旨、侍より申告あり。
**基盤、隠密静的レビューでCONFIRMED2件・PR-01再発（2026-07-18、
`docs/ecad2-t067-foundation-static-review-onmitsu.md`）**：`ReplaceDocument`が新設
`SelectedFrame`/`_frameDraft`をクリアしない、`HasAnyDraft`が`_frameDraft`を含まない。制度化済み
チェックリスト（`samurai.md`「新規選択可能状態の横展開チェックリスト」5項目）存在下での再発ゆえ
実効性に疑義（P-106として記帳、隠密へ運用実態点検を委譲）。
**基盤欠陥2件、修正完了（2026-07-18、侍、コミット8c996fe）**：(1)`ReplaceDocument`へ
`SelectedFrame=null`/`ClearFrameDraftIfAny()`追加 (2)`HasAnyDraft`へ`_frameDraft`判定追加。
回帰テスト3件追加・RED先行証明済み。**侍の正直な報告**＝当初4件目のテスト
（「枠記入確定後はHasAnyDraftがfalseに戻る」）はConfirmFrameDraft内部でCancelFrameDraftが呼ばれ
修正前コードでも通ってしまう検出力なしのテストと判明、自ら削除（RED証明できたのは3件のみ）。
build/test全合格（App.Tests715/Core.Tests120、既知数から減少なし）。
**侍の所見（PR-01実効性への追加情報）**：基盤実装(837b407)時点では「View層未着手」の区切りで
SelectedCell setter経由のクリア連鎖は機能済みだったが、`ReplaceDocument`/`HasAnyDraft`という
「setterをバイパスする別経路」への横展開はチェックリスト対象外だった可能性——既存7項目は
選択排他setter・Esc・矢印キー等が中心で`ReplaceDocument`系は明示的に挙がっていない。P-106調査
（隠密、運用実態点検）へ追加の調査観点として申し送り済み。
**(1)ヒットテスト+選択ハイライト描画、完了（2026-07-18、侍、コミット9fd10a8）**：GuiEcad原本
（`HitTestFrame`）を直接調査し移植——枠は塗りつぶし無しゆえ境界線近傍(margin付き)のみヒット、
複数該当時は面積最小(入れ子の枠)優先、左クリック優先順位はConnectorの直後。UI/UX新規分岐でなく
GuiEcad一次資料の忠実な移植と侍判断・家老確認済み。build/test全合格（App.Tests712/Core.Tests120）。
実機確認は忍者へ委ねる。次段階(2)〜(5)は継続作業。
**(1)隠密静的レビュー完了（2026-07-18）**：GuiEcad原本（`MainPage.Pointer.cs`/`MainPage.xaml.cs`）と
数式レベルで完全一致（優先順位・margin計算・面積最小優先とも）、忠実な移植と確認。修正必須の重大
バグなし。code-reviewスキル(low)でPLAUSIBLE1件（`FrameRectMm`/`FrameRectDip`がWidth/Height非負
検証なしに`new Rect(...)`構築、旧ファイル互換・破損データでArgumentExceptionの懸念、severity低）。
気づき（範囲外）＝FreeLine/ConnectionDot判定順序がGuiEcad原本と逆転（T-041増分5由来と推測）。
**(1)忍者実機確認、NG（2026-07-18、再描画漏れ）**：境界線クリックで選択セルは変化するが、枠の
選択ハイライトが即座に反映されず、テストモードON/OFFを挟むとオレンジハイライトが現れる（再描画
漏れ）。範囲内欠陥として侍へ差し戻し。
**(1)欠陥2件、修正完了（2026-07-18、侍、コミットf648897）**：(1)真因＝`ViewModel_PropertyChanged`
の再描画トリガー列挙に`SelectedFrame`が漏れており枠クリック選択直後にRedrawCanvasが走らなかった
（忍者観測と完全整合） (2)`FrameRectMm`のWidth/Heightを非負クランプ（隠密PLAUSIBLE指摘対応）。
View層コードビハインドのためRED証明不可（既存方針）、実機確認へ委ねる。build/test exit0確認済み。
忍者の現行検証セッションは本修正込みの最新ビルドで動作中——次の境界クリック再検証で確認できる
見込み。
**基盤欠陥3件目、修正完了（2026-07-18、侍、コミットc544bde）**：`CancelResidualDraftForToolSwitch()`
へ`ClearFrameDraftIfAny()`追加（隠密P-106点検指摘、着手前チェックが名指しした3箇所全て解消）。
回帰テストは既存Theory(Connector/FreeLine/Image)へFrameケースを4種目として追加（rule of three超えの
複製回避）、RED先行証明済み。build/test全合格（App.Tests716/Core.Tests120）。**T-067基盤欠陥3件、
全て解消**。侍はT-099要件(2)(3)実装を再開。
**着手（2026-07-18）**：commit 9fd10a8（ヒットテスト+選択ハイライト描画）の隠密静的レビュー（1周目
軽量既定）を采配。完了後、忍者へ実機確認を回す（検証パイプライン既定順）。

### T-046 「必ず通過するテスト」防止の仕組み化 — In-progress（制度は運用開始済み、CI化のみ残置）

**殿裁定2026-07-08**：①RED先行証明の必須化＝バグ修正の回帰テストは修正前コードでREDになることを
git stash実測で証明し報告に含める（`samurai.md`・`karo.md`へ【MUST】追記済み、即日運用開始）
②Stryker.NET＝**手動棚卸しから段階導入で確定（殿裁定2026-07-08）**：隠密調査
（`docs/ecad2-t046-stryker-survey-onmitsu.md`、WPF相性OK・全体3分弱・当初score Core 3.76%/App 19.79%
→T-045補遺2後 App 22.15%）を受け、往復修正案件のクローズ時に隠密が手動実行してテストの穴を
棚卸しする運用から開始（`karo.md`・`onmitsu.md`へ運用追記済み）。score改善後にCI化を再検討
③**テスト設計と実装の分離**＝バグ修正・往復案件では隠密が仕様側からテスト設計（同値分割・境界値
分析・状態遷移・対称性点検・`[Theory]`活用の技法適用必須）を先に起草し、侍はコード化に徹する
（実装者バイアス対策、殿指摘2件を制度化。`onmitsu.md`「テスト設計の起草」・`samurai.md`
「テスト設計と実装の分離」・`karo.md`同名節へ【MUST】追記済み、即日運用開始）。
背景=T-041増分7で「旧実装でも新実装でも通る回帰テスト」が複数残存していた実例。
**残作業はCI化の再検討のみ（score改善待ち）**。T-050クローズ時棚卸し（2026-07-10）時点の
App score=23.88%（19.79→22.15→23.88と漸増中、`docs/archive/ecad2-t050-stryker-review-onmitsu.md`）。

### T-044 OR自動配線の冗長縦分岐抑止 — Done（新規バグ報告分、2026-07-19、コミット4e11c3a・push済み）

実装d04c9a3＋隠密再レビュー＋忍者実機とも全OK・電気的に正常だが、連鎖ORで低ズーム時に
右合流線が断線して見える描画欠陥が判明。自動ロジック精緻化は袋小路（真因はP-003/P-028の既存欠陥）
ゆえ手動配線基盤T-041で対処する方針へ転換（T-041は2026-07-08完全Done、2026-07-07裁定）。

**方針転換・再開（2026-07-19、殿直接指示）**：殿より新規スクリーンショット提示とともに
「接点C右側の配線は本来、線番2に繋がらなければいけない。手前の行4列の場所までしか縦分岐が
描画されない。この状態を線番2まで自動で結線して欲しい。描画ロジックを見直して欲しい」との
明確なご指示。忍者・隠密の再調査で真因が完全確定——`DiagramRenderer.RightTerminator`
（367-387行、T-026/P-003で意図的に`(TopRow||BottomRow)`から`BottomRow`限定へ変更した副作用）
により基準行（B接点等）の右側配線が省略され、下流の縦コネクタが視覚的に「浮いて」見える
（横線がB接点右端手前で終わり、離れた列から縦線が生える）。画像・診断ログ双方で完全一致確認済み
（忍者、`docs-notes/t044v2-diag-log-newpattern-ninja.log`・証拠画像`t044v2-*`）。殿裁定により
**自動ロジック精緻化を本命として再開**、手動配線での回避方針は撤回。対処案（「自分自身より右の
別コネクタ」と「自分を経由し下流へ分岐するコネクタ」を区別するより精密な条件、P-028時代の提案を
具体化）は隠密が設計中、まとまり次第侍へ実装采配。
**新規バグ報告（殿直接指摘2026-07-18、未調査）**：「ORを書いた場合の分岐線が必ず1段上までしか
連結しない。1段上の配線に分岐線があった場合は連結しなくなる」。当時の記録は「低ズーム時に見た目
上断線するが電気的には正常」という**描画のみの欠陥**だったが、今回の症状は「連結しなくなる」と
いう、より機能的な問題に読める——同一原因の言い換えか、別の新規バグかは未確認。隠密へ独立調査
（再現条件の特定・原因が既存P-003/P-028由来か否かの切り分け）を委譲する。
**調査中断・中間仮説あり（2026-07-18、隠密）**：Ecad2.App起動不能インシデント（下記）優先のため
一旦中断、再開待ち。中断前に有力仮説を発見済み——`PlaceElementAtSelectedCell`の
`NothingBetweenRailAndColumn`（左分岐省略ロジック）が、基準行が既に`rightColumn`側の縦コネクタを
持つ場合に`leftColumn`側の判定が及ばず誤って「何も挟まっていない」と誤判定し左分岐を省略、結果
Cの左ポートが意図せず母線直結される懸念。殿提示の実機画像とも符合する可能性あり、再開時に活用。
**調査再開・中間報告（2026-07-18、隠密）**：単純な反例（3階層、baseRow行が右側に縦コネクタを1本
持つのみ）では電気的にも描画的にも再現できず——`LeftRailReached`（NetlistBuilder、電気側）と
`LeftTerminator`（DiagramRenderer、描画側）は同じ`BottomRow==row`限定ロジックで整合しており、
コード内コメントの「省略しても電気的分断は起きない」という前提はこの範囲では成立。殿実機画像
（行1接点→行4横線がN24側に届かず浮遊→行8から縦線接続）の再現には、より複雑な多階層構成——
特に**新要素をbaseElementより左側の列に配置する**（通常の右へ広がる連鎖ORとは逆方向）ケースが
鍵と推測するが、手計算では確定的な反例を構築できず。時間対効果を鑑み実機再現へ移行、忍者へ
采配済み（(a)3階層以上・新要素を左列配置 (b)殿画像構図の再現+DRC出力確認）。文書化は忍者の
再現結果を見てから（家老裁定、仮説が外れた場合の手戻り回避）。
**Explore調査で新たな手がかり（2026-07-18、忍者）**：`NetlistBuilder`(電気側`LeftRailReached`)と
`DiagramRenderer`(描画側`LeftTerminator`)の判定ロジックが実は食い違っている疑い——隠密の「両者
整合」という記述はコード実態と不一致、電気側は旧ロジックのまま未修正の可能性。詳細は忍者の後続
報告待ち。
**実機再現成功（2026-07-18、忍者、殿ご自身の操作立会い）**：行1/4/8構図（Row1Col5にa接点→Row2Col5
にORa接点→Row3Col3[左2列]にORa接点）で、行8(baseElementが行4接点より2列左)の**母線接続線が完全に
欠落**、視覚的に接点が浮遊する現象を再現。DRC出力は0件（電気的には正常判定）——Exploreの新仮説
（電気/描画ロジック不一致）と符合。証拠画像=`docs-notes/screenshots/t044-row8-disconnect-repro-
{3x,full}.png`。殿がサンプル図面を保存予定、忍者は操作を一旦停止し待機中。
**殿サンプル図面でも再現確認（2026-07-18、忍者、`C:\ECAD2\sample\setuzoku.gcad`、
`docs-notes/ecad2-t044-realmachine-repro-ninja.md`）**：殿ご指摘「X4リレー右側配線が線番2に届いて
いない」を実機で裏付け。X4(行9)の右側配線は上方X3(行8)の縦コネクタ下端で途切れ、線番2(CRコイルへの
経路)に到達せず。DRC出力5件はいずれも「駆動元不明/死にリレー」系のみで配線欠落自体は未検出
（構図Aと同型）。**忍者作成の構図A・殿作成のサンプル図面、独立した2件で同型症状を確認**——
再現条件はほぼ確立。証拠画像=`docs-notes/screenshots/t044-x4-*`・`t044-sample-*`。原因特定を
隠密へ委譲。
**本格調査・行き詰まり、診断ログ注入へ切替（2026-07-18、隠密）**：忍者指摘の非対称性を確認・自己
訂正——`LeftRailReached`(電気側)は`(TopRow==row||BottomRow==row)`だが`LeftTerminator`(描画側)は
`BottomRow==row`のみで、以前の「両者整合」報告は誤りだったと判明。画像精査で症状を精緻化——
忍者文面「行8欠落」より正確には**行4の左側(母線N24〜接点B間)が完全に欠落**（行1・行8は母線から
正しく線が伸びる）。構図Aを手計算で追ったが、行4の左分岐は`NothingBetweenRailAndColumn`により
省略が正しい計算結果となり、欠落理由を机上で説明できず——忍者発見の非対称性も本ケースには影響
しない計算結果。**コード推論ベースの修正が複数周不発の様相**、診断ログ注入（`leftColumn`/
`NothingBetweenRailAndColumn`各判定値・`LeftTerminator`戻り値の実機ログ出力）を次善策として提案、
侍へ采配。
**診断ログ第1弾・構図Aは非再現と判明（2026-07-18、忍者、`docs-notes/t044-diag-log-koudzuA-ninja.log`）**：
構図A(Row1Col5→Row2Col5→Row3Col3)を再現しログ取得したが**バグ非再現**（視覚的にも正常）。ログ解析
——既存縦コネクタ`col6:r0-1`は新要素の列3より右にあり「column=3より左に何も無い」判定は電気的にも
描画的にも妥当（母線直結が正しい動作）、この構図はそもそもバグの発生条件を満たしていなかったと
判明。実際に再現できたのは「行1/4/8」構図（行を離して配置）と殿のsetuzoku.gcadで、いずれも既存の
縦コネクタが新要素の判定範囲と交差する位置関係だったと忍者推察——隠密の当初仮説（3階層・baseRow
行の右側縦コネクタ1本）は再現条件を捉えきれていなかった可能性。忍者は「行1/4/8」構図での再取得へ
移行。
**診断ログ第2弾・決定的発見（2026-07-18、忍者、`docs-notes/t044-diag-log-koudzu148-ninja.log`）**：
最初の再現記録と同一手順（行1列5→行4列5→行8列3、座標も同一値）で実施し、視覚的にも同じバグ
（行8左側配線完全欠落）を再現。ログでは`NothingBetweenRailAndColumn(row=7,column=3)=True`・
`LeftTerminator(row=7,lb=3)→null(母線まで延ばす)`と**判定ロジック自体は正しく「母線まで延ばす
べき」と結論している**にもかかわらず、**実際の画面には母線までの線が全く引かれていない**。
**真因の絞り込み＝判定ロジック（NothingBetweenRailAndColumn/LeftTerminator）ではなく、その判定
結果を反映する描画処理側の不具合**（もう一段階描画寄りの箇所、別の描画経路に阻まれている可能性）。
証拠画像=`docs-notes/screenshots/t044-diag-repro-148-full.png`。原因特定を侍・隠密へ委譲、次段階は
「LeftTerminatorがnullを返した後、実際に母線まで線を描画する処理はどこか、なぜ実行されないか」の
追跡。
**描画処理側の追跡完了・座標計装を提案（2026-07-18、隠密）**：`LadderCanvas.cs`は`DiagramRenderer
.Render`単一経路のみ呼んでおり画面/PDF出力とも同一経路（別経路で上書きされる懸念は排除）。
`LeftTerminator`null後の`DrawRungSegment`→`DrawWire`→`r.DrawLine`を仔細に追ったが、線を消す・
スキップする要因はコード上見当たらず（`if (xR<=xL) return;`ガードも通常成立せず、`DrawLine`自体は
`net`パラメータ有無に関わらず無条件実行）。行7のページ範囲外仮説も検討したが、これが真因なら接点C
自体も非表示になるはずのところ実際は表示されており矛盾、排除。**次の一手＝座標計装**——
`DrawRungSegment`呼び出し直前・`r.DrawLine`呼び出し直前で実際に渡される座標(xL,xR,y)・lb・rowの
値をログ出力し、判定は正しいのに実描画が起きない原因を座標計算自体(`X(lb)`等)に絞って追跡する
方針。侍へ采配。
**座標計装・決定的発見（2026-07-18、忍者、`docs-notes/t044-diag-log-koudzu148-v2-ninja.log`）**：
「行1/4/8」構図で再実測、同じバグ再現。**`DrawWire`呼び出し自体は行8の左側配線に対しても正しく
発生している**（`DrawWire: (15.50,87.50)-(47.00,87.50) net=0`←行8左側配線）——座標値(X=15.50〜
47.00,Y=87.50)は行1・行4の同種呼び出しと同型で異常なし。にもかかわらず**この呼び出しだけ実際の
画面には一切表示されない**（行1・行4は正常表示）。**真因のさらなる絞り込み＝判定ロジックでも
DrawWire呼び出しの発生有無でもなく、DrawWire呼び出し後の実描画パイプライン（内部実装・後続の
クリップ/上書き処理等）**。証拠画像=`docs-notes/screenshots/t044-diag-repro-148-v2-full.png`。
侍・隠密の継続調査待ち。
**パイプライン追跡・2仮説排除、残る筋を提示（2026-07-18、隠密）**：`WpfRenderer.DrawLine`実装自体は
単純明快で特別分岐なし。**クリッピング仮説は排除**——`PushClip`は`DrawFreeLines`/`DrawFrames`の2箇所
のみ、いずれもY方向限定・X方向は広大な範囲で行7左側を狙い撃ちで隠せず、しかも各関数内でPush直後に
Popし`DrawRungWires`実行時点では解除済み。**色(Stroke)仮説も排除**——行7左側`net=0`は行0左側
`net=0`と同一net値ゆえ色計算ロジックは同一結果になるはずで片方だけ透明化する理由なし。**残る筋＝
`DrawRungWires`実行後の`DrawConnectors`/`DrawElement`(選択ハイライト含む)等、後続描画呼び出しに
よる物理的な上書き**——行7・列3付近特有の何か(既存縦コネクタ`col6:r0-3`の描画、選択中接点Cの
ハイライト矩形等)が座標(15.50〜47.00,y=87.50)と偶然重なっている可能性。侍へ`DrawConnectors`・
選択ハイライトの実描画座標ログ計装を追加采配、DrawWire座標との突き合わせが次の一手。
**再実測・座標重なり仮説は否定（2026-07-18、忍者、
`docs-notes/t044-diag-log-koudzu148-v3-drawconnectors-ninja.log`）**：DrawWire終点X=47.00と
DrawElement始点X=47.00は隣接するが重ならず（境界一致のみ）、DrawConnectors(X=74.00)は全く別の
X座標——3者とも座標上の衝突なしと確認、隠密の物理的上書き仮説は否定された。**新たな手がかり**＝
問題のDrawWire呼び出しは「配置確定時の1回」のみログに記録され、その後（Esc押下後等）の再描画では
記録が更新されていない——**選択状態変更等で別のRedrawCanvasが走った際に、この線を含まない形で
再描画されている可能性**（最終的に画面に反映されるのは最後のRedrawCanvas呼び出し結果のはず）。
証拠画像=`docs-notes/screenshots/t044-diag-repro-148-v3-full.png`。
**症状精緻化を撤回・目視誤読と判明（2026-07-18、殿直接ご指摘）**：「行4の左側が完全に欠落」という
上記の精緻化情報は隠密の目視誤読と判明し撤回。3倍拡大画像を殿ご自身が確認され「行4には左端(母線)
から接点まで横線が明確に描画されている」とご指摘。真の断線箇所は再調査中、診断ログ注入自体（計装
内容は変わらず）の采配は有効のまま進行。正確な症状は隠密の再報告待ち。
**再訂正（2026-07-18、隠密）**：フルサイズ・3倍拡大とも再確認したところ行4・行8とも母線から接点
まで薄いグレーの線が実在（解像度・圧縮で視認しづらかっただけ）——「完全欠落」という目視判定自体が
不適切だったと認め撤回。**静止画の目視のみでは断線箇所を確定できないと判断**（`ecad2-ui-automation`
スキル既存の「画素採取が万能でない例外」節と同種の罠、新規追記は不要と判断）。以後は診断ログの
実測データが整うまで目視での断定を控える。
**新規バグ報告、決着・バグ非実在と確定（2026-07-19、忍者、3手法+殿目視の裏取り）**：診断ログで
`DrawWire`が正しく発火している（前述、行7左側配線 `(15.50,87.50)-(47.00,87.50) net=0`）にも
関わらず画面に反映されないという矛盾を追っていたところ、殿の実機目視「行8左側配線は確実に
繋がっている」との指摘を機に検証手法自体を疑い、忍者が同一状態をPrintWindow・CopyFromScreen・
PDF出力の3手法で撮り比べ。結果＝CopyFromScreen・PDF出力・殿目視の3者は「正しく繋がっている」で
一致、**PrintWindow方式のみが異常値（欠落して見える）**。WPFのDirectXハードウェアアクセラレーション
描画とGDIベースのPrintWindow APIとの間のタイミング起因の一過性不整合が原因と推定（隠密裏取り、
`PW_RENDERFULLCONTENT`フラグは`ecad2-ui-automation`スキルで既に使用済みのためフラグ未指定が原因
ではない）。**これまでの構図A・殿サンプル図面(setuzoku.gcad)での「視覚的断線」実機報告も含め、
全て検証手法(PrintWindow)側の誤検出だったと確定**——判定ロジック（`LeftTerminator`/
`NothingBetweenRailAndColumn`）・描画ロジック（`DrawWire`）自体は最初から正しかった。**この件に
関する実装修正は不要**。教訓は`ecad2-ui-automation`スキルへ制度化済み（隠密、`SKILL.md`6節・
memory`feedback_printwindow_capture_limitation_t044.md`）。
**訂正・家老の見出し「決着」は誤り（2026-07-19、殿直接ご指摘・画像提示）**：上記でPrintWindow
誤検出と確定したのは「行8左側の母線への水平配線」という**これまで忍者・隠密が追っていた症状**に
ついてのみであり、この結論自体は覆らない。しかし殿が最初にご指摘の新規バグ「分岐線が1段上までしか
連結しない」の正確な再現ではなかった可能性が高いと判明——殿が新たに提示した実機スクリーンショット
（`C:\Users\kojif\Desktop\claude_TEMP\T-044_bug.png`）が示す症状は**別物**：接点Cの右側から上へ
伸びる縦分岐線が、本来到達すべき「線番2」（コイル手前のネット）まで届かず、手前（行4付近の高さ）
で止まっている。構図＝行1(N24→A接点→分岐点→線番1→無名接点→線番2→コイル→P24、分岐点から行4へ
B接点が並列)、行7にC接点があり右の配線が縦分岐点(行5付近)から上へ伸びるべきところ短く終わる。
**この症状は未検証・未解決のまま**、忍者へ正確な構図での実機再現を再依頼中。一時診断ログ計装は
除去せず維持（侍へ指示済み）。

**実機再現・本物のバグと確定（2026-07-19、忍者、3手法一致）**：殿が実機で構築した該当構図
（A/B並列→線番1→接点→線番2→コイル、C接点をOR追加）をそのまま`C:\ECAD2\sample\T044-sample.gcad`
として保存、PrintWindow・CopyFromScreen・PDF出力の3手法**全てが一致**して「C接点右側の縦分岐線が
分岐点(行4付近)で途切れ線番2まで届かない」ことを確認——今回は3手法一致ゆえキャプチャ手法の限界
ではなく実描画バグと確定。

**原因確定（2026-07-19、隠密、診断ログ+一次ソース精読）**：真因は`DiagramRenderer.RightTerminator`
がrow(基準行)を起点(TopRow)とする下流分岐コネクタを一切見ず、`BottomRow`側のみで「B自身の右端で
終端」と誤判定していたこと。B接点は(1)A-B間結線の**入力側**(BottomRow)と(2)B-C間結線の**出力側
起点**(TopRow)の両方の役割を同時に持つが、旧ロジックは(2)を無視していたため、B-C間の縦コネクタ
(離れた列)まで横線が届かず、視覚的に「浮いて」見えていた。真因はT-026(2026-07-03、`docs/proposed.md`
P-003)で`(TopRow||BottomRow)`から`BottomRow`限定へ意図的に変更した副作用——2026-07-07に
P-028として理論予測・実証済みだった「右合流側の隙間」現象と同一と確定。

**修正完了・コミット4e11c3a・push済み（2026-07-19、侍）**：`RightTerminator`へ「rowをTopRowとする
下流分岐(Column>=rb)があれば最大列まで延ばし、無ければ従来のBottomRow側最小列へフォールバック」
という判定を追加（隠密設計）。NetlistBuilder(電気的判定)は無変更。新規回帰テスト
`DiagramRendererRightTerminatorTests.cs`（A-B縦積み+B-C横ずれ連鎖）をRED先行証明の上追加、
Core.Tests121件・App.Tests716件全合格。診断ログ計装（AppendDiagLog等）は原因確定後に除去済み
（除去後、`MainWindowViewModel.cs`はHEADと完全一致=このファイルには診断ログ以外の変更は無かった
と判明）。

**なお元々のT-044本題（OR自動配線の冗長縦分岐抑止、2026-07-07裁定）はBlockedのまま変わらず、
再開要否は引き続き殿判断待ち。今回Doneとしたのは殿新規指摘分のうち「視覚的な隙間の解消」のみ。**

**訂正・電気的な結線は未達成と判明（2026-07-19、隠密、`sample/T044-sample.gcad`直読+ネット追跡）**：
殿が実機で提示した「意図する正しい回路」（証跡=`docs-notes/screenshots/t044-tono-intended-circuit
-ninja.png`）は、C接点右側が**A/B並列ブロック全体の出力ネット（線番2、X接点〜コイル間）へ直接
合流するバイパス経路**。しかし`RightTerminator`修正は描画（横線の到達範囲）のみを変え、電気的な
接続先（`sheet.Connectors`が表すネット）は無変更——`ResolveNode`ロジック手計算追跡により、
Cの縦コネクタは`B.rightNode`=`A.rightNode`=**線番1のネットに帰着**しており、殿が意図する線番2
には到達していないと確定。忍者の追加実測（殿の意図通りに手動修正した図面でDRC再実行しても、
出力が元図面と一字一句同一）も裏付け——**DRCはこの種の未接続を検査対象にしていないため、当初
「DRC実測と符合」とした家老の伝達は誤りだった（訂正済み）**。
**根本原因＝OR自動配線の基準選択ロジック自体の設計限界（隠密所見）**：現行の`baseRow`探索は
「直近上の行を機械的に選ぶ」のみで、「どのネット・どの既存ブロックへ合流させたいか」という
操作意図を認識する手段が原理的に存在しない。描画層の修正では解決不可、基準選択ロジックそのものの
再設計を要する可能性が高い。**殿裁定（2026-07-19）：新規タスク化し後回し（他の修正を優先）**、
詳細はT-102参照。

### T-102 OR自動配線、既存並列ブロックへの合流先を操作意図に基づき選択できるようにする — Approved（gated、殿直接指示2026-07-19、優先度低・後回し）

**起票=T-044調査の派生（2026-07-19）**。3階層以上の並列（OR）構成——例：A/B並列ブロックに対し、
Cをさらに並列（バイパス）として追加したい場合——で、現行のOR自動配置ロジックが「直近上の行」を
機械的に基準として選ぶだけのため、Cが実際にはBとのみ結線され、殿が意図する上位ネット（A/B
ブロック全体の出力）へは合流しない。電気的な誤配線であり、`RightTerminator`等の描画層修正では
対処不可——**OR自動配線の基準選択ロジック自体（`baseRow`探索、`MainWindowViewModel.cs`）の
再設計を要する**（隠密所見、詳細はT-044節参照）。操作意図（どの既存ブロックへ合流させたいか）を
システムがどう認識するか自体が設計課題——GX Works3等の先行UIでの類似ケースの扱いも含め、
着手時に調査・UI/UX分岐は必ず殿確認【MUST】。**殿裁定（2026-07-19）：優先度低、他の修正
（T-099残り2件・T-067(4)等）を優先し、当面は後回し。**

### T-028 浮動インライン入力ダイアログの「拡張表示」ボタン — Proposed（gated）

T-026段階4の浮動インライン入力ダイアログ実装時、仕様未確定のため侍が未実装のまま起票（2026-07-03）。
殿確認済み：今回は実装不要、別タスク化。位置・レイアウトの殿注文3点は殿裁定によりT-033へ統合済み
（T-033増分3で拡張表示ボタンの「配置のみ」実装済み・押下無反応・当面Tab除外。押下時の詳細画面が
本タスクの残スコープ）。

### T-022 ステータスバーの高情報密度化（機種名/局番/ステップ数等） — Proposed（gated）

T-009残課題9。機種名/局番/ステップ数など他機能への依存があり、依存元の実装状況次第。

### T-013 ツールバー/メニューアイコンの本格的な意匠制作 — Proposed（gated）

T-009段階3では簡易プレースホルダ（Path Geometry/Unicode記号）で仕組みのみ実装。
本格的な意匠（記号・単色ベースのグラフィックデザイン）は別タスクとして切り出し（家老裁量）。
T-040でツールバー配置系ボタンはGX様式グリフ化済み。

### T-032 CSV取り込みによる図面自動生成 — Proposed（将来構想、gated）

殿の将来構想（2026-07-03）。GX Works3が出力するCSV（SHIFT-JISエンコード、ニーモニック命令列：
LD/AND/ANI/OR/ORI/OUT等）を読み込んでラダー図を表示・自動生成したい。実装時はニーモニックと
回路記号のマッピングが必要（T-031の調査結果が基礎資料になる見込み）。GX Works3の実際のCSV出力
フォーマット（列構成、命令表記の実例）は未調査。詳細仕様・着手時期は未定。

## 完了・取り止めタスク索引（詳細経緯は `docs/todo-archive.md`）

並びはおおむね着手順。

- [x] T-001 技術スタック裁定（WPF本命仮確定）
- [x] T-002 フォーカス保持PoC
- [x] T-003 最終スタック確定（WPF正式確定）
- [x] T-005 WPF非技術面検証
- [x] T-006 タブ切替フォーカス喪失対策
- [x] T-004 4セッション体制・雛形/アーキ設計
- [x] T-007 GuiEcad実ソース移植（全層）
- [x] T-011 保存先フォルダ名変更（GuiEcad→Ecad2）
- [x] T-008 UI/UX全体像設計（殿裁定）
- [x] T-010 GX Works3 UI/UX調査（Web一次情報）
- [x] T-012 GX Works3実機追加調査
- [x] T-014 GX Works3技術スタック調査
- [x] T-031 ニーモニック（接点表記）の扱い方基準の調査
- [x] T-009 Ecad2.App UI実装（骨格、全8段階）
- [x] T-024 gui_ecad 新規/開く/保存フロー調査
- [x] T-025 gui_ecad 要素選択・ヒットテスト調査
- [x] T-016 要素配置ロジック本体
- [x] T-026 左パネル→ナビツリー化・ツールバー移行（2026-07-03）
- [x] T-030 グリッド線表示の有効化（殿直接依頼）
- [x] T-017 要素選択・編集フォーカス制御の本実装（2026-07-03）
- [x] T-027 選択中セルの視覚的ハイライト表示（T-017に統合）
- [x] T-018 DesignRuleCheckと下部出力パネルの接続（2026-07-04）
- [x] T-020 空状態⇔作業領域の動的切替（2026-07-04、濃紺#24325A確定は2026-07-05）
- [x] T-023 LadderCanvasアクセシビリティ強化（2026-07-04）
- [x] T-021 キーボード規約の残り（Enter配置・Esc4層・パン追従、2026-07-05 mainマージ）
- [x] T-019 ドキュメント管理（新規/開く/保存、完全Done 2026-07-05）
- [x] T-015 図形ビジュアルプレビュー（部品リストサムネイル、2026-07-05）
- [x] T-036 配置時の機器表即時反映＋デバイス名編集修正（完全Done 2026-07-05）
- [x] T-038 診断ログ連携運用の整備（2026-07-05）
- [x] T-034 App層テストプロジェクト新設（完全Done 2026-07-06）
- [x] T-035 .gcadpart読込時のID重複検出＋再採番（完全Done 2026-07-06）
- [x] T-037 部品選択リストへORa/ORb追加・固定7種化（完全Done 2026-07-06）
- [x] T-039 操作トレースログ基盤（完全Done 2026-07-06）
- [x] T-042 App層テストの実環境副作用解消（完全Done 2026-07-06）
- [x] T-040 ツールバー配置系ボタンのGX様式化（完全Done 2026-07-07）
- [x] T-043 ORa/ORbサムネイルのシンボル統一（完全Done 2026-07-07）
- [x] T-033 配置後入力の真の非モーダル浮動インライン化（全5増分、完全Done 2026-07-07）
- [x] T-041 主回路用の横配線・縦分岐線の手動記入＋消去＋修正（全7増分、完全Done 2026-07-08）
- [x] T-045 App層リファクタリング（全4増分＋補遺2、完全Done 2026-07-09、mainマージ`5f2ee6e`）
- [x] T-029 ツールバーボタン配置時のゴースト表示（**Rejected 2026-07-09**＝キーボードファースト
  理念と相反するため不要と殿裁定。先行調査書`docs/archive/ecad2-t029-presurvey-onmitsu.md`は参考資料として収蔵）
- [x] T-047 手動配線系（F9/F10系）のツールバーボタン作成（5ボタン+シート種別連動グレーアウト+
  並び替え[選択→F5〜F10→区切り→部品]+無効時半透明化、**完全Done 2026-07-09**。修正往復1周=
  フォーカス残留+接続点誤配置を制度適用[隠密設計→侍修正]で解消。グリフ変更は次回T-048へ）
- [x] T-049 デバイス名編集中の未確定編集を確定してから保存（P-013起票、**完全Done 2026-07-10**。
  隠密レビュー要修正なし・忍者実機全観点OK。範囲外の気づき=P-045）
- [x] T-048 手動配線ボタン（sF9・F10系）のグリフ変更（殿意匠提示→プレビュー承認制で往復2回調整、
  **完全Done 2026-07-10**。制御シート=矢印形・はさみ形、主回路シート=既存の棒・点を維持。
  隠密レビュークリーン・忍者実機回帰4観点OK）
- [x] T-050 TraceLogの全角ラテン文字正規化統一（P-014/P-015統合。往復3周=隠密レビュー2周+
  殿裁定のテスト補強3周目、隠密レビュー3回クリーン・忍者実機全観点OK・Stryker棚卸し3件は
  殿裁定で経過観察、**完全Done 2026-07-10**。期間中の出力破損§5離脱2回を引き継ぎ書2本で
  作業損失ゼロで完遂）
- [x] T-051 シート追加・削除操作をUndo対象に含める（Undo/Redo基盤MVP新設、往復3周、完全Done 2026-07-11）
- [x] T-052 未解決PartIdフォールバックのDRC警告追加（往復1周、完全Done 2026-07-11）
- [x] T-053 機器表「種別」列の日本語表示化（完全Done 2026-07-11）
- [x] T-054 部品選択リストの選択中部品を配置バー内に表示（完全Done 2026-07-11）
- [x] T-055 行数拡張のGuiEcad方式踏襲＋母線番号入力の同仕様化（全増分1〜3、完全Done 2026-07-11）
- [x] T-056 キャンバスのグリッド線表示切替機能（完全Done 2026-07-11）
- [x] T-057 v0.2仮リリースビルドの作成（完全Done 2026-07-10）
- [x] T-059 出力パネルの高さをドラッグで調整可能にする（完全Done 2026-07-11）
- [x] T-062 .NET 10（net10.0-windows）への移行（完全Done 2026-07-11、mainマージ済み）
- [x] T-063 「名前を付けて保存」「削除」のメニュー露出（往復1周、完全Done 2026-07-11）
- [x] T-071 経路B部品（押釦・タイマ接点等）の部品選択リスト追加（10種+専用グリフ、往復1周、完全Done 2026-07-11）
- [x] T-072 v0.3仮リリースビルドの作成（完全Done 2026-07-11）
- [x] T-073 ecad2-ui-automationスキルのSendKeysフォーカス誤爆対策（P-056対策、往復1周、完全Done 2026-07-11）
- [x] T-074 「バージョン情報」ダイアログへのバージョン番号表示（完全Done 2026-07-11）
- [x] T-075 主要機能の仕様書整備（全11領域、完全Done 2026-07-11、docs/spec/配下収蔵）
- [x] T-076 docs/配下の整理（173→37件、136件をarchiveへ移動・リンク327箇所更新、完全Done 2026-07-12）
- [x] T-078 todo.mdの軽量化（958→435行、15タスク分をarchiveへ移送、完全Done 2026-07-12）
- [x] T-065 ドキュメント情報の編集UI（往復1周、完全Done 2026-07-12）
- [x] T-066 機器表のBOM編集（型式のみ、往復1周+緊急バグP-058対応、完全Done 2026-07-12）
- [x] T-079 機器配置直後の保存操作で機器表エントリが消失するバグ修正（P-058、完全Done 2026-07-12）
- [x] T-060 PDF出力機能のUI結線（プレビュー・全シート・常に枠あり、往復1周、完全Done 2026-07-12）
- [x] T-081 GuiEcad仕様書の作成（全11領域、比較3表つき、docs/spec/guiecad-spec-*収蔵、完全Done 2026-07-12）
- [x] T-061 テストモード機能のUI結線（A-1構造対処含む往復2周+忍者実機全観点OK、完全Done 2026-07-14）
- [x] T-070 検索・置換機能（往復4周、完全Done 2026-07-14）
- [x] T-086 セレクトSWのノッチ番号(Position)設定UI新設（完全Done 2026-07-14）
- [x] T-087 ショートカットキー追加（部品パネルF11・テストモードCtrl+T、往復5周・完全Done 2026-07-14）
- [x] T-088 基本図形（Element）の配置後移動機能新設（完全Done 2026-07-14）
- [x] T-090 Ctrl+Shift+Up/DownのCanExecute素通り修正（完全Done 2026-07-14）
- [x] T-091 F5〜F10グローバルショートカットのHasAnyDraft見落とし修正（完全Done 2026-07-14）
- [x] T-092 ドラフト中の行操作/Undo/Redoによる無警告ズレ確定の防止（ブロック方式、完全Done 2026-07-15）
- [x] T-093 ShouldAllowShortcutPlacement/ShouldSuppressPartSelectionActivationの重複実装統合（完全Done 2026-07-15）
- [x] T-094 Ctrl+Shift+Up/Down・Ctrl+Z/YへのIsCanvasFocused判定追加（完全Done 2026-07-15）
- [x] T-095 ツールバー1段目のラベル表示方式変更（ショートカット表示→機能名表示、完全Done 2026-07-15）
- [x] T-058 パネル（ツールバー含む）のドック化・フロート配置機能（AvalonDock導入、全5増分完全Done 2026-07-15。左パレット・出力パネル・右パネル・ツールバー2段目のドッキング化＋レイアウト保存/復元＋Ctrl+Alt+Rリセット）
- [x] T-085 表示灯(Lamp)の色記号入力UI新設（フリーテキスト記号方式、完全Done 2026-07-15）
- [x] T-096 タイマー設定時間（Setpoint）入力UI新設＋残り時間リアルタイム表示（GuiEcad完全踏襲、完全Done 2026-07-15）
- [x] T-084 シート削除時の後始末2件（PageNumber欠番警告+DRC結果破棄案内、完全Done 2026-07-15）
- [x] T-097 ラベル高さオフセット（LabelDy）入力UI新設＋コイル機器名の中心配置検証（往復2周、完全Done 2026-07-15）
