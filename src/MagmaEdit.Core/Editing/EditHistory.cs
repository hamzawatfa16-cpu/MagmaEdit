namespace MagmaEdit.Core.Editing;

/// <summary>Single-source undo/redo history for all editor mutations.</summary>
public sealed class EditHistory
{
    private readonly List<IEditCommand> _undo = [];
    private readonly List<IEditCommand> _redo = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public string? UndoLabel => CanUndo ? _undo[^1].Label : null;
    public string? RedoLabel => CanRedo ? _redo[^1].Label : null;

    public void Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Apply();
        _undo.Add(command);
        _redo.Clear();
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        IEditCommand command = _undo[^1];
        command.Revert();
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(command);
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        IEditCommand command = _redo[^1];
        command.Apply();
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(command);
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
