# T-103 着手前確認事項【MUST】一次ソース調査（侍）

調査日: 2026-07-20　調査者: 侍　委任元: 家老（`docs/todo.md` T-103節「着手前確認事項」）
手法: 一次ソース直読（GitHub `Dirkster99/AvalonDock` タグ`v4.74.1`、
`source/Components/AvalonDock/Controls/LayoutFloatingWindowControl.cs`、`gh api`でraw取得しRead）。
静的読解のみ、実機検証は未実施。

---

## 確認事項(1): タブ切り離しドラッグとフロート窓タイトルバードラッグのメッセージ経路差異

### 結論

**WM_NCLBUTTONDOWN以降のメッセージ経路自体は同一。差異は「WM_NCLBUTTONDOWN送信に至るまでの前段（トリガー）」のみ。**

### 根拠

`AttachDrag(bool onActivated)`（L347-360）は2経路を持つ:

- `onActivated=true`（既定。`AnchorablePaneTitle`のタブ切り離しドラッグ、
  `StartDraggingFloatingWindowForPane`経由）: `_attachDrag=true; Activated += OnActivated`で
  Activatedイベントまで遅延。`InternalOnActivated`（L689-767）は
  `!_attachDrag || Mouse.LeftButton != Pressed`で即returnするガードを通過して初めて、
  L766で能動的に`WM_NCLBUTTONDOWN`(HTCAPTION)を送信する。
- `onActivated=false`（`LayoutContent.Float()`の`startDrag:false`経路）: 即座に
  `WM_NCLBUTTONDOWN`(HTCAPTION)を送信（L358）。

いずれの経路も最終的に同一の`WM_NCLBUTTONDOWN`(HTCAPTION)送信に帰着し、その後はOS標準の
ノンクライアント領域ドラッグ（`WM_MOVING`→`WM_EXITSIZEMOVE`）に入る。「フロート窓タイトルバー
ドラッグ」（既にフロート化済みのウィンドウをユーザーが直接タイトルバーで掴む操作）は、OS自身が
直接`WM_NCLBUTTONDOWN`を送出しAvalonDockコードを経由しない点が異なるのみで、以降のメッセージ
（`WM_MOVING`/`WM_EXITSIZEMOVE`、いずれも`FilterMessage`L362-411でハンドリング）は完全に共通。

**含意**: T-103方式のstep2「フロート窓へのHwndSource.AddHookでWM_EXITSIZEMOVE検知」は、
タブ切り離しドラッグ経由・フロート窓タイトルバードラッグ経由のいずれで生じたフロートウィンドウに
対しても等しく機能する見立て（メッセージ経路上の分岐は無い）。

**訂正（2026-07-24、T-121新規発見C、隠密再調査で判明）**: 上記の含意は「実際にマウスボタンが
押下されている状態」を暗黙の前提としており、この前提が成立しない経路では成り立たない。
`onActivated=false`経路（L358の`WM_NCLBUTTONDOWN`即時送信）はコード上は確かに発火するが、
これは**実マウスボタン押下と同時に送出される場合のみ**OSのノンクライアント領域ドラッグ
（`WM_MOVING`→`WM_EXITSIZEMOVE`）を開始させる。T-121でメニュー項目（`MenuItem.Click`）から
`LayoutContent.Float()`（`startDrag:false`→`onActivated=false`）を呼んだ実例では、呼び出し時点で
実マウスボタンは既に離されている（クリックイベント自体がボタン押下→解放の完了後に発火するため）ため、
合成`WM_NCLBUTTONDOWN`は送出されるがOS側のドラッグループへは実際には入らず、`WM_MOVING`/
`WM_EXITSIZEMOVE`のいずれも発生しないことが確認された（家老・隠密確定、侍の仮説と一致）。
よって「メッセージ経路上の分岐は無い」という結論は**実マウスドラッグ操作から生じるフロート化**
（タブ切り離し・フロート窓タイトルバードラッグ）に限っては成立するが、**プログラム的にFloat()を
呼ぶ経路（実マウス操作を伴わない）には適用できない**、と限定する必要がある。詳細・対処は
`docs/todo.md` T-121節「新規発見」参照。

