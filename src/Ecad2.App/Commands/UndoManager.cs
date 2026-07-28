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

    /// <summary>Undoスタックの深さ(T-134)。「積んだ操作そのものが取り消された」ことを検知するために、
    /// 積んだ直後の深さを呼び出し元が控えておき、<see cref="DiscardLastSnapshot"/>へ渡す用途で使う。</summary>
    public int UndoDepth => _undoStack.Count;

    /// <summary>
    /// 直前に積んだスナップショットを破棄する(T-134、殿裁定2026-07-28=(U-1))。
    /// <para>
    /// 用途は「積んだ後に、その操作自体が取り消された」場合に限る。具体的にはOR配置の合流先確認を
    /// Escで取り消した経路(CancelOrJoinTarget)——要素配置ごと取り消されるため、配置時に積んだ
    /// スナップショットを残すと「Undoを押しても何も変わらぬ一回」が生じる。
    /// </para>
    /// <para>
    /// <paramref name="expectedDepth"/>は積んだ直後のUndoDepth。<b>現在の深さが一致する場合のみ破棄する</b>
    /// ——間に別の操作が積んでいれば一致せず、その操作のスナップショットを誤って捨てることを防ぐ
    /// (安全側に倒す設計)。戻り値は実際に破棄したか。
    /// </para>
    /// <para>
    /// Redoスタックには触れない。RecordSnapshotが積む際に既にredoStackをClearしており、
    /// 破棄してもRedo履歴は戻らないため(T-134計画書§3-4の留保、侍が一次ソースで確認)。
    /// </para>
    /// </summary>
    public bool DiscardLastSnapshot(int expectedDepth)
    {
        if (_undoStack.Count != expectedDepth || _undoStack.Count == 0) return false;
        _undoStack.Pop();
        return true;
    }

    /// <summary>Undo/Redo履歴を全て破棄する(T-051バグ修正#1)。文書差し替え(新規/開く)の入口で呼び、
    /// 無関係な旧文書のスナップショットが残存したままUndoされ別ファイルへ誤って上書き保存される
    /// データ破損事故を防ぐ。既に空でも例外は投げない(Stack.Clear()の既定挙動)。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
