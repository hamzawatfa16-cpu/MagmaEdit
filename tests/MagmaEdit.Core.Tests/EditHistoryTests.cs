namespace MagmaEdit.Core.Tests;

using MagmaEdit.Core.Editing;

public sealed class EditHistoryTests
{
    [Fact]
    public void ExecuteAppliesCommandAndClearsRedo()
    {
        TestCommand command = new("Add clip");
        EditHistory history = new();

        history.Execute(command);

        Assert.True(command.Applied);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal("Add clip", history.UndoLabel);
    }

    [Fact]
    public void UndoRevertsLatestCommandAndMovesItToRedo()
    {
        TestCommand command = new("Add clip");
        EditHistory history = new();
        history.Execute(command);

        bool result = history.Undo();

        Assert.True(result);
        Assert.False(command.Applied);
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.Equal("Add clip", history.RedoLabel);
    }

    [Fact]
    public void RedoReappliesLatestUndoneCommand()
    {
        TestCommand command = new("Add clip");
        EditHistory history = new();
        history.Execute(command);
        history.Undo();

        bool result = history.Redo();

        Assert.True(result);
        Assert.True(command.Applied);
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void NewCommandClearsRedoHistory()
    {
        TestCommand first = new("First");
        TestCommand second = new("Second");
        EditHistory history = new();
        history.Execute(first);
        history.Undo();
        history.Execute(second);

        Assert.False(history.CanRedo);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal("Second", history.UndoLabel);
    }

    [Fact]
    public void UndoAndRedoReturnFalseWhenHistoryIsEmpty()
    {
        EditHistory history = new();

        Assert.False(history.Undo());
        Assert.False(history.Redo());
    }

    [Fact]
    public void ExecuteRejectsNullCommand()
    {
        EditHistory history = new();

        Assert.Throws<ArgumentNullException>(() => history.Execute(null!));
    }

    [Fact]
    public void ClearRemovesAllHistory()
    {
        EditHistory history = new();
        history.Execute(new TestCommand("First"));
        history.Execute(new TestCommand("Second"));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Null(history.UndoLabel);
        Assert.Null(history.RedoLabel);
    }

    [Fact]
    public void FailedUndoLeavesCommandInUndoHistory()
    {
        TestCommand command = new("Undo failure") { ThrowOnRevert = true };
        EditHistory history = new();
        history.Execute(command);

        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.True(command.Applied);
        Assert.Equal(1, history.UndoCount);
        Assert.Equal(0, history.RedoCount);
        Assert.Equal("Undo failure", history.UndoLabel);
    }

    [Fact]
    public void FailedRedoLeavesCommandInRedoHistory()
    {
        TestCommand command = new("Redo failure");
        EditHistory history = new();
        history.Execute(command);
        history.Undo();
        command.ThrowOnApply = true;

        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.False(command.Applied);
        Assert.Equal(0, history.UndoCount);
        Assert.Equal(1, history.RedoCount);
        Assert.Equal("Redo failure", history.RedoLabel);
    }

    private sealed class TestCommand(string label) : IEditCommand
    {
        public string Label { get; } = label;
        public bool Applied { get; private set; }
        public bool ThrowOnApply { get; set; }
        public bool ThrowOnRevert { get; set; }

        public void Apply()
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("Apply failed.");
            }

            Applied = true;
        }

        public void Revert()
        {
            if (ThrowOnRevert)
            {
                throw new InvalidOperationException("Revert failed.");
            }

            Applied = false;
        }
    }
}
