# T-125 増分γ/δ事前調査・第2段：依存関係・分割可否・危険箇所（隠密）

> 2026-07-27 隠密調査。家老采配「T-125 γ/δの事前調査」を受けての第2段。2026-07-25の第1段
> （`docs/ecad2-t125-vm-clustering-survey-onmitsu.md`＝γ機能クラスタリング、
> `docs/ecad2-t125-view-clustering-survey-onmitsu.md`＝δ機能クラスタリング）が「未確認・次段候補」
> として残した3点——(1)各クラスタ間の実際の依存関係 (2)P-077ヒットテスト重複の実態確認
> (3)独立ViewModel化・分離の実現可能性——を埋めるのが本報の主眼。調査のみ、実装提案は案の
> 提示に留める。共有main上での一時注入は行わず、静的読解のみで実施した。

---

## 0. 前提の申し送り（気づき）

着手前に気づいたが、**T-125 γ/δの機能クラスタリング自体は2026-07-25に隠密の手で既に一度
行われていた**（上記2文書）。今回の家老采配ではこの2文書への言及が無く、DoDも「機能クラスタ
リング」からの再委任だったが、実際に着手してみると内容は重複ではなく、2文書が「次段候補」として
残した依存関係・P-077実態・分割可否の3点を埋める形に自然と収まった。**実害は無いが、今後
T-125関連の采配時は上記2文書もあわせて参照されたい**（落とし先＝`karo.md`の采配前チェック観点、
`memory: feedback_check_existing_survey_before_dispatch`と同型の事例）。

---

## 1. 着手前指標の再実測（DoD(4)）

| 指標 | 2026-07-25時点 | 2026-07-27時点（本調査） | 差分 |
|---|---|---|---|
| `MainWindowViewModel.cs` 行数 | 3395 | 3395 | 変化なし |
| 同ファイル メンバー数 | 283（public180/private103） | 287 | +4 |
| `MainWindow.xaml.cs` 行数 | 4132 | 4169 | +37 |
| 同ファイル `private void` 数 | 143 | 146 | +3 |
| 同ファイル `case`文数 | 49 | 49 | 変化なし |

**差分の主因＝T-128（「既定レイアウトに戻す」新設、2026-07-27 Done）**。δ側調査で
`RestoreFactoryDefaultDockingLayout`とそのメニュー項目3件の追加を確認した（詳細は3節）。
γ側の+4メンバーは未特定（本調査ではVM側の差分要因までは深追いしていない）。**いずれも
既存の機能群の輪郭を崩す規模の変化ではない**——2日間の増分実装が定常的に食い込んでいるだけで、
指標としての傾向（肥大化継続）に変わりはないと判断する。

---

## 2. γ：MainWindowViewModel.cs 依存関係・分割可否

### 2.1 既存切り出し先例の実態（重要な前提訂正）

`SheetNavigationViewModel`/`FindViewModel`/`OutputPanelViewModel`/`DeviceTableViewModel`は
いずれも「独立ViewModel」と呼ばれてはいるが、実態は疎結合ではない。コンストラクタで
`MainWindowViewModel owner`を保持し、`_owner.Document`/`_owner.CurrentSheetIndex`/
`_owner.SelectedCell`/`_owner.MarkDirty()`/`_owner.UndoManager`等へ頻繁に呼び返す**「オーナー
コールバック方式」**。すなわちこのコードベースにおける「分割」は責務の物理的な移動であり、
依存自体の切断ではない。**γの増分計画もこの前提に立つのが安全**（既存パターンからの逸脱は
新たなリスクを持ち込む）。

### 2.2 View層との結合度（実測）

- `MainWindow.xaml.cs`内 `_viewModel.` 参照：**443箇所**
- `MainWindow.xaml`内 `Binding`/`Path=`：**202箇所**
- `MainWindow.xaml.cs:815-831`の`PropertyChanged`ディスパッチのみで`CurrentSheet`/
  `SelectedCell`/`SelectedConnector`/`ConnectorDraftPreview`等**12種**を`nameof()`で直接参照

