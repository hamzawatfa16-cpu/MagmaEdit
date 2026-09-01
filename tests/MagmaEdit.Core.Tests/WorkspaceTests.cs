using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public void CreateProducesExpectedFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(root);

        try
        {
            Assert.Equal(Path.GetFullPath(root), layout.Root);
            Assert.Equal(Path.Combine(layout.Root, "Media"), layout.Media);
            Assert.Equal(Path.Combine(layout.Root, "Projects"), layout.Projects);
            Assert.Equal(Path.Combine(layout.Root, "Exports"), layout.Exports);
            Assert.Equal(Path.Combine(layout.Root, "Cache"), layout.Cache);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ForCurrentUserUsesContentCreationUnderVideos()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        Assert.False(string.IsNullOrWhiteSpace(videos));

        WorkspaceLayout layout = WorkspaceLayout.ForCurrentUser();

        Assert.Equal(
            Path.Combine(Path.GetFullPath(videos), WorkspaceLayout.WorkspaceFolderName),
            layout.Root);
    }

    [Fact]
    public void EnsureCreatedCreatesAllWorkspaceDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(root);

        try
        {
            WorkspaceManager manager = new(layout);
            manager.EnsureCreated();
            manager.EnsureCreated();

            Assert.True(Directory.Exists(layout.Root));
            Assert.True(Directory.Exists(layout.Media));
            Assert.True(Directory.Exists(layout.Projects));
            Assert.True(Directory.Exists(layout.Exports));
            Assert.True(Directory.Exists(layout.Cache));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
