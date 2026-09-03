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
