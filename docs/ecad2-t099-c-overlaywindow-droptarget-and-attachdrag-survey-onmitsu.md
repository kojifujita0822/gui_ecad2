# T-099(c) 追加調査: ドッキングガイド表示制御 と AttachDragの中間的失敗モード（隠密）

調査日: 2026-07-19　調査者: 隠密　委任元: 家老（殿の追加質問2件、並行調査）
手法: 一次ソース確認（GitHub `Dirkster99/AvalonDock` master、package v4.74.1使用箇所と対応）。
静的読解のみ、実機検証は行っていない。

---

## 調査1: ドッキングガイド（OverlayWindow/DropTarget）の表示制御

### 結論

**設定1行では実現不可。ただし「中央（Inside=タブ統合）のみ有効化」は、OverlayWindowの
ControlTemplateをカスタムコピーし、Left/Top/Right/BottomのドロップターゲットUI要素を
`Visibility="Collapsed"`にすることで実現できる可能性が高い**（未実機検証、一次ソースからの
強い推測）。実装規模は中程度（T-100/`AnchorablePaneTitleNoDragHandleStyle`と同じ
「既定コピー+標的差し替え」手法、OverlayWindow全体のControlTemplateコピーを要する）。

### 根拠

1. `OverlayWindow.cs`の`IOverlayWindow.GetTargets()`（L324-）が実際に表示・判定対象となる
   `IDropTarget`一覧を生成する。`AnchorablePane`向け（L342-374、単一ペインへのドラッグ時に
   該当）は以下の非対称な実装：
   ```csharp
   yield return new AnchorablePaneDropTarget(..., _anchorablePaneDropTargetLeft.GetScreenArea(), DropTargetType.AnchorablePaneDockLeft);
   yield return new AnchorablePaneDropTarget(..., _anchorablePaneDropTargetTop.GetScreenArea(), DropTargetType.AnchorablePaneDockTop);
   yield return new AnchorablePaneDropTarget(..., _anchorablePaneDropTargetRight.GetScreenArea(), DropTargetType.AnchorablePaneDockRight);
   yield return new AnchorablePaneDropTarget(..., _anchorablePaneDropTargetBottom.GetScreenArea(), DropTargetType.AnchorablePaneDockBottom);
   if (_anchorablePaneDropTargetInto.IsVisible)
       yield return new AnchorablePaneDropTarget(..., _anchorablePaneDropTargetInto.GetScreenArea(), DropTargetType.AnchorablePaneDockInside);
   ```
   **Left/Top/Right/Bottomは`IsVisible`チェックなしで常にyield returnされる**（Into＝中央のみ
   チェックあり、L350）。ここだけ見ると「非表示化できない」ように見えるが、次の点で覆る。

2. `GetScreenArea()`（`TransformExtentions.cs:53`）は`element.TransformActualSizeToAncestor()`
   （実測サイズ）を元にRectを構築する。**要素が`Visibility="Collapsed"`ならMeasure/Arrangeが
   スキップされ`ActualWidth`/`ActualHeight`は0になり、`GetScreenArea()`はゼロサイズのRectを
   返す**。

3. `DragService.UpdateMouseLocation`（L148）は`_currentWindow.GetTargets().FirstOrDefault(dt =>
   dt.HitTestScreen(dragPosition))`でドロップターゲットを判定する。`HitTestScreen`は内部で
   `DetectionRect.Contains(dragPoint)`相当の判定（`DropTargetBase`基底、今回は未読だが一般的な
   実装パターンから推測、確度中）を行うため、**ゼロサイズのRectは如何なる座標も含まないため
   常にfalse＝そのDropTargetは実質選択不可能になる**。

4. よって、`_anchorablePaneDropTargetLeft/Top/Right/Bottom`に対応するXAML要素（`PART_
   AnchorablePaneDropTargetLeft`等、`OverlayWindow.GetTemplateChild`で取得）を含む
   OverlayWindowのControlTemplateをコピーし、これら4要素だけ`Visibility="Collapsed"`にすれば、
   **コード側（`GetTargets()`）を一切変更せずとも、中央（Inside）のみ有効なドロップターゲットに
   絞れる**可能性が高い。

### 対処法・実装規模の見立て

- 案: OverlayWindowの既定ControlTemplate（AvalonDock本体`themes/generic.xaml`または適用中の
  VS2013テーマのOverlayWindowスタイル）を丸ごとコピーし、`PART_AnchorablePaneDropTargetLeft/
  Top/Right/Bottom`の該当要素へ`Visibility="Collapsed"`を追加、DockingManagerへ
  `OverlayWindowStyle`的なプロパティ（要確認、`DockingManager`にOverlayWindow用スタイル
  プロパティがあるかは未調査）で適用する。
- 実装規模: **設定1行では済まない**。T-100（`AnchorablePaneTitleNoDragHandleStyle`、既定
  コピー+ハッチング模様のみ非表示化）と同種の作業ボリューム（ControlTemplate全体コピー＋
  対象要素の差し替え）。OverlayWindow自体のXAMLソースをGitHub一次ソースから取得しコピー元と
  するのが確実（`themes/generic.xaml`側、または`Dirkster.AvalonDock.Themes.VS2013`パッケージ
  側のいずれに定義があるか要追加確認、本調査では未特定）。
