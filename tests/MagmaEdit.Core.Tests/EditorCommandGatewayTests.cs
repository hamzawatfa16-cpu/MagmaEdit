using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.Core.Tests;

public sealed class EditorCommandGatewayTests
{
    [Fact]
    public void TimelineOperationsShareOneUndoRedoHistory()
    {
        ProjectDocument project = ProjectDocument.Create("Gateway Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack track = gateway.AddTrack("Video 1");
        TimelineClip clip = gateway.InsertClip(
            track.Id,
            "media-1",
            EditTime.Zero,
            EditTime.Zero,
            EditTime.FromSeconds(4));

        gateway.MoveClip(track.Id, clip.Id, EditTime.FromSeconds(8));
        Assert.Equal(EditTime.FromSeconds(8), clip.TimelineStart);
        Assert.Equal(3, gateway.History.UndoCount);

        Assert.True(gateway.Undo());
        Assert.Equal(EditTime.Zero, clip.TimelineStart);
        Assert.True(gateway.Undo());
        Assert.Empty(track.Clips);
        Assert.True(gateway.Redo());
        Assert.Single(track.Clips);
        Assert.Equal(2, gateway.History.UndoCount);
    }

    [Fact]
    public void MultipleGatewaysOverOneProjectShareRuntimeHistory()
    {
        ProjectDocument project = ProjectDocument.Create("Shared History Test");
        EditorCommandGateway first = new(project);
        EditorCommandGateway second = new(project);

        first.AddTrack("Video 1");
        Assert.Equal(1, second.History.UndoCount);
        Assert.Same(first.History, second.History);

        Assert.True(second.Undo());
        Assert.Empty(project.Timeline.Tracks);
    }

    [Fact]
    public void GatewayTrimAndSplitUseTheSameCommandLayer()
    {
        ProjectDocument project = ProjectDocument.Create("Gateway Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack track = gateway.AddTrack("Video 1");
        TimelineClip clip = gateway.InsertClip(
            track.Id,
            "media-1",
            EditTime.Zero,
            EditTime.Zero,
            EditTime.FromSeconds(10));

        gateway.TrimClip(track.Id, clip.Id, EditTime.FromSeconds(2), EditTime.FromSeconds(8));
        Assert.Equal(EditTime.FromSeconds(6), clip.Duration);

        gateway.SplitClip(track.Id, clip.Id, EditTime.FromSeconds(5));
        Assert.Equal(2, track.Clips.Count);
        Assert.True(gateway.Undo());
        Assert.Single(track.Clips);
        Assert.Equal(EditTime.FromSeconds(6), track.Clips[0].Duration);
    }

    [Fact]
    public void GatewayCanDuplicateClipAtEndAndUndoIt()
    {
        ProjectDocument project = ProjectDocument.Create("Duplicate Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack track = gateway.AddTrack("Video 1");
        TimelineClip original = gateway.InsertClip(
            track.Id,
            "media-1",
            EditTime.Zero,
            EditTime.FromSeconds(1),
            EditTime.FromSeconds(4));

        TimelineClip duplicate = gateway.DuplicateClip(track.Id, original.Id);

        Assert.Equal(2, track.Clips.Count);
        Assert.NotEqual(original.Id, duplicate.Id);
        Assert.Equal(original.MediaId, duplicate.MediaId);
        Assert.Equal(original.SourceIn, duplicate.SourceIn);
        Assert.Equal(original.SourceOut, duplicate.SourceOut);
        Assert.Equal(EditTime.FromSeconds(3), original.Duration);
        Assert.Equal(original.TimelineEnd, duplicate.TimelineStart);

        Assert.True(gateway.Undo());
        Assert.Single(track.Clips);
        Assert.Equal(original.Id, track.Clips[0].Id);
        Assert.True(gateway.Redo());
        Assert.Equal(2, track.Clips.Count);
    }

    [Fact]
    public void GatewayCanMoveClipToAnotherTrackAndUndoIt()
    {
        ProjectDocument project = ProjectDocument.Create("Cross Track Move Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack sourceTrack = gateway.AddTrack("Video 1");
        TimelineTrack destinationTrack = gateway.AddTrack("Video 2");
        TimelineClip original = gateway.InsertClip(
            sourceTrack.Id,
            "media-1",
            EditTime.Zero,
            EditTime.FromSeconds(1),
            EditTime.FromSeconds(4));

        gateway.MoveClipToTrack(
            sourceTrack.Id,
            destinationTrack.Id,
            original.Id,
            EditTime.FromSeconds(6));

        Assert.Empty(sourceTrack.Clips);
        TimelineClip moved = Assert.Single(destinationTrack.Clips);
        Assert.Equal(original.Id, moved.Id);
        Assert.Equal(EditTime.FromSeconds(6), moved.TimelineStart);
        Assert.Equal(EditTime.FromSeconds(3), moved.Duration);

        Assert.True(gateway.Undo());
        Assert.Single(sourceTrack.Clips);
        Assert.Empty(destinationTrack.Clips);
        Assert.Equal(EditTime.Zero, sourceTrack.Clips[0].TimelineStart);

        Assert.True(gateway.Redo());
        Assert.Empty(sourceTrack.Clips);
        Assert.Single(destinationTrack.Clips);
        Assert.Equal(EditTime.FromSeconds(6), destinationTrack.Clips[0].TimelineStart);
    }

    [Fact]
    public void MoveClipToAnotherTrackDoesNotPartiallyMutateWhenDestinationOverlaps()
    {
        ProjectDocument project = ProjectDocument.Create("Cross Track Validation Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack sourceTrack = gateway.AddTrack("Video 1");
        TimelineTrack destinationTrack = gateway.AddTrack("Video 2");
        TimelineClip original = gateway.InsertClip(
            sourceTrack.Id,
            "media-1",
            EditTime.Zero,
            EditTime.Zero,
            EditTime.FromSeconds(4));
        _ = gateway.InsertClip(
            destinationTrack.Id,
            "media-2",
            EditTime.FromSeconds(5),
            EditTime.Zero,
            EditTime.FromSeconds(4));

        Assert.Throws<InvalidOperationException>(() => gateway.MoveClipToTrack(
            sourceTrack.Id,
            destinationTrack.Id,
            original.Id,
            EditTime.FromSeconds(6)));

        TimelineClip remainingSourceClip = Assert.Single(sourceTrack.Clips);
        Assert.Equal(original.Id, remainingSourceClip.Id);
        Assert.Equal(EditTime.Zero, remainingSourceClip.TimelineStart);
        Assert.Single(destinationTrack.Clips);
        Assert.Equal(1, gateway.History.UndoCount);
    }

    [Fact]
    public void GatewayMediaOperationsAreUndoable()
    {
        ProjectDocument project = ProjectDocument.Create("Gateway Test");
        MediaAsset asset = new("media-1", "original.mp4", "C:\\source.mp4", "C:\\library\\original.mp4");
        project.Media.Add(asset);
        EditorCommandGateway gateway = new(project);

        gateway.RenameMedia(asset.Id, "edited.mp4");
        gateway.SetMediaPublished(asset.Id, true);
        Assert.Equal("edited.mp4", project.Media[0].FileName);
        Assert.True(project.Media[0].IsPublished);

        Assert.True(gateway.Undo());
        Assert.False(project.Media[0].IsPublished);
        Assert.True(gateway.Undo());
        Assert.Equal("original.mp4", project.Media[0].FileName);
        Assert.True(gateway.Redo());
        Assert.Equal("edited.mp4", project.Media[0].FileName);
    }

    [Fact]
    public void GatewayMediaCollectionOperationsAreUndoable()
    {
        ProjectDocument project = ProjectDocument.Create("Gateway Test");
        MediaAsset asset = new("media-1", "clip.mp4", "C:\\source.mp4", "C:\\library\\clip.mp4");
        EditorCommandGateway gateway = new(project);

        Assert.Same(asset, gateway.AddMedia(asset));
        Assert.Single(project.Media);
        Assert.True(gateway.Undo());
        Assert.Empty(project.Media);
        Assert.True(gateway.Redo());
        Assert.Single(project.Media);

        gateway.RemoveMedia(asset.Id);
        Assert.Empty(project.Media);
        Assert.True(gateway.Undo());
        Assert.Single(project.Media);
        Assert.Equal(asset.Id, project.Media[0].Id);
    }
}
