# T-133増分5 静的レビュー（コミット `e85b5e4`）

隠密（key=1785925652116）記す。2026-08-05。家老采配より。**深度＝軽量（1周目）。`code-review`スキルは用いておらぬ。**

**対象範囲**＝`git show e85b5e4`（5ファイル・197行追加・2行削除）。**未コミットの作業中コードは見ておらぬ。**

---

## 0. 総括

| # | 所見 | 重み | 落とし先 |
|---|---|---|---|
| **1** | **`SelectedCell` setterへの通知追加漏れ（4箇所中1箇所）** | **要修正** | 侍 |
| **2** | **通知を検証するテストが11件中0件**（所見1を捕らえる網が無い） | **要修正** | 侍 |
| 3 | `BreakerTypeOptions.All`の`"NFB"`が`Default`と独立リテラル | 軽微 | 侍の判断 |
| 4 | コミットメッセージに簡体字「增」1件（**コード内は0件**） | 軽微・作法 | 侍 |

**家老の観点2点への答え**——
- **観点1（`"NFB"`の散在）＝侍の申告どおり。散っておらぬ**（下記2-1）
- **観点2（早期returnの罠）＝侍の申告はsetter内部については正しい。されど罠は別の層に在った**（下記2-2・所見1）

---

## 1. 所見

### 所見1【要修正】選択切替時の通知が、4箇所のうち1箇所へ入っておらぬ

**箇所**＝`MainWindowViewModel.cs:494-511`／検索文字列＝`if (SetProperty(ref _selectedCell, value))`
（`SelectedCell`のsetter内、一括通知のブロック）。

**この並びに新設2プロパティ（`IsSelectedElementBreaker3P`／`SelectedElementBreakerType`）が無い。**

**数の再現手段**＝
```powershell
Select-String -Path <MainWindowViewModel.cs> -Pattern "OnPropertyChanged\(nameof\(<プロパティ名>\)\)"
```

| プロパティ | 件数 | 行 |
|---|---|---|
| `IsSelectedElementSelectSwitch` | **4** | 501, 2542, 2679, 3633 |
| `SelectedElementNotchPosition` | **4** | 502, 2543, 2680, 3634 |
| `IsSelectedElementLamp` | **4** | 503, 2546, 2683, 3637 |
| `SelectedElementLampColor` | **4** | 504, 2547, 2684, 3638 |
| `IsSelectedElementTimerRelated` | **4** | 505, 2548, 2685, 3639 |
| **`IsSelectedElementBreaker3P`** | **3** | 2544, 2681, 3635 |
| **`SelectedElementBreakerType`** | **3** | 2545, 2682, 3636 |

**4箇所の正体**（宣言行まで遡って確認済み）＝
(a)`SelectedCell`のsetter（`:446`） (b)`DeleteSelectedElement()`（`:2519`）
(c)`NotifySelectedElementChanged()`（`:2673`） (d)`ReplaceDocument()`（`:3548`）。

**侍は(b)(c)(d)へ足し、(a)を落とした。**
**挿入位置まで既存に倣うており**（`SelectSwitch`／`NotchPosition`の直後）、**(a)でも`:502`と`:503`の間へ2行足すのが正しい姿にござる。**

**【(a)が(c)へ帰着せぬことを確かめた】**——`SelectedCell`のsetter（`:443-512`を丸ごと直読）は
`NotifySelectedElementChanged()`を**呼んでおらぬ**。独自に`OnPropertyChanged`を並べておる。
**同メソッドの呼び出し元6箇所（`:2573` `:2635` `:3202` `:3283` `:3501` `:3804`）にも`SelectedCell` setterは含まれぬ。**
**ゆえに(a)は独立した経路にて、漏れは他の3箇所で埋め合わされておらぬ。**

**【時制＝既に起きておる。将来の条件待ちではない】**
セル選択が変わる経路（クリック・矢印キー等）で`SelectedCell`が更新されても、
**ComboBoxの`Visibility`（`IsSelectedElementBreaker3P`束縛）と選択値（`SelectedElementBreakerType`束縛）が再評価されぬ**——
**WPFのBindingは`PropertyChanged`が飛ばねば再評価せぬゆえ**にござる。