- **単一ペイン構成での妥当性**: 家老の見立てどおり、`PlacementToolBarDockingManager`は単一
  ペインゆえLeft/Top/Right/Bottomへの分割ドッキングは分割先が存在せず意味をなさない。中央
  （Inside）のみへ絞ることは機能的に無害と考えられる。

### 不明点

- OverlayWindowのControlTemplateがAvalonDock本体`generic.xaml`側かVS2013テーマパッケージ側の
  どちらにあるかは未特定（本調査では時間の関係で追跡していない、着手時に要確認）。
- `DockingManager`側にOverlayWindow用のスタイル差し替えプロパティが存在するか（`AnchorablePane
  ControlStyle`と同様の仕組みがあるか）は未確認。

---

## 調査2: AttachDragの中間的失敗モード（Height=5px関連、殿の新規報告への回答）

### 結論

質問1（Height依存の閾値判定）: **無い**。
質問2（中間的失敗モード）: **一次ソース上に確定的に存在する**。フロートウィンドウの
アクティブ化タイミングでマウス左ボタンが既に離されていると、OSレベルのドラッグ処理が
一切発動せず、静かに終了する経路がある。
質問3（十字型矢印の正体）: 確度中、AvalonDockのドッキングガイド（DropTargetアイコン群、
視覚的に十字配置）である可能性が高いが断定はできない。

### 根拠

1. **質問1への回答**: `AnchorablePaneTitle.cs`のドラッグ開始トリガーはHeight/ActualHeightと
   無関係。`OnMouseMove`（L85-89）はボタン状態のリセットのみ、`OnMouseLeave`（L92-114）が
   唯一のトリガーで、`_isMouseDown && e.LeftButton == Pressed`の条件のみで
   `manager.StartDraggingFloatingWindowForPane(paneModel)`を呼ぶ。
   `SystemParameters.MinimumHorizontalDragDistance`等のしきい値判定はコード上どこにも
   現れない。むしろHeightが小さいほど`OnMouseLeave`（矩形外に出る）は容易に発火しやすい
   （マウスの垂直方向のわずかな動きで即座に矩形外に出るため）。

2. **質問2への回答（確定的、重要）**: `StartDraggingFloatingWindowForPane`
   （`DockingManager.cs:2070-2099`）は`CreateFloatingWindowForLayoutAnchorableWithoutParent`で
   フロートウィンドウを生成後、`fwc.AttachDrag()`→`fwc.Show()`の順で呼ぶ。
   `AttachDrag()`（`LayoutFloatingWindowControl.cs:380-393`、デフォルト`onActivated=true`）は
   即座にOSドラッグを発動せず、`_attachDrag = true; Activated += OnActivated;`で**ウィンドウの
   Activatedイベントまで遅延**する。実際のOSレベルドラッグ（`WM_NCLBUTTONDOWN`送信、
   `InternalOnActivated`、L751-813）は、
   ```csharp
   if (!_attachDrag || Mouse.LeftButton != MouseButtonState.Pressed)
   {
       return;
   }
   ```
   （L755-758）**という条件で即returnする。フロートウィンドウがアクティブ化される瞬間に、
   マウス左ボタンが既に離されていれば、OSドラッグは一切発動しない**（ログ・例外なし、
   静かに終了）。この時点で`fwc.Show()`（ウィンドウの表示自体）は`StartDraggingFloatingWindowForPane`
   内で既に実行されているため、**フロートウィンドウは画面上に生成・表示されるが、マウスに
   追従しない「宙に浮いた」状態になりうる**——これが「何か動きがあるように見えるが実際には
   フロート化として機能しない」という殿の観測と整合しうる中間的失敗モードである。
   極小Height=5pxの領域は、正確なマウスダウン維持が難しく、この失敗モードが起きやすい
   可能性が高い（推測、確度中——実機再現待ち）。

3. **質問3への回答（確度中）**: 「十字型矢印」の一次ソース上の直接的裏付けは無い（AvalonDock
   本体のC#コードにはカーソル形状を明示的に十字にする記述は見当たらなかった）。ただし、
   `OverlayWindow.GetTargets()`が生成するドロップターゲット群は、視覚的に中央+上下左右の
   「十字型」配置になるのがAvalonDock（および同種のドッキングライブラリ全般）の標準的UIパターン
   であり、殿の言う「十字型矢印」はこの**ドッキングガイドのアイコン群**を指している可能性が
   最も高いと推測する（断定不可）。もしこれが正しければ、**ドッキングガイドが表示される＝
   DragServiceによるドラッグプロセス自体は開始できている**ことを意味し、上記2の「AttachDrag
   不発」失敗モードとは別のタイミング（AttachDragは成功したが、その後のドロップ操作で意図せず
   中央=Insideへ落ちてしまい、結果的に元のペインへ戻ってしまう）である可能性も残る。
   Windows標準のSizeAllカーソル（4方向矢印）の可能性も完全には排除できない。

