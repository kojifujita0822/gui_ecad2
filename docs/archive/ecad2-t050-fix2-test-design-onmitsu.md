# T-050修正 往復2周目 テスト設計（隠密起草）

対象：`docs/ecad2-t050-fix-review-onmitsu.md`で発見した新規バグ2件（AddCommand/DeleteCommandの二重発火・old値不整合、ResetSheetsのタイミング不正確性）の修正。テスト設計と実装の分離【MUST】に基づき仕様側からの設計のみ。実装は侍へ委譲。

---

## あるべき振る舞い（仕様）

`SheetNavigationViewModel`の`Sheets`構成が変わる（追加・削除・丸ごと差し替え）操作は、いずれも**SelectedSheetのPropertyChangedをちょうど1回だけ発火し**、そのoldValueは**操作直前に実際にUIが認識していたSelectedSheet**と一致しなければならない。二重発火・「もっともらしいが誤った値」の先行発火は、いずれも不正な状態として排除する。

---

## テスト可能性の見極め（本丸）

### 問題の構造

`ViewModelBase.OnPropertyChanged(string, object? oldValue)`が渡す`oldValue`は`TraceLog.LogPropertyChanged`にのみ渡され、標準`PropertyChangedEventArgs`（`PropertyName`のみ保持）には反映されない。一方、**発火回数**自体は通常の`PropertyChanged`イベント購読で直接カウント可能（TraceLog非依存、既存インフラで即実施できる）。

つまり検証すべき2つの性質は、テスト容易性が異なる：

| 検証したい性質 | 観測手段 | テスト可能性 |
|---|---|---|
| 発火回数（ちょうど1回か） | `PropertyChanged`イベント購読、`e.PropertyName==nameof(SelectedSheet)`をカウント | **既存インフラで可能**（TraceLog非依存） |
| old値の中身 | `TraceLog.LogPropertyChanged`にのみ渡る | **フック新設が必要** |

### 推奨アプローチ：二層構成

**層1（優先・必須）：発火回数の直接検証**
今回のバグの核心は「1回のはずが2回発火する」ことなので、これ自体は既存インフラで強力に検証できる。`SheetNavigationViewModel.PropertyChanged`（または`MainWindowViewModel`経由）を購読し、対象操作中に`SelectedSheet`の変更通知が**ちょうど1回**であることをアサートする。これだけでも「二重発火の再発」を機械的に検出できる回帰テストとして高い価値がある。

**層2（推奨・old値の中身検証）：old値計算ロジックの純粋関数化＋ユニットテスト**
`AddCommand`が既に持つ`DetermineOldSelectedSheetForAdd`（1周目修正、経路X）の型を踏襲し、`DeleteCommand`・`ResetSheets`経由のケースでも「操作前の状態→あるべきoldValue」を導出する純粋関数を切り出せるなら、その関数はTraceLog非依存でユニットテスト可能。実装がこの形を取れば、層1（発火回数）と層2（値の正しさ）の組み合わせで、実質的に「正しい値がちょうど1回通知される」ことを検証できる（TraceLogフックなしで完結）。

**層3（層2が困難な場合の代替）：TraceLog/ViewModelBaseへの軽量テストフック**
もし修正がセッタ内部の状態に強く依存し純粋関数化が困難な場合、家老示唆の「テスト用シンク注入・internalフック」を検討する。案（実装の詳細は侍に委ねる）：
- `ViewModelBase.OnPropertyChanged(string, object?)`内、`TraceLog.LogPropertyChanged`呼び出しの前後に、テスト専用の軽量な記録（例：`internal`な直近通知履歴リスト、または`internal static event`）を追加する。
- 満たすべき要件：(a) 本番挙動に影響しない、(b) `TraceLog.IsEnabled`のON/OFFに依存せず常時観測できる（TraceLog自体はIsEnabled=falseなら早期returnするため、TraceLog内にフックを置く場合はIsEnabled有効化がテスト側で必要になる——`ViewModelBase`側に置けばこの依存を避けられる）、(c) 発火順序・回数・値のすべてを追跡できる。

**方針**：層1を必須、層2を強く推奨。層3は層2で対応しきれない箇所（もしあれば）にのみ適用する。P-044既存前例（TraceLog経由のみで検証、RED証明不可）に逆戻りするのは層2が完全に不可能と判明した場合の最終手段とする。

---

## 状態遷移表・境界値分析（5ケース＋境界値展開）

### ケース1: AddCommand 0→1（wasEmpty）

| 項目 | 内容 |
|---|---|
| 遷移前 | `Sheets.Count=0`, `SelectedSheet=null` |
| 操作 | `AddCommand`実行 |
| 遷移後 | `Sheets.Count=1`, `SelectedSheet=S1`(新規) |
| あるべき発火回数 | **1回** |
| あるべきoldValue | `null` |
| 現状(1周目修正後) | 2回発火、1回目=old==new(誤)、2回目=old=null(正) |

### ケース2: AddCommand N→N+1（境界値：N=1, N=3）

| 項目 | 内容 |
|---|---|
| 遷移前 | `Sheets.Count=N`(N≥1), `SelectedSheet=Sx` |
| 操作 | `AddCommand`実行 |
| 遷移後 | `Sheets.Count=N+1`, `SelectedSheet=S(N+1)`(新規) |
| あるべき発火回数 | **1回** |
| あるべきoldValue | `Sx`（追加前の選択シート） |

