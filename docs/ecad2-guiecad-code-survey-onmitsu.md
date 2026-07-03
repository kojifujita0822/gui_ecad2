# GuiEcadコード調査：保存フロー・ヒットテスト（T-024/T-025・隠密）

> 2026-07-03 隠密調査。家老依頼（T-016と並行、殿承認済み）。前身プロジェクトGuiEcad
> (`C:\Users\kojif\Desktop\生産物\gui_ecad\src`)のソースコードを実地調査し、ecad2実装で
> 同じ不具合・設計上の穴を繰り返さないための実装パターンを抽出した。

---

## T-024: 新規/開く/保存フロー・Dirty判定の抜け穴調査

### 結論

Dirty確認の抜け穴の根本原因は、**「文書破棄を伴う入口が複数あり、かつダイアログ表示手段が
2系統（排他制御ありの`ShowDialogAsync`と、排他制御なしの生`dialog.ShowAsync()`）に分裂しているため、
入口ごとにガードの有無・タイミングが不揃いになっている」**こと。加えて`ConfirmDiscardIfDirtyAsync`
自体は`New`/`Open`/×ボタン/D&D には正しく組み込まれているが、**「再起動」系の別入口
（`OnMenuRestart`）には最初から組み込まれておらず**、これが現存する最も明確な抜け穴。

### 根拠

**Dirtyフラグの管理**（`MainPage.xaml.cs`）:
- `IsDirty => _history.UndoDepth != _savedUndoDepth`（469行）。Undo対象外の変更（ドキュメント情報・
  シート設定・BOM・シート追加/削除/改名・列数変更）は`MarkDirty()`の**手動呼び出しが必要**で、
  呼び忘れがあれば`IsDirty`がfalseのまま検知漏れになる構造。呼び出し箇所は9箇所確認済み。

**`ConfirmDiscardIfDirtyAsync`の呼び出し状況（全数洗い出し）**:

| 呼ばれている（5箇所） | 呼ばれていない（抜け穴） |
|---|---|
| `OnMenuNew`（MainPage.Menu.cs:76） | **`OnMenuRestart`（MainPage.Menu.cs:300-339）** — Dirty判定なしで即座に`Application.Current.Exit()` |
| `OnMenuOpen`（同:84） | `OpenFileOnStartupAsync`（MainWindow.xaml.cs:93）※起動直後で実害なし、意図的 |
| `OnMenuNewFromTemplate`（MainPage.Templates.cs:96） | |
| ×ボタン（MainWindow.xaml.cs:45） | |
| D&Dでファイルを開く（同:84） | |

**設計上の原因**: `MainPage.Dialogs.cs`にWinUI3の「複数ContentDialog同時オープン不可」制約への
対策として`_dialogGate`（SemaphoreSlim）で直列化する`ShowDialogAsync`ラッパーがあるが、
**`ConfirmDiscardIfDirtyAsync`自体を含む複数箇所がこのラッパーを経由せず生の`ShowAsync()`を
直接呼んでいる**。忍者の棚卸し文書（`docs/ecad2-ui-ux-inventory.md`1.5節）は「すべて
ContentDialog…SemaphoreSlimラッパーで直列表示を保証」と記述しているが、**ソースコードの実態と
食い違っている**ことを確認した。この不統一により、×ボタン押下時に既に別ダイアログが開いていると
`OnAppWindowClosing`（`async void`）内で例外が発生し、`async void`の未処理例外はプロセスを
クラッシュさせるため、**確認ダイアログ自体が出せずに異常終了→データ喪失**というシナリオが
構造的に存在する（コードレビューに基づく推定、実機再現検証は未実施）。

### 推奨案（ecad2への適用）

1. **文書破棄操作を単一ゲートウェイに集約**: New/Open/Close/再起動/終了等「現在の文書を手放す」
   全操作を共通の`RequestDiscardCurrentDocumentAsync()`的な単一メソッド経由に強制し、新しい入口を
   追加してもこのゲートウェイを通さない限り破棄できない構造にする。
2. **ダイアログの排他制御を例外なく単一の仕組みに統一**: 「重要だから生で呼ぶ」という特別扱いを
   作らない。WPFはWindowのモーダル入れ子に強い（decision-brief・keyboard-requirements R7参照）ため
   WinUI3のような直列化ラッパー自体が不要になる可能性が高いが、それでも呼び出し経路は1つに絞る。
3. **Dirty判定をUndo履歴のみに依存させない**: MVVM化の前提を活かし、ドキュメントモデルの変更通知
   （`INotifyPropertyChanged`等）に`IsDirty`更新を紐付け、UI側の呼び出し漏れを構造的に防ぐ。
4. **全終了経路を洗い出してテスト項目化**: ×ボタン・Alt+F4・メニュー終了・再起動等、想定される
   全終了経路を明文化し、「未保存状態でこの経路を通ったら確認が出るか」を手動/自動テスト項目に含める。

### GcadSerializer Load/Save API

