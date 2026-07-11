# T-047範囲内欠陥 修正テスト設計（隠密）

> 2026-07-09 隠密起草。制度＝テスト設計と実装の分離（`onmitsu.md`）を適用（修正往復1周目）。
> 対象コミット`4ecae77`（main、T-047手動配線系ボタン新設）。必読資料：
> `docs/archive/ecad2-t047-review-onmitsu2.md`所見1（隠密2）、`docs/archive/ecad2-t047-ninja-verification.md`
> （忍者、観点4）、`docs/archive/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`（懸念4原設計）。
> 静的検討のみ・共有main上への一時注入検証は行っていない（`feedback_no_live_injection_on_
> shared_main`の家老裁定どおり）。

---

## 0. バグの同根性の確認（事実）

家老采配にある2件（所見1＝Tab+Enter後の矢印/Enter無反応、新規重大＝矢印キーがツールバー
ナビゲーションに奪われ隣接ボタンを誤起動しデータ混入）は、**単一の根本原因**から生じる：

- `Window_PreviewKeyDown`（`MainWindow.xaml.cs:759`他）の矢印キー/Enter確定処理は
  いずれも`IsCanvasFocused()`（871行目、`Keyboard.FocusedElement`がキャンバス配下かを判定）
  をガード条件に持つ。
- 新設5ボタンはすべて既存`ConsumeToolButtonFocusRestore(sender)`（1111-1119行目）を呼ぶ。
  この共通メソッドは「キーボード起因（Tab+Enter/Space）ならボタン自身にフォーカスを残し、
  マウス起因ならキャンバスへ戻す」という判定を行う（T-021増分vi・懸念4対応ポリシー）。
- **自由線(横線)記入・自由線(縦線)記入・縦分岐線記入の3ボタン**（`FreeLineHorizontalButton_
  Click`/`FreeLineVerticalButton_Click`/`VerticalConnectorButton_Click`、1301-1317行目）は、
  呼び出し先（`TryBeginFreeLineDraft`/`TryBeginConnectorDraft`）が`Tool.Mode`を
  `PlaceLine`/`PlaceConnector`（記入中状態）へ変更し、**後続の矢印キー調整・Enter確定が
  必須**（`Window_PreviewKeyDown`765-768行目・836-853行目）という設計。
- Tab+Enterでキーボード起因と判定されフォーカスがボタンに残ると、
  `IsCanvasFocused()`が偽になり、矢印キー/Enterの処理が丸ごとスキップされる
  （所見1＝無反応）。**さらに**、スキップされた矢印キーイベントは`e.Handled`が
  立たないままWPF既定のツールバー方向ナビゲーションへ流れ、フォーカスが隣接ボタン
  （例：「接続点記入」）へ移動する。その状態でEnterを押すと隣接ボタンが誤起動され、
  即時確定型（接続点・配線分断）ならデータが黙って確定配置される（新規重大所見）。
- **対照的に、接続点記入・配線分断記入の2ボタン**（`ConnectionDotButton_Click`/
  `WireBreakButton_Click`、1319-1329行目）の呼び出し先（`TryPlaceConnectionDot`/
  `TryPlaceWireBreak`）は`Tool.Mode`を変更しない即時確定型（1259-1281・1192-1213行目を
  確認、記入中状態への遷移なし）。後続のキー操作が一切不要なため、フォーカスがボタンに
  残っても機能上の支障がない（隠密2所見1もこの2ボタンを対象外としている）。

**結論**：2件は同一原因（記入中状態＝`Tool.Mode`が`PlaceLine`/`PlaceConnector`である間、
フォーカスがボタンに残ると矢印キー処理の入口である`IsCanvasFocused()`が満たされないこと）
から生じる1つの欠陥である。

---

## 1. 修正方針の技術検討

### 1-1. 提案（隠密2・忍者提示の候補案の検証）

候補案＝「新設3ボタンのみ`ConsumeToolButtonFocusRestore`を使わず常時`FocusCanvas()`を呼ぶ
専用ポリシーとする」。この方針は**妥当**と判断する。理由：

