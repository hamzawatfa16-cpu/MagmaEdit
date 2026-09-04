using MagmaEdit.PluginHost;
using MagmaEdit.PluginHost.TestPlugin;

namespace MagmaEdit.Core.Tests;

public sealed class PluginIntegrityTests
{
    [Fact]
    public async Task ManagerRejectsAssemblyWhenDiscoveryHashNoLongerMatches()
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
            PluginDescriptor tamperedDescriptor = descriptor with
            {
                AssemblySha256 = new string('0', 64)
            };

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await manager.LoadAsync(tamperedDescriptor));

            Assert.Equal(
                "Plugin assembly changed after discovery for 'com.magmaedit.tests.plugin'.",
                exception.Message);
            Assert.Empty(manager.LoadedPluginIds);
        }
        finally
        {
            await manager.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ManagerRejectsManifestMismatchAfterLoadingAssembly()
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
            PluginDescriptor mismatchedDescriptor = descriptor with
            {
                Manifest = descriptor.Manifest with { Name = "Unexpected Plugin Name" }
            };

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(
                async () => await manager.LoadAsync(mismatchedDescriptor));

            Assert.Equal(
                "Plugin manifest changed between discovery and load for 'com.magmaedit.tests.plugin'.",
                exception.Message);
            Assert.Empty(manager.LoadedPluginIds);
        }
        finally
        {
            await manager.DisposeAsync();
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEditorCommands : MagmaEdit.Plugin.Abstractions.IPluginEditorCommands
    {
        public ValueTask<MagmaEdit.Plugin.Abstractions.PluginCommandResult> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string?> parameters,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(MagmaEdit.Plugin.Abstractions.PluginCommandResult.Success());
    }
}
