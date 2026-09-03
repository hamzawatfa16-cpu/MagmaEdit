using MagmaEdit.Plugin.Abstractions;
using MagmaEdit.PluginHost;
using MagmaEdit.PluginHost.TestPlugin;

namespace MagmaEdit.Core.Tests;

public sealed class PluginHostTests
{
    [Fact]
    public async Task LoadsInitializesAndDisposesPluginAssembly()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);

        var commands = new TestEditorCommands();
        var host = new MagmaEditPluginHost(dataRoot, commands);

        try
        {
            string assemblyPath = typeof(PluginHostTestsPlugin).Assembly.Location;
            LoadedPlugin loaded = await host.LoadAsync(assemblyPath);
            string pluginDataDirectory = Path.Combine(dataRoot, loaded.Manifest.Id);

            Assert.Equal("com.magmaedit.tests.plugin", loaded.Manifest.Id);
            Assert.True(Directory.Exists(pluginDataDirectory));
            Assert.True(File.Exists(Path.Combine(pluginDataDirectory, "initialized.marker")));

            await loaded.DisposeAsync();
            Assert.True(File.Exists(Path.Combine(pluginDataDirectory, "shutdown.marker")));
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeniesEditorCommandsWhenPluginDoesNotDeclareCapability()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);

        var host = new MagmaEditPluginHost(dataRoot, new TestEditorCommands());

        try
        {
            string assemblyPath = typeof(NoEditorCommandsPlugin).Assembly.Location;
            LoadedPlugin loaded = await host.LoadAsync(assemblyPath);
            string pluginDataDirectory = Path.Combine(dataRoot, loaded.Manifest.Id);

            Assert.Equal(
                "The plugin manifest does not declare the EditorCommands capability.",
                File.ReadAllText(Path.Combine(pluginDataDirectory, "editor-command-result.txt")));

            await loaded.DisposeAsync();
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public async Task UnloadsPluginWhenShutdownFails()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);

        var commands = new TestEditorCommands();
        var host = new MagmaEditPluginHost(dataRoot, commands);

        try
        {
            string assemblyPath = typeof(ThrowingShutdownPlugin).Assembly.Location;
            LoadedPlugin loaded = await host.LoadAsync(assemblyPath);

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await loaded.DisposeAsync());
            await loaded.DisposeAsync();
        }
        finally
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public void CatalogDiscoversValidPluginsInDeterministicOrderAndReportsProblems()
    {
        string pluginRoot = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginRoot);

        try
        {
            string validAssembly = typeof(ThrowingShutdownPlugin).Assembly.Location;
            string firstPlugin = Path.Combine(pluginRoot, "01-first", "Plugin.dll");
            string duplicatePlugin = Path.Combine(pluginRoot, "02-duplicate", "Plugin.dll");
            string invalidAssembly = Path.Combine(pluginRoot, "03-invalid", "NotAPlugin.dll");

            Directory.CreateDirectory(Path.GetDirectoryName(firstPlugin)!);
            Directory.CreateDirectory(Path.GetDirectoryName(duplicatePlugin)!);
            Directory.CreateDirectory(Path.GetDirectoryName(invalidAssembly)!);
            File.Copy(validAssembly, firstPlugin);
            File.Copy(validAssembly, duplicatePlugin);
            File.Copy(typeof(MagmaEditPluginHost).Assembly.Location, invalidAssembly);

            PluginDiscoveryResult result = PluginCatalog.Discover(pluginRoot);

            Assert.Single(result.Plugins);
            Assert.Equal("com.magmaedit.tests.throwing-shutdown", result.Plugins[0].Manifest.Id);
            Assert.Equal(firstPlugin, result.Plugins[0].AssemblyPath);
            Assert.Equal(2, result.Issues.Count);
            Assert.Contains(result.Issues, issue => issue.AssemblyPath == duplicatePlugin);
            Assert.Contains(result.Issues, issue => issue.AssemblyPath == invalidAssembly);
        }
        finally
        {
            Directory.Delete(pluginRoot, recursive: true);
        }
    }

    private sealed class TestEditorCommands : IPluginEditorCommands
    {
        public ValueTask<PluginCommandResult> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string?> parameters,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(PluginCommandResult.Success());
    }
}

public sealed class PluginHostTestsPlugin : IMagmaEditPlugin
{
    private string? _pluginDataDirectory;

    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.plugin",
        "MagmaEdit Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _pluginDataDirectory = context.PluginDataDirectory;
        File.WriteAllText(Path.Combine(_pluginDataDirectory, "initialized.marker"), "initialized");
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_pluginDataDirectory is null)
        {
            throw new InvalidOperationException("Plugin was not initialized.");
        }

        File.WriteAllText(Path.Combine(_pluginDataDirectory, "shutdown.marker"), "shutdown");
        return ValueTask.CompletedTask;
    }
}
