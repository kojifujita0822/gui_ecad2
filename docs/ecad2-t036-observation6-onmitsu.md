# T-036観点6 Wチェック（理論側）：プロパティパネル経由デバイス名編集が反映されない

忍者実測（`docs-notes/ecad2-t036-verification-ninja.md` 観点6）を受けた理論側調査。
殿の実操作判別との突合材料として、事実と推測を峻別して報告する。

## 調査事項1: SelectedElementDeviceNameセッターの発火条件と、UIA/SendKeys+Tabで発火しない理論的可能性

**事実**:
- `MainWindow.xaml:328-329`: `<TextBox x:Name="DeviceNameBox"
  Text="{Binding SelectedElementDeviceName, UpdateSourceTrigger=LostFocus}"
  LostFocus="DeviceNameBox_LostFocus"/>`
- セッター本体（`MainWindowViewModel.cs` 199-225行）は、`SelectedElement`が非nullの場合のみ
  `DeviceRenamer.Rename`（既存名→既存名）または新規登録（`DeviceClass.Other`）を行い、
  `MarkDirty()`→`OnPropertyChanged()`→`DeviceTable.Refresh()`の順に呼ぶ。ロジック自体に
  条件反転・null考慮漏れ等の欠陥は見当たらない。
- コード配線側の`LostFocus="DeviceNameBox_LostFocus"`ハンドラは`RedrawCanvas()`のみを行い
  （`MainWindow.xaml.cs:63`）、`SelectedCell`/`SelectedElement`を変更する副作用はない。

**推測（明示）**:
- WPFの`UpdateSourceTrigger=LostFocus`は`FrameworkElement.LostFocus`（論理フォーカスの喪失）に
  依存する実装であり、`Keyboard.LostKeyboardFocus`（実キーボードフォーカス）とは別のイベント系列。
  UI Automation経由の操作が、TextBoxの`Text`依存プロパティ自体は変更しても、WPFが期待する
  「フォーカスを得てから失う」という論理フォーカスのライフサイクル全体を正しく発火させていない
  可能性は理論上ある。
- ただし忍者の報告では「`Send-Ecad2Keys`はWPFへ正常に届く実装」「Tabキー明示でLostFocus発火
  （視覚的にメニューへのフォーカス移動を確認）」としており、フォーカス移動自体は起きている
  模様。実キー入力に近い操作でも再現している事実は、上記仮説（UI Automation固有の限界）だけ
  では説明しきれない可能性を示唆する。**この場からは断定できない。**

## 調査事項2: T-017検証時から今日までの、この経路への影響有無（git logでの裏取り）

**事実（確認済み）**:
- セッターのコアロジック（`DeviceRenamer.Rename`呼び出し・新規登録・`OnPropertyChanged`・
  `DeviceTable.Refresh`）は、T-017実装コミット`a58ed86`（2026-07-03 18:46、忍者実機検証で
  発覚した「機器表未反映」バグの初回修正）で確立され、以降**変更されていない**。
- `MainWindow.xaml`の`DeviceNameBox`定義（Text Binding + LostFocusハンドラ配線）も、
  同じくT-017実装コミット`1b4301e`（2026-07-03 18:28）で確立され、以降**変更されていない**。
- T-017完了後〜今日までにセッターへ加わった唯一の変更は、`d9aa49b`（2026-07-05、T-019）での
  `MarkDirty();`追加のみ。これは`IsDirty`フラグを立てるだけで、機器表反映（`OnPropertyChanged`/
  `DeviceTable.Refresh`）の経路には影響しない。
- 家老言及のT-021フォーカス制御変更（`bb6acfb`「ToolButtonLostKeyboardFocus撤去」）は、
  ツールバー3ボタン（選択/配置/自作パーツ）専用の別ハンドラの撤去であり、`DeviceNameBox`の
  Binding・LostFocusとは無関係（配線先が異なる）。
- `Window_PreviewKeyDown`／`Window_PreviewMouseLeftButtonDown`（Window全体のグローバル
  ハンドラ）にも、単純Tabキー押下や`DeviceNameBox`のフォーカス遷移に介入する処理、
  `SelectedCell`/`SelectedElement`をクリアする処理は見当たらない。

**結論**: git logで確認する限り、**該当する変更は見つからなかった**。T-017検証完了時点から
今日まで、この経路（Binding配線・セッターのコアロジック）はコード上一切変わっていない。

**不明点**: T-017検証当時の「忍者検証で検出された3件の修正後の再検証」（`todo.md` T-017行に
記載）が、具体的にどの操作方法（物理操作かUI Automationか）で行われたかを示す一次資料
（docs-notes配下）が見当たらない。当時の検証記録ファイルが現存しないため、今回のUI
Automation検証と同条件だったかどうかは比較できない。

## 調査事項3: 実バグならどこで切れているか

