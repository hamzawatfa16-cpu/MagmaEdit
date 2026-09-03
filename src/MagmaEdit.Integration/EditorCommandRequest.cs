using MagmaEdit.Core.Editing;

namespace MagmaEdit.Integration;

/// <summary>Vendor-neutral JSON-safe request for one editor mutation or history action.</summary>
public sealed record EditorCommandRequest(
    EditorCommandKind Command,
    string? TrackId = null,
    string? ClipId = null,
    string? MediaId = null,
    string? Name = null,
    string? SourceInTicks = null,
    string? SourceOutTicks = null,
    string? TimelinePositionTicks = null,
    bool? IsPublished = null);

public enum EditorCommandKind
{
    AddTrack,
    RemoveTrack,
    InsertClip,
    RemoveClip,
    TrimClip,
    MoveClip,
    SplitClip,
    RenameMedia,
    SetMediaPublished,
    Undo,
    Redo
}

/// <summary>Result returned to an automation client after a gateway action.</summary>
public sealed record EditorCommandResult(
    bool Succeeded,
    string Message,
    string? TrackId = null,
    string? ClipId = null,
    string? MediaId = null,
    int UndoCount = 0,
    int RedoCount = 0);
