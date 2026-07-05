# T-036修正案 静的レビュー（隠密）

対象: コミット ab7cf7e（プロパティパネルのデバイス名編集がFocusScope跨ぎで反映されない
回帰の修正）+ 529092b（Escは入力を破棄してキャンセル）。code-review medium（8角度finder
→1-vote verify）を併用。

## 家老指定4観点への回答

| # | 観点 | 判定 |
|---|------|------|
| 1 | Explicit化の副作用（他Binding経路・初期表示・SelectedElement切替時のTarget更新） | 良好。UpdateSourceTrigger=Explicitはターゲット→ソース方向の更新タイミングのみを制御し、ソース→ターゲット方向（PropertyChanged発火時の自動反映）には影響しない。TwoWayの既定動作は維持されるため、選択セル切替時の表示更新に問題なし。他Bindingは無変更。 |
| 2 | Enter/LostKeyboardFocus二本立ての重複発火時の安全性 | 同値ガード(`oldName==newName`)で二重発火自体は安全。ただし**Enter確定はRedrawCanvas()を呼ばずキャンバス表示が更新されない欠陥を検出**（下記#2）。 |
| 3 | Esc処理の位置と順序の妥当性（層処理・FocusCanvasとの整合） | **欠陥あり**。DeviceNameBox編集中のEscは、UpdateTarget()による表示復元の直後に既存層3(選択解除)が無条件実行され、復元表示が即座に上書きされる（下記#1）。 |
| 4 | 空文字確定時の既存ガード通過 | 良好。setterのロジック自体は無変更のため、`newName.Length>0`ガードは従来通り機能する。 |

## 検出事項（verify済み、CONFIRMED）

### #1 Escapeが「編集破棄」と「要素選択解除」を同時に行い、復元表示を無意味化する

`MainWindow.xaml.cs:249-270`。DeviceNameBoxが表示・編集可能な状態は`HasSelectedElement==true`
が前提であり、これは必然的に`SelectedCell!=null`を含意する（`SelectedElement`の計算式より）。
つまりDeviceNameBox編集中は**常に**Window_PreviewKeyDownの層3条件(`SelectedCell is not null`)
を満たしている。

Escapeケースでは、249-250行のUpdateTarget()でTextBox表示を元のデバイス名へ戻した直後、
266-270行の層3が続けて実行され`SelectedCell=null`となる。このsetterは
`OnPropertyChanged(nameof(SelectedElementDeviceName))`を発火し、`SelectedElementDeviceName`
のgetterは`SelectedElement`がnullになったことで`""`を返す。TwoWayバインディングにより
TextBox.Textは直後に空文字へ再上書きされ、さらにプロパティパネル自体（HasSelectedElement
連動のStackPanel）もCollapsedになりDeviceNameBoxごと消える。

T-021裁定の「1回のEscで内側から1層だけ戻す」という設計方針に反し、編集中のEscは常に
「編集キャンセル＋選択解除」の2層分をまとめて実行してしまう。ユーザーが「入力だけ取り消して
選択は維持したい」と期待する操作感（GX Works3的な直感）と食い違う可能性が高い。

**UI/UX判断が必要**: これを「1層のみ」にするなら、DeviceNameBox編集中のEscでは
UpdateTarget()の後にreturnし、層2/層3判定へ進ませない、という対応が考えられる（要殿確認、
仕様判断であり隠密からは断定しない）。

### #2 Enter確定時、RedrawCanvas()が呼ばれずキャンバス表示が更新されない

`MainWindow.xaml.cs:74-78`（`DeviceNameBox_PreviewKeyDown`）。標準TextBox（`AcceptsReturn`
未設定・`IsDefault`ボタン無し）はEnterでフォーカス移動しないため、Enter押下後もフォーカスは
DeviceNameBoxに残留する。このハンドラは`UpdateSource()`のみを呼び、`RedrawCanvas()`を
呼んでいない（対照的に`DeviceNameBox_LostKeyboardFocus`は両方呼ぶ）。

`RedrawCanvas()`はキャンバス上のデバイス名ラベルを含む描画全体を`Draw()`で都度再構築する
唯一の経路（バインディングによる自動再描画ではない、コード内コメントで明言済み）であり、
`SelectedElementDeviceName`のsetterが発火する`PropertyChanged`（自身と
`SelectedElementKindDisplay`のみ）はキャンバス再描画をトリガーしない。

結果、コメント（72-73行）が謳う「Enterキーでの即時確定」に反し、Enter押下後もユーザーが
フォーカスを移動しない限りキャンバス上のラベルは旧デバイス名のまま表示され続ける。
**修正候補**: `DeviceNameBox_PreviewKeyDown`のEnter分岐に`RedrawCanvas()`を追加、または
`LostKeyboardFocus`ハンドラと共通処理化する。

## 検出事項（verify済み、CONFIRMED・ただし既存課題として報告）

### #3 グローバル操作（Ctrl+S/N/O・ウィンドウクローズ）が未確定編集を検知しない

DeviceNameBox編集中（未確定）にCtrl+S/Ctrl+N/Ctrl+O、またはウィンドウクローズ（×/Alt+F4）
を行うと、これらはいずれもDeviceNameBoxのフォーカスを移動させないため、`UpdateSource()`の
契機（LostKeyboardFocusまたはEnter）が発生しない。結果：
- Ctrl+S: 編集前の古いデバイス名のまま保存され、ユーザーは保存成功と誤認（サイレントな
  データ損失）。
- Ctrl+N/O・ウィンドウクローズ: `IsDirty=false`のため`ConfirmDiscardIfDirty()`が無確認で
  素通りし、編集中の入力が確認なく破棄される。

**ただし検証の結果、これは本diff(ab7cf7e〜529092b)による新規回帰ではなく、T-019のIsDirty
機構導入時点から存在する独立の既存課題と判明**（旧`UpdateSourceTrigger=LostFocus`も、
フォーカスが全く移動しないこのシナリオでは同様に発火しなかったはずのため）。本diffが
解決したのは「フォーカスは移動するがFocusScopeを跨ぐため発火しない」ケースのみで、
「フォーカスが全く移動しないグローバル操作」は対象外だった。T-036の趣旨（デバイス名編集の
確定漏れ全般）に照らすと、この既知の隣接課題が未解消のままである点は殿への報告・要検証
事項として扱う価値がある（今回のスコープに含めるかは家老・殿の判断）。

## 参考所見（cleanup、低優先度）

- `DeviceNameBox.GetBindingExpression(TextBox.TextProperty)`の取得パターンが3箇所
  （LostKeyboardFocus／PreviewKeyDown／Escapeケース）に重複。将来同種のExplicit確定/破棄
  パターンを他コントロールへ広げる際の温床になりうる。ヘルパーメソッド化の余地あり
  （PLAUSIBLE、直ちの対応は不要）。

## 隠密所見

要修正候補として家老の判断を仰ぐべきは**#1・#2**（いずれもCONFIRMED、本diff固有の欠陥）。
#1はUI/UX判断（1層原則を厳密適用するか）を伴うため殿確認が妥当、#2は`RedrawCanvas()`
追加という機械的な修正で解消可能。#3は既存課題であり本タスクのスコープ外の可能性が高いが、
T-036の趣旨との近さから報告に含めた。参考所見（重複コード）は経過観察で十分と判断する。
