# P-106運用実態点検: 制度化済みチェックリストは侍の実装経路に届いていたか(隠密)

調査日: 2026-07-18　調査者: 隠密　依頼元: 家老（P-106[`docs/proposed.md`]の運用実態点検、コード変更を伴わない
制度・運用面の調査）

## 依頼内容(DoD)

制度化済みチェックリスト（`samurai.md`「新規選択可能状態の横展開チェックリスト」5項目、PR-01対応）が
存在するにもかかわらずT-067基盤で再発した件について、チェックリストの内容ではなく**侍が実装時に
実際に目にする経路になっているか**を点検する。

## 結論(先出し)

**経路自体は機能していた（むしろ模範的）。真因は別の2層に分解できる。**

1. **ReplaceDocument未対応・CancelResidualDraftForToolSwitch未対応（新規発見の3件目）**：
   着手前チェック（`docs/ecad2-t067-pretask-check-onmitsu.md`、T-067着手前日=2026-07-17に隠密自身が
   作成）が、P-080判定として**「ReplaceDocument」「CancelResidualDraftForToolSwitch()」を名指しで
   3箇所とも追加が必要と明記していた**。経路は確実に届いていたが、実装（コミット837b407）は
   3箇所のうち**`SelectedCell`setterのみ**対応し、残り2箇所が反映されなかった。「経路の弱さ」では
   なく「警告文書の一部しか実装に反映されなかった」という**実装時の汲み取り漏れ**。
2. **HasAnyDraft未対応**：これは着手前チェックのP-071/P-077/P-080いずれにも一言も登場しない。
   T-092（`_frameDraft`のような新規ドラフト種別を追加したら`HasAnyDraft`へ列挙する必要がある、という
   教訓の元ネタ）の対処後、この教訓がチェックリストとして制度化（`samurai.md`/`task-implementation`
   スキルへの追記）されなかったため、**そもそも警告が発生しようがなかった**「制度の空白」型。

両者は性質が異なるため、補強案も2種類に分けて提示する。

**追記（家老経由、侍からの申し送りとの符合）**：侍自身も「制度化済みチェックリストは`ReplaceDocument`/
`HasAnyDraft`のような『setterをバイパスする別経路』を明示的に対象としていない可能性がある」と申告して
いる。これは下記「型1」の技術的根拠そのものと完全に一致する——`SelectedCell`のsetter自体は
`ClearFrameDraftIfAny()`を正しく呼ぶが、`ReplaceDocument`（`_selectedCell`への直接代入、コード内
コメントに「setterをバイパスする直接代入」と明記）と`CancelResidualDraftForToolSwitch()`（setter経由
ではない独立入口）はいずれも「setter中心に書かれたPR-01チェックリスト」の対象範囲外に位置する。
**「運用面（読んだか否か）」ではなく「内容面（そもそもsetter以外の経路を項目として持たない）」の穴、
という侍の指摘は本調査の結論と一致する。**

---

## 調査方法(事実)

1. `.claude/skills/task-implementation/SKILL.md`を全文確認——41〜54行に「新規選択可能状態の横展開」
   「境界検証」「文書/シート構成変更処理の状態リセット」等、複数のチェックリストが**制約(Constraints)
   として明示的に埋め込まれている**ことを確認。侍がこのスキルでタスクを実装する限り、着手時に必ず
   目に入る構造になっている（`samurai.md`はあくまで詳細版へのポインタで、スキル本文だけでも要点は
   完結している）。
2. `docs-notes/roles/samurai.md`を全文確認——「新規選択可能状態の横展開チェックリスト」（71-88行、
   7項目）と「文書/シート構成変更処理の状態リセットチェックリスト」（149行以降、PR-05、4項目）が
   **別々のチェックリストとして存在する**ことを確認。
3. 両チェックリストの項目を精読——**どちらにも「新しいSelected*状態を追加したら、既存の
   ReplaceDocument等の文書差し替え処理へその状態のクリアを追従させる」という項目が存在しない**
   ことを確認（PR-01は「新状態を追加する側」の視点、PR-05は「ReplaceDocument等を変更する側」の視点
   で書かれており、「新状態追加→既存ReplaceDocumentへの追従」という逆方向の接続が両者の狭間に
   落ちている）。
4. `docs/ecad2-t067-pretask-check-onmitsu.md`（T-067着手前チェック、隠密作成、2026-07-17）を確認——
   P-080判定（40-46行）に**「少なくとも新設するドラフトのクリア処理を、既存3箇所（`SelectedCell`
   setter・`ReplaceDocument`・`CancelResidualDraftForToolSwitch()`）全てへ追加する必要がある」**と
   明記されていることを確認（3.の制度的空白とは別に、この案件固有の警告としては経路が実際に機能して
   いたことの直接証拠）。
5. 実装（`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`コミット837b407 + 現HEAD）を確認——
   `SelectedCell`setter（406-408行）には`ClearFrameDraftIfAny()`が追加されているが、
   `ReplaceDocument`（2844-2899行）と`CancelResidualDraftForToolSwitch()`（1877-1882行）には
   `_frameDraft`関連のクリア呼び出しが**存在しない**ことを実測確認。

## 新規発見：CancelResidualDraftForToolSwitch()未対応（T-067基盤の3件目の横展開漏れ）

```csharp
public void CancelResidualDraftForToolSwitch()
{
    CancelConnectorDraft();
    CancelFreeLineDraft();
    CancelImageInsertDraft();
    // CancelFrameDraft() が無い
}
```

