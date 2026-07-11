# T-050修正 テスト設計（隠密起草）

対象：T-050静的レビュー（`docs/ecad2-t050-review-onmitsu.md`）指摘1・指摘2の修正。テスト設計と実装の分離【MUST】（2026-07-08殿裁定）に基づき、仕様側からの設計のみ。実装は侍へ委譲。

---

## 指摘2（本命）：SelectedSheetセッタold==newバグ

### あるべき振る舞い（仕様）

`SheetNavigationViewModel.SelectedSheet`セッタが呼ばれてOnPropertyChangedへ渡す`oldValue`は、**そのセッタ呼び出しの直前にUIが認識していたSelectedSheetの値**と一致しなければならない。

具体的には：
- シートが0枚の状態から初めて1枚追加する操作（`AddCommand`、`wasEmpty=true`の経路）では、あるべきoldValueは**null**（追加前は選択中のシートが存在しないため）。
- シートが1枚以上ある状態から追加する操作（`wasEmpty=false`の経路）では、あるべきoldValueは**追加直前に選択されていたシート**。

### 状態遷移表

| No | 遷移前Sheets.Count | 遷移前SelectedSheet | 操作 | 遷移後Sheets.Count | 遷移後SelectedSheet(new) | あるべきoldValue | 現状 |
|---|---|---|---|---|---|---|---|
| 1 | 0（下限） | null | AddCommand（初回追加） | 1 | 新シートS1 | null | **CONFIRMED バグ**：old==new（S1自身）になる |
| 2 | 1（下限+1） | S1 | AddCommand（2枚目追加） | 2 | 新シートS2 | S1 | 未検証（本設計の検証対象に含める） |
| 3 | N（一般） | Sx | AddCommand（N+1枚目追加） | N+1 | 新シートS(N+1) | Sx | 未検証（対称性点検として同上） |

No.2・No.3は今回のバグ報告（No.1）とは異なる経路（`wasEmpty=false`、`SelectedSheet=sheet`代入時点でgetterは旧選択シートを正しく返すはず）だが、境界値分析上「隣接する正常系」として併せて検証し、修正が退行を生まないことを確認する。

### テスト可能性の見極め（純粋関数切り出しの要否）

**核心の問題**：`SelectedSheet`セッタ内`var oldValue = SelectedSheet;`（getter呼び出し）が実行される時点で、`AddCommand`の実装（`SheetNavigationViewModel.cs:88-106`）は既に`Sheets.Add(sheet)`（88-89行目、同期実行）を完了済みであり、セッタ自体は`_dispatcher.BeginInvoke`により**遅延実行**される（104-106行目）。よってセッタ内部だけでは「追加前の状態」を再現できない——これがバグの構造的原因。

一方、`AddCommand`は既に`bool wasEmpty = _owner.Document.Sheets.Count == 0;`（77行目）という**追加前状態を記憶するローカル変数**を、`Sheets.Add`実行前に確保している（`NotifyCurrentSheetChanged`の要否判定に使用中、95-98行目）。この前例パターンの存在は、「追加前のSelectedSheet相当値」を同様に`Sheets.Add`実行前に確定させることが実装上難しくないことを示す。

以上より、**2つの実装経路が考えられ、どちらを侍が選ぶかでテスト可能性が変わる**：

**経路X（推奨）：AddCommand側で追加前状態を明示的に捕捉**
`Sheets.Add(sheet)`実行前に`var oldSelectedSheet = SelectedSheet;`（またはwasEmptyから導出する`wasEmpty ? null : SelectedSheet`）を計算し、BeginInvokeのラムダ内で`SelectedSheet = sheet`実行後、セッタが計算した誤ったoldValueではなく、この事前捕捉値を使ってOnPropertyChangedを発火させる（実装の詳細は侍の裁量）。
→ 「追加前状態→あるべきoldValue」の対応ロジックは入出力が明確な小さなロジックとして切り出し可能。**この場合はユニットテストでRED証明できる**（下記[Theory]案）。

**経路Y：SelectedSheetセッタ内部のみで対処**
（例：セッタが外部から旧値を注入されるオーバーロードを持つ、あるいはgetter自体をキャッシュする等）
→ 効果はTraceLogログにのみ現れ、通常のPropertyChangedイベント（`PropertyChangedEventArgs`はPropertyNameのみ保持）では検証不能。**この場合はP-015既存前例（finding3同型）に倣い、RED証明は成立せず隠密静的レビューのみで検証する。**

**設計としての推奨**：経路Xを優先候補として提示する。理由：(1) `wasEmpty`という前例が既にありコスト増が小さい、(2) テスト可能性が生まれ次回の再発を機械的に防げる、(3) `SelectedSheet`セッタ自体の汎用的振る舞い（`RenameCommand`等の他の呼び出し元）に影響を与えない局所修正で済む。ただし最終的な実装方式の決定は侍に委ねる。

### テストケース設計（経路Xが採用された場合）

対象関数（仮称、侍の切り出し次第で実名は変わる）：「追加前のSheets状態から、AddCommand完了後にOnPropertyChangedへ渡すべきoldValueを決定する」ロジック。

