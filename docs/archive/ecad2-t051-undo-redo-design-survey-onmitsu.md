# T-051 Undo/Redo基盤 設計調査（隠密）

殿直接指示によりT-055増分3設計調査より先行して着手（2026-07-11、家老経由）。
起票背景：P-032（忍者T-041増分5実機確認）を殿裁定で承認したが、着手前侍調査（2026-07-10）で
「ecad2にはUndo機構自体が未実装」と前提が崩れ一旦保留（`docs/todo.md`T-051節）。本調査はその
再開に向けたスコープ再定義（基盤新設の設計調査）にあたる。**本調査は読み取り専用、実装は行わない。**

---

## 1. GuiEcad（旧アプリ）のUndo/Redo実装前例

**結論：前例あり。** `C:\Users\kojif\Desktop\生産物\gui_ecad` にUndo/Redo実装が現存する。

- **層**：`src/GuiEcad.App/Commands/` 配下（App層のみ。Model/ViewModel/Coreには及ばない）
- **方式**：`IUndoCommand`インターフェース（`Execute`/`Undo` + `Target`プロパティ）＋
  Undo/Redo二本の`Stack<IUndoCommand>`を持つ`CommandHistory`クラス。各コマンドがExecute時に
  変更前後の値をフィールドで自己保持し、Undoで復元する「**コマンドの逆操作記録**」型
  （Memento／状態スナップショット差分ではない）。
  - `Execute`時にRedoスタックをクリア（分岐後の操作でRedo履歴を破棄する標準的挙動）
  - 複数コマンドをまとめる`BatchCommand`（逆順Undo）
  - シート削除時に該当シート対象のコマンドを履歴から除去する`RemoveCommandsForSheet`
- **対象範囲**：要素配置/削除/移動、フリーライン、接続ドット、縦コネクタ、ワイヤブレーク、
  デバイス名/コメント/パラメータ編集、グループ枠（追加/削除/リネーム/移動/枠線スタイル）、
  行挿入・削除（要素・コネクタ・枠の連動シフトを含む）、ラング注釈、画像挿入/移動/リサイズ/
  トレース設定——**要素配置に限らず配線・シート構造・プロパティ編集まで広く網羅**。
- **規模感**：`IUndoCommand.cs`12行、`CommandHistory.cs`57行、`ElementCommands.cs`639行に
  約28種類のコマンドクラス、計約708行。UIバインドは`MainPage.KeyBindings.cs`（Ctrl+Z/Ctrl+Y）。
- **テスト**：`tests/GuiEcad.Tests`・`tests/GuiEcad.UiTests`ともUndo/Redo/CommandHistory関連の
  テストは0件（未検証のまま運用されていた）。
- **技術スタック**：WinUI3（`net8.0-windows10.0.26100.0`）。

---

## 2. ecad2現行アーキテクチャの前提条件

### Model層（`src/Ecad2.Core/Model/`）
- すべて可変（mutable）な素のPOCO。`INotifyPropertyChanged`実装なし＝変更を横取りするフックが
  構造的に存在しない（`Document.cs:4-14`, `Sheet.cs:4-28`, `Device.cs:6-16`）。
- `DeepClone()`は**一部の葉ノードのみ**存在：`ElementInstance`・`FreeLine`・`ConnectionDot`・
  `ImageInsert`・`WireBreak`（`Element.cs:60-139`各所）。`Sheet`・`LadderDocument`・`GroupFrame`・
  `VerticalConnector`・`Device`にはDeepCloneが無い。
- `GcadSerializer.Serialize`/`Deserialize`（`src/Ecad2.Core/Persistence/GcadSerializer.cs:13-46`）
  でJSON往復による**ドキュメント全体**の値コピーは代用可能（要素が全てSystem.Text.Json対応POCOのため）。
  ただし粒度は「全シート丸ごと」で、1操作ごとのUndoには重い可能性がある（要検証）。
- 規模感：`GridSpec.MaxRows=60`・既定`Columns=40`（`Sheet.cs:34-39`）、要素はスパース配置。

### Command層（`src/Ecad2.App/Commands/`）
- **`ICommand`実装は`RelayCommand`1種類のみ**。`XxxCommand`という命名の専用クラスは存在せず、
  実体は`MainWindowViewModel`コンストラクタ内で`new RelayCommand(実行ラムダ, CanExecuteラムダ)`
  として生成されるプロパティ（例: `MainWindowViewModel.cs:1350,1356,1689-1716`）。
- 実行ラムダは例外なくModelを**直接・破壊的**に変更（`sheet.Elements.Add/.Remove`・フィールド
  直接代入・`RowOps.InsertRow/DeleteRow`のインプレース変更）。専用の「変更用メソッド」を経由する
  統一規律は無く、操作ごとに直書き。
