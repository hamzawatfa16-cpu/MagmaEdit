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

public sealed class ThrowingShutdownPlugin : IMagmaEditPlugin
{
    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.throwing-shutdown",
        "MagmaEdit Throwing Shutdown Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new InvalidOperationException("Expected test shutdown failure."));
}
