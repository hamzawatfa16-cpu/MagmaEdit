namespace MagmaEdit.Core.Editing;

/// <summary>Represents one reversible mutation of the editing model.</summary>
public interface IEditCommand
{
    string Label { get; }
    void Apply();
    void Revert();
}
