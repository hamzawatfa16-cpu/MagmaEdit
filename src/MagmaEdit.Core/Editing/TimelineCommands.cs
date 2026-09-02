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

public sealed class RemoveTimelineClipCommand : IEditCommand
{
    private readonly TimelineEditor _editor;
    private readonly string _trackId;
    private readonly TimelineClip _clip;
    private readonly int _index;

    public RemoveTimelineClipCommand(TimelineEditor editor, string trackId, string clipId)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clipId);
        _trackId = trackId;

        TimelineTrack track = editor.Timeline.GetTrack(trackId);
        int index = track.Clips.FindIndex(clip => string.Equals(clip.Id, clipId, StringComparison.Ordinal));
        if (index < 0)
            throw new KeyNotFoundException($"Timeline clip '{clipId}' does not exist.");

        _index = index;
        _clip = track.Clips[index].Clone();
    }

    public string Label => "Remove clip";

    public void Apply() => _editor.RemoveClip(_trackId, _clip.Id);

    public void Revert() => _editor.InsertExistingClip(_trackId, _clip.Clone(), _index);
}

public sealed class TrimTimelineClipCommand : IEditCommand
{
    private readonly TimelineEditor _editor;
    private readonly string _trackId;
    private readonly string _clipId;
    private readonly EditTime _oldSourceIn;
    private readonly EditTime _oldSourceOut;
    private readonly EditTime _newSourceIn;
    private readonly EditTime _newSourceOut;

    public TrimTimelineClipCommand(
        TimelineEditor editor,
        string trackId,
        string clipId,
        EditTime sourceIn,
        EditTime sourceOut)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clipId);

        TimelineClip clip = editor.Timeline.GetTrack(trackId).Clips.FirstOrDefault(existing =>
            string.Equals(existing.Id, clipId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Timeline clip '{clipId}' does not exist.");

        _trackId = trackId;
        _clipId = clipId;
        _oldSourceIn = clip.SourceIn;
        _oldSourceOut = clip.SourceOut;
        _newSourceIn = sourceIn;
        _newSourceOut = sourceOut;
    }

    public string Label => "Trim clip";

    public void Apply() => _editor.TrimClip(_trackId, _clipId, _newSourceIn, _newSourceOut);

    public void Revert() => _editor.TrimClip(_trackId, _clipId, _oldSourceIn, _oldSourceOut);
}

public sealed class SplitTimelineClipCommand : IEditCommand
{
    private readonly TimelineEditor _editor;
    private readonly string _trackId;
    private readonly string _clipId;
    private readonly EditTime _timelinePosition;
    private TimelineClip? _originalClip;
    private TimelineClip? _leftClip;
    private TimelineClip? _rightClip;
    private int _index;

    public SplitTimelineClipCommand(
        TimelineEditor editor,
        string trackId,
        string clipId,
        EditTime timelinePosition)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clipId);
        _trackId = trackId;
        _clipId = clipId;
        _timelinePosition = timelinePosition;
    }

    public string Label => "Split clip";

    public void Apply()
    {
        if (_originalClip is null)
        {
            TimelineTrack track = _editor.Timeline.GetTrack(_trackId);
            TimelineClip original = track.Clips.FirstOrDefault(clip =>
                string.Equals(clip.Id, _clipId, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"Timeline clip '{_clipId}' does not exist.");
            _index = track.Clips.IndexOf(original);
            _originalClip = original.Clone();
            (TimelineClip left, TimelineClip right) = _editor.SplitClip(_trackId, _clipId, _timelinePosition);
            _leftClip = left.Clone();
            _rightClip = right.Clone();
            return;
        }

        _editor.RemoveClip(_trackId, _originalClip.Id);
        _editor.InsertExistingClip(_trackId, _leftClip!.Clone(), _index);
        _editor.InsertExistingClip(_trackId, _rightClip!.Clone(), _index + 1);
    }

    public void Revert()
    {
        if (_originalClip is null || _leftClip is null || _rightClip is null)
            throw new InvalidOperationException("The split command has not been applied.");

        _editor.RemoveClip(_trackId, _leftClip.Id);
        _editor.RemoveClip(_trackId, _rightClip.Id);
        _editor.InsertExistingClip(_trackId, _originalClip.Clone(), _index);
    }
}
