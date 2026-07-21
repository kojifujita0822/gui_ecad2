# ecad2 仕様書：機器表・BOM

T-075（殿裁定、2026-07-11起票）体系の第6号、第4弾1件目。実装コード・殿裁定記録
（`docs/todo.md`/`docs/todo-archive.md`/`docs/proposed.md`）・忍者実機検証記録（`docs-notes/`配下）を
突き合わせ、「仕様として確定している挙動」を出典付きで明文化する。

---

## 0. 要点：機器表・BOMのPDF出力はT-060で実装済み、Device.CommentはT-107で新設

**（2026-07-21更新、T-060・T-107反映）** 画面の機器表（`DeviceTableGrid`）とPDF出力の
BOM表・クロスリファレンス表は、`PdfExporter.Export`（`src/Ecad2.Pdf/PdfExporter.cs`、T-060、
殿裁定2026-07-12）が`DiagramRenderer.RenderBomPage`・`RenderCrossRefPage`を呼び出す形で実際に
機能する（メニュー「PDF出力(_P)」/Ctrl+Pから、`PdfExportMenuItem_Click`→`PdfExporter.Export`、
`MainWindow.xaml`916行に`Click`属性あり）。**旧版本節の「App層から一切呼び出されていない」
「到達不能」という記述はT-060実装（2026-07-12）以前の情報であり誤り。**

画面の機器表とPDF側のBOM表は、コード上は同一ソートキー（`Name`昇順・大小無視）・同一の種別
表示文言（`DeviceClassLabel`）で設計されている。

**T-107（2026-07-21）でDevice.Commentプロパティが新設**された（Model/Makerと同じ位置づけ、
同一デバイス名の全要素間で共有される注記）。ただし**機器表グリッド（`DeviceTableGrid`）自体には
Commentの表示・編集列が無い**（機器名・種別・型式の3列のみ、下記6節参照）。編集はプロパティ
パネル（選択中要素の`SelectedElementComment`、`Document.Devices.ByName`経由）から行う。PDF出力
のクロスリファレンス表（`CrossReference`、機器表とは別ページ）のコメント列にはDevice.Commentが
反映される（`docs/spec/ecad2-spec-placement.md`参照、機器名ラベル下にラダー図本体でも表示）。

---

## 1. `Device`の生成・登録

`Document.Devices.ByName`への新規`Device`追加は**2箇所のみ**：

| 箇所 | 条件 |
|---|---|
| `PlaceElementAtSelectedCell`（要素配置時、`MainWindowViewModel.cs:1511-1512`） | `deviceName`非空かつ未登録名のときのみ新規登録（既存デバイス名は上書きしない） |
| `SelectedElementDeviceName`セッター（プロパティパネル編集、空→非空、1237-1238行） | 同様に未登録名のみ新規登録 |

旧名→新名の**改名**（両方非空）は新規`Device`生成ではなく`DeviceRenamer.Rename`（3節）に委譲される。

### `DeviceClass`の決定（`ResolveDeviceClass`、1476-1485行）

`PartPalette.Entries`から`element.PartId`一致エントリを検索し、`Category==""`かつ`Role=ContactNO`
かつ`IsOrEligible=false`なら`SelectSwitch`固定、それ以外は`PartResolver.CreatesComponent`→
`ComponentKind`→マッピング、非対応パーツは`Other`にフォールバック。**登録時に一度だけ決定され、
以後要素種別が変わっても再計算されない**（`Model`/`Maker`/`Quantity`同様、値の後追い変更手段は
存在しない）。

---

## 2. `Device`の削除

`RemoveDeviceIfUnreferenced`（`MainWindowViewModel.cs:1286-1294`）が唯一の削除ロジック：
`Document.Sheets`全体を走査し、当該`deviceName`を参照する要素が1件もなければ`ByName.Remove`。

呼び出し元3箇所（参照有無判定→除去のロジックはT-055増分3往復1周目で一本化済み）：
- `SelectedElementDeviceName`セッター、非空→空への確定時
- `DeleteSelectedElement`（Deleteキー）
- `CleanupRemovedDeviceNames`（行削除`RowOps.DeleteRow`起因の一括削除時）

