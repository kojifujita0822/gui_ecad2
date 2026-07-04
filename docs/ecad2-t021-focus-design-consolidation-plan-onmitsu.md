# T-021 フォーカス制御「設計集約プラン」— 2026-07-04（隠密）

家老依頼: 増分1(457cddb/cf54a3e/c83d9e3/1315fe4)の「フォーカス喪失のたびに個別ハンドラへ
FocusCanvas()を後追いで足す」方式から、懸念4(ツールバーのキーボードナビゲーション中の逆方向
副作用)を解決できる設計への集約案。**調査+設計案のみ・実装はしない。**

---

## 結論(サマリ)

**推奨: 案(a)+案(c)のハイブリッド。増分1の4コミットは破棄不要、上乗せ修正で対応可能。**

- 案(b)(PreviewLostKeyboardFocus+Handled化)は、一次情報により**懸念4を解決できないことが判明**
  (理由は根拠1参照)。不採用。
- 案(c)(入力手段の判別)は、Clickイベントの事後判定では不可能だが、**入力経路そのものを
  PreviewMouseLeftButtonUp(マウス)とPreviewKeyDown(Enter/Space、キーボード)に分離すれば
  確実に実現できる**(根拠2参照)。
- 案(a)(ToolMode変更の単一箇所への集約)は単独では懸念4を解けないが、案(c)と組み合わせる
  ことで「呼び出し漏れ防止」と「マウス/キーボードの出し分け」を両立できる。
- 対象操作は3箇所(SelectDefaultButton_Click / BuiltinPlaceButton_Click /
  OpenPartSelectionButton_Click)のみ。他の経路(Enter配置/F5-F8/Escape/クリック配置/
  ダイアログクローズ後)は元々「グローバルショートカット」または「常時キャンバス復帰が
  自然な文脈」であり、変更不要(根拠3のトレース表参照)。
- GridSplitterのタブオーダー問題(懸念4の迷い込み先)は、設計案に依らず独立した小修正
  (`KeyboardNavigation.IsTabStop="False"`)が必要(根拠4参照)。

---

## 根拠

### 根拠1: 案(b) PreviewLostKeyboardFocus+Handled化が懸念4を解決できない理由

Web一次情報調査の結果:
- `KeyboardFocusChangedEventArgs`(`PreviewLostKeyboardFocus`の引数)が公開するプロパティは
  `OldFocus`/`NewFocus`/`Device`/`KeyboardDevice`/`Timestamp`/`Handled`/`Source`のみで、
  **フォーカス移動の「原因」(Tab等の標準ナビゲーションか、アプリコードのKeyboard.Focus()
  呼び出しか)を示す情報は存在しない**
  ([KeyboardFocusChangedEventArgs Class](https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.keyboardfocuschangedeventargs)、隠密調査)。
