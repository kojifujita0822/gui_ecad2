# T-082 修正1再修正 テスト設計書(隠密起草・往復3周目)

- 題目: MoveSheetCommandの「所見L型再発」修正1再修正に対するテスト設計
- 起草者: 隠密
- 前提: `docs/ecad2-t082-sheet-reorder-review2-onmitsu.md`でCONFIRMEDした欠陥(修正1のガードが「添字変化」を判定基準にしており、「移動対象=選択中シート」という最も基本的な使い方で必ずSelectedCell・記入中ドラフトが消える)への再修正
- スコープ境界: 設計書のみ。実装はしない。侍が本設計に基づきテストコードへ落とす(設計にないテスト追加は自由、設計にあるものを勝手に省くのは不可)

## 1. 仕様(あるべき振る舞い)

**実体不変の原則**: `MoveSheetCommand`の実行によって、Document.Sheetsの**並び順**は変わるが、**シートの実体(オブジェクト参照)そのものが選択状態から外れることは無い**。移動対象が選択中シート自身であっても、他のシートであっても、「今開いている(選択中の)シート」という実体は常に同一である。

この原則から導かれる期待動作:

1. **CurrentSheetIndex(添字)は、選択中シートの実体の新しい位置へ正しく追従する**(添字自体は変わりうる)。
2. **SelectedCell(選択中セル)は、MoveSheetCommandの実行によって一切クリアされてはならない**(実体が変わらない以上、セル選択状態を破棄する理由が無い)。
3. **記入中ドラフト(コネクタ・自由線・接続点等)は、MoveSheetCommandの実行によって一切破棄されてはならない**(同上の理由)。
4. 上記1〜3は、**移動対象が選択中シート自身かどうかに関わらず**、常に同時に成立する(1つのテストで両立を確認する。追従の検証と保持の検証を別テストに分けると、片方の回帰を見逃す)。

## 2. 同値分割: 移動対象と選択中シートの関係

| パターン | 定義 | 既存テストの有無(review2時点) |
|---|---|---|
| P1: 移動対象=選択中シート自身 | `fromIndex == 選択中シートの旧添字` | CurrentSheetIndex追従のみ検証済み、**SelectedCell/ドラフト保持は未検証(今回の主眼)** |
| P2: 移動対象≠選択中、添字が間接シフト | 選択中シートは動かないが、他シート移動で添字がずれる | CurrentSheetIndex追従のみ検証済み、**SelectedCell/ドラフト保持は未検証** |
| P3: 移動対象≠選択中、添字も不変 | 選択中でない2シートの入替で、選択中シートの添字も不変 | SelectedCell保持は検証済み、**記入中ドラフト保持は未検証** |

CanMoveSheetのガード(`fromIndex != toIndex`)より、P1では新添字と旧添字が必ず異なる。P2・P3はシート枚数と移動位置の組み合わせで発生要否が決まる(下記境界値参照)。

## 3. 対称性点検表(このテストスイートが最終的に埋めるべきセル)

| 検証観点 | P1(移動対象=選択中) | P2(間接シフト) | P3(添字不変) |
|---|---|---|---|
| SelectedCell保持 | **新規** | **新規** | 既存(`WhenMovingUnrelatedSheets_DoesNotClearSelectedCell`) |
| 記入中ドラフト保持 | **新規** | **新規(任意、P1で代表すれば可)** | **新規(穴埋め、任意)** |
| CurrentSheetIndex追従(実体追跡) | 既存(`WhenMovingSelectedSheet_CurrentSheetIndexFollows`) — ただし**保持と同時アサート化が必要** | 既存(`WhenUnrelatedMoveShiftsSelectedSheetIndex_CurrentSheetIndexFollows`) — 同上 | 既存(`WhenMovingOtherSheet_SelectedSheetStaysSame`) |

「新規」セルが今回の必須追加分。「保持と同時アサート化」とは、既存の追従検証テストに`SelectedCell`保持のアサーションを追加統合するか、新規テストで両方を同時に見ることを指す(項番1の「両立」要求)。

## 4. 境界値分析

### 4.1 シート枚数の下限