**同値分割**
- 有効域A：追加前Sheets.Count == 0 → oldValueはnull
- 有効域B：追加前Sheets.Count >= 1 → oldValueは追加前のSelectedSheet（`Sheets[CurrentSheetIndex]`）

**境界値分析**
- Count = 0（下限）
- Count = 1（下限+1、初回追加後2回目の追加操作）

**[Theory]案**
```
[Theory]
[InlineData(0)]  // wasEmpty=true相当: 追加前0枚 → oldValueはnull
[InlineData(1)]  // 追加前1枚 → oldValueは既存の1枚目
[InlineData(3)]  // 追加前3枚(選択中が末尾以外のケースも含め) → oldValueは選択中のシート
public void AddCommand_NotifiesSelectedSheetChange_WithCorrectOldValue(int sheetsBeforeAdd)
```
期待値：`sheetsBeforeAdd == 0`のときnull、それ以外は「追加前に選択されていたシート参照」と一致すること。

**検証方法（TraceLog非経由の場合）**：`SheetNavigationViewModel.PropertyChanged`イベント購読では標準`PropertyChangedEventArgs`にOldValueが乗らないため、経路Xで切り出された関数を直接呼び出すユニットテスト、または経路Xの実装が`ViewModelBase.OnPropertyChanged(name, oldValue)`を呼ぶ直前の値を何らかの形でテストから観測できる設計（例えば当該関数を`internal`にしてリフレクションアクセス、既存の`TraceLog`同様のIVTパターンを流用）が必要。侍の実装設計と合わせて具体化する。

### 対称性点検

`RenameCommand`・`DeleteCommand`は本バグ修正の対象外（残存3箇所はP-044として別途起票済み、殿判断待ち・着手禁止）。ただし今回の修正がAddCommand側の実装変更を伴う場合、No.2・No.3（`wasEmpty=false`経路）が退行しないことを回帰確認の観点として含める。

---

## 指摘1：NormalizeFullWidthの不対サロゲート例外

### あるべき振る舞い（仕様）

`TraceLog`の設計方針（ファイル内の他の全メソッドWrite/LogPropertyChangedが体現する「トレースログ機構の失敗が本来の処理を道連れにしてはならない」というベストエフォート原則）に合わせ、`NormalizeFullWidth`（またはその呼び出し元`Initialize()`）は、不正なUTF-16文字列（不対サロゲート）を含む環境変数値を受け取っても**例外を投げてはならない**。

変換後の具体的な文字列内容（不正文字をどう扱うか＝除去/置換/そのまま）までは本設計では規定しない（実装の自由度として侍に委ねる）。**必須の検証観点は「例外が発生しないこと」のみ**とする。

### 同値分割

- 有効域：正常なUTF-16文字列（半角英数字、全角英数字、空文字列）——既存`TraceLogTests.cs`の8ケースでカバー済み、対象外
- 無効域：不正なUTF-16文字列（単独high surrogate、単独low surrogate、正常文字列中への混在）

### 境界値分析

| No | 入力 | 説明 |
|---|---|---|
| 1 | `"\uD800"` | 単独high surrogate（U+D800、下限） |
| 2 | `"\uDBFF"` | 単独high surrogate（上限） |
| 3 | `"\uDC00"` | 単独low surrogate（下限） |
| 4 | `"\uDFFF"` | 単独low surrogate（上限） |
| 5 | `"false\uD800"` | 正常値の末尾に不対サロゲート混在 |
| 6 | `"\uD800false"` | 正常値の先頭に不対サロゲート混在 |

### [Theory]案

```
[Theory]
[InlineData("\uD800")]
[InlineData("\uDBFF")]
[InlineData("\uDC00")]
[InlineData("\uDFFF")]
[InlineData("false\uD800")]
[InlineData("\uD800false")]
public void NormalizeFullWidth_DoesNotThrow_OnIllFormedSurrogates(string input)
{
    var exception = Record.Exception(() => Invoke(input));
    Assert.Null(exception);
}
```
（`Invoke`は既存`TraceLogTests.cs`のリフレクションヘルパーを流用）

このテストは文字列リテラルのみで再現可能であり、TraceLog経由の観測は不要。**RED先行証明が成立する**（現状の実装＝try/catch非保護のままこのテストを実行すれば`ArgumentException`でREDになるはず、修正後はGREENになる）。

### 対称性点検

指摘1は指摘2と異なり、環境変数入力→純粋関数出力という単純な構造のため、既存の8ケース（全角ラテン文字・全角数字の正常系）と対称の形で無効域6ケースを追加するのみでよい。既存`[Theory]`への追加が自然（新規テストクラス不要）。

---

## 不明点

- 指摘2の経路X／経路Yどちらを侍が採用するかは実装判断次第であり、経路Yの場合はRED証明が成立しない（P-015既存前例踏襲）。最終的なテスト可否は侍の実装方針確定後に再確認が必要。

## 派生提案の有無

なし。
