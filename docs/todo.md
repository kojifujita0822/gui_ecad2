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

### T-111 ステータスバー表示改善（モード日本語化・警告文字色ダークモード対応） — Approved（auto-OK、殿直接指示2026-07-22）

**起票=殿直接指示2026-07-22**、2件の指摘（家老確認・調査済み）：

1. **「モード: {0}」の日本語化**：`MainWindow.xaml`1438行、`{Binding Mode}`（`AppMode` enum、
   `ToolState.cs:34` `Drawing`/`Test`の2値）がConverter未適用のままenum名（英語）で表示される。
   T-109（Tool.Mode→日本語、`ToolModeToTextConverter`）と同型の表示専用変換を追加し、
   `Drawing`→「作画」、`Test`→「テスト」と表示する（内部enum自体は変更しない）。
2. **警告文字色のダークモード対応**：`MainWindow.xaml`1469行、`StatusMessage`のForegroundが
   `Foreground="DarkRed"`固定でテーマ非対応。殿指摘「ダークモード時、赤は視認しづらい」。
   **ダークモード時のみ**、既存の選択ハイライト色`Brushes.OrangeRed`（`LadderCanvas.cs`の
   `SelectedCellPen`等、コードベース内で唯一のオレンジ系色、殿の言う「選択ツール色のオレンジ」に
   相当と家老判断）へ変更する。ライトモードは現状のDarkRedを維持（殿指摘は「ダークモード時」限定）。
   実装方式はT-101の`PlacementToolActiveBackgroundBrush`等と同型：`Theme.Light.xaml`/
   `Theme.Dark.xaml`双方に新規ブラシキー（例：`WarningMessageBrush`）を定義し、XAML側は
   `Foreground="{DynamicResource WarningMessageBrush}"`へ変更する。

侍へ実装を采配。

### T-112 Undo/Redo「0枚袋小路」対処 — Done（2026-07-22、検証パイプライン完了）

**起票=P-120**（隠密根本原因調査`docs/ecad2-t110-increment2-sheet-loss-investigation-onmitsu.md`）。
Undoでシート0枚状態まで遡ると、Redoスタックにデータが残っていてもRedoの`CanExecute`が`HasProject`
ゲートで無効化されUIから復旧不能になる袋小路を検出。さらに0枚から新規シートを追加すると
`RecordSnapshot`が`_redoStack.Clear()`を呼ぶため復旧の道が完全に閉じ、実ユーザーが誤ってUndoを
連打すると恒久的なデータ喪失を招きうる。

**対処方針（殿裁定2026-07-22＝隠密提示の対処案2つのうち案1採用）**：RedoのCanExecuteから
`HasProject`ゲートを外し、0枚状態でもRedoスタックにデータがあればRedoボタンを有効化、UIから
復旧できるようにする。Undo/Redoの基本挙動（0枚まで遡れること自体）は変更しない。

**実装完了（2026-07-22、コミット`25896a8`）**：`RedoCommand`のCanExecuteのみ
`Mode==AppMode.Drawing && !HasAnyDraft && UndoManager.CanRedo`へ変更（`HasProject`ゲート除去、
`UndoCommand`・`CanEditDiagram`本体は無変更）。RED先行証明済み（修正前FAILED実測→修正後PASSED）。
隠密静的レビュー完了（指摘事項なし）。忍者実機確認完了（シート追加→Undo→0枚→Redo有効化→Redo実行
→復元、複数シートでの通常Undo/Redoにも副作用なし、いずれもOK）。build/test exit 0（Core131/App797）。

### T-113 配置ツールバーペイン既定高さの調整 — Done（2026-07-22、検証パイプライン完了）

殿がEcad2.App実機にて配置ツールバーペインの高さを手動リサイズ、この値を既定サイズとして反映して
ほしいとの仰せ。忍者計測（UIA、ウィンドウ左上基準の相対座標）：
- 配置ツールバーペイン全体（タブ行+ツールバー本体）Top=55px, Height=100px
- 内訳：ツールバー本体（ボタン行）のみ Height=48px

**追加指示（殿2026-07-22）**：既定高さ(Height)だけでなく`MinHeight`も同時に100pxへ設定する。

`MainWindow.xaml`該当ペインの高さ定義（RowDefinition/Height指定・MinHeight指定等）をこれらの値へ
変更する。

侍へ実装を采配（T-110バグ修正完了後に着手、優先度はT-110が上）。

**実装完了（2026-07-22、コミット`bb1a9dd`）**：`DockHeight`123→100、`DockMinHeight`新規100を設定
（変更は該当ペイン1属性行のみ）。build/test exit 0。隠密静的レビュー完了（指摘事項なし）。殿より
「初期状態に戻して」とのご指示を受け、既存`main-layout.xml`（DockHeight=123保存済み、T-058仕様で
起動時優先されるため既定値変更のみでは反映されない）を退避（rm不使用、バックアップ確保）。忍者が
初回起動状態でUIA計測、ペイン全体Top=55px/Height=100px・ツールバー本体Height=48pxいずれも殿実機
計測値と完全一致を確認、OK。

### T-114 技術的負債の一括清掃（隠密レビュー由来の軽微cleanup12件） — Approved（auto-OK、殿裁定2026-07-22）

**起票=proposed.md pending仕分け**（家老が実害なし・単発の構造改善提案12件を分類・提示、殿裁定
2026-07-22「まとめて1タスク化しバッチ処理」を採用）。個別出所はいずれもT-064/T-069/T-080の隠密
静的レビュー（code-review併用）由来、severity低〜経過観察扱い、実害はいずれも未達または軽微。

DoD（各P-IDの指摘を個別に対処、既存テストGREEN維持が前提）：
1. P-061: `MainCircuitContentMaxX`等の重複計算ロジックを共有ヘルパーへ集約
2. P-062: F2キー経路と`HitTestRungCommentRow`の適格条件重複を共有ヘルパー化
3. P-064: `InverseBool`未使用リソース削除、`RecordingRenderer`テストダブル重複定義の解消
4. P-065: `PositionPlacementBar`のクランプ基準をCanvasArea化（課題3修正の横展開）
5. P-068: `SetCurrentSheetIndexWithoutCrossCut`等の`OnPropertyChanged`へnameof明示追加（2箇所）
6. P-073: `ClampResizeTarget`の二重異常条件無効化、意図的仕様か見直すか判断のうえ対処
7. P-074: `currentMm >= anchorMm`の等号偏りの見直し
8. P-075: `HitTestImage`の冗長呼び出し（右クリックハンドラ内2回）を1回化
9. P-076: `ClampResizeTarget`の2つの独立if文を`hasRoom`判定へ統合
10. P-081: 機器表「型式」列に`CanEditDiagram`ガードを追加（テストモード中の編集不可化）
11. P-082: 配置系ボタン群の`IsEnabled`複製構造をStyle既定値化
12. P-085: `IndexOfSelectedCellOrZero`のシート比較条件へのテスト追加
13. P-117副次気づき: `Theme.Dark.xaml`のコメント記載値`#FF202224`と実際の
    `DrawingTheme.Dark.Background`計算値`#FF202226`の1桁ズレをコメント修正（視覚上判別不可・
    実害なしのコメント誤記、隠密所見2026-07-22）

各P-IDの詳細・出所コミットは`docs/proposed.md`参照。規模は各単発は小、合計12箇所のためRED先行
証明は該当項目（P-085等の振る舞い変更を伴うもの）のみ適用。検証パイプラインは通常どおり
侍実装→隠密静的レビュー→忍者実機確認（見た目に影響する項目=P-081/082のみ実機確認必須、他は
静的レビューで足りるか家老が采配時に仕分ける）。

### T-115 ダークモードの空状態/作業領域背景色の区別復元（P-117対処） — Approved（auto-OK、殿裁定2026-07-22）

**起票=proposed.md P-117対処案**（隠密調査`docs/ecad2-p117-darkmode-color-unification-intent-investigation-onmitsu.md`）。
増分2実装（ダークモード対応）で`EmptyStateBackgroundBrush`が独自色を持たず、`WorkAreaBackgroundBrush`
と同一（Core層Dark.Background系#FF202226相当）に流用されてしまった見落とし（断定不可だが可能性高、
6点の根拠あり）。ライトモードは元々「作業領域=Core層Default.Backgroundと一致・空状態=Core層と
無関係な独自の濃紺#24325A」という2方針の組合せだったが、ダーク化時に後者が踏襲されなかった。

**殿裁定2026-07-22（配色案比較の実機確認Artifactを提示、3択より選定）＝案1採用**：ライト濃紺
#24325Aと同色相の暗色バリエーション（目安#18223D〜#1A2440）。GX3由来の「状態依存で配色を変える」
コンセプトを色相ごと継承する方向。具体的な1点の値は範囲内で侍が実装時に決定してよい。

DoD：
1. `Theme.Dark.xaml`の`EmptyStateBackgroundBrush`（ダーク値）を上記範囲内の値へ変更
   （`WorkAreaBackgroundBrush`側は現状維持、変更なし）
2. シート空状態（要素未配置）と作業領域（要素配置後）が、ダークモードでも視覚的に区別できることを確認

検証：規模小（1ブラシの値変更のみ）だが色・視認性に関わる観点のため、忍者実機確認は**画素採取
による機械的判定**（目視でなく座標指定のRGB値確認、殿裁定2026-07-16のルール適用）。

### T-116 FreeLine/ConnectionDotの当たり判定順序をGuiEcad原本の優先順位へ復元（P-107対処） — Approved（auto-OK、殿裁定2026-07-22）

**起票=proposed.md P-107対処**（隠密調査`docs/ecad2-p107-freeline-connectiondot-order-investigation-onmitsu.md`、
実害あり=可能性が高い）。GuiEcad原本（`MainPage.Pointer.cs`354行）は「線の交点上に置かれるため
自由直線より先に判定」という明示コメントつきでConnectionDotを先に判定するが、ecad2
（`MainWindow.xaml.cs`1768-1780行、T-041増分5）はFreeLine→ConnectionDotの順に逆転している。
当たり判定半径が両者同程度（`LadderCanvas.cs`116-117行、いずれも2.0mm）で範囲が重複するため、
主回路シートで自由線交点上に接続点(F10)を配置する通常運用で、常にFreeLineが選択され
ConnectionDotが選択・移動・削除いずれも実質的に到達不能になりうる。

DoD：
1. `MainWindow.xaml.cs`1768-1780行、FreeLine判定ブロックとConnectionDot判定ブロックの前後を
   入れ替え、GuiEcad原本と同じ「ConnectionDot先→FreeLine後」の順序へ復元
2. 主回路シートで自由線を配置→その交点上に接続点(F10)を配置→クリックでConnectionDotが正しく
   選択されることを確認（回帰テスト追加）

検証：規模小（2ブロックの前後入替のみ）。忍者実機確認は「自由線交点上に配置した接続点をクリック
で選択できるか」を具体観点に含める（隠密調査は机上のコード読解に留まり実機未確認のため、ここで
初めて実機確認する）。

### T-117 シート削除時の機器表クリーンアップ欠落を修正（P-104対処） — Approved（auto-OK、殿裁定2026-07-22）

**起票=proposed.md P-104対処**（隠密原因確定`docs/ecad2-p104-p109-cause-investigation-onmitsu.md`、
パターン再発台帳PR-12候補）。既存の削除系操作（`DeleteSelectedElement`単体要素削除・
`DeleteRowAtCommand`行削除）は`RemoveDeviceIfUnreferenced`/`CleanupRemovedDeviceNames`で機器表
(`Document.Devices.ByName`)クリーンアップを行うが、`SheetNavigationViewModel.DeleteCommand`
（シート削除）だけこの呼び出しが欠落している。シート丸ごと削除すると`sheet.Elements`は消えるが
機器表エントリだけゴーストとして残存する。

DoD：
1. `SheetNavigationViewModel.DeleteCommand`に、削除対象シートの`sheet.Elements`が参照していた
   機器名を`CleanupRemovedDeviceNames`（他要素からも参照されなくなったもののみ除去）で
   クリーンアップする処理を追加
2. シートに要素配置(機器名設定)→シート削除→機器表に旧機器名が残らないことを確認する回帰テスト追加

検証：規模小。忍者実機確認は「シート削除後の機器表」を具体観点に含める。

### T-118 シート改名時の選択色消失を根本対処（P-109対処・案A） — Approved（auto-OK、殿裁定2026-07-22）

**起票=proposed.md P-109対処**（隠密対処案検討`docs/ecad2-p104-p109-cause-investigation-onmitsu.md`
追記部）。忍者の追加検証で判明した正確な条件＝「改名対象がリスト末尾でない場合に選択色消失」
（当初「1番目限定」との報告は不正確、訂正済み）。根本原因＝`RenameCommand`が`Sheet`モデルの
`INotifyPropertyChanged`未実装を回避するため`Sheets.RemoveAt(index); Sheets.Insert(index,
sheet);`でListBoxコンテナを強制再生成する手法自体が、WPFの選択状態管理（`Selector`内部の
`_selectedItems`/`ItemInfo`）と衝突する（機序完全特定には`ItemContainerGenerator.cs`のさらなる
精読を要すが、対処案提示を優先し区切った）。

**殿裁定2026-07-22＝案A（根本対処）採用**：`RemoveAt`+`Insert`自体を廃止し、選択状態に一切触れ
ない方式へ変更する。

DoD：
1. `Sheet`の`Name`をラップする軽量ViewModelアイテム（`INotifyPropertyChanged`実装、`Name`
   プロパティのgetter/setterで`Sheet.Name`を仲介）を`SheetNavigationViewModel`層に新設。
   Core層`Sheet`モデル自体は変更しない（他Core層モデルと同じ「永続化対象はPOCO」方針を維持）