`CanMoveSheet`は`Sheets.Count > 1`が前提。よって最小枚数は**2枚**。

- P1は2枚から成立する(2枚で唯一の移動=先頭⇔末尾の交換)。
- P2は**3枚から成立する**(3枚シート`[A,B,C]`でBを選択中(index1)、`Execute((0,2))`でAを末尾へ移動すると`[B,C,A]`となりBの新添字は0——3枚で間接シフトが発生する最小構成。4枚を待つ必要はない)。
- P3は3枚から成立する(選択中でない2シートの入替に最低3枚必要)。

### 4.2 移動位置(端/中間)・方向(上/下)

- 端(先頭→末尾、末尾→先頭): 最大距離の移動、CanMoveSheetの境界(`toIndex`が0または`Count-1`)
- 隣接移動(上下1つ): 最小距離の移動
- 中間シートの移動: 端でも隣接でもない代表点(枚数4以上で意味を持つ)

上移動(`toIndex < fromIndex`)・下移動(`toIndex > fromIndex`)の両方を、少なくとも端のケースで確認する(`CalculateSheetDropIndex`のoff-by-one系バグは方向で挙動が変わりうるため——既存のD&D側テストと同じ理由)。

### 4.3 選択中セル・記入中ドラフトの有無

- `SelectedCell`設定あり/なし(なしのケースは「元々nullなら実行後もnullのまま」という退行しにくい自明ケースなので優先度は低い。設計上は言及するが必須ケースからは除外してよい)
- 記入中ドラフト(コネクタドラフト`BeginConnectorDraft()`等)あり——P1の代表ケース1件で十分(P1で保持されるならP2/P3でも同一のクロスカット処理経由なので理屈上保持されるはずだが、**実装のクロスカット処理箇所がP1/P2/P3で分岐するなら全パターンでの確認が望ましい**。侍の実装が単一の分岐点(呼ぶ/呼ばない)に集約されるなら1件の代表確認で足りる、実装後にレビューで確認する)

## 5. 状態遷移としての整理

```
[選択中シート実体 X が存在する状態]
        |
        | MoveSheetCommand.Execute(fromIndex, toIndex)  [CanMoveSheetガード通過済み]
        v
[Xの並び順上の位置(添字)が変わりうる状態]
        - X自身が移動対象だった場合 → Xの新添字 = toIndex
        - Xが間接シフトした場合     → Xの新添字 = 移動前後でのIndexOf(X)差分
        - Xの添字が不変だった場合   → Xの新添字 = 旧添字

[許される遷移後の状態]
        - CurrentSheetIndex = Xの新添字 (実体追跡の結果)
        - SelectedCell = 遷移前の値のまま (不変)
        - 記入中ドラフト = 遷移前の値のまま (不変)

[許されない遷移後の状態(退行)]
        - SelectedCell = null (Xの添字が変わったことを理由にクリアされる) ← 今回の再修正対象
        - CurrentSheetIndexが誤った添字を指す(Xでない別シートを指してしまう)
```

## 6. 具体的テストケース一覧

### 6.1 P1(移動対象=選択中シート自身) — [Theory]で境界値網羅、必須

`SelectedCell`設定→対象シート自身を移動→**同一テスト内で** (a)`SelectedCell`が保持されていること、(b)`CurrentSheetIndex`が`toIndex`へ正しく追従していること、の両方をアサートする。

| # | シート枚数 | fromIndex | toIndex | 説明(境界) |
|---|---|---|---|---|
| 1 | 2 | 0 | 1 | 先頭→末尾(下移動・隣接・端の両方を兼ねる最小ケース) |
| 2 | 2 | 1 | 0 | 末尾→先頭(上移動・隣接・端) |
| 3 | 4 | 0 | 3 | 先頭→末尾(下移動・端・最大距離) |
| 4 | 4 | 3 | 0 | 末尾→先頭(上移動・端・最大距離) |
| 5 | 4 | 1 | 2 | 中間シートの隣接下移動(端でも最大距離でもない代表点) |
| 6 | 4 | 2 | 1 | 中間シートの隣接上移動 |

