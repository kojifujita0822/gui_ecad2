# T-061 テストモード 修正方針設計書（隠密起草、2026-07-13）

土台: `docs/archive/ecad2-t061-review-onmitsu.md`（静的レビュー結果、全指摘CONFIRMED）。
本書は「テスト設計と実装の分離」パターンに則り、実装前に仕様側から修正方針とテスト設計を確定する。
実装は侍、UI/UX判断を要する箇所は本書内で選択肢を提示し殿確認を仰ぐ（自分では決めない）。

---

## 1. Mode/Tool相互排他の統一設計（A-1/A-2対応）

### 現状の問題
マウス側（`LadderCanvasHost_PreviewMouseLeftButtonDown`/`PreviewMouseRightButtonDown`）だけが関数冒頭で
個別に`if (Mode==Test) {...; return;}`を持ち、キーボード（`Window_PreviewKeyDown`全体）・ツールバー配置
ボタン（`IsEnabled="{Binding HasProject}"`のみ）・メニュー「編集→削除」「元に戻す/やり直す」には
Mode参照が一切無い。個別対応の積み重ねは今回のバグそのものの原因（PR-12候補）であり、同じ轍を踏まない
ため単一ゲートへ集約する。

### 修正方針：`CanEditDiagram`単一プロパティへの集約

`MainWindowViewModel`に以下を新設する：
```csharp
public bool CanEditDiagram => HasProject && Mode == AppMode.Drawing;
```
`Mode`セッタの`OnPropertyChanged(nameof(Mode))`と同じタイミングで`OnPropertyChanged(nameof(CanEditDiagram))`
も発火させる（`HasProject`変更箇所でも同様）。

これを以下全ての入口に束ねる：

1. **ツールバー配置ボタン群**（`MainWindow.xaml:282-341`）：`IsEnabled="{Binding HasProject}"`を
   `IsEnabled="{Binding CanEditDiagram}"`へ変更。
2. **キーボード側**：`Window_PreviewKeyDown`冒頭、既存の`IsPlacementBarVisible`/`_rungCommentEditingRow`
   早期returnと同列に`if (!_viewModel.CanEditDiagram) return;`を追加。**ただし下記2点は
   `CanEditDiagram`ガードの対象外とする（殿裁定2026-07-13、確認事項1確定）**：
   - **矢印キー（1430行目ケース、`MoveSelectedCell`によるSelectedCell移動）は対象外**——Test中も
     従来通り有効。選択中のConnector/WireBreak/FreeLine/ConnectionDot/Image等プリミティブの移動
     ケース（1470-1497行目付近、Shift併用含む）は「編集操作」に該当するため対象外にせず
     `CanEditDiagram`ガードの内側（禁止側）のまま、という解釈で実装する（殿裁定の文言
     「矢印キー（SelectedCell移動）」はセル移動のみを指すと解する。実装・レビュー時に解釈の
     齟齬が無いか要再確認）。
   - **Enterキーによるテスト通電操作を新規結線する（殿裁定、本設計書の当初案には無かった追加要求）**。
     `Mode==Test`かつ`SelectedCell`上に要素がある場合、Enter押下を「その要素へのマウス左クリック
     相当」として扱い、既存の`TestModePress`（`MainWindowViewModel.cs:1991`、マウスハンドラが
     呼んでいるのと同じメソッド）を呼び出す。モーメンタリ動作（押しボタン）の解除は、対応する
     `PreviewKeyUp`ハンドラ（既存になければ新設）でEnterキーアップ時に`TestModeRelease`を呼ぶ形で
     実現する。WPFのキーリピート（押しっぱなしでKeyDownが連続発火する挙動）による重複実行を
     防ぐため、`e.IsRepeat`をチェックし2回目以降は無視するガードを入れる。
     この新規結線は`CanEditDiagram`ガードより前（＝Mode==Testの間、通常のEnter配置確定ケース
     （1522-1555行目、`Tool.Mode==PlaceElement`条件）とは別の独立したcaseとして）に置く。
3. **メニュー「編集(_E)→削除(_D)」「元に戻す/やり直す」**：`IsEnabled="{Binding CanEditDiagram}"`を
   追加（現状バインド無し、A-1で確認済みの非対称の是正）。
