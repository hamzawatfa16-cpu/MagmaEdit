using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost;

/// <summary>Owns the lifecycle of explicitly loaded MagmaEdit plugins.</summary>
public sealed class PluginManager : IAsyncDisposable
{
    private readonly MagmaEditPluginHost _host;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.Ordinal);
    private bool _disposed;

    public PluginManager(string pluginDataRoot, IPluginEditorCommands editorCommands)
    {
        _host = new MagmaEditPluginHost(pluginDataRoot, editorCommands);
    }

    public IReadOnlyList<string> LoadedPluginIds =>
        _loadedPlugins.Keys.Order(StringComparer.Ordinal).ToArray();

    public async ValueTask<LoadedPlugin> LoadAsync(
        PluginDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (_loadedPlugins.ContainsKey(descriptor.Manifest.Id))
        {
            throw new InvalidOperationException(
                $"Plugin '{descriptor.Manifest.Id}' is already loaded.");
        }

        LoadedPlugin loaded = await _host.LoadAsync(
            descriptor.AssemblyPath,
            cancellationToken).ConfigureAwait(false);

        if (!string.Equals(loaded.Manifest.Id, descriptor.Manifest.Id, StringComparison.Ordinal))
        {
            await loaded.DisposeAsync().ConfigureAwait(false);
            throw new InvalidDataException(
                $"Plugin manifest changed between discovery and load for '{descriptor.Manifest.Id}'.");
        }

        _loadedPlugins.Add(loaded.Manifest.Id, loaded);
        return loaded;
    }

    public async ValueTask<bool> UnloadAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_loadedPlugins.Remove(pluginId, out LoadedPlugin? loaded))
        {
            return false;
        }

        await loaded.DisposeAsync().ConfigureAwait(false);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Exception? firstException = null;
        foreach (string pluginId in _loadedPlugins.Keys.Order(StringComparer.Ordinal).ToArray())
        {
            LoadedPlugin loaded = _loadedPlugins[pluginId];
            try
            {
                await loaded.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        _loadedPlugins.Clear();
        if (firstException is not null)
        {
            throw firstException;
        }
    }
}
