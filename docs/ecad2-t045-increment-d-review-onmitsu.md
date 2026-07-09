# T-045増分D（ドラッグ外枠共通化：ForceCancelIfAny+ConnectionDot PoC）静的レビュー（隠密）

> 2026-07-09 隠密レビュー。対象コミット`10b350c`（`refactor(app): T-045増分D ドラッグ外枠共通化
> (ForceCancelIfAny+ConnectionDot PoC)`、親`fec5022`）。T-045最終増分のため念入りに実施。
> `code-review`スキル（8角度、7エージェント並行、1-vote検証5件）をhigh effortで併用。実測検証
> （`dotnet test`全件・ドラッグ関連フィルタ実行）も併用した。

---

## 結論：**機能面はクリーン（挙動等価・実測208件全合格）。ただし計画書DoD文言と実装の防御力に
ギャップがあり、DoD(6)については侍のPoC所見を覆す技術的材料がある——事実として報告する**

DoD(1)(3)(4)(5)は出典付きで確認でき、機能バグは0件（`code-review`のAngle A/B/C/Conventions
いずれも空）。ただしDoD(2)についてはCONFIRMED所見あり（計画書のDoD文言と実装の実効性に
ギャップ）、DoD(6)についてもCONFIRMED所見あり（侍のPoC所見が実装の実態と食い違う）。いずれも
**機能バグではなく、設計の実効性・所見の正確性に関する指摘**であり、増分Dのクローズ自体を
妨げるものではないと判断するが、T-045最終増分として正直に報告する。

---

## DoD(1)(3)(4)(5) の検証結果

### (1) ForceCancelIfAny共通ヘルパーの挙動等価性

```csharp
private void ForceCancelIfAny(Func<bool> isActive, Action cancel, Action notify)
{
    if (!isActive()) return;
    cancel();
    notify();
}
```
（`MainWindowViewModel.cs:309-314`付近）。4箇所（Connector/WireBreak/FreeLine/ConnectionDot）の
`ForceCancelDragXxxIfAny()`が、それぞれ対応する型のdelegateを正しく束縛してこのヘルパーへ委譲
していることを確認した（`code-review` Angle A確認済み）。旧実装の`if (_draggingXxx is null)
return; CancelDragXxx(); OnPropertyChanged(nameof(IsDraggingXxx));`と処理順序・呼び出し内容が
完全に一致し、挙動等価。呼び出し元（各`SelectedXxx`のsetter）はシグネチャ変更なし。

### (3) UpdateDrag*クランプロジック不可侵

`UpdateDragConnector`（343-380行）・`UpdateDragFreeLine`（687-730行）等、実クランプロジックを
含む関数はdiffに一切含まれておらず不可侵。今回の変更はConfirm/Cancelのみを対象としている。

### (4) スコープ確認

`git show 10b350c --stat`で変更ファイルが`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`のみ
であることを確認。

### (5) dotnet test実測

```
成功! -失敗: 0、合格: 14、スキップ: 0、合計: 14 - Ecad2.Core.Tests.dll
成功! -失敗: 0、合格: 194、スキップ: 0、合計: 194 - Ecad2.App.Tests.dll
```
Core14+App194＝208件全合格。加えて`dotnet test --filter "FullyQualifiedName~Drag"`でドラッグ
関連テストのみを実行し**96件全合格**を実測確認。コミットメッセージの報告と一致。

---

## DoD(2) 所見Y型再発防止の実効性 — CONFIRMED（計画書DoD文言と実装の防御力にギャップ）

`docs/ecad2-t045-implementation-plan-samurai.md:149-150`のDoD文言：

> 外枠共通化により「所見Y型」のような欠陥が構造的に再発しにくくなっていること（**新規追加時に
> 共通ヘルパー経由で自動的に復元が効く設計**）

しかし実装された`ForceCancelIfAny(Func<bool> isActive, Action cancel, Action notify)`は、
`isActive→cancel→notify`という**呼び出し順序**を強制するのみで、`cancel`として渡される
`Action`が実際に「開始時位置への復元」を行うかどうかは型システムでは一切検証されない。
実装コメント自身（`MainWindowViewModel.cs:305-306`付近）も「cancel呼び出しの書き忘れが
**レビューで見えやすくなる**」と述べており、型システムでの保証ではなく人的レビューへの依存を
前提としている。将来第5の状態機械が追加された際、開発者が誤って「null化のみでrestoreを伴わ
ない」独自ロジックを`cancel`に渡しても、コンパイルは通り、レビューで見逃されれば所見Yと
同型のバグが再発しうる。

対照的に、ConnectionDot専用の`CancelDrag<T>(ref T? dragging, Action<T> restore)`
（`MainWindowViewModel.cs:921-925`付近）は`if (dragging is not null) restore(dragging);
dragging = null;`という構造をヘルパー内部に持ち、`restore`呼び出しを省略できない、より強い
設計になっている。この設計は現状ConnectionDotのみに適用されたPoCであり、他3種には及ばない。