`DeviceTable.Refresh()`は呼び出し元の責務として各メソッド末尾で明示的に呼ばれる。

---

## 3. `DeviceRenamer`（改名）

`src/Ecad2.Core/Simulation/DeviceRenamer.cs`。`Rename(doc, from, to)`：

1. `doc.Sheets`全体の`Sheet.Elements`を走査し、`DeviceName`が`from`と大小無視一致する要素の
   `DeviceName`を`to`に置換。
2. `doc.Devices.ByName`のキーを`from`一致で検索、見つかれば`Remove`後に`device.Name=to`として
   `ByName[to]=device`で再登録——**同一`Device`インスタンスを保持したままキーとNameのみ変更**
   （`Model`/`Maker`/`Quantity`は維持される）。

呼び出し元は`SelectedElementDeviceName`セッターのみ（旧名・新名がともに非空の場合に限定）。
`DeviceTable`未登録の`from`はシート上の`DeviceName`だけ置換されキー移行は発生しない。

---

## 4. `DeviceTableViewModel`

`src/Ecad2.App/ViewModels/DeviceTableViewModel.cs`。

- `BuildList()`：`ByName.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList()`——
  **Name昇順（大小無視）、登録順ではない**。
- `Refresh()`：`Devices`を作り直し`OnPropertyChanged`（旧値明示渡し）。
- `Rebind(DeviceTable table)`：参照先`DeviceTable`自体を差し替えてから`Refresh`。新規/開く
  （`ReplaceDocument`）、Undo/Redo（`ApplyUndoRedoSnapshot`）から呼ばれる。
- `Device`が`INotifyPropertyChanged`未実装のため、個別セル値変更の追跡ができず**スナップショット
  全差し替え方式**（`docs/spec/ecad2-spec-sheet-document.md`4節の状態プロパティとも整合）。

---

## 5. `CircuitNumberer`との関係（機器の自動採番は存在しない）

`CircuitNumberer.cs`は`Sheet.Lines`（`CircuitLine{Row, CircuitNumber}`）に**行番号ベースの回路番号**
を採番するもので、`Device`クラスとは無関係（`Device`を一切参照しない）。

**機器の自動命名（例：CR1, CR2…のような自動採番）はリポジトリ全体を通じて実装されていない**
（`Device.Name`はユーザー手入力のみ）。さらに`CircuitNumberer.Number`の呼び出し箇所は`src`配下に
存在せず（`CrossReference.cs`のコメント言及のみ）、**現状App層から一切呼ばれていない未結線状態**。

T-055殿裁定（2026-07-11）でも「`Sheet.Lines`は実質未使用と確定（書込・読取経路とも呼び出し元
なしのデッドコード）」「回路番号(CircuitNumber)は母線名とは別概念、対象外」と明確に整理されている
（`docs/spec/ecad2-spec-sheet-document.md`5節と一部重複するが機器表視点で再掲）。

---

## 6. 裁定根拠

### T-036「機器表基盤」（完全Done、2026-07-05）

起票元はP-011（忍者、T-015実機検証中の発見「配置時に機器表へデバイス名が反映されない」）。
殿裁定「修正する」で起票。実装2点：(1)配置時反映（全配置経路でdeviceName非空なら機器表へ即時
登録、非上書きポリシー）(2)検証中に発覚したデバイス名編集不能バグの一連修正
（`UpdateSourceTrigger=LostFocus`がFocusScope跨ぎで不発火する構造問題への対処、Explicit化＋
LostKeyboardFocus/EnterでのUpdateSource明示実行）。派生でP-013（編集中のCtrl+S等での入力消失）が
起票され、後にT-049で対処済み。

### T-053「機器表『種別』列の日本語表示化」（完全Done、2026-07-11）

起票元はP-020（「種別」列が要素種別によらず一律「Other」表示になる現象）。ElementKind→
DeviceClassマッピング自体はT-045増分Bで実装済みと判明、T-053で残るのは表示（日本語化）のみと
確定。殿裁定＝案A（既存の`DiagnosticSeverityToTextConverter`と同型の`DeviceClassToTextConverter`
新設）、**日本語ラベルはPDF出力の既存表記と統一する**（PDF出力機能自体はT-060で実装済み、
前述0節参照）。

