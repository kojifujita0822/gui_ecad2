# T-125増分β-1が生んだ実害の根本原因調査（隠密、2026-07-28）

家老采配。忍者の実機確認（`docs-notes/ecad2-t125-beta1-verification-ninja.md`）が検出した
**(1-b)配置済み要素の消失・回復不能**と**(1-c)案内文言の残留**の機序を、一次ソースで確定する。

**本書はβ-1（`2df5ab6`）を通した隠密自身のレビューの見落としを追うものにござる。率直に記す。**
調査のみ。`src/`への書き込みは行っておらぬ。

---

## 0. 総括

**規模は膨らまぬ。むしろ極小にござる**——**案A（発火条件を`Tool.Mode==Select`限定へ揃える）一つで
(1-b)(1-c)が同時に消える**。殿裁定の前提（「対処は小規模で済む見込み」）は保たれる。

**ただし家老の見立て一点を訂正する**——**Undoで戻らぬ理由は「スナップショットの記録順」ではない。
要素配置そのものがUndo機構の対象外である**（`UndoManager`のMVP範囲）。

---

## 1. (1-b) の機序＝4段の連鎖（すべて一次ソース確認済み）

| 段 | 何が起きるか | 出典 |
|---|---|---|
| 1 | 枠境界のダブルクリックで`OpenFrameLabelEditor`が呼ばれ、**β-1で加えた`_viewModel.SelectedCell = null`**が走る | `MainWindow.xaml.cs:3916-3928`（差分は`2df5ab6`） |
| 2 | `SelectedCell`のsetterが**副作用群を実行する。`SetProperty`の早期returnより前に置かれておるため、値が変わらずとも常時走る** | `MainWindowViewModel.cs:415-464`（意図は`:423-425`のコメントに明記＝「値が変化しない場合も含め常時クリアする」） |
| 3 | 副作用の**6番目**が`ClearOrJoinTargetDraftIfAny()` | `MainWindowViewModel.cs:463` |
| 4 | 同メソッドが**配置済み要素と機器を実際に削除する** | `MainWindowViewModel.cs:3087-3098`。**`draft.Sheet.Elements.Remove(draft.NewElement)`（`:3090`）**／**`draft.Document.Devices.ByName.Remove(deviceName)`（`:3092`）** |

**Escと完全に同一のメソッドが走っておる**——`CancelOrJoinTarget`（`:3078`）は
`=> ClearOrJoinTargetDraftIfAny();` の一行であり、**Esc経路とダブルクリック経路は同じ処理を共有する**。
**忍者の見立て「Escの仕様と同じ処理が意図せぬ経路で走る」は正しい。**

### 【核心】5種のドラフトのうち、1種だけ「破棄」の意味が違う

`SelectedCell`のsetterがクリアする記入中ドラフトは**5種**（`:451`〜`:463`を1つずつ列挙）——

1. `ClearConnectorDraftIfAny()`（`:451`）＝ドラフト変数を`null`にするのみ（`:1957-1963`）
2. `ClearFreeLineDraftIfAny()`（`:453`）＝同型
3. `CancelImageInsertDraft()`（`:457`）＝同型
4. `ClearFrameDraftIfAny()`（`:460`）＝同型
5. **`ClearOrJoinTargetDraftIfAny()`（`:463`）＝要素と機器を実削除する**

**5種目だけが破壊的にござる。** その理由は同メソッドのdocコメント（`:3080-3086`）に明記されておる——
**「このドラフトは既に確定済みの要素（NewElement）を指しているため、単純にドラフトを破棄するだけでは
要素がOR接続されない孤立要素として残ってしまう。よって取消時は要素配置そのものを取り消す」**。

**設計としては筋が通っておる。** 問題は**この破壊性が、他4種と同じ列に並んで見えること**にござる。

---

## 2. Undo/Redoで戻らぬ理由——**家老の見立てを訂正する**

