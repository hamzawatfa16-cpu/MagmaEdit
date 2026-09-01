using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectStoreTests
{
    [Fact]
    public void SaveAndLoadPreservesProjectAndMedia()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        ProjectDocument project = ProjectDocument.Create("My Shorts");
        MediaAsset media = MediaAsset.Create(
            Path.Combine(root, "source.mp4"),
            Path.Combine(layout.Media, "source.mp4"));
        project.Media.Add(media);
        string path = store.GetDefaultPath(project.Name);

        try
        {
            Directory.CreateDirectory(root);
            store.Save(project);
            ProjectDocument loaded = store.Load(path);

            Assert.Equal(project.Id, loaded.Id);
            Assert.Equal(project.Name, loaded.Name);
            Assert.Equal(ProjectDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Single(loaded.Media);
            Assert.Equal(media.Id, loaded.Media[0].Id);
            Assert.Equal(media.LibraryPath, loaded.Media[0].LibraryPath);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void SaveUsesSafeFileNameForInvalidCharacters()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);

        try
        {
            Directory.CreateDirectory(root);
            string path = store.GetDefaultPath("  My: Shorts?  ");

            Assert.EndsWith(Path.Combine("Projects", "My_ Shorts_.magmaedit.json"), path, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void LoadRejectsUnsupportedSchemaVersion()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        string path = Path.Combine(root, "project.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"id\":\"id\",\"name\":\"name\",\"schemaVersion\":999,\"createdUtc\":\"2026-01-01T00:00:00+00:00\",\"modifiedUtc\":\"2026-01-01T00:00:00+00:00\",\"media\":[]}");

            Assert.Throws<InvalidDataException>(() => store.Load(path));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void SaveReplacesExistingProjectAtomically()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        ProjectDocument first = ProjectDocument.Create("Project");
        string path = store.GetDefaultPath(first.Name);

        try
        {
            Directory.CreateDirectory(root);
            store.Save(first, path);

            ProjectDocument second = ProjectDocument.Create("Project");
            second.Media.Add(MediaAsset.Create("C:\\source.mp4", "C:\\library.mp4"));
            store.Save(second, path);

            ProjectDocument loaded = store.Load(path);
            Assert.Equal(second.Id, loaded.Id);
            Assert.Single(loaded.Media);
            Assert.Empty(Directory.GetFiles(layout.Projects, "*.tmp"));
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
