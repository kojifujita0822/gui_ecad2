# T-134観点4 根本原因調査（文書情報ダイアログでOKするとRedo履歴が消える）（隠密、2026-07-28）

**侍の診断は見ておらぬ**（家老の采配どおり、忍者の観測のみを材料に独立に一次ソースへ当たった）。

家老采配7（Wチェック）。**調査のみ。`src/`・`tests/`への書き込みは行っておらぬ。**

---

## 0. 結論

**機序を特定した。原因＝`DocumentInfo.Date`フィールドのみが`null`許容型で、`DocumentInfoDialog`の
往復（読込→表示→書戻し）で`null`が`""`（空文字列）へ変換されてしまい、`ApplyDocumentInfo`の
同値ガード（単純な`==`比較）が「変化なし」と判定できなくなること。**

**帰属＝「同値ガードが効かぬ」の側**（忍者の2択のうち）。ガード自体（8フィールド比較の実装）は
正しいが、**比較する2つの値のうち片方（ダイアログからの戻り値）が、比較前の時点で既に
`null`から`""`へ変換されてしまっている**ため、ガードの入力そのものが汚染されている。

---

## 1. 機序（一次ソース直読で確認）

### 1-1. `DocumentInfo`の8フィールド中、`Date`のみが`null`許容

`src/Ecad2.Core/Model/Document.cs:17-30`（`DocumentInfo`クラス全文）——

```csharp
public string CompanyName { get; set; } = "";
public string Title { get; set; } = "";
public string DrawingNo { get; set; } = "";
public string Customer { get; set; } = "";
public string Designer { get; set; } = "";
public string Drafter { get; set; } = "";
public string Checker { get; set; } = "";
public string? Date { get; set; }        // ← これだけ null 許容・既定値の初期化子も無い
public List<RevisionEntry> Revisions { get; set; } = new();
```

**7フィールドは`string`型で既定値`""`、`Date`だけが`string?`型で初期化子が無く既定値`null`**——
新規文書（`NewDocument()`→`new LadderDocument()`）では`Document.Info.Date`は**`null`のまま**
（`MainWindowViewModel.cs`を`Info.Date`・`Date =`でgrepしても、`NewDocument`等の初期化経路に
明示的な設定は無いことを確認済み）。

### 1-2. `DocumentInfoDialog`が読込時に`null`を`""`へ丸め、書戻し時に戻さない

`src/Ecad2.App/Views/DocumentInfoDialog.xaml.cs`（全44行、全文直読）——

```csharp
public DocumentInfoDialog(DocumentInfo current)
{
    ...
    DateBox.Text = current.Date ?? "";   // :23 ← null→""に丸める(表示のため)
    ...
}

private void OkButton_Click(object sender, RoutedEventArgs e)
{
    ...
    Result.Date = DateBox.Text;          // :40 ← ""をそのまま書き戻す。nullには戻さない
    DialogResult = true;
}
```

**`current.Date`が`null`であっても、`DateBox.Text`は必ず`""`（WPFの`TextBox.Text`は`null`を
取らない）。ユーザーが何も入力せずOKを押すと、`Result.Date`は`""`のまま`ApplyDocumentInfo`へ渡る。**

### 1-3. `ApplyDocumentInfo`の同値ガードが`null`と`""`を区別してしまう

`src/Ecad2.App/ViewModels/MainWindowViewModel.cs:277-295`——

```csharp
public void ApplyDocumentInfo(DocumentInfo info)
{
    var cur = Document.Info;
    if (cur.CompanyName == info.CompanyName && cur.Title == info.Title
        && cur.DrawingNo == info.DrawingNo && cur.Customer == info.Customer
        && cur.Designer == info.Designer && cur.Drafter == info.Drafter
        && cur.Checker == info.Checker && cur.Date == info.Date) return;   // :283

    UndoManager.RecordSnapshot(Document);   // :285 ← ガードを抜けるとここでRedo破棄
    ...
}
```

**`cur.Date`（`null`）と`info.Date`（`""`、ダイアログ経由）を`==`で比較すると、C#の文字列比較で
`null == ""`は`false`**——**両者は「同じ値」のはずなのに、型上は異なる値として扱われる**。
ゆえに7フィールドが完全一致していても、**`Date`だけが不一致と判定され、ガード全体が`false`（変化あり）
に倒れる**。

### 1-4. 忍者の観測との整合