**【台帳DoDに直に触れる】** 殿裁可（`docs/todo.md:368`）＝
**「P-100＝プロパティパネル内ComboBox、Breaker3P選択時のみ出現・即時反映」**。
**「Breaker3P選択時のみ出現」は、まさに選択切替時の通知で成り立つ条項にござる。**

**【射程・某が自ら区切る】静的読解のみ。**
**実際に画面でComboBoxが出ぬ／古い値が残るところまでは見ておらぬ**（忍者の実機確認の領分）。
**構造として通知が飛ばぬことは確定しておるが、実害の現れ方は測っておらぬ。**

#### **パターン再発検知＝`PR-17候補`の2件目にござる**

`docs-notes/pattern-recurrence-log.md` `PR-17候補`の記述が本件そのもの——

> 新設`SelectedElementXxx`プロパティを、選択切替のたびに`OnPropertyChanged`を発火する**既存4箇所**
> （`SelectedCell`のsetter・`DeleteSelectedElement`・`NotifySelectedElementChanged`・Document差し替え処理）へ
> **追加し忘れ**、表示が更新されないまま……実害を招く

- **1件目**＝T-107（2026-07-21、`SelectedElementComment`新設時に**4箇所すべて**への追加漏れ）
- **本件＝2件目**（4箇所中1箇所への漏れ）

**台帳は「2件目が出れば正式パターン化検討」と記しておる。**
**併せて、制度化済みチェックリスト（`samurai.md`「新規選択可能状態の横展開チェックリスト」項目9）が
在りながらの再発**にて、**`PR-01`が既に抱えておる「チェックリストの実効性への疑義」と同じ形**にござる。
**制度化の是非は家老の領分ゆえ、事実の指摘に留める。**

### 所見2【要修正】通知を検証するテストが11件中0件——所見1を捕らえる網が無い

**テスト件数の内訳**（`T133Increment5BreakerTypeTests.cs`をケース単位で数え直した。**侍の申告11件と一致**）＝

| テスト | 形 | ケース数 |
|---|---|---|
| `IsSelectedElementBreaker3P_Breaker3P選択時true` | Fact | 1 |
| `IsSelectedElementBreaker3P_ContactNO選択時false` | Fact | 1 |
| `SelectedElementBreakerType_未設定なら既定値NFBを返す` | Fact | 1 |
| `..._選択肢の値は反映されUndo可能になる` | Theory | 2（MCCB／ELB） |
| `..._選択肢に無い値は変更しない` | Theory | 3（空／`nfb`／`XYZ`） |
| `..._既定値と同じ値を明示設定してもParamsへ書き込まずUndo履歴も作らない` | Fact | 1 |
| `..._値未変化ならUndo履歴を作らない` | Fact | 1 |
| `BreakerTypeChoices_選択肢一覧がBreakerTypeOptionsAllと一致する` | Fact | 1 |
| **計** | | **11** |

**すべてgetter/setterの値を直接読む形にて、`PropertyChanged`の発火を検める観点が一つも無い。**
**ゆえに所見1は、テストが全件緑のまま素通りする。**

**【「基盤が無い」ではござらぬ】**——`ViewModelBase`に**テスト専用フック`PropertyChangedForTest`が既に在る**
（検索文字列＝`internal event Action<string?, object?>? PropertyChangedForTest;`）。
**T-050往復2周目に隠密のテスト設計（層3）で新設されたもので、docコメントは
「発火回数だけでなく『正しい旧値がちょうど1回通知される』ことの単体検証に使う」と記す。**
**まさに本件のための器にござる。**

