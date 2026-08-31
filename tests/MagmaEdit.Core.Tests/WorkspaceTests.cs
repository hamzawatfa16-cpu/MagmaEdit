using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class WorkspaceTests
{
    [Fact]
    public void Create_ProducesExpectedFolders()
    {
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N")));

        Assert.EndsWith(Path.Combine("Content Creation"), layout.Root);
        Assert.Equal(Path.Combine(layout.Root, "Media"), layout.Media);
        Assert.Equal(Path.Combine(layout.Root, "Projects"), layout.Projects);
        Assert.Equal(Path.Combine(layout.Root, "Exports"), layout.Exports);
        Assert.Equal(Path.Combine(layout.Root, "Cache"), layout.Cache);
    }

    [Fact]
    public void EnsureCreated_CreatesAllWorkspaceDirectories()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(root);

        try
        {
            new WorkspaceManager(layout).EnsureCreated();

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