4. **`Tool`セッタへの二重の安全網**（`MainWindowViewModel.cs:28-40`）：Test中に`SelectDefault`以外への
   変更を拒否するガードを追加（A-2「Test+PlaceElement」の矛盾状態を型レベルで防ぐ）：
   ```csharp
   public ToolState Tool
   {
       get => _tool;
       set
       {
           if (_appMode == AppMode.Test && value.Mode != ToolMode.Select) return;
           ...
       }
   }
   ```
   これによりツールバーボタン（3.の`IsEnabled`）が万一漏れても、Tool自体が変化しないため
   `IsPartSelectionVisible`（`Tool.Mode==PlaceElement`依存）等の副作用が発生しない、二段構えの防御になる。

### 確認事項1：確定済み（殿裁定2026-07-13）
キー無効化範囲は案A・案Bいずれでもなく第三案で確定した：矢印キー（SelectedCell移動）とEnterキー
（選択中要素へのテスト通電操作、新規結線）は許可、それ以外の編集系ショートカット
（F5-F10配置・Delete・Enter配置確定・Ctrl+Z/Y等）は全面禁止。Tab/Escapeは元々対象外のまま。
詳細は上記2.参照。

---

## 2. Undo/RedoとTestSessionの関係設計（A-3対応）

### 現状の問題
`_testSessions`はSheet参照キー。Undo/Redoは`Document`ごと差し替えるため、Undo後のSheetは別実体になり
`CurrentTestSession`が黙って空の新セッションを生成、進行中のシミュレーション状態（自己保持等）が
無警告で消失する。上記1.の`CanEditDiagram`集約でUndo/Redoコマンドの`IsEnabled`をテストモード中false化
すれば、「テストモード中にUndo/Redoボタンを押す」経路は塞がれる。ただし**テストモードに入る前の操作を
Undoしたい**という需要とは別論点であり、下記の選択肢は「テストモード中はUndo/Redo自体をどう扱うか」の
UI/UX判断を要する。

### 確認事項2：確定済み（殿裁定2026-07-13）
**案A採用**。テストモード中はUndo/Redoを無効化する（`CanEditDiagram`にUndoCommand/RedoCommandの
CanExecuteも統合）。「テストモード＝観察専用」の一貫性が保て、1.の統一ゲートにそのまま乗る。
テスト実行前の編集をUndoしたければ、一旦テストモードをOFFにしてから行う運用になる。

---

## 3. 通電表示配色ロジックの修正方針（B群）

### 根本原因（B-1・B-2・B-3は同一原因）
`DiagramRenderer.DrawElement`（`DiagramRenderer.cs:949-957`）は`energized[DeviceName]`
（＝コイルの励磁状態のみ、`Evaluator.cs:52-62`で`ComponentRole.Load`にのみ書き込まれる）を直接見て
線色を決めており、`Evaluator.IsConducting`（`Evaluator.cs:132-172`、NO/NC反転・タイマ限時判定・
セレクトSWノッチ判定を正しく持つ既存ロジック）を一切参照していない。接点・押しボタン・セレクトSWが
コイルではない（`Energized`に登録されない）ため常にグレー固定になり（B-1）、限時接点はコイル励磁の
瞬間から時期尚早に赤くなり（B-2）、NC系の反転も`DrawElement`側では機能しない（B-3、`IsConducting`
自体はNO/NC正しく反転しているのでNetlist評価は正常、描画色だけがズレている）。

### 修正方針：Evaluatorの評価結果に要素単位の導通状態を追加する

`Component`は`SourceElementId`（`Netlist.cs:29`、発生元`ElementInstance.Id`）を既に持っている
（描画で要素↔ネットを対応づける既存の仕組み、rule of three対応で新設不要）。これを使い:

1. `EvalResult`（`Evaluator.cs:15-26`）に`Dictionary<Guid, bool> ElementConducting`を追加。
2. `Evaluator.Evaluate`の収束時（`Evaluator.cs:71-78`のreturn直前）に、`_net.Components`を走査し
   既存のprivateメソッド`IsConducting(c, next)`（既存ロジックそのまま再利用、新規判定は書かない）の
   結果を`ElementConducting[c.SourceElementId] = ...`へ格納する。
