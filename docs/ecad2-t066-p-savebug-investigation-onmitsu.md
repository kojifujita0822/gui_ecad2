# T-066 保存時機器表消失バグ 調査（隠密）

- 依頼元: 家老（2026-07-11、忍者実機確認`docs-notes/ecad2-t065-t066-verify-ninja.md`③のNGを受けて）
- 調査対象: `fd67ed9`のB修正（`CommitDeviceTableEdit()`）が根本原因か
- 手法: コード読解＋一次情報（Microsoft Learn `DataGrid.CommitEdit`、`ListCollectionView`実装、
  技術記事）。**実機での比較実験（fe11e30時点との再現有無の切り分け）は行っていない**（隠密は
  調査専任、実機は忍者の担務）。

## 結論（推測含む、明示区分）

**状況証拠としてB修正が引き金である蓋然性は高いが、WPF内部の完全な発火経路までは静的調査で
特定しきれなかった。以下、事実→仮説の順で記す。**

### 事実（コード確認済み）

1. `CommitDeviceTableEdit()`（`MainWindow.xaml.cs:171-175`）は`SaveDocument`/`SaveAsMenuItem_Click`/
   `ConfirmDiscardIfDirty`の3箇所すべてで、`CommitDeviceNameEdit()`の直後に**常に**呼ばれる
   （型式セルを触っていたかどうかに関わらず無条件）。
   ```csharp
   private void CommitDeviceTableEdit()
   {
       DeviceTableGrid.CommitEdit(DataGridEditingUnit.Cell, true);
       DeviceTableGrid.CommitEdit(DataGridEditingUnit.Row, true);
   }
   ```
2. 忍者再現手順の2回目・3回目は**型式セルに一切触れていない**（a接点配置直後、機器名確定後、
   即Ctrl+S）。それでも機器表から直近配置分が消失している。これは「型式セル編集中の未確定値の
   扱い」という当初の懸念（B修正の設計意図）とは異なる経路で発生していることを意味する。
3. `DeviceTableViewModel.Devices`（`DeviceTableViewModel.cs:13,42-43`）は`Refresh()`のたびに
   **全く新しい`List<Device>`インスタンス**に差し替わる（`_table.ByName.Values...ToList()`）。
   機器配置（`SelectedElementDeviceName`セッター`MainWindowViewModel.cs:1267`、または新規配置
   `MainWindowViewModel.cs:1543`）のたびに`DeviceTable.Refresh()`が呼ばれ、`DeviceTableGrid`の
   `ItemsSource`は都度新しいListへ切り替わる。
4. `DeviceTableGrid`に`SelectedItem`バインディングは無い（`ItemsSource`のみ）。SelectedItem不整合
   ではない。

### 一次情報（確認できた範囲）

- `DataGrid.CommitEdit()`（引数なし版）の公式リマークス："If a cell is not currently being edited,
  all pending row edits are committed."（セル非編集時は行レベルの保留編集をコミットする、という
  フォールバック挙動が明記）。`CommitEdit(DataGridEditingUnit, bool)`版にはこの詳細な分岐の記載が
  無いが、共通ロジックを使っている可能性がある。
- `ListCollectionView.CommitNew()`のソース確認（`dotnet/wpf`）：`if (_newItem == NoNewItem) return;`
  という早期returnがあり、**AddNew操作が進行中でなければ何もせず安全に終了する**ことをソースで
  確認済み。少なくとも`ListCollectionView`単体のレベルでは、「編集していないのに勝手に削除する」
  ロジックは見当たらない。
- Web検索で得た一般的な既知パターン："When a user clicks the New Item row or calls
  `DataGrid.BeginAddNew()`, the DataGrid creates a temporary new item and enters an add
  transaction. If the ItemsSource is replaced before this transaction is completed, the newly
  added item is lost."（ItemsSource差し替えとAddNewトランザクションの競合で新規アイテムが消える、
  というのはWPF DataGridの既知の落とし穴パターン）。ただし、本件ではDataGrid自身のUI経由での
  AddNew（新規行プレースホルダ入力）は`CanUserAddRows="False"`により発生し得ないはずで、
  「ItemsSourceの差し替え」の方向も逆（本件は差し替え→時間を置いて→CommitEdit、という順）。
  そのままでは一致しないが、**「DataGridの編集トランザクション管理とItemsSource差し替えの
  相互作用がバグの温床になりやすい」という構造的な脆さ自体は本件の状況と符合する**。

### 最有力仮説（未確証、推測と明記）

`DeviceTableGrid_CellEditEnding`（C修正後）は`e.EditingElement`から新値を読むが、この一連の
`CommitEdit(Cell,true)→CommitEdit(Row,true)`という2段階呼び出しが、**型式セルを一度も
編集していないのに何らかの形でDataGridに「未確定の編集状態」があると誤認識させ**、その
誤ったコミット処理の過程で、直前の`Refresh()`によって生成された「新しいCollectionViewが
まだ完全に安定していない行」に対して、確定ではなく除去側の処理が働いてしまっている可能性を
最有力とみる。

**確証に至らなかった理由**：`DataGrid.cs`本体（内部の編集トランザクション経路、
`DataGridRow`/`ItemContainerGenerator`との連携部分）はソース行数が8600行超あり、WebFetch経由の
取得では該当箇所（`CommitEdit(DataGridEditingUnit, bool)`のフル実装、および`BeginningEdit`との
相互作用）まで到達できなかった。GitHub code search はサインインが必要で不可。実機デバッグ
（ブレークポイントでの`IsEditingItem`/`IsAddingNew`実測）が最も確実な特定手段と考える。

## 対処案（原因の確定を待たず適用可能な安全策）

Web上のベストプラクティスとしても「`IsEditingItem`/`IsAddingNew`が実際にtrueの場合のみ
CommitEdit/CancelEditを呼ぶ」ことが推奨されている。`CommitDeviceTableEdit()`を無条件実行では
なく、以下のようなガードを掛けることで、根本原因が何であれ「編集していないのに触ってしまう」
経路自体を塞げる可能性が高い：

```csharp
private void CommitDeviceTableEdit()
{
    if (DeviceTableGrid.CurrentCell.Column is null) return; // 何も編集対象になっていない
    // または DeviceTableGrid.CurrentColumn / CurrentItem の編集中フラグ相当を確認
    DeviceTableGrid.CommitEdit(DataGridEditingUnit.Cell, true);
    DeviceTableGrid.CommitEdit(DataGridEditingUnit.Row, true);
}
```

ただし正確なガード条件（DataGridの編集中判定に使えるプロパティ）は要調査。侍のデバッグ実測で
「実際にどのタイミング・どの内部状態で消失が起きるか」を先に特定した方が、対症療法でなく
根治につながると考える。

## 不明点

- `DataGrid.CommitEdit(DataGridEditingUnit, bool)`の完全な内部実装（未編集状態での正確な挙動）
- fe11e30時点（B修正前）で同じ再現手順を試した場合に消失が起きるか否か（未検証、侍または忍者の
  切り分けが必要）
- 「保存を完了させた場合、`Document.Devices.ByName`自体から本当に削除されているか、それとも
  DataGrid表示（DeviceTableViewModel.Devicesスナップショット）だけの見た目の消失か」
  （忍者報告の未確認事項と同一、こちらも未確認）

## 派生提案

- なし（本件はスコープ内の緊急調査）。
