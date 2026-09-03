using System.Reflection;
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

    public bool IsLoaded(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        return _manager.LoadedPluginIds.Contains(pluginId, StringComparer.Ordinal);
    }

    public async Task<bool> LoadPluginAsync(
        PluginDescriptor descriptor,
        Action<string> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        if (IsLoaded(descriptor.Manifest.Id))
        {
            return true;
        }

        try
        {
            await _manager.LoadAsync(descriptor, cancellationToken).ConfigureAwait(false);
            report($"Loaded plugin: {descriptor.Manifest.Name}");
            return true;
        }
        catch (Exception exception) when (
            exception is FileLoadException or
            FileNotFoundException or
            BadImageFormatException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException or
            TypeLoadException or
            ReflectionTypeLoadException)
        {
            report($"Plugin failed to load: {descriptor.Manifest.Name}: {exception.Message}");
            return false;
        }
    }

    public async Task<bool> UnloadPluginAsync(
        string pluginId,
        Action<string> report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            bool unloaded = await _manager.UnloadAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (unloaded)
            {
                report($"Unloaded plugin: {pluginId}");
            }

            return unloaded;
        }
        catch (Exception exception) when (
            exception is FileLoadException or
            FileNotFoundException or
            BadImageFormatException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException or
            IOException or
            UnauthorizedAccessException or
            TypeLoadException or
            ReflectionTypeLoadException)
        {
            report($"Plugin failed to unload: {pluginId}: {exception.Message}");
            return false;
        }
    }

    public ValueTask DisposeAsync() => _manager.DisposeAsync();
}