**【PR-27＝退化入力の型でもある】**
`IsSelectedElementBreaker3P_ContactNO選択時false`は`vm.SelectedCell`へ**同じ値`new GridPos(0,0)`を2度**設定しておる。
**2度目は`SetProperty`の早期returnで通知ブロックごとスキップされる経路**にて、
**まさに所見1が現れる場面を通っておりながら、getterを直接読むゆえ緑になる。**
**「実装のガードを一時的に壊してREDになるか」を試せば露見した類にござる。**

### 所見3【軽微】`BreakerTypeOptions.All`の`"NFB"`が`Default`と独立したリテラル

```csharp
// Element.cs（検索文字列＝public static class BreakerTypeOptions）
public const string Default = "NFB";
public static readonly string[] All = { "NFB", "MCCB", "ELB" };
```

**`Default`を改めても`All`は追随せぬ**——`All = { Default, "MCCB", "ELB" }` とすれば一元化が完全になり申す。
**現時点で値は一致しており挙動の食い違いは無い**ゆえ軽微にて、**採否は侍の判断に委ねる**。
`PR-06`（型強制不足＝規約レベルの誘導に留まる）の観点にござる。

### 所見4【軽微・作法】コミットメッセージに簡体字「增」1件。**コード内は0件**

**文字コードで判定した**（`增`＝U+589E／`増`＝U+5897）——

| 対象 | 増(U+5897) | 增(U+589E) |
|---|---|---|
| `Element.cs` | 14 | **0** |
| `MainWindow.xaml` | 68 | **0** |
| `MainWindowViewModel.cs` | 164 | **0** |
| `T133Increment5BreakerTypeTests.cs` | 1 | **0** |
| **コミットメッセージ** | 0 | **1** |

**【某の自己訂正】** **差分を目視した段では「コード内のコメントにも混入しておる」と見立てたが、
機械判定で己の誤りと分かった。** **コード内は全て正字にござる。**
**字形の判別は目視に頼ってはならぬ**——`CLAUDE.md`【MUST】が「執筆中に気づきにくい」と戒める所以を、
**検める側でも同じく踏んだ形**にござる。

**コミットメッセージの訂正には履歴の書き換えが要る**ゆえ、**採否は家老の判断を仰ぐ**。

---

## 2. 家老の観点2点の検分

### 2-1. 観点1（`"NFB"`が二箇所に文字列で散っておらぬか）＝**侍の申告どおり。散っておらぬ**

**再現手段**＝`src`配下（`.cs`＋`.xaml`、`obj/bin`除外）で `"NFB"|"MCCB"|"ELB"` を検索。

**リテラルの残存＝`Element.cs`の`BreakerTypeOptions`内のみ**（`Default`の1件＋`All`の3件＝所見3）。
**`DiagramRenderer.cs`に`"NFB"`のリテラルは1件も残っておらぬ**（在るのは`:1042`のコメントのみ）。
`MainWindowViewModel.cs`の`"NFB"`該当2件（`:2349` `:3237`）は**いずれもXMLコメント中の言及**にて、実コードではない。

**なお`"ELB"`が`DiagramRenderer.cs:307`／`if (variant == "ELB")`に在る**が、
**これは本コミット以前からの既存コード（形状分岐）で、差分に含まれておらぬ。**
**ELBの形状分岐は忍者の領分と家老が区切られた通り、某は判定を控える。**

### 2-2. 【家老が足した観点】`DiagramRenderer.cs:1046`の書き換えで描画挙動が変わっておらぬか＝**変わらぬ**

```diff
-var typ = ... ? t : "NFB";
+var typ = ... ? t : BreakerTypeOptions.Default;
```

**変更は1行のみ。`BreakerTypeOptions.Default`は`const string = "NFB"`にて値が同一**、
**条件式（`TryGetValue` ＋ `!string.IsNullOrEmpty`）にも手が入っておらぬ。**
**コミット全体の削除行は2行のみ**（`numstat`＝`DiagramRenderer.cs`が1行、`MainWindowViewModel.cs`が1行＝
XMLコメントの文末改訂）——**すなわち本コミットは実質「追加のみ」にて、既存の実行経路は構造的に不変にござる。**