### 不明点・実機確認への申し送り

- 「十字型矢印」が具体的に何を指すかは、忍者の実機確認（画面キャプチャ、`CopyFromScreen`推奨
  ——本件はキャンバスオーバーレイと同様の描画タイミング罠がある可能性を考慮）でのみ確定できる。
- AttachDrag不発仮説が正しいかは、殿・忍者の操作時に「マウスダウンからドラッグ開始までの
  時間・移動量」を意識的に変えて再試行（ゆっくり・確実にボタンを押し続けたまま移動する）
  ことで切り分け可能。もし「ゆっくり操作すれば成功する」なら本仮説が濃厚。

## 家老・忍者への申し送り

- 調査1（ドッキングガイド中央限定）は設計対応が必要な規模（中）。着手判断は殿・家老に委ねる。
- 調査2のAttachDrag不発仮説は、Height=5px化そのものが問題の根なのか、それとも別要因かの
  切り分けに使える。忍者の実機再確認では「ゆっくり確実な操作」と「素早い操作」の両方を
  試行し、成否の違いが出るか観察されたい。

---

## 調査3: AttachDrag不発問題の深掘り（家老追加采配2026-07-19、殿裁定＝ドラッグ機構維持のまま深掘り）

前提となる新事実: Height="5"変更後も殿の物理マウス操作ではフロート化不成立
（十字型矢印は出るがフロートしない）。忍者のUIA検証（ゆっくり/素早い両方）では成功
＝**物理マウス特有の問題**と絞り込まれた。

### 観点1: なぜ不整合が起きるか（タイミング連鎖の一次ソース深掘り）

ドラッグ開始からOSドラッグ発動までの完全な連鎖（すべて一次ソース確認済み）:

1. `AnchorablePaneTitle.OnMouseLeftButtonDown`（L117-137）: `_isMouseDown = true`のみ。
   **`CaptureMouse()`は呼ばない**。
2. `AnchorablePaneTitle.OnMouseLeave`（L92-114）: `_isMouseDown && e.LeftButton == Pressed`なら
   `manager.StartDraggingFloatingWindowForPane(paneModel)`を同期呼び出し。
3. `StartDraggingFloatingWindowForPane`（`DockingManager.cs:2070-2099`）:
   `CreateFloatingWindowForLayoutAnchorableWithoutParent`＝**レイアウトツリーの再構築**
   （ペインをメインウィンドウからフロートウィンドウモデルへ移設、UIスレッド上で同期実行、
   コストが大きい）→ `fwc.AttachDrag()`（`_attachDrag=true`＋`Activated`イベント購読のみ）→
   `fwc.Show()`。
4. `Show()`によりウィンドウがアクティブ化され`Activated`発火 → `InternalOnActivated`
   （v4.74.1: `LayoutFloatingWindowControl.cs:689-767`）→ 冒頭ガード
   `if (!_attachDrag || Mouse.LeftButton != MouseButtonState.Pressed) return;`

**不整合の構造**: ステップ2〜4の間（ツリー再構築＋ウィンドウ生成＋Show、数十〜数百ms）、
**マウスキャプチャを誰も保持していない**。`Mouse.LeftButton`はWPF InputManagerのプライマリ
マウスデバイス状態＝Win32の物理ボタン状態のリアルタイム反映であり、この間にボタンが
離れる（または離れたとOSレベルで判定される）と、ガードで静かにreturnし、ウィンドウは
Show済みのまま「マウスに追従しない」状態で残る。

**物理マウス特有性の説明として最有力な符合（重要）**: 殿の環境には**MouseAssistant**
（マウス補助ツール）が常駐しており、過去に「物理クリック合成との競合でクリックが
アプリへ届かない（UIA経由は正常）」という実例が確定している（memory
`env_mouseassistant_click_conflict.md`、アプリのバグと誤診しやすいという教訓つき）。
今回のパターン——**忍者のUIA合成入力では成功・殿の物理マウスでは失敗**——はこの記録と
完全に一致する。MouseAssistant類の低レベルマウスフックツールは、ボタンイベントの
一時消費・遅延・再合成を行うため、上記ステップ4のガード（`Mouse.LeftButton`読み取り）や
`WM_NCLBUTTONDOWN`後のOSムーブループが「ボタンが離れた」と誤認する事態を引き起こしうる
（推測、確度中——ツールの内部実装に依存するため断定不可）。
**切り分け手順として、殿にMouseAssistantを一時終了した状態でのフロート化再試行を
依頼することを最優先で推奨する**（過去実例でも同手順で解消を確認済み）。

### 観点2: 回避策の可能性

**(a) ecad2側での明示的な`Mouse.Capture()`注入 → 非推奨（機構を壊す）**。
`AnchorablePaneTitle`のドラッグトリガーは`OnMouseLeave`（マウスが要素矩形外へ出ること）に
依存している。WPFの仕様上、`CaptureMouse()`を呼ぶと以後のマウスイベントは要素外でも
その要素へ送られ続け、`MouseLeave`の発火条件自体が変わる——キャプチャ注入はドラッグ開始
トリガーそのものを壊すリスクが高い。また`AnchorablePaneTitle`はAvalonDock内部で生成される
コントロールであり、ecad2側からイベントフックを差し込むにはEventSetter付きカスタムStyle等の
込み入った手段を要する。実装規模小〜中・リスク高で、割に合わない。