- 部分的な前例：`ConfirmDrag<T>`（`MainWindowViewModel.cs:928-932`）はドラッグ開始時の値を保持し
  確定時に比較するミニパターンを既に持つ。これを一般化すれば「実行前後スナップショット」または
  「Do/Undoペアのデリゲート」を持つCommandオブジェクト化の足がかりになりうる（構造上の余地の指摘、
  実装が存在するわけではない）。

### 既存の「元に戻せて然るべき」操作の一覧（コード上に実在するもの）
| 操作 | 該当箇所 |
|---|---|
| 要素配置 | `PlaceElementAtSelectedCell`（`MainWindowViewModel.cs:1446-1478`） |
| 要素削除 | `DeleteSelectedElement`（同1259-1285） |
| プロパティ編集（DeviceName） | `SelectedElementDeviceName`セッター（同1207-1250、`DeviceRenamer.Rename`一括改名含む） |
| 縦コネクタ 追加/削除/ドラッグ移動 | 同1051-1054／499-502／443-487 |
| WireBreak 追加/削除/ドラッグ移動 | 同619-622／531-534／604-607 |
| FreeLine 追加/削除/ドラッグ移動 | 同1139-1142／650-653／787-790,839-842 |
| ConnectionDot 追加/削除/ドラッグ移動 | 同979-982／868-871／963-966 |
| 行挿入/削除 | `AddRowCommand`/`DeleteRowCommand`(1642-1660)、`InsertRowBeforeCommand`/`DeleteRowAtCommand`(1689-1716、`RowOps`経由) |
| シート設定一括変更 | `UpdateSheetSettingsCommand`(1663-1684、行数・母線名) |
| シート追加/削除/並べ替え | `SheetNavigationViewModel.cs`（`Add`110／`RemoveAt`148／`Insert`182-183） |
| GroupFrame（グループ枠） | **App層でのAdd/Remove/編集コマンドは未発見**（`RowOps`・`DiagramRenderer`の参照のみ、UI操作としては未実装の可能性が高い＝要検証） |

`Params`・`Comment`はモデルにフィールドは存在するが、UI経由の専用セッターは今回の調査範囲では
未発見（別途確認要）。

### 変更検知フック
`MainWindowViewModel.MarkDirty()`（同96行、`IsDirty`セッターは`private`）が唯一の変更通知フック。
差分検知ではなく**コマンド実行末尾での手動呼び出し方式**（コメント82-88に明記）。呼び出し箇所は
`MainWindowViewModel.cs`内38箇所＋`MainWindow.xaml.cs`3箇所＋`SheetNavigationViewModel.cs`3箇所の
計44箇所。これらは「各操作の変更完了地点」としてUndo記録のフック候補地点そのものだが、裏を返せば
**「MarkDirty呼び忘れ」と同型の「Undo記録漏れ」リスクが構造的に存在する**（GuiEcadのDirty判定不備の
教訓が`docs/archive/ecad2-guiecad-code-survey-onmitsu.md`に記録されている）。

### 副次所見（引き継ぎ済み、再掲）
「元に戻す」「やり直し」ボタンは操作履歴皆無でも常時`IsEnabled=True`（履歴と非連動）。Undo基盤新設時に
併せて対処すべき事項。

---

## 3. 設計方式の選択肢

### 案A: コマンドパターン（逆操作記録型、GuiEcad方式踏襲）
各操作を`IUndoCommand`的なオブジェクト化し、Do時に変更前後の値（削除要素そのもの・シフト量等）を
自己保持、Undoで戻す。

- 実装コスト：**大**。ecad2は現状`RelayCommand`直書きラムダのみで統一規律が無く、全操作を専用
  クラス化する改修が必要（GuiEcad実績で約700行28種類）。
- 対象範囲：理論上は全操作に対応可能（GuiEcadの実績が示す）。
- リスク：GuiEcad同様「テスト無し」で運用されると同種の脆さを引き継ぐ。MarkDirtyと同型の記録漏れリスク。
  ただし既存コマンド層を専用クラスへ再構成する副産物として、直書きラムダの分散も整理される可能性。

### 案B: Memento（スナップショット）方式
操作ごとに変更前後の対象オブジェクト全体（例: Sheet単位）をコピーして保持。

- 実装コスト：**中**。前提として`Sheet`/`LadderDocument`/`GroupFrame`/`VerticalConnector`/`Device`に
  現状無いDeepCloneをまず追加する必要がある。