**家老の采配文＝「スナップショットの記録順が疑わしい」。これは誤りにござる。**

**真因＝要素配置そのものがUndo機構の対象外である。**

| 根拠 | 出典 |
|---|---|
| Undo基盤のMVP範囲が**シート追加/削除のみ**と明記されておる | `src/Ecad2.App/Commands/UndoManager.cs:9-10`＝「**MVP対象範囲は候補1(SheetNavigationViewModelのシート追加/削除のみ)**」 |
| 同じ断りがViewModel側にもある | `MainWindowViewModel.cs:3375-3376`＝「**MVP対象範囲はSheetNavigationViewModelのシート追加/削除のみ（RecordSnapshotの呼び出しはSheetNavigationViewModel.AddCommand/DeleteCommand側で行う）**」 |
| **`PlaceElementAtSelectedCell`を全文（`:2916-2961`）読んだが`RecordSnapshot`の呼び出しは無い**。あるのは`MarkDirty()`（`:2932`）のみ | `MainWindowViewModel.cs:2916-2961` |
| `ClearOrJoinTargetDraftIfAny`にも`RecordSnapshot`は無い | `MainWindowViewModel.cs:3087-3098` |
| `RecordSnapshot`の**実呼び出し**は`SheetNavigationViewModel.cs`の**3箇所**のみ | `:157`／`:198`／`:261` |

**すなわち、配置は最初からUndoスタックに積まれておらぬ。** ロールバックが記録を残さぬ以前に、
**戻す先が存在せぬ**。

### 忍者の実測値の説明（**この節は推測を含むと明示する**）

忍者の手順では検証の下拵えで **`＋`から制御回路シートを追加**しておる（検証記録の環境表）。
**Undoスタックに積まれた操作はこれだけ**であった、と読むと実測3点がすべて説明できる——

| 操作 | 実測 | 説明 |
|---|---|---|
| `Ctrl+Z` | 機器表 **1行 → 0行** | シート追加が巻き戻り、**そのシートに載っていたX1もろとも消えた** |
| `Ctrl+Y` | **1行**へ | Undo時に`_redoStack`へ積まれた「X1のみ」の状態が戻る（`UndoManager.cs:32`） |
| `Ctrl+Y`（2度目） | **1行のまま** | `_redoStack`が空ゆえ`null`を返す（`UndoManager.cs:39`） |

**実測3点すべてと符合する。** ただし**「シート追加が唯一積まれた操作である」ことは忍者の手順からの
推論であり、実機で確かめてはおらぬ**。**確定を要するなら忍者の再実測が要る**（Undo前の`CanUndo`と
スタック深さを見れば足りる）。

**なお「戻らぬ理由」そのものは推論に依らぬ**——上表の一次ソース5点で確定しておる。

### 【留意】`RecordSnapshot`の出現数を数え上げてはおらぬ

`MainWindowViewModel.cs`内に`RecordSnapshot`の文字列は多数現れるが、**抽出した4箇所
（`:1277`/`:1417`/`:2189`/`:2455`）はいずれもコメント内の言及であった**。**実呼び出しの全数は
数えておらぬ**（本件の結論には要さぬため）。

**ただし1件、後続の判断に効く記述を拾った**——`:1277`のコメントに
**「画像操作は全てUndo対象、他要素との非対称は許容」**とある。すなわち**Undo対象は
MVP範囲（シート追加/削除）から既に広がっており、要素の種別ごとに非対称**にござる。
**これはP-125（`ConfirmDrag<T>`経由4種がUndo対象外、Image/Frame/Elementのドラッグ確定は対象）と
同じ地図の話**にござる。

---

## 3. (1-c) の経路＝案内文言はView側の管轄であり、VM側の取消経路は触れぬ