**(b) マウスダウン時点でのOSドラッグ先行実行 → 技術的に不成立**。
`WM_NCLBUTTONDOWN`はドラッグ対象のウィンドウハンドルへ送る必要があるが、マウスダウン時点
ではフロートウィンドウがまだ存在しない（生成は`StartDraggingFloatingWindowForPane`内）。
メインウィンドウへ送ればメインウィンドウ自体が動いてしまう。順序を逆転できない構造。

**(c) AvalonDock新バージョンでの修正 → 期待できない**。
最新リリースはv4.74.1（2026-04-25、ecad2使用中と同一）が現時点の最新。master（v5.0.0開発中、
2026-07-01のPR #598まで確認）の`InternalOnActivated`はv4.74.1と実質同一ロジック
（NET40分岐削除のみ、ガード条件は不変）。PR #598はナビゲータ/フロートウィンドウの
スタイル・コンテンツ処理の修正であり、本件ガードには触れていない。**当該の
「Activated時ボタン非押下→静かにreturn」構造は上流でも未修正**。

**(d)【本調査での新発見・最有力】プログラム的フロート化API `LayoutContent.Float()` の活用**。
`LayoutContent.Float()`（v4.74.1 `LayoutContent.cs:524-552`、public API）は
`StartDraggingFloatingWindowForContent(this, startDrag: false)`を呼ぶ——**`startDrag: false`
のためAttachDrag機構（Activatedタイミングのガード）を完全にバイパス**し、フロート
ウィンドウを生成・表示するだけで終わる（マウス状態と無関係に確実に成功する）。
ユーザーはその後、表示されたフロートウィンドウのタイトルバーを通常のOSウィンドウ操作で
自由に移動できる。
- ecad2側の実装: 「フロート化」ボタン（またはコンテキストメニュー項目・ショートカット）
  から対象`LayoutAnchorable`の`Float()`を1回呼ぶだけ。**実装規模は小**（数行＋UI1要素）。
- 副次的事実: AvalonDock標準の`AnchorableContextMenu`（`AnchorablePaneTitle`テンプレート内の
  `MenuDropDownButton`から開くメニュー）には標準でFloatコマンドが含まれており、既定UIでも
  マウスドラッグなしのフロート化経路が本来存在する。ただし現在の配置ツールバーは上段が
  Opacity=0+Height=5のためこのボタンは実質操作不能——専用ボタン新設の方が確実。
- 注意: ドラッグでの直感的フロート化（つかんで引き剥がす）の代替ではなく**並行経路の追加**。
  ドラッグ機構は殿裁定どおり維持したまま、確実に成功する第2経路を提供する位置づけ。

### 実装規模・リスクまとめ

| 案 | 規模 | リスク | 見立て |
|---|---|---|---|
| (a) Mouse.Capture注入 | 小〜中 | 高（MouseLeaveトリガー自体を破壊） | 非推奨 |
| (b) OSドラッグ先行実行 | - | - | 技術的に不成立 |
| (c) バージョン更新 | - | - | 上流未修正、期待できない |
| (d) Float()の並行経路追加 | 小 | 低（既存ドラッグ機構に無変更） | **最有力** |
| (参考) MouseAssistant切り分け | 依頼のみ | なし | **最優先で実施推奨** |

### 家老への申し送り（調査3）

1. まず**MouseAssistant一時終了での再試行**を殿に依頼し、環境要因を切り分けられたし
   （過去実例と症状パターンが完全一致しており、これだけで解決する可能性がある）。
   →【追記2026-07-19】殿実測で「終了させても変わらず」＝**本仮説は棄却された**。
2. 環境要因でなかった場合（またはMouseAssistant常用を前提とする場合）、案(d)の
   `Float()`並行経路が最有力。UI/UX分岐（ボタン配置・メニュー項目・ショートカットの
   どれにするか）を伴うため、着手時は殿確認必須。

---

## 調査4: Float()呼び出し手段の選択肢洗い出し（家老追加采配2026-07-19、殿「他の選択肢も探して」）

前提: MouseAssistant仮説は殿実測で棄却。`LayoutContent.Float()`(startDrag:false経路、
AttachDragガード回避)を何らかのUIから呼ぶ方向で、選択肢を広く列挙する。決定は殿。

### 前提となる一次ソース確認結果

- **標準の「フローティング」メニューは既に存在する**: VS2013テーマ標準の
  `AvalonDockThemeVs2013AnchorableContextMenu`（`AvalonDock.Themes.VS2013/Themes/Generic.xaml:1291-1300`、
  v4.74.1）は「Float／Dock／DockAsDocument／AutoHide／Hide」の5項目構成。先頭のFloat項目は
  `FloatCommand`→`DockingManager.ExecuteFloatCommand`（`DockingManager.cs:2313-2328`）→
  `contentToFloat.Float()`と繋がっており、**まさに調査3の案(d)＝AttachDragガードを回避する
  安全経路が標準装備されている**（一次ソースで呼び出し連鎖を全段確認済み）。