着手前チェックが名指しした3箇所のうち、この箇所への追加漏れは本日先の隠密T-067基盤レビュー
（家老へ報告済み、`docs/ecad2-t067-foundation-static-review-onmitsu.md`）でも見落としており、
本P-106調査の過程で追加発見した。**失敗シナリオ**：枠記入中（`_frameDraft`保持中）にツールバーの
別ツール（部品配置・自作パーツ選択ボタン）を押すと、`Tool.Mode`は切り替わるが`_frameDraft`は
残留する。`FrameDraftPreview`はTool.Mode非依存で無条件に描画されるため（既存の`ImageInsertDraftPreview`
と同型の設計、コメントに明記）、ツール切替後も枠の点線プレビューが幽霊表示され続ける
（次にセルを選択する等で`SelectedCell`のsetterが発火するまで）。`HasAnyDraft`が`_frameDraft`を
含まないため実害の連鎖はないが、単独の表示バグとして成立する。

## 補強案（2種）

### 型1（ReplaceDocument・CancelResidualDraftForToolSwitch）：完了報告と着手前チェックの突き合わせ工程が無い

着手前チェック調査書自体が「対処必要箇所」を具体的に列挙していたにもかかわらず、侍の完了報告
（`docs/todo.md` T-067節「基盤区切り完了」683-692行）は「P-080対応=ドラフトクリア4種目として追加」
とのみ記載し、**3箇所のうちどこへ追加したかを明示していない**。家老・隠密とも「P-080対応済み」と
いう一言を鵜呑みにし、着手前チェックの3箇所リストと突き合わせる工程が検証パイプラインに存在しな
かった。

**提案**：`onmitsu.md`「侍実装のコードレビュー」節へ、「対象タスクに着手前チェック（pretask-check）
調査書が存在する場合、静的レビュー時にその調査書が列挙した対処必要箇所リストと実装差分を1対1で
突き合わせる」観点を追加する。今回のケースでは、この突き合わせを最初のレビューで行っていれば
ReplaceDocument・CancelResidualDraftForToolSwitch双方の漏れをその場で検出できていた（本調査で
事後的に気づけた事実がその証左）。

### 型2（HasAnyDraft）：個別タスクの教訓がチェックリストへ汎化されないまま埋もれる

T-092はP-094起票の「ドラフト中にAddRow/DeleteRow/Undo/Redoを実行すると無警告でズレる」問題への
対処（ブロック方式採用、2026-07-15完全Done）だが、完了時に「新しいドラフト種別を追加したら
`HasAnyDraft`（またはそれに類する集約判定）へ列挙する」という教訓がsamurai.md/task-implementation
スキルへ制度化されなかった。`docs/todo.md`の完了タスク一覧にも「制度化」の記載がなく、単発の実装
で終わっている。

**提案**：`samurai.md`「新規選択可能状態の横展開チェックリスト」（PR-01）へ8番目の項目として
「記入中ドラフトの集約判定（`HasAnyDraft`等、Undo/Redo・行操作等のCanExecuteガードに使われる
横断的な判定プロパティ）に新規ドラフト種別を追加したか」を追加する、または独立した新規チェック
リストとして制度化するかは家老・侍の判断に委ねるが、**現状「該当項目が存在しない」状態は確実に
埋めるべき穴**と考える。

## 事実と推測の峻別

- 「着手前チェックがReplaceDocument/CancelResidualDraftForToolSwitchを名指ししていた」「実装は
  SelectedCellのsetterのみ対応した」は実測に基づく事実。
- 「侍が着手前チェックを読んだ上で3箇所のうち1箇所しか実装しなかったのか、それとも読まずに
  P-080を『ドラフトクリア追加』とだけ解釈したのか」は**不明**（侍本人への確認が必要、本調査の
  範囲外）。ただし結果としてどちらであっても、型1の補強案（完了報告と着手前チェックの突き合わせ
  工程）は有効な対策になる。

## 不明点

- 侍が実装時に着手前チェック調査書を実際に開いたか（開いたが3箇所のうち1箇所しか反映しなかったのか、
  そもそも参照しなかったのか）は本調査では確認できない。
- HasAnyDraft型の教訓（T-092）が他にも制度化漏れの類例を持つか（横断調査はしていない、本調査は
  P-106の対象範囲＝T-067の2件+新規発見1件に限定）。

## 派生提案の有無

CancelResidualDraftForToolSwitch()未対応は範囲外の新規発見だが、T-067基盤コミット837b407の直接の
不具合であり、`docs/proposed.md`行きではなく家老へ直接報告し侍への修正采配を仰ぐのが適切と判断
（P-106の派生ではなく本調査中に見つかった同一タスクの追加バグのため）。

---

## 出典

- `.claude/skills/task-implementation/SKILL.md`（41-54行）
- `docs-notes/roles/samurai.md`（71-88行「新規選択可能状態の横展開チェックリスト」、149-159行
  「文書/シート構成変更処理の状態リセットチェックリスト」）
- `docs/ecad2-t067-pretask-check-onmitsu.md`（P-080判定、40-46行）
- `docs/todo.md` T-067節（683-692行、基盤区切り完了の記載）
- `docs/proposed.md`（P-094=103行、T-092起票の経緯）
- 実装コード実測：`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`406-408行・1869行・1877-1882行・2844-2899行
