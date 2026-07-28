# spec/guiecad-spec-undo-redo.md 原本突合監査（隠密、2026-07-28）

家老采配14。**原本ソース実在（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）を受け、本日T-134・T-135で
Undo周りを扱ったばかりの今、`docs/spec/guiecad-spec-undo-redo.md`を一次ソースと突合した。
調査のみ、specファイルの修正は行っておらぬ。**

---

## 0. 総括

| # | 種別 | 内容 |
|---|---|---|
| 1 | **確認（変化なし）** | 行数・クラス数・`IsEnabled`欠如等、機械的に検証可能な主要記述は**すべて正確**。コミット履歴上もspec執筆（2026-07-12）後にCommands配下の変更は無い |
| 2 | **既存「不明点」の解消・その1** | `CommandHistory.Clear()`の呼び出し箇所＝**発見できた**（文書新規・読込の2箇所） |
| 3 | **既存「不明点」の解消・その2、かつspec結論の訂正** | 「シート自体の追加/削除がGuiEcad側でUndo対象か」＝**対象外と確定**。これによりspecの「(2) ecad2のみにある機能：該当なし」という結論は**誤りと判明**——**シート単位のUndo/Redo対応はecad2独自の機能**（T-102/合流先確認モードと同型の「原本に無い」ケース） |
| 4 | **【新規・未記載】重大な網羅漏れ** | GuiEcadの`IsDirty`は`UndoDepth`との比較による**算出プロパティ**であり、ecad2の単純bool方式とは設計思想が異なる。**spec本文に一切記載が無い** |

---

## 1. 確認できた記述（変化なし・正確）

**再現手段**＝`wc -l`で3ファイルの行数を直接確認。

| ファイル | spec記載 | 実測 |
|---|---|---|
| `IUndoCommand.cs` | 全12行 | **12行（一致）** |
| `CommandHistory.cs` | 全57行 | **57行（一致）** |
| `ElementCommands.cs` | 全639行、35クラス+BatchCommand=36 | **639行・36クラス（両方一致、`grep -c "class.*IUndoCommand\|class BatchCommand"`で機械確認）** |

**`git log`確認**——`src/GuiEcad.App/Commands/`配下の最終コミットは**2026-07-01**、spec執筆日（T-081、
2026-07-12）より前。**spec執筆後にこの領域のコード変更は無く、経年劣化（陳腐化）の心配は無い**
（リポジトリ全体の最新コミットも2026-07-03で、spec執筆時点で最新状態を捉えていたと確認できる）。

**`IsEnabled`の欠如**——`MainPage.xaml`全体を`IsEnabled`で検索し**0件**。spec「grep確認：IsEnabled
絡みのUndo/Redo関連ヒットなしと」の記述を裏付けた（対象を限定せず全文検索でも0件ゆえ、より強い
確認になった）。`OnMenuUndo`/`OnMenuRedo`は4件ヒットし、spec記載の`:134-135`（メニュー）・
`:198-202`（ツールバーボタン）と符合する。

---

## 2.【解消】不明点1＝`CommandHistory.Clear()`の呼び出し箇所

spec§5・「不明点」節が「呼び出し箇所は本調査では特定していない」としていたが、**`_history.Clear()`で
機械検索したところ2箇所ヒットした**——

| 箇所 | 文脈 |
|---|---|
| `MainPage.Menu.cs:135`（`ApplyLoadedDocument`） | ファイルを開く・オートセーブ復元の共通処理 |
| `MainPage.Templates.cs:76`（`ApplyNewDocument`） | 新規作成・テンプレート読込の共通処理 |

**いずれも「文書を丸ごと差し替える」ゲートウェイであり、ecad2の`ReplaceDocument`が
`UndoManager.Clear()`を呼ぶのと構造的に対応する**。specの「不明点」はこれで解消してよい。

---

## 3.【解消・かつspec結論の訂正】不明点4＝シート追加/削除はGuiEcad側でUndo対象外と確定

spec§8「(2) ecad2のみにある機能」は「**該当なし**……**シート自体の追加/削除がGuiEcad側でUndo対象か
否かは本調査では確認できず、不明点として残す**」としていたが、**一次ソース直読で解消できた**。

`MainPage.Sheets.cs`の`OnAddSheetBtn`（`:118-141`）・`OnDeleteSheetBtn`（`:143-`）を全文直読——

- **`OnAddSheetBtn`**＝`_document.Sheets.Add(sheet)`と直接操作し、末尾で`MarkDirty()`を呼ぶのみ。
  **`_history.Execute(...)`は一切呼ばれぬ**
- **`OnDeleteSheetBtn`**＝`_document.Sheets.Remove(sheet)`と直接操作し、
  `_history.RemoveCommandsForSheet(sheet)`（そのシートに紐づく**既存の**要素コマンドを履歴から
  除去するのみ）を呼ぶ。**シート削除そのものをUndo可能にするコマンドは生成されぬ**

**帰結＝GuiEcad原本にはシート追加/削除のUndo/Redoが存在しない。**

**含意＝specの結論「(2) ecad2のみにある機能：該当なし」は誤りであり、以下へ訂正すべきと考える**——