2. `RenameCommand`の`Sheets.RemoveAt`+`Insert`+`BeginInvoke(ContextIdle)`パターンを廃止し、
   ラッパーの`Name`変更時に`OnPropertyChanged(nameof(Name))`を発火するだけでListBox表示が
   更新される方式へ置き換え
3. `ListBox.ItemsSource`のバインディング先・`SelectedSheet`の型（`Sheet`のままかラッパー型
   経由か）等、関連コードへの影響範囲を精査し、既存の`AddCommand`/`DeleteCommand`等他コマンドの
   動作に回帰がないことを確認
4. シートが末尾/非末尾いずれの位置でも、改名確定後に選択色が消失しないことを確認する回帰テスト
   追加

検証：規模中のため通常の検証パイプライン（侍実装→隠密静的レビュー→忍者実機確認）。忍者実機
確認は「末尾シートの改名」「非末尾シートの改名」両方を具体観点に含める。

### T-119 配置ツールバーのタブ形状をAeroテーマ風に変更 — Approved（gated、殿直接指示2026-07-22）

**起票=殿直接指示2026-07-22**（AvalonDock公式ドキュメントのAeroTheme画面例を提示、「このタブの
形にしたい」）。家老がAvalonDock一次ソース（`AvalonDock.Themes.Aero`、Ms-PLライセンス）を取得し
正体を特定：タブ左側の丸みは単純な角丸ではなく`SplineBorder`という専用コントロール（`OnRender`
を自前実装、2本の`QuadraticBezierSegment`で曲線を描く、`source/Components/AvalonDock.Themes.Aero/
Controls/SplineBorder.cs`、124行）。一次ソースの`ControlTemplate`（`Theme.xaml`128-211行）は
`Grid.ColumnDefinitions`で**幅20pxの列にのみ**`SplineBorder`を配置し、残りは通常の直線Border
（`CornerRadius="0,2,0,0"`、右上のみ角丸）という構成。選択中タブは単色でなく縦グラデーション
（Aero原色は`#FCFDFE`→`#D2E6FA`）。座標を正確に再現したモックアップを提示し殿確認済み
（Artifact、v2で訂正——初版は曲線をタブ全幅に誤って伸ばしていた）。

**対象範囲**：配置ツールバーのタブ（「基本機能」「配置ツール」、`MainWindow.xaml`の
`PlacementToolBarPaneControlStyle`系ItemContainerStyle、236-334行付近）。AeroTheme画像自体は
AvalonDockの「ドキュメントタブ」用デザインで、配置ツールバーが使う「ツールウィンドウタブ」には
元々別デザインが使われるが、同じ形をecad2側で移植する。

DoD：
1. `SplineBorder`相当のカスタムControl（`OnRender`オーバーライド、2本のベジェ曲線描画）を
   ecad2側に新設（一次ソースのMs-PLコードを参考に実装、ライセンス表記の要否を確認）
2. 配置ツールバーのTabItem用ControlTemplateを、`Grid.ColumnDefinitions`（Width="20"+Width="*"）
   構成へ変更。左列に新設の曲線コントロール、右列に直線Border（右上2px角丸）
3. 選択中/非選択の背景を単色から縦グラデーションへ変更。色はecad2既存パレット
   （`PanelHeaderBackgroundBrush`/`PanelContentBackgroundBrush`系）をベースにしたグラデーション
   バリエーションとし、Aero原色そのままの流用はしない（既存のライト/ダーク両テーマとの整合を
   優先、具体的な色調整は侍裁量）
4. 選択中タブが隣接タブに重なる視覚効果（Aero原本のマイナスMargin、ZIndex調整）も踏襲するか
   どうかは実装時の見た目で判断してよい
5. ライト/ダーク両テーマで表示確認

検証：見た目の変更のため通常の検証パイプライン（侍実装→隠密静的レビュー→忍者実機確認）。
忍者実機確認は両テーマでの表示確認を含める（色に関わる観点は画素採取等の機械的判定を推奨）。

**往復2周目（殿実機確認2026-07-22、指摘）**：形状自体は問題ないが、**上下が逆**になっている。
配置ツールバーのタブは`TabStripPlacement="Bottom"`（既存仕様、`MainWindow.xaml`259行）でコンテンツ
（ツールバー本体）の下に配置されるため、タブの曲線（コンテンツへの接続部）は本来**上側**にある
べきだが、現在の`SplineBorder`はAero原本（`TabStripPlacement="Top"`前提）と同じ向きのまま描画され
「下端が曲線で垂れ下がる」形になっており、視覚的に不自然（殿所見：シートリストパネルと混同し
やすい）。`SplineBorder`を上下反転する対処が必要（`LayoutTransform`での反転、または内部の
`OnRender`座標計算にBottom用の分岐を追加、実装方式は侍裁量）。

**追加要望（殿2026-07-22、往復2周目のビルド確認前に合流）**：選択中タブの強調表示を強める。
現状は背景色変化+`FontWeight="Bold"`のみ（`PlacementToolBarTabItemTemplate`のControlTemplate.
Triggers、219-223行付近）。殿選択＝以下2点を組み合わせる：
1. 選択中タブの**外周に枠線を目立たせる**（アクセントカラー、既存の`PlacementTabBorderBrush`
   より明確に区別できる太さ・色で）
2. 選択中タブの**背景コントラストを強める**（現状の`PlacementTabSelectedBackgroundBrush`
   グラデーションを、非選択との差がもっとはっきり分かる色合いへ調整）
具体的な色・太さの数値は侍裁量でよい、ライト/ダーク両テーマで確認すること。

**往復3周目（殿実機確認2026-07-22、3点指摘）**：

1. **上下反転がまだ効いていない**。家老がスクリーンショット・`SplineBorder.cs`を突き合わせて
   原因を特定——`OnRender`の`fillFigure.StartPoint`は依然`(w, 0)`（右上）、`BezierSegment`の
   終点も依然`(0, h)`（左下）のまま。制御点の数・種類（2次×2→3次×1）は変わったが、**始点・
   終点自体は往復1周目から一切変わっていない**ため、「上部が細く下部が広い」という塗り分けの
   向きが変わっておらず、実質的に無反転。正しい修正＝Y軸を反転する（始点`(w, h)`〔右下〕→
   終点`(0, 0)`〔左上〕、下辺(y=h)側が細く・上辺(y=0)側が広くなるよう塗り分ける。境界線
   （`borderFigure`）側も同様に反転要）。`BottomBorderMargin`の意味（現状「上辺寄りの制御点の
   微調整」）も併せて反転後の基準（下辺基準）に合わせて見直すこと。
2. **「配置ツール」タブ選択時、タブ文字（見出し）が無表示になる**（「基本機能」選択時は
   「基本機能」の文字が正常表示、殿添付スクリーンショット参照）。原因未特定——`ContentPresenter`
   はGrid.Column="1"側で両タブ共通の構成のため、単純なXAML構造の差ではなさそう。`Header`が
   `LayoutAnchorableTabItem`（AvalonDock本体コントロール、`docs-notes/vendor-reference/
   avalondock-v4.74.1/source/Components/AvalonDock/Controls/LayoutAnchorableTabItem.cs`）経由で
   描画される仕組みのため、選択中(`IsSelected`)とアクティブ(`IsActive`)状態の組み合わせに応じた
   内部の色切替ロジックとの相互作用を疑う。対症療法（強制的にForeground上書き等）より先に、
   **なぜ「配置ツール」だけ起きるのか原因を特定してから対処すること**（侍または隠密、原因調査を
   優先）。
3. **強調色を水色系ではなく濃いグレーへ変更**。`PlacementTabSelectedBackgroundBrush`（往復2周目で
   青み系グラデーションへ変更済み、Light `#D6E9FA`・Dark `#FF2A3A4E`）を、濃いグレー系の配色へ
   差し替え。`PlacementTabSelectedBorderBrush`（T-101アクセントカラー流用、青系）も見直しが要るか
   判断すること（濃いグレー背景との組み合わせで視認性を再確認）。

**モグラ叩き注意**：同一箇所（`PlacementToolBarTabItemTemplate`/`SplineBorder`）への往復が3周目に
入る。上記1は「反転したつもりが実は変わっていなかった」という見落とし、2は新規の副作用。次の
修正でも解消しない、または新たな副作用が出るようなら、自作`SplineBorder`+`ControlTemplate`全置換
というアプローチ自体の妥当性を俯瞰評価すること（`karo.md`「モグラ叩き検知」参照）。

**忍者実機確認結果（2026-07-22、往復3周目分）**：(1)上下反転＝OK（曲線が上辺で水平・下辺側に丸み、
拡大画像で確認）。(2)文字消失解消＝OK（両モード・両状態とも文字は正常表示）。(3)強調色＝
**条件付きNG（往復4周目が必要、次回セッションへ持ち越し）**——シート追加直後等、配置ツールタブが
起動時からの初期選択状態のまま（タブ操作未経由）だと、選択中タブの強調表示（背景色・枠線）が
全く反映されず非選択と見分けがつかない。基本機能→配置ツールへ明示的にクリックし直すと正しく
反映される。ダークモードで対照実験により確定、ライトモードでも同様の不反映を確認（クリック後は
色差が小さく画素採取だけでは確度中程度）。**忍者所見（推測、断定せず）**：往復3周目で対処した
文字消失と同型（Template切替直後に既にIsSelected=Trueな場合のタイミング問題）が、背景色/枠線の
Triggerには残っている疑い。詳細・再現手順は`docs/ecad2-t119-verify-ninja.md`参照。次回セッション
は、文字消失対処時（TargetName Setter全廃→TemplateBinding化）と同じ設計思想を、強調色の
Trigger（`Setter TargetName="SplineBd"`/`"Bd"`使用箇所）にも適用できないか検討することから
着手する。

### T-121 配置ツールバーペインのタイトルバー（青い帯）常時非表示・省スペース化 — Approved（gated、殿直接指示2026-07-22、設計フェーズから開始）

**起票=殿直接指示2026-07-22**（T-119実機確認中の派生発見）。配置ツールバーペイン
（`MainToolBar`/`PlacementToolBar`の2タブ同居）のタイトルバー（`AnchorablePaneTitle`、青い帯）が、
「基本機能」タブ選択時は表示され「配置ツール」タブ選択時は非表示になる、という非対称な挙動を
家老が発見（原因＝`UnifiedAnchorablePaneTitleStyle`719-725行目の`Model.ContentId="PlacementToolBar"`
限定の既存条件、T-099(c)由来。当時はタブ切替機能がなくPlacementToolBar単独ペインだったため
問題化しなかったが、T-104のタブ切替導入時の見落としで非対称化したと推定）。

**殿裁定＝両タブとも常時非表示（省スペース化が目的）、フロート時はタイトルバー表示、ドッキング
操作はフロート化後のタイトルバー経由で行う想定**。

**要検討事項（設計フェーズで詰めること）**：
1. `Header`テキスト部分のみ非表示では高さの縮小効果がほぼ無い（T-099(c)見積もり＝ボタン列
   `MenuDropDownButton`等が支配要素のため、約21px前後のまま）。真の省スペース化には**タイトル
   バーの帯全体**（`AnchorablePaneTitle`ごと、T-110増分3の`TitleBarHiddenAnchorableControlStyle`と
   同型の「Header Border層」Collapse方式）を対象にする必要がある
2. 帯全体を消すと`MenuDropDownButton`（Float/Dock/DockAsDocument/AutoHide/Hide等の標準ドッキング
   操作メニュー）も同居しているため一緒に消える。T-099(c)当時、このメニューは「タブドラッグでの
   フロート化に何らかの問題（AttachDragガード）があり、それを回避する確実な手段」として設けられた
   経緯があった（`docs/ecad2-t099-c-paneltitle-label-only-hide-design-onmitsu.md`参照）。**通常の
   タブドラッグによるフロート化が実際に機能するか**（AttachDragガードの影響有無）を検証してから
   帯全体除去に踏み切ること。機能しない場合は代替のドッキング操作手段（T-110増分3のAutoHide代替UI
   と同様、表示メニューへのサブメニュー追加等）を設計する
3. `MainToolBar`は`CanFloat="False"`（フロート不可）・`PlacementToolBar`のみフロート可能、という
   既存の非対称構成を踏まえ、両タブで同じ扱いにしてよいか、それとも役割に応じた差を設けるべきか
4. フロート時に帯を表示する条件（既存の`IsDirectlyHostedInFloatingWindow`ベースの判定が単純に
   使えるか、T-110増分3のトリガー1〜5相当の精査が要るか）

**進め方**：T-099(c)・T-110増分3と同じ手順（隠密が設計書を作成→殿裁定→侍実装→隠密レビュー→
忍者実機確認）を踏む。まず隠密へ設計調査・設計書作成を委譲する。

### T-120 配置ツールタブ末尾にテストモードボタンを増設 — Approved（auto-OK、殿直接指示2026-07-22）

**起票=殿直接指示2026-07-22**。テストモード切替ボタン（`TestModeToolBarButtonStyle`、
`IsChecked="{Binding IsTestMode}"`、`MainWindow.xaml`983-991行）は現在「基本機能」タブ
（`ContentId="MainToolBar"`）にのみ存在し、「配置ツール」タブ（`ContentId="PlacementToolBar"`）
を選択している間は見えないため、配置操作中にテストモードかどうかの判断が難しいとの殿指摘。

DoD：
1. 「配置ツール」タブのToolBar末尾（「自作パーツ (F11)」ボタンの後、`MainWindow.xaml`1160-1168
   行付近）に`Separator`を挟んで、既存のテストモード`ToggleButton`（983-991行）と**完全に同一の
   見た目・バインディング**を複製追加
2. 両タブのボタンが同じ`IsTestMode`にバインドされ、一方で切替えれば他方の表示も連動して更新
   されることを確認

規模小。既存要素の複製のみ、新規ロジックなし。