### 2-3. 観点2（`SetProperty`早期returnの罠）＝**setter内部については侍の申告どおり。されど罠は別の層に在った**

**侍の申告**＝「get/setとも都度直読みゆえ該当せず」「前例`SelectedElementNotchPosition`も同型で該当せず」。

**一次ソースで検めた結果、この判断は正しい**——

- **getter**＝`SelectedElement?.Params.TryGetValue(...)` にて、**バッキングフィールドを持たぬ算出プロパティ**
- **setter**＝`oldValue`を**その場で`el.Params`から読み直す**（`string oldValue = el.Params.TryGetValue(...)`）。
  **キャッシュした添字や旧`SelectedElement`と比較する形ではない**
- **前例`SelectedElementNotchPosition`（`:2285-2305`）も同一の形**にて、**「同型で該当せず」も正しい**
- `memory: ecad2_setproperty_early_return_trap`が説く罠（**値が数値上偶然一致する経路でクリア処理が丸ごと
  スキップされる**）は、**`SetProperty(ref field, value)`のバッキングフィールド比較に由来する**ものにて、
  **本setterの`if (oldValue == value) return;`は「効果値どうしの比較」ゆえ性質が異なる**

**されど——罠そのものは、この案件に確かに在った。層が違うだけにござる。**

**`SelectedCell`のsetter（`:494`）が持つ`SetProperty(ref _selectedCell, value)`の早期return**——
**これは正しく`memory`の説く形**にて、**同じセルを選び直せば通知ブロックごとスキップされる。**
**そして新設2プロパティは、そもそもそのブロックに載っておらぬ（所見1）。**

**すなわち「該当せず」という答えは、問われた層では正しく、隣の層では正しくなかった。**
**侍の申告を誤りとは判じておらぬ**——**問いの層と、罠の在る層がずれておった**という所見にござる。

---

## 3. 台帳DoDとの整合（観点(a)）

| 殿裁可（`docs/todo.md:368`） | 実装 | 判定 |
|---|---|---|
| プロパティパネル内ComboBox | `MainWindow.xaml:1592-1604`／`x:Name="BreakerTypeCombo"` | **○** |
| 選択肢＝NFB/MCCB/ELB | `BreakerTypeOptions.All` | **○** |
| **Breaker3P選択時のみ出現** | `Visibility={Binding IsSelectedElementBreaker3P, ...}` | **△＝所見1**（束縛は正しいが、選択切替時の通知が1経路欠ける） |
| 即時反映 | setterが`Params`へ書き`MarkDirty()` | **○**（**画面反映の実地確認は忍者の領分**） |

**併せて確認した点**——
- **未編集要素の`Params`を空のまま保つ設計**（既定値と同値なら書かぬ）は、
  `MainWindowViewModel.cs:3235-3240`の既存コメント（増分4で「書き漏らしではない」と明記した箇所）と
  **首尾一貫**しており、**同コメントも今回あわせて改訂されておる**。**二重管理の懸念は解消の方向にござる**
- **`IsEnabled="{Binding CanEditDiagram}"`**＝テストモード中の編集を塞ぐ既存ゲートに接続済み

---

## 4. 不明点・申し送り

- **所見1の実害の現れ方は測っておらぬ**（静的読解のみ）。**忍者の実機確認の観点に加えられたい**——
  **「Breaker3P要素を配置後、別セルをクリックしてから再びBreaker3Pのセルを選び直した時、
  ComboBoxが現れるか／選択値が正しく出るか」**。
- **ELBの形状分岐は見ておらぬ**（家老の区切りに従う）。
- **ビルド・テスト実行は行うておらぬ**——**忍者が実機を使うておる恐れがあるゆえ**（`onmitsu.md`の作法）。
  **侍のbuild/test合格の申告を信じ、静的差分の一致確認のみで判じた。**

---

## 5. 報告

家老へ`send_message`で本書のパスと要旨を送る。

---

# 【再レビュー】修正コミット `68e24c6`（2026-08-05、往復1周目）