- **このメニューを開く標準UIは2つ**（いずれも`AnchorablePaneTitle`テンプレート内）:
  (1) `MenuDropDownButton`＝タイトルバー右側の小ボタン（ecad2コピーテンプレートでは
  ToolTip「メニュー」、`MainWindow.xaml:388-408`）を左クリック
  (2) `DropDownControlArea`＝タイトルラベル領域そのもの（`DropDownControlArea.cs:38-42`、
  `PreviewMouseRightButtonUp`で`DropDownContextMenu`を表示）を**右クリック**
- **現状の障害**: 上記2つとも上段`AnchorablePaneTitle`内にあり、ドッキング時は
  Opacity=0+Height=5のため実質視認・操作不能。WPFのOpacityは子孫へ乗算継承されるため、
  「ボタンだけOpacityを戻す」ことは原理的に不可（親0×子任意=0）。ボタンのみ可視化するには
  テンプレート再設計（ラベル・背景のみCollapsedにしボタンを残す等）が必要。
- **ダブルクリックでのフロート化トグルはAvalonDock標準に存在しない**:
  `AnchorablePaneTitle.cs`・`LayoutAnchorableTabItem.cs`・`LayoutAnchorableControl.cs`の
  いずれにもダブルクリック（ClickCount>=2）処理は無い（v4.74.1全文grepで確認）。
  Visual Studio本家の「ツールウィンドウタイトルのダブルクリックでフロート⇔ドックのトグル」
  はAvalonDockでは未実装。

### 選択肢一覧（決定は殿、順不同）

**案A: メインメニューバー「表示」への項目追加**
- ecad2の既存メニューバー（表示メニュー等）へ「配置ツールバーをフロート化」（またはトグル）
  項目を追加し、コードビハインドから`LayoutAnchorable.Float()`／`Dock()`を呼ぶ。
- 実装規模: 小（メニュー項目1つ＋数行）。AvalonDockテンプレートに一切触れない。
- 自然さ: IDE系（GX Works3含む）では「表示」メニューからパネルの表示状態を操作するのは
  定番パターン。キーボードファースト方針ともメニューアクセスキー経由で整合。
- 弱点: 「つかんで剥がす」直感操作の代替にはならない（操作の起点がパネルから遠い）。

**案B: 下段TabItem帯への右クリックメニュー追加**
- 既にドッキング時の視覚的主体である下段帯（`DockedDragHandle`のあるTabItem）へ、
  ecad2側の`ItemContainerStyle`で`ContextMenu`を追加し、Float()を呼ぶ項目を置く。
  標準`AnchorableContextMenu`（5項目）をそのまま流用するか、Float単独の専用メニューに
  するかは選べる。
- 実装規模: 小〜中（既存`PlacementToolBarPaneControlStyle`の`ItemContainerStyle`へ
  ContextMenu Setter追加＋コマンド配線）。
- 自然さ: 「パネルを右クリック→フローティング」はVS系・GX Works3系とも自然な操作。
  操作の起点がパネル自身にあり、案Aより発見しやすい。
- 弱点: 右クリックメニューの存在自体に気づく手がかりが無い（発見可能性は中程度）。

**案C: 下段TabItem帯へのダブルクリックトグル追加**
- 下段帯へecad2側でダブルクリックハンドラを追加し、Float()（フロート時はDock()）を呼ぶ。
  VS本家の「タイトルダブルクリックでフロート⇔ドック」トグルの再現。
- 実装規模: 小〜中（イベントハンドラ＋トグル判定）。
- 自然さ: VSに慣れた利用者には最も直感的。ドラッグ不成立問題の実用上の迂回路として自然。
- 弱点: AvalonDock標準に無い機能のためecad2独自実装となる。誤操作（意図せぬダブルクリック）
  でフロート化する可能性。帯は高さ5px＋タブ領域と狭く、ダブルクリックの当てやすさに難。

**案D: 配置ツールバー本体（ToolBar領域）への小型フロート化ボタン追加**
- 家老の案3の具体化。AvalonDockテンプレートでなく、配置ツールバーのToolBar部分
  （ecad2自身のUI）へ小型アイコンボタン（例: フロート化グリフ）を追加し、Float()を呼ぶ。
- 実装規模: 小（既存ToolBarへのボタン1個＋数行、AvalonDockテンプレート非接触）。
- 自然さ: ボタンが常時可視のため発見可能性が最も高い。GX Works3系の「パネル右上の
  小ボタン群」の文法とも整合。
- 弱点: ツールバーの限られた横幅を1ボタン分消費。配置ツール群（機能ボタン）と
  ウィンドウ操作ボタンが同じ列に混在する違和感はありうる。

**案E: 上段タイトルバーのテンプレート再設計（ボタンのみ残す）**
- 上段の`PlacementToolBarAnchorablePaneTitleStyle`を「Opacity=0で全体を消す」方式から
  「ラベル・背景のみ非表示、`MenuDropDownButton`（標準メニュー、Float項目入り）だけ残す」
  テンプレート再カスタムへ変更。標準機能のみで完結する。
