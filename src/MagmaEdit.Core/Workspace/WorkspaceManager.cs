namespace MagmaEdit.Core.Workspace;

/// <summary>Creates and validates the local MagmaEdit workspace without touching user media.</summary>
public sealed class WorkspaceManager
{
    public WorkspaceLayout Layout { get; }

    public WorkspaceManager(WorkspaceLayout layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    /// <summary>Creates the workspace directories. Existing directories are preserved.</summary>
    public void EnsureCreated()
    {
        Directory.CreateDirectory(Layout.Root);
        Directory.CreateDirectory(Layout.Media);
        Directory.CreateDirectory(Layout.Projects);
        Directory.CreateDirectory(Layout.Exports);
        Directory.CreateDirectory(Layout.Cache);
    }
}
