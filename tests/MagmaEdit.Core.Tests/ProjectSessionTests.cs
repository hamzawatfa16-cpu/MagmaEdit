using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectSessionTests
{
    [Fact]
    public void CreateNewPersistsAndActivatesCollisionFreeProject()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            ProjectSession session = new(layout);

            ProjectDocument first = session.CreateNew("My Shorts");
            ProjectDocument second = session.CreateNew("My Shorts");

            Assert.NotEqual(first.Id, second.Id);
            Assert.Same(second, session.CurrentProject);
            Assert.Equal(
                Path.Combine(layout.Projects, "My Shorts 2.magmaedit.json"),
                session.CurrentPath);
            Assert.True(File.Exists(session.CurrentPath));
            Assert.Equal(second.Id, ProjectStore.Load(session.CurrentPath!).Id);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void OpenAndSaveUseTheManagedProjectPath()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        ProjectDocument project = ProjectDocument.Create("Open Me");
        string path = store.GetDefaultPath(project.Name);

        try
        {
            Directory.CreateDirectory(root);
            store.Save(project, path);

            ProjectSession session = new(layout);
            ProjectDocument opened = session.Open(path);
            Assert.Equal("Open Me", opened.Name);
            opened.ModifiedUtc = DateTimeOffset.UtcNow.AddMinutes(1);
            session.Save();

            Assert.Equal(Path.GetFullPath(path), session.CurrentPath);
            Assert.Equal(opened.Id, ProjectStore.Load(path).Id);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void OpenRejectsProjectOutsideManagedWorkspace()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string outsidePath = Path.Combine(root, "outside.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(outsidePath, "{}");

            ProjectSession session = new(layout);

            Assert.Throws<UnauthorizedAccessException>(() => session.Open(outsidePath));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void OpenRejectsNonProjectExtensionInsideManagedWorkspace()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string path = Path.Combine(layout.Projects, "not-a-project.txt");

        try
        {
            Directory.CreateDirectory(layout.Projects);
            File.WriteAllText(path, "{}");

            ProjectSession session = new(layout);

            Assert.Throws<NotSupportedException>(() => session.Open(path));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