- 実装規模: 中（ControlTemplateの再調整、T-100型の作業）。
- 自然さ: AvalonDock標準UIをそのまま活かす王道。ボタンからFloat/Dock/AutoHide等の
  全操作が可能になる。
- 弱点: これまでの経緯（Collapse化→Opacity=0→Height調整と3周した領域）をさらに触る
  ことになり、新たな回帰リスク。上段の高さをある程度戻す必要もありうる
  （ボタン15pxが視認・操作できる高さ）＝省スペース化(要件1)と再びトレードオフ。

**案F: キーボードショートカット追加**
- 例「配置ツールバーのフロート化トグル」を既存ショートカット体系へ追加し、Float()/Dock()を
  呼ぶ。単独でも、案A〜Eいずれとの併設でも可。
- 実装規模: 小。
- 自然さ: **本プロジェクトの主眼（キーボードファースト）と最も整合**。マウス操作の
  不安定性問題を原理的に完全回避。
- 弱点: ショートカットの発見可能性は最も低い（メニュー表記等との併設が実質前提）。

### 参考（一次ソースからの余談）

- ドキュメントタブ側（`LayoutDocumentTabItem.cs:100-125`）は`CaptureMouse()`＋
  `SystemParameters.MinimumHorizontalDragDistance`しきい値の近代的なドラッグ実装を持つ。
  Anchorable系タイトル（`AnchorablePaneTitle`、キャプチャ無し・MouseLeaveトリガー）とは
  実装世代が異なる——今回の物理マウス不成立がAnchorable系固有の実装古さに起因する
  可能性を示唆する状況証拠（推測）。

### 組み合わせの視点

案は排他ではない。例えば「案D（可視ボタン、発見可能性）＋案F（キーボード、主眼整合）」の
併設は、実装規模小のまま両方の弱点を補完し合う。標準メニュー5項目（AutoHide等）まで
提供したい場合のみ案B/Eが要る。

---

## 調査5: 「ドッキング」タブ自己複製バグ＋フロート位置(0,0)固定の根本原因（家老采配2026-07-19、実装後の実機NG 2件）

前提となる観測（忍者実機・殿目視、いずれも再現確認済み）:
(a) フロートウィンドウのメニュー「ドッキング」(Dock)選択→メインへ再統合されず、フロート
ウィンドウ内にタブが自己複製（3回試行で3つ重複）。「タブ付きドキュメントとしてドッキング」
(DockAsDocument)は成立する（ただし配置ツールバー位置でなくドキュメントエリアへの統合）。
(b) メニューからのフロート化は常にプライマリモニタ原点(0,0)に生成（3回とも Left=0, Top=0）。

### (a) タブ自己複製——全連鎖を一次ソースで確定（CONFIRMED級）

以下の5段連鎖。すべてAvalonDock v4.74.1一次ソースの実コードで裏付け済み:

1. **メニューFloat→`CreateFloatingWindowCore`**（`DockingManager.cs:3239-3245`）:
   `PreviousContainer=P0`（メイン側の元ペイン）を正しく設定し、P0から自分を除去（P0は空になる）。
2. **`CollectGarbage`がPreviousContainerを強制クリアしP0を削除**（`LayoutRoot.cs:352-396`）:
   空ペインP0を参照するコンテンツのうち、**`IsVisible=true`のAnchorable（＝今まさにフロート
   表示中の配置ツール）のPreviousContainerはnullへ強制クリアされる**（L374-379、保護されるのは
   `!IsVisible`＝Hide中のもののみ）。その結果P0は無参照となり**削除される**（L389-394）。
   ※T-099再ドック調査書§5の「PreviousContainerから参照されている間は削除しない」という
   当時の記述は**不正確だった**と本調査で判明（保護条件はIsVisible=falseの場合のみ）。訂正する。
3. **メニューDock→`Dock()`→PreviousContainer==null→`InternalDock()`フォールバック**
   （`LayoutContent.cs:598-634`）。
4. **`InternalDock()`の最終フォールバック探索にフロートウィンドウ除外フィルタが無い**
   （`LayoutAnchorable.cs:197`）: `root.Descendents().OfType<LayoutAnchorablePane>().FirstOrDefault()`
   ——探索1(ActiveContent=自分でスキップ)・探索2(右サイド非フロートペイン、該当なし)を経て、
   **唯一残存するAnchorablePane＝フロートウィンドウ内の自分の親ペイン**が拾われる
   （探索2にはある`!pane.IsHostedInFloatingWindow`フィルタが探索3には無い）。
5. **自分の親への`Children.Add(this)`が重複エントリを生む**（`LayoutGroup.cs:198-209`）:
   `Children_CollectionChanged`のAdd処理は`if (element.Parent == this) continue;`（L204）——
   **同一親への再Addでは旧エントリの除去がスキップされ、ObservableCollectionに同一インスタンス
   が2個並ぶ**。タブが1個増える。Dockを押すたび繰り返し→3回で3タブ（観測と完全一致）。

