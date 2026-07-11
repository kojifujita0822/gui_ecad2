# T-055増分3 実装コードレビュー（隠密、増分3差し込み分）

対象: コミット`424130e`（削除対象行への「要素ごと削除」対応、GuiEcad同型）。設計書=
`docs/archive/ecad2-t055-increment3-delete-occupied-design-onmitsu.md`。家老指定5観点の手動確認＋
`code-review`スキル（high、8角度→7候補→1-vote verify）を併用。

---

## 1. 家老指定5観点（手動確認）

| 観点 | 判定 | 根拠 |
|---|---|---|
| (a) 設計書どおりの実行順序（4種削除→GroupFrame判定[未シフト座標系]→4種シフト→GroupFrame位置シフト） | **OK** | `RowOps.cs:48-85`実物確認、コメントに「変更禁止」明記、順序も一致 |
| (b) 境界値9パターン（B1〜B9）のテスト実装 | **OK** | `RowOpsTests.cs`にB1〜B7・B9対応テスト実在（B7は既存テスト`ShiftsTopAndBottomIndependently`を再利用と明記）、`RowInsertDeleteCommandsTests.cs`にB8対応テスト実在 |
| (c) 機器表クリーンアップ（CleanupRemovedDeviceNames）がDeleteSelectedElement同型か | **OK（機能的には正しい）、ただし重複あり** | ロジック自体は正しいが3箇所目の重複（§2-2参照） |
| (d) Theory→Fact分離（VerticalConnector）が設計書§3.3と整合するか | **OK** | 共有`PlaceElementAt`ヘルパーの配置パターン(row,row+1)により新設の端点削除ロジックが誤発火する既存テストを専用Factへ分離、理由もコメントで明記 |
| (e) 増分1・2への不可侵 | **OK** | `RowOps.DeleteRow`の呼び出し元は`DeleteRowAtCommand`のみ、`TryRejectOccupiedRow`は`DeleteRowCommand`/`UpdateSheetSettingsCommand`で無傷、`IsRowOccupied`自体も無変更（角度B検証済み） |

---

## 2. `code-review`スキル（high、8角度→7候補→verify）

### 2-1. 要修正（CONFIRMED、正しさバグ、重大）

**a. 削除対象行そのものを選択中のとき、PropertyChanged未発火→後続編集で別要素を誤改名（データ破損）**

`MainWindowViewModel.cs:1741-1748`（`DeleteRowAtCommand`）。`SelectedCell.Row == row`（削除対象行
そのものを選択中）の場合、`sc.Row > row`がfalseのため`SelectedCell`のsetterが一切呼ばれず、
`SelectedElement`系4プロパティのPropertyChangedが発火しない。`RowOps.DeleteRow`は削除対象行の
要素を消し、後続行を-1シフトするため、同じ座標には別の要素（元は1行下にあった要素）が来る。
右パネルは削除前の要素情報を表示したまま固定される。

再現手順：行3のa接点（DeviceName=X001）を選択→右クリックで「行3を削除」実行→（フォーカス状態は
不問、DeviceNameBoxの表示はPropertyChanged駆動でフォーカスと無関係にX001のまま）→ユーザーが
改めてDeviceNameBoxをクリックして編集しTab確定→`SelectedElementDeviceName`のsetterが
**その時点のSelectedElement**（＝シフトしてきた別要素、例Z003）を`el`として取得し、
`DeviceRenamer.Rename`で**ドキュメント全体**のZ003参照を意図しない新名称へ改名してしまう。

**b. 削除対象行より後ろを選択中のとき、シフト前の座標系でSelectedElementが評価され誤表示のまま固定**

