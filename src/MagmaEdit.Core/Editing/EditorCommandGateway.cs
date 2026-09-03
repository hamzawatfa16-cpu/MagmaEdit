using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.Core.Editing;

/// <summary>Routes editor mutations through the same undoable command history used by the desktop editor.</summary>
public sealed class EditorCommandGateway : IEditorCommandGateway
{
    private readonly TimelineDocument _timeline;
    private readonly TimelineEditor _timelineEditor;
    private readonly IList<MediaAsset> _media;

    public EditorCommandGateway(ProjectDocument project, EditHistory? history = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        _timeline = project.Timeline;
        _timelineEditor = new TimelineEditor(_timeline);
        _media = project.Media;
        History = history ?? new EditHistory();
    }

    public EditHistory History { get; }

    public TimelineTrack AddTrack(string name)
    {
        AddTimelineTrackCommand command = new(_timeline, name);
        History.Execute(command);
        return command.Track;
    }

    public void RemoveTrack(string trackId)
    {
        History.Execute(new RemoveTimelineTrackCommand(_timeline, trackId));
    }

    public TimelineClip InsertClip(
        string trackId,
        string mediaId,
        EditTime timelineStart,
        EditTime sourceIn,
        EditTime sourceOut)
    {
        InsertTimelineClipCommand command = new(
            _timelineEditor,
            trackId,
            mediaId,
            timelineStart,
            sourceIn,
            sourceOut);
        History.Execute(command);

        TimelineTrack track = _timeline.GetTrack(trackId);
        return track.Clips.First(clip => string.Equals(clip.Id, command.Clip.Id, StringComparison.Ordinal));
    }

    public void RemoveClip(string trackId, string clipId)
    {
        History.Execute(new RemoveTimelineClipCommand(_timelineEditor, trackId, clipId));
    }

    public void TrimClip(string trackId, string clipId, EditTime sourceIn, EditTime sourceOut)
    {
        History.Execute(new TrimTimelineClipCommand(
            _timelineEditor,
            trackId,
            clipId,
            sourceIn,
            sourceOut));
    }

    public void MoveClip(string trackId, string clipId, EditTime timelineStart)
    {
        History.Execute(new MoveTimelineClipCommand(_timelineEditor, trackId, clipId, timelineStart));
    }

    public void SplitClip(string trackId, string clipId, EditTime timelinePosition)
    {
        History.Execute(new SplitTimelineClipCommand(_timelineEditor, trackId, clipId, timelinePosition));
    }

    public MediaAsset AddMedia(MediaAsset asset)
    {
        AddMediaAssetCommand command = new(_media, asset);
        History.Execute(command);
        return asset;
    }

    public void RemoveMedia(string mediaId)
    {
        History.Execute(new RemoveMediaAssetCommand(_media, mediaId));
    }

    public void RenameMedia(string mediaId, string newFileName)
    {
        History.Execute(new RenameMediaAssetCommand(_media, mediaId, newFileName));
    }

    public void SetMediaPublished(string mediaId, bool isPublished)
    {
        History.Execute(new SetMediaPublishedCommand(_media, mediaId, isPublished));
    }

    public bool Undo() => History.Undo();

    public bool Redo() => History.Redo();
}
