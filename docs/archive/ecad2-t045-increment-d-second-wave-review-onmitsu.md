# T-045増分D第2波（ConfirmDrag/CancelDrag骨格を4種全てへ展開）差分再レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`9225c3a`（`refactor(app): T-045増分D第2波 ConfirmDrag/
> CancelDrag骨格を4種全てへ展開`、親`0375459`）。前回レビュー（`docs/archive/ecad2-t045-increment-d-review-onmitsu.md`）のDoD(6)実査（侍PoC所見の誤認発覚）を受け、家老が展開見送りを撤回し
> 采配した第2波。`code-review`スキル（8角度、6エージェント並行、1-vote検証2件）をhigh effortで
> 併用。実測検証（`dotnet test`全件・ドラッグ関連フィルタ実行）も併用した。

---

## 結論：**機能面クリーン（挙動等価・実測208件全合格）。DoD(2)は既存4種について実効解消したが、
型システムでの完全強制ではなく「規約レベルの誘導」に留まる点を正直に報告する**

DoD(1)(3)(4)(5)は出典付きで確認でき、機能バグは0件（`code-review`のAngle A/B+Cいずれも空）。
DoD(2)（重点予告どおり）については、既存4種の所見Y型再発防止は実効化されたが、将来の拡張性に
関する完全な保証には至っていないという評価をCONFIRMED所見として報告する。

---

## DoD(1)(3)(4)(5) の検証結果

### (1) 展開分Confirm/Cancel挙動等価性

`ConfirmDragConnector`/`CancelDragConnector`・`ConfirmDragWireBreak`/`CancelDragWireBreak`・
`ConfirmDragFreeLine`/`CancelDragFreeLine`の新実装（`ConfirmDrag<T>`/`CancelDrag<T>`呼び出し）を
旧実装（削除された行）と1対1で比較した。フィールド名・比較演算子・代入内容・処理順序（旧実装の
`is VerticalConnector c`型パターンマッチは、フィールドが既に`VerticalConnector?`等の具象型で
宣言されているため単なるnullチェックへの置換で意味的に等価）いずれも完全一致。`code-review`
Angle Aでも「コピペミスによるフィールド名・演算子の食い違いは無い」ことを確認済み。

### (3) 実装パターン統一（前回所見K該当部分の解消）

前回指摘した所見K「ConnectionDotのみPoC、Connector/WireBreak/FreeLineは旧インライン実装のまま」
という混在は、4種全てが`ConfirmDrag<T>`/`CancelDrag<T>`経由に統一されたことで解消した。ただし
`code-review`で新たに、**`ForceCancelIfAny`（`Func<bool>`/`Action`ベース）と`ConfirmDrag<T>`/
`CancelDrag<T>`（`ref T?`ベース）という別軸の2パターン併存は残る**ことが指摘された（詳細後述、
PLAUSIBLE）。これは今回のコミットのスコープ（Confirm/Cancel統一）外であり、意図的な設計判断
（XMLコメントに「isActive/cancel/notifyを型ごとに明示的に渡す構造にすることで書き忘れがレビュー
で見えやすくなる」と明記）と確認できるため妥当と判断する。

### (4) UpdateDrag*不可侵・スコープ

`UpdateDragConnector`/`UpdateDragWireBreak`/`UpdateDragFreeLine`はdiffに含まれず不可侵。
`git show 9225c3a --stat`で変更ファイルが`MainWindowViewModel.cs`のみであることを確認。

### (5) dotnet test実測

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 194、スキップ: 0、合計: 194 - Ecad2.App.Tests.dll
```
Core14+App194＝208件全合格。`--filter "FullyQualifiedName~Drag"`でドラッグ関連96件も全合格を
実測確認。

---

## DoD(2)【重点】所見Y型構造的再発防止の実効化 — 既存4種は実効解消、将来拡張への保証は限定的

### 実効化した部分

4種全ての`CancelDragXxx()`が`CancelDrag<T>(ref T? dragging, Action<T> restore)`
（`MainWindowViewModel.cs:902-906`付近）経由になった：

```csharp
private void CancelDrag<T>(ref T? dragging, Action<T> restore) where T : class
{
    if (dragging is not null) restore(dragging);
    dragging = null;
}
```

`ForceCancelDragXxxIfAny()`（4箇所）は引き続き`ForceCancelIfAny(isActive, cancel=CancelDragXxx,
notify)`という形で`CancelDragXxx`をそのまま`cancel`引数に渡す。これにより「`SelectedXxx`の
setter → `ForceCancelDragXxxIfAny` → `ForceCancelIfAny` → `cancel()`=`CancelDragXxx()` →
`CancelDrag<T>`の`restore`必須構造」という連鎖が**4種全てで確立**されたことを確認した
（verify: CONFIRMED、`CancelDragConnector`L391-392／`CancelDragWireBreak`L555-556／
`CancelDragFreeLine`L731-732／`CancelDragConnectionDot`L915-916いずれも`CancelDrag<T>`経由）。

### 実効化していない部分（正直な報告）

`ForceCancelIfAny(Func<bool> isActive, Action cancel, Action notify)`の`cancel`引数は単なる
`Action`型であり、`CancelDrag<T>`経由であることを要求する型制約・インターフェースは存在しない
（`MainWindowViewModel.cs:309-314`）。実装コメント自身（302-307行付近）も「cancel呼び出しの
書き忘れが**レビューで見えやすくなる**」と述べるのみで、コンパイラ強制ではなく人的レビューへの
依存を設計者自身が認めている。

**評価（`code-review` Altitude、verify: CONFIRMED）**：既存4種については実効解消したが、将来
第5のドラッグ可能型が追加される際、開発者が`CancelDrag<T>`を使わず「復元を伴わない」独自の
`CancelDragXxx`を書いても、`ForceCancelIfAny`はそれをコンパイルエラーなく受理してしまう。これは
「新規追加時に自動的に復元が効く」という型システムレベルの保証ではなく、「4種の既存実装という
模範例に倣えば安全」という**規約レベルの誘導**に留まる。所見Yの再発を完全にゼロにする設計
（例：`ForceCancelIfAny`自体を`ref T?`＋`CancelDrag<T>`ベースに統合する等）は、さらなる
リファクタが必要であり、これは今回のスコープを超える。**機能バグではないが、T-045クローズの
総括として正直に記録する。**

---

## code-review追加指摘（PLAUSIBLE、対応は任意判断）

**所見M：`ForceCancelIfAny`系と`ConfirmDrag<T>`/`CancelDrag<T>`系という2つの抽象化パターンの
併存**（前回所見Kの残存部分）
今回のスコープ外（Confirm/Cancel統一が目的）であり、意図的な設計判断（コメントに明記）と確認
できるため、経過観察が妥当。統合するとすれば`ForceCancelIfAny`自体を`ref T?`ベースへ再設計する
ような、さらに大きなリファクタが必要になる。

---

## 家老への確認事項

1. DoD(2)所見（既存4種は実効解消、将来拡張への型システム保証は限定的）：機能バグではないため
   増分Dクローズ自体は妨げないと判断するが、T-045総括として正直に報告する。追加対応
   （`ForceCancelIfAny`自体の再設計等）は費用対効果次第、家老の判断に委ねる。
2. 所見M（2パターン併存の残存）は経過観察でよいと判断する。

増分D第2波は機能面でクリーンなため、忍者実機検証（4種ドラッグ回帰一巡）へ回してよいと判断
する。T-045クローズ判定は忍者確認後、家老の裁定を仰ぐ。