3. `SimState`（`Netlist.cs:33-48`）に`Dictionary<Guid, bool>? ElementConducting`を持たせるか、
   `TestSession.Result`（既存の`EvalResult? Result`）経由で`DiagramRenderer.Render`に渡す経路を
   新設するか、どちらかで`DrawElement`まで届ける（実装詳細は侍判断でよい、Renderメソッドの
   シグネチャ変更が絡むため呼び出し元の網羅確認を要する——`PdfExporter.cs`・`PdfPreviewDialog.xaml.cs`
   はsim=null経路なので影響なし、`LadderCanvas.cs`のみ要追随）。
4. `DrawElement`の`stroke`計算を、`energized[DeviceName]`直接参照から
   `elementConducting.TryGetValue(e.Id, out var conducting) && conducting`へ置き換える。

この1本の修正でB-1（コイル以外も正しい導通状態が引ける）・B-2（限時接点は`timedOut`込みの正しい判定）・
B-3（NC反転も`IsConducting`が既に正しく持つ）が同時に解消される。

### 確認事項3：実装後のプレビュー確認に据え置き（殿裁定2026-07-13、今は決めない）
コイル・接点以外（押しボタン・セレクトSW自体の記号）について、上記修正で「導通中は赤」という表示が
機能するようになるが、この見せ方（押している間だけ押しボタン記号が赤くなる、セレクトSWは選択ノッチに
応じて対応する接点が赤くなる、等）がGuiEcad踏襲の意図と合致するかは、実装完了後に画面（プレビュー）を
見てから改めて殿確認とする。

---

## 4. 型不整合の是正方針（C群）

### 根本原因
`PlaceElementAtSelectedCell`が新規要素生成時に`ElementInstance.Kind`を一切設定しないため、全ての
UI配置要素の`Kind`はC#既定値`ContactNO`のまま固定される（既存テスト`MainWindowViewModelTests.cs:349-353`
=T-046由来のコメントで既知の構造的制約として記録済み）。この制約自体はT-061のスコープ外（既存の
構造的課題、是正には配置経路全体の見直しを要し影響が広い）だが、**T-061の新規コードがこの制約を
見落として`ElementKind`直接判定を書いたことが直接原因**であり、当該2箇所は既存の確立解決パターン
（`PartResolver.ComponentKind`/`CreatesComponent`、`ResolveDeviceClass`が既に正しく使っている）へ
置き換えるだけで解決できる。

### C-1: セレクトSWノッチ順送り（`CycleSelectSwitch`）
`MainWindowViewModel.cs:2021`付近の`e.Kind == ElementKind.SelectSwitch`を、`ResolveDeviceClass(e)`
（既にインスタンスメソッドとして存在、Category/Role/IsOrEligible判定込み）が`DeviceClass.SelectSwitch`
を返すかどうかへ置き換える。`CycleSelectSwitch`は現状`static`だが、`ResolveDeviceClass`呼び出しのため
インスタンスメソッド化（またはPartPalette/PartLibraryを引数で渡す）が必要——実装詳細は侍判断。

### C-2: 右クリック接点限定ガード（`ShowTestModeContextMenu`）
`MainWindow.xaml.cs:1086`の`hit.Kind is not (ContactNO or ContactNC)`を、`ResolveDeviceClass`と
同型のガード（`PartResolver.CreatesComponent`で事前ガードした上で`PartResolver.ComponentKind(hit, lib)
is not (ContactNO or ContactNC)`）へ置き換える。`ComponentKind`は`PartDefinition.Role`から実際の
電気的種別を正しく解決するため（`PartResolver.cs:43-65`）、セレクトSW（Role=ContactNO）は正しく
「接点」側に含まれ、コイル・ContactorMain3P（Role=Coil等）は正しく除外される。

### C-3: 左クリックのコイル誤動作（`TestModePress`のdefault分岐）
`ResolveDeviceClass`が返す`DeviceClass.Relay`はContactNO/NC・Coil・ContactorMain3Pを一括りにしている
（機器表分類としては正しい設計、P-020対応）ため、これをそのまま左クリックの操作可否判定に使うのは
不適切。`DeviceClass.Relay`のdefault分岐内でさらに`PartResolver.ComponentKind(element, PartLibrary)
is ElementKind.ContactNO or ElementKind.ContactNC`を確認し、真の場合のみ`ToggleInput`を実行、
Coil/ContactorMain3Pの場合は無反応（`return null`）とする。

