using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost.TestPlugin;

[MagmaEditPlugin(
    "com.magmaedit.tests.throwing-shutdown",
    "MagmaEdit Throwing Shutdown Test Plugin",
    "1.0.0",
    "MagmaEdit Tests",
    PluginCapability.EditorCommands)]
public sealed class ThrowingShutdownPlugin : IMagmaEditPlugin
{
    public ThrowingShutdownPlugin()
    {
        if (string.Equals(
                Environment.GetEnvironmentVariable("MAGMAEDIT_TEST_FAIL_PLUGIN_CONSTRUCTOR"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Plugin constructor should not run during manifest discovery.");
        }
    }

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
