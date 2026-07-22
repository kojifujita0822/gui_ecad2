# T-065・T-066 静的レビュー報告（隠密）

- 対象コミット: `fe11e30`（未push）
- 変更ファイル: `MainWindow.xaml`／`MainWindow.xaml.cs`／`ViewModels/MainWindowViewModel.cs`／
  `Views/DocumentInfoDialog.xaml(.cs)`（新設）／`tests/Ecad2.App.Tests/MainWindowViewModelTests.cs`
- 手法: 手動確認5観点＋`code-review`スキル（Skill tool、high、8角度finder→1票verify）
- 一次情報: Microsoft Learn（`DataGrid.IsReadOnly`/`CanUserAddRows`）、WPF DataGrid編集ライフサイクルに関する
  技術記事、および本リポジトリの既存実装（`SheetNavigationViewModel.cs`・`MainWindowViewModel.cs`等）との突合

## 結論（仕分け）

### 要修正・重大（2件）

**A. 機器表グリッドの新規行追加・行削除が意図せず有効化されている**
- 箇所: `MainWindow.xaml:458`（`DeviceTableGrid`）
- 経緯: グリッド全体の`IsReadOnly="True"`を削除し列単位の`IsReadOnly`（機器名・種別のみ）に置換した際、
  `CanUserAddRows`/`CanUserDeleteRows`（既定値true）を明示的に`False`にしていない。
- 実害: `DeviceTableViewModel.Devices`は`IReadOnlyList<Device>`と宣言されているが実体は
  `List<Device>`（`BuildList()`が`.ToList()`で生成）。WPFはランタイム型を見るため、この一覧は
  DataGridから見て編集可能な`IList`として機能する。
  - 新規行プレースホルダの「型式」セルへ入力→コミットすると、`Device`が**このスナップショット
    リストのみ**に追加され、`Document.Devices.ByName`（永続化対象の実体）には一切反映されない。
    `MarkDirty()`は発火するため「保存済みのはず」に見えるが、次の`Refresh()`（他のどんな編集でも
    発火しうる）で幽霊行は跡形もなく消える。
  - 行選択→Deleteキーでも同様に、DataGrid上は消えるが`ByName`は変化せず、次の`Refresh()`で復活する。
- verify: CONFIRMED（3finder独立検出＋verify、WPFメカニズム・コード両面で確認）
- 対処案: `DeviceTableGrid`に`CanUserAddRows="False" CanUserDeleteRows="False"`を明示追加。

**B. 型式セル編集中の値が、保存・新規・クローズ時に警告なく破棄されうる（T-049/P-013の再発）**
- 箇所: `MainWindow.xaml.cs`（`SaveDocument`/`SaveAsMenuItem_Click`/`ConfirmDiscardIfDirty`）
- 経緯: 上記3箇所は`CommitDeviceNameEdit()`を呼び機器名編集中のTextBoxを強制コミットしてから
  保存判定を行う（T-049/P-013の既存対策）。しかし本コミットで追加された`DeviceTableGrid`の
  型式セル編集には対応するコミット処理が一切追加されていない。
- 実害: Ctrl+S/N/OはXAMLの`KeyBinding`ではなく`Window_PreviewKeyDown`内の分岐で処理されており、
  フォーカス移動を伴わない。型式セルを編集中（Enter/Tab未押下）にCtrl+Sを押すと`CellEditEnding`が
  発火せず`MarkDirty()`も走らないため、`IsDirty`はfalseのまま。結果、`ConfirmDiscardIfDirty()`は
  未保存扱いと判定できず、New/Open/クローズが**無警告で**入力値を破棄する。保存(Ctrl+S)自体も、
  コミットされていない型式値は書き込まれない。
- verify: CONFIRMED（コード上のフォーカス遷移不在を実証込みで確認）
- 対処案: 上記3箇所に`DeviceTableGrid.CommitEdit(DataGridEditingUnit.Row, true)`相当の強制コミットを
  `CommitDeviceNameEdit()`と並べて追加。

### 要修正・中（2件）

**C. `DeviceTableGrid_CellEditEnding`が値未変更でも無条件`MarkDirty()`**
- 箇所: `MainWindow.xaml.cs`（`DeviceTableGrid_CellEditEnding`）
- 実害: 型式セルを閲覧目的で開いてEnter/Tabで抜けるだけ（無変更）でも`EditAction.Commit`は発火し
  ダーティ化する。既存の同値ガード規約（`SelectedElementDeviceName`の`oldName==newName`ガード等）と
  不整合。
