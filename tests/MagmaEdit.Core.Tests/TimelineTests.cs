using MagmaEdit.Core.Editing;

namespace MagmaEdit.Core.Tests;

public sealed class TimelineTests
{
    [Fact]
    public void InsertSortsClipsByTimelineStart()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);

        TimelineClip later = editor.InsertClip(track.Id, "media-2", EditTime.FromSeconds(5), EditTime.Zero, EditTime.FromSeconds(2));
        TimelineClip earlier = editor.InsertClip(track.Id, "media-1", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(3));

        Assert.Equal(earlier.Id, track.Clips[0].Id);
        Assert.Equal(later.Id, track.Clips[1].Id);
    }

    [Fact]
    public void InsertRejectsOverlapOnSameTrack()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        editor.InsertClip(track.Id, "media", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(3));

        Assert.Throws<InvalidOperationException>(() =>
            editor.InsertClip(track.Id, "other", EditTime.FromSeconds(2), EditTime.Zero, EditTime.FromSeconds(2)));
    }

    [Fact]
    public void TrimPreservesTimelineStartAndChangesDuration()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip clip = editor.InsertClip(track.Id, "media", EditTime.FromSeconds(5), EditTime.Zero, EditTime.FromSeconds(10));

        editor.TrimClip(track.Id, clip.Id, EditTime.FromSeconds(2), EditTime.FromSeconds(8));

        Assert.Equal(EditTime.FromSeconds(5), clip.TimelineStart);
        Assert.Equal(EditTime.FromSeconds(6), clip.Duration);
    }

    [Fact]
    public void SplitCreatesContiguousSourceAndTimelineRanges()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip clip = editor.InsertClip(track.Id, "media", EditTime.FromSeconds(5), EditTime.FromSeconds(10), EditTime.FromSeconds(20));

        (TimelineClip left, TimelineClip right) = editor.SplitClip(track.Id, clip.Id, EditTime.FromSeconds(9));

        Assert.Equal(EditTime.FromSeconds(10), left.SourceIn);
        Assert.Equal(EditTime.FromSeconds(14), left.SourceOut);
        Assert.Equal(EditTime.FromSeconds(14), right.SourceIn);
        Assert.Equal(EditTime.FromSeconds(20), right.SourceOut);
        Assert.Equal(EditTime.FromSeconds(5), left.TimelineStart);
        Assert.Equal(EditTime.FromSeconds(9), right.TimelineStart);
        Assert.Equal(EditTime.FromSeconds(9), left.TimelineEnd);
    }

    [Fact]
    public void RemoveDeletesClip()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);
        TimelineClip clip = editor.InsertClip(track.Id, "media", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(1));

        editor.RemoveClip(track.Id, clip.Id);

        Assert.Empty(track.Clips);
    }

    [Fact]
    public void DifferentTracksMayOverlap()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack first = timeline.AddTrack("Video 1");
        TimelineTrack second = timeline.AddTrack("Video 2");
        TimelineEditor editor = new(timeline);

        editor.InsertClip(first.Id, "media-1", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(5));
        editor.InsertClip(second.Id, "media-2", EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(5));

        Assert.Single(first.Clips);
        Assert.Single(second.Clips);
    }

    [Fact]
    public void FromSecondsRejectsNegativeSeconds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EditTime.FromSeconds(-1));
    }

    [Fact]
    public void FromSecondsRejectsNonFiniteSeconds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EditTime.FromSeconds(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditTime.FromSeconds(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditTime.FromSeconds(double.NegativeInfinity));
    }

    [Fact]
    public void InsertRejectsTimelineEndOverflow()
    {
        TimelineDocument timeline = TimelineDocument.CreateDefault();
        TimelineTrack track = timeline.AddTrack("Video 1");
        TimelineEditor editor = new(timeline);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            editor.InsertClip(
                track.Id,
                "media",
                new EditTime(long.MaxValue - 1),
                EditTime.Zero,
                new EditTime(2)));
    }
}
