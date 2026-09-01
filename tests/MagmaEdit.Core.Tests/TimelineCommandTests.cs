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
}
