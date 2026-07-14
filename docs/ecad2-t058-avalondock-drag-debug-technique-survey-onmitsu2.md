# T-058向け調査: AvalonDockのドラッグ操作デバッグ・自動テスト手法(隠密2)

調査日: 2026-07-14　調査者: 隠密2　依頼元: 家老（T-058、殿ご下問）

## 調査題目

ecad2側はUI Automation標準のSendInput合成では、AvalonDockのドラッグ系操作(タブ切り離し・境界リサイズ)を
検知できず、忍者が`mouse_event`多段階合成という代替手法で突破しているが確実性は高くない(閾値・タイミング
に敏感)。この手法の裏付け・改善余地・他の選択肢を、以下4観点でWeb一次情報を中心に調査する。

1. AvalonDock本家(Dirkster99/AvalonDock)自体のテストコードがドラッグ&ドロップ系機能をどうテストしているか
2. WPFのドラッグ&ドロップ操作を自動テストする一般的な手法、AvalonDock特有の相性問題の報告有無
3. AvalonDock利用者コミュニティでのデバッグ知見(ログ出力・イベントトレース等)
4. `mouse_event`多段階合成手法と同種のアプローチが他で報告されていないか

## 結論（要約）

- **AvalonDock本家自身も、実マウスドラッグ操作(MouseDown→Move→Up)を自動テストで検証していない。**
  `source/AutomationTest/AvalonDockTest/FlaUI/`にNUnit+FlaUIベースのUI自動テストがあり、CI(GitHub Actions)
  でも実行されているが、フロート化・ペイン操作はいずれも「メニューコマンド」「UIA Invoke/Toggle/Transform
  パターン」「内部APIの直接呼び出し」で代替しており、ドラッグシーケンスをシミュレートするコードは一件も
  確認できなかった。開発元自身が実ドラッグ自動化の正面突破を避けている、という強い状況証拠。
- **AvalonDockはUIA標準のDrag/DropTargetパターンを一切実装していない。** ソース検索の結果、
  `DoDragDrop`(OLE標準D&D)、`IDragProvider`等いずれも使用箇所0件。実体は`CaptureMouse`+`MouseMove`
  イベント追跡による独自`DragService`。そのためFlaUIの`DragPattern`(UIA標準)経由の操作は原理的に使用不可で、
  合成マウス入力(SendInput相当)一択という構造は裏付けが取れた。
- **忍者の「多段階合成」は特異な手法ではなく、他の自動化コミュニティでも独立に報告されている定石。**
  pywinauto・FlaUI関連文書・AutoHotkey等で「単純なダウン→アップでは失敗し、中間MOUSEMOVEを複数回挟む必要
  がある」という知見が繰り返し確認できた。具体的な改善余地として、**3〜5ステップの中間移動・各ステップ間
  50ms程度のウェイト**という数値ガイダンス（FlaUI系ドキュメント）が見つかっており、現行の閾値・タイミング
  調整の参考にできる。
- **AvalonDock独自のログ・トレース機構はほぼ無いに等しい。** `#if TRACE`ガード付きの`ConsoleDump()`
  (レイアウトツリー全体のスナップショット出力、ドラッグ専用ではない)のみで、呼び出し箇所もデフォルトで
  コメントアウトされ事実上休眠。ドラッグ専用の診断ログ・イベントトレース機構は存在しない。

## 事実（出典付き）

### 観点1: AvalonDock本家のテスト手法

- テストは`source/AutomationTest/AvalonDockTest/`配下。**UIを介さない純粋な単体テスト**
  (`OverlayPreviewRulesTests`、`DockZoneStackingTests`等、ドッキング先計算ロジックを内部API直接呼び出しで
  検証)と、**FlaUIベースのUI自動テスト**(`FlaUI/`サブディレクトリ、`FloatingWindowTests`・`DocumentTabTests`・
  `ToggleDockTests`・`StressTests`等)の二段構え。
  出典: https://github.com/Dirkster99/AvalonDock/tree/master/source/AutomationTest/AvalonDockTest
- FlaUI導入はPR #529(2026-04マージ)と比較的最近の取り組み。NUnit + FlaUI.Core/FlaUI.UIA3を使用。
  出典: https://github.com/Dirkster99/AvalonDock/pull/529
- `FloatingWindowTests.cs`は`ClickMenuItemByName("Tools", "New floating window")`というメニュー経由生成のみ、
  `StressTests.cs`のウィンドウリサイズはUIA Transformパターン(`Patterns.Transform.PatternOrDefault?.Resize()`)、
  `ToggleDockTests.cs`はUIA Invoke/Toggleパターン。**いずれもマウスdown+move+upの実ドラッグは使っていない。**
  出典: https://raw.githubusercontent.com/Dirkster99/AvalonDock/master/source/AutomationTest/AvalonDockTest/FlaUI/FloatingWindowTests.cs
  ほか(DocumentTabTests.cs / ToggleDockTests.cs / StressTests.cs / FlaUITestBase.cs、いずれも実測確認済み)
