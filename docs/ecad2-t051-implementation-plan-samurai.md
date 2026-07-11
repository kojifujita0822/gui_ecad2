# T-051 Undo/Redo基盤 実装計画（侍起草）

殿裁定を受けての実装計画。本書は**起草段階**であり、実装はまだ着手していない。殿裁可後に着手する。

前提資料:
- `docs/ecad2-t051-undo-redo-design-survey-onmitsu.md`（隠密、設計調査。GuiEcad前例・ecad2現行アーキテクチャ・案A/B/C比較・段階導入MVP案）
- `docs/todo.md` T-051節
- 家老采配DoD5点（2026-07-11）

---

## 0. 殿裁定の確認（本書の前提）

- **設計方式=案C**（既存`GcadSerializer`流用のドキュメント全体JSONスナップショット）
- **MVP対象範囲=候補1**（`SheetNavigationViewModel`のシート追加/削除のみ。要素配置・行操作・シート改名等は対象外）

---

## 1. アーキテクチャ設計

### 1.1 UndoManager（新規、Ecad2.App層）

配置: `src/Ecad2.App/Commands/UndoManager.cs`（`RelayCommand.cs`と同じ`Commands`名前空間）。

```csharp
public sealed class UndoManager
{
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>操作実行の直前に呼ぶ。現在のDocument状態をUndoスタックへ積み、Redo履歴をクリアする。</summary>
    public void RecordSnapshot(LadderDocument doc)
    {
        _undoStack.Push(GcadSerializer.Serialize(doc));
        _redoStack.Clear();
    }

    /// <summary>直前の状態へ戻す。現在の状態はRedo用に積む。履歴が無ければnull。</summary>
    public LadderDocument? Undo(LadderDocument current)
    {
        if (_undoStack.Count == 0) return null;
        _redoStack.Push(GcadSerializer.Serialize(current));
        return GcadSerializer.Deserialize(_undoStack.Pop());
    }

    /// <summary>Undoで戻した状態をやり直す。現在の状態はUndo用に積む。履歴が無ければnull。</summary>
    public LadderDocument? Redo(LadderDocument current)
    {
        if (_redoStack.Count == 0) return null;
        _undoStack.Push(GcadSerializer.Serialize(current));
        return GcadSerializer.Deserialize(_redoStack.Pop());
    }
}
```

- 既存`GcadSerializer.Serialize`/`Deserialize`をそのまま流用（**変更不要**、殿裁定の適用範囲どおり）。
- `RecordSnapshot`は「**操作実行の直前**」に呼ぶ契約（家老DoD(1)指定）。呼び出しは呼び出し元(`SheetNavigationViewModel`)の責務とする。
- **Redo履歴のクリア条件**：`RecordSnapshot`呼び出し時に必ずクリアする（新規操作が入ると分岐先のRedo履歴は無効になるため。GuiEcad`CommandHistory`の`Execute`時クリアを踏襲、隠密調査書1節参照）。`Undo`/`Redo`自体はRedo/Undoスタックをクリアしない（往復可能な設計）。

`MainWindowViewModel`のコンストラクタで`UndoManager`インスタンスを生成し、公開プロパティとして持たせる（`OutputPanel`等の子VMと同じ配置流儀。ただし`UndoManager`自体はViewModelではなく状態管理クラスのため`ViewModelBase`は継承しない）。

### 1.2 呼び出し箇所（対象=`SheetNavigationViewModel`の`AddCommand`/`DeleteCommand`のみ）

- `AddCommand`: `_owner.Document.Sheets.Add(sheet)`実行**直前**に`_owner.UndoManager.RecordSnapshot(_owner.Document)`を呼ぶ。
- `DeleteCommand`: `_owner.Document.Sheets.RemoveAt(index)`実行**直前**に同様に呼ぶ。
- `RenameCommand`は対象外（候補1＝シート追加/削除のみ、殿裁定の範囲外）。

### 1.3 Undo/Redo実行と状態復元

`MainWindowViewModel`へ`UndoCommand`/`RedoCommand`を新設（`AddRowCommand`等と同じ配置流儀、コンストラクタ末尾）。

