using System.Reflection;
using MagmaEdit.Core.Automation;
using MagmaEdit.Core.Plugins;
using MagmaEdit.PluginHost;
using MagmaEdit.PluginHost.TestPlugin;
using Xunit;

namespace MagmaEdit.Core.Tests;

public sealed class PluginHostTests
{
    [Fact]
    public async Task DiscoveryReadsManifestWithoutRunningPluginConstructor()
    {
        string assemblyPath = typeof(ThrowingShutdownPlugin).Assembly.Location;
        Environment.SetEnvironmentVariable(ThrowingShutdownPlugin.ThrowOnConstructionEnvironmentVariable, "1");
        try
        {
            PluginManifest manifest = MagmaEditPluginHost.InspectManifest(assemblyPath);
            Assert.Equal("com.magmaedit.tests.throwing-shutdown", manifest.Id);
            Assert.Equal("Throwing Shutdown Test Plugin", manifest.Name);
            Assert.Contains("timeline.edit", manifest.Capabilities);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ThrowingShutdownPlugin.ThrowOnConstructionEnvironmentVariable, null);
        }
    }

    [Fact]
    public void CatalogDiscoveryIsDeterministicAndSkipsInaccessibleEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string pluginPath = typeof(PluginHostTestsPlugin).Assembly.Location;
            string target = Path.Combine(root, "Plugin.dll");
            File.Copy(pluginPath, target);

            PluginCatalog catalog = new(root);
            IReadOnlyList<PluginDescriptor> first = catalog.Discover();
            IReadOnlyList<PluginDescriptor> second = catalog.Discover();

            Assert.Equal(first.Select(item => item.Manifest.Id), second.Select(item => item.Manifest.Id));
            Assert.Single(first);
            Assert.Equal("com.magmaedit.tests.plugin", first[0].Manifest.Id);
        }
        finally
        {
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
            Assert.Equal(new[] { "com.magmaedit.tests.plugin" }, manager.LoadedPluginIds);

            await manager.UnloadAsync("com.magmaedit.tests.plugin");
        }
        finally
        {
            await manager.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(T? First, T? Second, Exception? FirstError, Exception? SecondError)> ObserveLoadsAsync<T>(Task<T> first, Task<T> second)
    {
        T? firstResult = null;
        T? secondResult = null;
        Exception? firstError = null;
        Exception? secondError = null;

        try
        {
            firstResult = await first.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            firstError = ex;
        }

        try
        {
            secondResult = await second.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            secondError = ex;
        }

        return (firstResult, secondResult, firstError, secondError);
    }
}