- CI(`​.github/workflows/ci.yml`)は`windows-latest`上で通常テスト(`Category!=FlaUI`)とUIテスト
  (`Category=FlaUI`)を分離実行。
  出典: https://raw.githubusercontent.com/Dirkster99/AvalonDock/master/.github/workflows/ci.yml

### 観点2: WPF D&D自動テストの一般手法とAvalonDockとの相性

- FlaUIの`Mouse.Drag()`系メソッドは`Down→(補間付きMoveTo)→Up`という合成マウス入力の実装であり、
  UIA標準の`DragPattern`(`IDragProvider`)とは別物。**`DragPattern`は対象アプリが明示的に実装していないと
  使えないopt-in機能。**
  出典: https://github.com/FlaUI/FlaUI/blob/main/src/FlaUI.Core/Input/Mouse.cs ,
  https://learn.microsoft.com/en-us/windows/win32/winauto/ui-automation-support-for-drag-and-drop
- **AvalonDock自身のソース調査（GitHub Code Search、実測）**: `DoDragDrop`使用箇所0件、`IDragProvider`等
  UIA Drag系パターンの実装も0件。`CaptureMouse`は`LayoutDocumentTabItem.cs`・`ToggleDockButtonBar.cs`・
  `LayoutFloatingWindowControl.cs`で使用され、独自`DragService`(`OnMouseLeftButtonDown`起点)で動作。
  出典: https://github.com/Dirkster99/AvalonDock (source/Components/AvalonDock/Controls/配下、コード検索実測) ,
  概要: https://github.com/Dirkster99/AvalonDock/wiki/DragService
- WinAppDriverの`DragAndDrop`/`ClickAndHold+MoveToElement+Release`は、複数のIssueで「クリック・ホールドは
  されるがドラッグの動きが発生しない」という**未解決の既知問題**として報告され続けている。
  出典: https://github.com/microsoft/WinAppDriver/issues/1223 ,
  https://github.com/microsoft/WinAppDriver/issues/1811 ,
  https://github.com/microsoft/WinAppDriver/issues/971
- FlaUI Issue #212でも、UWPリスト間ドラッグで「カーソルは動くが要素が移動しない」事例が報告されており、
  UIAがDrag/DropTarget対応を返してもアプリ固有のデータ転送処理が起動しないケースがあると分かる。
  出典: https://github.com/FlaUI/FlaUI/issues/212

### 観点3: AvalonDockコミュニティのデバッグ知見

- ログ機構は`LayoutElement.cs`等複数クラスに`#if TRACE`ガード付きの`ConsoleDump(int tab)`のみ
  (`Trace.WriteLine`でレイアウトツリーをダンプするだけ、ドラッグ専用ではない)。`TestApp`側の呼び出し行も
  `// Uncomment when TRACE is activated on AvalonDock project`とコメントアウトされ、配布バイナリでは
  事実上使えない。
  出典: https://github.com/Dirkster99/AvalonDock/blob/master/source/Components/AvalonDock/Layout/LayoutElement.cs ,
  https://github.com/Dirkster99/AvalonDock/blob/master/source/TestApp/MainWindow.xaml.cs
- Issue #292(Open、未実装)でレイアウト要素へのName/ID付与・ドラッグ&ドロップ時の中間コンテナへの説明的命名
  という提案があるが、実装状況は確認できず放置されている模様。
  出典: https://github.com/Dirkster99/AvalonDock/issues/292
- サードパーティ`gong-wpf-dragdrop`とのIssue #44で「AvalonDockのフローティングウィンドウ化した場合に限り
  `IDropTarget`メソッドが一切発火しない」という未解決報告があり、フローティング状態がドラッグ判定に影響を
  与えうることの傍証。
  出典: https://github.com/punker76/gong-wpf-dragdrop/issues/44
- 本家FlaUIテストの要素発見パターンとして、UIAツリー上の`Name=="AvalonDock.Layout.LayoutDocument"` /
  `Name=="AvalonDock.Layout.LayoutAnchorable"`という命名規則を使っている実装が確認できた(ecad2側の要素特定
  にも応用可能な具体知見)。
  出典: https://github.com/Dirkster99/AvalonDock/pull/529 (FlaUITestBase.cs)
- Stack OverflowはWebFetch/WebSearchのドメイン制約で直接アクセスできず、当該コミュニティでの議論の有無は
  **確認不能（不明）**。GitHub Discussions機能自体がこのリポジトリでは無効(GraphQL APIで確認)。

### 観点4: `mouse_event`多段階合成の類例・改善余地