同`MainWindowViewModel.cs:1741-1745`。`sc.Row > row`の場合、`SelectedCell = sc with { Row = sc.Row
- 1 }`が`RowOps.DeleteRow`実行**より先**に評価される。この代入は同期的に`OnPropertyChanged
(SelectedElement)`を発火させるが、この時点で`sheet.Elements`はまだ未シフト（削除前）。よって
「シフト後の新Row」×「シフト前のElements」という不整合な組み合わせで`SelectedElement`が評価され、
誤った要素情報がパネルに表示される。事後（`FinishRowCountChange`）は`CurrentSheet`系のみ再通知し
`SelectedElement`系は再通知されないため、次にユーザーが自分でSelectedCellを動かすまで**永続的に
誤表示のまま固定**される（一瞬の乱れではない）。

このメカニズム自体は既存コード（往復1周目コミット`e9d062a`で導入、`424130e`は変更していない）に
内在するが、**占有行削除の解禁（`424130e`）により到達頻度が実質的に上昇**した。往復1周目の
レビューでは「SelectedCellをRowOpsと同じ規則で追随させるべきか」は検証済みだったが、「同期
PropertyChangedによる中間状態の誤読」という本論点は当時未検証だった。

**a・bは根本原因が共通**（行削除操作がSelectedCellの座標だけでなく、その座標に紐づく実データの
変化までは通知しない設計）。恒久対応としては、`DeleteRowAtCommand`実行後に無条件で
`SelectedElement`系PropertyChangedを発火させる（`RowOps.DeleteRow`実行後にSelectedCellの
setterを経由させる、または明示的に`OnPropertyChanged`を呼ぶ）方式への転換を推奨する。

### 2-2. 要修正（CONFIRMED、保守性）

**c. 機器表クリーンアップロジックの3箇所目の重複**

`MainWindowViewModel.cs:1291-1304`（`CleanupRemovedDeviceNames`、新設）が、`stillReferenced`判定
（`Document.Sheets.Any(...)`）→`ByName.Keys.FirstOrDefault(...)`→条件付き`Remove(key)`という
5-6行のブロックを、既存の`SelectedElementDeviceName`セッター（1234-1241行）・
`DeleteSelectedElement`（1269-1276行）に続き3箇所目としてほぼ同一のロジックで複製している
（変数名の違いのみ）。コミットメッセージ自身が「DeleteSelectedElement同型の機器表クリーンアップを
追加」と重複を認めている。共通private helper（例: `RemoveDeviceIfUnreferenced(string deviceName)`）
への抽出を推奨する。

### 2-3. 経過観察（cleanup、簡潔化）

**d. `RowOps.DeleteRow`内のWhere().ToList()→foreach().Removeパターン**

`RowOps.cs:53-61`。Connectors/WireBreaks/RungCommentsの3種は削除以外に中間リストを使わないため、
`List<T>.RemoveAll(predicate)`一発に置き換えられる（Elementsのみ戻り値用に`Where().ToList()`が
必要）。実害はシート規模（最大60行、要素スパース）から軽微、コード簡潔化の余地として記録。

### 2-4. 経過観察（altitude、設計所見、PLAUSIBLE）

**e. 実行順序契約がプローズコメントのみで構造的強制がない**：将来6種目の行所有要素が追加された際、
コメントを読み飛ばして誤った順序で実装するリスク。既存テストはこの退行を検出できない。

**f. 5種の個別列挙が`DeleteRow`/`InsertRow`/`IsRowOccupied`等複数箇所に分散**：新型追加時の
機械的追記漏れリスク（設計書でも「共通インターフェース抽象化は不要」と判断済み、コスト対効果を
踏まえた既知のトレードオフ）。

**g. `RowOps.DeleteRow`の戻り値`IReadOnlyList<ElementInstance>`がApp層都合でCore層APIに追加**：
将来クリーンアップ対象が広がるとCore層APIがApp層の要求に引きずられ肥大化する懸念。

---

## 3. 結論

**要修正3件（a・b・c）を侍へ差し戻し推奨。a・bは実データ破損（誤改名）に直結する重大バグのため、
忍者実機確認より先に修正が必要。** e・f・gは経過観察のみで着手不要（設計書の既知トレードオフの
延長）。dは軽微な簡潔化提案、緊急性なし。