---

## 確認事項(2): AvalonDock正規ドロップ成立時との二重実行ガード要否

### 結論

**要。確定（一次ソースで裏付け済み）。**

### 根拠

- `_dragService`（`DragService`インスタンス、L48）は`WM_MOVING`ハンドラ内の
  `UpdateDragPosition()`（L802-808）で初めて生成される。**フロートウィンドウが少しでも
  マウスで動かされれば（切り離しドラッグ経由でもタイトルバードラッグ経由でも）、
  この生成は無条件に起きる**。
- `WM_EXITSIZEMOVE`ハンドラ（L372-384）は`_dragService != null`なら常に
  `_dragService.Drop(mousePosition, out var dropFlag)`を呼ぶ。`Drop()`はOverlayWindow/
  DropTargetのヒットテスト判定（位置ズレバグの発生源そのもの）を行い、成立（`dropFlag=true`）
  なら`InternalClose()`でフロートウィンドウを閉じる。
- つまり**AvalonDock標準のOverlayWindow/DropTarget判定は、T-103の自前ヒットテスト（枠矩形＋
  GetCursorPos）と同一の`WM_EXITSIZEMOVE`タイミングで常に並行して走る**。ecad2側が独自枠への
  ドロップ成立を検知し`ResetPlacementToolBarLayoutToDefault()`を呼んでも、同時にAvalonDock標準
  側が（位置ズレの結果、偶然にも）Insideヒットと判定すれば標準Dock処理も走り得る——これは
  調査済みのタブ自己複製バグ（`docs/ecad2-t099-c-overlaywindow-droptarget-and-attachdrag-survey-
  onmitsu.md`調査5、`ContentDocking`→`InternalDock`フォールバック→重複`Children.Add`）の
  引き金と同型の経路であり、二重実行時のリスクは実証済みの重大バグと直結する。

### 未解明点・実装時の要検証事項

- `HwndSource.AddHook`で複数フックを登録した場合の**呼び出し順序**（ecad2側フックがAvalonDock
  自身の`FilterMessage`より先に呼ばれるか後か）は、WPF本体（`PresentationCore`）側の実装次第で
  あり、本調査（AvalonDockリポジトリのみ対象）の範囲外。確度低、実装時に検証必須。
  - 先に呼ばれる場合: ecad2が`ResetPlacementToolBarLayoutToDefault()`でレイアウトをDeserialize
    した直後にAvalonDock自身の`_dragService.Drop()`が走り、既に消えたフロートウィンドウに対する
    処理で例外リスクがある。
  - 後に呼ばれる場合: AvalonDock標準のDrop処理が先に完了してしまい、ecad2側の判定が無意味化
    または競合する可能性がある。
- ガード案（未検証、実装時に要設計）: ecad2側のWM_EXITSIZEMOVEハンドラで自前ヒットテストが
  成立した場合、`_dragService`はprivateフィールドのため直接null化するにはリフレクション等が
  必要。あるいはAvalonDock標準側のDrop成立自体を無害化する別経路（`DragService.Abort()`相当を
  呼べる手段があるか等）を追加調査する必要がある。

---

## 追記（2026-07-20、家老采配によるPoC着手前の追加調査）: 二重実行ガードの設計確定

### フック呼び出し順序——確定（.NET WPF一次ソース、`dotnet/wpf` mainブランチ
`PresentationCore/System/Windows/InterOp/HwndSource.cs`で裏付け）

`PublicHooksFilterMessage`（L1619-1669）:
```csharp
Delegate[] handlers = _hooks.Item2;
for (int i = handlers.Length - 1; i >= 0; --i)
{
    var hook = (HwndSourceHook)handlers[i];
    result = hook(hwnd, msg, wParam, lParam, ref handled);
    if (handled) break;
}
```
**フックは登録順とは逆順（LIFO＝後から`AddHook`したものが先）に呼ばれ、かつ`handled=true`を
返すとそれ以降（＝より先に登録されたフック）の呼び出しは丸ごとスキップされる。**

### 登録順序の裏付け（AvalonDock側、`LayoutFloatingWindowControl.cs`）