### 確認事項4：確定済み（殿裁定2026-07-13）
**無反応のみ採用**（ステータスメッセージなし）。C-3でコイル・ContactorMain3Pを左クリックしても
`session.ToggleInput`を呼ばず`return null`するのみで、`StatusMessage`等の追加表示は行わない。

---

## 5. テストケース設計（体系的技法適用）

`onmitsu.md`「テスト設計の起草」節の技法（同値分割・境界値・状態遷移・ペア対称性・Theory活用）に
従い、観点を列挙する。侍はこの設計にないテストの追加は自由、設計にあるものを勝手に省くのは不可。

### 5-1. 状態遷移（Mode遷移表、Drawing⇔Test）

| 現在状態 | 事象 | 遷移先 | 検証すべき副作用 |
|---|---|---|---|
| Drawing（要素選択中） | テストモードON | Test | `Tool`がSelectDefaultへ戻る／選択状態は本設計では意図的に保持されるか要確認（下記注） |
| Test | テストモードOFF | Drawing | `_testSessions.Clear()` |
| Test | Undo/Redo実行 | （確認事項2の裁定次第） | 案Aなら「実行不可（CanExecute=false）」を検証 |
| Test | F5等キー入力 | 変化なし | `Tool.Mode`が変化しないこと（CanEditDiagram=falseでガード） |
| Test | ツールバー配置ボタン押下 | 変化なし | 同上、マウス経路でも同じ結果になること（ペア対称性） |
| Test | 矢印キー | SelectedCell移動 | `CanEditDiagram`ガード対象外（殿裁定確定）、Test中も従来通り移動できること |
| Test（SelectedCell上に押しボタン等） | Enterキー押下→キーアップ | モーメンタリON→OFF | `TestModePress`/`TestModeRelease`がマウス左クリック時と同じ結果になること（新規結線、殿裁定確定）。キーリピート(`e.IsRepeat`)で多重発火しないこと |
| Test | Ctrl+Z/Y | 変化なし | UndoCommand/RedoCommandのCanExecuteがfalseになること（確認事項2=案A確定） |
| Test | Sheet切替（シートナビ） | Test継続 | 新シートの`CurrentTestSession`が遅延生成され`Evaluate()`済みであること |

**注（設計判断が必要）**: `Mode`セッタは現状Test突入時に`Tool`のみリセットし`SelectedCell`等は
意図的に保持しない設計だが、A-1の`CanEditDiagram`ガードにより「選択状態が残っていてもキー操作
自体が無効化される」ため実害は塞がる。ただし念のため、Test突入時に`SelectedCell`等もクリアするか
（PR-05的な状態リセットの一貫性）は軽微な論点として本文4.のD群と合わせて仕分けてよい。

### 5-2. 境界値・同値分割（DeviceClass判定、C群）

- 同値クラス: (a)ContactNO/NC（自作パーツ含む、Category=""判定境界） (b)Coil/ContactorMain3P
  (c)SelectSwitch（Role=ContactNO&IsOrEligible=false） (d)通常のContactNO（Role=ContactNO&
  IsOrEligible=true、OR対象） (e)PushButton (f)Lamp/Terminal/Timer/Counter等その他
- 境界: (c)と(d)は同じRole=ContactNOだが`IsOrEligible`で分岐——このフラグの境界（true/false）を
  明示的にテストする（`[Theory][InlineData(false, DeviceClass.SelectSwitch)][InlineData(true, ...)]`
  等）。
- `CreatesComponent==false`（NonSimulatedロール）の要素に対し、C-2/C-3の新ガードが例外を投げず
  正しく除外側に倒れることを確認する境界ケース。

### 5-3. ペア対称性（マウス/キーボード、A群）

「同じ操作の異なる入力経路」の表を作り、テストモード中は両方とも同じ結果（＝変化なし）になることを
確認する：