| 観測（忍者） | 本調査の説明 |
|---|---|
| キャンセルならRedoは残る | `OkButton_Click`を通らずダイアログが閉じるため、`ApplyDocumentInfo`自体が呼ばれぬ |
| OKだと消える | `Date: null→""`の不一致でガードが素通りし、`RecordSnapshot()`（:285）が走りRedoスタックがClearされる |
| OK直後のUndo1回目が何も変えぬ | Undoが戻す先は「`Date`が`null`のままの状態」——**他の7フィールドは元々同値ゆえ何も変わって見えぬ**。`Date`が`""`→`null`に戻るのみで、UIに表示差が出ない |
| 2回目でX1が消えた | 2回目のUndoで初めて、その手前に積まれていた**本来のUndoポイント**（X1配置等）まで遡る |

**忍者の観測（4点）全てが、この機序で矛盾なく説明できる。**

---

## 2.【家老の問い3】P-145（`DiscardLastSnapshot`がRedoスタックに触れぬ件）と同根か——**別物**

**別の不具合であり、同根ではない**と判断する。

| | 本件（観点4） | P-145 |
|---|---|---|
| 経路 | `DocumentInfoDialog`→`ApplyDocumentInfo` | `CancelOrJoinTarget`（Esc）→`DiscardLastSnapshot` |
| 直接原因 | 同値ガードの比較対象（`Date`）が`null`/`""`の不一致で汚染される | `DiscardLastSnapshot`自体がRedoスタックへ一切触れない設計（そもそも見ていない） |
| 性質 | **ガードが効くべき場面で効かなかった**（誤検出＝false negativeでガードをすり抜けた） | **ガードの範囲外**（Undoスタックの破棄のみを扱う関数の設計上、Redo側は最初から対象外） |

**共通するのは「Redo履歴が意図せず失われる」という症状の見え方のみ**で、コード上の原因・経路とも
完全に独立している。**同根と扱うと誤った一括修正（片方の修正がもう片方に効くと誤認）を招きうる**
ため、別件として扱うべきと考える。

---

## 3. 対処の方向性（判断は侍・家老に委ねる、隠密は提案のみ）

原因が「`Date`フィールドの`null`/`""`不一致」に一点集約されるため、対処は小さく済む見込み。
案を2つ、優劣は付けずに示す（**設計判断ゆえ隠密が決めることではない**）。

1. **`ApplyDocumentInfo`の比較を`null`と`""`を同一視する形へ変える**
   （例＝`(cur.Date ?? "") == (info.Date ?? "")`、または`string.IsNullOrEmpty`同士の比較）
2. **`DocumentInfo.Date`を他7フィールドと揃えて非null化する**（既定値`""`にし、`string?`をやめる）
   ——ただし影響範囲（永続化・シリアライズ・他の`Date`参照箇所）の洗い出しが別途要る

**いずれの案でも`T134UndoCoverageTests.cs`の既存テスト（`文書情報_I3`系）が`SampleInfo()`で
`Date`に具体的な文字列を毎回設定しているため、この不具合を検出できていなかった**——**`Date`が
`null`のまま同値判定を試すテストケースが無い**ことが、T-134レビュー時に見逃された一因と考える
（**推測。テスト設計自体は隠密が起草した`docs/ecad2-t134-test-design-onmitsu.md`にこの境界値が
含まれていたかは未確認**）。

---

## 出典

- `src/Ecad2.Core/Model/Document.cs:17-30`（`DocumentInfo`全文）
- `src/Ecad2.App/Views/DocumentInfoDialog.xaml.cs`（全44行、全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:277-295`（`ApplyDocumentInfo`全文）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs:3191-3201`（`NewDocument`、`Info.Date`が
  未設定のまま既定値`null`で残ることの確認）
- `tests/Ecad2.App.Tests/T134UndoCoverageTests.cs:523-533`（`SampleInfo()`、`Date`に常に
  具体的な文字列を設定していることの確認、本日の采配2レビューで直読済みの内容を再利用）

## 不明点

- `docs/ecad2-t134-test-design-onmitsu.md`（隠密起草のテスト設計書）が`Date`の`null`初期値
  ケースを境界値として含んでいたか否かは、本調査では確認していない（設計書自体の再読が要る）
- ダイアログの他フィールド（7件）は既定値が`""`で揃っているため同型の不具合は生じないと判断したが、
  将来`DocumentInfo`へフィールドを追加する際に同型の`null`許容フィールドを増やすと再発しうる
  （**推測、横展開の一般論**）