1. `FocusCanvas()`（1131-1136行目）を記入開始直後に同期呼び出しすれば、ユーザーの次の
   キー入力（矢印キー）が来る時点で既に`IsCanvasFocused()`が真になっている。よって
   (a)矢印キー/Enterが正しく`AdjustFreeLineDraft`/`AdjustConnectorDraft`/確定処理へ
   ディスパッチされ（所見1解消）、(b)`e.Handled=true`が立つため矢印キーがツールバーの
   既定ナビゲーションへ流れることも無くなる（新規重大所見も**同一修正で同時に解消**、
   忍者検証記録の末尾推測と一致）。
2. 接続点記入・配線分断記入（即時確定型）は現状維持でよい（0節の理由により実害なし、
   既存の8ボタン＝T-021懸念4対応ポリシーと同型のままが自然）。

### 1-2. 実装粒度についての設計上の推奨（侍の技術選択に委ねる、指示ではない）

候補案は「ボタン単位（sender参照）で分岐する」実装（例：3メソッドだけ`FocusCanvas()`直呼び、
他2メソッドは従来どおり）でも成立するが、**「アクション後の`Tool.Mode`が`PlaceLine`または
`PlaceConnector`かどうか」で分岐する**実装の方がより本質的な条件を捉えると考える（推奨、
必須ではない）：

```csharp
// 例（侍の実装を拘束するものではない、設計意図の参考）
private void ConsumeToolButtonFocusRestore(object sender)
{
    bool isKeyboardOrigin = ReferenceEquals(_toolButtonKeyboardClickSource, sender);
    bool requiresCanvasContinuation =
        _viewModel.Tool.Mode is ViewModels.ToolMode.PlaceConnector or ViewModels.ToolMode.PlaceLine;
    if (isKeyboardOrigin && !requiresCanvasContinuation)
        (sender as UIElement)?.Focus();
    else
        FocusCanvas();
    _toolButtonKeyboardClickSource = null;
}
```

**この設計を推奨する理由**：
- 「どのボタンか」ではなく「結果としてどの状態に入ったか」で分岐するため、将来6つ目の
  手動配線ボタン（記入中状態を持つ種別）が追加されても、この条件だけで自動的に正しく
  分類される（ボタンごとの個別対応漏れを構造的に防ぐ、T-041増分7の「1種直すと別種で漏れる」
  教訓＝`docs-notes/handover-next-session.md`3節と同種のリスクを事前に潰す）。
- 既存8ボタン（SelectDefault/6×BuiltinPlace/OpenPartSelection）は、実行後の`Tool.Mode`が
  常に`Select`または`PlaceElement`であり、`PlaceConnector`/`PlaceLine`には決してならない
  （`ActivateSelectDefault`/`ActivateBuiltinTool`/`ActivateOpenPartSelection`の実装を
  確認済み、1041-1046・1059-1066・1286-1289行目）。よって新条件は既存8ボタンの挙動に
  一切影響しない＝**懸念4の非再発を構造的に保証できる**（下記1-3で詳述）。
- 接続点記入・配線分断記入も実行後`Tool.Mode`は変化しない（0節で確認済み）ため、
  この条件では「常時FocusCanvas」対象に含まれず、現状維持のまま自然に扱われる。

侍がボタン単位の分岐（3メソッド直書き）を選んでも観察可能な外部挙動は同一になるため、
**どちらの実装粒度でも本設計のDoDを満たす**。ただし後者を選ぶ場合、3箇所への変更適用漏れが
無いことを実装後にセルフチェックされたい。

### 1-3. 懸念4（T-021、選択ツールのTab迷子防止）の非再発の整合確認

`docs/archive/ecad2-t021-focus-design-consolidation-plan-onmitsu.md`根拠3のトレース表（68-88行目）
に、本修正で追加される3行を合成すると以下のようになる：

| 経路 | 修正後の挙動 | 懸念4への影響 |
|---|---|---|
| 選択ツールボタン・キーボード | `FocusCanvas()`を呼ばない（維持） | 影響なし（既存どおりツールバー内ナビ継続） |
| a接点等配置ボタン・キーボード | `FocusCanvas()`を呼ばない（維持） | 影響なし |
| 自作パーツボタン・キーボード | `FocusCanvas()`を呼ばない（維持） | 影響なし |
| 接続点記入・配線分断記入・キーボード | `FocusCanvas()`を呼ばない（維持） | 影響なし（新規、即時確定ゆえ懸念4と同種のリスクは無い＝ボタン単体で完結する操作だから） |
| **自由線(横線/縦線)記入・縦分岐線記入・キーボード** | **`FocusCanvas()`を常時呼ぶ（新規ポリシー）** | **意図的な逸脱。理由＝この3ボタンの「次の一手」は必ずキャンバス側のキー操作であり、「ツールバー内ナビゲーションを継続したい」という懸念4が想定した利用シーンがそもそも存在しない** |

