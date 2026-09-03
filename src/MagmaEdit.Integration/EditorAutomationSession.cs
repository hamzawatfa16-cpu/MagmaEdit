using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Integration;

/// <summary>Owns one project-backed automation session and persists successful editor commands.</summary>
public sealed class EditorAutomationSession
{
    private readonly ProjectStore _store;
    private readonly string _projectPath;
    private readonly EditorCommandGateway _gateway;
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
        _gateway = new EditorCommandGateway(project);
        _router = new AuthorizedEditorCommandRouter(new EditorCommandRouter(_gateway));
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

    public EditorProjectState GetState()
    {
        IReadOnlyList<EditorMediaState> media = Project.Media
            .OrderBy(asset => asset.Id, StringComparer.Ordinal)
            .Select(asset => new EditorMediaState(
                asset.Id,
                asset.FileName,
                asset.SourcePath,
                asset.LibraryPath,
                asset.IsPublished,
                asset.Metadata?.Duration.TotalSeconds,
                asset.Metadata?.Width,
                asset.Metadata?.Height,
                asset.Metadata?.FramesPerSecond))
            .ToArray();

        IReadOnlyList<EditorTrackState> tracks = Project.Timeline.Tracks
            .OrderBy(track => track.Id, StringComparer.Ordinal)
            .Select(track => new EditorTrackState(
                track.Id,
                track.Name,
                track.Clips
                    .OrderBy(clip => clip.TimelineStart)
                    .ThenBy(clip => clip.Id, StringComparer.Ordinal)
                    .Select(clip => new EditorClipState(
                        clip.Id,
                        clip.MediaId,
                        clip.TimelineStart.Ticks,
                        clip.SourceIn.Ticks,
                        clip.SourceOut.Ticks,
                        clip.Duration.Ticks))
                    .ToArray()))
            .ToArray();

        return new EditorProjectState(
            Project.Id,
            Project.Name,
            Project.SchemaVersion,
            Project.Timeline.Width,
            Project.Timeline.Height,
            Project.Timeline.FrameRateNumerator,
            Project.Timeline.FrameRateDenominator,
            media.Count,
            media,
            tracks,
            _gateway.History.UndoCount,
            _gateway.History.RedoCount);
    }
}