XAMLバインドはフラットなプロパティ名を直接参照するため、メンバーを子VMへ移すと該当パスすべての
書き換えが要る。**WPFバインド失敗はコンパイルエラーにならず実行時に静かに失敗する**——本ファイル
のコメント内には「隠密レビュー指摘」「忍者実機検証で発覚」形式の通知漏れ・クリア漏れの実例が
数十件記録されており、この種の欠陥がこのクラスで繰り返し実際に起きてきた過去がある。**分割時も
同種の欠陥が再発しうる、という点を最大のリスクとして先に明記する。**

### 2.3 機能群間の依存関係（具体名）

- **SelectedCellのsetter（423-463行）→ 他6種のSelected\*・全ドラフト**：`SelectedConnector=null`
  他11呼び出しで無条件クリア。コメント自身が「このsetter自身を『選択状態をクリアする唯一の入口』
  にする」（423行）と明記する**意図的な一極集中**。
- **Mode setter → ドラッグ・記入ドラフトの一部＋テストセッション**：`_testSessions.Clear()`
  他4クリア呼び出し（96-115行）。
- **ReplaceDocument（3091-3193行）→ 全機能群横断**：Selected\*全クリア、全ドラフトクリア、
  4子ViewModelの再構築、`UndoManager.Clear`。「文書破棄操作の入口を分散させない」という設計
  意図がコメントに明記（3074-3076行、GuiEcadの反省を踏まえた設計と読める）。
- **ApplyUndoRedoSnapshot（3351-3393行）→ コア状態＋子ViewModel**：4子VMの再構築＋
  `SetCurrentSheetIndexCore`。
- **要素プロパティパネル ↔ 配置検証・DeviceClass解決**：`IsSelectedElementSelectSwitch`等が
  `ResolveDeviceClass`を共有呼び出し。
- **HasAnyDraft（1956-1961行）**：5種のドラフトフィールドを串刺し参照するプロパティ。行操作の
  CanExecuteおよび`FindViewModel`（既存独立VM側）からも参照される——**既存の子VMも本体の内部
  状態に依存**しており、切り離しの参考例としては「疎結合の証明」にはならない。

### 2.4 独立ViewModel化の候補（推奨度つき）

| 候補 | 推奨度 | 理由 |
|---|---|---|
| DeviceClass解決（`ResolveDeviceClass`/`MapToDeviceClass`）の**サービス抽出** | **高** | PartPalette等のみに依存する準純粋関数。XAML直接バインド対象でないためView破壊リスクがほぼゼロ |
| 行操作/シート設定コマンド群の子VM化 | 中 | スコープ明確、既存`SheetNavigationViewModel`と同型で技術的見込みは立つ。XAMLの`Command=`直結の可能性は要確認（未確認・推測） |
| OR合流先確認（Confirm/Cancel/MoveOrJoinTargetCandidate）の子VM化 | 中 | コードビハインドからの直接呼び出しと推測（未確認）——コンパイラ検知が効く分XAMLより安全 |
| テストモード（`_testSessions`/`TestModePress`等）の子VM化 | 中 | 専用性が高く他クラスタとの共有フィールドが少ない |
| 要素プロパティパネル（SelectedElement系22メンバー）の丸ごと切り出し | **低（見送り推奨）** | XAML直接バインド対象の疑いが強い（未確認・推測）、費用対効果と実行時サイレント破損リスクのバランスが悪い |
| **選択・ドラッグ状態機械7種（全体の約48%）** | **単純移動は不可、要再設計** | 下記2.5参照 |
| コア状態／ファイルI/O・文書ライフサイクル／子VM参照束 | 据え置き推奨 | 切り出すと「もう一つのGod Object」を作るだけで責務分割にならない |

### 2.5 【最重要】選択・ドラッグ状態機械7種は「単純な移動」では済まない

行数では最大（全体の約48%）で分割効果も最大に見えるが、**SelectedCellのsetterが7種すべてを
無条件で串刺しクリアする設計**（2.3節）がある限り、どこにSelectedCellを置いても他6種の子VMへの
参照（またはコールバック機構）を持たざるを得ない。単純にメンバーをコピーしてオーナー参照化する
だけなら機械的に可能だが、それは**このクラスが繰り返し踏んできた「クリアし忘れ」という不具合
パターン**（T-041/T-064/T-067/T-088の各所に隠密レビュー指摘の実例が残る）を再度招き込む構造
そのものである。

