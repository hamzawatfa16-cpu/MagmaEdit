using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.Integration;

/// <summary>Owns automation state for the already-open desktop project without creating a second project copy.</summary>
public sealed class LiveEditorAutomationSession
{
    private readonly EditorCommandGateway _gateway;
    private readonly AuthorizedEditorCommandRouter _router;
    private readonly Action _saveProject;

    public LiveEditorAutomationSession(
        ProjectDocument project,
        AutomationClientContext client,
        Action saveProject)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(saveProject);

        Project = project;
        Client = client;
        _saveProject = saveProject;
        _gateway = new EditorCommandGateway(project);
        _router = new AuthorizedEditorCommandRouter(new EditorCommandRouter(_gateway));
    }

    public ProjectDocument Project { get; }

    public AutomationClientContext Client { get; }

    public EditorCommandResult Execute(EditorCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        EditorCommandResult result = _router.Execute(request, Client);
        if (result.Succeeded)
        {
            _saveProject();
        }

        return result;
    }

    public EditorProjectState GetState()
    {
        EditorMediaState[] media = Project.Media
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

        EditorTrackState[] tracks = Project.Timeline.Tracks
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
            media.Length,
            media,
            tracks,
            _gateway.History.UndoCount,
            _gateway.History.RedoCount);
    }
}
