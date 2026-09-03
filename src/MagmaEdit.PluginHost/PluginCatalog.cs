namespace MagmaEdit.PluginHost;

public sealed record PluginDescriptor(string AssemblyPath, MagmaEdit.Plugin.Abstractions.PluginManifest Manifest);

public sealed record PluginDiscoveryIssue(string AssemblyPath, string Message);

public sealed record PluginDiscoveryResult(
    IReadOnlyList<PluginDescriptor> Plugins,
    IReadOnlyList<PluginDiscoveryIssue> Issues);

public sealed class PluginCatalog
{
    public static PluginDiscoveryResult Discover(string pluginRoot)
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

        string[] assemblies;
        try
        {
            assemblies = Directory
                .EnumerateFiles(fullRoot, "*.dll", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException exception)
        {
            issues.Add(new PluginDiscoveryIssue(fullRoot, exception.Message));
            return new PluginDiscoveryResult(plugins, issues);
        }
        catch (UnauthorizedAccessException exception)
        {
            issues.Add(new PluginDiscoveryIssue(fullRoot, exception.Message));
            return new PluginDiscoveryResult(plugins, issues);
        }

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
            catch (Exception exception) when (
                exception is FileLoadException or
                FileNotFoundException or
                BadImageFormatException or
                InvalidDataException or
                ArgumentException or
                IOException or
                UnauthorizedAccessException or
                NotSupportedException or
                TypeLoadException or
                System.Reflection.ReflectionTypeLoadException)
            {
                string message = exception is System.Reflection.ReflectionTypeLoadException reflectionException
                    ? string.Join(
                        Environment.NewLine,
                        reflectionException.LoaderExceptions
                            .Where(error => error is not null)
                            .Select(error => error!.Message))
                    : exception.Message;

                issues.Add(new PluginDiscoveryIssue(assemblyPath, message));
            }
        }

        return new PluginDiscoveryResult(plugins, issues);
    }
}
