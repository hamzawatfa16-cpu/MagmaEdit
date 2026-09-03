using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
using MagmaEdit.Integration;
using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.App;

/// <summary>Bridges the plugin string-parameter contract to the shared MagmaEdit integration command boundary.</summary>
internal sealed class PluginEditorCommandAdapter : IPluginEditorCommands
{
    private readonly Func<ProjectDocument> _projectAccessor;
    private readonly Action _saveProject;
    private readonly Dictionary<string, EditorCommandGateway> _gateways = new(StringComparer.Ordinal);

    public PluginEditorCommandAdapter(Func<ProjectDocument> projectAccessor, Action saveProject)
    {
        _projectAccessor = projectAccessor ?? throw new ArgumentNullException(nameof(projectAccessor));
        _saveProject = saveProject ?? throw new ArgumentNullException(nameof(saveProject));
    }

    public ValueTask<PluginCommandResult> ExecuteAsync(
        string command,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Enum.TryParse(command.Trim(), ignoreCase: true, out EditorCommandKind commandKind))
        {
            return ValueTask.FromResult(PluginCommandResult.Failure($"Unsupported editor command '{command}'."));
        }

        try
        {
            ProjectDocument project = _projectAccessor();
            EditorCommandGateway gateway = GetGateway(project);
            EditorCommandRequest request = BuildRequest(commandKind, parameters);
            EditorCommandResult result =
                new AuthorizedEditorCommandRouter(
                    new EditorCommandRouter(gateway))
                .Execute(
                    request,
                    new AutomationClientContext(
                        "desktop-plugin",
                        AutomationClientKind.Plugin,
                        new HashSet<EditorCommandCapability>
                        {
                            EditorCommandCapability.TimelineEditing,
                            EditorCommandCapability.MediaManagement,
                            EditorCommandCapability.History
                        }));

            if (result.Succeeded)
            {
                _saveProject();
            }

            return ValueTask.FromResult(result.Succeeded
                ? PluginCommandResult.Success(result.Message)
                : PluginCommandResult.Failure(result.Message));
        }
        catch (ArgumentException exception)
        {
            return ValueTask.FromResult(PluginCommandResult.Failure(exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return ValueTask.FromResult(PluginCommandResult.Failure(exception.Message));
        }
        catch (KeyNotFoundException exception)
        {
            return ValueTask.FromResult(PluginCommandResult.Failure(exception.Message));
        }
        catch (OverflowException exception)
        {
            return ValueTask.FromResult(PluginCommandResult.Failure(exception.Message));
        }
    }

    private EditorCommandGateway GetGateway(ProjectDocument project)
    {
        if (!_gateways.TryGetValue(project.Id, out EditorCommandGateway? gateway))
        {
            gateway = new EditorCommandGateway(project);
            _gateways.Add(project.Id, gateway);
        }

        return gateway;
    }

    private static EditorCommandRequest BuildRequest(
        EditorCommandKind command,
        IReadOnlyDictionary<string, string?> parameters)
    {
        string? Get(string name) => parameters.TryGetValue(name, out string? value) ? value : null;

        return command switch
        {
            EditorCommandKind.AddTrack => new(command, Name: Required(Get(nameof(EditorCommandRequest.Name)), nameof(EditorCommandRequest.Name))),
            EditorCommandKind.RemoveTrack => new(command, TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId))),
            EditorCommandKind.InsertClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                MediaId: Required(Get(nameof(EditorCommandRequest.MediaId)), nameof(EditorCommandRequest.MediaId)),
                TimelinePositionTicks: Required(Get(nameof(EditorCommandRequest.TimelinePositionTicks)), nameof(EditorCommandRequest.TimelinePositionTicks)),
                SourceInTicks: Required(Get(nameof(EditorCommandRequest.SourceInTicks)), nameof(EditorCommandRequest.SourceInTicks)),
                SourceOutTicks: Required(Get(nameof(EditorCommandRequest.SourceOutTicks)), nameof(EditorCommandRequest.SourceOutTicks))),
            EditorCommandKind.DuplicateClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(EditorCommandRequest.ClipId)), nameof(EditorCommandRequest.ClipId))),
            EditorCommandKind.RemoveClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(EditorCommandRequest.ClipId)), nameof(EditorCommandRequest.ClipId))),
            EditorCommandKind.TrimClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(EditorCommandRequest.ClipId)), nameof(EditorCommandRequest.ClipId)),
                SourceInTicks: Required(Get(nameof(EditorCommandRequest.SourceInTicks)), nameof(EditorCommandRequest.SourceInTicks)),
                SourceOutTicks: Required(Get(nameof(EditorCommandRequest.SourceOutTicks)), nameof(EditorCommandRequest.SourceOutTicks))),
            EditorCommandKind.MoveClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(EditorCommandRequest.ClipId)), nameof(EditorCommandRequest.ClipId)),
                TimelinePositionTicks: Required(Get(nameof(EditorCommandRequest.TimelinePositionTicks)), nameof(EditorCommandRequest.TimelinePositionTicks))),
            EditorCommandKind.SplitClip => new(
                command,
                TrackId: Required(Get(nameof(EditorCommandRequest.TrackId)), nameof(EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(EditorCommandRequest.ClipId)), nameof(EditorCommandRequest.ClipId)),
                TimelinePositionTicks: Required(Get(nameof(EditorCommandRequest.TimelinePositionTicks)), nameof(EditorCommandRequest.TimelinePositionTicks))),
            EditorCommandKind.RenameMedia => new(
                command,
                MediaId: Required(Get(nameof(EditorCommandRequest.MediaId)), nameof(EditorCommandRequest.MediaId)),
                Name: Required(Get(nameof(EditorCommandRequest.Name)), nameof(EditorCommandRequest.Name))),
            EditorCommandKind.SetMediaPublished => new(
                command,
                MediaId: Required(Get(nameof(EditorCommandRequest.MediaId)), nameof(EditorCommandRequest.MediaId)),
                IsPublished: ParseBoolean(Get(nameof(EditorCommandRequest.IsPublished)))),
            EditorCommandKind.Undo => new(command),
            EditorCommandKind.Redo => new(command),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported editor command.")
        };
    }

    private static string Required(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Trim();
    }

    private static bool ParseBoolean(string? value)
    {
        string required = Required(value, nameof(EditorCommandRequest.IsPublished));
        if (!bool.TryParse(required, out bool parsed))
        {
            throw new ArgumentException("IsPublished must be true or false.", nameof(value));
        }

        return parsed;
    }
}
