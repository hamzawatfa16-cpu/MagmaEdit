using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class EditorCommandRouterTests
{
    [Fact]
    public void InsertAndMoveCommandsUseTheSharedGatewayHistory()
    {
        ProjectDocument project = ProjectDocument.Create("Router Test");
        EditorCommandGateway gateway = new(project);
        TimelineTrack track = gateway.AddTrack("Video 1");
        EditorCommandRouter router = new(gateway);

        EditorCommandResult insert = router.Execute(new(
            EditorCommandKind.InsertClip,
            TrackId: track.Id,
            MediaId: "media-1",
            TimelinePositionTicks: "0",
            SourceInTicks: "0",
            SourceOutTicks: "960000"));

        Assert.True(insert.Succeeded);
        Assert.NotNull(insert.ClipId);
        Assert.Equal(2, gateway.History.UndoCount);

        EditorCommandResult move = router.Execute(new(
            EditorCommandKind.MoveClip,
            TrackId: track.Id,
            ClipId: insert.ClipId,
            TimelinePositionTicks: "480000"));

        Assert.True(move.Succeeded);
        Assert.Single(track.Clips);
        Assert.Equal(EditTime.FromSeconds(2), track.Clips[0].TimelineStart);
        Assert.Equal(3, gateway.History.UndoCount);
    }

    [Fact]
    public void InvalidRequestReturnsStructuredFailureWithoutChangingHistory()
    {
        ProjectDocument project = ProjectDocument.Create("Router Test");
        EditorCommandGateway gateway = new(project);
        EditorCommandRouter router = new(gateway);

        EditorCommandResult result = router.Execute(new(
            EditorCommandKind.InsertClip,
            TrackId: "missing",
            MediaId: "media-1",
            TimelinePositionTicks: "not-a-number",
            SourceInTicks: "0",
            SourceOutTicks: "1"));

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.Equal(0, result.UndoCount);
        Assert.Equal(0, result.RedoCount);
    }

    [Fact]
    public void UndoAndRedoAreExposedAsHistoryActions()
    {
        ProjectDocument project = ProjectDocument.Create("Router Test");
        EditorCommandGateway gateway = new(project);
        EditorCommandRouter router = new(gateway);

        EditorCommandResult addTrack = router.Execute(new(EditorCommandKind.AddTrack, Name: "Video 1"));
        Assert.True(addTrack.Succeeded);
        Assert.Single(project.Timeline.Tracks);

        EditorCommandResult undo = router.Execute(new(EditorCommandKind.Undo));
        Assert.True(undo.Succeeded);
        Assert.Empty(project.Timeline.Tracks);

        EditorCommandResult redo = router.Execute(new(EditorCommandKind.Redo));
        Assert.True(redo.Succeeded);
        Assert.Single(project.Timeline.Tracks);
    }
}