検証：通常の検証パイプライン（侍実装→隠密静的レビュー→忍者実機確認）。忍者実機確認は「配置
ツールタブでの切替が基本機能タブ側にも反映されること」を具体観点に含める。

### T-110 4分割DockingManagerの単一統合 — Approved（gated、殿直接指示2026-07-21）

**起票=殿直接指摘2026-07-21**（スクリーンショット添付）「シート」「出力」タブのみアクティブ色
（青）、「機器表」「プロパティ」は非アクティブ色（灰）という非対称性の質問を発端に、隠密調査
（`docs/ecad2-panel-title-color-investigation-onmitsu.md`）でバグではなく複数`DockingManager`
（4つ）構成の構造的帰結と判明。対処案3択（現状維持／`ActiveContent`同期処理追加／単一
`DockingManager`統合）を提示したところ、**殿裁定＝単一統合（根本対処）を選択**。「将来的な
リスクも考慮に入れると単一統合がいい、大分作業が後退するがプランを作成してほしい」とのご指示。

**現状（4つのDockingManager）**：`PlacementToolBarDockingManager`（配置ツールバー2段目）・
`LeftPaletteDockingManager`（シート等）・`RightPanelDockingManager`（機器表・プロパティ）・
`OutputPanelDockingManager`（出力パネル）。T-058の増分実装（左パレット→出力パネル→右パネル→
ツールバー2段目の順）が積み重なった自然な結果で、単一統合案が検討され却下された記録は無い
（隠密調査済み）。

**高リスク領域注記**：`AnchorablePaneControlStyle`系はT-099(c)で3周のモグラ叩きを経験した
最重要警戒領域。加えてT-100（ハッチング模様除去）・T-103（独自ドロップ枠方式）・T-104
（タブストリップ切替）・T-106（ダークモード対応）等、4分割構成を前提に積み重ねてきた対応が
複数存在し、統合により影響範囲が広い見込み（殿の「大分作業が後退する」というご認識どおり）。
**プラン→PoC（リスク検証）→増分実装＋各増分で忍者検証、の順で慎重に進める**
（`memory: 高リスク領域は検証優先`原則）。

**依頼内容（隠密へプラン作成を委任）**：
1. 現状4つのDockingManagerが管理するペイン一覧・レイアウト構造・テーマ適用処理の詳細調査
2. 単一DockingManagerへの統合方式の検討（レイアウト定義の再設計、初期配置指定方法）
3. 影響範囲の洗い出し——レイアウトリセット機構（Ctrl+Alt+R）・保存済みレイアウトファイルとの
   互換性・T-058/T-099(c)/T-100/T-103/T-104/T-106等これまでの対応がどう影響を受けるか
4. 高リスク領域（`AnchorablePaneControlStyle`系）への影響評価
5. PoC先行の増分計画提案（規模が大きいため段階分けを推奨）
6. **追加依頼（2026-07-21、殿直接指示）**：単一統合後、各ペイン（シート・機器表・プロパティ・
   出力等）の**単一ペインタイトルバー自体を非表示にできないか検討**（T-100でハッチング模様を
   除去した`AnchorablePaneTitleStyle`の帯、「シート ×」「機器表 ×」等の表示部分）。タイトル
   バーにはペイン名表示に加え、ピン留め（AutoHide）・閉じるボタン等の機能が乗っているため、
   非表示化した場合にこれらの操作をどう提供するか（別UIでの代替、機能自体の要否も含む）を
   併せて検討すること。単一統合で複数ペインが同一グループに入った場合のタブ切替UIとは別論点
   （殿確認済み、今回の対象は単一ペイン時のタイトルバーのみ）。

**隠密プラン完了（2026-07-21、`docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`）**：
依頼内容1〜6全対応。統合方式=案1（キャンバス=LayoutDocumentPane）推奨、難所3件（スタイルの
Manager単位スコープ分離不可／T-099(c)案Y部分リセット不可能／T-103フロート検知の全ペイン化）、
増分計画=PoC→骨格統合→回帰総点検→タイトルバー非表示の4段。殿裁定事項=裁1〜裁6を提示。

**家老実装プラン起草・隠密再検証完了（2026-07-21）**：家老が実装プラン（`docs/ecad2-t110-implementation-plan-karo.md`、
着手ゲート・増分采配整理・侍采配文面）を起草し、隠密が再検証（`docs/ecad2-t110-karo-plan-review-onmitsu.md`、
総合判定=概ね妥当、必須修正2件・推奨2件を反映済み）。

**殿裁可（2026-07-21、裁1〜裁6）**：裁1〜裁5=隠密推奨どおり採用（案1統合トポロジ／ドキュメントタブ
非表示化／旧レイアウトは既定へフォールバック／`CanFloat="False"`で封止／タイトルバー案A完全非表示）、
裁6=許容（アクティブ色表示喪失を許容、代替の視覚表現は設けない）。裁5付帯裁定「AutoHide機能の要否」
は増分0のPoC結果を踏まえ別途確認（増分1着手のブロッカーにはしない）。増分0（PoC、
`poc/t110-single-dockingmanager-poc/`）の実装を侍へ采配。

**増分0（PoC）完了（2026-07-22）**：侍実装（`96d3164`）→隠密静的レビュー（要修正2件を差し戻し
＝ダーク切替時のテーマ辞書差し替え欠落／`Items.Count==1`タブCollapseトリガー欠落）→侍修正
（`b584cc0`）→隠密確認OK→忍者実機確認（`docs/ecad2-t110-poc-verification-ninja.md`）の順で完了。

**実機確認結果**：(d)アクティブ色一元化（T-110発端の最重要項目）＝明確にOK、単一DockingManager
統合で4パネルとも常に1つだけアクティブになることを実証。(b)(c)(e)(g)(h)・AutoHide・全ペイン
副作用もOK。**(f)ドキュメントタブ非表示ONでキャンバス内容が生のオブジェクト名文字列に化ける
重大バグを検出**したが、隠密が一次ソースで真因確定（既定`DocumentPaneControlStyle`の
`ContentTemplate`等3件のSetter転記漏れ、PR-21の3例目＝機能Setter漏れで機能喪失に至る変種、
`docs-notes/pattern-recurrence-log.md`記帳済み）。**増分1で採用する`ShowHeader="False"`方式
なら本バグの型は構造的に発生し得ないと確認済みのため、PoC自体の修正は不要**。

**増分1への申し送り事項**：(1)裁2の実装は`ShowHeader="False"`属性方式（テンプレコピー約60行
不要）を採用 (2)全ペイン統合スタイルに`Items.Count==1`タブCollapseトリガーを追加（本実装の
既存スタイルにも無い設計ギャップとして検出） (3)`DockAsDocument`経路で生成される新規ペインの
`ShowHeader`封止要否を要検討 (4)`SelectedContentIndex="1"`が実機で反映されない事象・
DataGridColumnHeader単体クリック非アクティブ化（いずれも軽微） (5)配置ツールバーが単独
ドッキングペインになる場面の実在有無（ContentId分岐ラベル非表示の発火条件確認）。詳細は
`docs/ecad2-t110-poc-review-onmitsu.md`「実機確認後の追補」参照。

**環境上の発見（範囲外・重大）**：検証中、この環境でGPUハードウェアアクセラレーション経由の
WPF描画が機能しておらず、PrintWindow/CopyFromScreen双方でスクリーンショットが白紙になる事象に
遭遇（UIA探索は正常）。`DisableHWAcceleration=1`で回避可能と判明、殿裁可で検証中のみ一時適用・
検証後は復元済み。T-110固有ではない環境異常のため、`memory: ecad2_gpu_hw_render_blank_screenshot`
へ記録済み。

増分1（骨格統合の本実装）着手可能な状態。

**増分1（骨格統合の本実装）完了（2026-07-22）**：侍実装（`a78b802`）→隠密フル観点静的レビュー
（着手前チェック`docs/ecad2-t110-increment1-pretask-check-onmitsu.md`との1対1突き合わせ、指1
DockAsDocument封止を差し戻し`e1c8f73`）→忍者実機確認14項目（`docs/ecad2-t110-increment1-verification-ninja.md`）
の順で進行。(4)アクティブ色一元化（T-110発端・最重要）は本実装でも明確にOK。

**検出・解消した重大所見3件**：
1. **(2)起動時選択タブNG**：`SelectedContentIndex`が実機で反映されない既知事象（増分0由来）
   → 侍修正（`c83dc2a`、Loaded後ContentIdベースでIsActive明示設定）、忍者再検証OK
2. **(9)レイアウト保存が無反応**：バグ対応のWチェック（侍調査+隠密独立調査を並行）で追跡。
   初報「旧4ファイル更新」は忍者の日付誤読と訂正、真の異常はmain-layout.xml不生成。隠密の
   合成キー入力仮説を忍者の決定的実験（メニュー経由保存は成功／診断ログで合成Ctrl+Alt+Sが
   キーバインディング判定に到達すらしていないと確定）で裏付け、**実装バグでなく検証手法
   （Alt絡みショートカットの合成キー入力配送問題）の限界と確定、修正不要**。知見は
   `ecad2-ui-automation`スキルへ追記済み（機序は推測と明記、残余リスクも記録）
3. **十字型ドロップターゲット（AvalonDock標準OverlayWindow）の残留**：モグラ叩き型3周の末に
   解決。1周目=対象取り違え（ecad2自前枠のみCollapse化、`e45d8b8`）→隠密レビューでNG。2周目=
   隠密提案APIが`internal interface`でコンパイル不可と判明。ここで隠密が俯瞰評価に転換し
   「表示させてから消す」枠を出て**案D＝`CanDock="False"`でOverlayWindow自体を非生成化**
   （`5123eb3`）を一次ソース裏付きで提示。さらに隠密が既存保存済みレイアウトファイル起因の
   潜在的な罠（CanDock属性欠落で次回起動時に無音で上書き消滅）を検出、防御コード追加（指2、
   `aa06980`）。忍者の最終実機確認で全項目OK（既存ファイル状態での起動→ドラッグでも十字型
   不出現を実証）。

副次修正：起動時読込の退行（保存ファイル無し時の無意味なDeserialize、旧ガード復元、`c83dc2a`）。
軽微所見（配置ツールバーのラベル非表示がフロート時に不発火＝案E設計どおりの正常動作と訂正確認、
ドロップ枠判定範囲の全域拡大＝仮実装として許容）は増分2以降への申し送り。

増分2（回帰総点検）・増分3（タイトルバー非表示）は次回セッションで着手。

**殿裁定（2026-07-22）＝裁5付帯裁定「AutoHide機能を残すか」＝残す（代替UIを新設）**。タイトル
バー自体は案A（完全非表示）のまま、AutoHide（ピン留め）機能は維持しメニュー等へ代替UI（例：
「パネルを自動的に隠す」項目）を新設する方向で増分3を設計・実装する
（`docs/ecad2-t110-single-dockingmanager-unification-plan-onmitsu.md`§6.4付帯裁定に対応）。
増分2（回帰総点検）は忍者へ采配済み（2026-07-22）。

**増分3設計完了（2026-07-22、隠密、`docs/ecad2-t110-increment3-titlebar-hide-and-autohide-ui-design-onmitsu.md`）**：
(1)タイトルバー完全非表示＝PoC(h)の本実装化、対象4ペインContentId名指しのDataTrigger方式（フェイル
セーフ化）、T-099の罠は3層で回避確認（AutoHideフライアウトのピン復帰UIは構造的に温存されると一次
ソース確認済み）。(2)AutoHide代替UIは3案を提示、隠密推奨=案1。

**殿裁定（2026-07-22）＝増3裁1=案1（表示メニューにサブメニュー「パネルを自動的に隠す」+ペイン別
トグル4項目）採用、増3裁2=ショートカットキーは今回追加しない**。いずれも隠密推奨どおり。隠密の
提案（設計書§7）に従い、増分3の侍実装は増分2（回帰総点検）完了後に着手する。

**増分2（回帰総点検）一通り完了（2026-07-22、忍者）**：対象7件中T-100/T-101/T-104/T-106/T-107/T-108は
回帰なし確認。**T-103のみ独自ドロップ枠自体の直接確認が未完**（後述所見Cによりドラッグ操作を保留）。

**増分2検証中に検出した重大所見3件（調査中）**：
1. **所見A**：要素配置ツール選択中(`IsMainContentEnabled=False`連動)、メニューバー全体がLight/Dark
   問わず常にライト固定色になる。増分1範囲内(単一MainDockingManager化の影響)か既存見落とし
   (T-033増分2由来)かは未確定、隠密が範囲判定調査中。