懸念4が保護しようとしたのは「ツール選択後もツールバーを見て回りたい／別のツールに乗り換え
たい、という自然な操作を、フォーカス強制移動で妨げない」というシナリオである
（`docs-notes/...focus-design-consolidation-plan`3-4節参照）。記入中状態（PlaceLine/
PlaceConnector）に限っては、次に取るべき行動は矢印キー調整とEnter確定以外にありえず
（マウスによる代替完了経路も無い、隠密2所見1で確認済み）、「ツールバーに留まりたい」という
利用シーンは原理的に発生しない。したがって、この3ボタンに限定した逸脱は懸念4の意図に
矛盾しない。

---

## 2. テスト設計

### 2-1. ヘッドレステストで担保できる範囲と、担保できない範囲の切り分け（事実確認）

以下を実際にgrepで確認した：

- `tests/Ecad2.App.Tests`配下に`ConsumeToolButtonFocusRestore`/`FocusCanvas`/
  `ToolButtonPreviewKeyDown`/`_toolButtonKeyboardClickSource`を参照するテストは
  **1件も存在しない**（新規調査、grep実施）。T-021増分vi（3周の往復修正を経た機能）
  ですら、このフォーカス復帰メカニズム自体をヘッドレスUnitTestで検証した実績が無い
  （全て忍者の実機・UI Automation検証で担保されてきた）。
- `tests/Ecad2.App.Tests.csproj`に`STAThread`/`ApartmentState`の設定は無く、
  `MainWindow`（`Window`派生・DependencyObject）を直接インスタンス化して`Keyboard.Focus()`/
  `Keyboard.FocusedElement`を検証するテストも1件も存在しない（既存テストは全て
  `MainWindowViewModel`等のViewModel層POCOのみを対象）。
- T-042（「App層テストの実環境副作用解消」）は、App層テストから実環境依存の副作用を
  **除去する方向**の既往決定であり、今回新たに「実Window＋実HWNDでの実フォーカス検証」
  という重い環境依存をヘッドレステストへ持ち込むことは、この既定路線と逆行する
  （KISS・スコープ規律の観点からも非推奨）。

**結論（karo指定の切り分け）**：

- **ヘッドレステストで担保できない範囲**：実際のキーボードフォーカスが「キャンバスに
  移ったか」「ボタンに残ったか」そのものの検証。これはWPFの実HWND・実フォーカス管理が
  必要で、本プロジェクトの現行テスト基盤（STA/Window未対応）では検証できない。
  **忍者実機観点（2-3節）で担保する。**
- **ヘッドレステストで担保できる可能性がある範囲**：1-2節で提案した「実装粒度の推奨」
  （`Tool.Mode`ベースの条件分岐）を侍が採用した場合に限り、その**条件判定ロジック自体**
  （フォーカス操作を伴わない純粋な分岐条件）は、既存のRoutedEventArgs等を介さず
  `_viewModel.Tool.Mode`の値だけで完結するため、ユニットテスト可能な形に切り出せる
  可能性がある。ただし、侍がボタン単位の分岐（1-2節「どちらの実装粒度でも可」）を選んだ
  場合はこの限りではない。**このヘッドレステストは任意（あれば良いが必須ではない）**とし、
  DoDの必須要件にはしない（実装粒度は侍の技術選択事項のため、隠密が特定の実装形を強制する
  ことは要求解釈の範囲外）。

### 2-2.（任意）ヘッドレステスト設計 — Tool.Modeベース実装を選んだ場合

侍が1-2節の推奨実装（`Tool.Mode`による分岐）を採用した場合、以下の**同値分割**でユニット
テストを設計できる：

