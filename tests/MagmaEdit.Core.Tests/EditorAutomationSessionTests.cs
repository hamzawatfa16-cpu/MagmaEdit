using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;
using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class EditorAutomationSessionTests
{
    [Fact]
    public void ExecuteAuthorizedCommandPersistsProjectChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Automation.magmaedit.json");

        try
        {
            var layout = WorkspaceLayout.Create(root);
            var store = new ProjectStore(layout);
            ProjectDocument project = ProjectDocument.Create("Automation");
            store.Save(project, projectPath);

            var client = new AutomationClientContext(
                "test-mcp",
                AutomationClientKind.Mcp,
                new HashSet<EditorCommandCapability>
                {
                    EditorCommandCapability.TimelineEditing,
                    EditorCommandCapability.MediaManagement,
                    EditorCommandCapability.History
                });
            EditorAutomationSession session = EditorAutomationSession.Load(projectPath, client);

            EditorCommandResult result = session.Execute(new EditorCommandRequest(
                EditorCommandKind.AddTrack,
                Name: "AI Track"));

            Assert.True(result.Succeeded);
            Assert.Contains(session.Project.Timeline.Tracks, track => track.Name == "AI Track");

            ProjectDocument reloaded = ProjectStore.Load(projectPath);
            Assert.Contains(reloaded.Timeline.Tracks, track => track.Name == "AI Track");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ExecuteDeniedCommandDoesNotPersistOrMutateProject()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Automation.magmaedit.json");

        try
        {
            var layout = WorkspaceLayout.Create(root);
            var store = new ProjectStore(layout);
            ProjectDocument project = ProjectDocument.Create("Automation");
            store.Save(project, projectPath);

            var client = new AutomationClientContext(
                "history-only",
                AutomationClientKind.Mcp,
                new HashSet<EditorCommandCapability> { EditorCommandCapability.History });
            EditorAutomationSession session = EditorAutomationSession.Load(projectPath, client);

            EditorCommandResult result = session.Execute(new EditorCommandRequest(
                EditorCommandKind.AddTrack,
                Name: "Must Not Exist"));

            Assert.False(result.Succeeded);
            Assert.DoesNotContain(session.Project.Timeline.Tracks, track => track.Name == "Must Not Exist");

            ProjectDocument reloaded = ProjectStore.Load(projectPath);
            Assert.DoesNotContain(reloaded.Timeline.Tracks, track => track.Name == "Must Not Exist");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetStateReturnsReadOnlyDeterministicProjectSnapshot()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string projectPath = Path.Combine(root, "Automation.magmaedit.json");

        try
        {
            var layout = WorkspaceLayout.Create(root);
            var store = new ProjectStore(layout);
            ProjectDocument project = ProjectDocument.Create("State Test");
            var track = project.Timeline.AddTrack("Video");
            store.Save(project, projectPath);

            var client = new AutomationClientContext(
                "state-test",
                AutomationClientKind.Mcp,
                new HashSet<EditorCommandCapability> { EditorCommandCapability.History });
            EditorAutomationSession session = EditorAutomationSession.Load(projectPath, client);

            EditorProjectState first = session.GetState();
            EditorProjectState second = session.GetState();

            Assert.Equal(first, second);
            Assert.Equal(project.Id, first.ProjectId);
            Assert.Equal("State Test", first.ProjectName);
            Assert.Equal(project.SchemaVersion, first.SchemaVersion);
            Assert.Equal(project.Timeline.Width, first.TimelineWidth);
            Assert.Equal(project.Timeline.Height, first.TimelineHeight);
            Assert.Single(first.Tracks);
            Assert.Equal(track.Id, first.Tracks[0].Id);
            Assert.Equal("Video", first.Tracks[0].Name);
            Assert.Empty(first.Tracks[0].Clips);
            Assert.Empty(first.Media);
            Assert.Equal(0, first.UndoCount);
            Assert.Equal(0, first.RedoCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