### T-066「BOM編集」（完全Done、2026-07-12、コミット`fe11e30`/`fd67ed9`）

着手前調査（`docs/ecad2-t065-t066-pre-investigation-onmitsu.md`）を踏まえ、殿裁定で**型式（Model）
列のみ**編集可能とする案が確定・実装された（メーカー・数量列は編集UI自体を追加していない）。

`MainWindow.xaml:459-466`：`DeviceTableGrid`（`AutoGenerateColumns="False"`、
`CanUserAddRows="False"`、`CanUserDeleteRows="False"`）の3列のうち、機器名・種別は
`IsReadOnly="True"`のまま、**型式列のみ`IsReadOnly`指定なし＝編集可能**
（`Binding="{Binding Model}"`）。

`CellEditEnding="DeviceTableGrid_CellEditEnding"`（`MainWindow.xaml.cs:197-204`）：Bindingが
`Device.Model`へ直接書き戻すため、ハンドラ自体は`MarkDirty()`呼び出しのみを担う。ただし
`EditAction!=Commit`（キャンセル時）は無視し、さらに編集要素(`TextBox`)の新値と`Device.Model`の
旧値を比較して**実際に変化した場合のみ**`MarkDirty()`する同値ガードを実装（コメントに「隠密静的
レビュー指摘C、往復1周目」と明記）——他のプロパティ編集箇所と同じ同値ガード規約に揃えた形。

---

## 7. 既知の罠・実装バグ

### 修正確認済み

- T-036検証時、プロパティパネルのデバイス名欄をUIA経由（`ValuePattern.SetValue`）で編集しても
  機器表・キャンバスへ反映されない現象を発見→修正確認済み（Enter/Tab/欄外クリックの3経路で反映）。
- 空文字確定時に機器表へ元のデバイス名エントリが孤立残存する現象を発見→`Delete`と同一ポリシーで
  解消確認済み。
- 境界外セル配置による機器表/DRCのみに出る幽霊機器（P-024）→T-045増分Bで再発しないことを確認。

### 未解決・参考記録

- **P-012**：行0での母線・シンボル描画異常。機器表自体への反映は正常だが描画層の問題として保留継続
  （`docs/spec/ecad2-spec-placement.md`7節と同一事象）。

### UI Automation検証固有の罠（実装バグではない）

- **DataGridのUI仮想化により、スクロール範囲外（非表示）の行がUIAツリーに存在しない**——実際には
  全件生成されているにもかかわらず`FindAll`で取得できず件数不足に見える。全件検証時はウィンドウ/
  パネルを十分な高さにするかスクロールしてから確認する必要がある（`ecad2-ui-automation`スキルへの
  追記候補として忍者から申告済みだが、本調査時点でスキル本体には未反映——**気づきとして記録**）。
- プロパティパネル`DeviceNameBox`へのUIA `SetValue`は機器表へ反映されない（配置ダイアログの
  テキストボックスは正常）——実装バグではなく検証手法固有の制約。

---

## 8. 実機確認記録

- 配置直後の機器表反映（F5/F6/F7/Shift+F5、部品選択リスト経由、OR配置）はいずれも正常確認済み。
  デバイス名空欄配置は機器表に載らない、同名2回配置は重複なし1件。
- 削除時の参照カウント方式（他要素参照ありなら残存、参照なしなら消去）は正常動作確認。
- T-049検証：デバイス名欄へフォーカス保持したまま未確定状態でCtrl+S/Ctrl+N/Ctrl+O/ウィンドウ
  クローズを送出しても、`LostKeyboardFocus`発火で機器表が即座更新され保存ファイルにも正しく反映
  されることを確認。未確定編集もIsDirty判定に取り込まれる。

## 不明点

- `Device.Maker`/`Quantity`への値セット手段は現状も皆無（T-066は型式(Model)列のみを対象として
  完了、メーカー・数量列の編集UIは今回スコープ外のまま）。
- `DeviceClass`が登録時一度だけ決定され後から再計算されない設計の妥当性（要素種別変更時に追従
  すべきかどうかは未検討。T-066は型式列編集のみでこの論点には触れておらず、未解決のまま残る）。
