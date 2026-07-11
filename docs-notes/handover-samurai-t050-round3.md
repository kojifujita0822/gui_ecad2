# 侍・臨時引き継ぎ（T-050往復3周目テスト補強、§5離脱による）

最終更新: 2026-07-10 13:15頃(JST)、侍記す（出力破損の同種2回目検知=§5発動により離脱）。
次の侍はこのファイルだけで再開できるよう、`long-horizon-discipline`スキル§6の5点セットで記す。

---

## 1. 目的とDoD（家老采配の原文要旨、task_id=T-050）

殿裁定=T-050往復3周目で補強決定。
**DeleteCommand境界値テスト4件に、削除後のSelectedSheet実値を検証するアサートを追加する。**

- DoD: RED先行証明必須（**補強前は検知できないことを実測で確認**してから実装）、既存268件+新規分すべて合格
- スコープ: 当該テストのみ。他テスト・実装コードは触らぬこと（便乗拡大禁止）
- 参照: `docs-notes/handover-next-session.md` 2節・`docs/archive/ecad2-t050-fix2-review-onmitsu.md`
  「テストコード自体の静的レビュー」節（指摘の原文）
- 完了後はT-055増分計画の起草へ（`docs/archive/ecad2-t055-guiecad-row-busnumber-survey-onmitsu.md`参照）

## 2. 現在の状態（三区分）

### 検証済み（根拠あり）

- **標的テストの特定** — `tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs:80-97`
  `DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce`（[Theory] 4ケース: (3,0)先頭/(3,1)中間/(3,2)末尾/(2,1)下限）。
  現アサートは発火回数（`Assert.Single`）と旧値（`Assert.Same(deleted, only)`）のみ — 根拠: Read直読
- **退行注入点の特定** — `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs:153`
  `_owner.SetCurrentSheetIndexCore(Math.Min(index, Sheets.Count - 1));`（RemoveAt後ゆえCount-1=削除後末尾index）。
  想定退行=`Math.Min(...)`→素の`index`（隠密レビュー指摘のシナリオそのもの） — 根拠: Read直読
- **`SetCurrentSheetIndexCore`にクランプ無し** — `MainWindowViewModel.cs:139-144`は素の
  `SetProperty(ref _currentSheetIndex, value)`+依存通知+SelectedCell=null のみ。範囲外indexはそのまま格納され、
  `SelectedSheet` getter（`SheetNavigationViewModel.cs:30-34`）が境界チェックでnullを返す。
  **ゆえに退行注入でRED証明が成立可能**（末尾/下限ケースでSelectedSheetがnullになり実値アサートが落ちる） — 根拠: Read直読
- **退行注入時も既存アサートはGREENのまま**（=補強前は検知できない、の理屈）: `SetCurrentSheetIndexCore`は
  SelectedSheet通知を撃たず、DeleteCommand自前の`OnPropertyChanged(nameof(SelectedSheet), sheet)`（:160）が
  1回だけ発火し旧値=削除シート。発火回数・旧値とも退行の影響を受けない — 根拠: コード追跡
  （隠密レビュー書の同判定とも一致）。**ただし実測はまだ**（下記「未着手」）

### 実施したが未検証

- なし（src/testsへの変更・ビルド・テスト実行は一切未着手のまま離脱。作業ツリーの変更は
  `docs-notes/output-corruption-log.md`（#3・#4追記）と本ファイルのみ、いずれも未コミット=家老がコミット）

### 未着手 / スキップ

- テスト補強の実装・RED証明の実測・全件テスト・コミット・家老への完了報告（すべて次の侍へ）

## 3. 試して失敗したアプローチと結果

- コード面の試行はゼロ（失敗アプローチ無し）。離脱理由は**Grepツール結果の破損2件**（同種2回目で§5発動）:
  - #3: `Math.Min`広域grep（-C 10）→ パス接頭辞欠落+`MainWindow.xaml.cs:903-904`の`//`が`\ `化け
  - #4: `SetCurrentSheetIndexCore`単一ファイルgrep（-A 20）→ `MainWindowViewModel.cs:127,136`の`///`が`\ `化け
  - 両件とも実ファイルは無傷（Read直読で確認済み）。**破損はGrep結果の行頭コメントトークンに限局、
    Read結果は当セッション全て正常**。詳細は`docs-notes/output-corruption-log.md` #3・#4
