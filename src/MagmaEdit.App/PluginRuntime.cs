using MagmaEdit.Core.Workspace;
using MagmaEdit.PluginHost;

namespace MagmaEdit.App;

/// <summary>Discovers and owns the lifecycle of installed desktop plugins.</summary>
internal sealed class PluginRuntime : IAsyncDisposable
{
    private readonly PluginManager _manager;
    private readonly PluginDiscoveryResult _discovery;

    private PluginRuntime(PluginManager manager, PluginDiscoveryResult discovery)
    {
        _manager = manager;
        _discovery = discovery;
    }

    public PluginDiscoveryResult Discovery => _discovery;

    public IReadOnlyList<string> LoadedPluginIds => _manager.LoadedPluginIds;

    public static PluginRuntime Create(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        WorkspaceLayout workspace = WorkspaceLayout.ForCurrentUser();
        PluginDiscoveryResult discovery = PluginCatalog.Discover(workspace.Plugins);
        var commands = new PluginEditorCommandAdapter(
            window.GetProjectForExport,
            window.SaveProjectForExport);
        var manager = new PluginManager(
            Path.Combine(workspace.Cache, "Plugins"),
            commands);

        return new PluginRuntime(manager, discovery);
    }

    public async Task LoadDiscoveredPluginsAsync(
        Action<string> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        foreach (PluginDescriptor descriptor in _discovery.Plugins)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _manager.LoadAsync(descriptor, cancellationToken).ConfigureAwait(false);
                report($"Loaded plugin: {descriptor.Manifest.Name}");
            }
            catch (Exception exception) when (exception is FileLoadException or FileNotFoundException or BadImageFormatException or InvalidDataException or ArgumentException or InvalidOperationException or IOException)
            {
                report($"Plugin failed to load: {descriptor.Manifest.Name}: {exception.Message}");
            }
        }
    }

    public ValueTask DisposeAsync() => _manager.DisposeAsync();
}
