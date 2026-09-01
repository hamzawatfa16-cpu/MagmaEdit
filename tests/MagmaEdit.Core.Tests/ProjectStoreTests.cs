using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectStoreTests
{
    [Fact]
    public void SaveAndLoadPreservesProjectMediaAndTimeline()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        ProjectDocument project = ProjectDocument.Create("My Shorts");
        MediaAsset media = MediaAsset.Create(
            Path.Combine(root, "source.mp4"),
            Path.Combine(layout.Media, "source.mp4"));
        project.Media.Add(media);
        TimelineTrack track = project.Timeline.AddTrack("Video 1");
        TimelineClip clip = new TimelineEditor(project.Timeline).InsertClip(
            track.Id, media.Id, EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(5));
        string path = store.GetDefaultPath(project.Name);

        try
        {
            Directory.CreateDirectory(root);
            store.Save(project);
            ProjectDocument loaded = ProjectStore.Load(path);

            Assert.Equal(project.Id, loaded.Id);
            Assert.Equal(project.Name, loaded.Name);
            Assert.Equal(ProjectDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Single(loaded.Media);
            Assert.Equal(media.Id, loaded.Media[0].Id);
            Assert.Equal(media.LibraryPath, loaded.Media[0].LibraryPath);
            Assert.Single(loaded.Timeline.Tracks);
            Assert.Single(loaded.Timeline.Tracks[0].Clips);
            Assert.Equal(clip.Id, loaded.Timeline.Tracks[0].Clips[0].Id);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void SaveAndLoadPreservesPublishedState()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        ProjectStore store = new(layout);
        ProjectDocument project = ProjectDocument.Create("Published State");
        MediaAsset media = MediaAsset.Create(
            Path.Combine(root, "published.mp4"),
            Path.Combine(layout.Media, "published.mp4"));
        media.IsPublished = true;
        project.Media.Add(media);
        string path = store.GetDefaultPath(project.Name);

        try
        {
            Directory.CreateDirectory(root);
            store.Save(project);
            ProjectDocument loaded = ProjectStore.Load(path);

            Assert.True(loaded.Media.Single().IsPublished);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void LoadMigratesSchemaVersionOneToCurrentTimelineSchema()
    {
        string root = CreateTemporaryRoot();
        string path = Path.Combine(root, "legacy.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"id\":\"legacy-id\",\"name\":\"Legacy\",\"schemaVersion\":1,\"createdUtc\":\"2026-01-01T00:00:00+00:00\",\"modifiedUtc\":\"2026-01-01T00:00:00+00:00\",\"media\":[]}");

            ProjectDocument loaded = ProjectStore.Load(path);

            Assert.Equal(ProjectDocument.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.Equal(1080, loaded.Timeline.Width);
            Assert.Equal(1920, loaded.Timeline.Height);
            Assert.Empty(loaded.Timeline.Tracks);
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
        string path = Path.Combine(root, "project.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"id\":\"id\",\"name\":\"name\",\"schemaVersion\":999,\"createdUtc\":\"2026-01-01T00:00:00+00:00\",\"modifiedUtc\":\"2026-01-01T00:00:00+00:00\",\"media\":[]}");

            Assert.Throws<InvalidDataException>(() => ProjectStore.Load(path));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void LoadRejectsTimelineClipReferencingMissingMedia()
    {
        string root = CreateTemporaryRoot();
        string path = Path.Combine(root, "invalid.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"id\":\"id\",\"name\":\"name\",\"schemaVersion\":2,\"createdUtc\":\"2026-01-01T00:00:00+00:00\",\"modifiedUtc\":\"2026-01-01T00:00:00+00:00\",\"media\":[],\"timeline\":{\"schemaVersion\":1,\"width\":1080,\"height\":1920,\"frameRateNumerator\":30,\"frameRateDenominator\":1,\"tracks\":[{\"id\":\"track\",\"name\":\"Video 1\",\"clips\":[{\"id\":\"clip\",\"mediaId\":\"missing\",\"timelineStart\":{\"ticks\":0},\"sourceIn\":{\"ticks\":0},\"sourceOut\":{\"ticks\":240000}}]}]}}");

            Assert.Throws<InvalidDataException>(() => ProjectStore.Load(path));
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

            ProjectDocument loaded = ProjectStore.Load(path);
            Assert.Equal(second.Id, loaded.Id);
            Assert.Single(loaded.Media);
            Assert.Empty(Directory.GetFiles(layout.Projects, "*.tmp"));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void LoadRejectsDuplicateMediaIds()
    {
        string root = CreateTemporaryRoot();
        string path = Path.Combine(root, "duplicate-media.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(path, "{\"id\":\"id\",\"name\":\"name\",\"schemaVersion\":2,\"createdUtc\":\"2026-01-01T00:00:00+00:00\",\"modifiedUtc\":\"2026-01-01T00:00:00+00:00\",\"media\":[{\"id\":\"media\",\"fileName\":\"a.mp4\",\"sourcePath\":\"C:\\\\a.mp4\",\"libraryPath\":\"C:\\\\a.mp4\"},{\"id\":\"media\",\"fileName\":\"b.mp4\",\"sourcePath\":\"C:\\\\b.mp4\",\"libraryPath\":\"C:\\\\b.mp4\"}],\"timeline\":{\"schemaVersion\":1,\"width\":1080,\"height\":1920,\"frameRateNumerator\":30,\"frameRateDenominator\":1,\"tracks\":[]}}");

            Assert.Throws<InvalidDataException>(() => ProjectStore.Load(path));
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
