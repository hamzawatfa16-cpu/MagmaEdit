using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Projects;
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
            MagmaEdit.Integration.EditorCommandRequest request = BuildRequest(commandKind, parameters);
            MagmaEdit.Integration.EditorCommandResult result =
                new MagmaEdit.Integration.AuthorizedEditorCommandRouter(
                    new MagmaEdit.Integration.EditorCommandRouter(gateway))
                .Execute(
                    request,
                    new MagmaEdit.Integration.AutomationClientContext(
                        "desktop-plugin",
                        MagmaEdit.Integration.AutomationClientKind.Plugin,
                        new HashSet<MagmaEdit.Integration.EditorCommandCapability>
                        {
                            MagmaEdit.Integration.EditorCommandCapability.TimelineEditing,
                            MagmaEdit.Integration.EditorCommandCapability.MediaManagement,
                            MagmaEdit.Integration.EditorCommandCapability.History
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

    private static MagmaEdit.Integration.EditorCommandRequest BuildRequest(
        EditorCommandKind command,
        IReadOnlyDictionary<string, string?> parameters)
    {
        string? Get(string name) => parameters.TryGetValue(name, out string? value) ? value : null;

        return command switch
        {
            EditorCommandKind.AddTrack => new(command, Name: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.Name)), nameof(MagmaEdit.Integration.EditorCommandRequest.Name))),
            EditorCommandKind.RemoveTrack => new(command, TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId))),
            EditorCommandKind.InsertClip => new(
                command,
                TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)),
                MediaId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)), nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)),
                TimelinePositionTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks)),
                SourceInTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.SourceInTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.SourceInTicks)),
                SourceOutTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.SourceOutTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.SourceOutTicks))),
            EditorCommandKind.RemoveClip => new(
                command,
                TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)), nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId))),
            EditorCommandKind.TrimClip => new(
                command,
                TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)), nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)),
                SourceInTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.SourceInTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.SourceInTicks)),
                SourceOutTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.SourceOutTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.SourceOutTicks))),
            EditorCommandKind.MoveClip => new(
                command,
                TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)), nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)),
                TimelinePositionTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks))),
            EditorCommandKind.SplitClip => new(
                command,
                TrackId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)), nameof(MagmaEdit.Integration.EditorCommandRequest.TrackId)),
                ClipId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)), nameof(MagmaEdit.Integration.EditorCommandRequest.ClipId)),
                TimelinePositionTicks: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks)), nameof(MagmaEdit.Integration.EditorCommandRequest.TimelinePositionTicks))),
            EditorCommandKind.RenameMedia => new(
                command,
                MediaId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)), nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)),
                Name: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.Name)), nameof(MagmaEdit.Integration.EditorCommandRequest.Name))),
            EditorCommandKind.SetMediaPublished => new(
                command,
                MediaId: Required(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)), nameof(MagmaEdit.Integration.EditorCommandRequest.MediaId)),
                IsPublished: ParseBoolean(Get(nameof(MagmaEdit.Integration.EditorCommandRequest.IsPublished)))),
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
        string required = Required(value, nameof(MagmaEdit.Integration.EditorCommandRequest.IsPublished));
        if (!bool.TryParse(required, out bool parsed))
        {
            throw new ArgumentException("IsPublished must be true or false.", nameof(value));
        }

        return parsed;
    }
}
