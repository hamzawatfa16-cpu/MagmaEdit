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
    public async Task ShutdownRunsWhenInitializationFailsAfterPluginStarts()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        string previous = Environment.GetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE") ?? string.Empty;
        Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE", "1");

        var host = new MagmaEditPluginHost(dataRoot, new TestEditorCommands());

        try
        {
            string assemblyPath = typeof(PluginHostTestsPlugin).Assembly.Location;

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await host.LoadAsync(assemblyPath));

            Assert.Equal("Expected test initialization failure.", exception.Message);
            string pluginDataDirectory = Path.Combine(dataRoot, "com.magmaedit.tests.plugin");
            Assert.True(File.Exists(Path.Combine(pluginDataDirectory, "initialize-failed.marker")));
            Assert.True(File.Exists(Path.Combine(pluginDataDirectory, "shutdown-after-init-failure.marker")));
        }
        finally
        {
            if (previous.Length == 0)
            {
                Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE", previous);
            }

            Directory.Delete(dataRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeniesEditorCommandsWhenCapabilityIsMissing()
    {
        var commands = new TestEditorCommands();
        var gate = new PluginEditorCommandGate([], commands);

        PluginCommandResult result = await gate.ExecuteAsync(
            "AddTrack",
            new Dictionary<string, string?>());

        Assert.False(result.Succeeded);
        Assert.Equal(
            "The plugin manifest does not declare the EditorCommands capability.",
            result.Message);
        Assert.Equal(0, commands.ExecutionCount);
    }

    [Fact]
    public async Task ForwardsEditorCommandsWhenCapabilityIsDeclared()
    {
        var commands = new TestEditorCommands();
        var gate = new PluginEditorCommandGate([PluginCapability.EditorCommands], commands);

        PluginCommandResult result = await gate.ExecuteAsync(
            "AddTrack",
            new Dictionary<string, string?>());

        Assert.True(result.Succeeded);
        Assert.Equal(1, commands.ExecutionCount);
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

    [Fact]
    public void CatalogReadsManifestWithoutRunningPluginConstructor()
    {
        string previous = Environment.GetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_CONSTRUCTOR") ?? string.Empty;
        Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_CONSTRUCTOR", "1");
        try
        {
            string assemblyPath = typeof(ThrowingShutdownPlugin).Assembly.Location;
            PluginManifest manifest = MagmaEditPluginHost.InspectManifest(assemblyPath);

            Assert.Equal("com.magmaedit.tests.throwing-shutdown", manifest.Id);
            Assert.Equal("MagmaEdit Throwing Shutdown Test Plugin", manifest.Name);
            Assert.Equal([PluginCapability.EditorCommands], manifest.Capabilities);
        }
        finally
        {
            if (previous.Length == 0)
            {
                Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_CONSTRUCTOR", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_CONSTRUCTOR", previous);
            }
        }
    }

    [Fact]
    public async Task ManagerLoadsAndUnloadsPluginsByStableIdentifier()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var manager = new PluginManager(Path.Combine(root, "data"), new TestEditorCommands());
        try
        {
            string assemblyPath = typeof(PluginHostTestsPlugin).Assembly.Location;
            PluginDescriptor descriptor = new(
                assemblyPath,
                MagmaEditPluginHost.InspectManifest(assemblyPath));

            LoadedPlugin loaded = await manager.LoadAsync(descriptor);

            Assert.Equal("com.magmaedit.tests.plugin", loaded.Manifest.Id);
            Assert.Equal(
                ["com.magmaedit.tests.plugin"],
                manager.LoadedPluginIds);
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await manager.LoadAsync(descriptor));

            Assert.True(await manager.UnloadAsync(loaded.Manifest.Id));
            Assert.Empty(manager.LoadedPluginIds);
            Assert.False(await manager.UnloadAsync(loaded.Manifest.Id));
        }
        finally
        {
            await manager.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentManagerLoadsAllowOnlyOnePluginInstance()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var manager = new PluginManager(Path.Combine(root, "data"), new TestEditorCommands());
        try
        {
            string assemblyPath = typeof(PluginHostTestsPlugin).Assembly.Location;
            PluginDescriptor descriptor = new(
                assemblyPath,
                MagmaEditPluginHost.InspectManifest(assemblyPath));

            Task<LoadedPlugin> first = manager.LoadAsync(descriptor).AsTask();
            Task<LoadedPlugin> second = manager.LoadAsync(descriptor).AsTask();
            (LoadedPlugin? firstLoaded, LoadedPlugin? secondLoaded, Exception? firstError, Exception? secondError) =
                await ObserveLoadsAsync(first, second);

            Assert.NotEqual(firstError is null, secondError is null);
            Assert.Single(new LoadedPlugin?[] { firstLoaded, secondLoaded }.OfType<LoadedPlugin>());
            Exception duplicateError = firstError ?? secondError!;
            Assert.IsType<InvalidOperationException>(duplicateError);
            Assert.Equal(["com.magmaedit.tests.plugin"], manager.LoadedPluginIds);

            await manager.UnloadAsync("com.magmaedit.tests.plugin");
        }
        finally
        {
            await manager.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(LoadedPlugin? First, LoadedPlugin? Second, Exception? FirstError, Exception? SecondError)> ObserveLoadsAsync(
        Task<LoadedPlugin> first,
        Task<LoadedPlugin> second)
    {
        LoadedPlugin? firstLoaded = null;
        LoadedPlugin? secondLoaded = null;
        Exception? firstError = null;
        Exception? secondError = null;

        try
        {
            firstLoaded = await first.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstError = exception;
        }

        try
        {
            secondLoaded = await second.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            secondError = exception;
        }

        return (firstLoaded, secondLoaded, firstError, secondError);
    }

    [Fact]
    public async Task ManagerContinuesDisposingPluginsAfterOneShutdownFailure()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var manager = new PluginManager(Path.Combine(root, "data"), new TestEditorCommands());
        try
        {
            string assemblyPath = typeof(ThrowingShutdownPlugin).Assembly.Location;
            PluginDescriptor descriptor = new(
                assemblyPath,
                MagmaEditPluginHost.InspectManifest(assemblyPath));

            await manager.LoadAsync(descriptor);
            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await manager.DisposeAsync());

            Assert.Equal("Expected test shutdown failure.", exception.Message);
            Assert.Empty(manager.LoadedPluginIds);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEditorCommands : IPluginEditorCommands
    {
        public int ExecutionCount { get; private set; }

        public ValueTask<PluginCommandResult> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string?> parameters,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            return ValueTask.FromResult(PluginCommandResult.Success());
        }
    }
}

[MagmaEditPlugin(
    "com.magmaedit.tests.plugin",
    "MagmaEdit Test Plugin",
    "1.0.0",
    "MagmaEdit Tests",
    PluginCapability.EditorCommands)]
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
        if (string.Equals(
                Environment.GetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE"),
                "1",
                StringComparison.Ordinal))
        {
            File.WriteAllText(Path.Combine(_pluginDataDirectory, "initialize-failed.marker"), "failed");
            return ValueTask.FromException(new InvalidOperationException("Expected test initialization failure."));
        }

        File.WriteAllText(Path.Combine(_pluginDataDirectory, "initialized.marker"), "initialized");
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_pluginDataDirectory is null)
        {
            throw new InvalidOperationException("Plugin was not initialized.");
        }

        if (string.Equals(
                Environment.GetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_INITIALIZE"),
                "1",
                StringComparison.Ordinal))
        {
            File.WriteAllText(
                Path.Combine(_pluginDataDirectory, "shutdown-after-init-failure.marker"),
                "shutdown");
        }

        File.WriteAllText(Path.Combine(_pluginDataDirectory, "shutdown.marker"), "shutdown");
        return ValueTask.CompletedTask;
    }
}