```csharp
UndoCommand = new RelayCommand(
    () =>
    {
        if (UndoManager.Undo(Document) is not LadderDocument restored) return;
        ApplyUndoRedoSnapshot(restored);
    },
    () => UndoManager.CanUndo);

RedoCommand = new RelayCommand(
    () =>
    {
        if (UndoManager.Redo(Document) is not LadderDocument restored) return;
        ApplyUndoRedoSnapshot(restored);
    },
    () => UndoManager.CanRedo);
```

**`ApplyUndoRedoSnapshot`は新規メソッドとして設計する（既存`ReplaceDocument`を流用しない）**。`ReplaceDocument`は新規/開く専用の「全リセット」処理で、`SelectedCell`/`SelectedConnector`等の記入中状態クリア・`Tool`初期化・`StatusMessage`クリア・`IsDirty=false`まで巻き込む。Undo/Redoの意味論（「シート構成だけを戻す、操作中の状態まではリセットしない」）とは重ならない部分があるため、軽量な専用メソッドを新設する方針とする（設計案は次の「開かれた論点1」参照、確定は殿裁可後）。

設計案（叩き台）:

```csharp
private void ApplyUndoRedoSnapshot(LadderDocument restored)
{
    var oldDocument = Document;
    Document = restored;
    OnPropertyChanged(nameof(Document), oldDocument);
    SheetNavigation.ResetSheets();
    // シート数が変化しうるため、CurrentSheetIndexを新しい範囲へクランプする。
    int clampedIndex = Math.Clamp(_currentSheetIndex, 0, Math.Max(0, restored.Sheets.Count - 1));
    SetCurrentSheetIndexCore(clampedIndex);
    NotifyCurrentSheetChanged();
    // Undo/Redoが「シート0枚⇔1枚以上」の境界を跨ぐ可能性がある(最後の1枚のAdd/Deleteを取り消す場合)。
    NotifyHasProjectChanged();
    DeviceTable.Rebind(restored.Devices);
    MarkDirty(); // Undo/Redoも「変更」の一種として扱う(既存MarkDirty規約を踏襲)
}
```

### 1.4 UIバインド

- `MainWindow.xaml`のMenuItem「元に戻す(_U)」「やり直し(_R)」（121-122行、現状プレースホルダで`Command`未結線）へ`Command="{Binding UndoCommand}"`/`Command="{Binding RedoCommand}"`を追加。
- ToolBar Button「元に戻す」「やり直し」（166-177行、同じくプレースホルダ）へも同様に`Command=`を追加。
- `MainWindow.xaml.cs`の`Window_PreviewKeyDown`へCtrl+Z/Ctrl+Yのグローバルショートカットを追加。既存Ctrl+S/O/N・T-055のCtrl+Shift+Up/Downと同じ`case`パターンを踏襲：
  ```csharp
  case Key.Z when Keyboard.Modifiers == ModifierKeys.Control:
      _viewModel.UndoCommand.Execute(null);
      e.Handled = true;
      break;
  case Key.Y when Keyboard.Modifiers == ModifierKeys.Control:
      _viewModel.RedoCommand.Execute(null);
      e.Handled = true;
      break;
  ```
- **IsEnabled連動（副次所見の対処）**：`RelayCommand.CanExecuteChanged`は`CommandManager.RequerySuggested`経由（既存全コマンド共通の仕組み、`RelayCommand.cs`参照）のため、`Command=`を結線するだけでWPFが自動的に`CanExecute`を再評価し`IsEnabled`が履歴の有無に連動する。明示的な`RaiseCanExecuteChanged`呼び出しは不要（`AddRowCommand`等の既存コマンドと同じ流儀）。これにより「操作履歴皆無でも常時`IsEnabled=True`」の副次所見（引き継ぎメモ記載）が解消する。

---

## 2. テスト設計（往復案件ではないため侍がテストも書く、殿裁定2026-07-08）