**DockAsDocumentが正常な理由**（観点2への回答）: `DockAsDocument()`（`LayoutContent.cs:555-595`）
の探索は`LayoutDocumentPane`限定＋`FindParent<LayoutDocumentFloatingWindow>() == null`フィルタ
付き（L576）。AnchorablePaneは対象外のため自己参照ループに陥らない。

**T-099(c)との関係**（観点3への回答）: **無関係**。Header Collapse化はモデル層
（PreviousContainer/CollectGarbage）に一切関与しない。本質は**AvalonDock標準ロジックが
「単一ペイン・単一Anchorable・独立DockingManager」というecad2特有の構成で破綻する潜在バグ**
（通常の複数ペイン構成ではInternalDockのフォールバックが別の適切なペインを見つけるため
実害が出ない）。これまでMenuDropDownButtonが実質操作不能（Opacity=0/Height=5）だったため
誰も踏まなかっただけで、T-099(c)がメニューを操作可能にしたことによる顕在化である。

### (b) フロート位置(0,0)固定——仕様的制約と確定

`CreateFloatingWindowCore`（`DockingManager.cs:3281-3287`）:
```csharp
fwc = new LayoutAnchorableFloatingWindowControl(...)
{
    Width = fwWidth, Height = fwHeight,
    Left = contentModel.FloatingLeft,   // 既定0.0のまま → Left=0
    Top = contentModel.FloatingTop      // 既定0.0のまま → Top=0
};
```
`LayoutAnchorable.FloatingLeft/FloatingTop`は誰も設定していなければ既定0.0で、**そのまま
ウィンドウ座標になる＝プライマリモニタ原点(0,0)**。ドラッグ経路では`InternalOnActivated`
（`LayoutFloatingWindowControl.cs:795-796`）が`Left/Top=マウス位置`へ上書きするため正しい
位置に出るが、メニュー経路（startDrag:false）にはこの補正が無い。観測（常に0,0）と完全一致。

### 対処案の見立て（実装は侍、UI/UX分岐は殿確認）

**(a) Dock自己複製への対処**（いずれも机上設計、未検証）:
- **案1: `ContentDocking`イベントでのフック（最有力・侵襲小）**——`ExecuteDockCommand`は
  実行前に`RaiseContentDocking`を発火し、**キャンセル可能**（`DockingManager.cs:2334-2337`）。
  ecad2側でこのイベントを購読し、`Cancel=true`で標準Dock()を止め、代わりに独自の
  「正しい再ドッキング」（XAMLで定義済みの元のLayoutAnchorablePaneをx:Name参照で取得し
  `Children.Add`、その後フロートウィンドウは自動クローズ）を実行する。標準メニューは
  そのまま使える。
- 案2: `LayoutAnchorableItem.DockCommand`（公開DP）を独自コマンドへ差し替え——効果は案1と
  同等だが、LayoutItemの取得タイミング管理が必要で案1よりやや複雑。
- 案3: カスタムContextMenuへ差し替えて「Dock」項目自体を除去（`DockingManager.
  AnchorableContextMenu`プロパティ）——標準機構自体を触らない最保守案だが、再ドッキング
  経路をドラッグ（従来問題含み）に戻すことになり本末転倒の懸念。
- （上流報告に値するバグでもある: `InternalDock`探索3へのIsHostedInFloatingWindowフィルタ
  欠落＋`Children_CollectionChanged`の同一親Add重複許容。ただし上流修正待ちは非現実的）

**(b) フロート位置への対処**（侵襲小）:
- `ContentFloating`イベント（Float実行前に発火、`StartDraggingFloatingWindowForContent`
  L2017-2025）または事前設定で、`LayoutAnchorable.FloatingLeft/FloatingTop`へメイン
  ウィンドウ近傍の適切な座標（例: 配置ツールバーの現在スクリーン座標）を設定しておく。
  `CreateFloatingWindowCore`はこの値をそのまま使うため、これだけで意図位置に出るはず。
  代替: `LayoutFloatingWindowControlCreated`イベントでfwc.Left/Topを直接補正。

### 不明点

- 案1のイベント購読で、複数DockingManager構成（ecad2特有）における発火元Managerの特定は
  `PlacementToolBarDockingManager`のイベントのみ購読すれば足りるはず（フロートウィンドウは
  同Managerに属す）だが、実装時に要確認。
- (b)のFloatingLeft/Topの座標系（スクリーン座標・DPI補正の有無）は実機で要確認。

---

## 調査6: 「縦長44pxタブとして左端に別追加」の根本原因——RootPanel自動補完によるDocumentPane侵入（殿裁定の徹底調査、2026-07-19）

### 観測（忍者実測）

独自再ドッキング後、配置ツールバーが元の横長位置（569×81）でなく縦長（44×81）の別ペイン
として左端に現れ、ボタン列がオーバーフロー収納される。同型の見た目はHeight=1/5時代の
ドラッグ再ドック検証でも観測されていた。

### 根本原因（全段一次ソースCONFIRMED）