- 次の侍への示唆: 本タスクに必要な情報は本ファイルに全て転記済みゆえ、**広めのgrepは不要**。
  読取はRead（offset/limit指定）を主とし、grep結果を使う場合はコメント行の内容を鵜呑みにせず要所はReadで裏取りする

## 4. スコープ境界

- 触ってよい: `tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs`の
  `DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce`（[Theory]本体+InlineData+当該XMLコメント）のみ
- 触らぬ: 実装コード（`src/`配下）・他テスト。**RED証明のための実装への一時退行注入は例外**
  （samurai.md「実装の該当ガードを一時的にコメントアウトするとREDになる」の標準手法。
  計測後に`git checkout -- src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`で完全復元し、
  最終差分に実装変更を残さないこと）
- 軽微指摘（命名の粒度不揃い等、レビュー書「軽微な指摘」節）は**残置が正**（便乗拡大禁止、2周目と同判断）

## 5. 次の1手（具体的な実施計画、起草済み・未実施）

### 補強案（設計済み、次の侍は妥当性を自分でも確認の上で採用可否を判断せよ）

InlineDataに第3引数「削除後に選択されるべきシートの**削除前index**」を追加し、削除前に期待シート参照を
捕捉→削除後に`Assert.Same`で実値検証する。期待値は各境界値ごとに手導出の明示値
（実装式`Math.Min`の複製を避け、仕様を試験に書き下す形）:

| ケース | InlineData | 削除後の期待選択 | 削除前index（第3引数） |
|---|---|---|---|
| 3a 先頭削除 | (3, 0, **1**) | [A,B,C]→[B,C]、選択=B | 1 |
| 3b 中間削除 | (3, 1, **2**) | [A,B,C]→[A,C]、選択=C | 2 |
| 3c 末尾削除 | (3, 2, **1**) | [A,B,C]→[A,B]、選択=B | 1 |
| 3d 下限 | (2, 1, **0**) | [A,B]→[A]、選択=A | 0 |

メソッド本体: act前に `var expectedSelectedAfterDelete = vm.SheetNavigation.Sheets[expectedIndexBeforeDelete];`
を捕捉、既存2アサートの後に `Assert.Same(expectedSelectedAfterDelete, vm.SheetNavigation.SelectedSheet);` を追加。
XMLコメントに往復3周目補強の一文を追記（検証範囲が名前より広い旨は既存の軽微指摘と同扱いで触らない）。

### RED証明の手順（karo DoD「補強前は検知できないことの確認」を含む）

0. 【MUST】ビルド前に忍者のEcad2.App.exe起動有無を確認（`Get-Process Ecad2.App -ErrorAction SilentlyContinue`
   +必要ならpeerで一言。MSB3027対策）
1. 退行注入: `SheetNavigationViewModel.cs:153`の`Math.Min(index, Sheets.Count - 1)`→`index`
2. `dotnet build tests/Ecad2.App.Tests --no-incremental` →
   `dotnet test tests/Ecad2.App.Tests --no-build --filter "FullyQualifiedName~DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce"`
   → **4/4 GREEN を確認**（=現行テストは退行を検知できない、の実測証明）
3. テスト補強を実装（上記案）
4. 同ビルド+同テスト → **3c・3d の2ケースがRED**（SelectedSheetがnullになり実値アサートが落ちる）、
   3a・3bはGREENのまま（退行がMin結果を変えないケースゆえ正しい挙動）を確認
5. 復元: `git checkout -- src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`
6. `dotnet build src/Ecad2.sln --no-incremental` → `dotnet test src/Ecad2.sln --no-build` →
   **268件全合格**（Core14+App254、テスト件数は増えずアサート追加のみゆえ268のまま）
   ※RED証明の罠（メモリ既載）: 古いDLL実行防止に`--no-incremental`+`--no-build`の組を厳守
7. 差分自己点検（変更が当該テストファイルのみか`git diff --stat`で確認）→ staged検分
   （共有index巻き込み対策）→ パス限定コミット
   `git commit -- tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs`
8. 家老へ三区分で完了報告（RED証明の手法・対象テスト名・件数を明記）→ 次はT-055増分計画起草へ
