using MagmaEdit.Core.Editing;

namespace MagmaEdit.Core.Tests;

public sealed class TimelineCommandTests
{
    [Fact]
    public void AddTrackCommandSupportsUndoAndRedo()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        EditHistory history = new();
        AddTimelineTrackCommand command = new(timeline, "Video 1");

        history.Execute(command);
        Assert.Single(timeline.Tracks);
        Assert.True(history.CanUndo);

        Assert.True(history.Undo());
        Assert.Empty(timeline.Tracks);
        Assert.True(history.CanRedo);

        Assert.True(history.Redo());
        Assert.Single(timeline.Tracks);
        Assert.Equal(command.Track.Id, timeline.Tracks[0].Id);
    }

    [Fact]
    public void InsertClipCommandSupportsUndoAndRedo()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        EditHistory history = new();
        InsertTimelineClipCommand command = new(
            editor,
            track.Id,
            "media-1",
            EditTime.Zero,
            EditTime.Zero,
            EditTime.FromSeconds(4));

        history.Execute(command);
        Assert.Single(track.Clips);
        Assert.Equal(command.Clip.Id, track.Clips[0].Id);

        Assert.True(history.Undo());
        Assert.Empty(track.Clips);

        Assert.True(history.Redo());
        Assert.Single(track.Clips);
        Assert.Equal(command.Clip.Id, track.Clips[0].Id);
    }

    [Fact]
    public void RemoveClipCommandSupportsUndoAndRedo()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip first = editor.InsertClip(track.Id, "media-1", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(2));
        editor.InsertClip(track.Id, "media-2", EditTime.FromSeconds(2), EditTime.Zero, EditTime.FromSeconds(2));
        EditHistory history = new();
        RemoveTimelineClipCommand command = new(editor, track.Id, first.Id);

        history.Execute(command);
        Assert.Single(track.Clips);
        Assert.DoesNotContain(track.Clips, clip => clip.Id == first.Id);

        Assert.True(history.Undo());
        Assert.Equal(2, track.Clips.Count);
        Assert.Equal(first.Id, track.Clips[0].Id);

        Assert.True(history.Redo());
        Assert.Single(track.Clips);
    }

    [Fact]
    public void TrimClipCommandSupportsUndoAndRedo()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip clip = editor.InsertClip(track.Id, "media", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(10));
        EditHistory history = new();
        TrimTimelineClipCommand command = new(
            editor,
            track.Id,
            clip.Id,
            EditTime.FromSeconds(2),
            EditTime.FromSeconds(8));

        history.Execute(command);
        Assert.Equal(EditTime.FromSeconds(6), clip.Duration);
        Assert.Equal(EditTime.FromSeconds(2), clip.SourceIn);

        Assert.True(history.Undo());
        Assert.Equal(EditTime.Zero, clip.SourceIn);
        Assert.Equal(EditTime.FromSeconds(10), clip.SourceOut);

        Assert.True(history.Redo());
        Assert.Equal(EditTime.FromSeconds(2), clip.SourceIn);
        Assert.Equal(EditTime.FromSeconds(8), clip.SourceOut);
    }

    [Fact]
    public void SplitClipCommandSupportsUndoAndRedo()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip clip = editor.InsertClip(track.Id, "media", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(10));
        EditHistory history = new();
        SplitTimelineClipCommand command = new(editor, track.Id, clip.Id, EditTime.FromSeconds(4));

        history.Execute(command);
        Assert.Equal(2, track.Clips.Count);
        Assert.Equal(EditTime.FromSeconds(4), track.Clips[0].TimelineEnd);
        Assert.Equal(EditTime.FromSeconds(4), track.Clips[1].TimelineStart);

        Assert.True(history.Undo());
        Assert.Single(track.Clips);
        Assert.Equal(clip.Id, track.Clips[0].Id);
        Assert.Equal(EditTime.FromSeconds(10), track.Clips[0].Duration);

        Assert.True(history.Redo());
        Assert.Equal(2, track.Clips.Count);
    }
}