**分割するなら「SelectedCellが具象7種を個別に知る」現行方式を「クリア可能な選択スロットの集合を
SelectedCellが走査する」ような抽象化に置き換える設計変更が前提になる。これは事前調査の範囲を
超え、実装方針の分岐そのものであるため、増分計画時に殿へ確認すべき論点と考える**（要求解釈上、
勝手に方針を決めず確認する事項——`memory: feedback_route_design_decisions_to_user`と同型）。

### 2.6 分割困難な塊（コード根拠）

1. `MainWindowViewModel.cs:423-463`（SelectedCellのsetter）——2.5節
2. `MainWindowViewModel.cs:3091-3193`（ReplaceDocument）——全機能群を横断的に触る「全部を知る」役
3. `MainWindowViewModel.cs:1230-1421` + `1702-1724` + `1726-1854`（ImageInsert関連が**3箇所に
   物理分断**）——ファイル内配置自体が機能群として整理されていない証拠。機械的grepのみに頼ると
   見落としうる
4. `MainWindow.xaml.cs:815-831` + XAMLバインド202箇所——子VM化とセットでの書き換えが要り、
   実行時サイレント破損の性質上、忍者の実機確認が必須
5. `MainWindowViewModel.cs:1956-1961`（HasAnyDraft）——5フィールド串刺し参照、ドラフト系を
   複数子VMへ分散させる場合は「各子VMがIsDraftingを公開し本体がOR結合する」形への再設計が要る

---

## 3. δ：MainWindow.xaml.cs 依存関係・P-077実態・分割可否

### 3.1 現時点のクラスタ一覧（前回19クラスタ→21クラスタ、行番号は4169行版）

前回調査（19クラスタ）から輪郭は不変、T-128差分（`RestoreFactoryDefaultDockingLayout`関連）を
Aクラスタへ吸収した上でU（右クリックコンテキストメニュー構築）を独立クラスタとして切り出した。
詳細な行範囲・代表メンバーは付録（本報告末尾）参照。特に重要な2クラスタ：

- **クラスタL（グローバルキーボード、`Window_PreviewKeyDown`単体で607行）**：前回同様の最大異常値
- **クラスタK（キャンバスマウス、8プリミティブ×4メソッドの横断的関心事）**：P-077の主戦場

### 3.2 機能群間の依存関係（具体名、前回未確認だった点）

- **クラスタC（ViewModel連携）→ クラスタK（キャンバスマウス）：強結合、切り離し困難**
  `ViewModel_PropertyChanged`（813-946行）が、ForceCancel系プロパティ変更時に**クラスタK専有の
  はずの**フィールド（`_connectorDragStarted`等、8組16フィールド）を直接書き換え、
  `LadderCanvasHost.ReleaseMouseCapture()`まで呼ぶ（884-945行）。マウスキャプチャの後始末という
  「Kの責務」がCのイベントハンドラ内に実装されている。
- **クラスタL（キーボード）↔ クラスタK（マウス）の共有フィールド**：`_testModePressedDevice`/
  `_testModeEnterPressedDevice`をマウス側・キーボード側の両方が読み書き（テストモードのモーメン
  タリ操作を両経路で扱うための意図的共有）。
- **クラスタL → クラスタQ・R（行コメント/枠ラベルエディタ）：制御結合**
  `Window_PreviewKeyDown`冒頭で編集中フィールドを見て即return——3クラスタがフィールド越しに結合。
- **中心的ゲートウェイメソッド群**：`CommitDeviceNameEdit()`（クラスタD所有、I/K/L/Uから計10箇所
  以上呼ばれる「未確定編集を確定してから状態を動かす」唯一の関門、P-071再発防止の要）、
  `FocusCanvas()`（L/N/P/Q/R/Jから広く呼ばれるフォーカス制御の集約点）、`TryPlaceActiveTool()`
  （K・L双方が収束する意図的な共有経路、T-021由来）。**これらの「単一の関門」性質を壊さない
  ことが分割時の最優先制約**。

### 3.3 P-077（ヒットテスト優先順位重複）の実態確認【最重要の新規判明事項】

3つの独立したヒットテスト連鎖が存在し、**順序も対象種別も三者三様**であることを実測確認した。

