namespace MagmaEdit.Core.Editing;

/// <summary>Moves a timeline clip between tracks at a requested timeline position as one undoable operation.</summary>
public sealed class MoveTimelineClipToTrackCommand : IEditCommand
{
    private readonly TimelineEditor _editor;
    private readonly string _sourceTrackId;
    private readonly string _destinationTrackId;
    private readonly string _clipId;
    private readonly EditTime _newTimelineStart;
    private readonly TimelineClip _originalClip;
    private readonly int _sourceIndex;

    public MoveTimelineClipToTrackCommand(
        TimelineEditor editor,
        string sourceTrackId,
        string destinationTrackId,
        string clipId,
        EditTime newTimelineStart)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTrackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationTrackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clipId);

        if (string.Equals(sourceTrackId, destinationTrackId, StringComparison.Ordinal))
            throw new ArgumentException("Source and destination tracks must be different.", nameof(destinationTrackId));
        if (newTimelineStart.Ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(newTimelineStart), "Timeline position cannot be negative.");

        TimelineTrack sourceTrack = editor.Timeline.GetTrack(sourceTrackId);
        _ = editor.Timeline.GetTrack(destinationTrackId);
        int sourceIndex = sourceTrack.Clips.FindIndex(clip =>
            string.Equals(clip.Id, clipId, StringComparison.Ordinal));
        if (sourceIndex < 0)
            throw new KeyNotFoundException($"Timeline clip '{clipId}' does not exist.");

        _sourceTrackId = sourceTrackId;
        _destinationTrackId = destinationTrackId;
        _clipId = clipId;
        _newTimelineStart = newTimelineStart;
        _originalClip = sourceTrack.Clips[sourceIndex].Clone();
        _sourceIndex = sourceIndex;
    }

    public string Label => "Move clip to track";

    public void Apply()
    {
        TimelineClip movedClip = _originalClip.Clone();
        movedClip.TimelineStart = _newTimelineStart;

        TimelineTrack sourceTrack = _editor.Timeline.GetTrack(_sourceTrackId);
        TimelineTrack destinationTrack = _editor.Timeline.GetTrack(_destinationTrackId);

        try
        {
            _editor.RemoveClip(_sourceTrackId, _clipId);
            _editor.InsertExistingClip(_destinationTrackId, movedClip);
        }
        catch
        {
            if (destinationTrack.Clips.Any(clip => string.Equals(clip.Id, _clipId, StringComparison.Ordinal)))
                _editor.RemoveClip(_destinationTrackId, _clipId);

            if (!sourceTrack.Clips.Any(clip => string.Equals(clip.Id, _clipId, StringComparison.Ordinal)))
                _editor.InsertExistingClip(_sourceTrackId, _originalClip.Clone(), _sourceIndex);

            throw;
        }
    }

    public void Revert()
    {
        _editor.RemoveClip(_destinationTrackId, _clipId);
        _editor.InsertExistingClip(_sourceTrackId, _originalClip.Clone(), _sourceIndex);
    }
}
