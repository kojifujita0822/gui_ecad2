# GuiEcad仕様書：Undo/Redo

T-081（殿直接指示、2026-07-12起票、隠密2指名）体系の試作領域。GuiEcad原本
（`C:\Users\kojif\Desktop\生産物\gui_ecad\`）のUndo/Redo実装を実物照合で調査し、`docs/spec/ecad2-spec-undo-redo.md`
（ecad2側、T-075起票）と同一領域区分・比較可能な形で纏める。

対応するecad2側仕様書：`docs/spec/ecad2-spec-undo-redo.md`

---

## 0. 前提：GuiEcadは全操作対応、ecad2はMVP限定（最重要）

ecad2側は「シート追加/削除のみ」というMVP限定範囲だが（`ecad2-spec-undo-redo.md`0節）、GuiEcadは
**要素配置から画像挿入まで、編集操作のほぼ全域をコマンドパターンでUndo/Redo対応済み**。両者の
対象範囲の非対称性が本領域最大の差分である（4節で詳述）。

---

## 1. アーキテクチャ：`IUndoCommand` + `CommandHistory`（コマンドパターン、逆操作記録型）

`src/GuiEcad.App/Commands/IUndoCommand.cs`（全12行）：

```csharp
internal interface IUndoCommand
{
    void Execute();
    void Undo();
    Sheet Target { get; }  // シート削除時の履歴除去に使用
}
```

`src/GuiEcad.App/Commands/CommandHistory.cs`（全57行）：

- 保持方式：`Stack<IUndoCommand>`を2本（`_undo`/`_redo`、7-8行）。中身はJSON文字列ではなく
  **コマンドオブジェクトそのもの**（ecad2の「ドキュメント全体スナップショット」方式とは根本的に異なる）。
- `CanUndo`/`CanRedo`（10-11行）：件数判定のみ、ecad2と同型。
- `UndoDepth`（12行）：Undoスタック件数を返すプロパティ。**ecad2の`UndoManager`には相当プロパティなし**。
- `Execute(IUndoCommand cmd)`（14-19行）：`cmd.Execute()`実行→Undoスタックへ`Push`→**Redoスタックを`Clear()`**
  （ecad2の`RecordSnapshot`と同型の分岐時Redo破棄）。
- `Undo`/`Redo`（21-35行）：`Pop`→`cmd.Undo()`または`cmd.Execute()`→対辺スタックへ`Push`。
- `Clear()`（37-41行）：両スタックをClear。
- `RemoveCommandsForSheet(Sheet sheet)`（43-56行）：**指定シートを対象とするコマンドのみを履歴から
  選択的に除去**（`ReferenceEquals(c.Target, sheet)`で判定、Stack順序を保ったまま積み直す）。
  シート削除時に他シートの履歴を残したまま該当シート分のみ破棄する機構——**ecad2の`Clear()`は
  ReplaceDocument時の全履歴一括破棄のみで、この種の選択的除去は存在しない**（3節・4節参照）。

---

## 2. コマンド一覧・対象範囲（35種の個別操作コマンド+`BatchCommand`＝計36クラス）

`src/GuiEcad.App/Commands/ElementCommands.cs`（全639行）に実装。カテゴリ別内訳：

| カテゴリ | コマンドクラス | 件数 |
|---|---|---|
| 要素（配置/削除/移動） | PlaceElementCommand・DeleteElementCommand・MoveElementCommand | 3 |
| フリーライン | PlaceFreeLineCommand・DeleteFreeLineCommand・MoveFreeLineCommand | 3 |
| 接続点(Dot) | PlaceDotCommand・DeleteDotCommand・MoveDotCommand | 3 |
| 縦コネクタ | AddConnectorCommand・DeleteConnectorCommand・MoveConnectorFullCommand・MoveConnectorCommand | 4 |
| ワイヤブレーク | AddWireBreakCommand・DeleteWireBreakCommand | 2 |
| デバイス名/コメント/パラメータ | RenameDeviceCommand・SetCommentCommand・SetParamCommand | 3 |
| グループ枠(Frame) | AddFrameCommand・DeleteFrameCommand・RenameFrameCommand・MoveFrameFullCommand・MoveFrameCommand・SetFrameBorderStyleCommand | 6 |
| 行操作 | InsertLastRowCommand・DeleteLastRowCommand・InsertRowCommand・DeleteRowCommand | 4 |
| ラング注釈(Rung Comment) | SetRungCommentCommand・AddRungCommentCommand | 2 |
| 画像 | AddImageCommand・DeleteImageCommand・MoveImageCommand・ResizeImageCommand・SetImageTracingOnlyCommand | 5 |
| 複合 | BatchCommand（複数コマンドをまとめ、逆順Undo） | 1 |

**訂正記録**：`docs/archive/ecad2-t051-undo-redo-design-survey-onmitsu.md:26`は「約28種類」と記載しているが、
本調査で`ElementCommands.cs`を実物再照合したところ**35種+BatchCommand=計36クラス**が正確な件数（概数
ではなく列挙による確定値）。T-051調査時点では概数表記だったための差と見られる（推測）。

各コマンドはExecute時に変更前後の値をフィールドで自己保持し、Undoで復元する「逆操作記録」型
（ecad2のMemento/スナップショット差分方式ではない）。

---

## 3. Undo/Redo実行時の処理

`src/GuiEcad.App/MainPage.Menu.cs:31-32`：

```csharp
private void DoUndo() { _history.Undo(); RefreshDevicePanel(); RefreshPropertiesPanel(); Canvas.Invalidate(); }
private void DoRedo() { _history.Redo(); RefreshDevicePanel(); RefreshPropertiesPanel(); Canvas.Invalidate(); }
```

`CommandHistory.Undo/Redo`実行後、機器パネル・プロパティパネル・キャンバス描画を明示的に再描画。
ecad2の`ApplyUndoRedoSnapshot`（`SelectedCell`維持・`Tool`状態維持等の細かな意味論定義）に相当する
**選択状態やツール状態の扱いを定めた専用メソッドは見当たらない**——各コマンドの`Undo()`実装が個別に
自分の対象データのみを戻す設計のため、選択状態等はコマンド側で意識する対象外と見られる（推測、
`ElementCommands.cs`各コマンドの`Undo()`実装は今回精読していない範囲）。

---

## 4. `IsEnabled`連動：ボタン・メニューとも常時有効（ecad2と挙動が異なる）

`src/GuiEcad.App/MainPage.xaml:134-135`（メニュー）・`198-202`（ツールバーボタン）ともに
`Click="OnMenuUndo"`/`Click="OnMenuRedo"`のみで、**`IsEnabled`バインディングは一切存在しない**
（grep確認：`IsEnabled`絡みのUndo/Redo関連ヒットなし）。

- 履歴が空の状態でクリックしても、`CommandHistory.Undo/Redo`内部の`if (!CanUndo) return;`
  （`CommandHistory.cs:23,31`）で静かに無視される——**クラッシュ等の実害はないが、ボタンは常に
  クリック可能な見た目のまま**（グレーアウトなし）。
- ecad2側は`RelayCommand`のCommand バインディングにより、WPFの`CommandManager`標準機構で
  `CanExecute`結果が自動反映され、履歴が空なら自動的にグレーアウトする
  （`ecad2-spec-undo-redo.md`4節）。**この自動グレーアウトはWPFの標準機構に依るもので、GuiEcad側
  (WinUI3)で同じ効果を得るには個別の`IsEnabled`バインディングまたは手動更新コードの追加実装が
  必要だったと見られる（未実装のまま、推測）。**

---

## 5. 履歴クリア（シート単位の選択的除去、ecad2との差分大）

- `CommandHistory.Clear()`：全履歴一括破棄。呼び出し箇所は本調査では特定していない（範囲外、
  ecad2の`ReplaceDocument`相当の呼び出し元は未確認、不明点として記録）。
- **`RemoveCommandsForSheet(Sheet sheet)`（1節参照）**：シート削除時に該当シートを`Target`とする
  コマンドのみを選択的に履歴除去。**ecad2にはこの機構が存在しない**——ecad2はUndo対象がシート
  追加/削除のみで、ドキュメント全体スナップショット方式のため「特定シートに紐づく履歴だけを消す」
  という概念自体が生じない（アーキテクチャの違いに起因する必然的差分）。

---

## 6. キーボードショートカット・カスタマイズ性（GuiEcadのみの機能）

`src/GuiEcad.App/MainPage.KeyBindings.cs:17`のコメントに「カスタマイズ可能: Undo/Redo・保存・削除・
検索・コメント編集・行追加削除・コピペ」と明記。Undo/Redoの既定キーバインドは50-53行：

```csharp
new() { Id = "Undo", Label = "元に戻す", DefaultKey = VirtualKey.Z, DefaultModifiers = VirtualKeyModifiers.Control,
        Execute = () => { DoUndo(); return true; } },