コード静的解析の範囲では、セッター自体のロジックに欠陥は見出せなかった。呼ばれれば正しく
機器表に反映されるはずである（ダイアログ経由の配置=観点1-5で同一の`DeviceTable.Refresh()`
機構が正常動作していることからも、Refresh機構自体は疑わしくない）。

したがって理論側の暫定結論（推測）は、**「セッター自体のロジックバグ」ではなく「UI
Automation操作でセッターが実際に呼ばれていない（Bindingのソース更新が発火していない）」
可能性が高い**、というもの。ただし断定はできない。

## 次の一手の提案（家老判断へ）

コード推論だけではこれ以上の切り分けが困難な段階と考える。過去に有効だった「診断ログ注入」
（`SelectedElementDeviceName`セッター先頭に一時ログを仕込み、UI Automation操作時に実際に
セッターへ到達しているかを忍者に実測してもらう）が、事実確認の近道になりうる。setterに
到達していれば実装バグ、到達していなければBinding/UI Automation側の問題、と一発で切り分く。

## 追補調査（殿実機再現後）：SelectedCell／SelectedElementの状態遷移側

家老指摘のとおり、UIA固有限界説は殿の物理操作でも再現したため否定された。残る筋
(a)setterは呼ばれるが実行時状態でスキップ／(b)物理操作でもLostFocus非発火の構造問題、に
ついて、`SelectedCell`/`SelectedElement`の状態遷移側を追補調査する。

### 事実（追加調査）

1. `SelectedCell`はT-016導入時（`GridPos?`）から一貫してnullable型。**「T-019でnullable化」
   という事実はSelectedCell自体には確認できなかった**（家老の指す対象は`CurrentSheet`
   （`Sheet?`、T-019で「Sheets=0濃紺スタート」導入時にnullableとして扱われるようになった
   構造）の可能性がある）。
2. `SelectedElement`は`SelectedCell is {} pos && CurrentSheet is Sheet sheet`の両方が
   非nullの場合のみ非null評価となる。`CurrentSheet`がnullになるのは`Document.Sheets.Count
   == 0`のときのみで、これはプロパティパネルに要素が表示されている（＝要素が存在する
   シートを開いている）状況とは両立しない。この経路での意図しないnull化は考えにくい。
3. `SelectedCell`への代入箇所は全6箇所を洗い出した：
   `LadderCanvasHost_PreviewMouseLeftButtonUp`（クリック位置）／Escキー処理層3（null）／
   `MoveSelectedCell`（矢印キー）／`ReplaceDocument`（null、新規/開く時）／
   `CurrentSheetIndex`のsetter（null、シート切替時）／`OutputPanelViewModel`（DRCジャンプ時）。
   **`DeviceNameBox`のクリックやTab移動それ自体が、これら6箇所のいずれの経路も通常操作では
   通らないことを確認した**（別のUI要素・別のトリガー条件のため）。
4. `LadderCanvas.cs`のクラスコメント（19-20行）に「要素単位の選択・編集フォーカス制御
   （`PreviewLostKeyboardFocus`のキャンセル等）は配置ツール機能の実装に合わせて将来追加する」
   とあるが、これは**未実装の将来コメントのみ**。キャンバス側の実装は
   `PreviewMouseLeftButtonDown += (_, _) => Focus();`だけで、`LostFocus`/`LostKeyboardFocus`
   関連の実装はキャンバス側に一切存在しない。

### 推測（明示）

- `CanvasArea`（ScrollViewer）が`FocusManager.IsFocusScope="True"`の独立FocusScopeという
  既知の構造（T-016の罠）が、`DeviceNameBox`（Windowの既定FocusScopeに属する）からの
  フォーカス遷移にも何らかの形で影響している可能性はゼロではないが、具体的な影響経路は
  コード解析だけでは特定できなかった。
- (a)(b)いずれの仮説も、コード上に「これが原因」と断定できる具体的経路は見つからなかった。
  これは「原因が無い」ことの証明ではなく、「静的解析の限界」である点に留意されたい。

### 現状（診断ログ版）

調査中、作業ツリーに侍による診断ログ版（未コミット）が既に仕込まれていることを確認した：
`SelectedCell`のsetter入口・値変化通知、`SelectedElementDeviceName`のsetter入口・各分岐
（`SelectedElement is null`スキップ／`oldName==newName`スキップ／Rename呼び出し／直接代入＋
新規登録／setter完了）、`DeviceNameBox`の`GotFocus`／`LostFocus`発火、をそれぞれ
`%TEMP%\ecad2-diag.log`へ記録する実装（`MainWindowViewModel.DiagLog`）。殿の実操作でこの
ログを取得すれば、GotFocus/LostFocus発火の有無・setter到達の有無・到達時の分岐先が
すべて記録され、(a)(b)を機械的に切り分けられる見込み。
