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
            PluginManifest manifest = GetDeclaredManifest(pluginType);
            ValidateManifest(manifest);
            return manifest;
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
            PluginManifest declaredManifest = GetDeclaredManifest(pluginType);
            ValidateManifest(declaredManifest);

            var plugin = (IMagmaEditPlugin)Activator.CreateInstance(pluginType)!;
            ValidateManifest(plugin.Manifest);
            ValidateManifestConsistency(declaredManifest, plugin.Manifest);

            string pluginDataDirectory = Path.Combine(_pluginDataRoot, plugin.Manifest.Id);
            Directory.CreateDirectory(pluginDataDirectory);

            IPluginEditorCommands editorCommands = new PluginEditorCommandGate(
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
            .Where(type =>
                typeof(IMagmaEditPlugin).IsAssignableFrom(type) &&
                type is { IsAbstract: false, IsInterface: false } &&
                type.GetConstructor(Type.EmptyTypes) is not null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .FirstOrDefault();

        return pluginType ?? throw new InvalidDataException(
            $"Assembly '{assembly.GetName().Name}' does not contain a public parameterless MagmaEdit plugin.");
    }

    private static PluginManifest GetDeclaredManifest(Type pluginType)
    {
        CustomAttributeData? attribute = pluginType
            .CustomAttributes
            .FirstOrDefault(item => item.AttributeType == typeof(MagmaEditPluginAttribute));

        if (attribute is null)
        {
            throw new InvalidDataException(
                $"Plugin type '{pluginType.FullName}' must declare MagmaEditPluginAttribute metadata.");
        }

        IReadOnlyList<CustomAttributeTypedArgument> arguments = attribute.ConstructorArguments;
        if (arguments.Count != 5)
        {
            throw new InvalidDataException(
                $"Plugin type '{pluginType.FullName}' has invalid MagmaEditPluginAttribute metadata.");
        }

        string id = arguments[0].Value as string
            ?? throw new InvalidDataException("Plugin metadata is missing an identifier.");
        string name = arguments[1].Value as string
            ?? throw new InvalidDataException("Plugin metadata is missing a name.");
        string version = arguments[2].Value as string
            ?? throw new InvalidDataException("Plugin metadata is missing a version.");
        string publisher = arguments[3].Value as string
            ?? throw new InvalidDataException("Plugin metadata is missing a publisher.");

        var capabilities = new List<PluginCapability>();
        if (arguments[4].Value is IReadOnlyCollection<CustomAttributeTypedArgument> capabilityArguments)
        {
            foreach (CustomAttributeTypedArgument argument in capabilityArguments)
            {
                if (argument.Value is not int rawValue || !Enum.IsDefined(typeof(PluginCapability), rawValue))
                {
                    throw new InvalidDataException("Plugin metadata contains an invalid capability.");
                }

                capabilities.Add((PluginCapability)rawValue);
            }
        }

        return new PluginManifest(id, name, version, publisher, capabilities);
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

    private static void ValidateManifestConsistency(
        PluginManifest declaredManifest,
        PluginManifest runtimeManifest)
    {
        if (!string.Equals(declaredManifest.Id, runtimeManifest.Id, StringComparison.Ordinal) ||
            !string.Equals(declaredManifest.Name, runtimeManifest.Name, StringComparison.Ordinal) ||
            !string.Equals(declaredManifest.Version, runtimeManifest.Version, StringComparison.Ordinal) ||
            !string.Equals(declaredManifest.Publisher, runtimeManifest.Publisher, StringComparison.Ordinal) ||
            !declaredManifest.Capabilities.SequenceEqual(runtimeManifest.Capabilities))
        {
            throw new InvalidDataException(
                "Plugin manifest metadata does not match the runtime plugin manifest.");
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

/// <summary>Restricts plugin editor commands to capabilities declared by the plugin manifest.</summary>
public sealed class PluginEditorCommandGate : IPluginEditorCommands
{
    private readonly IReadOnlyList<PluginCapability> _capabilities;
    private readonly IPluginEditorCommands _inner;

    public PluginEditorCommandGate(
        IReadOnlyList<PluginCapability> capabilities,
        IPluginEditorCommands inner)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(inner);
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