- WPFの`KeyboardNavigation`クラスのTabキー処理は、内部で次要素を特定した後
  `IInputElement.Focus()`を呼ぶだけで、アプリコードが呼ぶのと同一のAPIであり、区別用の
  フラグは付与されない([KeyboardNavigation.cs 参照ソース](https://www.dotnetframework.org/default.aspx/Dotnetfx_Win7_3@5@1/Dotnetfx_Win7_3@5@1/3@5@1/DEVDIV/depot/DevDiv/releases/Orcas/NetFXw7/wpf/src/Framework/System/Windows/Input/KeyboardNavigation@cs/1/KeyboardNavigation@cs))。
- `PreviewGotKeyboardFocus`/`PreviewLostKeyboardFocus`を`Handled=true`にすればフォーカス
  変更自体を阻止できる点は事実として明記されている
  ([Focus Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/focus-overview))。

**結論**: 「ボタンへフォーカスが移ろうとする」という一つの事象自体は`PreviewLostKeyboardFocus`
で検知・阻止できるが、その事象が「ユーザーの意図的なTabナビゲーション」なのか「他の理由」なのか
を区別する材料が引数に無い。したがって、この仕組みだけでは懸念4(意図的なキーボードナビゲーション
中は戻さない、それ以外は戻す)という**条件分岐そのものを表現できない**。不採用。

### 根拠2: 案(c) 入力経路の分離による判別

- `RoutedEventArgs`(Clickイベントの引数)自体には発火源(マウス/キーボード)を示す情報は
  含まれず、`Source`/`OriginalSource`も同じButtonを指すのみ
  ([ButtonBase.Click Event](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.primitives.buttonbase.click))。
- 同ページの公式Remarksによれば、**Enterキー押下時は`IsPressed`もマウスキャプチャも変化しない**
  ため、`Mouse.LeftButton`や`IsPressed`をClickハンドラ内で調べる方法は信頼できない。
- 公式Remarksは、素の`MouseLeftButtonDown`がButtonBase内部でHandled化されるため、
  マウス操作を捕捉したい場合は`PreviewMouseLeftButtonDown`(またはUp)を使うことを明記している。
- 以上より、**Clickイベントを使わず、`PreviewMouseLeftButtonUp`(マウス専用)と
  `PreviewKeyDown`(Key.Enter/Key.Space、キーボード専用)を最初から別ハンドラとして
  登録する方式**であれば、判別ではなく「そもそも別経路」として確実に区別できる。

`InputManager.Current.MostRecentInputDevice`という内部的な最新入力デバイス追跡APIも存在する
(`FocusVisualStyle`の表示可否判定に使われている、[Styling for Focus in Controls, and FocusVisualStyle](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/styling-for-focus-in-controls-and-focusvisualstyle))が、
これは非公開API相当でありドキュメント化された用途ではないため、今回は採用を推奨しない。

### 根拠3: 全経路トレース

| 経路 | 現状(1315fe4) | 推奨案適用後 |
|---|---|---|
| Enter配置(選択セル+Enter) | フォーカスは既にキャンバス(ガード条件) | 変更なし |
| F5/F6/F7/F8キー配置 | 常時FocusCanvas()(TryPlaceElement経由) | 変更なし(グローバルショートカットなので常時復帰が妥当) |
| クリック配置(キャンバス上) | 常時FocusCanvas() | 変更なし |
| Escapeキー | 常時FocusCanvas() | 変更なし(グローバルショートカット) |
| ダイアログクローズ後(TryPlaceElement末尾) | 常時FocusCanvas() | 変更なし(モーダル復帰は常時キャンバスへが自然) |
| 選択ツールボタン・マウスクリック | FocusCanvas()実行 | 維持(マウス専用ハンドラ) |
| 選択ツールボタン・キーボード(Enter/Space) | FocusCanvas()実行(懸念4の原因) | **FocusCanvas()を呼ばない**(キーボード専用ハンドラ) |
| a接点等配置ボタン・マウスクリック | FocusCanvas()実行 | 維持 |
| a接点等配置ボタン・キーボード | FocusCanvas()実行(懸念4の原因) | **呼ばない** |
| 自作パーツボタン・マウスクリック | FocusCanvas()実行 | 維持 |
| 自作パーツボタン・キーボード | FocusCanvas()実行(懸念4の原因) | **呼ばない** |
| GridSplitter(Tab移動) | タブオーダーに含まれ迷い込む | `KeyboardNavigation.IsTabStop="False"`で除外 |
| CyclePanelFocus(Shift+Tab) | 独自の2段ロジック | 変更なし |

変更対象は**3つのボタンハンドラのみ**。他はグローバルショートカット的性質のため、常時
`FocusCanvas()`を呼ぶ現状の設計のままで問題ない(懸念4はボタンのマウス/キーボード両対応
という性質に起因する問題であり、キー入力専用の経路には当てはまらない)。

### 根拠4: GridSplitterのタブオーダー(懸念4の迷い込み先)

一次情報(dotnet/wpfソースコード)で確認:
- `GridSplitter → Thumb → Control`という継承関係で、`Control`が`Focusable`を`true`に
  上書きした後、`Thumb`が`false`に再上書き、さらに`GridSplitter`が`true`に再々上書きする
  ([Thumb.cs](https://raw.githubusercontent.com/dotnet/wpf/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/Primitives/Thumb.cs)、
  [GridSplitter.cs](https://raw.githubusercontent.com/dotnet/wpf/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Controls/GridSplitter.cs))。
- `Control.IsTabStop`の既定値は`true`で、`Thumb`/`GridSplitter`に上書きは無い
  ([Control.IsTabStop](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.control.istabstop))。
- **結論: GridSplitterは既定でFocusable=true・IsTabStop=true**であり、これが忍者実機検証の
  「Tabで迷い込む」現象の直接原因(事実)。
- `IsTabStop`は`KeyboardNavigation.IsTabStopProperty`のエイリアスであるため、
  `src/Ecad2.App/MainWindow.xaml`の2箇所のGridSplitter(235行目・266行目付近)に
  `KeyboardNavigation.IsTabStop="False"`を追加すればタブオーダーから除外できる(事実)。
  `Focusable`はtrueのまま残すため、矢印キーによるリサイズ操作(対応していれば)は温存されると
  推測される(一次情報での動作確認はできておらず、侍実装後の忍者実機検証が必要)。
- この修正は設計案(a)(b)(c)(d)のいずれを採用しても独立に必要。

---

## 移行/リバート範囲の推奨

**増分1の4コミットは破棄不要。現在のHEAD(1315fe4)の上に追加コミットを積む形を推奨。**

| コミット | 内容 | 判定 |
|---|---|---|
| 457cddb | Enter配置本体(案X) | 維持。懸念とは無関係 |
| cf54a3e | BuiltinPlaceButton_ClickへFocusCanvas追加 | 部分修正。マウス専用ハンドラへ分離 |
| c83d9e3 | SelectDefaultButton/TryPlaceElementへFocusCanvas統一 | TryPlaceElement側は維持。SelectDefaultButton側は分離 |
| 1315fe4 | EscapeケースとOpenPartSelectionButtonへFocusCanvas追加 | Escapeケースは維持。OpenPartSelectionButton側は分離 |

根拠3のトレース表の通り、変更が要るのは「ボタン3種のマウス/キーボード入力経路の分離」のみで、
Enter配置本体・グローバルショートカット群・ダイアログ復帰処理は無傷で使える。ブランチを
作成時点まで戻す、または各コミットをリバートする必要はなく、**増分(vi)として追加コミットする
方が作業量・リスクとも小さい**と判断する。

## 侍の実装段取り案(増分vi)

1. ツール選択処理(ToolMode変更本体)を、各ボタンのハンドラから独立した共通メソッドへ
   切り出す(例: `ActivateSelectDefault()`, `ActivateBuiltinTool(partName, isOr)`,
   `ActivateOpenPartSelection()`。既存の`TryPlaceActiveTool`と同様の切り出しパターン)。
2. 3つのボタン(SelectDefaultButton/BuiltinPlaceButton/OpenPartSelectionButton)について、
   XAMLの`Click`属性を外し、`PreviewMouseLeftButtonUp`(マウス専用: 共通メソッド+
   `FocusCanvas()`)と`PreviewKeyDown`(`Key.Enter`または`Key.Space`時のみ: 共通メソッドの
   み、`FocusCanvas()`は呼ばない)の2ハンドラに置き換える。
3. `MainWindow.xaml`のGridSplitter2箇所に`KeyboardNavigation.IsTabStop="False"`を追加。
4. 忍者による実機検証で重点確認してほしい点:
   - 3ボタンとも、マウスクリック時は従来通りキャンバスへ復帰し、Enter配置が成立すること。
   - Tab/矢印キーでツールバーへ入り、3ボタンのいずれかをEnter/Spaceで押した場合、
     フォーカスがツールバーに留まり、続けてTab/矢印キーでツールバー内ナビゲーションが
     継続できること(懸念4の解消確認)。
   - GridSplitterがTabオーダーから外れ、かつ(対応していれば)矢印キーでのリサイズ操作
     自体は温存されていること(根拠4の推測部分の検証)。
   - **[推測・要確認]** `PreviewMouseLeftButtonUp`/`PreviewKeyDown`への置き換えにより、
     `ecad2-ui-automation`スキル等の外部からのボタン操作(UI Automationの`Invoke`パターン
     経由)が、内部的にButtonの`OnClick`(Clickイベント)を経由する実装になっている場合、
     新設ハンドラを迂回してしまう可能性がある。実際にUI Automation経由でこれらのボタンを
     操作し、想定通りFocusCanvas()の有無が切り替わるか(またはそもそも配置自体が動くか)を
     確認すべき。

## 残増分(iv)Esc・(v)スクロールとの相互作用

- (iv) Escの多段階キャンセル(4層)は、Escapeケースの中身(SelectedCell/Tool/StatusMessage
  のリセット)が将来拡張されても、`FocusCanvas()`を常時呼ぶという扱い(根拠3のグローバル
  ショートカット群と同じ)は変更不要と考えられる(推測、Esc自体はボタンのマウス/キーボード
  二重発火問題を持たないため)。
- (v) 矢印キー移動時の自動スクロール(`BringIntoView`案、隠密の別調査
  `docs/ecad2-t021-enter-placement-survey-onmitsu.md`参照)は、フォーカス制御そのものとは
  独立した機能であり、今回の設計変更と直接の依存関係は無いと考えられる(推測)。むしろ今回の
  集約でフォーカス制御がより一貫すれば、(v)も安定して動作すると期待できる。

## 不明点

1. `KeyboardNavigation.IsTabStop="False"`設定後、GridSplitterの矢印キーによるリサイズ操作が
   実際に機能するか(一次情報で未確認、実機検証要)。
2. `PreviewMouseLeftButtonUp`/`PreviewKeyDown`への置き換えが、UI Automation経由の操作
   (Invoke パターン)にどう影響するか(一次情報で未確認、実機検証要)。
3. WPFの`ButtonBase`が内部で`AutomationPeer.Invoke()`をどう`OnClick`/`Click`イベントに
   接続しているかの実装詳細は本調査では確認していない(不明)。
