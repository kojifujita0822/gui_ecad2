# T-051往復3周目修正 最終レビュー（隠密）

対象: コミット`8a6eb13`（隠密再々レビュー`docs/archive/ecad2-t051-round3-review-onmitsu.md`§2-1のPLAUSIBLE
対応、テスト設計書=`docs/archive/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md`）。家老指定4観点の
手動確認を実施。Stryker手動棚卸し（往復2周以上のクローズ時の制度）は環境要因で完了できず、別途
`docs/archive/ecad2-t051-stryker-analysis-blocker-onmitsu.md`に記録する。

**結論を先に：通常観点(a)〜(d)は全て妥当。T-051は本コミットで往復3周を経てクローズ可能な水準に
達している（Stryker棚卸しの結果を除く）。**

---

## 1. 家老指定4観点

### (a) 設計書突合（T-selclamp-1〜5全実装、ClampSelectedCellToSheetRowsヘルパーが提案どおりRowのみ・副作用なしか）

**OK、完全一致。**

`MainWindowViewModel.cs`（1798-1803行）に新設された`ClampSelectedCellToSheetRows`ヘルパー：

```csharp
private static GridPos? ClampSelectedCellToSheetRows(GridPos? cell, Sheet? sheet)
    => cell is GridPos pos && sheet is Sheet s && pos.Row >= s.Grid.Rows
        ? pos with { Row = s.Grid.Rows - 1 }
        : cell;
```

`static`な純粋関数として実装され、`Row`のみをクランプし`StatusMessage`等の副作用は一切含まない
（設計書§0.3の提案どおり）。`Column`は判定式に登場せず対象外（設計書§0.2の判断どおり）。

| 設計書 | 実装（テスト名） | Given/When/Then |
|---|---|---|
| T-selclamp-1 | `UndoCommand_Execute_WhenRowFarExceedsRestoredGridRows_ClampsToLastRow` | 一致（D3・大幅超過） |
| T-selclamp-2 | `UndoCommand_Execute_WhenRowExceedsRestoredGridRowsByOne_ClampsToLastRow` | 一致（D2・境界値） |
| T-selclamp-3 | `UndoCommand_Execute_WhenRowIsLastValidRow_PreservesSelectedCell` | 一致（D4・退行防止） |
| T-selclamp-4 | `RedoCommand_Execute_AfterClamp_PreservesSelectedCell` | 一致（対称性点検） |
| T-selclamp-5 | `UndoCommand_Execute_WithSheetIndexClampAndRowClamp_UsesRestoredSheetGridRows` | 一致（複合ケース） |

### (b) RED証明整合（1/2/4/5 FAIL・3 PASSが設計書想定どおりか）

**OK。** コミットメッセージに「RED証明: T-selclamp-1/2/4/5は修正前コード（`f2aaaad`）でFAIL実測済み」
と明記。

T-selclamp-4（Redo対称性）がFAILする理由も論理的に妥当：GivenがT-selclamp-1のWhen実行後の状態を
引き継ぐため、修正前コードならUndo実行時点で既に`SelectedCell`は`(14,2)`のまま（クランプされない）
残っており、続くRedo実行でも同様、最終アサート（期待値`(9,2)`）と一致せずFAILする。

T-selclamp-3（境界値内・退行防止確認）は修正前後で挙動が変わらない（`Row=9`は`Rows=10`の範囲内、
クランプ条件`pos.Row >= s.Grid.Rows`が偽のため）——設計書の想定どおりPASSする。

### (c) クランプ基準が「復元後CurrentSheet」で正しく確定しているか

**OK。** `ApplyUndoRedoSnapshot`内、該当代入（1832行）：

```csharp
SetCurrentSheetIndexCore(clampedIndex);
SelectedCell = ClampSelectedCellToSheetRows(oldSelectedCell, CurrentSheet);
```

`CurrentSheet`は算出プロパティ（`CurrentSheetIndex >= 0 && CurrentSheetIndex < Document.Sheets.Count
? Document.Sheets[CurrentSheetIndex] : null`）であり、`SetCurrentSheetIndexCore(clampedIndex)`実行
（`_currentSheetIndex`をクランプ済みの値へ更新）の**直後**に評価されるため、常に「クランプ確定後の
CurrentSheet」を正しく参照する。T-selclamp-5（複合ケース、CurrentSheetIndexクランプとRowクランプが
同時発生）で、クランプ基準が「Undo実行前に選択していたシート」ではなく「クランプ後の新しい
CurrentSheet」であることが実測で検証されている。

### (d) 前回検出のPLAUSIBLE経路（シート追加Undo+行数拡張）が本修正で実際に塞がったか

**OK、実測で確認。**

```
dotnet build src/Ecad2.sln --no-incremental → 0エラー・0警告
dotnet test src/Ecad2.sln --no-build
  Ecad2.Core.Tests: 64件 合格
  Ecad2.App.Tests: 419件 合格（失敗0、T-selclamp-1〜5の5件増加を確認）
```

`docs/archive/ecad2-t051-round3-review-onmitsu.md`§2-1で報告した再現手順（シート追加→行拡張(Undo管理対象
外)→シート追加をUndo）に相当するT-selclamp-1・T-selclamp-5がいずれもPASSしており、当該経路は
実測で塞がったことを確認した。

---

## 2. 結論・推奨

T-051往復3周目修正（`8a6eb13`）は家老指定4観点いずれも妥当。往復1周目（4件のデータ破損/表示
不整合バグ）→往復2周目（SelectedCell無条件nullリセット解消）→往復3周目（クランプ欠如の解消）と
段階的に修正され、現時点で隠密が把握している未解決のCONFIRMED/PLAUSIBLEな正しさバグは無い。

既知の残存課題（記入中ドラフトが退避・復元の過程で無警告に破棄される、`docs/archive/ecad2-t051-round2-review-onmitsu.md`
§2-2・`docs/archive/ecad2-t051-round3-review-onmitsu.md`§2-2で言及済み）は、家老指定DoDの範囲外
（SelectedCellの座標維持が対象、Tool状態全体の維持は対象外）として一貫して扱われており、対応要否
は別途の裁定事項として持ち越されている。

**忍者実機確認へ回すこと自体に支障はないと判断する。** Stryker手動棚卸しの結果は別紙参照。

---

## 出典
- `docs/archive/ecad2-t051-round3-review-onmitsu.md`（起点、PLAUSIBLE詳細）
- `docs/archive/ecad2-t051-selectedcell-clamp-test-design-onmitsu.md`（テスト設計）
- `docs/archive/ecad2-t051-stryker-analysis-blocker-onmitsu.md`（Stryker実行障害の記録）
- `src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`ClampSelectedCellToSheetRows`:1798-1803、
  `ApplyUndoRedoSnapshot`:1806-1841）