1. **ドラッグ継続判定**（`PreviewMouseLeftButtonDown`内）：Connector→WireBreak→FreeLine→
   ConnectionDot→Image resize→Image本体→Element→Frameの8段
2. **新規選択判定**（`PreviewMouseLeftButtonUp`内）：Connector→Frame→WireBreak→ConnectionDot→
   FreeLine→Imageの6段。**Elementのヒットテストがこの連鎖に存在しない**——`SelectedCell`を
   設定するのみで、`SelectedElement`はViewModel側の派生プロパティ（`MainWindowViewModel.cs:
   2056-2058`、`SelectedCell is pos && sheet.Elements.FirstOrDefault(el => el.Pos == pos)`）が
   単純位置一致で解決する
3. **右クリックメニュー判定**（`PreviewMouseRightButtonDown`内）：Element→Connector→Frame→Image
   の4段、該当なしは行操作メニューへフォールバック。**WireBreak/FreeLine/ConnectionDotの
   右クリックメニューはそもそも存在しない**

**重複の程度は「単なるコピペ」ではなく機能的な非対称にまで及ぶ**。コード自身のコメント
（2085-2096行）が明記する通り、`HitTestElement`（右クリック・テストモードで使用）は複数セル幅
要素の非アンカーセルも占有範囲判定で検出するのに対し、`SelectedElement`（左クリックの選択解決）
は単純位置一致のみ——**複数セル幅の要素を非アンカー位置で左クリックしても選択できず
プロパティパネルが開かない一方、同じ位置を右クリックすればメニューは開く**という実際の挙動差が
生じている。この非対称は2026-07-13（P-077起票）から現在まで未解消（`docs/todo-archive.md`
1113-1128行・2610-2639行参照、T-064で起票・T-067で「2件目」確認済みだが対処は都度「意識する」
に留まり構造的共通化は未着手）。

**γ節2.6項3との接続**：`SelectedElement`はγ側でいう「要素プロパティパネル」機能群の起点に
あたる。P-077の非対称解消は**単にδ側3メソッドを整理するだけでは済まず、γ側のSelectedElement
解決ロジックにも手を入れる必要がある**——γ/δ両方にまたがる論点である。

### 3.4 分離しやすい候補

| 候補 | 推奨度 | 理由 |
|---|---|---|
| シートD&Dクラスタ（4044-4169行） | **高** | 専有フィールド4個が他と無共有、`CalculateSheetDropIndex`は既にinternal static純粋関数化済み |
| 行コメント/枠ラベルエディタの共通化 | 中 | 7点が構造的に完全同型（`int?`と`GroupFrame?`の型差のみ）、統合してから移動する候補 |
| 自作パーツメニュー | 中 | 共有フィールド無し |
| 矢印キー移動ヘルパー | 中 | 個々がVMへの薄い委譲、呼び出し元がグローバルキーボードのswitch内という点のみが障害 |

### 3.5 分割困難な塊（コード根拠）

1. `Window_PreviewKeyDown`（607行）——Escapeケース単体191行が「1回のEscは1層だけ戻す」設計原則
   を条件分岐順序そのものに埋め込む。分割すると層の順序保証を別途仕組み化する必要
2. キャンバスマウス4メソッド（8プリミティブ×Down/Move/Up/LostCapture）——プリミティブ追加時に
   4メソッドすべてへの横展開が必要な横断的関心事構造（T-041/T-064/T-067/T-088で実際に4回発生）
3. `ViewModel_PropertyChanged`（813-946行）——3.2節のC→K結合
4. `_toolButtonKeyboardClickSource`まわり——N・Windowレベルのマウスフックが1フィールドで協調する
   状態機械、部分切り出し不可

### 3.6 View層特有の危険箇所（実機確認必須）

AvalonDock関連全般（既知の罠が集積）／独立FocusScope越えのフォーカス制御／`Key.System`経由の
システムキー（F10・Alt+Up/Down、`WM_SYSKEYDOWN`特殊扱い）／`UpdateSourceTrigger=Explicit`編集欄
の確定漏れ（P-013既知パターン）／ダブルクリック判定のDown/Up非対称（T-080既知、`ClickCount`は
Up側で常に1固定というWPF仕様）／`Dispatcher.BeginInvoke`による遅延フォーカス・再描画（タイミング
依存で単体テスト不可）。