- pywinauto Issue #542: 単純な`.drag_mouse()`(WM_MOUSEMOVEのみ)は失敗し、`.drag_mouse_input()`
  (SendInputベース、ボタンダウン→中間移動→ボタンアップ)に切替えたところ成功、という実例。
  出典: https://github.com/pywinauto/pywinauto/issues/542
- **FlaUIクロスプロセス入力に関する文書**: 「`SetCursorPos`は対象プロセスへWM_MOUSEMOVEを注入せず、WPFは
  WM_MOUSEMOVE受信時のみヒットテストを更新する」と明記。対策は`SendInput`で`MOUSEEVENTF_MOVE`を明示発行、
  **「3〜5ステップの中間移動で十分」「各ステップ間`Thread.Sleep(50)`」**という具体的数値ガイダンスあり
  (忍者の現行手法とパラメータ感覚が一致)。
  出典: https://skillsmp.com/skills/parksanghoon-sys-testcode-src-modbus-wpf-dev-pack-agents-skills-flaui-cross-process-input-skill-md
- The Old New Thing(Microsoft公式技術ブログ): `SendInput`の`MOUSEINPUT.time`は遅延配信機能ではなく打刻の
  書き換えに過ぎず、**時間差のあるドラッグを模擬するには`SendInput`を複数回・実ウェイトを挟んで呼ぶ必要が
  ある**ことが公式に説明されている。多段階合成という設計の技術的必然性を裏付ける一次情報。
  出典: https://devblogs.microsoft.com/oldnewthing/20121101-00/?p=6193
- Win32 `DragDetect`関数: `SM_CXDRAG`/`SM_CYDRAG`矩形をマウスが越えたかをWM_MOUSEMOVE受信のたびに判定する
  設計。ドラッグ検知がボタン押下中のWM_MOUSEMOVE連続受信に依存することの公式根拠(ただしAvalonDock/WPF
  `Thumb`が`DragDetect`自体を使うかは未確認)。
  出典: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-dragdetect
- pyautogui(`dragTo(duration=...)`のtween補間)、AutoHotkeyフォーラム(`SendInput`/`SendPlay`は瞬間移動になり
  一部アプリで認識されないため`SendEvent`+明示`MouseMove`+`Sleep`ループが推奨される議論)など、他の自動化
  ツールでも「中間移動を挟む」設計パターンが独立に採用されている。
  出典: https://pyautogui.readthedocs.io/en/latest/mouse.html ,
  https://www.autohotkey.com/boards/viewtopic.php?style=2&t=103991
- 対照例として、FlaUI標準の`Mouse.Drag`実装は開始点へ移動→ダウン→**終了点へ直接ジャンプ**→アップ、と
  中間点補間を行っていない(`MoveTo`にはあるが`Drag`には無い)。標準APIを鵜呑みにせず自前でSendInputラッパー
  を書く動機になり得る一次情報。
  出典: https://github.com/FlaUI/FlaUI/blob/main/src/FlaUI.Core/Input/Mouse.cs

## 不明点

- なぜAvalonDock本家がタブ切り離し・境界リサイズを実マウスドラッグでテストしないのか、理由を明言した
  Issue/コミットメッセージは見つからなかった(技術的制約か優先度の問題か不明)。
- 「中間MOUSEMOVEの回数・間隔がAvalonDockのタブ切り離し判定(フローティングウィンドウ生成のしきい値)に
  具体的にどう影響するか」を直接検証した一次情報は見つからなかった(観点4のskillsmp.com文書は一般的なWPF
  キャンバス操作向けで、AvalonDock固有の検証ではない)。
- UI Automation標準のSendInput合成がドラッグ閾値判定を原理的に検知できない、と明言した**公式**一次情報は
  見つからなかった。本調査での裏付けはコミュニティの実践知見・AvalonDockのアーキテクチャ調査からの論理的
  推論の域に留まる。
- Stack Overflowでの議論内容、Issue #292のコメント欄・実装予定は、ツール制約(ドメインアクセス不可/コメント
  取得不可)により確認不能。
- WPF `Thumb`やAvalonDockの内部ドラッグ判定ロジックが`DragDetect`関数を直接使うか独自実装かは、ソース
  コードの詳細処理まで追い切れておらず不明。

## 派生提案（範囲外の気づき）

- AvalonDock開発元自身が実ドラッグの自動テストを回避し、「メニュー操作・UIAパターン・内部API直接呼び出し」
  という**代替経路でのテスト設計**に振り切っている事実は、ecad2側の検証方針にも応用できる可能性がある
  (例: タブ切り離し確認は`DragService`相当の内部APIを直接叩くテストで代替し、実機忍者確認は「最終的な見た目
  確認」のみに絞る等)。これは実装方針の判断を伴うため、隠密2からは着手せず気づきとして報告に留める。
