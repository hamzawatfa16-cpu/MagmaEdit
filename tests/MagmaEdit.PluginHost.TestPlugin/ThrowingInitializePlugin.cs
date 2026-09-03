using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost.TestPlugin;

[MagmaEditPlugin(
    "com.magmaedit.tests.throwing-initialize",
    "MagmaEdit Throwing Initialize Test Plugin",
    "1.0.0",
    "MagmaEdit Tests",
    PluginCapability.EditorCommands)]
public sealed class ThrowingInitializePlugin : IMagmaEditPlugin
{
    private string? _pluginDataDirectory;

    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.throwing-initialize",
        "MagmaEdit Throwing Initialize Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _pluginDataDirectory = context.PluginDataDirectory;
        File.WriteAllText(Path.Combine(_pluginDataDirectory, "initialize-started.marker"), "started");
        return ValueTask.FromException(new InvalidOperationException("Expected test initialization failure."));
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_pluginDataDirectory is null)
        {
            throw new InvalidOperationException("Plugin was not initialized.");
        }

        File.WriteAllText(Path.Combine(_pluginDataDirectory, "shutdown-from-failed-initialize.marker"), "shutdown");
        return ValueTask.CompletedTask;
    }
}
