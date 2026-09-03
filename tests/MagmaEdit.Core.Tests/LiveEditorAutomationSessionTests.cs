using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class LiveEditorAutomationSessionTests
{
    [Fact]
    public void ExecuteAuthorizedCommandMutatesLiveProjectAndInvokesSave()
    {
        ProjectDocument project = ProjectDocument.Create("Live Automation");
        int saveCount = 0;
        var client = new AutomationClientContext(
            "live-test",
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.TimelineEditing });
        var session = new LiveEditorAutomationSession(
            project,
            client,
            () => saveCount++);

        EditorCommandResult result = session.Execute(new EditorCommandRequest(
            EditorCommandKind.AddTrack,
            Name: "AI Track"));

        Assert.True(result.Succeeded);
        Assert.Equal(1, saveCount);
        Assert.Contains(project.Timeline.Tracks, track => track.Name == "AI Track");
    }

    [Fact]
    public void ExecuteDeniedCommandDoesNotSaveLiveProject()
    {
        ProjectDocument project = ProjectDocument.Create("Live Automation");
        int saveCount = 0;
        var client = new AutomationClientContext(
            "live-test",
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.History });
        var session = new LiveEditorAutomationSession(
            project,
            client,
            () => saveCount++);

        EditorCommandResult result = session.Execute(new EditorCommandRequest(
            EditorCommandKind.AddTrack,
            Name: "Must Not Exist"));

        Assert.False(result.Succeeded);
        Assert.Equal(0, saveCount);
        Assert.DoesNotContain(project.Timeline.Tracks, track => track.Name == "Must Not Exist");
    }

    [Fact]
    public void GetStateReadsTheSameLiveProject()
    {
        ProjectDocument project = ProjectDocument.Create("Live State");
        project.Timeline.AddTrack("Video");
        var client = new AutomationClientContext(
            "live-state-test",
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability>());
        var session = new LiveEditorAutomationSession(
            project,
            client,
            () => { });

        EditorProjectState state = session.GetState();

        Assert.Equal(project.Id, state.ProjectId);
        Assert.Equal("Live State", state.ProjectName);
        Assert.Single(state.Tracks);
        Assert.Equal("Video", state.Tracks[0].Name);
    }
}