| 操作 | マウス経路 | キーボード経路 | 期待結果（Test中） |
|---|---|---|---|
| 要素配置 | ツールバーF5相当ボタン | F5キー | いずれもTool.Mode変化なし |
| 要素削除 | メニュー「編集→削除」 | Deleteキー | いずれも`sheet.Elements`変化なし |
| セル移動 | （該当なし） | 矢印キー | CanEditDiagram対象外につきSelectedCellは移動する（殿裁定確定、他行と扱いが異なる点に注意） |
| 通電操作（押しボタン等） | 左クリック（`TestModePress`） | Enterキー（新規結線） | 同一要素に対し同じ結果（モーメンタリON/OFF・トグル・ノッチ順送り）になること（新規のペア対称性、殿裁定で追加） |
| Undo | メニュー/ツールバー | Ctrl+Z | いずれも実行されない（確認事項2=案A確定） |

### 5-4. Theory活用（DeviceClass×操作のマトリクス）

`TestModePress`の全DeviceClass×全操作（左クリック）の組み合わせを`[Theory][InlineData]`で網羅する：
PushButton→モーメンタリON/OFF、SelectSwitch→ノッチ順送り、ContactNO/NC→トグル、
Coil/ContactorMain3P→無反応、Lamp/Terminal/Timer/Counter→無反応。C-3修正前はCoil/ContactorMain3Pが
誤ってトグルされていたため、この境界がまさに今回のバグの再発防止テストになる。

### 5-5. 通電色（B群、パラメタライズド）

ElementKind×Energized×Inputs×TimerElapsedの組み合わせで期待色（Powered/NonEnergizedGray/既定色）を
`[Theory]`で網羅：ContactNO(Energized=true→赤/false→グレー)、ContactNC(Energized=true→グレー/
false→赤、反転確認＝B-3の再発防止)、TimerContactNO(Energized=true&timedOut=false→グレー、
Energized=true&timedOut=true→赤、B-2の再発防止)、PushButton(Inputs=true→赤/false→グレー、
B-1の再発防止)。既存`DiagramRendererTestModeColorTests.cs`（T-061既存追加分、83行）を土台に拡張する。

---

## 6. D/E群の反映要否仕分け

| 項目 | 仕分け | 理由 |
|---|---|---|
| D-1 残留ドラフト+HasAnyDraft迂回 | **反映要** | `Mode`セッタでTool変更時に既存の`ClearConnectorDraftIfAny`等（`SelectedCell`セッタ経由の確立パターン）を呼ぶ形に揃えれば解消。右クリック分岐の順序もHasAnyDraftチェックより前に置くか要検討 |
| D-2 CaptureMouse戻り値未チェック | **反映要** | 既存確立パターン（隠密レビュー所見C対応）を踏襲するだけの低コスト修正 |
| D-3 メニューHasProjectガード非対称 | **反映要（自動解決）** | 1.の`CanEditDiagram`統合で自動的に解消 |
| E-1 行範囲チェック3箇所重複（PR-07） | 反映推奨（緊急でない） | 今回C群修正で右クリックハンドラに手を入れるついでに共有ヘルパー化する好機 |
| E-2 100msごとの無駄な再評価 | 家老/殿判断 | 実害は体感パフォーマンスのみ、今回のスコープに含めるかは規模次第 |
| E-3 Mode切替毎の全DRC再実行 | 家老/殿判断 | 同上 |
| E-4 Mode setter重複クリア | 反映推奨（低コスト） | else節削除のみ、1.の修正と同時に手を入れる好機 |
| E-5 CLAUDE.md違反（丸数字） | 対応不要 | 既に後続コミット7fa43dbで是正済み |

---

## 7. UI/UX確認事項まとめ（殿裁定確定、2026-07-13）

1. **確定**：キー無効化は矢印キー（SelectedCell移動）とEnterキー（テスト通電操作、新規結線）を
   除き全面禁止（Tab/Escapeは元々対象外）。第三案（案A/Bいずれでもない）。
2. **確定（案A）**：Undo/RedoとTestSessionの関係はテスト中無効化。
3. **据え置き**：押しボタン・セレクトSW自体の記号の導通色ハイライトの見せ方は実装完了後プレビューで確認。
4. **確定**：コイル等を左クリックした場合は無反応のみ、ステータスメッセージは出さない。

裁定内容は本書各節（1.・2.・3.・4.）へ反映済み。侍への実装采配をお願いする。
