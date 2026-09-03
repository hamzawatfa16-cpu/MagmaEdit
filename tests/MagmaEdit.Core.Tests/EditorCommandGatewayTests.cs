using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;

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
}
