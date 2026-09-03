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
    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.throwing-initialize",
        "MagmaEdit Throwing Initialize Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        File.WriteAllText(Path.Combine(context.PluginDataDirectory, "initialize-started.marker"), "started");
        return ValueTask.FromException(new InvalidOperationException("Expected test initialization failure."));
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default)
    {
        File.WriteAllText(
            Path.Combine(Path.GetDirectoryName(typeof(ThrowingInitializePlugin).Assembly.Location)!, "shutdown-from-failed-initialize.marker"),
            "shutdown");
        return ValueTask.CompletedTask;
    }
}