---

## 4. γ/δ統合所見

1. **C→K結合とSelectedCellの「唯一の入口」設計は表裏一体**——γのSelectedCellクリア一極集中と
   δの`ViewModel_PropertyChanged`によるKフィールド直接操作は、同一の設計思想（「状態遷移の
   後始末を1箇所に集約する」）の異なる面である。分割時はこの設計思想自体を継承する前提で
   進めるべきで、γ・δを別々に分割すると集約点が失われる恐れがある。
2. **P-077はγ・δ両方にまたがる**（3.3節）。増分β（P-077対応、侍実装中）の範囲設計時に、
   δ側のヒットテスト連鎖整理だけでなくγ側のSelectedElement解決ロジックの扱いも合わせて
   検討する必要がある——**これは増分βの担当範囲を広げうる新規判明事項であり、家老・侍への
   申し送りが要ると考える**。
3. **中心的ゲートウェイメソッド（`CommitDeviceNameEdit`/`FocusCanvas`/`TryPlaceActiveTool`/
   `HasAnyDraft`/`ReplaceDocument`）は、既知バグ（P-071/P-013等）の再発防止機構でもある**。
   分割時にこれらの「単一の関門」性質を壊さないことが、責務分離と同等かそれ以上に重要な制約。
4. **既存の子ViewModel切り出し例（`SheetNavigationViewModel`等）はいずれも「オーナーコールバック
   方式」の密結合**であり、疎結合の前例にはならない。γ/δの分割設計も、依存自体を切断するので
   はなく責務の物理的な移動として計画するのが、既存パターンとの整合性の観点から安全と考える。

---

## 5. 結論（DoDへの回答）

- **(1) 機能クラスタリング**：γ＝9機能群（第1段18クラスタを再集約）、δ＝21機能群（前回19＋
  T-128差分吸収）で分類完了。詳細は2.1〜2.3・3.1節および付録。
- **(2) 責務境界の再定義案**：γは「DeviceClass解決の高推奨サービス抽出」を筆頭に4段階の推奨度
  で提示（2.4節）、δは「シートD&D分離」を筆頭に4候補提示（3.4節）。
- **(3) 分割の実現可能性と危険箇所**：γは選択・ドラッグ状態機械7種（全体48%）が単純移動不可・
  要再設計と判明（2.5節）、δはグローバルキーボード607行・キャンバスマウス4メソッドが最大の
  分割困難箇所（3.5節）。両者を貫く制約として中心的ゲートウェイメソッド群の関門性維持がある
  （4節）。
- **(4) 着手前指標の再実測**：1節参照、傾向に変化なし（T-128分の定常増分のみ）。

**総じて、γ/δ着手（増分α/β完了後の再評価対象）に進む場合、単なる「メンバーの物理的移動」で
済む部分（DeviceClass抽出・シートD&D分離等）と、設計変更を伴う部分（選択状態機械7種・
P-077のSelectedElement解決）とを明確に区別して増分計画を立てる必要がある**、というのが本調査の
中心的な結論である。後者は事前調査の範囲を超える実装方針の分岐であり、増分計画時に殿へ諮る
べき論点と考える。

---

## 出典・参照

- `docs/ecad2-t125-vm-clustering-survey-onmitsu.md`（γ第1段、2026-07-25）
- `docs/ecad2-t125-view-clustering-survey-onmitsu.md`（δ第1段、2026-07-25）
- `docs/ecad2-t125-app-layer-structure-survey-onmitsu.md`（T-125第1報）
- `docs/todo.md`（T-125節、323行〜、効果測定指標のDoD）
- `docs/todo-archive.md`（P-077起票経緯 1113-1128行、T-067着手前チェック 2610-2639行）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（現状3395行、全287メンバー実測）
- `src/Ecad2.App/MainWindow.xaml.cs`（現状4169行、全146ハンドラ実測）
- `src/Ecad2.App/MainWindow.xaml`（Binding/Path 202箇所実測）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`ほか既存子ViewModel群（全文読了、
  オーナーコールバック方式の確認）
