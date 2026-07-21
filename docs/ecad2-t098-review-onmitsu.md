# T-098 静的レビュー（隠密）

日付: 2026-07-21
対象コミット: `0a12585`（シート追加時のPageNumber採番方式見直し）
手法: `git show 0a12585` で範囲を明示した手動レビュー＋静的読解によるRED妥当性検証（共有main上での一時注入は`feedback_no_live_injection_on_shared_main.md`により禁止のため）。effort=low（1周目既定）。

## 結論

**指摘なし。DoDを満たすと判断する。** ビルド・テスト裏取り済み（`SheetNavigationViewModelTests`61件全合格）。忍者実機確認へ進めてよい。

## 確認観点と根拠

### (a) DoD整合確認

`docs/todo.md` T-098節（600-611行）には番号付きDoDリストは無く、「修正方針（案）＝既存シートの最大PageNumber+1を採番する方式へ変更」という記述のみ。実装（`DetermineNextPageNumber`、既存最大PageNumber+1・0枚なら1）はこの方針と一致。テストコード内コメントに現れる「DoD1」「DoD3」は侍が独自に具体化した番号と見受けられるが、内容（欠番状態からの重複防止／T-084ロジックとの整合）はtodo.md記載の問題意識と矛盾しない。整合確認OK。

### (b) RED先行証明の報告と実際のテスト内容の整合確認

侍報告「旧実装(`Sheets.Count+1`)で期待値4に対し実際3=重複バグを検出」を、コードへの一時注入ではなく**静的読解**で検証した（`docs-notes/feedback_no_live_injection_on_shared_main.md`により隠密の共有main一時注入は禁止のため代替）。

テスト`AddCommand_削除で欠番が生じた状態から追加すると既存最大PageNumberの次が付く`のシナリオを机上で追跡：
1. `NewDocument()`→シート1(PageNumber=1)
2. シート2(PageNumber=2)・シート3(PageNumber=3)を追加→計3枚
3. シート2(index=1)を`DeleteCommand`で削除→残るのはシート1・シート3、`Sheets.Count=2`
4. `AddCommand`実行

旧実装`Sheets.Count + 1` = 2+1 = **3**。しかし既にPageNumber=3のシートが存在するため重複（侍報告の「期待値4に対し実際3」と一致）。
新実装`DetermineNextPageNumber`＝既存最大(3)+1 = **4**（テストの`Assert.Equal(4, ...)`と一致）。

侍報告の数値は机上検証と一致し、RED証明の主張は妥当と判断する。

### (c) `DetermineNextPageNumber`の境界値網羅確認

実装：
```csharp
internal static int DetermineNextPageNumber(IEnumerable<Sheet> existingSheets)
{
    int maxPageNumber = 0;
    foreach (var sheet in existingSheets)
        if (sheet.PageNumber > maxPageNumber) maxPageNumber = sheet.PageNumber;
    return maxPageNumber + 1;
}
```

テスト4件で以下を網羅：
- 0枚（空コレクション）→1（境界値下限）
- 欠番なし連番(1,2,3)→4（通常ケース）
- 欠番あり(1,3、2欠番)→4（DoDの主目的、旧実装のバグ再現条件そのもの）
- 列挙順序不定(3,1,2の順)→4（`Sheets`が`ObservableCollection`等で挿入順序に依存しないことの確認）

同値分割の観点では「0枚」と「1枚以上」の2クラスで十分（`maxPageNumber`初期値0を番兵として使う単純な走査ロジックのため、1枚のみのケースが2枚以上と別挙動を取る余地がない）。「1枚のみ」の独立テストは無いが、ロジックの単純さから見て過不足なしと判断する。境界値分析としては妥当。

### (d) T-084 DeleteCommand欠番警告ロジックとの整合

`SheetNavigationViewModel.cs`169行目：`bool createsPageNumberGap = Sheets.Any(s => s.PageNumber > sheet.PageNumber);`

このロジックは「削除対象シートのPageNumberより大きいPageNumberを持つシートが残っているか」というPageNumber値同士の比較のみに依存しており、`AddCommand`側の採番方式（旧`Sheets.Count+1`固定／新`DetermineNextPageNumber`）を一切参照しない。本コミットの差分にも`DeleteCommand`部分の変更は含まれていない（`AddCommand`内のpageNumber計算のみ変更）。侍所見「PageNumber値比較のみで採番方式に依存せず変更不要」はコード上正しいと確認した。

新規テスト`DeleteCommand_T098新方式で追加したシートを末尾削除しても欠番警告は出ない`も、新方式で追加したシート（末尾・他に大きいPageNumberなし）を削除した場合に`createsPageNumberGap=false`となり警告が出ないことを確認する内容で、整合性テストとして妥当。

### (e) code-reviewスキル併用

marketplace版導入後も起動不可（本セッション冒頭で再確認済み）。`onmitsu.md`既定どおり手動レビューで代替。

### ビルド・テスト裏取り

`dotnet build tests/Ecad2.App.Tests/Ecad2.App.Tests.csproj --no-incremental` → 0警告0エラー。続けて`dotnet test --no-build --filter "FullyQualifiedName~SheetNavigationViewModelTests"` → **61件全合格**（`ecad2_red_proof_build_reflection_pitfalls.md`の教訓どおり`--no-incremental`+`--no-build`の2段階手順を使用、古いDLLでの誤判定を回避）。

## 不明点

なし。

## 派生提案（範囲外の気づき）

特になし。

## 出典

- `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`DetermineNextPageNumber`58-67行、`AddCommand`内呼び出し、`DeleteCommand`欠番判定169行）
- `tests/Ecad2.App.Tests/SheetNavigationViewModelTests.cs`（T-098新規テスト7件）
- `docs/todo.md` T-098節（600-611行）
- `docs-notes/feedback_no_live_injection_on_shared_main.md`（一時注入禁止・静的読解代替の根拠）
