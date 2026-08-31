namespace MagmaEdit.Core.Workspace;

/// <summary>Defines the user-owned local folders managed by MagmaEdit.</summary>
public sealed record WorkspaceLayout(
    string Root,
    string Media,
    string Projects,
    string Exports,
    string Cache)
{
    public const string WorkspaceFolderName = "Content Creation";

    /// <summary>Builds a deterministic layout under the user's Windows Videos folder.</summary>
    public static WorkspaceLayout ForCurrentUser()
    {
        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        if (string.IsNullOrWhiteSpace(videos))
        {
            throw new InvalidOperationException("Windows Videos folder could not be resolved.");
        }

        string root = Path.Combine(videos, WorkspaceFolderName);
        return Create(root);
    }

    /// <summary>Creates a layout from an explicit root, primarily for tests and future profile migration.</summary>
    public static WorkspaceLayout Create(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        root = Path.GetFullPath(root);

        return new WorkspaceLayout(
            root,
            Path.Combine(root, "Media"),
            Path.Combine(root, "Projects"),
            Path.Combine(root, "Exports"),
            Path.Combine(root, "Cache"));
    }
}