| 経路 | 文言のクリア | 出典 |
|---|---|---|
| **設定**（OR配置が合流先確認モードへ入った時） | `_viewModel.StatusMessage = "上下キーで合流先候補を切替、Enterで確定、Escで配置ごと取消"` | `MainWindow.xaml.cs:3982-3983` |
| **Esc** | **消える**。`_viewModel.StatusMessage = ""` を**条件分岐の外で一律実行** | `MainWindow.xaml.cs:2598`（`:2594-2597`のコメントに「**層に依らず全Esc押下で一度だけ行う**」と意図が明記） |
| **ツール切替** | 消える（View側の同型処理） | `CancelResidualDraftForToolSwitch`（`MainWindowViewModel.cs:1988-1999`）はVM側だが、ツール切替ボタンのView側ハンドラが文言も扱う |
| **枠境界のダブルクリック** | **残る** | `OpenFrameLabelEditor`（`MainWindow.xaml.cs:3916-`）に`StatusMessage`の代入が無い |

**`ClearOrJoinTargetDraftIfAny`（VM側、`:3087-3098`）は`StatusMessage`に一切触れておらぬ。**
**文言のクリアはView側の各ハンドラが個別に持つ責務**であり、**VM経由で暗黙にドラフトが消える経路には
その責務が付いてこない**——これが(1-c)の正体にござる。

**忍者の対照実験（Esc・ツール切替・ダブルクリックの3経路）は、この構造をそのまま言い当てておる。**

---

## 4. 対処案と代償

### 【推奨】案A＝`OpenFrameLabelEditor`の発火を`Tool.Mode==Select`限定へ揃える

**家老のDoD 3-(a)への回答＝(1-b)(1-c)とも実害は消える。**

- **(1-b)が消える理屈**＝記入中（`Tool.Mode == ConfirmOrJoinTarget`）にはそもそも
  `SelectedCell = null`へ至らぬゆえ、`ClearOrJoinTargetDraftIfAny`が走らぬ
- **(1-c)が同時に消える理屈**＝文言が残るのは「ドラフトだけがVM経由で消える」ためであり、
  **ドラフトが消えなくなれば文言と実態の食い違い自体が生じぬ**
- **規模＝条件式1つ。極小にござる**
- **代償**＝記入中は枠ラベル編集が開かなくなる。**ただし`SelectedFrame`を設定する他3箇所は既に
  `Tool.Mode==Select`限定**ゆえ、**一貫性はむしろ増す方向**（β-1の当初の狙いと同じ筋）
- **留意**＝「記入中に枠ラベルを編集できぬ」ことの是非は**UI/UX判断**を含む。ただし
  **現状は「編集できるが要素が消える」**ゆえ、**どちらが良いかを問うまでもないと見受ける**

### 案B＝ロールバック前に`RecordSnapshot`を積む（Undoで戻せるようにする）

- **推さぬ。規模＝中〜大。** `PlaceElementAtSelectedCell`がそもそも積んでおらぬゆえ、
  **ロールバックだけ積んでも「戻した先」が配置前ではない**——**Undo対象の拡張という別工事になる**
- **P-125（`ConfirmDrag<T>`経由4種のUndo欠落）と同根**ゆえ、**やるなら一括で設計する筋**
- **殿裁定の前提（小規模）を壊す**

### 案C＝`SelectedCell`のsetterからロールバックを外す

- **不可。** T-102の殿裁定「解釈(i)＝要素配置ごと取消」を壊す（`:3077`のdocコメント）。
  **Esc経路の仕様そのものにござる**

### 案D＝(1-c)だけを個別に塞ぐ（`OpenFrameLabelEditor`で`StatusMessage=""`）

- **単独では採らぬ**。(1-b)が残るゆえ**対症療法**にすぎぬ。**案Aを採れば不要**

---

## 5. 隠密自身のレビューの見落とし（率直に記す）

β-1のレビューで拙者は**「発火条件の非対称（他3箇所は`Tool.Mode==Select`限定）」を指摘し、
実害シナリオとして「記入中ドラフトの意図せぬ破棄」を挙げた**。**在り処は言い当てておった。**