- verify: CONFIRMED
- 対処案: `e.Row.Item`から旧値を取得し比較、変化時のみ`MarkDirty()`。

**D. `DocumentInfoDialog`のnullガードがDate欄のみで他7欄は無防備**
- 箇所: `Views/DocumentInfoDialog.xaml.cs`コンストラクタ
- 実害: `DocumentInfo`の7フィールドはコンパイル時のみ非null（`string`型だが実行時強制なし）。
  `JsonOptions`にも非null強制設定なし。手動編集等で`"companyName": null`を含む`.gcad`を開き
  「図面→ドキュメント情報」を開くと`TextBox.Text = null`で未処理例外・ダイアログクラッシュ。
- verify: CONFIRMED（シリアライズ層に非null強制なしを確認済み）
- 対処案: 他7欄も`?? ""`で統一。

### 経過観察（軽微、3件）

**E. Date欄がnullコンファーム後は常に`""`へ固定化**（非対称）
- ダイアログを開いてOKするだけで`Document.Info.Date`が`null→""`に変わる（JSON上も
  `"date"`キー有無が変わる）。現状これを区別するロジックは無く実害なしだが、Cの修正
  （変更なければ何もしない）を入れれば副次的に解消する。verify: CONFIRMED（低重要度）。

**F. DocumentInfoの8フィールドコピーが3箇所（ctor/OkButton_Click/ApplyDocumentInfo）に分散**
- 将来フィールド追加時、3箇所のうち1つを書き忘れてもコンパイルエラーにならず静かに壊れる。
  `ApplyDocumentInfo`は`info.Revisions = Document.Info.Revisions; Document.Info = info;`の
  丸ごと差し替えで1箇所に集約可能（`LadderDocument.Info`はsetter持ち、`Document.Info`への
  XAMLバインディング無しを確認済みにつき安全）。verify: CONFIRMED。

**G. 型式列の編集がDeviceTableViewModelの仲介（Refresh）層を素通りしている**
- 現状は同一`Device`インスタンス共有につき無害だが、将来`Devices`が複製・フィルタ投影に
  変わると無警告でこの列だけ編集が反映されなくなるリスク。verify: PLAUSIBLE（現状バグなし、
  将来リスクのみ）。

### REFUTED（指摘却下、2件）

- **ApplyDocumentInfoが無条件MarkDirty（既存の同値ガード規約と不整合）** →
  同種の「ダイアログ結果を一括適用」パターンである既存`UpdateSheetSettingsCommand`
  （T-055増分2、隠密レビュー済み）も同様に無条件MarkDirtyであり、これは本コミット固有の
  逸脱ではなく既存パターン踏襲と判明。
- **ApplyDocumentInfoがRelayCommandでなく生メソッド呼び出し** →
  `SaveToFile`/`LoadFromFile`/`NewDocument`等、既に生メソッド直呼び出し＋
  `Window_PreviewKeyDown`分岐でのショートカット対応という前例が同ファイル内に複数あり、
  将来のショートカット追加を妨げるという主張は反証された。

## 殿裁定内容との整合性確認

- T-065: 案A（`RenameDialog`同型）／Revisions除外／新規「図面(_D)」メニュー配下 — **すべて裁定通り**。
- T-066: 型式列のみ編集可能・機器名種別は読み取り専用・Maker/Quantity列追加せず — **すべて裁定通り**。
- ただし型式列を編集可能にした副作用でグリッド全体の行追加・削除まで意図せず解放されてしまった点は
  （A参照）、「型式列のみ」という裁定の意図を実質的に逸脱している。

## 新規テスト2件（`ApplyDocumentInfo_UpdatesFieldsAndMarksDirty`／`ApplyDocumentInfo_DoesNotChangeRevisions`）評価

- DoDの検証としては妥当（8フィールド反映＋MarkDirty、Revisions除外）。
- 一方、T-066側（型式列編集・CellEditEnding経由のMarkDirty連動）に対応する単体テストは無い
  （UIイベントハンドラのため技術的にViewModelテストで検証しづらい制約は理解できる）。
  結果として、最も重大な2件（A・B）はいずれもテストで拾えない領域に潜んでいた。実機確認での
  重点確認を推奨。

## 不明点

- なし（全指摘は一次情報またはリポジトリ既存コードとの突合で検証済み）。

## 派生提案（範囲外の気づき）

- なし（本件はスコープ内の指摘のみ）。
