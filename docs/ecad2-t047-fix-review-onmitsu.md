# T-047修正（記入中フォーカス残留対応）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`afeb068`（main上、push保留中。
> `src/Ecad2.App/MainWindow.xaml.cs`+12行、`tests/Ecad2.App.Tests/
> ManualWiringFocusContinuationTests.cs`新規32行）。隠密設計書
> （`docs/ecad2-t047-fix-test-design-onmitsu.md`）との突合を実施。`code-review`スキル
> （medium effort、2角度＝正しさ[diffスキャン+削除挙動+クロスファイル]／クリーンアップ
> [Reuse+Simplification+Efficiency+Altitude+Conventions]、1-vote検証込み）を併用。
> 実測検証（`dotnet test`読み取り専用実行）も併用。共有main上への一時注入検証は
> 行っていない（`feedback_no_live_injection_on_shared_main`の家老裁定どおり）。

---

## 結論：**クリーン（機能バグなし）。ドキュメント精度の軽微な指摘1件＋将来リスクの経過観察1件を記録**

設計書どおりの実装であることを確認した。忍者実機再確認へ回してよいと判断する。

---

## 1. 設計書との突合結果

### (1) 設計書どおりの実装か

**一致。** `docs/ecad2-t047-fix-test-design-onmitsu.md`1-2節が推奨した実装（ボタン単位でなく
`Tool.Mode`ベースの分岐）がそのまま採用されている：

```csharp
private void ConsumeToolButtonFocusRestore(object sender)
{
    bool isKeyboardOrigin = ReferenceEquals(_toolButtonKeyboardClickSource, sender);
    if (isKeyboardOrigin && !RequiresCanvasFocusContinuation(_viewModel.Tool.Mode))
        (sender as UIElement)?.Focus();
    else
        FocusCanvas();
    _toolButtonKeyboardClickSource = null;
}

private static bool RequiresCanvasFocusContinuation(ViewModels.ToolMode mode) =>
    mode is ViewModels.ToolMode.PlaceConnector or ViewModels.ToolMode.PlaceLine;
```

`RequiresCanvasFocusContinuation`はWPF型に一切依存しない純粋関数（`ToolMode`→`bool`のみ）で
設計書2-2節の要件どおり。新規テスト`ManualWiringFocusContinuationTests.cs`は
`[Theory]`+`[InlineData]`で`ToolMode`全7値（Select/PlaceElement/PlaceConnector/PlaceFrame/
PlaceLine/PlaceDot/PlaceWireBreak）を網羅し、reflection（`BindingFlags.NonPublic |
BindingFlags.Static`）経由でprivate static メソッドを直接検証している。全呼び出し元8箇所
（`MainWindow.xaml.cs:1051,1084,1304,1314,1320,1326,1332,1338`）とenum全7値の対応を実際に
突合し、テストのカバレッジに漏れがないことを確認した。設計書2-2節が示唆した「同値分割で
全7値を網羅する」というテスト設計技法が正しく適用されている。

### (2) 既存への無影響（設計書1-3節トレース表との突合）

**部分的に不正確**。code-reviewで検出した所見1（下記2節）参照。既存8ボタン・接続点記入・
配線分断記入の**各ボタン単体の直接効果**としては`Tool.Mode`を`PlaceConnector`/`PlaceLine`に
しないという設計書の主張は正しい（実装を確認済み）。ただし、**他の操作で既にモードが
記入中状態へ遷移済みの場合**（例：F9キーボードショートカットで既に自由線記入中の状態から、
未確定のままTabで接続点記入ボタンへ移りEnterで起動する経路）まで含めると、接続点記入・
配線分断記入ボタンの挙動も影響を受けうる。詳細は2節所見1参照（実害は無く、むしろ副次的に
改善する方向であることを検証済み）。

### (3) RED証明の整合

**クリーン、実測で確認。** コミットメッセージの「`RequiresCanvasFocusContinuation`を一時的に
常時falseへ戻し、`[Theory]`全7値のうちPlaceConnector/PlaceLineの2件が失敗することを実測」
という手法を検証した。`!false`は常に`true`となるため、この一時的な変更は
`if (isKeyboardOrigin && true)` = `if (isKeyboardOrigin)`と、**修正前の分岐条件と完全に
一致**する。すなわちこのRED再現は「修正前のロジックを正確に再構成した状態でテストを実行し、
新設テストが期待どおり失敗する」ことを示しており、テストが実際にバグ経路（記入中状態での
分岐）を突いていることの妥当な証明になっている。

`dotnet test`を読み取り専用で実行し、Core 14件＋App 227件＝**241件全合格**を確認した
（コミットメッセージの「既存234+新規7」と一致）。

### (4) code-reviewスキル併用

実施済み（medium effort、正しさ3角度統合1エージェント＋クリーンアップ5角度統合1エージェント、
1-vote検証込み）。結果は2節。

---

## 2. code-reviewスキルで検出した所見

