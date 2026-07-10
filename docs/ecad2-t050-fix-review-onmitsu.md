# T-050修正 事後レビュー（隠密）

対象コミット: `e2f44d7`（不対サロゲート例外保護・SelectedSheet旧値バグ・P-044旧値null化3箇所）

## 結論

**要修正あり（重要）。** 家老指定4観点のうち観点1・2・4は妥当だが、観点3の検分過程で**新規の重大な構造的バグ**を発見した。3つの独立したcode-reviewエージェントが同一の根本原因・同一の失敗シナリオに到達しており、信頼度は高い（3者クロス確認、CONFIRMED相当）。

---

## 家老指定4観点への回答

### 観点1: 設計書どおりの実装か

**妥当。** 経路X（`docs/ecad2-t050-fix-test-design-onmitsu.md`が推奨した方式）が採用され、`DetermineOldSelectedSheetForAdd(int sheetsCountBeforeAdd, Sheet? currentSelectedSheet) => sheetsCountBeforeAdd == 0 ? null : currentSelectedSheet`という純粋関数が新設された。`[Theory]`0/1/3枚の境界値テストも設計書どおり。0枚のケースで意図的に非nullな`current`を渡し「0枚なら常にnull」の契約を突くテスト設計も的確。

### 観点2: CurrentSheetIndexセッタへの波及（P-030前科箇所ゆえ厳しく）

**単体では妥当。** 追加されたのは`var oldSelectedSheet = SheetNavigation.SelectedSheet;`という1行の読み取りのみで、`SetProperty`実行前（`_currentSheetIndex`更新前）に正しく配置されている。既存の分岐・代入構造には手を加えておらず、「読み取りのみ・挙動不変」という主張は単体では正しい。

**ただし**、この変更（`RefreshSelectedSheet`が2引数化され、セッタが無条件でこれを呼ぶようになったこと）が、後述する新規バグの一因になっている。単体レビューでは見えない、他コマンドとの組み合わせで顕在化する問題。

### 観点3: P-044の3箇所の過不足

- **RefreshSelectedSheet**：実装済み、呼び出し元2箇所（`CurrentSheetIndex`セッタ・`RenameCommand`）とも一見正しい。ただし`CurrentSheetIndex`セッタ経由の呼び出しが、AddCommand/DeleteCommand由来で呼ばれる際に問題を生む（後述）。
- **ResetSheets**：**新規バグ発見（CONFIRMED）**。後述。
- **DeleteCommand**：`sheet`ローカル変数の利用自体（153行目`OnPropertyChanged(nameof(SelectedSheet), sheet)`）は正しいが、その直前で`CurrentSheetIndex`セッタ経由の誤ったネスト発火が挟まる（後述、二重発火問題の一部）。
- **RenameCommand側の旧値=sheet**：正しい。改名は同一シートに留まる操作（index・参照とも不変）のためold==newは意図通りで、バグではない。

### 観点4: 指摘1のtry/catch保護とテスト実質性

**妥当、問題なし。** `catch (ArgumentException)`と型を絞っており適切（無差別catchではない）。テストの罠対処（xUnitの`InlineData`が不対サロゲートをU+FFFDへ変換してしまう問題を、コードポイント(int)＋位置(string)をメソッド内で組み立てる方式で回避）は正しく機能しており、真に不正なUTF-16文字列を`Normalize`へ与えられている。6ケース（単独high/low surrogate上下限・混在2パターン）は設計書どおり。

---

## 新規発見（最重要）：AddCommand/DeleteCommandの二重発火・old値不整合

### 根本原因

`AddCommand`・`DeleteCommand`はいずれも「**先にコレクション（`Sheets`/`Document.Sheets`）を変更し、その後で`_owner.CurrentSheetIndex = index`を代入する**」という順序を取る。`MainWindowViewModel.CurrentSheetIndex`セッタは今回の修正で、`SetProperty`実行前に`var oldSelectedSheet = SheetNavigation.SelectedSheet;`（`SheetNavigation.Sheets`ゲッター経由）を捕捉し、無条件で`SheetNavigation.RefreshSelectedSheet(oldSelectedSheet)`を呼ぶよう変更された。

しかし`AddCommand`/`DeleteCommand`が`CurrentSheetIndex`セッタを呼ぶ時点では、**コレクションは既に変更済み**（Add/Removeが先行実行済み）のため、セッタ内部の`oldSelectedSheet`捕捉は「変更済みコレクション×旧（または新）index」という不整合な組み合わせを読んでしまう。

### 具体的な失敗シナリオ（3独立エージェントが一致して特定）

