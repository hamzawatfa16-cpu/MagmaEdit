using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.Core.Tests;

public sealed class PluginContractsTests
{
    [Fact]
    public void PluginManifest_PreservesIdentityAndCapabilities()
    {
        var manifest = new PluginManifest(
            "com.magmaedit.example",
            "Example Plugin",
            "1.0.0",
            "MagmaEdit",
            [PluginCapability.EditorCommands, PluginCapability.InspectorPanel]);

        Assert.Equal("com.magmaedit.example", manifest.Id);
        Assert.Equal("Example Plugin", manifest.Name);
        Assert.Equal("1.0.0", manifest.Version);
        Assert.Equal("MagmaEdit", manifest.Publisher);
        Assert.Equal([PluginCapability.EditorCommands, PluginCapability.InspectorPanel], manifest.Capabilities);
    }
}
