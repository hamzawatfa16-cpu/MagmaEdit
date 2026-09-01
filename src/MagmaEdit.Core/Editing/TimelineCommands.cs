namespace MagmaEdit.Core.Editing;

public sealed class AddTimelineTrackCommand : IEditCommand
{
    private readonly TimelineDocument _timeline;
    private readonly TimelineTrack _track;

    public AddTimelineTrackCommand(TimelineDocument timeline, string trackName)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _track = TimelineTrack.Create(trackName);
    }

    public string Label => "Add track";

    public TimelineTrack Track => _track;

    public void Apply()
    {
        if (_timeline.Tracks.All(track => !string.Equals(track.Id, _track.Id, StringComparison.Ordinal)))
            _timeline.Tracks.Add(_track);
    }

    public void Revert()
    {
        _timeline.Tracks.RemoveAll(track => string.Equals(track.Id, _track.Id, StringComparison.Ordinal));
    }
}

public sealed class InsertTimelineClipCommand : IEditCommand
{
    private readonly TimelineEditor _editor;
    private readonly string _trackId;
    private readonly TimelineClip _clip;

    public InsertTimelineClipCommand(
        TimelineEditor editor,
        string trackId,
        string mediaId,
        EditTime timelineStart,
        EditTime sourceIn,
        EditTime sourceOut)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        _trackId = trackId;
        _clip = TimelineClip.Create(mediaId, timelineStart, sourceIn, sourceOut);
    }

    public string Label => "Insert clip";

    public TimelineClip Clip => _clip;

    public void Apply() => _editor.InsertExistingClip(_trackId, _clip.Clone());

    public void Revert() => _editor.RemoveClip(_trackId, _clip.Id);
}
