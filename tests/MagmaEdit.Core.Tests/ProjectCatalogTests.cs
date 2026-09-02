using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectCatalogTests
{
    [Fact]
    public void ListReturnsValidAndInvalidProjectsInModifiedOrder()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            ProjectStore store = new(layout);
            ProjectDocument older = ProjectDocument.Create("Older Project");
            ProjectDocument newer = ProjectDocument.Create("Newer Project");

            string olderPath = Path.Combine(layout.Projects, "older.magmaedit.json");
            string newerPath = Path.Combine(layout.Projects, "newer.magmaedit.json");
            store.Save(older, olderPath);
            Thread.Sleep(10);
            store.Save(newer, newerPath);
            File.WriteAllText(Path.Combine(layout.Projects, "broken.magmaedit.json"), "{\"schemaVersion\":\"broken\"}");

            IReadOnlyList<ProjectSummary> summaries = new ProjectCatalog(layout).List();

            Assert.Equal(3, summaries.Count);
            Assert.Equal("Newer Project", summaries[0].Name);
            Assert.True(summaries[0].IsValid);
            Assert.Equal("Older Project", summaries[1].Name);
            Assert.True(summaries[1].IsValid);
            Assert.False(summaries[2].IsValid);
            Assert.NotNull(summaries[2].Error);
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
    public void GetUniqueProjectPathAvoidsExistingNamesAndKeepsProjectsInWorkspace()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            Directory.CreateDirectory(layout.Projects);
            File.WriteAllText(Path.Combine(layout.Projects, "Untitled Project.magmaedit.json"), "placeholder");
            File.WriteAllText(Path.Combine(layout.Projects, "Untitled Project 2.magmaedit.json"), "placeholder");

            string path = new ProjectCatalog(layout).GetUniqueProjectPath("Untitled Project");

            Assert.Equal(Path.Combine(layout.Projects, "Untitled Project 3.magmaedit.json"), path);
            Assert.Equal(layout.Projects, Path.GetDirectoryName(path));
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