- 対象範囲：Sheet単位で丸ごと戻すなら比較的シンプルに全操作に対応可能。
- リスク：Sheet全体コピーのメモリ・性能コスト（スパース配置なら軽微と見られるが要検証）。粒度が
  Sheet単位だと、細粒度Undo（1要素のプロパティのみ戻す等）が扱いづらい。

### 案C: ドキュメント全体スナップショット（既存GcadSerializer流用）
既存のJSONシリアライザをそのまま流用し、操作前後でシリアライズ文字列を保持、Undo時にデシリアライズ
して丸ごと復元。

- 実装コスト：**小〜中**。既存機構の流用で新規クラスは最小限。
- 対象範囲：全操作に一律対応可能（粒度に依らず「実行前の全体状態」を保存するだけでよい）。
- リスク：ドキュメント全体を毎回シリアライズするコスト（複数シート・大規模図面で操作の度に発生）。
  ドラッグ中の連続更新のような高頻度操作には不向き（確定時のみ記録すれば許容範囲と見られるが要検証）。
  UIの選択状態（`SelectedCell`等）はUndo対象外とすべきで、モデルとビューモデルの記録範囲の切り分けが要る。

### 隠密所感
3案とも「全操作対応」は理論上可能だが、実装コスト・リスクの立ち上がり方が異なる。**案Cが最小コストで
着手でき、段階導入のMVPと相性が良い**（後述4節）。案Aは対象範囲の網羅性でGuiEcad実績があるが改修
規模が大きく、テスト設計を伴わない移植は同じ脆さの再生産になりうる。決定は殿・家老に委ねる。

---

## 4. 段階導入MVP案（叩き台）

全操作を一度に対応するのは構造的大仕事（殿裁定2026-07-10で保留した経緯どおり）。狭い対象から
始める場合の候補：

1. **候補1: シート追加/削除のみ**（起票背景P-032に直結）。案Cとの相性が良い——シート単位の
   Add/RemoveAt/Insertは`Document.Sheets`という単一リストへの操作のみで、JSON全体スナップショット
   でも「シート追加前/削除前のDocument全体」を1回保存するだけで足りる。操作頻度も低く性能懸念が薄い。
2. **候補2: 要素配置/削除のみ**。頻度が高くUndo需要が体感されやすい中核操作だが、案Aなら専用
   コマンド化が要素種別ごとに必要、案Cなら高頻度操作でのシリアライズコストが候補1より問題になりうる。
3. **候補3: 行挿入/削除**。T-055で直近実装されたばかりの複雑な操作（シフト・GroupFrame連動を含む）。
   影響範囲が広くUndo需要は高いが、実装難度・検証コストも高い。T-055増分3の残論点（GroupFrame内部
   削除の要素ごと削除方針、殿裁定2026-07-11）が確定した直後だけに、Undo対応は仕様が固まってから
   着手する方が手戻りが少ない。

**隠密所感**：起票背景（P-032）を素直に汲むなら候補1が最も筋が通り、かつ実装コスト最小の案Cと
組み合わせやすい。候補2・3は候補1で基盤（Undoスタック・MarkDirty連携・ボタンIsEnabled連動含む）を
先に立ち上げてから対象を広げる二段階目として位置づけるのが妥当と考えるが、優先順位の決定は殿・家老に委ねる。

---

## 5. 不明点・要追加調査

- GroupFrame（グループ枠）のApp層Add/Remove/編集コマンドの有無（未発見、UI未実装の可能性）
- `Params`・`Comment`編集のUI経由専用セッターの有無
- 案B・案Cそれぞれの実際のパフォーマンス（シリアライズ/DeepCloneのコスト実測）は未計測（机上の推測）
- GuiEcad `CommandHistory`のUndoDepth＝Dirty判定運用の詳細（`docs/archive/ecad2-guiecad-code-survey-onmitsu.md`に
  存在の記録のみ、実装詳細は本調査スコープ外で未確認）

---

## 出典
- GuiEcad: `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\`（IUndoCommand.cs・
  CommandHistory.cs・ElementCommands.cs等）、`tests\GuiEcad.Tests`・`tests\GuiEcad.UiTests`（grep確認）
- ecad2: `src/Ecad2.Core/Model/*.cs`、`src/Ecad2.Core/Persistence/GcadSerializer.cs`、
  `src/Ecad2.App/Commands/RelayCommand.cs`、`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`、
  `src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`、`src/Ecad2.App/MainWindow.xaml:115-116,160-168`
- 背景：`docs/todo.md`T-051節、`docs/archive/ecad2-t051-precheck-undo-verification-ninja.md`、
  `docs/archive/ecad2-guiecad-code-survey-onmitsu.md`
