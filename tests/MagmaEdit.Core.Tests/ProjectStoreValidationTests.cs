using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectStoreValidationTests
{
    [Fact]
    public void SaveRejectsTimelineClipThatOverflowsTimelineEndAsInvalidData()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            ProjectDocument project = ProjectDocument.Create("Overflow Test");
            MediaAsset media = MediaAsset.Create(
                Path.Combine(root, "source.mp4"),
                Path.Combine(layout.Media, "source.mp4"));
            project.Media.Add(media);

            TimelineTrack track = project.Timeline.AddTrack("Video 1");
            TimelineClip clip = TimelineClip.Create(
                media.Id,
                new EditTime(long.MaxValue - 1),
                EditTime.Zero,
                new EditTime(2));
            track.Clips.Add(clip);

            ProjectStore store = new(layout);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                store.Save(project));

            Assert.Contains("outside the supported time range", exception.Message, StringComparison.Ordinal);
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
    public void LoadNormalizesMalformedJsonValueShapeToInvalidData()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        string projectPath = Path.Combine(root, "broken.magmaedit.json");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(projectPath, "{\"schemaVersion\":\"broken\"}");

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                ProjectStore.Load(projectPath));

            Assert.Contains("invalid JSON value shape", exception.Message, StringComparison.Ordinal);
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
    public void SaveCreatesBackupOfExistingProjectWithoutLeavingTemporaryFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string projectPath = Path.Combine(layout.Projects, "backup-test.magmaedit.json");

        try
        {
            ProjectStore store = new(layout);
            ProjectDocument first = ProjectDocument.Create("Backup Test");
            store.Save(first, projectPath);

            ProjectDocument second = ProjectDocument.Create("Backup Test Updated");
            store.Save(second, projectPath);

            string backupPath = ProjectStore.GetBackupPath(projectPath);
            Assert.True(File.Exists(projectPath));
            Assert.True(File.Exists(backupPath));
            Assert.Equal(first.Id, ProjectStore.Load(backupPath).Id);
            Assert.Equal(second.Id, ProjectStore.Load(projectPath).Id);
            Assert.Empty(Directory.GetFiles(layout.Projects, "*.tmp", SearchOption.TopDirectoryOnly));
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
