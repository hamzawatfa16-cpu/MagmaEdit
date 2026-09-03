using System.Reflection;
using System.Runtime.Loader;
using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost;

public sealed class MagmaEditPluginHost
{
    private readonly string _pluginDataRoot;
    private readonly IPluginEditorCommands _editorCommands;

    public MagmaEditPluginHost(string pluginDataRoot, IPluginEditorCommands editorCommands)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDataRoot);
        ArgumentNullException.ThrowIfNull(editorCommands);

        _pluginDataRoot = Path.GetFullPath(pluginDataRoot);
        _editorCommands = editorCommands;
        Directory.CreateDirectory(_pluginDataRoot);
    }

    public static PluginManifest InspectManifest(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        string fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new FileNotFoundException("Plugin assembly was not found.", fullAssemblyPath);
        }

        byte[] assemblyBytes = File.ReadAllBytes(fullAssemblyPath);
        var loadContext = new PluginLoadContext(fullAssemblyPath);
        try
        {
            using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
            Assembly assembly = loadContext.LoadFromStream(assemblyStream);
            Type pluginType = FindPluginType(assembly);
            var plugin = (IMagmaEditPlugin)Activator.CreateInstance(pluginType)!;
            ValidateManifest(plugin.Manifest);
            return plugin.Manifest;
        }
        finally
        {
            loadContext.Unload();
        }
    }

    public async ValueTask<LoadedPlugin> LoadAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullAssemblyPath = Path.GetFullPath(assemblyPath);
        if (!File.Exists(fullAssemblyPath))
        {
            throw new FileNotFoundException("Plugin assembly was not found.", fullAssemblyPath);
        }

        var loadContext = new PluginLoadContext(fullAssemblyPath);
        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(fullAssemblyPath);
            Type pluginType = FindPluginType(assembly);
            var plugin = (IMagmaEditPlugin)Activator.CreateInstance(pluginType)!;
            ValidateManifest(plugin.Manifest);

            string pluginDataDirectory = Path.Combine(_pluginDataRoot, plugin.Manifest.Id);
            Directory.CreateDirectory(pluginDataDirectory);

            IPluginEditorCommands editorCommands = new CapabilityRestrictedEditorCommands(
                plugin.Manifest.Capabilities,
                _editorCommands);
            var context = new PluginContext(pluginDataDirectory, editorCommands);
            await plugin.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
            return new LoadedPlugin(loadContext, plugin, plugin.Manifest);
        }
        catch
        {
            loadContext.Unload();
            throw;
        }
    }

    private static Type FindPluginType(Assembly assembly)
    {
        Type? pluginType = assembly
            .GetExportedTypes()
            .FirstOrDefault(type =>
                typeof(IMagmaEditPlugin).IsAssignableFrom(type) &&
                type is { IsAbstract: false, IsInterface: false } &&
                type.GetConstructor(Type.EmptyTypes) is not null);

        return pluginType ?? throw new InvalidDataException(
            $"Assembly '{assembly.GetName().Name}' does not contain a public parameterless MagmaEdit plugin.");
    }

    private static void ValidateManifest(PluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateIdentifier(manifest.Id, nameof(manifest.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Name, nameof(manifest.Name));
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Version, nameof(manifest.Version));
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.Publisher, nameof(manifest.Publisher));
        ArgumentNullException.ThrowIfNull(manifest.Capabilities);

        if (manifest.Capabilities.Count != manifest.Capabilities.Distinct().Count())
        {
            throw new ArgumentException("Plugin capabilities must not contain duplicates.", nameof(manifest));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (!value.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'))
        {
            throw new ArgumentException(
                "Plugin identifiers may contain only letters, digits, '.', '-' and '_'.",
                parameterName);
        }
    }

    private sealed class PluginContext : IPluginContext
    {
        public PluginContext(string pluginDataDirectory, IPluginEditorCommands editorCommands)
        {
            PluginDataDirectory = pluginDataDirectory;
            EditorCommands = editorCommands;
        }

        public string PluginDataDirectory { get; }

        public IPluginEditorCommands EditorCommands { get; }
    }

    private sealed class CapabilityRestrictedEditorCommands : IPluginEditorCommands
    {
        private readonly IReadOnlyList<PluginCapability> _capabilities;
        private readonly IPluginEditorCommands _inner;

        public CapabilityRestrictedEditorCommands(
            IReadOnlyList<PluginCapability> capabilities,
            IPluginEditorCommands inner)
        {
            _capabilities = capabilities;
            _inner = inner;
        }

        public ValueTask<PluginCommandResult> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string?> parameters,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(command);
            ArgumentNullException.ThrowIfNull(parameters);

            if (!_capabilities.Contains(PluginCapability.EditorCommands))
            {
                return ValueTask.FromResult(PluginCommandResult.Failure(
                    "The plugin manifest does not declare the EditorCommands capability."));
            }

            return _inner.ExecuteAsync(command, parameters, cancellationToken);
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string assemblyPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(assemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(
                    assemblyName.Name,
                    typeof(IMagmaEditPlugin).Assembly.GetName().Name,
                    StringComparison.Ordinal))
            {
                return null;
            }

            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
        }
    }
}

public sealed class LoadedPlugin : IAsyncDisposable
{
    private readonly AssemblyLoadContext _loadContext;
    private readonly IMagmaEditPlugin _plugin;
    private bool _shutdown;

    internal LoadedPlugin(
        AssemblyLoadContext loadContext,
        IMagmaEditPlugin plugin,
        PluginManifest manifest)
    {
        _loadContext = loadContext;
        _plugin = plugin;
        Manifest = manifest;
    }

    public PluginManifest Manifest { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_shutdown)
            {
                _shutdown = true;
                await _plugin.ShutdownAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _loadContext.Unload();
        }
    }
}
