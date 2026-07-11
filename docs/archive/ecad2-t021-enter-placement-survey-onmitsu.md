# T-021 論点2(Enter=配置確定)先行調査 — 実現性と地雷 — 2026-07-04（隠密）

家老依頼: 「選択セルにカーソルがある状態でEnterキー→アクティブなツールの要素をそのセルに配置→
直後に浮動インライン入力欄へフォーカスを委譲」（案X）のWPFでの実現性評価。GuiEcad(WinUI3)が
Issue #6179で9ラウンド格闘の末に断念した経緯（`docs/ecad2-keyboard-requirements.md`）を踏まえ、
着手前にWPF固有の地雷を洗い出す。**調査のみ・実装はしない。**

---

## 結論（サマリ）

| 観点 | 結論 |
|---|---|
| 1. 既存フォーカス委譲の足場 | **フォーカス取得（Focus）の足場は既にある。フォーカス返却（ダイアログクローズ後の戻し先）は未実装・未検証** |
| 2. EnterがIsDefaultボタン等に奪われないか | **奪われるリスクは低い**（MainWindow.xamlにIsDefaultボタンが存在しないため） |
| 3. 選択セル+Enter起点の配置ロジック | **既存コードとほぼ同型のパターンが2つ既にあり、流用できる見込み** |
| 4. 矢印キー移動時の自動追従スクロール | **未実装だが、流用できる座標変換の既存足場がある**(`BringIntoView`実機未検証) |
| 5. WinUI3 Issue #6179相当の問題の有無 | **限定的なWeb検索の範囲では見つからず。「無し」と断定はできない（確認できず）** |

総評: WPFはGuiEcad(WinUI3)が躓いた領域（フォーカスの暗黙移動）についてアプリ側で確実に
上書きできる公式な仕組み（`PreviewLostKeyboardFocus`のHandled化）を持つことが一次情報で確認できた。
既存コード（T-016/T-017/T-026の実装）にも案Xと同型の経路が複数既に存在しており、土台面では
実現性は高いと考えられる（推測）。ただし個別の実機検証（ダイアログクローズ後のフォーカス戻り先、
ズーム時のBringIntoView挙動）は侍実装時に要確認。

---

## 根拠（出典・該当コード行）

### 観点1: 現行の浮動インライン入力のフォーカス取得・返却

**事実**: `docs/archive/ecad2-t021-keyboard-spec.md` 論点4は「入力は浮動インライン（非モーダル）を基本とする」
と規定しているが、現行実装の`ElementPlacementDialog`（`src/Ecad2.App/Views/ElementPlacementDialog.xaml`）
は**独立した`Window`であり、`TryPlaceElement`（`src/Ecad2.App/MainWindow.xaml.cs:246`）から
`dialog.ShowDialog()`で呼ばれる通常の"モーダルダイアログ"**である。「モーダル非ネスト規約」
（小窓は同時に1枚まで）は満たしているが、規約文言の「浮動インライン（非モーダル）」という
表現とは実装の実態が異なる（**気づき**として下記に記載、隠密からの判断はしない）。

- フォーカス取得: `Loaded += (_, _) => DeviceNameBox.Focus();`
  （`src/Ecad2.App/Views/ElementPlacementDialog.xaml.cs:22-25`）。
  ダイアログの`Loaded`イベントで対象TextBoxへ明示的にFocus()する実装が既にある。これは
  Microsoft Learn "Focus Overview"が推奨する手法（"The recommended place to set initial
  focus is in the Loaded event handler"）と合致している。
- フォーカス返却: `TryPlaceElement`（`MainWindow.xaml.cs:233-254`）で`dialog.ShowDialog()`が
  返った後、`_viewModel.SelectedCell = null`等の状態更新はあるが、**キャンバスやセルへ明示的に
  フォーカスを戻すコードは存在しない**。ダイアログが閉じた際にMainWindow内のどのコントロールへ
  実際にフォーカスが移るかはコード上未規定・未検証。

### 観点2: WPFでEnterがIsDefaultボタン等に横取りされないか