| 入力（`_viewModel.Tool.Mode`） | 期待される`requiresCanvasContinuation`相当の判定 | 分類 |
|---|---|---|
| `ToolMode.PlaceConnector` | true（常時FocusCanvas対象） | 有効同値クラス1 |
| `ToolMode.PlaceLine` | true（常時FocusCanvas対象） | 有効同値クラス2 |
| `ToolMode.Select` | false（既存ポリシー維持） | 無効同値クラス1 |
| `ToolMode.PlaceElement` | false（既存ポリシー維持） | 無効同値クラス2（既存8ボタンの実行後状態） |
| `ToolMode.PlaceFrame`／`PlaceDot`／`PlaceWireBreak` | false（未配線のモード、将来の保険） | 無効同値クラス3 |

xUnitの`[Theory]`+`[InlineData]`で全7値（`ToolMode`enum全列挙、`ToolState.cs:12`参照）を
網羅し、各値についてこの条件判定メソッド（侍が`internal`化するか、reflection経由で
検証するかは実装選択）が期待どおりの真偽を返すことをアサートする。これはフォーカス操作を
一切呼ばない純粋な条件判定のテストであり、実HWND無しで完結する。

**RED証明の形（この任意テストを実施する場合）**：修正前のコード（`ConsumeToolButtonFocusRestore`
が`isKeyboardOrigin`のみで分岐する現行版）に対してこのテストを書けば、`PlaceConnector`/
`PlaceLine`の場合に「常にtrue」を期待するアサーションが（現行コードにはこの概念が存在しない
ため）コンパイルエラーまたは的外れになる。実務的には「修正後のコードに対してテストを書き、
修正前のコードに一時的に戻して失敗することを確認する」のではなく、**「この条件判定メソッドが
存在しない現行コードでは、そもそもこのテストの対象コードパスが存在しない」こと自体がRED
相当**と整理してよい（新設ロジックのテストのため、既存コードへの回帰RED証明とは性質が異なる）。

### 2-3. 忍者実機観点で担保する範囲【必須】

フォーカスの実際の移動先はUI Automation（`AutomationElement.FocusedElement`、忍者が
`ecad2-ui-automation`スキルで既に使用中の手法、`docs/archive/ecad2-t047-ninja-verification.md`
58-70行目で実績あり）で検証する。以下、**状態遷移**と**対称性**の技法を適用した観点表。

#### 状態遷移表（記入中3ボタン共通のモデル）

各ボタンについて、以下の遷移が正しく起きることを確認する：

```
[Idle] --Tab+Enterで起動--> [記入中・フォーカス=?]
[記入中・フォーカス=?] --矢印キー押下--> [プレビュー範囲変化=?, フォーカス=?]
[プレビュー範囲変化後] --Enter--> [確定・データ設置=?]
```

| 状態遷移 | 修正前（RED、忍者実機記録済み） | 修正後の期待値（GREEN） |
|---|---|---|
| Tab+Enterで起動直後のフォーカス | ボタン上に残留（`t047-finding1-focus-stuck-connector-ninja.png`で実測済み＝既存RED証明として流用可） | `LadderCanvasHost`（キャンバス）に移動 |
| 起動直後の矢印キー押下 | 無反応（プレビュー変化なし）、または隣接ボタンへフォーカス移動（新規重大所見） | プレビュー範囲が変化する（`AdjustFreeLineDraft`/`AdjustConnectorDraft`が発火）、フォーカスは移動しない |
| プレビュー調整後のEnter | 無反応（フォーカスがボタン上のため`IsCanvasFocused()`不成立） | 記入が確定し、`Tool.Mode`が記入前の状態（`Select`等）へ戻る |
| （新規重大所見の再現手順）矢印キー押下後の状態 | フォーカスが隣接ボタンへ移動しうる（`t047-newfinding-arrowkey-hijacked-ninja.png`） | フォーカスはキャンバスに留まり続ける（隣接ボタンへの移動が起きない） |

#### 対称性表（5ボタン×起動経路の網羅、T-041増分7の教訓を踏まえ全種を明記）

忍者実機記録（`archive/ecad2-t047-ninja-verification.md`4-a/4-cで確認済みなのは「縦分岐線記入」
（上下キー）と「自由線(横線)記入」（左右キー）の2件のみ。**残る「自由線(縦線)記入」
（上下キー）は未検証**（同記録「不明点」に明記済み）ため、以下の対称表で全種を漏れなく
確認されたい：