- AvalonDock自身の`Loaded += OnLoaded`はコンストラクタ内（L67）で登録される。
  `OnLoaded`（L580-593）が`HwndSource`取得と自身の`FilterMessage`の`AddHook`（L587-589）を行う。
- `DockingManager.LayoutFloatingWindowControlCreated`イベント（`DockingManager.cs` L1734/L1772）は
  フロートウィンドウ**生成直後・`Show()`前**に発火する（コンストラクタ実行後だが`Loaded`発火前）。
  ecad2側がこのイベントで`fwc.Loaded += 独自ハンドラ`を追加登録すれば、`Loaded`のマルチキャスト
  発火順（登録順＝FIFO）により**必ずAvalonDock自身の`OnLoaded`より後に実行される**——ecad2側の
  ハンドラが呼ばれる時点でAvalonDock自身のHwndSource取得・AddHookは完了済みであり、ecad2側は
  その後に同一HwndSourceへ`AddHook`する形になる。

### 結論・PoC設計方針

上記2点の組み合わせにより、**特別な細工なしに自然な実装順序で「ecad2側フックが先に呼ばれる」
関係が成立する**。よって二重実行ガードは以下の方式で構造的に実現できる：

1. `PlacementToolBarDockingManager.LayoutFloatingWindowControlCreated`を購読し、
   `e.Window.Loaded += ecadHandler`で独自Loadedハンドラを登録する。
2. `ecadHandler`内で`HwndSource.FromVisual(fwc)`を取得し、独自の`FilterMessage`相当を`AddHook`。
3. 独自フック内、`WM_EXITSIZEMOVE`受信時：
   - 自前ヒットテスト（枠矩形＋`GetCursorPos`）が成立 → `handled = true`で返す
     → **AvalonDock自身の`_dragService.Drop()`（OverlayWindow/DropTarget判定、位置ズレバグの
     発生源）は完全にスキップされる**。続けて`ResetPlacementToolBarLayoutToDefault()`を呼ぶ。
   - 不成立 → `handled = false`のまま返す → AvalonDock標準のFilterMessageがそのまま走り、
     従来のOverlayWindow/DropTarget判定（位置ズレバグ込み、枠外へドロップされた場合の
     フォールバック動作）に委ねる。

確度: 中〜高（.NET WPF・AvalonDockとも一次ソースの直読で裏付け済みだが、`dotnet/wpf`は
mainブランチ参照のためecad2実行時の実際の.NETランタイム実装との完全一致は未検証。フックの
基本ロジックが版間で変わる可能性は低いと見立てるが、**PoC実装後の実機確認で必ず実証すること**
（診断ログ推奨、`_dragService`が生成されないこと・AvalonDock標準Drop処理が走らないことを
ログで確認する）。

---

## 家老への申し送り

- 確認事項(1)(2)とも一次ソースで確定的に回答できた。(2)の二重実行ガードは
  `LayoutFloatingWindowControlCreated`→`Loaded`後`AddHook`→`handled=true`によるスキップ方式で
  設計確定（上記追記）。PoC実装へ進める。
- 意匠（枠の線種・色等）はUI/UX分岐のため、家老指示どおり本調査では扱っていない。

---

## 追記（2026-07-20）: 最小PoC実装完了・実機確認チェックリスト

### 実装内容（`MainWindow.xaml`/`MainWindow.xaml.cs`）

- XAML: 配置ツールバー帯のStar列（`Grid.Column="1"`）へ`PlacementToolBarDropZoneOverlay`
  （破線枠Rectangle＋案内文言TextBlock、既定Collapsed）を追加。意匠は仮実装（線色=
  `ToolBarForegroundBrush`、太さ2、破線4-2、角丸4、文言「ここへドラッグしてドッキング」）、
  後日殿プレビューで確定（UI/UX分岐、未着手）。
- コードビハインド: `PlacementToolBarDockingManager.LayoutFloatingWindowControlCreated`購読→
  対象が`PlacementToolBar`ならフロート化検知で枠を`Visible`化→`Loaded`後`HwndSource.AddHook`→
  `WM_EXITSIZEMOVE`受信時、枠のスクリーン矩形と`GetCursorPos`でヒットテスト→成立時
  `handled=true`＋`ResetPlacementToolBarLayoutToDefault()`呼出、不成立時はAvalonDock標準へ委ねる。
  `ContentDocking`ハンドラ（標準ドロップ経由の場合）でも枠を`Collapsed`に戻す。
