# T-055増分1 往復2周目 テスト設計（隠密起草）

> 2026-07-10 隠密起草。忍者実機確認（`docs-notes/ecad2-t055-increment1-realmachine-verification-ninja.md`
> 「範囲外の気づき」節）で検出されたStatusMessage残留バグの修正に向けた設計。
> 家老采配のとおり、(1)根本原因のWチェックと(2)テスト設計をここにまとめる。
> スコープ: 警告残留の解消のみ（便乗拡大禁止、成功時の新規メッセージ追加等のUX拡張は含めない）。

---

## 1. 根本原因（Wチェック、独立調査）

`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1607-1626`（`DeleteRowCommand`）:

```csharp
DeleteRowCommand = new RelayCommand(
    () =>
    {
        if (CurrentSheet is not Sheet sheet || sheet.Grid.Rows <= GridSpec.MinRows) return;
        int lastRow = sheet.Grid.Rows - 1;
        if (IsRowOccupied(sheet, lastRow))
        {
            StatusMessage = "最終行に要素があるため削除できません";   // 拒否パス: セット
            return;
        }
        sheet.Grid.Rows--;
        // ...SelectedCellクランプ処理(c28ec40)...
        MarkDirty();
        NotifyCurrentSheetChanged();
        // ← 成功パス: StatusMessageに一切触れていない（欠落箇所）
    },
    () => CurrentSheet is Sheet sheet && sheet.Grid.Rows > GridSpec.MinRows);
```

**確定**: 拒否パス（`IsRowOccupied`がtrue）はStatusMessageへ警告文言をセットするが、成功パス
（`sheet.Grid.Rows--`以降）にはクリア処理が一切無い。忍者が報告した再現手順
（拒否→要素除去→再削除で成功するがメッセージが残る）は、このコード構造から直接説明できる。

**既存設計慣習との対比**: 同ファイル内の`ReplaceDocument`（:1566）・Escキー処理
（`MainWindow.xaml.cs:681,1065,1477`）は、状態が変わる操作の後に無条件で`StatusMessage = "";`を
セットする慣習を徹底している（:1562-1565のコメントが「ステータスメッセージ残留...が起こりうる」
という過去の教訓を明記）。`DeleteRowCommand`の成功パスだけがこの慣習から逸脱しており、
増分1実装時の単純な書き漏れと判断する（推測ではなく、既存パターンとの構造比較による確定）。

**追記（2026-07-10、家老裁定でスコープ吸収）**: `AddRowCommand`（:1597-1605）の成功パスにも
同様にStatusMessageクリアが無い。当初は範囲外の気づきとして報告したが、「同じ増分1が導入した
双子コマンドの同一欠陥であり、対称実装の片側残置は制度が戒める型」との家老裁量により、
本往復2周目のスコープへ吸収された（P起票せず本設計書へ追補、下記2節末尾参照）。

```csharp
AddRowCommand = new RelayCommand(
    () =>
    {
        if (CurrentSheet is not Sheet sheet || sheet.Grid.Rows >= GridSpec.MaxRows) return;
        sheet.Grid.Rows++;
        MarkDirty();
        NotifyCurrentSheetChanged();
        // ← 成功パス: DeleteRowCommandと同型でStatusMessageに一切触れていない（欠落箇所）
    },
    () => CurrentSheet is Sheet sheet && sheet.Grid.Rows < GridSpec.MaxRows);
```

AddRowCommand自体は「拒否してStatusMessageへ警告をセットする」経路を持たない
（上限到達時はCanExecute=falseでボタン自体が無効化されるのみ）。ゆえに忍者の再現手順
（削除→削除）単体では顕在化しないが、「DeleteRowCommandの拒否で警告が出た直後、
AddRowCommandを実行して成功する」という経路（あるいは他操作由来の任意メッセージが
残っている状態でAddRowCommandが成功する経路）では、DeleteRowCommand側と全く同型の
残留が起こる。

---

## 2. テスト設計（状態遷移＋境界値、バグ修正の制度適用）

### 状態遷移表

対象状態: `StatusMessage`（"" / 拒否警告文言 / 任意の他文言）。
イベント: DeleteRowCommand実行（拒否 or 成功）／AddRowCommand実行（成功、対称追補分）。

| # | 事前状態 | イベント | 事後状態（期待） | 種別 |
|---|---|---|---|---|
| 1 | ""（初期） | 削除→拒否（最終行に要素あり） | 拒否警告文言 | 既存機能・回帰確認 |
| 2 | 拒否警告文言 | 削除→成功（最終行が空） | **""（クリア）** | **本体（往復2周目対象）** |
| 3 | ""（初期） | 削除→成功（最終行が空） | ""（不変） | 対照（誤クリアで別値が入らないことの確認） |
| 4 | 拒否警告文言 | 削除→再度拒否（まだ要素あり） | 拒否警告文言（不変・冪等） | 対照（連続拒否での意図せぬ変化なし） |
| 5 | 他操作由来の任意文言（例:「配置するセルを先に選択してください」） | 削除→成功 | **""（クリア）** | 境界（一律クリア方式の妥当性、特定文言決め打ちでないことの確認） |
| 6 | 拒否警告文言（DeleteRowCommand由来） | **行追加→成功** | **""（クリア）** | **本体対称（AddRowCommand側、家老裁定で追補）** |
| 7 | ""（初期） | 行追加→成功 | ""（不変） | 対照（対称、#3のAddRowCommand版） |
| 8 | 他操作由来の任意文言 | 行追加→成功 | **""（クリア）** | 境界（対称、#5のAddRowCommand版） |