**AddCommand（0→1、初回追加＝`wasEmpty`ケース）**：
1. `Sheets.Add(sheet)`が同期実行済み（`SheetNavigationViewModel.cs:110`）
2. `BeginInvoke`ラムダで`_owner.CurrentSheetIndex = index`（`index=0`）を実行
3. `CurrentSheetIndex`セッタ内の`oldSelectedSheet`捕捉：`_owner.CurrentSheetIndex`はまだ未更新（0のまま不変）、`Sheets`は既に1枚（新シート含む）→ getterは`Sheets[0]`＝**追加されたばかりの新シート自身**を返す＝**old==new**。これは**T-050が修正したはずのold==newバグが、CurrentSheetIndexセッタという別経路から再現している**。
4. セッタ内`SheetNavigation.RefreshSelectedSheet(oldSelectedSheet)`が発火（1回目、誤った値）
5. 直後にラムダ自身の`OnPropertyChanged(nameof(SelectedSheet), oldSelectedSheet)`（`AddCommand`内で事前に正しく計算した`null`）が発火（2回目、正しい値）
6. **結果：1回のシート追加操作でSelectedSheetのPropertyChangedが2回発火し、TraceLogには「old=new(誤)→old=null(正)」という矛盾する2行が連続して残る。**

**DeleteCommand**：
1. `Sheets.RemoveAt(index)`が実行済み（`SheetNavigationViewModel.cs:145`）
2. `_owner.CurrentSheetIndex = Math.Min(index, Sheets.Count - 1)`を実行（146行目）
3. セッタ内`oldSelectedSheet`捕捉：削除後の縮小済みコレクションを、削除前のindexに近い値で読むため、削除された実際のシートとは無関係の別シート（またはnull）を返す
4. 例：`[A,B,C]`でB(index=1)選択中に削除→`Sheets=[A,C]`、`Math.Min(1,1)=1`（数値上不変）→ネスト通知は`Sheets[1]=C`をoldとして報告（実際の旧選択Bとは無関係）
5. 直後に153行目の`OnPropertyChanged(nameof(SelectedSheet), sheet)`（正しくold=B）が発火
6. **結果：修正前は常にold=null（P-044が問題視した状態）だったのに対し、修正後は「もっともらしいが誤った具体的シート」が1回目のログに残り、診断担当者が最初の行を鵜呑みにすると誤診断を招く。**

### 実害範囲

`ViewModelBase.OnPropertyChanged(string, object?)`の`oldValue`は`TraceLog.LogPropertyChanged`にのみ渡され、`PropertyChangedEventArgs`（`PropertyName`のみ保持）には反映されない。**WPFバインディング・UI表示への機能的実害はない**（既定OFFのオプトイン診断機能限定）。ただし、T-050/P-044の目的そのもの（正確な旧値のTraceLog記録）が、AddCommand/DeleteCommandの2経路では**完全には達成されておらず、むしろ修正前より紛らわしい中間状態（もっともらしい誤値）を生んでいる**点は看過できない。

### 修正方向性（参考、決定は侍に委ねる）

`ResetSheets()`が採用した「呼び出し元から正しい旧値を引数で受け取る」設計（`RefreshSelectedSheet(Sheet? oldValue)`）と同様に、AddCommand/DeleteCommandも**`CurrentSheetIndex`セッタを経由せず**、コレクション変更前に捕捉した正しい旧値を使って`SelectedSheet`の通知を直接行う（`CurrentSheetIndex`自体の更新は`_owner.CurrentSheetIndex = index`ではなく、内部的にセッタのネスト通知を起こさない手段で行うか、あるいはセッタ側で「呼び出し元が既に正しい通知を済ませた」ことを示すオーバーロードを設ける等）方向が筋が良いと考えられる。

---

## 新規バグ（既出、独立発見済み・再掲）

### ResetSheetsのoldValue取得タイミング不正確性（CONFIRMED）

`MainWindowViewModel.ReplaceDocument`は`_currentSheetIndex = 0;`（1491行目、直接フィールド代入）を`SheetNavigation.ResetSheets()`（1521行目）より**先に**実行する。`ResetSheets()`内の`var oldValue = SelectedSheet;`（69行目）実行時点で、`_owner.CurrentSheetIndex`は既に0（新Document用）だが、`Sheets`コレクションは`Sheets.Clear()`実行前＝**旧Documentの一覧のまま**。結果、getterは「旧Documentの先頭シート（index=0）」を返す——旧ドキュメントで2枚目以降が選択されていた場合、誤ったoldValueが通知される。

比較：同じ`ReplaceDocument`内の`OnPropertyChanged(nameof(CurrentSheetIndex), oldSheetIndex)`は`oldSheetIndex`を`_currentSheetIndex`変更**前**（1486行目）に正しく捕捉する既存の正パターンを踏襲している。`ResetSheets()`はこの非対称性を継承していない。

実害範囲は同様にTraceLog診断ログのみ。

---

## 軽微な指摘（参考）

- **Reuse**：`AddCommand`のBeginInvokeラムダ（`IndexOf`→`CurrentSheetIndex`代入）が、既存の`SelectedSheet`セッタとほぼ同一ロジックを複製している。将来`SelectedSheet`セッタの挙動が変わった際に追従漏れが起きうる（このファイル自身がP-030同種リスクとして警戒している構図の再発）。
- **軽微な重複**：`bool wasEmpty = _owner.Document.Sheets.Count == 0;`（93行目）と`DetermineOldSelectedSheetForAdd`内部の`sheetsCountBeforeAdd == 0`判定（56行目）が同じ「空か否か」の判定を2箇所で行っている。実害小。

---

## 派生提案の有無

なし（本レビューの指摘自体が家老采配の範囲内）。

## 不明点

なし（各判定は3独立エージェントの一致・実測相当の追跡により確度が高い）。