### ケース3: DeleteCommand（境界値：先頭削除／中間削除／末尾削除／2→1枚の下限）

`Sheets=[A,B,C]`を基準に4パターン：

| No | 削除対象 | 削除前index | 遷移後Sheets | `Math.Min(index,Count-1)` | 遷移後SelectedSheet(new) | あるべきoldValue |
|---|---|---|---|---|---|---|
| 3a | A(先頭, index0) | 0 | [B,C] | Min(0,1)=0 | B | A |
| 3b | B(中間, index1) | 1 | [A,C] | Min(1,1)=1 | C | B |
| 3c | C(末尾, index2) | 2 | [A,B] | Min(2,1)=1 | B | C |
| 3d | 下限：[A,B]でB(index1)削除 | 1 | [A] | Min(1,0)=0 | A | B |

いずれも**あるべき発火回数=1回**、あるべきoldValueは削除対象のシート自身（`DeleteCommand`内で既にローカル変数`sheet`として保持されている値と一致するはず）。

### ケース4: ResetSheets経由（ReplaceDocument、新規/開く）

| 項目 | 内容 |
|---|---|
| 遷移前 | 旧Document：`Sheets.Count=3`(X1,X2,X3)、`CurrentSheetIndex=1`(X2選択中) |
| 操作 | `LoadFromFile`等→`ReplaceDocument`→新Document(Y1,Y2) |
| 遷移後 | `Sheets.Count=2`(Y1,Y2)、`SelectedSheet=Y1`(index0) |
| あるべき発火回数 | 1回（`SelectedSheet`について。`ReplaceDocument`全体では他プロパティも発火するが、本設計では`SelectedSheet`のみに着目） |
| あるべきoldValue | `X2`（旧Documentで実際に選択されていたシート、`Sheets[0]`(X1)ではない） |

境界値：旧Documentが1枚のみ（選択中もそれ1枚）、旧Documentの末尾が選択中、のケースも同型で追加。

### ケース5: 回帰確認

**5a. RenameCommand**：`Selected=X`→改名→`Selected=X`（同一参照）。あるべき発火回数=1回、あるべきoldValue=`X`（old==newは意図通り、バグではない）。

**5b. wasEmpty純粋関数契約**：既存`DetermineOldSelectedSheetForAdd_ReturnsNullOnlyWhenSheetsEmpty`（`[Theory]` 0/1/3枚）が今回の修正後も変わらずGREENのままであること。

**5c. CurrentSheetIndexセッタの汎用呼び出し経路**（P-030教訓を踏まえた追加観点）：`CurrentSheetIndex`セッタは「常時無条件でクロスカットクリアを実行する」設計が確立済み（T-041増分5往復3周目の教訓、`MainWindowViewModel.cs:106-124`のコメント参照）。この設計自体は今回変更対象ではない。AddCommand/DeleteCommand**以外**の呼び出し経路（DRC出力パネルのジャンプ、シートタブ直接クリック等）で、`CurrentSheetIndex`セッタが従来どおり動作し続けること（1回発火・正しいold値）を回帰確認する。

---

## [Theory]設計案

### 発火回数検証（層1、PropertyChangedイベント購読）

```
[Theory]
[InlineData(0)]  // ケース1: wasEmpty
[InlineData(1)]  // ケース2: N=1
[InlineData(3)]  // ケース2: N=3
public void AddCommand_RaisesSelectedSheetChanged_ExactlyOnce(int sheetsBeforeAdd)
{
    // Arrange: sheetsBeforeAdd枚のシートを持つvmを用意
    int fireCount = 0;
    vm.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(vm.SelectedSheet)) fireCount++; };

    // Act: AddCommand実行

    Assert.Equal(1, fireCount);
}
```

同型で`DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce`（ケース3a〜3d、`[Theory]`で4パターン）、`ResetSheets経由`（ケース4）、`RenameCommand_RaisesSelectedSheetChanged_ExactlyOnce`（ケース5a）を設計する。

### old値検証（層2、純粋関数が切り出された場合）

`DeleteCommand`用（案）：
```
[Theory]
[InlineData(/* [A,B,C], 削除対象=A */)]
[InlineData(/* [A,B,C], 削除対象=B */)]
[InlineData(/* [A,B,C], 削除対象=C */)]
[InlineData(/* [A,B], 削除対象=B(下限) */)]
public void DetermineOldSelectedSheetForDelete_ReturnsDeletedSheet(...)
```
（関数シグネチャは侍の実装次第。要求は「削除対象そのものを返す」という単純な契約）

`ResetSheets`用（案）：`ReplaceDocument`呼び出し前の`(CurrentSheetIndex, Sheets)`から旧SelectedSheetを導出するロジックが切り出せるなら、同型の`[Theory]`で境界値（Count=1/複数、index=0/末尾）を検証する。

---

## 不明点

- 層2（純粋関数切り出し）が`DeleteCommand`・`ResetSheets`双方で実現可能かは、侍の修正方針（`docs/ecad2-t050-fix-review-onmitsu.md`が提案した「CurrentSheetIndexセッタのネスト通知を経由させない」方向を採るか等）に依存する。方針確定後、層2の対象範囲を再確認する必要がある。
- ケース4（ResetSheets経由）のテストは`ReplaceDocument`（`MainWindowViewModel`のprivateメソッド）を`LoadFromFile`/`NewDocument`等の公開経路から間接的に駆動する必要があり、既存テストのセットアップパターン（もしあれば）に倣う。

## 派生提案の有無

なし。