- `UndoManager`単体テスト（`tests/Ecad2.App.Tests/`、Core層依存無しの純粋なクラスだがApp層所属のため配置はApp.Tests）:
  - `RecordSnapshot`→`Undo`で直前の状態が復元されること
  - `Undo`→`Redo`で往復できること
  - `RecordSnapshot`（新規操作）でRedo履歴がクリアされること
  - `CanUndo`/`CanRedo`の境界（履歴0件でfalse、1件以上でtrue）
  - 履歴が無い状態での`Undo`/`Redo`は`null`を返し例外を投げないこと
- `SheetNavigationViewModel.AddCommand`/`DeleteCommand`実行後に`UndoCommand`でシート数・シート内容が復元されることの結合テスト
- Undo後のRedoでシート追加/削除がやり直されることの結合テスト
- 新規操作後のRedo履歴クリア確認（Undo→別の追加操作→`RedoCommand.CanExecute`がfalseになること）
- `UndoCommand`/`RedoCommand`の`CanExecute`境界（履歴0件時はfalse、`MenuItem`/`Button`の`IsEnabled`確認は忍者実機確認に委ねる）
- Undo/Redo実行後も`IsDirty=true`であることの確認（開かれた論点2の暫定方針に対応するテスト）

---

## 3. 開かれた論点（殿確認が必要、着手前に回答を得ること）

1. **`ApplyUndoRedoSnapshot`の巻き戻し範囲**：1.3節の設計案は「シート構成（Document全体）のみ戻す、`SelectedCell`・`Tool`状態・`StatusMessage`は現状維持」という最小範囲。GuiEcadの前例は要素配置等も含む広い対象で直接の参考にならない。この最小範囲方針でよいか。
2. **`IsDirty`の扱い**：Undo/Redo実行後も`IsDirty=true`のまま（提案、既存「変更操作の入口でMarkDirty」規約をそのまま適用）とするか。あるいは「保存時点まで戻ればIsDirty=falseに戻す」という高度な追跡は候補1のMVPでは過剰と判断し対象外とするか。
3. **家老采配DoD(2)の「Insert」の解釈**：DoDは「`SheetNavigationViewModel`のAdd/RemoveAt/Insertのみ」と記載があるが、現行コードにシート挿入（先頭以外の任意位置への挿入）・並べ替え機能は存在しない。`RenameCommand`内部の`Sheets.Insert`（`ObservableCollection`のUIコンテナ再構築用、`RemoveAt`+`Insert`の一部）はシート数を変えない実装詳細のため対象外と判断した。この解釈で正しいか確認したい。
4. **ボタン配置の確認**：メニュー「元に戻す」「やり直し」・ツールバー「元に戻す」「やり直し」は既存のプレースホルダ（構成済みUI）へCommand結線するのみで、新規UI要素の追加は無い。UI/UX分岐は発生しない想定だが、念のため確認したい。

---

## 4. スコープ境界

- 触る想定: `src/Ecad2.App/Commands/UndoManager.cs`（新規）、`src/Ecad2.App/ViewModels/MainWindowViewModel.cs`（`UndoCommand`/`RedoCommand`/`ApplyUndoRedoSnapshot`新設）、`src/Ecad2.App/ViewModels/SheetNavigationViewModel.cs`（`AddCommand`/`DeleteCommand`へ`RecordSnapshot`呼び出し追加）、`src/Ecad2.App/MainWindow.xaml`（Command結線）、`src/Ecad2.App/MainWindow.xaml.cs`（Ctrl+Z/Y）、`tests/Ecad2.App.Tests/`
- 触らない想定: `src/Ecad2.Core/Persistence/GcadSerializer.cs`（流用のみ、変更不要）、シート追加/削除以外の操作（要素配置・配線・行操作・シート改名等、Undo対応させない）、`RenameCommand`
- 単一増分として独立コミット・忍者実機確認とする（T-055の増分運用を踏襲、候補2以降への拡張は本増分の範囲外で別途起票）

---

## 5. 次の1手

殿裁可を仰ぎ、上記「開かれた論点」4点への回答を得てから実装着手する。
