using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Integration;

/// <summary>Owns one project-backed automation session and persists successful editor commands.</summary>
public sealed class EditorAutomationSession
{
    private readonly ProjectStore _store;
    private readonly string _projectPath;
    private readonly AuthorizedEditorCommandRouter _router;

    public EditorAutomationSession(
        ProjectDocument project,
        string projectPath,
        AutomationClientContext client)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(client);

        _projectPath = Path.GetFullPath(projectPath);
        string? projectDirectory = Path.GetDirectoryName(_projectPath);
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new ArgumentException("The project path must include a directory.", nameof(projectPath));
        }

        _store = new ProjectStore(WorkspaceLayout.Create(projectDirectory));
        var gateway = new EditorCommandGateway(project);
        _router = new AuthorizedEditorCommandRouter(new EditorCommandRouter(gateway));
        Project = project;
        Client = client;
    }

    public ProjectDocument Project { get; }

    public AutomationClientContext Client { get; }

    public string ProjectPath => _projectPath;

    public static EditorAutomationSession Load(
        string projectPath,
        AutomationClientContext client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentNullException.ThrowIfNull(client);

        string fullPath = Path.GetFullPath(projectPath);
        ProjectDocument project = ProjectStore.Load(fullPath);
        return new EditorAutomationSession(project, fullPath, client);
    }

    public EditorCommandResult Execute(EditorCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EditorCommandResult result = _router.Execute(request, Client);
        if (result.Succeeded)
        {
            _store.Save(Project, _projectPath);
        }

        return result;
    }
}