> (2) ecad2のみにある機能：**シート追加/削除のUndo/Redo対応**（T-051 MVP対象範囲）。
> GuiEcadは`OnAddSheetBtn`/`OnDeleteSheetBtn`で`Sheets`コレクションを直接操作し、
> `_history.Execute`を一切経由しない。シート単位の取消・やり直しという概念自体が存在しない。

**【DoD2の弁別】これは「原本と違う（ecad2が仕様を変えた）」のではなく「原本に機能自体が無い」
ケース**——本日の采配5（T-135原本挙動調査、合流先確認モード）と同型の構図にござる。**ecad2の
T-051 MVPが「シート追加/削除から始めた」という設計判断は、GuiEcadを部分移植したものではなく、
GuiEcadに存在しない機能をecad2が独自に一から作った**という位置づけになる。

---

## 4.【新規発見・spec未記載】GuiEcadの`IsDirty`は`UndoDepth`比較による算出——ecad2と設計思想が異なる

**spec本文には`IsDirty`・`MarkDirty`・ダーティ判定に関する記述が一切無い**（spec全体を
「Dirty」「dirty」で検索しても0件）。**しかしUndo/Redo機構と密接に絡む重要な仕組みであり、
今回一次ソースを読む過程で発見した。**

`MainPage.xaml.cs:473-482`（全文）——

```csharp
/// <summary>未保存の変更があるか（保存時の UndoDepth と現在の UndoDepth が異なる）。</summary>
public bool IsDirty => _history.UndoDepth != _savedUndoDepth;

// Undo 履歴に乗らない変更（ドキュメント情報・シート設定・BOM 等）を確実にダーティ表示にする。
// _savedUndoDepth=-1 は UndoDepth と決して一致しないセンチネル。次回保存でリセットされる。
private void MarkDirty()
{
    _savedUndoDepth = -1;
    UpdateStatusExtras();
}
```

`MainPage.Menu.cs:160,186`（保存成功時）＝`_savedUndoDepth = _history.UndoDepth;`
（**保存した時点のUndo深さを記録する**）。

### 4-1. 仕組み

| 状態 | 判定 |
|---|---|
| 保存直後 | `_savedUndoDepth == UndoDepth`（一致）→`IsDirty=false` |
| その後Undo可能な操作を数回行う | `UndoDepth`が増える→不一致→`IsDirty=true` |
| **その状態からCtrl+Zで保存時の深さまで正確に戻す** | **`UndoDepth`が再び`_savedUndoDepth`と一致→`IsDirty`は明示操作なしで自動的に`false`へ戻る** |
| Undo対象外の変更（文書情報・シート設定等） | `MarkDirty()`が`_savedUndoDepth=-1`という**UndoDepthと絶対に一致しないセンチネル**を立て、次回保存まで強制的に`IsDirty=true`を維持する |

**ecad2の`IsDirty`（単純bool、`MarkDirty()`で`true`に立てるのみ、Undoで自動的に`false`へ
戻ることはない）とは、設計思想が明確に異なる。** GuiEcadは「Undoスタックの深さ」という
既存の状態を再利用してダーティ判定を導出する設計であり、**ecad2は独立したフラグを都度
明示的に立てる設計**——**どちらが優れているという話ではなく、比較の材料として書き漏らして
はならぬ差**と考える。

### 4-2.【DoD3の弁別】これは「specの誤り」——ecad2の実装がこの設計から逸脱しているわけではない

**ecad2の`IsDirty`実装自体は、GuiEcadのこの設計を意図的に踏襲しなかった、というだけであり、
「移植すべきものを移植し損ねた」という性質のものではないと考える**（ecad2側でUndoDepth比較方式へ
変える方が優れているという主張ではなく、単純boolでも実用上問題は生じていないため）。**ここでの
問題はecad2実装ではなく、spec側がこの比較軸自体を欠いていたこと**——調査時にUndo/Redoの範囲を
コマンド一覧・履歴操作に絞り、隣接する`IsDirty`機構までは踏み込まなかったための**網羅漏れ**と
見受ける。

---

## 5. 不明点・射程

- **本調査は`ElementCommands.cs`の個々の`Undo()`実装（選択状態・ツール状態の扱い）までは
  精読していない**——既存specの不明点と同じ範囲が今回も未解消のまま残る
- **`_savedUndoDepth`の発見は本調査で偶然の副産物**（`_history.Clear()`の呼び出し元を
  追う過程で目に入った）。**IsDirty機構を主題とした網羅的な調査ではない**ため、
  他にも同種の「spec未記載だが密接に関わる仕組み」が残っている可能性は排除できない
- **キーボードカスタマイズ機構の設定画面・永続化方法**（spec既存の不明点）は本調査では
  追っておらぬ

---

## 出典

- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\IUndoCommand.cs`（全12行）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\CommandHistory.cs`（全57行）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\ElementCommands.cs`（全639行、
  クラス数を`grep -c`で機械確認）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Menu.cs:120-190`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Templates.cs:65-79`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.Sheets.cs:115-160`（全文近く直読）
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.xaml.cs:468-482`
- `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\MainPage.xaml`（`IsEnabled`・`OnMenuUndo`/
  `OnMenuRedo`検索）
- GuiEcadリポジトリの`git log`（コミット履歴によるspec執筆後の変更有無確認）
- `docs/spec/guiecad-spec-undo-redo.md`（監査対象、全文）