`GuiEcad.Core\Persistence\GcadSerializer.cs`（静的クラス、`CurrentSchemaVersion = 1`）:
- `Save(doc, path)`: `doc.SchemaVersion`を副作用として書き換えJSON化、`File.WriteAllText`
  （同期・非アトミック、テンポラリ経由リネームなし）。
- `Load(path)`: スキーマバージョンが完全一致しない場合`NotSupportedException`（前方/後方互換なし）。
- **ecad2側（`C:\ECAD2\src\Ecad2.Core\Persistence\GcadSerializer.cs`）は名前空間以外バイト単位で
  同一の移植**であることを確認済み（T-007コミット`88ea0fd`）。

**移植先で注意すべき点**:
- `Save`の`SchemaVersion`書き換え副作用に注意
- 同期・非アトミックI/O — オートセーブがUIスレッドから直接同期呼び出しされる設計を踏襲するなら
  バックグラウンド化・一時ファイル→リネームのアトミック化を検討
- スキーマ厳密一致のみ — 将来のスキーマ進化時の互換読込パスが未整備なことを認識
- Load/Saveの例外がそのまま呼び出し元へ伝播 — 技術的例外メッセージをそのままユーザーに見せない
  変換層をecad2では最初から組み込むことを推奨

**不明点**: `Application.Current.Exit()`が実機で`AppWindow.Closing`を発火させるか否かは実機検証が必要。

---

## T-025: 要素選択・ヒットテストロジック調査

### 結論

ヒットテストは「座標→グリッドセル変換 + 要素リストの線形走査によるセル包含判定」方式。
`GridGeometry`でmm座標をグリッド行/列に変換した後、`_sheet.Elements`を毎回ループする素朴な実装で、
空間インデックス（グリッド配列・クアッドツリー等）は存在しない。要素種別ごとに独立したHitTest
メソッドがあり、`OnPointerPressed`内で決め打ちの優先順位で順に呼び出す方式。

### 根拠

**実装の所在**（`GuiEcad.App\MainPage.xaml.cs`）:
- `HitTest(int row, int col)`: `_sheet.Elements`を線形走査し`PartResolver.BoundarySpan`で得た
  左右境界内かで判定、先勝ち。
- `HitTestConnector`/`HitTestFrame`/`HitTestDot`/`HitTestFreeLine`/`HitTestImage`: 各種別ごとに
  許容誤差付きの近傍判定（枠は最小面積優先、画像は逆順走査で手前優先）。

**座標変換**（`GuiEcad.Core\Rendering\GridGeometry.cs`）: mm⇔グリッド座標の純粋変換をreadonly
structで分離。画面DIP⇔mmは`CanvasViewport.ToWorld`が担当し、ズーム・パン状態と幾何計算を分離。

**優先順位**: 明示的なZオーダー値は無く、`OnPointerPressed`内のif-elseチェーンの記述順が
優先順位そのもの：要素→縦コネクタ→枠→接続点→自由直線→画像（画像は「背面固定描画のため
当たり判定は最後」とコメントで明記）。同一グリッドセルへの複数要素同時配置は`PlaceElementAt`が
拒否するため、重なり自体がほぼ発生しない設計。

**GridPos/PortDef**: `GridPos(Row, Column)`は要素の配置座標、`PortDef(Name, RowOffset,
BoundaryOffset)`は要素種別ごとの固定ポート定義。**ポート個別のヒットテストは存在しない** — 
`Ports()`/`BoundarySpan()`は(1)要素の占有セル範囲判定、(2)`NetlistBuilder`でのネットリスト構築
（座標一致による結線判定）の2用途のみに使われ、「端子をクリックして配線開始」のようなUI操作は
GuiEcadに前例がない。

### 推奨案（ecad2への適用）

**踏襲すべき点**:
- `GridGeometry`のような「mm⇔グリッド座標の純粋変換クラス」の分離（描画・ヒットテスト双方から
  共有可能、テスタブル）
- 要素種別ごとの独立HitTestメソッド＋呼び出し順による優先順位表現（種別が少数のうちはKISSに合致）
- `PortDef`＝ノード座標宣言、電気的接続判定はネットリスト構築時に座標一致で行う、という
  UI操作とロジックの疎結合設計

**改善を検討すべき点**:
- `HitTest`はO(n)の全走査。ecad2で大規模盤図（数千要素）を想定するなら、行をキーにした
  `Dictionary<int, List<ElementInstance>>`程度のインデックスを検討（過剰実装は避け、計測してから判断）
- 優先順位がif-elseチェーンにハードコード。選択対象の種類が増える見込みなら「ヒットテスト候補＋
  優先度」のテーブル化を検討（現状6種類程度なら必須ではない）
- ポートレベルのヒットテストはGuiEcadに前例がないため、ecad2で「端子クリックで配線開始」の
  ようなUXを要求するなら新規設計が必要

### 不明点
- `_sheet.Elements`の順序がZオーダー（描画順）と厳密一致するかは`Sheet.cs`未確認（実質的な影響は
  小さいと推定）
- キーボードのみでの要素選択ロジック（Tab移動等）は本調査範囲外で未確認