2. **所見B**：ダークモードで単一ペインタイトル(アクティブ状態=青背景)の文字が完全に消える
   (ライトは正常、非アクティブは両テーマとも正常、100%再現)。侍の机上調査ではLight/Dark両テーマの
   `ToolWindowCaptionActiveText`(白#FFFFFF)が理論上同値でecad2側オーバーライドも無いことを確認
   したが原因特定に至らず、診断ログ注入+忍者実測へ切替中（バグ対応のWチェック、隠密も並行で
   独立診断中）。
3. **所見C**：配置ツールタブ(T-104タブストリップ)をドラッグするとフロート化せずタブ切替が発生、
   タブを戻すとツールバーボタン群が視覚的に消失(クリックで再描画・復旧、再現性2/2)。UIA合成
   ドラッグ特有の限界か実装回帰か忍者からは断定不可、隠密が調査中。

3件とも増分3・T-110完了の前提条件（範囲内欠陥として解消するか経過観察とするかの判断含む）。

**所見A・B・C 調査状況更新（2026-07-22）**：
- **所見A**：隠密調査完了（`docs/ecad2-t110-increment2-findings-ab-investigation-onmitsu.md`）＝増分1
  範囲外（既存事象）がほぼ確実。T-110の修正対象から外し`docs/proposed.md`P-119へ記録、殿裁定待ち。
- **所見B**：**「バグでなし」でクローズ（2026-07-22、忍者の追加実測で自己訂正）**。忍者が当初の
  検証で「基本機能」タブ（ライト時）と「配置ツール」タブ（ダーク時）という**別ContentId同士**を
  比較しており、それを同一パネルのテーマ差と誤認していたと判明。配置ツールタブのラベル非表示は
  T-099(c)案E・増分1裁2の既存意図的仕様（テーマ無関係）。侍の机上調査（両テーマの色定義は完全
  同値）・隠密の独立診断（静的機序は全経路で不発見）はいずれも正しかった——バグの実体が無かった
  ため機序も見つからなかった、という整合。侍の診断ログ計装（一時計装、未コミット）は除去を指示済み。
- **所見C**：隠密調査完了（`docs/ecad2-t110-increment2-finding-c-investigation-onmitsu.md`）＝**実装
  回帰の証拠なし**。論点1（タブドラッグでフロート化せず選択切替）はUIA合成ドラッグの限界+AvalonDock
  正規のタブセマンティクスで完全説明可能、論点2（描画消失）は増分0で確定済みの環境GPU HW描画不全の
  局所症状の疑いが濃厚（実装側に描画トリガー漏れの帰属先なし）。**殿裁可**を得て、忍者へ物理操作
  再検証+`DisableHWAcceleration=1`再現試験を依頼中（決着すれば所見Cは修正不要でクローズ見込み）。

**緊急事象（所見C論点1検証中に発生、2026-07-22、解決済み）**：忍者が物理ドラッグ再検証の準備中、
UIA合成ドラッグ操作直後にシート・機器表が全消失しUndo不可能になる重大事象が発生。隠密の調査で
**実装回帰ではなく環境競合と確定**——殿PC常駐のMouseAssistant（既知の環境競合要因）が合成ドラッグに
反応しUndo系入力を連発注入し、Undoスナップショット履歴を起動直後の「シート0枚」状態まで遡って
復元しただけ（設計想定内の挙動、`MainWindowViewModel.cs`3245行）。MouseAssistant終了後の再現試験
2/2で消失せず、確定。副産物として**Undo/Redoの設計上の罠**（0枚まで戻るとRedoがUI上復旧不能になる
袋小路）を検出、`docs/proposed.md` P-120へ記録（殿裁定待ち）。

**所見C論点1・2の実測决着（2026-07-22）**：
- **論点2（描画消失）＝環境GPU HW描画不全で確定**。`DisableHWAcceleration=1`環境下でタブ切替往復
  (基本機能→配置ツール→基本機能)を実施したところ描画消失は一度も再現せず。実装回帰ではない。
- **論点1（タブドラッグでフロート化不成立）＝UIA合成入力の限界が濃厚だが忍者の手段では100%決着せず**。
  MouseAssistant終了状態で物理相当のInvoke-Ecad2Dragを再試行するも再度不成立、隠密の静的机序
  (ワープ的な合成移動でMouseLeave/Enterイベント列が実操作と食い違う)と整合する結果。**真の決着には
  殿ご自身の物理マウス操作による検証が必要**（忍者提案）。

**殿ご自身の物理操作で最終決着（2026-07-22）**：(1)アプリ内でのドラッグは実際にはフロート化する
が、増分1完了時点で申し送り済みの**「ドロップ枠判定範囲の全域拡大（仮実装として許容）」**により、
アプリ内のどこで手を離してもドロップ枠に捕まり即座に再ドッキングされる——UIA合成ドラッグで
「フロート化不成立」に見えていたのはこの再ドッキングの速さゆえ。(2)アプリ外まで運べばフロート
状態を維持できることを確認、フロート化機構自体は正常。**新規の実装回帰ではなく、既知の仮実装課題
（増分1由来、増分2以降へ申し送り済み）の裏付けと確定**。同時に**T-103独自ドロップ枠自体の直接確認
も完了**（アプリ内全域に表示される挙動を実機で確認、範囲の精緻化は今回のスコープ外＝仮実装のまま
許容を継続）。

**増分2（回帰総点検）これにて完了**。所見A（範囲外・P-119）・所見B（バグでなし・決着）・所見C
（環境要因+既知仮実装の裏付け・決着）、いずれも実装回帰なしと確定。T-103含む対象7件、回帰なし。

**増分3（タイトルバー非表示＋AutoHide代替UI）実装完了（2026-07-22）**：侍実装（コミット
`0c33e76`＝タイトルバー非表示、`2989159`＝AutoHide代替UI案1）、build/test exit 0・件数維持。
隠密静的レビュー完了（設計書§6.1の7観点、指摘事項なし）。

**忍者実機確認中に重大バグ発見**：フライアウト（AutoHideサイドタブから開く一時表示）のピン
（PART_AutoHidePin）経由でドッキング状態へ復帰する操作の直後、ツールバー本体の高さが実質9px
まで潰れクリック不能になる実害を検出（表示メニュー経由の復帰は正常、ピン経由のみ異常）。

侍・隠密が独立かつ協調して長時間の一次ソース調査を実施。要点：
- 当初仮説（AvalonDock`CollectGarbage()`がAutoHide中の`PreviousContainer`参照を破壊）は、
  侍・隠密が独立に同じ結論（発火経路が見当たらない）へ到達し棄却。
- 殿ご自身の実機操作で重要な絞り込み＝ピン経由でも**物理操作では異常なし**、フライアウトの
  タブクリックだけでは復帰しない（仕様通り）と確認。
- 忍者の「物理クリック100%再現」は実はWin32 API合成マウスイベント（`SetCursorPos`+
  `mouse_event`）であり真の人手操作ではないと判明。これが殿の実操作との食い違いの核心。
- 侍が座標クリックでの実機能確認を行い、**UIA観測の限界ではなく実際にツールバーが機能不全に
  陥る本物のバグ**と確定（9px帯へクリックしても新規作成が実行されない）。
- 有力仮説（未確定、隠密の一次ソース機序解明）：`DockingManager.IsVirtualizingAnchorable`
  既定`true`によりタブ切替のたびに`LayoutAnchorableControl`が生成・破棄される（仮想化）。
  ピン経由の復帰はクリックされたボタン自身を含むビジュアルツリー（フライアウトのHwndHost）が
  処理の最中に切り離される自己言及的な構造を持ち、この仮想化コンテナの再評価タイミングと
  衝突するレースコンディションと推定。合成マウスイベント特有の押下・解放間隔の短さ/座標ワープ
  （所見C論点1と同根の性質）が実際に踏み抜かせている可能性が高い。
- `IsVirtualizingAnchorable="False"`での回避実験は**不採用確定**——無効化自体が新たな実害
  （通常のタブ切替だけで正常時のツールバーボタンが0件消失）を生むため。

副次発見：Ctrl+Alt+Rリセット後、シートパネルのタイトルバーに"AvalonDock.Layout.LayoutAnchorable"
という型名がそのまま表示される別の異常も確認・完全再現・機序特定済み（`UnifiedAnchorablePaneTitleStyle`
のContentTemplateが`Model.Root.Manager.AnchorableTitleTemplate`という間接参照バインディングで、
AutoHide復帰時のモデルツリー再構築中に一時null化しWPF標準の`ToString()`フォールバックが働く。
AvalonDock標準機構自体の設計上の脆さ、ecad2固有の実装ミスではない）。

**殿裁定（2026-07-22）**：
1. 型名表示異常＝**修正する**（侍案1＝`UnifiedAnchorablePaneTitleStyle`のHeader部分を間接参照
   ContentTemplateから`<TextBlock Text="{Binding Model.Title,...}"/>`という直接バインドへ置換、
   隠密確認済みで機能的にほぼ完全に等価・低リスク）。侍へ実装采配。
2. ツールバー機能不全（実害あり、合成マウスイベント特有のタイミングでのみ発生、殿の実操作
   では未発生）＝**既知リスクとして`docs/proposed.md`へ記録、実装対応は見送り**。P-121参照。

**型名表示修正完了（2026-07-22、コミット`636e04d`）**：`UnifiedAnchorablePaneTitleStyle`のHeader部分を
直接バインドへ置換。隠密静的レビュー完了（指摘事項なし）。忍者実機確認完了（4ペイン全てで型名
フォールバック再発なし確認）。build/test exit 0。

**忍者確認中の副次発見**：(1)殿指摘のシートパネル位置ずれ（P-122）を忍者もスクショで確認
（「シート▼」表示、通常位置と異なる）、記録のみで後日修正予定 (2)配置ツールバーペインのTabItem・
SheetNavList項目のUIA Name属性が型名フォールボックのまま残存（P-123・P-124、いずれも視覚的には
正しく表示され実害なし、アクセシビリティ名のみの軽微な既存事象）。

**T-110増分3（タイトルバー非表示＋AutoHide代替UI）これにて実装・検証パイプライン完了**。残存課題
（P-121ツールバー機能不全・P-122シートパネル位置ずれ・P-123/P-124 UIA Name型名残存）は殿裁定
どおりいずれも記録のみで対応見送り、後日必要に応じ再検討。

### T-109 ステータスバー「ツール」表示の日本語化 — Done（2026-07-21、検証パイプライン完了）

**起票=殿直接指示2026-07-21**「内部処理は英語で問題ないが表示は日本語にしたい」（T-077増分6の
ステータスバーツール値説明追記に関連して発覚）。

**現状（家老確認）**：`MainWindow.xaml`1698行、ステータスバーの「ツール: {0}」は`Tool.Mode`
（`ToolMode` enum、`ToolState.cs`12行）をそのまま`ToString()`表示。実際に出る値は8種類
（Select/PlaceElement/PlaceConnector/PlaceFrame/PlaceLine/PlaceDot/PlaceWireBreak/
PlaceImage）、いずれも英語のenum名がそのまま画面表示されている。

**方針**：内部実装（enum自体の名称・値）は変更せず、**表示専用の変換ロジック**（Converter等）
を追加し、画面表示のみ日本語化する。

**UI/UX分岐（着手前に殿確認要）**：各値の日本語訳語。家老が既存のツールバー・メニューの
ラベル表記を確認したところ、「グループ枠記入」（PlaceFrame）・「自由線(横線/縦線)記入」
（PlaceLine）・「接続点記入」（PlaceDot）・「配線分断記入」（PlaceWireBreak）・「画像挿入」
（PlaceImage、メニュー表記）という既存ラベルがあるが、末尾表現（記入/配置/挿入）が不統一。
Select・PlaceElement（a接点等の具体名は含まない汎用ツール名が必要）に対応する既存の短い
ラベルは見当たらない。**隠密へ訳語案の調査・提案を委任、最終選定は殿確認**。

**訳語案完了（2026-07-21、隠密、`docs/ecad2-t109-toolmode-translation-proposal-onmitsu.md`）**：
予備調査を裏取りしつつ`PlaceConnector`にも既存ラベル「縦分岐線記入」（Shift+F9、制御回路
限定）が実在することを追加発見。末尾表現の統一方針（案A=既存ラベル最大限維持／案B=統一感
重視）を論点として提示。

**殿裁定（2026-07-21）＝案B（統一感重視）採用**。確定訳語：
- Select → **選択**
- PlaceElement → **要素配置**
- PlaceConnector → **縦コネクタ記入**
- PlaceFrame → **グループ枠記入**
- PlaceLine → **自由線記入**
- PlaceDot → **接続点記入**
- PlaceWireBreak → **配線分断記入**
- PlaceImage → **画像配置**

侍へ実装を采配。

**侍実装完了（2026-07-21、コミット`2c4d1dc`、4ファイル/77insertions/12deletions）**：
`ToolModeToTextConverter`新設（内部`ToolMode` enum自体は変更せず表示専用）、StatusBarの
Bindingへ追加。`docs/usage/ecad2-usage-statusbar.md`の「英語表記のまま」記述も修正。回帰
テスト9件追加。隠密静的レビュー完了（指摘なし、8値訳語を1行ずつ突き合わせ完全一致確認）。

**忍者実機確認（2026-07-21、`docs/ecad2-t109-verify-ninja.md`）**：DoD(1)(3)OK。DoD(2)は
8値中6値を実機確認、**残り2値（PlaceDot＝接続点記入／PlaceWireBreak＝配線分断記入）は
構造的に実機で表示される機会が無いと判明**——両モードは即時配置処理のため`Tool.Mode`への
代入が発生せず、T-041以来の既存設計に由来（T-109の実装バグではない）。コード上（Converter
のswitch式）は隠密レビューで正しいと確認済みのため、実害なしと判断。

### T-108 ダークモード対応漏れの全点検 — Done（2026-07-21、優先度高全件完了）

**起票=殿直接指摘2026-07-21**（スクリーンショット添付）。「シート追加」ダイアログ
（`AddSheetDialog`）で、ダークモード時に「制御回路」「主回路」ラジオボタンのラベル文字が
黒のまま（背景はダークに追従済み）となっており視認困難。殿指摘：「この種類の設定抜けが
多すぎるので全点検してほしい。人力でのチェックが追い付かない」。

**家老確認**：`AddSheetDialog.xaml`はWindowレベルで`Foreground="{DynamicResource
DialogForegroundBrush}"`を設定しているが、`RadioButton`（`ControlCircuitRadio`・
`MainCircuitRadio`）に個別のForeground指定がない。プロジェクト内で`RadioButton`/`CheckBox`/
`Label`を使用しているのはこのファイルのみ（grep確認）。原因はWPF既定コントロールテンプレート
側のForeground継承阻害の可能性が高く、PR-20/PR-21（値は正しいが反映されない系）と同型の
疑いがある（隠密確認要）。

**進め方（家老裁定2026-07-21）**：
1. 今回の具体的箇所（`AddSheetDialog`のRadioButton）修正を侍へ先行采配
2. これまでT-083/T-089/T-100/T-104/T-106等で色設定漏れが繰り返し発見されてきた経緯を踏まえ、
   「人力チェックが追いつかない」というお言葉どおり機械的な全点検が必要と判断。App層の全
   XAMLファイル（`App.xaml`/`MainWindow.xaml`/`Themes/*.xaml`/`Views/*.xaml`計11ファイル）を
   対象に、隠密が静的解析で「暗黙的スタイルが存在しない型のコントロールで、Foreground/
   Background等の色プロパティが未設定またはStaticResource参照になっている箇所」を機械的に
   棚卸しする
3. 点検結果に基づき追加修正を侍へ采配、忍者が画素採取で実機確認
4. 同種の再発防止（チェックリスト化・自動検出手段の要否）も点検完了後に検討

**AddSheetDialog修正完了（2026-07-21、コミット`9c14b8b`、1ファイル/9insertions）**：一次ソース
（`dotnet/wpf` `Aero2.NormalColor.xaml`4513-4578行）確認により、既定`RadioButton`の**Style
本体**（`ControlTemplate.Triggers`ではない）がForegroundを`{DynamicResource
SystemColors.ControlTextBrushKey}`で明示Setterしていることが原因と確定。ecad2側に
`RadioButton`用の暗黙的スタイルが存在しないため既定Setterがそのまま生きていた。PR-20/21型
（ControlTemplate.Triggers優先順位問題）ではなく、個別Foreground指定（ローカル値が優先度で
Style Setterに勝つ）で解決する型と判明、RadioButton2箇所へ`DialogForegroundBrush`を個別
指定して解決。隠密静的レビュー完了（指摘なし、一次ソース裏取り済み）。**新規パターンPR-24
として確定**（`docs-notes/pattern-recurrence-log.md`、暗黙的スタイル不在によるAero2テーマ
スタイル本体Setterの継承阻害、`onmitsu.md`「値は正しいが反映されない」系調査の早期
エスカレーション節へ第4パターンとして制度化済み）。

**全体点検、重大発見2件（優先度高）（2026-07-21、隠密、`docs/ecad2-t108-darkmode-audit-onmitsu.md`）**：
1. `Views/PdfPreviewDialog.xaml`＝`Window`要素自体にBackground/Foreground指定が完全に欠落
   （他の全ダイアログ6件は指定済み）。PDF出力プレビュー画面全体がダークモードでも常にライト
   モード風のまま表示される。
2. `MainWindow.xaml`（`StatusBarArea`）＝`StatusBar`/`StatusBarItem`用の暗黙的スタイルが
   ecad2側に存在せず、Aero2既定Style本体のBackground（`#FFF1EDED`固定）・Foreground
   （SystemColors固定）がそのまま適用、RadioButtonと同一メカニズム（PR-24の2例目）。ステータス
   バー全体（7項目）が常時ライトモード風配色のまま。
優先度低3件（Slider・自作パーツリストのカテゴリラベルGray固定・SheetSettingsDialogの
エラーテキストRed固定）は実害小と推測され、家老裁定により見送り。

**優先度高2件、追加修正完了（2026-07-21、コミット`f0bb3f7`、2ファイル/20insertions/1deletion）**：
`PdfPreviewDialog.xaml`へ`DialogBackgroundBrush`/`DialogForegroundBrush`追加（`PageLabel`/
`ZoomLabel`は継承で解決）。`App.xaml`へ`StatusBar`/`StatusBarItem`の暗黙的スタイル新設、
`ToolBarBackgroundBrush`/`ToolBarForegroundBrush`へ差し替え（一次ソース確認済み、Template
新設は不要と判断）。`StatusBarItem`の`Background=Transparent`は意図的挙動として維持。隠密
静的レビュー完了（指摘なし、一次ソース裏取り済み）。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t108-addfix-verify-ninja.md`）**：DoD(1)(2)全OK。
PDFプレビュー（Window全体・ページ番号・ズームラベル）、ステータスバー全7項目とも、ライト/
ダーク両テーマで画素採取した結果、理論値と完全一致を確認。「案内/警告メッセージ」のみ
`Foreground="DarkRed"`固定（意図的な警告色設計、実害なし）。これにてT-108（AddSheetDialog+
優先度高2件）完了。

### T-103 配置ツールバー、独自ドロップ枠方式によるドッキング操作の確実化 — Done（2026-07-20、push済みa1f4646）

**起票=殿直接指示2026-07-19**「ドラッグでのドッキングが目視で困難。枠を作成してそこにドロップする方式は可能か」。
T-099(c)実機確認後、殿の物理マウス操作で配置ツールバーの十字型ドロップターゲットUI（AvalonDock標準
OverlayWindow機構）の位置が中央固定されずズレる不具合が判明（隠密調査
`docs/ecad2-t099-c-overlaywindow-position-drift-survey-onmitsu.md`）。忍者はUIA限界によりドラッグ
操作自体を再現できず（`EnumWindows`でウィンドウ検出不能、副次的にフロート窓Boundsも不自然な動きを
観測）、殿ご自身の実機操作でのみ症状を確認できる状態。侍の提案（骨子4点）を殿が採用、AvalonDock標準の
OverlayWindow/DropTargetに一切依存しない独自方式で解決を図る。

**方式（侍提案、2026-07-19）**：
1. フロート化中のみツールバー帯の空き領域(Star列)に破線枠+案内文言を表示
2. ドラッグ終了はフロート窓へのHwndSource.AddHookでWM_EXITSIZEMOVE検知（AvalonDock自身と同じ判定点、
   内部手術なし）
3. ドロップ判定はGetCursorPosと枠矩形の自前ヒットテスト＝位置固定バグのOverlayWindow/DropTargetに
   一切依存せぬ
4. 実行は実証済み`ResetPlacementToolBarLayoutToDefault()`を呼ぶだけ（案Yと同一機構）

**狙い**：十字オーバーレイのバグを丸ごと迂回する。AvalonDock標準のドラッグ&ドロップ機構自体には手を
入れず、モグラ叩き回避の案Y哲学と同轍。

**着手前確認事項【MUST】**：
- 一次ソース確認：タブ切り離しドラッグ（DragService制御）とフロート窓タイトルバードラッグでメッセージ
  経路が異なる可能性、実装前に確認を挟む（侍所見）
- AvalonDock正規ドロップ成立時との二重実行ガード要（軽微、侍所見）

**UI/UX分岐は着手時に殿確認【MUST】**（`karo.md`「UI/UX・使用感に関わる分岐は必ず殿へ」）：枠の意匠
（線種・色・太さ）・案内文言・表示タイミングは未確定。侍が叩き台を実装しプレビューを殿へ提示、殿が選ぶ
形で決定する（T-089と同型の進め方）。

**高リスク領域注記**：`AnchorablePaneControlStyle`系はT-099(c)で3周のモグラ叩きを経験した最重要警戒
領域（隠密所見）。PoC先行→実機確認の順で慎重に進める。

**完了（2026-07-20、コミットa1f4646、push済み）**：着手前確認事項（一次ソース調査、
`docs/ecad2-t103-drag-message-path-and-guard-survey-samurai.md`）完了——両ドラッグ経路は
最終的に同一メッセージ経路(WM_NCLBUTTONDOWN→WM_MOVING→WM_EXITSIZEMOVE)に合流、二重実行
ガードはHwndSource.AddHookのLIFO特性(後登録が先呼出・handled=trueで先登録側スキップ)を
利用して実装。忍者実機確認6項目全OK（枠表示・ヒットテスト成立・タブ自己複製バグ再発なし・
反復操作耐性等）。枠の意匠（線種・色・太さ・案内文言）は仮実装のまま殿実機確認で問題なしと
確定。

### T-104 配置ツールバー、タブストリップに「基本機能」「配置ツール」を切替表示 — Done（2026-07-20、増分1+2完了・push済み1894073）

**起票=殿直接指示2026-07-19**「配置ツールのタブストリップに『基本機能』『配置ツール』を作成し、選択で
切替表示できれば高さはそれほど大きくならないはず」。

**経緯**：殿発案「メインツールバー（新規/開く/保存等、フロート化しない1段目固定領域）を配置ツールバー
下段のタブストリップへ統合できれば高さに余裕ができるのでは」を受け隠密が実装可能性を調査
（`docs/ecad2-mainToolbar-to-tabstrip-integration-survey-onmitsu.md`）。技術的には
`PlacementToolBarPaneControlStyle`改修で実現しうるが、「メインツールバー10個超のボタンを常時同時表示
すると帯自体を大幅拡大せねばならず高さ節約効果が相殺されかねない」との懸念を提示していた。今回の
殿発案＝**同時表示でなく2タブ切替表示**とすることでこの懸念を解消する設計。

**内容**：配置ツールバーのタブストリップ（現在「配置ツール」1タブのみ、フロート時フル表示・ドッキング
時Height=5px）に「基本機能」タブを新設し2タブ構成とする。
- 「基本機能」タブ選択時：メインツールバー相当（新規・開く・保存等）を表示
- 「配置ツール」タブ選択時：既存の配置ツールボタン群（F5〜F10等）を表示
- 両者は同時表示せず切替のため、帯の高さは現状のいずれか一方の必要分で足りる見込み

**設計検討事項（隠密委任）**：
- AvalonDock標準のマルチタブペイン機構（複数`LayoutAnchorable`を1ペインに収める形）を使うか、独自
  タブコントロールを配置ツールバー内に自作するかの方式比較
- メインツールバーは現状ウィンドウ固定領域（AvalonDock管轄外）にあるため、AvalonDockペインへ
  組み込む場合のコマンドバインディング・キーボードショートカット（Undo/Redo等）への影響
  （「配置ツール」タブ選択中でも常時使えるべき機能の扱い）
- 高リスク領域（`AnchorablePaneControlStyle`系、T-099(c)で3周のモグラ叩き経験）への影響評価

**UI/UX分岐は着手時に殿確認【MUST】**：タブの意匠・切替時のアニメーション有無・既定選択タブ等は
設計叩き台を殿へ提示し決定する。

**設計プラン完了（2026-07-19、隠密、`docs/ecad2-t104-toolbar-tabswitch-design-onmitsu.md`）**：
技術方針＝AvalonDock標準マルチタブ機構（`LayoutAnchorablePane.SelectedContentIndex`）を推奨、独自
タブコントロール自作・`PlacementToolBarPaneControlStyle`改修とも不要（切替表示のため帯拡大の懸念は
発生せず）。案YのContentDockingガードはContentId限定のため新規タブには不適用、`CanFloat="False"`
設定で高リスク領域（タブ自己複製バグ等）への巻き込みを構造的に回避する設計。実装増分＝PoC（標準機構の
動作検証）→本実装→検証パイプライン（隠密静的レビュー→忍者実機確認6項目）の順。

**殿裁定（2026-07-19）＝要確認2点とも決着**：
(1) design-brief 3節#7「右パネルのタブ排他式・固定幅」（GuiEcad失敗の再発防止項目）との方向性の
違いは**承知のうえで進める**（高さ制約とのトレードオフとして許容）。
(2) TimerPause機能（配置ツール10機能中、メニュー・ショートカットとも代替経路が無い唯一の機能）は
**削除してよい**（対応案a/b/cはいずれも不採用、機能自体を廃止）。

**増分1PoC完了（2026-07-20）**：基本機能タブのダミー`LayoutAnchorable`
（`ContentId="MainToolBar"`）を追加、隠密静的レビューOK、忍者実機確認DoD4点**全OK**
（タブ切替・CanFloat・ドッキング復帰・Tabキーナビゲーション、コミット82c1bae）。
Tabキーナビゲーションは往復2周（LayoutAnchorSideControl側Focusable無効化→部分改善→
ItemsControl側VisualTreeHelperピンポイント検索で完全解決）、隠密所見「モグラ叩きではなく
多層防御の計画的深掘り」。副産物の新規バグ（配置操作中のシート/出力パネル文字色グレー化、
`IsEnabled`と`IsSelected`トリガーの継承競合）も往復で解決済み（コミットcc0402b）。

**増分2完了（2026-07-20、コミット1894073、push済み）**：(1)メインツールバー10ボタンを
「基本機能」タブへ移設、2段ラッパーGrid撤去し1段構造化 (2)TimerPause機能削除（隠密の
削除範囲調査書と照合、5箇所全て削除・横展開漏れなし確認済み）(3)レイアウト読込失敗
メッセージを「レイアウトを既定の状態に更新しました」へ変更（殿裁定=案1、原因を問わず
前向きに伝わる汎用文言）(4)初期選択タブ=「配置ツール」（殿裁定=案A）。隠密静的レビュー
OK・忍者実機確認8観点全OK（`docs/ecad2-t104-increment2-verification-ninja.md`）。

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

### T-106 配置ツールバーのタブ(基本機能/配置ツール)文字色・非選択背景のダークモード非対応 — Done（2026-07-21）

**起票=殿直接指示2026-07-21**（T-101実機確認中「stay」状態でのご指摘）。忍者が画素採取で実測
裏取り（`docs/proposed.md` P-112）：基本機能(非選択)は背景`#EEEEEE`(ライトのまま)・文字`#000000`、
配置ツール(選択中)は背景`#2D2D30`(ダーク対応済み)・文字`#000000`(非対応)。

**原因（忍者所見）**：`MainWindow.xaml`の`PlacementToolBarPaneControlStyle`ItemContainerStyle
（TabItem用ControlTemplate、236-302行目）。(1)非選択時Backgroundが`TemplateBinding`止まりで
既定色固定 (2)Foregroundが選択/非選択問わず`DynamicResource`未指定で常時黒。T-099由来の既存欠落
と見受けるが未確定。

**内容**：上記2箇所を`DynamicResource`化し、選択中・非選択とも背景・文字色がライト/ダーク両
テーマで適切に切り替わるようにする。

**殿裁定（2026-07-21）＝タスク化してすぐ着手**。

**スコープ拡大（2026-07-21、殿直接指摘・`docs/proposed.md` P-113）**：F5等配置ツールで表示される
インラインバー（T-033、`ElementPlacementBar`）内の以下2点もダークテーマ非対応と判明、併せて
対応する。
- 種別選択ドロップダウン`PlacementPartComboBox`（MainWindow.xaml 1763-1778行）：スタイル指定
  なし・`App.xaml`側にComboBox専用の暗黙的スタイルも存在せずWPF既定の見た目のまま
- インラインバー左端の未定アイコンボタン2個（MainWindow.xaml 1742-1751行、機能未定の
  プレースホルダ、`Style="{x:Null}"`でWPF既定Buttonスタイルのまま）

**スコープ再拡大（2026-07-21、殿直接指摘・`docs/proposed.md` P-114）**：スクロールバー全般が
ダークテーマ時に判別しづらい。家老確認＝`App.xaml`にScrollBar専用の暗黙的スタイルが存在せず
WPF既定の見た目のまま。**殿裁定＝即対応**、併せてT-106に含める。

**侍実装完了・第2弾（2026-07-21、コミット`0c3a7d4`）**：ComboBox/ComboBoxItem/ScrollBar/Thumb/
RepeatButtonの暗黙的スタイルをApp.xamlへ新設（Aero2一次ソース踏襲+色のみDynamicResource化、
新規ブラシ3件をLight/Dark両テーマへ追加）。未定アイコンボタンは`Style="{x:Null}"`のまま背景色
属性のみ追加。侍によるライトモード起動確認（シート追加→a接点配置→ComboBoxドロップダウン展開）
はクラッシュなく正常動作、新規DynamicResourceキー全24件がLight/Dark両テーマに漏れなく存在する
ことも機械チェック済み（T-089型UnsetValueクラッシュのリスク排除）。ビルド0警告0エラー、
テスト842件失敗0。第1弾（`48008f5`）と合わせT-106全項目の実装完了、隠密静的レビューへ。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t106-review-onmitsu.md`）**：両コミットとも問題
なし。一次ソース（AvalonDock本体+VS2013テーマgeneric.xaml）確認＝ScrollBar/ComboBox型の明示的
定義なく競合皆無、新規ブラシ3件+参照既存キー3件のLight/Dark両テーマ存在も裏取り済み。実機確認
追加要望＝(a)ダークモードでのComboBoxドロップダウン展開 (b)ScrollBar実出現(シート数増やし
オーバーフロー時の視認性)の2点、忍者実機確認へ引き継ぎ。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t106-verify-ninja.md`）**：DoD(1)〜(7)全項目OK、
色実測はライト/ダーク両テーマとも理論値と完全一致（画素採取）。隠密要望2点も対応済み——
(a)ダークモードComboBoxドロップダウン展開はPopupウィンドウ個別撮影で正常表示確認 (b)ScrollBar
実出現はシート30枚追加でオーバーフローさせThumb色を確認、理論値と完全一致。T-101既存分の回帰
なし。検証用シート30枚は未削除（要否は殿判断）。Ecad2.Appは検証完了につき忍者にて終了済み。

**侍実装完了・第1弾（2026-07-21、コミット`48008f5`）**：タブItemContainerStyleのStyle本体へ
Background/Foreground追加、既存`PanelHeaderBackgroundBrush`/`PanelHeaderForegroundBrush`
（DataGridColumnHeader等と同義）を流用しDynamicResource化。新規ブラシキー追加なし。ビルド
0警告0エラー、テスト842件失敗0。スコープ拡大分（インラインバーのComboBox・未定アイコン2個）は
上記采配と入れ違いのため未着手、追加采配済み。

### T-101 配置ツール選択中ツールの恒久的ハイライト表示 — Done（2026-07-21）

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

**殿裁定（2026-07-21）**：視覚表現＝背景色＋枠線の併用。対象範囲＝配置ツールボタン群のみ
（`PlacementToolBarButtonStyle`適用群、F5〜F10・グループ枠記入・自作パーツ等）。侍へ着手采配済み。

**侍実装完了（2026-07-21、コミット`ba0ebc7`）**：`ActiveToolTag`算出プロパティ＋`MultiBinding`＋
新規`StringEqualsMultiConverter`でXAML側`DataTrigger`実装。ビルド0警告0エラー、テスト842件失敗0。
設計判断＝接続点記入・配線分断記入（F10系、押下即実行でTool.Modeを保持しない仕様）は恒久
ハイライトの対象外とした。**殿確認済み（2026-07-21）＝侍判断通りでよい**。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t101-review-onmitsu.md`）**：概ね妥当、2点指摘。
(1)選択中ボタン押下時にDataTriggerが常時勝ちPressedフィードバックが隠れる可能性→忍者実機確認へ
引き継ぎ。(2)自作パーツ（`Category!=""`）選択後、`ResolvePlacementToolTag()`がnullを返しF11ボタンの
ハイライトが消える（組込部品選択時にF5〜F8等へ移るのと非対称）→**殿確認済み（2026-07-21）＝
F11ハイライトを維持させたい**、侍へ修正采配済み。

**侍修正完了・隠密差分再確認完了（2026-07-21、コミット`52d71cd`）**：`entry is null`分岐のみを
`"PartSelection"`へ変更、既存挙動（組込部品選択時のF5〜F8等ハイライト遷移）は不変と確認。
ビルド0警告0エラー、テスト842件失敗0。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t101-verify-ninja.md`）**：DoD(1)(2)(3)(4)(6)は
OK、色実測（画素採取）は理論値とほぼ完全一致。DoD(5)は経路により結果が分かれる要判断事項
だった——F5等ツールバー直接クリックはOK、F11→部品選択リストから組込部品を選ぶ経路は
T-021裁定（配置後もToolをリセットしない連続配置仕様）によりTool自体が更新されずF11ハイライト
据え置き。**殿確認済み（2026-07-21）＝F5等直接クリックのみで達成とみなす**、F11経由は
T-021既存仕様の帰結でありT-101スコープ外として確定。

隠密所見1（選択中ボタン押下時にDataTrigger優先でPressedフィードバックが隠れる可能性）は
忍者検証で確証困難のため殿の実機操作でのご確認待ち（バグでなく設計上のトレードオフ、
「stay」指示によりEcad2.App起動維持中）。範囲外検出（シート追加ダイアログの既定名が
「o」という短い値）は`docs/proposed.md`へP-111として記録。検証用自作パーツファイル
（`自作\T101検証用自作部品.gcadpart`）は殿確認済み＝殿ご自身で削除予定。

### T-107 機器コメント表示・入力機能の新設 — Done（2026-07-21、本実装+増分1+増分2全完了）

**起票=殿直接指示2026-07-21**（T-033未定ボタンの正体判明を受けた新規機能要望、参照＝殿提示の
GX Works3実機能キャプチャ）。未定1「連続して回路を入力」・未定2「連続してコメントを入力」と
判明。GX3は1セルを上下2分割で管理し、上段=回路記号+回路名、下段=コメントを格納（殿確認）。

**現状（家老grep確認）**：`Element.Comment`（string?、`Element.cs:58`）はCore層に既存、機器表
（クロスリファレンス表、`DiagramRenderer.cs`の`DrawTableRow`/`e.Comments`）では既に集約表示に
使われているが、ラダー図本体上の描画（`DrawElementLabel`は機器名をシンボル**上**に描画のみ）・
App層の入力UIとも一切未結線（`grep "\.Comment\b" src/Ecad2.App` 0件）。「Core完備・App未結線」
パターンの新例（P-086/P-099/P-100/P-101/P-102に続く6例目、`docs/proposed.md` P-115）。

**殿裁定（2026-07-21）**：
- 表示位置＝機器名は記号の上（既存どおり）、コメントは記号の下。同一セル内で行は専有しない
  （GX3の上下2分割構造に対応）
- 表示色＝GX3同様の緑色
- 入力スコープ＝基本の入力手段のみ。プロパティパネル内、既存`DeviceNameBox`（選択中要素の
  機器名編集欄）と同型の位置にコメント入力欄（TextBox）を新設する。GX3の「連続してコメントを
  入力」モード相当は**本タスクのスコープ外**、必要になれば別タスクとして分離する

**DoD**：
1. ラダー図本体上、機器シンボル直下（同一セル内、上下2分割の下段）に`Element.Comment`の値が
   緑色で表示される
2. コメント未設定（null/空文字）の要素では何も表示されない
3. プロパティパネル内、`DeviceNameBox`と同型の位置に新設したコメント入力欄で、選択中要素の
   `Comment`を編集できる（P-086/T-097と同型のExplicit確定パターンに倣う）
4. 既存の機器表（クロスリファレンス表）のコメント列表示に回帰がないこと
5. PDF出力（`Ecad2.Pdf`層、`IRenderer`共通経由）にも新規コメント描画が反映されること
6. ライト/ダーク両テーマで視認性確認（色は緑固定、背景とのコントラストのみ確認）
7. ビルド・既存テスト回帰なし

**侍実装完了（2026-07-21、コミット`bd87250`）**：`DrawingTheme`に`TextRole.Comment`+意味色
Comment（緑固定、テーマ非依存）新設。`DrawElementLabel`でDeviceName(上)と対称にComment(下)を
独立判定で描画。`MainWindowViewModel`に`SelectedElementComment`新設（標準パターンに倣い
`UndoManager.RecordSnapshot`呼び出し、値未変化なら呼ばない）。`MainWindow.xaml`に`DeviceNameBox`
と同型位置の`CommentBox`新設、Explicit確定パターンを`CommitDeviceNameEdit`へ集約。ビルド0警告
0エラー、テスト+10件（Core125件・App727件）失敗0。DoD(1)(2)(3)(7)は実装・テスト済み、DoD(4)は
`DrawCrossRefTable`等無変更のため回帰なしと侍判断、DoD(5)(6)は実機確認要（緑色RGB値`#008000`は
「GX3同様」の範囲内での侍技術判断、実機で違和感あれば調整）。隠密静的レビューへ。

**隠密静的レビュー完了・重大指摘（2026-07-21、`docs/ecad2-t107-review-onmitsu.md`）**：観点(1)(2)は
問題なし。観点(3)Undo/Redo整合確認で**T-079(P-058)と同型の欠陥発見**——選択要素切替時に
プロパティパネル表示用プロパティ群（`SelectedElementDeviceName`等8-9個）へ`OnPropertyChanged`を
発火する箇所が4箇所（`SelectedCell`のsetter・`DeleteSelectedElement`・
`NotifySelectedElementChanged`・Document差し替え処理）あるが、新設`SelectedElementComment`だけが
いずれにも未追加。実害＝要素Aのコメント表示のまま要素Bへ選択切替→`CommentBox`表示が更新されず
→古い値が要素Bへ誤ってコミットされる。新規テスト6件は単一要素操作のみでこのシナリオ未検証。
**侍へ修正差し戻し済み**。pattern-recurrence-log.md PR-01と根本原因型は同じだが、samurai.md
既存チェックリスト（Selected*状態そのもの対象）はこの「派生表示プロパティの通知網羅」という型を
直接カバーしておらず、亜種か新規パターン候補として制度化を検討中。

**制度化完了（2026-07-21、家老）**：`docs-notes/pattern-recurrence-log.md`へPR-17候補として記帳、
`docs-notes/roles/samurai.md`「新規選択可能状態の横展開チェックリスト」へ項目9として追加。

**侍修正完了（2026-07-21、コミット`4aecb35`）**：4箇所全てへ`OnPropertyChanged
(nameof(SelectedElementComment))`追加。**DoD(10)RED先行証明**＝修正前コードで新規テスト5件を
実行し全件FAIL確認→4箇所修正後に再実行し全件PASS。DoD(8)(9)は切替後のgetterが正しく要素Bの値を
返すことも別テストで確認済み。ビルド0警告0エラー、テスト+5件（Core125件・App732件）失敗0。
隠密再レビューへ。

**隠密再レビュー完了（2026-07-21）**：差分・RED先行証明・回帰テスト5件とも適切と確認。他の
同型見落とし確認＝既存の全`SelectedElementXxx`/`IsSelectedElementXxx`派生プロパティ（計10個+
`SelectedElement`自体）が漏れなく4箇所全てに存在することを機械的に突き合わせ確認、他に見落とし
なし。忍者実機確認へ。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t107-verify-ninja.md`）**：DoD(1)〜(9)全項目OK。
重点確認のDoD(8)(9)（隠密指摘の選択切替時通知漏れ修正）は実機で実効性確認済み。緑色文字は
ライト/ダーク両テーマとも理論値`#008000`と画素採取で一致、PDF出力・機器表への反映も回帰なし。

**新たな仕様確認事項（2026-07-21、忍者実機確認中の追加検証、殿確認待ち）**：同一デバイス名
（例：M1）の複数要素間でコメントが共有されるか実測したところ、**共有されない**と判明。
`Element.Comment`は要素インスタンス個別のプロパティで、Device側にはComment相当の項目が無い
実装。機器表（クロスリファレンス表）では既存の`CrossReference.cs`が同一デバイス名の複数要素の
`Comment`を集約表示する仕組みが既にあるが、ラダー図本体上で「1要素に入力すると同名の他要素にも
自動反映される」という共有動作は無い。GX3実機能としてデバイス単位の共有が正しい仕様か、要素
インスタンス単位（現状実装）のままでよいかは次回セッション再開時に殿へ確認する。

**殿裁定（2026-07-21）＝デバイス単位で共有すべき（GX3準拠）**。家老確認＝`Device`クラス
（`src/Ecad2.Core/Model/Device.cs`、`Model`/`Maker`等を既に保持し`DeviceTable.ByName`で
デバイス名をキーに管理、機器表`DeviceTableViewModel`にも既に結線済み）に`Comment`を新設し
Model/Makerと同じ位置づけで持たせるのが自然な設計と判断。

**T-107増分2として侍へ追加采配**：
- `Device`クラスに`Comment`プロパティを新設
- `Element.Comment`（T-107本実装分）は廃止し、表示・編集時は`DeviceTable.ByName[DeviceName]
  .Comment`を参照する方式へ移行
- `DrawElementLabel`（Rendering層）はDeviceTable経由でComment値を取得するようシグネチャ調整
- `CrossReference.cs`のComment集約ロジックは、同一デバイス名なら値が1つに定まるため単純化可能
- 永続化（保存・読込）でDevice.Commentが正しく保存・復元されること
- 項目9で制度化した選択切替時の派生表示プロパティ通知（4箇所）は、参照先がDevice経由に変わる
  ため実装し直しが必要（同一の観点は引き続き有効）

**侍実装完了（2026-07-21、コミット`6553fab`）**：`Device.Comment`新設・`Element.Comment`廃止。
`DiagramRenderer`に`DeviceTable`受け取りフィールド追加、`DrawElementLabel`がDeviceName経由で
Comment解決。`CrossReferenceBuilder`のコメント集約を単純参照へ簡素化。`SelectedElementComment`は
`Document.Devices.ByName`経由に変更。呼び出し元3系統（画面/PDF出力/PDFプレビュー）全てに
devices引数を配線。変更13ファイル・228insertions/40deletions。ビルド0警告0エラー、テスト+8件
（Core131件・App734件）失敗0。DoD(9)は4箇所の通知経路自体は維持で対応可、侍所見では見落とし
なしと判断。**未検証（実機確認要）**：画面上の同期表示・PDF出力目視・ライト/ダーク視認性。
隠密静的レビューへ（次回セッションで継続）。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t107-zoubun2-review-onmitsu.md`）**：指摘なし、
DoD全項目満たすと判断。重点確認の呼び出し元3系統（画面/PDF出力/PDFプレビュー）devices引数、
配線漏れなし。DeviceName nullable箇所の安全性・永続化（自動シリアライズ、テスト裏取り済み）・
選択切替時通知4箇所・Element.Comment廃止の完全性も確認済み。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t107-zoubun2-verify-ninja.md`）**：DoD(1)〜(6)全項目
OK。同一デバイス名（M1）の2要素を配置し一方にコメント入力→他方の表示・CommentBox欄とも即座に
同期反映を実機確認、デバイス単位共有が正しく機能。永続化・両テーマ画素採取（`#008000`一致）・
T-107本実装分の回帰（選択切替時通知含む）もいずれも異常なし。範囲外検出なし。これにてT-107
全体（本実装+増分1+増分2）完了。

### T-100 ドッキング済みタブのハッチング模様除去 — Done（2026-07-21、新規発見6も含め全完了）

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
**新情報（2026-07-21、殿直接指摘・画像添付）**：配置ツールバーでツールを選んでからOK確定するまでの
間（インラインバー表示中）、シートパネル・部品選択パネルは**ずっと白いまま持続する**と確認
（従来の「配置確定直後の一瞬」という理解とは異なる観察）。持続現象であれば診断ログ注入を待たず
通常のスクリーンショット・画素採取でも再現・観察できる可能性が高い、次回着手時の重要な手がかり。
**優先順位は予定通り変更なし（殿確認2026-07-21）＝T-105の後**。

**実機再現、確定的に成功（2026-07-21、忍者、`docs/ecad2-t100-shinki6-verify2-ninja.md`）**：
前回（2026-07-18）のGetPixel/Save矛盾は今回発生せず解消。インラインバー表示中（セルクリック後・
OK確定前）、シートパネル・部品選択パネルが持続的に純白(`#FFFFFF`)化（期待値`#2D2D30`）。500ms
間隔3回連続撮影・複数要素種別（コイル含む）で再現一貫、Save画像にも確実に反映。OK確定後は正常
復帰。対照の機器表・出力パネルは終始正常。**両パネルが完全同期して白化することを新規確認**（原因
候補の絞り込みに資する手がかり）。隠密へ原因の再調査を委任済み（前回2026-07-17静的調査の「両
パネル同時発生を説明しきれない」壁の突破口として、この新情報を活用する方針）。

**根本原因確定（2026-07-21、隠密、`docs/ecad2-t100-shinki6-root-cause-onmitsu.md`）**：
`MainContentArea`（メニュー・ツールバー・左パレット・キャンバス・右パネル・出力パネルを含む
巨大なGrid）のIsEnabledが配置バー表示中`false`になる設計（`IsMainContentEnabled`）と、WPF
既定Aero2テーマの`ListBox`ControlTemplateが持つ「`IsEnabled=false`時にBackgroundを白固定色
（`StaticResource`）へ強制上書きするTrigger」の組み合わせが原因。`SheetNavList`・
`PartSelectionList`は共に`ListBox`型でこの影響を直接受け同時に純白化、機器表・出力パネルは
`DataGrid`型でAero2既定テンプレートに同種トリガーが存在せず無事——殿観察の全事実と矛盾なく
整合。前回（T-083）の仮説は「一瞬の現象」という誤った時間軸前提によるもので誤りと判明。
PR-20（値は正しいが反映されない、StaticResource固定解決）の4例目として
`docs-notes/pattern-recurrence-log.md`へ再発記帳済み（既存の3パターン制度化枠内に収まる
事例と家老確認、追加制度化は不要と判断）。派生所見（`IsRungCommentEditorVisible`・
`IsFrameLabelEditorVisible`も同一機構に連動、行コメント/枠ラベルエディタ表示中も同型現象の
可能性）は`docs/proposed.md` P-116として記録。

**殿裁定（2026-07-21）＝方式1（ControlTemplate差し替え、T-106確立パターン踏襲）を採用**。
`App.xaml`のListBox暗黙的スタイルへTemplateを明示指定し、Trigger内の白固定色を
`DynamicResource`化する。侍へ実装采配済み。

**侍実装完了（2026-07-21、コミット`9f26852`、App.xaml、50insertions）**：一次ソース
（Aero2.NormalColor.xaml 2524-2565行）のListBox ControlTemplateを移植、`IsEnabled=false`
トリガーのBackground/BorderBrushを白固定色から`PanelContentBackgroundBrush`/
`PanelGridLineBrush`（DynamicResource）へ差し替え。棚卸し＝MainWindow.xaml内ListBoxは
SheetNavList/PartSelectionListの2箇所のみ。新規キー追加なし（既存キーを再利用）。ビルド
0警告0エラー、テスト回帰なし。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t100-shinki6-fix-review-onmitsu.md`）**：
指摘なし。PR-21トラップ（Style本体既定値Setter漏れ）該当せず、一次ソースと1行ずつ突合し
欠落なしと確認。棚卸し（他6ダイアログにListBoxなし）も裏取り済み。新規追加の
BorderThickness/BorderBrushの見た目影響のみ念のため忍者へ申し送り。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t100-shinki6-fix-verify-ninja.md`）**：DoD(1)〜(3)
全OK。インラインバー表示中の白化は完全解消、ダーク/ライト両モードとも期待値（`#2D2D30`/
`#FFFFFF`）を3回連続撮影で一貫維持。OK確定後の動作にも回帰なし。隠密申し送りの枠線変化も
1px程度の細線のみで実害なしと確認。これにてT-100全体（本体のハッチング模様除去＋新規発見6）
完了。

### T-098 シート追加時のPageNumber採番方式見直し — Done（2026-07-21）

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

**侍実装完了（2026-07-21、コミット`0a12585`、2ファイル/116insertions/1deletion）**：純粋関数
`DetermineNextPageNumber`（既存最大PageNumber+1、0枚なら1）を新設し`AddCommand`から呼び出す
方式へ変更。RED先行証明済み。ビルド0警告0エラー、テスト+7件全GREEN。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t098-review-onmitsu.md`）**：指摘なし。RED
先行証明の妥当性（共有main一時注入禁止のため静的読解で検証、侍報告の数値と完全一致確認）・
境界値網羅・T-084整合、いずれも問題なし。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t098-verify-ninja.md`）**：DoD(1)〜(3)全OK。
PageNumberはUI非表示の内部値のため.gcad保存→JSON実測で検証、中間シート削除→新規追加で
PageNumber=4（既存最大3+1）と確認、旧実装なら重複していたバグの解消を裏取り。欠番警告
メッセージの出る/出ないも両ケースとも正しく動作。これにてT-098完了。

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

**設計調査・プラン完了（2026-07-21、隠密、`docs/ecad2-t077-plan-onmitsu.md`）**：現状調査＝
ヘルプメニューは既存（バージョン情報のみ）、新設は同型パターンで容易。**プロジェクト内に
非モーダルWindowの前例が皆無**（既存Window派生6件は全て`ShowDialog()`）、T-077がプロジェクト
初の非モーダル実装になる。依存パッケージはAvalonDock系のみでゼロ依存。`docs/spec`対象は
`ecad2-spec-*.md`11ファイル・計2020行、ユーザー向け平易版は概算700-900行程度と推測（要精度
向上）。増分計画4段階（1:PoC非モーダル基盤+固定1領域 → 2:ナビゲーションUI+全11領域(原文
のまま) → 3:平易版変換・差し替え → 4:任意の付加機能）を叩き台として提示。

**殿裁定（2026-07-21）**：
- コンテンツ表示技術＝**案B（FlowDocument自作変換）**。新規依存なし、CLAUDE.md「不要な外部
  依存を追加しない」原則に最も合致、ダークモード連動も既存`DynamicResource`機構で容易。
- ナビゲーションUI＝**案1（左目次+右コンテンツ）**。GX Works3的、殿確定事項「GX Works3の
  ヘルプウィンドウに近い形」に最も忠実。
- 「使い方」メニュー項目のショートカットキー＝**F1を割り当てる**。

作業分担（`docs/spec`→ユーザー向け平易版への変換を誰が担当するか）は増分3着手時に改めて
判断（家老裁量事項、隠密プランで選択肢(a)隠密担当/(b)侍担当/(c)分担の3案提示済み）。
まず増分1（PoC）を侍へ実装采配。

**増分1（PoC）完了（2026-07-21、コミット`6063714`、7ファイル/353insertions）**：`UsageWindow`
新設（非モーダル・`Owner`設定・`Activate()`多重起動防止）、ヘルプメニュー「使い方(_G)」＋F1
キーから起動。`MarkdownFlowDocumentConverter`新設（見出し/段落/箇条書き/番号付き/コード
ブロック/水平線/インライン強調・コード対応、表構文はPoC範囲外）。代表領域は
`ecad2-spec-statusbar.md`を選定、EmbeddedResourceとして同梱。ダークモードは既存
DynamicResource機構を`SetResourceReference`経由で適用。回帰テスト8件追加、ビルド0警告
0エラー、テスト全GREEN。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t077-zoubun1-review-onmitsu.md`）**：指摘
なし。design-brief原則1（F1〜F12は常時有効）との整合裏取り済み、非モーダルWindow設計・
リソース解放とも妥当。申し送り1点＝`statusbar.md`に表構文1箇所（7行）が実は含まれ変換対象外
のため実機での崩れ表示確認を推奨。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t077-zoubun1-verify-ninja.md`）**：DoD(1)〜(6)
全OK。画素採取はライト/ダーク両テーマとも理論値と完全一致（大見出し領域で再測定し確定）。
表構文崩れも生Markdown記法のプレーンテキスト化のみで文字化け等の実害なしと確認。これにて
T-077増分1完了。次は増分2（ナビゲーションUI・全11領域対応）。

**増分2完了（2026-07-21、コミット`66a730e`、4ファイル/154insertions/15deletions）**：
`UsageWindow`を左目次（ListBox、11領域）+GridSplitter+右コンテンツ（FlowDocumentScrollViewer）
へ拡張、目次選択で増分1確立の`MarkdownFlowDocumentConverter`をそのまま流用しコンテンツ切替。
全11領域（`ecad2-spec-*.md`）をEmbeddedResource同梱（原文のまま、平易版変換は増分3）。初期
選択＝先頭「シート/ドキュメント管理」、並び順は侍判断（GX Works3的論理順序）。回帰テスト
15件追加。隠密静的レビュー完了（指摘なし、T-100修正済みListBoxスタイルの適用範囲・
GridSplitterダークモード対応とも確認済み）。忍者実機確認完了（`docs/ecad2-t077-zoubun2-verify-ninja.md`）：
DoD(1)〜(6)全OK、画素採取もライト/ダーク両テーマで理論値と完全一致。これにてT-077増分2完了。
次は増分3（`docs/spec`→ユーザー向け平易版への変換）。

**増分3、作業分担決定・試作着手（2026-07-21、家老裁量）**：`docs/spec`→ユーザー向け平易版
への変換は隠密が担当（T-075での仕様書作成実績を活かすため）。まず1-2ファイルの試作から着手し
規模見積りの精度を上げてから全11件へ展開する方針。

**試作2件完了・重大な陳腐化を発見（2026-07-21、隠密）**：`ecad2-spec-placement.md`（206→56行）・
`ecad2-spec-statusbar.md`（151→27行、圧縮率平均約23%、当初推測より圧縮率高め）を試作。
保存先＝`docs/usage/ecad2-usage-{領域}.md`（新設）。**statusbar.mdが本日完了のT-101（配置ツール
恒久ハイライト表示）以前の「ハイライト表示なし」という陳腐化した記述を含んでいたため、
`docs/spec/ecad2-spec-statusbar.md`・`docs/usage/ecad2-usage-statusbar.md`とも修正済み**。

**陳腐化リスク見立て完了（2026-07-21、隠密、`docs/ecad2-t077-zoubun3-spec-staleness-survey-onmitsu.md`）**：
T-075完了（2026-07-11）後の完了タスクとの一次推測マッピング（未検証）により、**高リスク4領域＝
menu-toolbar（T-089/101/103/104）・device-table・placement（T-107機器コメント機能欠落疑い）・
sheet-document（T-098採番見直し）**、低リスク6領域＝wiring/undo-redo/canvas-display/
part-management/drc-output/pdf-testmode、と見立て。T-104/T-105/T-107由来は「誤り」でなく
仕様書作成時に存在しなかった新機能の**丸ごと欠落**の可能性がある点に注意（陳腐化と性質が異なる）。

**家老裁定（2026-07-21）＝折衷案採用**：高リスク4領域を先行点検、残り6領域は増分3の変換
作業時に確認する。隠密へ継続采配。

**高リスク4領域、3件完了・中間報告（2026-07-21、隠密）**：
- **device-table**：委任範囲外の追加陳腐化も発見・修正——「PDF出力は到達不能」という記述が
  T-060（2026-07-12実装）で既に解消済みだったのに放置されていた。T-107（Device.Comment新設）
  分と併せて修正、usage版新規作成完了。
- **placement**：ElementInstance構成要素の表にT-107で廃止済みのComment欄が陳腐化残存、削除
  修正。usage版は試作済みのため影響なし。
- **sheet-document**：T-098（PageNumber採番方式）・T-084（欠番警告）の記述が2節に丸ごと欠落
  していたため追記。usage版新規作成完了。
- **menu-toolbar**：委任範囲（T-089/101/103/104）に加え、PDF出力・テストモードが「未結線」の
  まま記載される等より広範な陳腐化が疑われ、Exploreエージェントで現状構成を包括調査中。

**高リスク4領域、全完了（2026-07-21、隠密）**：menu-toolbarは委任範囲に加え、**T-060（PDF
出力）・T-061（テストモード）というT-075完了（2026-07-11）直後のタスクによる陳腐化**が
1〜9節の過半に及んでいたと判明（PDF出力は結線済みなのに「未結線」、テストモードはCtrl+T
なのに「F5表記」、T-104のタブ構造化未反映等）、全面修正。device-table/placement/
sheet-documentも完了、4領域とも usage版作成済み。

**重要な教訓（隠密）＝当初の「高リスク」見立てはT-089以降のタスクのみ根拠にしていたが、
実際はT-075完了直後（2026-07-11〜12）のT-060/T-061由来の陳腐化が最も深刻だった**。残り
6領域にも同時期の別タスクによる同型の陳腐化が潜む可能性が当初見立てより高いとの指摘。

**家老裁定（2026-07-21）＝教訓を採用**。残り6領域（wiring/undo-redo/canvas-display/
part-management/drc-output/pdf-testmode）も、T-089以降だけでなくT-075完了直後
（2026-07-11〜12頃）の完了タスクとの突合を含めた陳腐化点検を行った上で平易版を作成する
方針へ切り替える。隠密へ継続采配。

**残り6領域、5件完了・中間報告（2026-07-21、隠密）**：
- **wiring**：T-091/T-092（ドラフト中ブロック）が丸ごと未反映だったため追記。usage版完成。
- **undo-redo**：Undo/RedoのCanExecute条件（`CanEditDiagram`・`!HasAnyDraft`）追加が4節未反映、
  修正。usage版完成。
- **canvas-display**：**新規発見**＝ダークモードで空状態⇔作業領域の背景色が同一（`#FF202224`）
  で視覚的区別が消えている（3節記述はライトモード限定の古い前提）。意図的設計か見落としかは
  不明、`docs/proposed.md` P-117として記録。仕様書側はダーク対応記述を追記、usage版完成。
- **part-management**：T-071で組込みパーツが5種→15種に増えていたが7節「固定5種」のまま放置、
  修正。usage版完成。
- **drc-output**：**新規発見**＝T-070（検索・置換機能）が仕様書に一切未記載。出力パネルが
  DRC結果/検索結果を相互排他表示する統合パネルという重要仕様が丸ごと欠落していた、新規節を
  追加。usage版完成。
- **pdf-testmode**：**最も陳腐化が深刻**——「両機能とも未着手」という前提のまま放置されて
  いたが、実際はT-060/T-061（2026-07-12/14）で完全実装済み。ほぼ全面書き直しが必要な規模、
  Exploreエージェントで現状構成を包括調査中。

**全11領域、完了（2026-07-21、隠密、`docs/ecad2-t077-zoubun3-11areas-complete-onmitsu.md`）**：
pdf-testmodeも完了——PDF出力・テストモードとも「未着手」前提のまま放置されていたが実際は
T-060/T-061で完全実装済み（プレビュー操作・入力方式・通電配色・実時間タイマ等）、全面書き
直し。全体教訓＝T-075完了直後だけでなくその後も陳腐化が継続的に蓄積していた（T-060/T-061は
T-075完了1〜3日後の実装で以後放置）。これにて高リスク4領域+残り6領域、全11領域の`docs/spec`
点検・修正・`docs/usage`平易版作成が完了。canvas-displayの気づき（ダークモード配色問題）は
`docs/proposed.md` P-117として記録済み。

**残作業（家老確認）**：`docs/usage`11件はドラフト状態。増分1/2で実装済みの`UsageWindow`は
現状`docs/spec`原文をEmbeddedResource参照しているため、参照先を`docs/usage`平易版へ切替える
実装（増分4）が必要。侍へ采配。

**増分4完了（2026-07-21、コミット`f961c06`、3ファイル/60insertions/55deletions）**：csproj
側EmbeddedResourceを`docs/spec`原文から`docs/usage`平易版（全11領域）へ切替、Topics定義の
ResourceFileNameも更新。DisplayName（目次表示名）を平易版の実際の見出しに合わせ3件更新
（undo-redo/drc-output/statusbar）。既存テスト15件を平易版に追従させ全件GREEN。隠密静的
レビュー完了（軽微な指摘2点、いずれも実害なし・修正不要と判断）。忍者実機確認完了
（`docs/ecad2-t077-zoubun4-verify-ninja.md`）：DoD(1)〜(5)全OK、全11領域とも平易版への
切替・書式崩れなしを確認。これにてT-077増分1〜4完了。

**表構文未対応の発覚・殿裁定（2026-07-21）**：`docs/usage`11領域中6領域（menu-toolbar・
pdf-testmode・placement・sheet-document・statusbar・wiring）に表構文が含まれるが、増分1の
PoC設計時点で表構文は`MarkdownFlowDocumentConverter`の対応範囲外（プレーンテキストのまま
残る仕様）としていたため、可読性への影響が懸念事項として浮上。**殿裁定＝増分5として表構文の
FlowDocument変換対応を追加する**。侍へ采配。

**増分5完了（2026-07-21、コミット`725dbd8`、3ファイル/198insertions/14deletions）**：
Markdown表構文（ヘッダー行/区切り線/データ行）をWPF `Table`へ変換するロジックを既存の行単位
ステートマシンへ追加、ヘッダーは太字+`PanelHeaderBackgroundBrush`、罫線は`PanelGridLineBrush`
（いずれも既存DynamicResource機構準拠、新規キー追加なし）。列数不足行は空セル埋め。実装中に
**「段落結合ループの1行目除外条件による無限ループ」という重大な潜在バグを発見・自己判断で
修正**（表構文対応の直接的副作用、範囲内の欠陥修正としてRED証明つきで対応）。回帰テスト12件
追加。隠密静的レビュー完了（effort=medium、指摘なし。無限ループ修正の論理的妥当性を重点確認
し妥当と判断、侍のRED証明も30秒タイムアウトで再実測し整合確認）。忍者実機確認完了
（`docs/ecad2-t077-zoubun5-verify-ninja.md`）：DoD(1)〜(4)全OK、殿指摘6領域の表が罫線付き
テーブルへ正しく変換、ライト/ダーク両テーマとも画素採取で理論値と完全一致、11領域とも
406〜436msで応答しフリーズなし（無限ループ再発なし実機裏取り済み）。**これにてT-077
（増分1〜5）完了**。

**【報告】殿より検証中に「stay」のご指示あり**：殿ご自身で実機確認とのことゆえ、Ecad2.Appは
終了せず起動したまま維持中（使い方ウィンドウはダークモード・PDF出力・テストモード選択中の
まま残置）。次の采配はアプリ終了後に改めて判断する。

**増分6完了（2026-07-21、隠密）**：`docs/usage/ecad2-usage-statusbar.md`に「ツール表示の
見方」節を新設、ステータスバーの「ツール」欄に出る8値（Select/PlaceElement/PlaceConnector/
PlaceFrame/PlaceLine/PlaceDot/PlaceWireBreak/PlaceImage）の意味を追記。既に専用ページが
ある値は「詳しくは○○参照」で誘導、無い値（PlaceFrame/PlaceImage）はその場で完結説明。
**副産物の陳腐化発見**＝`docs/spec/ecad2-spec-statusbar.md`のToolMode列挙が7値のみで
`PlaceImage`（T-064、2026-07-13追加）が欠落していた、修正済み。

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

### T-105 GroupFrame（グループ枠）の矢印キーでの平行移動対応 — Done（2026-07-21）

**経緯**：T-067実装時、GroupFrameの移動操作をドラッグに加えて矢印キーでも対応させるか
（他の選択可能要素との一貫性・キーボードファースト理念との整合 vs 殿裁定④「移動=ドラッグ」の
明示範囲）が未決着のまま、侍は矢印キー対応を除く範囲で先行実装した（コミット837b407以降、
T-067(1)〜(5)は完了）。着手する場合はUI/UX分岐（キー割当・他要素との一貫性）につき着手時に
殿確認要【MUST】。

**殿裁可（2026-07-21）＝着手してよい**。侍へ着手前調査（既存の矢印キー移動対応要素との実装
パターン比較、キー割当案の叩き台作成）を采配。UI/UX分岐（キー割当・他要素との一貫性）は
叩き台が出た時点で殿確認【MUST】。

**着手前調査完了（2026-07-21、侍、`docs/ecad2-t105-investigation-samurai.md`）**：GroupFrame
座標系はGrid座標(int)、選択状態`SelectedFrame`は`SelectedImage`と同型の独立選択。既存
`MoveSelectedXxx`群と比較し`MoveSelectedElement`型（int deltaRow/Column・全否定境界判定・
Undo対応）が最も整合、境界判定は既存`IsFrameWithinGridBounds`を再利用可能。呼び出し元は
`MainWindow.xaml.cs`の無修飾矢印キーif-elseチェーンへImage判定直後・Cellフォールバック直前
に挿入。キー割当案3案（無修飾矢印キー／Shift+矢印／Ctrl+矢印）を提示。

**殿裁定（2026-07-21）＝案A（無修飾矢印キー、侍推奨）確定**。既存Connector/WireBreak/
FreeLine/ConnectionDot/Imageと同一のキーで一貫性が最も高いため。侍へ実装采配済み。

**侍実装完了（2026-07-21、コミット`3ca9f5f`、3ファイル/214insertions）**：`MoveSelectedFrame`
新設（`IsFrameWithinGridBounds`再利用・全否定境界判定・`RecordSnapshot`によるUndo対応）、
`MainWindow.xaml.cs`へ配線（Image判定直後・Cellフォールバック直前）。回帰テスト11件追加、
全GREEN（App.Tests745件・Core.Tests131件）。境界値4方向・重複許容（GroupFrame仕様）・
Undo/Redo巻き戻り確認済み。新規機能のためRED証明対象外。Undo/RedoでSelectedFrame（独立
フィールド）が幽霊参照として残る懸念は、`ApplyUndoRedoSnapshot`内の`SelectedCell`再代入が
既存の排他制御setter経由で正しくnullクリアされることを実測確認、対応不要と判明。

**隠密静的レビュー完了（2026-07-21、`docs/ecad2-t105-review-onmitsu.md`）**：指摘なし、DoD
全項目満たすと判断。DoD整合・SetProperty早期returnトラップ（該当なし、性質が逆のため罠には
非該当）・幽霊参照懸念の裏取り（`SelectedCell`のsetterに無条件`SelectedFrame=null`があり
`ApplyUndoRedoSnapshot`経由で必ず発火、侍所見は正しいとコードで確認）いずれも問題なし。

**忍者実機確認完了（2026-07-21、`docs/ecad2-t105-verify-ninja.md`）**：DoD(1)〜(5)全項目OK。
境界クランプ4方向・Undo/Redo往復（幽霊参照なしも実機裏取り）・他要素との重複許容、いずれも
異常なし。検証中、Element（接点等）はCtrl+矢印キーで移動・無修飾矢印キーはSelectedCellカーソル
移動という既存仕様（GroupFrame/Imageは無修飾矢印キー、Elementのみ別扱い）に気づいたが、コード
確認で意図的仕様と判明・実装側の不具合ではなく範囲外と判断（家老確認済み、妥当）。これにて
T-105完了。

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
- [x] T-099 配置ツールバー2段目のドックタグ表示制御＋幅動的調整（要件(1)(2)(3)完全Done 2026-07-19コミット546b924、T-099(c)[上段AnchorablePaneTitle表示制御]完全Done 2026-07-19コミット61ecdfd、いずれもpush済み）
- [x] T-083 ダークモード搭載（AvalonDock連動、T-058と同時実装、全7増分+新規発見5件、完全Done 2026-07-17。新規発見2[ハッチング模様]はT-100へ分離）
- [x] T-067 GroupFrame（グループ枠）作成・編集UI（(1)〜(5)本体、完全Done 2026-07-19、コミット4e11c3a／546b924。矢印キー移動対応はT-105へ分離）
- [x] T-044 OR自動配線の冗長縦分岐抑止（新規バグ報告分＝「視覚的な隙間の解消」のみ完全Done 2026-07-19、コミット4e11c3a push済み。元来の本題[冗長縦分岐抑止]はBlockedのまま再開待ち、電気的合流先の設計課題はT-102へ分離・優先度低で後回し）