- `src/Ecad2.App/MainWindow.xaml`を全文確認した結果、**MainWindow.xaml内に`IsDefault="True"`の
  ボタンは存在しない**（該当するのは`ElementPlacementDialog.xaml:13`のOKボタンのみで、これは
  別ウィンドウ）。よってメインウィンドウのキャンバス上でEnterキーを処理する場合、IsDefault
  ボタンに奪われるリスクは無い。
- `Window_PreviewKeyDown`（`MainWindow.xaml.cs:91`、`MainWindow.xaml:11`で
  `PreviewKeyDown="Window_PreviewKeyDown"`をバインド）は**トンネリングイベント**であり、
  Window→子要素の順で最初に発火する。既存の矢印キー（136-143行）・Deleteキー（144-150行）は
  いずれもこのハンドラ内で`IsCanvasFocused()`をガード条件にして`e.Handled = true`にしている。
  同じ枠組みでEnterキーのcaseを追加すれば、後続のバブリング処理やIsDefault機構より先に確実に
  捕捉できる見込み（既存パターンの延長のため確度は高いが、実機未検証）。

### 観点3: 要素配置ロジックを「選択セル+Enter起点」で呼ぶ既存経路

既に酷似したパターンが2つ存在する。

- (a) クリック起点: `LadderCanvasHost_PreviewMouseLeftButtonUp`（`MainWindow.xaml.cs:73-83`）。
  `_viewModel.Tool.Mode == PlaceElement`なら、そのToolの`PartId`に対応するエントリを
  `PartPalette.Entries`から検索し`TryPlaceElement`を呼ぶ。
- (b) キー起点（種別固定）: `TryPlaceBuiltin`（`MainWindow.xaml.cs:199-203`、F5/F6/F7/F8から
  呼ばれる）。`SelectedCell`が既にある前提で、指定した図形名のエントリを検索し`TryPlaceElement`
  を呼ぶ。

案X（「アクティブなツールの要素を配置」）は、(a)の判定ロジック（`Tool.Mode==PlaceElement`から
PartIdでエントリ解決）を、マウス位置ではなく`_viewModel.SelectedCell`に対して呼び出す形へ
差し替えるだけで実現できる見込み。実装規模は小さいと推測される。

### 観点4: 矢印キー移動時の自動追従スクロール

- 現状`MoveSelectedCell`（`MainWindow.xaml.cs:156-170`）は`SelectedCell`の更新のみで、
  スクロール処理は一切無い。
- `LadderCanvas`（`src/Ecad2.App/Views/LadderCanvas.cs:106-113`）に
  `internal Rect CellRectDip(GridPos cell)`という、セル→ローカル矩形（DIP座標）変換の
  既存メソッドがある（選択ハイライト描画`Draw()`内83行目でも使用中）。これを流用し
  `LadderCanvasHost.BringIntoView(CellRectDip(newCell))`（WPF標準`FrameworkElement.BringIntoView(Rect)`）
  を呼べば、親の`ScrollViewer`（`CanvasArea`、`MainWindow.xaml:241`）が自動スクロールする、
  というのがWPFの標準機能。
- **未検証点**: `LadderCanvasHost`には`ScaleTransform`によるズーム（`LayoutTransform`、
  `MainWindow.xaml:260-262`）が掛かっている。`BringIntoView`はLayoutTransformを考慮して
  座標計算される仕様のはずだが、実機（特にズーム倍率≠100%の状態）での動作確認は行っていない。

### 観点5: WinUI3 Issue #6179相当の問題がWPFに存在するか