**対象範囲**＝`git show 68e24c6`（3ファイル・43行追加・1行削除）。**深度は軽量のまま。**

## R-0. 総括——**3件とも正しく対処されており申す。新たな要修正は無い**

| 先の所見 | 対処 | 判定 |
|---|---|---|
| 所見1（通知の横展開漏れ） | `SelectedCell` setterへ2行追加（`:503-504`） | **○** |
| 所見2（通知を検める網が無い） | `PropertyChangedForTest`を使うテスト2件を新設 | **○（検出力も確かめた、R-2）** |
| 所見3（`All`の独立リテラル） | `{ Default, "MCCB", "ELB" }`へ統一 | **○（中身は不変、R-3）** |
| 所見4（コミットメッセージの簡体字） | 本コミットのメッセージは**正字**（U+589E＝0件） | **○** |

**新たに気づいた軽微1件のみ**（R-4。**先のレビューで挙げるべきであった観点にござる**）。

## R-1. 挿入位置が既存の並びと一致しておる

```diff
                 OnPropertyChanged(nameof(SelectedElementNotchPosition));
+                OnPropertyChanged(nameof(IsSelectedElementBreaker3P));
+                OnPropertyChanged(nameof(SelectedElementBreakerType));
                 OnPropertyChanged(nameof(IsSelectedElementLamp));
```

**`:502`と`:503`の間**——**先のレビューで「正しい姿」と記した位置そのもの**にて、
**他3箇所（`DeleteSelectedElement`・`NotifySelectedElementChanged`・`ReplaceDocument`）の並び順とも揃うた。**
**これで7プロパティすべてが4箇所で揃う**（数え直し済み）。

## R-2. 【家老の観点1・2】RED証明の「1件」とテストの検出力

### R-2-1. 新設テストBは、**追加した2行のみに依存する**——静的に確かめた

**新設テストB**＝`SelectedCellの移動で戻った時もIsSelectedElementBreaker3PとSelectedElementBreakerTypeが通知される`。

**懸念した経路**＝`SelectedCell`のsetterは冒頭で6つの`ClearXxxIfAny()`系を呼ぶ。
**そのうち`ClearOrJoinTargetDraftIfAny()`（`:3460`）は`EndOrJoinTargetDraft()`（`:3498`）を呼び、
同メソッドは`NotifySelectedElementChanged()`を呼ぶ（`:3503`）**——
**この経路が生きておれば、2行を消してもテストBは通ってしまい、検出力が消える。**

**開いて確かめた結果、生きておらぬ**——

```csharp
private void ClearOrJoinTargetDraftIfAny()
{
    if (_orJoinTargetDraft is null) return;   // ← テストBではドラフト不在ゆえ、ここで戻る
    EndOrJoinTargetDraft();
}
```

**テストBは要素を配置するのみで合流先確認ドラフトを作らぬゆえ、早期returnする。**
**`NotifySelectedElementChanged()`の呼び出し元6箇所**（`ReplaceOneDeviceName`／`ReplaceAllDeviceName`／
`PlaceElementAtSelectedCell`2種／`EndOrJoinTargetDraft`／コンストラクタ）**のいずれも、
購読開始（`vm.SelectedCell = new GridPos(0,0)`の後）以降には走らぬ。**

**→ 購読後に2プロパティの通知を出しうるのは、追加した2行だけにござる。消せば必ず落ちる。**

### R-2-2. 「狙った1件のみFAIL」の1件＝**テストBで整合する**

**テスト総数＝13件**（既存11＋新設2。既存の内訳は本書1節の表）。

| テスト | 2行を消した時 | 理由 |
|---|---|---|
| **テストB**（`SelectedCell`経由の通知） | **FAIL** | 上記R-2-1 |
| テストA（setter自身の通知） | PASS | setter内の`OnPropertyChanged`は別物、手を触れておらぬ |
| 既存11件 | PASS | すべて値の直読みにて通知を見ておらぬ |

**1 FAIL + 12 PASS = 13**——**侍の申告「狙った1件のみ失敗・他12件は無事」と数が合う。**

