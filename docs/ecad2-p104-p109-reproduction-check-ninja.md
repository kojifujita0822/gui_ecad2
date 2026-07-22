# P-104・P-109 再現性確認（忍者、2026-07-22）

## 対象
`docs/proposed.md` P-104（シート削除後の機器表旧機器名残存）・P-109（シート改名1番目限定の
選択色消失）の再現性確認。家老采配（殿裁可、pending項目6件中の優先2件）。

## 操作手法
すべてUI Automation経由の合成操作（`Invoke-Ecad2Button`/`InvokePattern.Invoke()`・
`SelectionItemPattern.Select()`・`Invoke-Ecad2CanvasClick`合成マウスクリック・`Send-Ecad2Keys`）。
殿ご自身の実操作による確認は未実施。

## P-104：シート削除後の機器表旧機器名残存

**判定：再現した（OK＝再現確認）**

### 手順・実測
1. 新規起動（シート0枚）から「Sheet1Renamed」「Sheet2Renamed」の2枚を追加
2. 「Sheet2Renamed」選択中にa接点を配置、機器名「X1」を入力し確定
   → 機器表（`DeviceTableGrid`）に `X1 / リレー` の1行が反映されたことを確認
3. 「－」（`DeleteSheetButton`）で「Sheet2Renamed」を削除
   → シート一覧は「Sheet1Renamed」のみ（1件）に減少。**要素は元々このシートには一切配置していない**
4. 削除後、機器表を再確認 → `X1 / リレー` の行が**残存したまま**（UIA `DataItem`件数1、セル値`X1`/`リレー`）

### スクショ所見
`p104-after-sheet-delete.png`（`scratchpad`保存）で目視確認。シート一覧「Sheet1Renamed」のみ・
中央キャンバス空欄（要素なし）にもかかわらず、右上機器表に「X1 リレー」の行が表示されたまま。
UIA実測とスクショが一致。

### 範囲外の気づき
シート削除ボタン押下時、確認ダイアログ（Yes/No等）が一切表示されず即座に削除された
（`EnumWindows`でウィンドウ増加なし）。取り消し確認の要否は仕様判断のため断定せず、事実のみ報告。

## P-109：シート改名（1番目限定）で選択色消失

**判定：再現した（OK＝再現確認、UIA経由に限る）**

### 手順・実測
1. 2枚のシート「Sheet1」「Sheet2」を作成（追加直後は末尾＝2番目が選択状態）
2. 1番目（Sheet1）を`SelectionItemPattern.Select()`で選択 → `IsSelected=True`確認
3. 「名前変更」→ダイアログで「Sheet1Renamed」に変更しOK確定
4. 確定直後、両シートとも `IsSelected=False`（**選択色消失を確認**）
5. 対照実験：2番目（Sheet2）を選択→「名前変更」→「Sheet2Renamed」に変更しOK確定
   → 確定直後、2番目のみ `IsSelected=True` で正しく維持（1番目は`False`のまま）

「1番目限定で選択色が失われる」という原観測（P-109、`docs/ecad2-t089-followup-mouseover-selection-verification-ninja.md`）と完全に一致する対照結果を得た。

### スクショ所見
`p109-after-rename-first.png` で目視確認。「Sheet1Renamed」「Sheet2Renamed」の両項目とも
ハイライト無し（選択色消失）と、UIA `IsSelected=False`×2 が一致。

### 留保（原記録から未解消）
今回もUI Automation経由（`SelectionItemPattern.Select()`）での検証であり、**殿の物理クリックでの
再現は依然として未確認**。UIA固有の事象である可能性は排除できていない。物理クリックでの追加検証は
グローバル入力（殿の他作業と衝突しうる、スキル0節原則）を要するため、今回は着手せず家老の判断を仰ぐ。

## 総括

| ID | 判定 | 再現条件 | 追加検証の要否 |
|---|---|---|---|
| P-104 | 再現（OK） | シートに機器配置→そのシート削除→機器表に残存 | 原因調査（侍/隠密）へ進めるか家老判断 |
| P-109 | 再現（OK、UIA限定） | 1番目シートの改名確定直後（2番目以降は正常） | 物理クリックでの裏取りの要否は家老判断 |

回帰の有無：両件とも既存のpending事象の再現確認であり、新規回帰の検出ではない。