`[Theory]`+`[InlineData(sheetCount, fromIndex, toIndex, expectedNewIndex)]`形式を推奨(既存`CalculateSheetDropIndex_ComputesCorrectInsertionIndex`と同型のパラメタライズド活用)。

### 6.2 P1 — 記入中ドラフト保持(代表1件、[Fact])

コネクタドラフト(`BeginConnectorDraft()`)を開始した状態で、選択中シート自身を移動(例: 上記#1のfromIndex/toIndex)。実行後、ドラフトが破棄されていないこと(`ConfirmConnectorDraft()`が成功するか、あるいはドラフト状態を検証できる既存の手段)をアサートする。既存の増分5往復3周目のテスト(所見L由来のRenameCommand回帰テスト)で使われている検証手法があれば、それに倣うこと(隠密は該当テストの有無を実装時に侍が確認)。

### 6.3 P2(間接シフト) — SelectedCell保持を追加、[Fact]または[Theory]

3枚シート`[A,B,C]`、B選択中(index1)、`SelectedCell`設定。`Execute((0,2))`でAを末尾へ移動(`[B,C,A]`、Bの新添字0)。

- (a) `SelectedCell`が保持されていること
- (b) `CurrentSheetIndex`が0(Bの新添字)へ追従していること
- (c) `SelectedSheet`が引き続きBの実体を指すこと(既存観点の統合)

既存テスト`MoveSheetCommand_WhenUnrelatedMoveShiftsSelectedSheetIndex_CurrentSheetIndexFollows`(4枚構成)を流用・拡張してSelectedCellのアサーションを追加する形でよい(新規テストを別途起こしてもよい、設計上の必須は「P2でSelectedCell保持を検証すること」)。

### 6.4 P3(添字不変) — 記入中ドラフト保持の穴埋め(任意、[Fact])

既存`MoveSheetCommand_WhenMovingUnrelatedSheets_DoesNotClearSelectedCell`はSelectedCellのみ検証。対称性点検表の空欄(P3×記入中ドラフト)を埋めるため、同一シナリオでドラフト保持も追加確認することが望ましい(必須ではないが、対称性点検の観点から推奨)。

### 6.5 退行防止(修正2・3への影響確認)

新規テストではないが、実装後に以下の既存テストが全てGREENのまま維持されることを実装者(侍)が確認すること:

- `MoveSheetCommand_RaisesSelectedSheetPropertyChanged`(修正3の通知発火)
- `MoveSheetCommand_DispatchesSelectionSyncWithContextIdlePriority`(修正3の優先度)
- `MoveSheetCommand_WhenDiagnosticsExist_ClearsResultsAndShowsStatusMessage` / `_WhenNoDiagnostics_DoesNotOverwriteStatusMessage`(修正2)
- `MoveSheetCommand_RenumbersPageNumberSequentially` / `_MarksDirty` / `_DoesNotRecordUndoSnapshot` / `_OrderIsPreservedAcrossSaveAndLoad`(既存基本機能)

これらのテストのfromIndex/toIndexパラメータ(多くが`(0,1)`等でP1に該当)が、再修正後も期待通りCurrentSheetIndexを追従させ続けることを個別に確認する(既存テストの前提=「選択中シート自身の移動でもCurrentSheetIndexは追従する」という要求は変わらない。今回の再修正は「追従はするがクリア処理は伴わない」という経路を新設するものであり、追従自体を無くすものではない点に注意)。

## 7. RED先行証明への示唆

P1の新規テスト(6.1のTheory + 6.2のFact)は、**現行コード(review2時点)では必ずFAILする**(移動対象=選択中シートの場合、ガードが常にtrueになりSelectedCellが必ずクリアされるため)。よってこれらは前修正のようなgit stash等の追加操作なしに、素の状態でREDを実測できる。P2の拡張テスト(6.3)も同様に現行コードでFAILする。P3の穴埋め(6.4)は現行コードでもPASSする可能性が高い(記入中ドラフトのクリア処理がSelectedCellと同じ経路に統合されているなら未検証のままPASSしていた可能性がある、実装確認要)。

## 8. 派生提案の有無

なし(全てT-082往復3周目の範囲内)。
