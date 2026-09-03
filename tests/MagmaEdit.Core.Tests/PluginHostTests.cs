using MagmaEdit.Plugin.Abstractions;
using MagmaEdit.PluginHost;

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
    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.plugin",
        "MagmaEdit Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(Path.Combine(context.PluginDataDirectory, "initialized.marker"), "initialized");
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        string assemblyDirectory = Path.GetDirectoryName(typeof(PluginHostTestsPlugin).Assembly.Location)
            ?? throw new InvalidOperationException("Test assembly directory was not available.");
        string? pluginDataDirectory = Directory.GetDirectories(
            Path.Combine(Path.GetTempPath(), "MagmaEditTests"),
            "*",
            SearchOption.TopDirectoryOnly)
            .FirstOrDefault(directory =>
                File.Exists(Path.Combine(directory, "com.magmaedit.tests.plugin", "initialized.marker")));
        if (pluginDataDirectory is not null)
        {
            File.WriteAllText(Path.Combine(pluginDataDirectory, "com.magmaedit.tests.plugin", "shutdown.marker"), assemblyDirectory);
        }

        return ValueTask.CompletedTask;
    }
}