**【射程・某が自ら区切る】某はテストを実行しておらぬ**（忍者の実機使用を慮ってのこと）。
**上は「侍の申告と静的読解が矛盾せぬ」ことを示すに留まり、実測ではござらぬ。**

### R-2-3. テストAの検出力＝**旧値まで検めており、素通りせぬ形**

```csharp
Assert.Contains(notified, n => n.Name == nameof(...SelectedElementBreakerType)
    && (string?)n.Old == BreakerTypeOptions.Default);
```

**発火の有無だけでなく「旧値が既定値`"NFB"`であること」まで固定しておる**——
**`OnPropertyChanged(name, oldValue)`の2引数版を1引数版へ書き換えれば旧値が`null`になって落ちる**
（`ViewModelBase`を開いて確認＝2引数版はトレース用に旧値を明示的に運ぶオーバーロード）。
**「通知は飛ぶが旧値が壊れる」型の後退も捕らえる形にござる。**

## R-3. 【家老の観点3】`All`の中身は変わっておらぬ

```diff
-public static readonly string[] All = { "NFB", "MCCB", "ELB" };
+public static readonly string[] All = { Default, "MCCB", "ELB" };
```

**`Default`は`const string = "NFB"`（コンパイル時定数）にて、`All`の初期化時にはリテラルとして埋め込まれる。**
**`static readonly`と`const`の初期化順序が問題になる余地は無い**（`const`は実行時初期化を持たぬ）。
**→ 中身は`{"NFB","MCCB","ELB"}`のまま不変にござる。**

## R-4. 【新たな軽微所見・某の見落としの補い】`BreakerTypeChoices`のテストがトートロジーにござる

```csharp
Assert.Equal(BreakerTypeOptions.All, vm.BreakerTypeChoices);
```

**`BreakerTypeChoices => BreakerTypeOptions.All`ゆえ、これは同じものどうしの比較**にて、
**`All`の中身が何であっても常に緑になり申す。**

**本コミットはまさにその`All`を書き換えた**——**中身が変わっておらぬことを担保しておるのは、
テストではなく某の静的読解（R-3）にすぎぬ。**
**`Assert.Equal(new[]{"NFB","MCCB","ELB"}, vm.BreakerTypeChoices)`と値を直に置けば、
選択肢の増減・順序変更が意図せず起きた時に鳴り申す。**

**【某の落度として明記する】これは先のレビュー（所見3で`All`を扱いながら）に挙げるべきであった観点にござる。**
**`All`のリテラルの持ち方には目が行きながら、それを守るテストの側は見ておらなんだ**——
**`onmitsu.md`「テスト入力の対称性・退化性チェック（PR-27）」の親戚**にて、
**退化しておるのは入力ではなくアサーションの方**という形。

**重みは軽微**（現時点で実害は無く、`All`は殿裁可の3値で固定される見込み）。**採否は侍の判断に委ねる。**

## R-5. 申し送り

- **実機確認の観点は先の申し送り（4節）のまま**——**「別セルをクリック後、再びBreaker3Pのセルを
  選び直した時、ComboBoxが現れるか／選択値が正しく出るか」。**
  **本修正はまさにその経路を直したものゆえ、忍者の確認がそのまま修正の裏づけになり申す。**
- **ビルド・テストの実行は今回も行うておらぬ**（侍の申告1753件GREENを信じ、静的差分の一致確認のみ）。
- **一時マーカーの残置確認**＝本コミットの差分に一時計装・デバッグ出力は含まれておらぬ（43行すべてが
  通知2行・定数参照1行・テスト40行）。**侍の「復元後マーカー0件」の申告と矛盾せぬ。**

---

# 【再レビュー2】即時反映の欠陥修正 `7dbc53b`（2026-08-05、往復1周目）

**対象範囲**＝`git show 7dbc53b`（5ファイル・93行追加・9行削除）。**深度は軽量のまま。**

## S-0. 総括——**要修正は無い。軽微1件のみ**