| ボタン | 調整キー方向 | Tab+Enter起動→フォーカス | 調整キー→反応 | 調整キー→フォーカス残留(regression) | Enter→確定 |
|---|---|---|---|---|---|
| 自由線(横線)記入 F9 | 左右 | 要確認（修正後=キャンバス） | 要確認 | 要確認（4-c再検証） | 要確認 |
| 自由線(縦線)記入 sF9 | 上下 | **要確認・既存記録に無し** | **要確認・既存記録に無し** | **要確認・既存記録に無し** | **要確認・既存記録に無し** |
| 縦分岐線記入 sF9 | 上下 | 要確認（4-a既存再現あり、GREEN化を再確認） | 要確認 | 要確認 | 要確認 |
| 接続点記入 F10（即時確定・回帰確認のみ） | — | 変更なし（ボタン上残留のままでよい） | 該当なし | 該当なし | Tab+Enterで即時確定される（現状維持の確認） |
| 配線分断記入 F10（即時確定・回帰確認のみ） | — | 変更なし（ボタン上残留のままでよい） | 該当なし | 該当なし | Tab+Enterで即時確定される（現状維持の確認） |

#### 既存8ボタンの非回帰確認（懸念4の再発防止確認）

選択ツール・a接点等6ボタン・自作パーツの計8ボタンについて、Tab+Enterで起動した場合に
**引き続きフォーカスがボタン上に残り、Tab/矢印キーでのツールバー内ナビゲーションが
継続できること**（懸念4対応の既存挙動が本修正で崩れていないこと）を軽く確認する
（全数再検証ではなく代表1〜2ボタンのサンプリングで可、T-021既存検証との重複を避ける）。

#### マウス起動経路の非回帰確認

忍者既存検証の観点(3)（UI Automation Invoke＝マウス相当起動）は全ボタンで既にOK確認済み
（`archive/ecad2-t047-ninja-verification.md`50-54行目）。本修正がマウス起動経路（常にFocusCanvas
済み）に影響しないことは1-2節の設計上明らかだが、念のため縦分岐線記入1件の再確認で足りる
（対称性の全数再検証は不要、設計上マウス経路は修正の分岐対象外のため）。

### 2-4. RED証明の形（忍者実機観点、karo指定の必須要件）

**既にRED証明は実質完了している**：`docs/archive/ecad2-t047-ninja-verification.md`の4-a・4-cは、
修正前（コミット`4ecae77`）の実機での再現記録そのものであり、スクリーンショット3枚
（`t047-finding1-focus-stuck-connector-ninja.png`／`t047-newfinding-arrowkey-hijacked-ninja.png`
／`t047-newfinding-unintended-dot-persists-ninja.png`）付きで証拠化済み。修正後は、
**同一の再現手順（Tab+Enterで起動→矢印キー→Enter）を同じ3ボタン＋未検証の自由線(縦線)
記入を含む4パターンで再実行**し、上記2-3節の状態遷移表どおりGREENに転じることを確認すれば
RED→GREENの証明として十分と考える（新規スクリーンショット取得を推奨、修正前後の対比を
明示するため）。

---

## 3. 侍への申し送り事項（実装時の技術選択、指示ではない）

- 1-2節の推奨実装（`Tool.Mode`ベース分岐）を採用するか、ボタン単位の直書き（3メソッドのみ
  `FocusCanvas()`直呼び）を採用するかは侍の判断に委ねる。前者を選んだ場合のみ2-2節の
  任意ヘッドレステストが適用可能。
- `_toolButtonKeyboardClickSource`のクリアタイミング（1-2節の例では毎回無条件クリア）は
  既存の`ConsumeToolButtonFocusRestore`と同じ挙動を維持しており、変更不要と考える。
- 接続点記入・配線分断記入の2メソッドは変更不要（0節で確認済み、現状維持）。

---

## 4. 不明点

- 自由線(縦線)記入（sF9、主回路シート限定）の上下キーでの4-c型ハイジャック再現性は
  忍者記録に無く未検証（対称性表で明記済み）。修正後の検証で初めて確認されることになる。
- `Tool.Mode`ベース実装を侍が採用した場合の具体的なテスト可能形（`internal`化・
  `InternalsVisibleTo`利用・reflection経由等）は実装後でないと確定できない
  （P-040補遺2でも同種の判断＝実装時点で侍が選択した実績あり）。