- GitHub検索（"dotnet/wpf" "implicit focus" "PointerReleased"等）を行った結果、
  [Issue #6179](https://github.com/microsoft/microsoft-ui-xaml/issues/6179)は
  `microsoft/microsoft-ui-xaml`（WinUI3）固有のissueであり、`dotnet/wpf`リポジトリに
  同種（PointerReleased後にアプリコードを経由せずタブオーダー上の別要素へ強制的にフォーカスが
  移動し、`LosingFocus`/`TryCancel`で検知はできても制御しきれない、というもの）のissueは
  **検索した範囲では見つからなかった**。ただしこれは隠密による限定的な検索であり、悉皆調査では
  ないため「無し」とは断定できない（**確認できず**、として扱う）。
- Microsoft Learn公式ドキュメント
  ["Focus Overview" (WPF)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview?view=netframeworkdesktop-4.8)
  には次の記述がある（原文引用）:
  > "If the PreviewGotKeyboardFocus event or the PreviewLostKeyboardFocusEvent event is
  > handled and Handled is set to true, then focus will not change."
  
  これは、要素が非表示化される等でフォーカスを失う場面（GuiEcadのA6と同種のリスク自体は
  WPFにも存在しうる）でも、**アプリ側が`PreviewLostKeyboardFocus`をハンドルし`Handled=true`
  にすれば確実にフォーカス変更を阻止できる**、と公式に明記されたものである。WinUI3の
  Issue #6179で問題になった「`LosingFocus`/`TryCancel`で検知はできても、フレームワークが
  移動を強行し直す（制御不能）」という性質とは対照的に、WPFでは公式にドキュメント化された
  確実な上書き手段が存在する。

**推測（明示）**: 以上より、WPFは「フォーカスの所在をアプリ側が完全にコントロールできるか」
（`docs/ecad2-keyboard-requirements.md` R1）という要件を、WinUI3より高い確度で満たせる
可能性が高いと考えられる。ただしこれは公式ドキュメントの記述に基づく推測であり、ecad2の
実際のFocusScope構成（`CyclePanelFocus`のコメントで言及されている`IsFocusScope`絡みの
既知の癖、`MainWindow.xaml.cs:260-264`）でEnter配置機能を実装した際に問題が起きないかは、
侍実装後の忍者実機検証が必要。

---

## 不明点

1. `ElementPlacementDialog`が閉じた後、実際にMainWindow内のどの要素へフォーカスが戻るか
   （コード上明示されていないため未検証）。
2. `LadderCanvasHost.BringIntoView`がズーム倍率≠100%の状態でも正しくスクロールするか
   （未検証・実機確認要）。
3. WPFにIssue #6179"相当"の問題が皆無であるとは断定できない（限定的なWeb検索の範囲のため）。

---

## 侍実装時の推奨・注意点

- 観点3の通り、Enterキーのハンドラは`Window_PreviewKeyDown`内に「`IsCanvasFocused()`かつ
  `Tool.Mode==PlaceElement`かつ`SelectedCell`あり」を条件とする既存パターン踏襲のcase追加で
  実現できる見込み。矢印キー・Deleteキーと同じ枠組みに乗せることで一貫性を保てる。
- ダイアログクローズ後のフォーカス戻り先（不明点1）は、GuiEcadのA6（Visual Tree変化時の
  意図しないフォーカス委譲）と同種の地雷になり得るため、`TryPlaceElement`終了時に
  `Keyboard.Focus(LadderCanvasHost)`等で明示的にキャンバスへ戻すコードを追加し、
  暗黙委譲に委ねないことを推奨する（観点5の一次情報の通り、WPFでは`PreviewLostKeyboardFocus`
  のHandled化という確実な手段があるため、必要なら合わせて使える）。
- 観点4の自動スクロールは`CellRectDip`を流用した`BringIntoView`実装が有力候補だが、
  ズーム時の座標計算は必ず実機（忍者）検証を挟むこと。

---

## 派生提案（気づき・範囲外、隠密からは着手しない）

- T-021論点4の規約文言「浮動インライン（非モーダル）」と、現行`ElementPlacementDialog`の
  実装（別Windowのモーダルダイアログ）に用語上の齟齬がある。機能面では「モーダル非ネスト規約
  （小窓は同時に1枚まで）」自体は満たしており実害は無さそうだが、将来「本当に非モーダルの
  埋め込みUIに作り直すべきか」という設計判断が必要になる可能性がある。UI/UXに関わる可能性が
  あるため、隠密からは判断せず気づきとして報告のみに留める。