| 中身 | 判定 |
|---|---|
| (1) 固定リストへ`SelectedElementBreakerType`を追加 | **○**（一件で足りる。理由づけも正確） |
| (2) 未凍結Pen 5件を`CreateFrozenPen`で凍結 | **○**（某の数えた5件と完全一致・過不足なし） |
| (3) `BreakerTypeChoices`のトートロジー解消 | **○**（値を直に置き、鳴る形になった） |
| **(4) `P-168`のコメント訂正** | **△＝軽微1件。趣旨は正しいが`<see cref>`の参照先が誤り** |

## S-1. 【軽微】`P-168`訂正コメントの`<see cref>`が別の型を指しており申す

**`PartEditorCanvas.cs:808`付近の新設コメント**——

> この前提は`<see cref="ElementInstance.RowSpanOf"/>`を **h セルちょうど**へ改めた T-139(C) 裁定で消えている

**`ElementInstance.RowSpanOf`は h セルちょうどではござらぬ。**

```csharp
// Element.cs:125（検索文字列＝public static int RowSpanOf）
public static int RowSpanOf(int cellHeight) => Math.Max(0, cellHeight / 2);
```

**これは「占有行の半径」にて、整数除算。占有行数は`2*(h/2)+1`となり、
h=2 では3行・h=4 では5行**——**hちょうどになるのは奇数hのみにござる。**

**h セルちょうどへ改まったのは`PartShapeGeometry.FrameRect`の方**——

```csharp
// PartShapeGeometry.cs:147-148（検索文字列＝public static (double X, double Y, double Width, double Height) FrameRect）
double halfSpanMm = Math.Max(1, heightCells) / 2.0 * cellMm;
return (0.0, -halfSpanMm, w, halfSpanMm * 2);   // 高さ = h * cellMm ＝ h セルちょうど
```

**同メソッドのコメント（`:133`）も「行方向の半径は ±h/2 セル＝高さ h セルちょうど」と明記しており、
`:137-142`が「P-148（枠が2h-1セルへ広がる）を本裁定で覆った」と述べておる**——
**侍の訂正の趣旨（前提が消えた）は正しゅうござる。**

**誤っておるのは参照先の型名のみ**にて、
**`<see cref="ElementInstance.RowSpanOf"/>` → `<see cref="PartShapeGeometry.FrameRect"/>`**へ改められたい。
**コミットメッセージの方は「T-139(C)裁定でFrameRectがhセルちょうどへ改まり」と正しく書かれており申す**
——**コメント本文だけが取り違えた形。**

### 【この一件は、某自身がそのコメントに導かれて誤りかけたものにござる】

**某は`<see cref>`に従って`RowSpanOf`を開き、「偶数hでは h+1 ゆえ前提は消えておらぬ」と
指摘しかけ申した。** **さらに`FrameRect`まで辿って初めて、参照先の取り違えと分かり申した。**

**`<see cref>`はIDEから直に辿れるゆえ、誤った参照は読む者を確実に別の場所へ導き申す。**
**`onmitsu.md`「関数名・プロパティ名から意味を推し量ったなら、その実装を開け」が効いた場面**
——**開いたうえで、さらにもう一段辿らねば正しい結論に至らなんだ。**

## S-2. 【観点1】一件で足り申す——**予備調査の結論どおり**

**固定リストは14件になり申した**（既存13＋`SelectedElementBreakerType`）。

**`IsSelectedElementBreaker3P`が対象外である理由づけも、コメントに正確に記されており申す**——
「絵に出ぬVisibility制御専用のため対象外（値がリストに載った`SelectedCell`等から派生する、
という理由づけではないと隠密の指摘）」。**某が申し上げた「理由が違えば射程も違う」が反映されておる。**

## S-3. 【観点4】`CreateFrozenPen`は既存ヘルパーと同型にござる

```csharp
private static Pen CreateFrozenPen(Brush brush, double thickness)
{
    var pen = new Pen(brush, thickness);
    pen.Freeze();
    return pen;
}
```

