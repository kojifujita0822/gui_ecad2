using Ecad2.Model;
using Ecad2.Persistence;

namespace Ecad2.App.Commands;

/// <summary>
/// Undo/Redo基盤(T-051、殿裁定=案C)。既存GcadSerializer.Serialize/Deserializeをそのまま流用し、
/// ドキュメント全体のJSONスナップショットをUndo/Redo二本のスタックで保持する(GuiEcad CommandHistoryの
/// Execute時Redoクリアを踏襲)。MVP対象範囲は候補1(SheetNavigationViewModelのシート追加/削除のみ、
/// 設計出典: docs/ecad2-t051-implementation-plan-samurai.md)。
/// </summary>
public sealed class UndoManager
{
    private readonly Stack<string> _undoStack = new();
    private readonly Stack<string> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>操作実行の直前に呼ぶ。現在のDocument状態をUndoスタックへ積み、Redo履歴をクリアする
    /// (新規操作で分岐先のRedo履歴は無効になるため)。</summary>
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
