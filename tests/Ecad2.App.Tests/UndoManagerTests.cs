using Ecad2.App.Commands;
using Ecad2.Model;

namespace Ecad2.App.Tests;

/// <summary>
/// T-051: Undo/Redo基盤(UndoManager)の単体テスト。案C(GcadSerializer流用のドキュメント全体
/// JSONスナップショット)の往復・履歴クリア条件を検証する。設計出典:
/// docs/ecad2-t051-implementation-plan-samurai.md。
/// </summary>
public class UndoManagerTests
{
    private static LadderDocument MakeDoc(int sheetCount)
    {
        var doc = new LadderDocument();
        for (int i = 0; i < sheetCount; i++)
            doc.Sheets.Add(new Sheet { PageNumber = i + 1, Name = $"シート{i + 1}", Grid = new GridSpec { Rows = 10, Columns = 20 } });
        return doc;
    }

    [Fact]
    public void CanUndo_Initially_ReturnsFalse()
    {
        var mgr = new UndoManager();

        Assert.False(mgr.CanUndo);
    }

    [Fact]
    public void CanRedo_Initially_ReturnsFalse()
    {
        var mgr = new UndoManager();

        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void RecordSnapshot_MakesCanUndoTrue()
    {
        var mgr = new UndoManager();

        mgr.RecordSnapshot(MakeDoc(1));

        Assert.True(mgr.CanUndo);
    }

    [Fact]
    public void Undo_WithNoHistory_ReturnsNull()
    {
        var mgr = new UndoManager();

        Assert.Null(mgr.Undo(MakeDoc(1)));
    }

    [Fact]
    public void Redo_WithNoHistory_ReturnsNull()
    {
        var mgr = new UndoManager();

        Assert.Null(mgr.Redo(MakeDoc(1)));
    }

    [Fact]
    public void Undo_RestoresPreviouslyRecordedState()
    {
        var mgr = new UndoManager();
        mgr.RecordSnapshot(MakeDoc(1)); // 追加前(1枚)の状態を記録
        var afterAdd = MakeDoc(2); // 追加後(2枚)の状態、という想定

        var restored = mgr.Undo(afterAdd);

        Assert.NotNull(restored);
        Assert.Single(restored!.Sheets);
    }

    [Fact]
    public void Undo_PushesCurrentStateToRedoStack()
    {
        var mgr = new UndoManager();
        mgr.RecordSnapshot(MakeDoc(1));

        mgr.Undo(MakeDoc(2));

        Assert.True(mgr.CanRedo);
    }

    [Fact]
    public void UndoThenRedo_RoundTrips()
    {
        var mgr = new UndoManager();
        mgr.RecordSnapshot(MakeDoc(1));
        var afterAdd = MakeDoc(2);

        var undone = mgr.Undo(afterAdd)!;
        var redone = mgr.Redo(undone);

        Assert.NotNull(redone);
        Assert.Equal(2, redone!.Sheets.Count);
    }

    /// <summary>新規操作(RecordSnapshot)が入ると、それより先のRedo履歴は分岐が無効になるためクリアされる
    /// (GuiEcad CommandHistoryのExecute時クリアを踏襲)。</summary>
    [Fact]
    public void RecordSnapshot_ClearsRedoHistory()
    {
        var mgr = new UndoManager();
        mgr.RecordSnapshot(MakeDoc(1));
        mgr.Undo(MakeDoc(2));
        Assert.True(mgr.CanRedo); // 前提: Undo直後はRedo可能

        mgr.RecordSnapshot(MakeDoc(3)); // 新規操作

        Assert.False(mgr.CanRedo);
    }
}
