namespace MagmaEdit.Plugin.Abstractions;

public interface IMagmaEditPlugin
{
    PluginManifest Manifest { get; }

    ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);

    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}

public interface IPluginContext
{
    string PluginDataDirectory { get; }

    IPluginEditorCommands EditorCommands { get; }
}

public interface IPluginEditorCommands
{
    ValueTask<PluginCommandResult> ExecuteAsync(
        string command,
        IReadOnlyDictionary<string, string?> parameters,
        CancellationToken cancellationToken = default);
}

public sealed record PluginCommandResult(bool Succeeded, string Message)
{
    public static PluginCommandResult Success(string message = "Command completed.") => new(true, message);

    public static PluginCommandResult Failure(string message) => new(false, message);
}

public sealed record PluginManifest(
    string Id,
    string Name,
    string Version,
    string Publisher,
    IReadOnlyList<PluginCapability> Capabilities);

public enum PluginCapability
{
    EditorCommands,
    MediaImport,
    MediaExport,
    InspectorPanel,
    TimelinePanel
}