### テストケース仕様（xUnit、パラメタライズド活用）

**#2・#3・#5は「削除成功時にStatusMessageが必ず""になる」という同一検証ロジックのため
`[Theory]`でパラメタライズする**（実装式の複製を避け、仕様を試験に書き下す）:

```
[Theory]
[InlineData("最終行に要素があるため削除できません")]  // #2: 直前が拒否警告だったケース(本体)
[InlineData("配置するセルを先に選択してください")]      // #5: 直前が無関係な他文言だったケース(境界)
[InlineData("")]                                        // #3: 直前が空だったケース(対照)
public void DeleteRowCommand_Execute_OnSuccess_ClearsStatusMessage(string priorMessage)
{
    // Arrange: Grid.Rows=10、最終行(row9)を空にした状態でStatusMessageへpriorMessageを事前セット
    // Act: DeleteRowCommand.Execute(null)
    // Assert: Rows==9（成功したこと）かつ StatusMessage == ""
}
```

**#1・#4は独立した`[Fact]`**（既存の拒否パス自体は本往復の変更対象ではないが、状態遷移の
完全性のため退行検知の網羅性を確保する）:

```
[Fact]
public void DeleteRowCommand_Execute_OnRejection_SetsWarningMessage()
{
    // Arrange: Grid.Rows=10、最終行(row9)に要素(ElementInstance等)を配置
    // Act: DeleteRowCommand.Execute(null)
    // Assert: Rows==10（拒否され不変）かつ StatusMessage == "最終行に要素があるため削除できません"
}

[Fact]
public void DeleteRowCommand_Execute_OnRepeatedRejection_KeepsWarningMessage()
{
    // Arrange: 最終行に要素がある状態で1回目のExecuteを実行済み(拒否警告が出た状態)
    // Act: 同条件のまま2回目のDeleteRowCommand.Execute(null)
    // Assert: Rows不変 かつ StatusMessage == "最終行に要素があるため削除できません"（同一文言のまま）
}
```

### AddRowCommand対称分の追補（#6・#7・#8、家老裁定でスコープ吸収）

DeleteRowCommand側の`[Theory]`（#2・#3・#5）を**そのまま対称適用**する。AddRowCommandには
「拒否してStatusMessageへ警告をセットする」経路自体が存在しない（1節の追記参照）ため、
#1・#4に対応するAddRowCommand版（拒否時セット・連続拒否）は作らない——これは非対称ではなく、
コマンドの性質上正しい非該当（ペア構成の対称性チェックは「両方に存在しうる遷移」にのみ適用する）。

```
[Theory]
[InlineData("最終行に要素があるため削除できません")]  // #6: 直前がDeleteRowCommand拒否警告だったケース(本体対称)
[InlineData("配置するセルを先に選択してください")]      // #8: 直前が無関係な他文言だったケース(境界、対称)
[InlineData("")]                                        // #7: 直前が空だったケース(対照、対称)
public void AddRowCommand_Execute_OnSuccess_ClearsStatusMessage(string priorMessage)
{
    // Arrange: Grid.Rows=10(上限未満)の状態でStatusMessageへpriorMessageを事前セット
    // Act: AddRowCommand.Execute(null)
    // Assert: Rows==11（成功したこと）かつ StatusMessage == ""
}
```

RED先行証明は#2系と同じ理屈（現行コードは成功パスにクリア処理が無いためpriorMessageが
非空のケースでAssert失敗＝RED、修正後は全InlineDataがGREEN）。

### RED先行証明の観点（侍実装時の指針）

`#2`系（Theory）が今回の修正対象の核心。現行コード（成功パスにクリア処理なし）で実行すると、
`priorMessage`が非空文字列のケース（"最終行に要素があるため削除できません"・"配置するセルを
先に選択してください"）でAssertが失敗（StatusMessageが事前セット値のまま）＝RED。
修正後（成功パスに`StatusMessage = "";`を追加）で全InlineDataがGREENになることを確認する。

---

## 3. スコープ境界（侍実装時の遵守事項、便乗拡大禁止）

- 触ってよい:
  - `DeleteRowCommand`の成功パス（`sheet.Grid.Rows--`〜`NotifyCurrentSheetChanged()`の間）
    へ`StatusMessage = "";`を1行追加。
  - `AddRowCommand`の成功パス（`sheet.Grid.Rows++`〜`NotifyCurrentSheetChanged()`の間）
    へ同様に`StatusMessage = "";`を1行追加（家老裁定で本往復スコープへ吸収、対称修正）。
- 触らぬ: 成功時の新規メッセージ追加等のUX拡張（家老采配で明示的に除外）。両コマンド以外の
  他コマンド・他ファイルへの波及は便乗拡大禁止。
- 設計にないテストの追加は自由、設計にあるものを勝手に省くのは不可（役儀書の原則どおり）。

---

## 出典

- 忍者実機確認: `docs-notes/ecad2-t055-increment1-realmachine-verification-ninja.md`
- 根本原因: `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:1287-1294,1560-1571,1607-1626`直読
- 既存クリア慣習の参照箇所: `MainWindow.xaml.cs:681,1065,1477`
