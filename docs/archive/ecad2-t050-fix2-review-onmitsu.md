# T-050往復2周目 再レビュー（隠密）

対象コミット: `1d6db37`（Add/Delete二重発火とResetSheets旧値タイミングを解消、268件全合格=新規10）

## 結論

**レビュークリーン。要修正なし。** 家老指定2件のバグ（バグ1=Add/Delete二重発火、バグ2=ResetSheets旧値タイミング）はいずれも正しく解消されたことをコード追跡・実測（`dotnet test`実行、254件App層全GREEN）で確認した。ただし、テストコード自体の静的レビュー（往復2周のため実施）で退行検知力の穴を1件発見し、軽微な指摘を複数添える。

---

## 家老指定5観点への回答

### 観点1: 設計書との突合、層3の本番影響ゼロ

**妥当。** `ViewModelBase.PropertyChangedForTest`（internal event）の購読箇所を全リポジトリgrepで確認したところ、`tests/Ecad2.App.Tests/SelectedSheetNotificationTests.cs`のみで、本番コード（`src/Ecad2.App`配下）からの購読は皆無。`?.Invoke`は購読者ゼロならno-opであり、本番挙動・コストへの影響はない。

設計書が提案した「層1（発火回数）＋層2（純粋関数）＋層3（フック、代替手段）」という優先順位に対し、侍は層3のみで発火回数と旧値の両方を同時に検証する統合的アプローチを採用した。層2（`DeleteCommand`/`ResetSheets`用の純粋関数切り出し）は見送られたが、これらのケースは状態依存が強く純粋関数化が困難な性質を持つため、層3への一本化は妥当な判断と評価する。

### 観点2: 通知移動方式（ResetSheetsは通知せずミラー再同期のみ、ReplaceDocumentが1回通知）の等価性・呼出箇所無影響

**妥当。** `ResetSheets()`の呼び出し元は本体1箇所（`ReplaceDocument`）＋テストコード15箇所（`ConnectionDotDragTests.cs`・`ConnectorDragAndResizeTests.cs`・`FreeLineDragAndResizeTests.cs`・`ManualWiringSheetTypeActivationTests.cs`・`WireBreakDragTests.cs`・`SheetNavigationViewModelTests.cs`）を独立エージェントが全読了し検証。**全箇所が「`Document.Sheets`を直接操作→`ResetSheets()`でミラー同期→別の操作で実際の通知を起こす」という既存のArrangeパターンのみ**で、`ResetSheets()`自体の通知有無に依存するテストは皆無（REFUTED＝懸念なし）。既存268件のGREENと矛盾しない。

### 観点3: SetCurrentSheetIndexCore抽出の公開挙動不変（P-030前科箇所ゆえ厳しく）

**妥当。** 公開`CurrentSheetIndex`セッタは`var oldSelectedSheet = ...; SetCurrentSheetIndexCore(value); SheetNavigation.RefreshSelectedSheet(oldSelectedSheet);`という構成で、旧セッタの4ステップ（SetProperty・NotifyCurrentSheetDependentPropertiesChanged・SelectedCell=null・RefreshSelectedSheet）と処理順序・内容が完全に同一（クロスカット処理3行をそのまま`SetCurrentSheetIndexCore`へ移動しただけ）。DRC出力パネルのジャンプ等、外部直接代入経路は従来どおり1回・正しい旧値で通知される（新規回帰テスト`CurrentSheetIndexSetter_DirectAssignment_..._ExactlyOnceWithCorrectOldValue`で確認）。

**`SelectedSheet`セッタ（`SheetNavigationViewModel.cs:28-45`）のP-047スコープ外判断も妥当と検証**：このセッタは依然として公開`CurrentSheetIndex`セッタを経由するため二重発火自体は起きるが、`Sheets`コレクション自体は変更されない経路（構成不変）のため、`SelectedSheet`セッタ内の`oldValue`捕捉と`CurrentSheetIndex`セッタ内の`oldSelectedSheet`捕捉は**同一時点・同一状態から読むため必ず同じ正しい値**になる（独立エージェントがコード追跡で確認）。バグ1（誤った値を伴う二重発火）とは性質が異なり、実害は「冗長な二重発火」のみに留まるため、今回スコープ外・P-047として先送りにした判断は妥当。

### 観点4: RED証明の整合（8件RED+回帰2件GREEN→修正後全GREEN）