new() { Id = "Redo", Label = "やり直し", DefaultKey = VirtualKey.Y, DefaultModifiers = VirtualKeyModifiers.Control,
        Execute = () => { DoRedo(); return true; } },
```

既定値はecad2と同じCtrl+Z/Ctrl+Yだが、`DefaultKey`という命名（「既定」を含意）と保存されたキー割当
テーブル構造から、**ユーザーによるキー再割当機構がGuiEcad側に存在すると見られる**（本調査ではこの
カスタマイズ機構自体の実装詳細＝設定画面・永続化方法は未調査、不明点）。ecad2側にはキーバインド
カスタマイズ機能は存在しない（`ecad2-spec-menu-toolbar.md`に該当記述なし、ハードコードされた
`Window_PreviewKeyDown`のみ）。

---

## 7. テスト状況

`tests/GuiEcad.Tests`・`tests/GuiEcad.UiTests`を「Undo」「Redo」「CommandHistory」でgrep、**0件ヒット**
（ヒットファイルなし）。T-051調査時点の記録と一致——**Undo/Redo関連のテストは存在しない**。
ecad2側は`UndoManager`・`ApplyUndoRedoSnapshot`関連のテスト件数は本調査範囲外（別途確認要、不明点）。

---

## 8. GuiEcadとecad2の比較（一覧）

### (1) GuiEcadのみにある機能

| 機能 | GuiEcad実装箇所 | 備考 |
|---|---|---|
| 要素配置以外の広範な対象（配線・グループ枠・行操作・画像・パラメータ等） | `ElementCommands.cs`全35種 | ecad2はシート追加/削除のみのMVP範囲 |
| シート単位の選択的履歴除去（`RemoveCommandsForSheet`） | `CommandHistory.cs:43-56` | ecad2はシート削除時の個別除去なし（全体スナップショットゆえ不要） |
| Undoスタック深さの取得（`UndoDepth`） | `CommandHistory.cs:12` | ecad2の`UndoManager`に相当プロパティなし |
| キーボードショートカットのカスタマイズ機構 | `MainPage.KeyBindings.cs:17,50-53` | ecad2はハードコード固定（詳細=6節、カスタマイズUIの実装詳細は未調査） |
| `BatchCommand`による複数コマンドの一括Undo | `ElementCommands.cs:625` | ecad2に複合操作の一括Undo概念なし（コード上未確認） |

### (2) ecad2のみにある機能

該当なし。ecad2のUndo/Redo対象範囲はGuiEcadの範囲に完全に包含される（シート追加/削除は
GuiEcadでも`InsertRowCommand`等とは別カテゴリだが、シート単位の追加/削除相当コマンドが
`ElementCommands.cs`一覧に見当たらない——**シート自体の追加/削除がGuiEcad側でUndo対象か否かは
本調査では確認できず、不明点として残す**）。

### (3) 両方にあるが挙動が異なる点

| 観点 | GuiEcad | ecad2 |
|---|---|---|
| 保持方式 | コマンドオブジェクト（逆操作記録、コマンドパターン） | JSON文字列（ドキュメント全体スナップショット） |
| ボタン`IsEnabled`連動 | 常時有効（無効化バインディングなし、空撃ちは内部でno-op） | 自動グレーアウト（WPF `CommandManager`標準機構） |
| Redoスタッククリアのタイミング | `Execute`時（新規コマンド実行の都度） | `RecordSnapshot`時（操作実行の直前） — 実質同義だがメソッド粒度が異なる |
| 履歴クリアの粒度 | 全体クリア＋シート単位の選択的除去の2種 | 全体クリアのみ（`ReplaceDocument`時） |

---

## 出典

- GuiEcad: `C:\Users\kojif\Desktop\生産物\gui_ecad\src\GuiEcad.App\Commands\IUndoCommand.cs`（全12行）・
  `CommandHistory.cs`（全57行）・`ElementCommands.cs`（全639行、35クラス+BatchCommand実物列挙）、
  `MainPage.Menu.cs:31-32,34-35`、`MainPage.KeyBindings.cs:17,50-53`、`MainPage.xaml:134-135,198-202`、
  `tests/GuiEcad.Tests`・`tests/GuiEcad.UiTests`（grep確認、0件ヒット）
- ecad2: `docs/spec/ecad2-spec-undo-redo.md`（比較対象）
- 先行調査：`docs/archive/ecad2-t051-undo-redo-design-survey-onmitsu.md`（T-051、GuiEcad実装の第一次
  調査。本書は実物再照合により件数訂正（2節）を含め独立に確認し直したもの）

## 不明点

- `CommandHistory.Clear()`の実際の呼び出し箇所（本調査では未特定）。
- 各コマンドの`Undo()`実装詳細（選択状態・ツール状態の扱いを個別に精読していない）。
- キーバインドカスタマイズ機構自体の設定画面・永続化方法。
- GuiEcad側でシート自体の追加/削除がUndo対象か否か（`ElementCommands.cs`にシート単位コマンドが
  見当たらないが、他ファイルに実装がある可能性を排除できていない）。
- ecad2側`UndoManager`関連のテスト件数（本調査範囲外）。