- 一時診断ログ（`%TEMP%\ecad2-diag.log`、コミット対象外、実機確認完了後に除去予定）: フロート化
  検知・AddHook完了・WM_EXITSIZEMOVE受信・ヒットテスト結果（hit真偽・cursor座標・dropZone矩形）・
  handled設定タイミングを記録。

### 実機確認チェックリスト（忍者向け）

1. 配置ツールバーをフロート化すると、Star列に破線枠＋案内文言が表示されるか
2. 枠内でドラッグ終了（マウスアップ）した場合、ドッキング復帰し診断ログで
   `hit=True`→`handled=true設定`の順が記録されているか
3. 標準AvalonDockのOverlayWindow/DropTarget（十字型UI）が、枠内ドロップ時は動作しない
   （二重実行が起きない＝タブ自己複製バグが再現しない）ことを確認
4. 枠外でドラッグ終了した場合、診断ログで`hit=False`のみ記録され、従来のAvalonDock標準動作
   （位置ズレバグ込み）にフォールバックすること
5. ドッキング復帰後、枠が`Collapsed`に戻っていること（フロート化→枠外ドロップ→再フロート化の
   反復でも枠の表示状態が破綻しないこと）
6. 診断ログの`AddHook完了`が`LayoutFloatingWindowControlCreated`の直後に記録され、
   `HwndSource取得失敗`ログが出ていないこと（フックが確実に登録できているかの確認）

---

## 実機確認結果（2026-07-20、忍者）

6項目すべて**OK**。UIA `Invoke-Ecad2Drag`によるタイトルバードラッグで検証、診断ログ
（`%TEMP%\ecad2-diag.log`）と突き合わせ。

1. **OK**: 「配置ツール」タブをフロート化すると、Star列に破線枠＋案内文言「ここへドラッグして
   ドッキング」が表示された（スクリーンショット目視確認）。
2. **OK**: 枠内（dropZone座標範囲内）でドラッグ終了したところ、診断ログに
   `hit=True cursor=(2724,199) dropZone=2223,146,1000.96,107` → `handled=true設定、
   ResetPlacementToolBarLayoutToDefault呼出`の順で記録され、実際にドッキング復帰した
   （フロートウィンドウ消滅、メインウィンドウに2タブ復元をスクリーンショットで確認）。
3. **OK**: 上記2の枠内ドロップ後、`TabItem`数は常に4（基本機能・配置ツール・シート・出力）の
   まま、タブ自己複製バグ（重複）は一度も再現しなかった。
4. **OK**: 枠外（座標2500,600、dropZone範囲外）でドラッグ終了したところ、診断ログには
   `hit=False cursor=(2500,600) dropZone=...`のみ記録され、`handled=true設定`ログは
   出現しなかった（＝標準AvalonDock動作にフォールバック）。フロートウィンドウはドロップ先座標
   付近に残存（標準動作の位置ズレバグ込みの挙動と整合）。
5. **OK**: 「フロート化→枠外ドロップ→再ドラッグ→枠内ドロップ」の反復後も、枠は正しく
   Collapsedに戻り、タブ構成（基本機能・配置ツール2タブ）も破綻しなかった。
6. **OK**: `LayoutFloatingWindowControlCreated`（09:50:54.404）→`Loaded: AddHook完了`
   （09:50:54.417、13ms後）の順で記録され、検証セッション全体を通じ`HwndSource取得失敗`
   ログは一度も出現しなかった。

**総括**: T-103独自ドロップゾーン方式のPoC実装は6項目すべてクリーン。二重実行ガード
（`handled=true`によるAvalonDock標準`_dragService.Drop()`スキップ）も設計どおり機能しており、
T-099(c)で確認されたタブ自己複製バグの再発は見られなかった。詳細ログは
`%TEMP%\ecad2-diag.log`（16013行目以降が本検証セッション分）参照。