**妥当、4項目すべてCONFIRMED。** 独立エージェントが本体修正を仮想的に巻き戻すコード追跡を行い、以下を確認：
- AddCommand系3件（`[Theory]` 0/1/3）：`SetCurrentSheetIndexCore`を旧`CurrentSheetIndex`公開セッタ代入に戻すと、セッタ内の無条件`RefreshSelectedSheet`ネスト通知＋自前通知で二重発火し`Assert.Single`がRED。
- DeleteCommand系4件（先頭/中間/末尾/下限）：同様に二重発火でRED。
- ReplaceDocumentテスト1件：`ReplaceDocument`の事前捕捉・事後通知を削除し旧`ResetSheets()`内部通知に戻すと、発火回数は1のままだが`SelectedSheet`getterが旧Document先頭シートを返すため`Assert.Same`がRED（発火回数では検出不能・旧値でのみ検出可能というテスト前提も正しいと確認）。
- Rename/DRCジャンプ回帰2件：両方の状態（1周目・2周目）で挙動が同一であり、GREEN固定という申告は妥当。

テストの`ImmediateDispatcherService`（`BeginInvoke`を同期即時実行）により、`Execute()`呼び出し内で全ての発火が観測される設計も確認済み。

### 観点5: 軽微2件（reuse・wasEmpty重複）の残置確認

**申告どおり。** `AddCommand`ラムダのロジック複製（`SelectedSheet`セッタとほぼ同一）、`wasEmpty`判定と`DetermineOldSelectedSheetForAdd`内部判定の重複は、いずれも今回のコミットで変更されておらず残置されている。便乗変更なし。

---

## テストコード自体の静的レビュー（往復2周のため実施）

### 【要検討】DeleteCommand境界値テストの退行検知力不足

`DeleteCommand_RaisesSelectedSheetChanged_ExactlyOnce`（`[Theory]` 4ケース：先頭/中間/末尾/下限）は、`Math.Min(index, Sheets.Count-1)`ロジックの実行経路（先頭=index<remaining、中間=tie、末尾/下限=index>remaining）自体は網羅しているが、**アサーションは発火回数と旧値のみで、削除後の実際の`CurrentSheetIndex`/新`SelectedSheet`の値を一切検証していない**。もし将来`Math.Min(index, Sheets.Count-1)`が単純な`index`に置き換わる退行が起きても、`SelectedSheet`のgetterは境界チェックでnullを返すのみのため、旧値（削除されたシート自身）は変わらず、本テストは**全てGREENのまま通過してしまう**——実質的な退行検知力を欠く。

対処要否は家老・侍の判断に委ねるが、`vm.SheetNavigation.SelectedSheet`（削除後の新選択シート）を各境界値で明示的にアサートする1行を追加すれば安価に解消できる。

### 軽微な指摘（参考、対処不要と判断）

- **命名とアサーション範囲の粒度不揃い**：`AddCommand_..._ExactlyOnce`/`DeleteCommand_..._ExactlyOnce`は名称上「発火回数のみ」に見えるが実際は旧値も検証している（`ReplaceDocument`/`CurrentSheetIndexSetter`系は範囲を名前に明示）。実害なし、将来「旧値検証テストが無い」と誤認するリスクのみ。
- **ReplaceDocumentテストにwasEmpty=0相当ケースの欠落**：バグ2は「旧文書が非空の場合のみ発現する性質」のため、欠落の実害は小さいと判断。
- **P-047スコープ外判断がコミットメッセージのみに存在**：テストファイル本体のXMLコメント・設計書のいずれにも「P-047」への言及がない。将来ファイル単体を読む担当者が「二重発火テストの漏れ」と誤認するリスクは低いが軽微に残る。

---

## code-reviewスキル（8角度）で新規発見・生存した所見（重大度低〜中）

| 所見 | 内容 | 重大度 |
|---|---|---|
| AddCommand遅延コールバックの`IndexOf(sheet)==-1`競合経路 | 「+」でシート追加直後（ContextIdle遅延実行前）に「新規/開く」でDocument丸ごと差し替えると、`SetCurrentSheetIndexCore`はスキップされるが直後の`OnPropertyChanged`は無条件発火し、既に無効な旧Documentのシート参照を伴う不整合通知が飛びうる。**既存コード（前回から変更なし）の一部**、新設テストもこの競合経路は未カバー。発生には狭いタイミング条件が必要 | 低 |
| `SetCurrentSheetIndexCore`＋`OnPropertyChanged`のペアリング不変条件が型システムで強制されない | `internal`のため同一アセンブリ内のどこからでも直接呼べ、「コアを呼んだら対で正しい旧値の通知を撃つ」という規約はコメントとテストのみで担保。将来Sheets構成を変える新規操作追加時に、同型のバグ（二重発火・通知漏れ）が再発しうる | 低〜中（設計上の注意点） |

いずれも今回のコミットが主張する2件のバグの修正自体を損なうものではなく、新規のレグレッションでもない。

---

## 不明点

なし。

## 派生提案の有無

あり——DeleteCommand境界値テストの退行検知力不足（要検討事項）を家老へ申し送る。対処は往復3周目とするか、経過観察とするかは家老・侍の判断に委ねる。