**評価**：計画書の「自動的に復元が効く」という表現が示唆する型システムレベルの保証は、
`ForceCancelIfAny`単体では達成されていない（呼び出し順序の集約という意味での「構造化」は
達成しているが、「復元の中身」までは保証しない）。`CancelDrag<T>`パターンの方がこの目的には
より適合するが、今回はPoC止まり。これは**機能バグではない**（現状4箇所とも正しく`CancelDragXxx`
を渡しており、所見Yが再発しているわけではない）が、計画書のDoD文言と実装の実効性に差がある
ことは家老・殿への報告価値があると判断する。

---

## DoD(6) 横展開見送り妥当性 — 侍のPoC所見を覆す技術的材料あり

コミットメッセージの侍所見：

> 一方Connector（端点リサイズ/本体移動の分岐、3フィールド）・FreeLine（水平/垂直判定・端点
> リサイズの分岐、4フィールド）は、hasChanged/restore delegateの中身が分岐を抱えて複雑化し、
> 共通化の可読性メリットが薄れる可能性がある

実際に`ConfirmDragConnector`/`CancelDragConnector`（385-404行）・`ConfirmDragFreeLine`/
`CancelDragFreeLine`（739-757行）を読んだところ、**いずれも`if`/`else`/`switch`を含まない
単純なフィールド比較・代入のみ**（ConnectionDotと完全に同型、フィールド数が2→3・2→4に増える
だけ）であることを確認した。分岐が実在するのは`UpdateDragConnector`（343-380行、
`_draggingConnectorIsEndpoint`による端点リサイズ/本体移動の分岐）・`UpdateDragFreeLine`
（687-730行、`isHorizontal`等による分岐）であり、これらは**DoD(3)によりスコープ外**とされた
Update系メソッド。

**この食い違いは、独立した3系統（隠密の一次調査・`code-review`のReuse+Simplification角度・
1-vote検証エージェント）が揃ってCONFIRMEDと判定した**。侍のPoC所見はConfirm/Cancel（展開対象）
とUpdate（スコープ外）を混同している可能性が高い。

**評価**：技術的には、Connector/FreeLineへの`ConfirmDrag<T>`/`CancelDrag<T>`展開は、
ConnectionDotと同程度の複雑さ（フィールド数の増加のみ）で可能と考えられる。これは家老の
「展開見送り（将来課題化）」という仮決定を**覆しうる技術的材料**である。ただし、T-045が
既に4増分に渡り長期化していること、今回のPoC（ConnectionDot）だけでも所見Yの構造的再発防止
（呼び出し順序レベル）は4型全てに適用済みであることを踏まえると、「今すぐ展開すべき」という
結論までは導かない——展開の要否は家老の裁定に委ねる。

---

## code-review追加指摘（PLAUSIBLE、対応は任意判断）

**所見K：2つの実装パターンの混在**
`ForceCancelIfAny`（`Func<bool>`/`Action`ベース）と`ConfirmDrag<T>`/`CancelDrag<T>`（`ref T?`
ベース）という、同じ「ドラッグ状態機械の共通骨格」に対する2つの非互換な抽象化手法が併存する。
検証の結果、`ForceCancelDragConnectionDotIfAny()`が`CancelDragConnectionDot()`をActionとして
渡す経路には実際の結線破綻はない（`CancelDragConnectionDot()`の外部シグネチャは`ConfirmDrag<T>`
経由に変わっても不変のため）が、将来他3種へ`ConfirmDrag<T>`/`CancelDrag<T>`を展開する際、
どちらのパターンに倣うべきか自明でないという設計一貫性の課題は残る。

**所見L：SelectedCellクリック毎のデリゲートアロケーション**
`SelectedCell`のsetterは通常のセルクリックのたびに4型全ての`ForceCancelDragXxxIfAny()`を呼ぶ
ため、最大12個の短命デリゲートがクリック毎にヒープ確保される（旧実装は完全アロケートフリー
だった）。実測・分析の結果、Gen0で即回収される数十バイト規模でありユーザー体感には出ない
実害の無い指摘と判断する。

### REFUTED（1件）
`ForceCancelDragConnectionDotIfAny`のisActiveと`CancelDrag<T>`内部のnullチェックが「ConnectionDot
のみ」二重ガードになっているという候補は誤り。実際は4型全てで同型の二重ガード構造（外側
`isActive`＋内側`CancelDragXxx`内の非nullガード）が存在し、ConnectionDot固有の非対称性ではない。

---

## 家老への確認事項

1. DoD(2)所見（計画書DoD文言と実装の防御力のギャップ）：機能バグではないため増分Dクローズ
   自体は妨げないと判断するが、事実として報告する。対応要否（例：ForceCancelIfAnyのコメントを
   より正確な表現に修正する等）は家老の判断に委ねる。
2. DoD(6)所見（侍PoC所見と実装の食い違い、展開見送りを覆しうる材料）：技術的には展開可能だが、
   展開の要否・時期は家老の裁定を仰ぐ。
3. 所見K・Lは経過観察でよいと判断する。

増分Dは機能面でクリーンなため、忍者実機検証（4種ドラッグ回帰一巡）へ回してよいと判断する。
