using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost;

/// <summary>Owns the lifecycle of explicitly loaded MagmaEdit plugins.</summary>
public sealed class PluginManager : IAsyncDisposable
{
    private readonly MagmaEditPluginHost _host;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
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
        cancellationToken.ThrowIfCancellationRequested();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_loadedPlugins.ContainsKey(descriptor.Manifest.Id))
            {
                throw new InvalidOperationException(
                    $"Plugin '{descriptor.Manifest.Id}' is already loaded.");
            }

            string currentAssemblySha256 = PluginIntegrity.ComputeSha256(descriptor.AssemblyPath);
            if (!string.Equals(
                    currentAssemblySha256,
                    descriptor.AssemblySha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Plugin assembly changed after discovery for '{descriptor.Manifest.Id}'.");
            }

            LoadedPlugin loaded = await _host.LoadAsync(
                descriptor.AssemblyPath,
                cancellationToken).ConfigureAwait(false);

            if (!ManifestMatches(descriptor.Manifest, loaded.Manifest))
            {
                await loaded.DisposeAsync().ConfigureAwait(false);
                throw new InvalidDataException(
                    $"Plugin manifest changed between discovery and load for '{descriptor.Manifest.Id}'.");
            }

            _loadedPlugins.Add(loaded.Manifest.Id, loaded);
            return loaded;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask<bool> UnloadAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        cancellationToken.ThrowIfCancellationRequested();

        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_loadedPlugins.Remove(pluginId, out LoadedPlugin? loaded))
            {
                return false;
            }

            await loaded.DisposeAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
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
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }

    private static bool ManifestMatches(PluginManifest expected, PluginManifest actual) =>
        string.Equals(expected.Id, actual.Id, StringComparison.Ordinal) &&
        string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) &&
        string.Equals(expected.Version, actual.Version, StringComparison.Ordinal) &&
        string.Equals(expected.Publisher, actual.Publisher, StringComparison.Ordinal) &&
        expected.Capabilities.SequenceEqual(actual.Capabilities);
}