### 所見1（ドキュメント精度、確度: CONFIRMED＝挙動変化は実在／実害はREFUTED相当）
記入中状態が他経路で既に成立している場合、接続点記入・配線分断記入ボタンの
フォーカス挙動もこの修正の影響を受ける

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1111-1119`（`ConsumeToolButtonFocusRestore`）、
  `1269-1291`（`TryPlaceConnectionDot`、`Tool.Mode`を一切変更しない）、`728-734`
  （`Window_PreviewKeyDown`のF9ケース、`ConsumeToolButtonFocusRestore`/`FocusCanvas()`を
  経由しないグローバルショートカット経路）

**到達手順（検証者エージェントが実際に確認）**：
1. 主回路シートでF9キー（グローバルショートカット、`ConsumeToolButtonFocusRestore`非経由）を
   押し、自由線(横線)記入モード（`Tool.Mode=PlaceLine`）を開始。フォーカスはどこにも
   強制移動しない。
2. Tabキーで「接続点記入」ボタン（同じく主回路シート限定で活性、F9系と同時に活性化しうる）
   へフォーカス移動し、Enterで起動。
3. `ConnectionDotButton_Click`→`TryPlaceConnectionDot()`実行（`Tool.Mode`は`PlaceLine`の
   まま不変）→`ConsumeToolButtonFocusRestore(sender=接続点記入ボタン)`が呼ばれる時点で
   `RequiresCanvasFocusContinuation(PlaceLine)`が`true`を返し、キーボード起因にも
   かかわらず`FocusCanvas()`が呼ばれる。

**コミットメッセージ・設計書の主張との差異**：「接続点記入・配線分断記入は実行後Tool.Modeが
PlaceConnector/PlaceLineに決してならないため無影響」という記述は、各ボタン**単体**の直接効果
のみを見た説明であり、**他操作による記入中状態の持ち越し**というケースを説明しきれていない。

**実害の評価（検証者エージェントが検証）**：この挙動変化は有害ではない。修正前の分岐のままで
あれば、この経路でも接続点配置後にフォーカスがボタンに残留し、進行中の自由線記入が
矢印キー操作不能になる＝本コミットが解消しようとした症状と同型の問題が別経路で再現して
しまっていたはずである。今回の修正はこの派生経路も意図せず同時に救済しており、
**データ破損・クラッシュ・誤操作誘発には繋がらない**。`TryPlaceConnectionDot`が`Tool.Mode`
非依存で即時確定する点自体（記入中でも接続点を打てること）は本コミット由来ではなく既存の
別論点であり、本修正の評価対象外。

**結論**：機能上のバグではない（むしろ望ましい副作用）。コミットメッセージ・設計書の
「無影響」という説明が、この派生経路を見落として不正確になっている点のみをドキュメント
精度の指摘として記録する。修正不要、記録のみでよいと判断する。

### 所見2（将来リスク、確度: PLAUSIBLE、経過観察向き）
`RequiresCanvasFocusContinuation`の2値ホワイトリストは`ToolMode`に将来値が追加され
配線される際に更新漏れの余地がある

- **file**: `src/Ecad2.App/MainWindow.xaml.cs:1128-1129`（`RequiresCanvasFocusContinuation`）、
  `src/Ecad2.App/ViewModels/ToolState.cs:12`（`ToolMode`enum定義）

`ToolMode`側に列挙値の意味（記入中・継続操作要否等）を分類する属性・命名規則等の仕組みは
無く、`RequiresCanvasFocusContinuation`は`PlaceConnector`/`PlaceLine`という生の2値
ハードコードである。**設計書自身（1-2節）が「Tool.Modeベースの分岐なら将来のボタン追加でも
自動的に正しく分類される」と述べているが、これは「ボタン単位の対応漏れ」は防ぐものの、
「`ToolMode`のenum値自体を追加する際の本関数の更新漏れ」は防がない**——設計書の主張は
厳密には過大である。

現状、`PlaceFrame`は未配線（grep確認、参照箇所なし、配線計画も`docs/todo.md`に見当たらず
不明）。`PlaceDot`/`PlaceWireBreak`は既に配線済みだが、いずれも即時確定型
（`TryPlaceConnectionDot`/`TryPlaceWireBreak`が`Tool.Mode`を変更しない）であることを
設計書0節で確認済みのため、現行の2値ホワイトリストに機能上の欠落は無い。

**既存の同種懸念との整合**：`docs/observations.md`#3（2026-07-04記録）に、`ToolMode`未配線値
が将来配線される際の同種の懸念（Esc層2の解除対象漏れ）が既に「経過観察・現状実害なし・
配線時に要注意」という扱いで記録されている。本件も同じ性質の構造的懸念であり、プロジェクトの
既定の扱い方（`observations.md`への記録・経過観察）に倣うのが整合的と考える。

**結論**：緊急修正は不要。`observations.md`への追記を推奨する（家老裁定に委ねる）。

### Reuse/Simplification/Efficiency/Conventionsの結果

- **Reuse**：`BindingFlags.NonPublic`によるreflectionパターンは`MainWindowViewModelTests.cs`
  に既存（`MapToDeviceClass`検証、P-040補遺2）だが、対象クラス（`MainWindowViewModel`と
  `MainWindow`）が異なり、テストファイル命名も既存の機能名ベース命名規則に沿っている。
  重複実装とは判定しない。
- **Simplification**：新設メソッド・テストコードともに簡潔で冗長性なし。
- **Efficiency**：低頻度イベント（ツールボタンのフォーカス処理）のため懸念なし。
- **Conventions**：`CLAUDE.md`の明確な規約違反なし。

---

## 3. 総括

設計書（`docs/ecad2-t047-fix-test-design-onmitsu.md`）どおりの実装が確認でき、RED証明も
バグ経路を正確に突いている。241件全テスト合格を実測確認済み。code-reviewで検出した2件は
いずれも**修正不要**（所見1＝機能バグでなくドキュメント精度の指摘、実害はむしろ改善方向。
所見2＝`observations.md`と同種の経過観察レベルの将来リスク）。**忍者実機再確認へ回すのが
妥当**と判断する。

修正要否・`observations.md`追記の要否は家老の裁定に委ねる。

---

## 4. 不明点

- 所見1で発見した派生経路（他操作による記入中状態の持ち越し）が、忍者の実機観点表
  （設計書2-3節）に明示的に含まれていないため、余力があれば忍者の回帰スモークに
  軽く加えることを推奨する（必須ではない、副作用は無害と判定済みのため）。