**既存の`CreateDraftPen()`（`:94-99`）・`CreateSelectedFrameDashPen()`（`:143-148`）と
「生成→`Freeze()`→`return`」の形が一致**しており申す。
**違いは`DashStyle`を持たぬ点のみ**——**それは対象5件が単純形ゆえにて、正しい差にござる。**

**【静的フィールドの初期化順序について】障りは無い。**
`SelectedCellPen`（`:86`）が`CreateFrozenPen`（`:90-95`）より前に在るが、
**C#では静的フィールド初期化子からのメソッド前方参照は差し支えござらぬ**
（初期化子は宣言順に走るが、メソッドは型初期化の前に解決される）。

## S-4. 【観点5】凍結5件は某の数えた5件と完全一致——**過不足なし**

| # | 対象 | 某の調査書2節(a)との突合 |
|---|---|---|
| 1 | `SelectedCellPen` | ○ |
| 2 | `SelectedConnectorPen` | ○ |
| 3 | `SelectedFreeLinePen` | ○ |
| 4 | `SelectedImagePen` | ○ |
| 5 | `SelectedFrameSolidPen` | ○ |

**過剰なし**——`Brushes.*`の参照代入2件（`SelectedWireBreakBrush`／`SelectedConnectionDotBrush`）と、
既にヘルパー経由で凍結済みの6件には手が入っておらぬ。
**`SheetReorderInsertionAdorner.cs:13`は本コミットに含まれぬが、家老が別途`proposed.md`へ
起票すると明言されておるゆえ、意図的な範囲外にござる。**

## S-5. 【観点3】トートロジー解消——**鳴る形になり申した**

```diff
-Assert.Equal(BreakerTypeOptions.All, vm.BreakerTypeChoices);
+Assert.Equal(new[] { "NFB", "MCCB", "ELB" }, vm.BreakerTypeChoices);
```

**値を直に置いたゆえ、`All`の中身（増減・順序）が変われば鳴り申す。**
**テスト名も`選択肢一覧はNFB_MCCB_ELBの順`へ改まり、順序を検めておることが名から分かる形。**

## S-6. 新設テスト`T133Increment5CanvasRedrawTests`の検出力——**あり**

**観測点＝`VisualTreeHelper.GetChild(LadderCanvasHost, 0)`の参照同一性**
（`Draw()`が呼ばれるたび新しい`DrawingVisual`を生成することを利用）。

**懸念した経路**＝`SelectedElementBreakerType`のsetterは`MarkDirty()`も呼ぶ。
**`MarkDirty`が固定リストに載る通知を起こせば、リストから当該一件を消してもテストが通ってしまう。**

**開いて確かめた結果、起き申さぬ**——`public void MarkDirty() => IsDirty = true;`のみにて、
**`IsDirty`は固定リスト14件のいずれでもござらぬ。**

**→ 固定リストから`SelectedElementBreakerType`を消せば再描画が起きず、`Assert.NotSame`が落ち申す。**

**【射程】`UndoManager.RecordSnapshot`が固定リストに載る通知を起こさぬかまでは追うており申さぬ。**
**固定リスト14件はいずれも選択状態・プレビュー・モード・種別にて、Undo記録が起こす類ではないと見申すが、
実測ではござらぬ。**

## S-7. 射程・申し送り

- **静的読解のみ。ビルド・テストは実行しておらず、侍の申告（1754件GREEN・4回連続）は検めており申さぬ。**
- **`DashStyle`の罠に5件とも該当せぬ**という侍の申告は、某の独立調査（凍結対象5件はいずれも
  `new(Brush, thickness)`の単純形）と一致いたし申す。
- **忍者への実機確認の観点**＝**「ComboBoxで種別を変えた瞬間、選択を外さずとも絵（ラベル・ELBの
  テストボタン）が変わるか」**。**併せてELBの形状分岐は本増分で初めて確かめられる**
  （増分4の実機で忍者が「`Type`切替UI未実装ゆえ確認対象外」と区切った件）。
