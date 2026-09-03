using MagmaEdit.Plugin.Abstractions;

namespace MagmaEdit.PluginHost.NoEditorCommandsTestPlugin;

public sealed class NoEditorCommandsPlugin : IMagmaEditPlugin
{
    public PluginManifest Manifest { get; } = new(
        "com.magmaedit.tests.no-editor-commands",
        "MagmaEdit No Editor Commands Test Plugin",
        "1.0.0",
        "MagmaEdit Tests",
        []);

    public ValueTask InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        PluginCommandResult result = context.EditorCommands
            .ExecuteAsync("AddTrack", new Dictionary<string, string?>())
            .GetAwaiter()
            .GetResult();
        File.WriteAllText(
            Path.Combine(context.PluginDataDirectory, "editor-command-result.txt"),
            result.Message);
        return ValueTask.CompletedTask;
    }

    public ValueTask ShutdownAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
