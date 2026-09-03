using MagmaEdit.Plugin.Abstractions;
using MagmaEdit.PluginHost;
using Xunit;

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

            Assert.Equal("com.magmaedit.tests.plugin", loaded.Manifest.Id);
            Assert.True(Directory.Exists(Path.Combine(dataRoot, loaded.Manifest.Id)));

            await loaded.DisposeAsync();
            Assert.True(PluginHostTestsPlugin.WasShutdown);
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
    public static bool WasShutdown { get; private set; }

    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.plugin",
        "MagmaEdit Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        Assert.True(Directory.Exists(context.PluginDataDirectory));
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        WasShutdown = true;
        return ValueTask.CompletedTask;
    }
}
