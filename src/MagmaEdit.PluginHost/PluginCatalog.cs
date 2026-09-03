namespace MagmaEdit.PluginHost;

public sealed record PluginDescriptor(string AssemblyPath, MagmaEdit.Plugin.Abstractions.PluginManifest Manifest);

public sealed record PluginDiscoveryIssue(string AssemblyPath, string Message);

public sealed record PluginDiscoveryResult(
    IReadOnlyList<PluginDescriptor> Plugins,
    IReadOnlyList<PluginDiscoveryIssue> Issues);

public sealed class PluginCatalog
{
    public PluginCatalog(MagmaEditPluginHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
    }

    public PluginDiscoveryResult Discover(string pluginRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginRoot);

        string fullRoot = Path.GetFullPath(pluginRoot);
        if (!Directory.Exists(fullRoot))
        {
            throw new DirectoryNotFoundException($"Plugin directory was not found: {fullRoot}");
        }

        var plugins = new List<PluginDescriptor>();
        var issues = new List<PluginDiscoveryIssue>();
        var knownIds = new HashSet<string>(StringComparer.Ordinal);

        IEnumerable<string> assemblies = Directory
            .EnumerateFiles(fullRoot, "*.dll", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (string assemblyPath in assemblies)
        {
            try
            {
                MagmaEdit.Plugin.Abstractions.PluginManifest manifest = MagmaEditPluginHost.InspectManifest(assemblyPath);
                if (!knownIds.Add(manifest.Id))
                {
                    issues.Add(new PluginDiscoveryIssue(
                        assemblyPath,
                        $"Duplicate plugin identifier '{manifest.Id}'."));
                    continue;
                }

                plugins.Add(new PluginDescriptor(assemblyPath, manifest));
            }
            catch (Exception exception) when (exception is FileLoadException or FileNotFoundException or BadImageFormatException or InvalidDataException or ArgumentException)
            {
                issues.Add(new PluginDiscoveryIssue(assemblyPath, exception.Message));
            }
        }

        return new PluginDiscoveryResult(plugins, issues);
    }
}