**AvalonDockの`LayoutRoot`は「RootPanelがnullになると、空のLayoutDocumentPane入りパネルを
自動補完する」という隠れた不変条件を持つ**。これが独自再ドッキング処理と衝突していた。

確定した連鎖（すべてv4.74.1一次ソースで裏付け）:

1. メニューFloat→配置ツールAnchorableがフロートへ→元ペインP0（PlacementToolBarPane）空に。
2. `CollectGarbage`: PreviousContainer強制クリア（調査5参照）→P0無参照→削除→**親のRootPanel
   （LayoutPanel）も空になり「空LayoutPanel削除」（`LayoutRoot.cs:413-419`）が発動→
   `parentGroup.RemoveChild(emptyLayoutPanel)`→`LayoutRoot.RemoveChild`は
   **`RootPanel = null`とプロパティsetter経由で書く**（L297-300）→setterのnull補完
   `_rootPanel = value ?? new LayoutPanel(new LayoutDocumentPane())`（**L91**）により
   **空のLayoutDocumentPane入り新RootPanelが自動生成される**。
3. この状態が終了時自動保存で焼き付いたのが、先に発見した汚染XML
   `<RootPanel Orientation="Horizontal"><LayoutDocumentPane /></RootPanel>`——**実ファイルの
   証拠と完全に一致**（DocumentPaneの侵入経路がこれで確定。DockAsDocument実験は不要、
   Float操作だけで侵入する）。
4. メニューDock→ContentDockingハンドラ→`PlacementToolBarPane.Parent==null`だが
   `layout.RootPanel`は**非null**（DocumentPane入り自動補完パネル）→else分岐→
   `RootPanel.Children.Add(PlacementToolBarPane)`→**Horizontal RootPanel内に
   [空DocumentPane, PlacementToolBarPane]が同居**→幅を分け合い配置ツール側が縮小→
   縦長44px・「左端に別追加」の観測そのもの。
5. さらにこの空DocumentPaneは「メインウィンドウ内最後のDocumentPane保護」
   （`LayoutRoot.cs:383-386`、空でも削除しない）により**CollectGarbageでも不死身**——
   ハンドラ末尾のCollectGarbage追加でも掃除されない。

### 家老の3観点への回答

1. **PlacementToolBarPane自体は同一インスタンスが正しく復帰している**（新規ペイン生成では
   ない）。問題は復帰先RootPanelに自動補完の「招かれざる空DocumentPane」が同居すること。
2. **縦長はOrientation取り違えではない**。Horizontal親パネル内でのDocumentPaneとの幅分割の
   帰結（DockWidth既定Star同士の分配＋Min制約の兼ね合い）。標準ドラッグ再ドックとの差では
   なく、Float時点で共通に発生する状態汚染（連鎖2）が根。Height=1/5時代の同型観測も同じ
   機構で説明がつく。
3. **ContentDockingフック方式の構造的限界は「ある」**。モデル手術の自前実装は、AvalonDockの
   隠れた内部不変条件（RootPanel自動補完・DocumentPane保護・CollectGarbageの掃除規則）との
   整合を全て自前で担うことを意味し、対処を重ねるたび新たな不変条件と衝突するモグラ叩き
   構造になっている（実績: オフツリー再接続→CollectGarbage省略→DocumentPane同居、と3周）。

### 対処案

- **案Y（本命・推奨）: T-058実証済みインフラの流用**——ContentDockingハンドラでは
  `e.Cancel=true`の後、モデル手術をやめて**ハードコード既定レイアウトXMLのDeserialize**
  （`TryDeserializeDockingLayout(PlacementToolBarDockingManager, 既定XML)`、
  `ResetDockingLayoutToDefault`の単一Manager版に相当）を呼ぶだけにする。
  - レイアウト全体が既定（横長・正位置）へ戻り、フロートウィンドウのモデルもDeserializeで
    丸ごと消え、Content再バインドは実証済みのLayoutSerializationCallbackが担う。
  - **「Ctrl+Alt+Rで復活した」という殿の実測が、この方式の有効性の実証そのもの**。
  - 現行ハンドラの再接続・Children.Add・CollectGarbage・InvalidateArrange等の積み上げは
    全て不要になり削除できる（コード簡素化）。
  - 副次効果: 復帰位置が常に既定＝「元の横長ツールバー位置への復帰」という殿の要望そのもの。
- 案Z（対症・非推奨）: ハンドラ内でRootPanelから空DocumentPaneを手動RemoveChildしてから
  Add——小規模だが、将来別の残骸が現れるたびに同型対処が要るモグラ叩き継続。
- 参考: 上流（AvalonDock）へのissue報告に値する挙動でもある（単一AnchorablePane構成での
  Float→RootPanel自動補完によるDocumentPane侵入）。

### 検証方法（忍者向け、案Y実装後）

1. Float→Dock→元の横長位置・サイズ（569×81相当）への復帰、DocumentPane残骸の不在
   （UIAツリーにTab相当が1つだけ）を確認。
2. Float→Dock→Float→Dockの反復2〜3周で劣化しないこと。
3. 終了→再起動で正常レイアウトが保存・復元されること（再発防止策の検証と併せて）。
