# MagmaEdit plugin system

MagmaEdit has a first-party plugin contract and collectible plugin host.

## What is implemented

A plugin is a .NET class library containing one public, parameterless type implementing `IMagmaEditPlugin`. The manifest supplies a stable identifier, display name, version, publisher, and declared capabilities.

The host provides:

- deterministic recursive DLL discovery through `PluginCatalog`
- manifest validation before a plugin is loaded
- duplicate plugin-ID detection
- collectible `AssemblyLoadContext` isolation
- per-plugin writable data directories
- capability-gated editor command access
- explicit initialize/shutdown lifecycle
- safe unloading even when plugin shutdown reports an error
- `PluginManager` lifecycle ownership for multiple explicitly loaded plugins
- an in-app plugin manager that requires explicit approval before a discovered plugin is loaded

## Security boundary

Discovery is not execution. MagmaEdit discovers DLLs but does not automatically execute them. The desktop application opens the plugin manager when plugins are discovered so the user can review each plugin's name, publisher, version, identifier, and requested capabilities before choosing **Approve & Load**.

A discovered plugin remains inert until the user explicitly loads it. Loaded plugins can later be unloaded from the same manager.

Plugins only receive the interfaces declared in `MagmaEdit.Plugin.Abstractions`. They do not receive direct access to the mutable Core project model.

## Plugin directory

The recommended user plugin directory is:

```text
%USERPROFILE%\Videos\Content Creation\Plugins\
```

Plugin-specific writable data is kept separately by the host under its configured plugin-data root.

## Minimal plugin

```csharp
public sealed class ExamplePlugin : IMagmaEditPlugin
{
    public PluginManifest Manifest { get; } = new(
        "com.example.magmaedit.plugin",
        "Example Plugin",
        "1.0.0",
        "Example Publisher",
        [PluginCapability.EditorCommands]);

    public ValueTask InitializeAsync(
        IPluginContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask ShutdownAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
```

## AI integration

AI clients should use the MagmaEdit MCP server rather than loading arbitrary plugin DLLs. MCP exposes the stable editor command and read-only project-state contracts and routes mutations through the same authorization and undo/redo boundary used by MagmaEdit automation.

The MCP server is local-process scoped today. Network/remote MCP requires a separate authenticated transport and authorization model before it should be enabled.