**見誤ったのは実害の重さにござる**——「ドラフトが消える」までは読んだが、
**5種のドラフトのうち`ConfirmOrJoinTarget`だけは破棄が要素削除を伴う**という一点を追わなんだ。
**`ClearOrJoinTargetDraftIfAny`の中身を読めば`Elements.Remove`は`:3090`に書いてある。**
**列挙はしたが、1つずつ中身を開かなんだ。**

**教訓＝「同じ列に並んでいるものは同じ重さだ」と暗黙に置いた。** 5種のクリア処理は
`SelectedCell`のsetter内で**外見上まったく同じ形（`ClearXxxIfAny()`の並び）で呼ばれておる**ゆえ、
**並びの均質さが中身の非対称を隠した。**

---

## 6. 気づきと落とし先

1. **【最重要】共通の入口へ並ぶ処理は、外見の均質さが中身の非対称を隠す** —
   `SelectedCell`のsetterに並ぶ5つの`ClearXxxIfAny()`は**見た目が完全に揃っておる**が、
   **5種目だけが破壊的**であった。**「横並びの処理を1つずつ開いて、破壊性の段階を確かめる」**
   工程が要る。**とりわけ「クリア」「キャンセル」「破棄」と名の付く処理は、名が軽くとも
   中身が重いことがある。**
   → **落とし先＝`onmitsu.md`のレビュー観点へ新設**（「複合UI操作は構成イベントへ分解して追え」の
   隣に置くのが座りが良い。**あちらは時間軸の分解、こちらは並列に並ぶ処理の重さの分解**にござる）
2. **VM側の暗黙経路には、View側の後始末が付いてこない** —
   `StatusMessage`のクリアはView側の各ハンドラが個別に持つ責務であり、
   **VM経由でドラフトが消える経路（setterの副作用）には付随せぬ**。
   **「状態」と「その状態を説明する表示」が別の層に置かれておる**ことの帰結にござる。
   → **落とし先＝`docs/proposed.md`起票の候補**（**隠密は起票せぬ**）。
   **なおβ-2/β-3で選択解決を一本化する際、この非対称も併せて見るのが安い**
3. **「Undo対象かどうか」は要素の種別ごとに非対称であり、その地図が一箇所にまとまっておらぬ** —
   MVP範囲は「シート追加/削除のみ」と明記されておるが、実際は**画像操作は全て対象**
   （`:1277`のコメント）、**ドラッグ確定は種別により対象/対象外**（P-125）と広がっておる。
   **本件のような「消えたものが戻らぬ」は、この地図の空白で起きる。**
   → **落とし先＝P-125へ追記する材料**（**家老の判断を仰ぐ**。P-125は既にpendingで
   「Undo対象拡張の設計判断が先」とあり、**本件はその判断を要する具体例が1つ増えた**という位置づけ）

---

## 出典

- `src/Ecad2.App/MainWindow.xaml.cs`＝`:2588-2642`（Esc多段階）／`:3916-3928`（`OpenFrameLabelEditor`）／`:3970-3988`（`PlacementOkButton_Click`・文言設定）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`＝`:415-479`（`SelectedCell`のsetter）／`:1935-1963`（`ConfirmConnectorDraft`・`ClearConnectorDraftIfAny`）／`:1988-1999`（`CancelResidualDraftForToolSwitch`）／`:2916-2961`（`PlaceElementAtSelectedCell`全文）／`:3031-3098`（`OrJoinTargetDraft`一式）／`:3375-3376`（Undo MVP範囲のコメント）
- `src/Ecad2.App/Commands/UndoManager.cs`（全文52行）
- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs:157,198,261`（`RecordSnapshot`の実呼び出し3箇所）
- `git show 2df5ab6 -- src/Ecad2.App/MainWindow.xaml.cs`（β-1の差分全文）
- `docs-notes/ecad2-t125-beta1-verification-ninja.md`（忍者の実機確認）
