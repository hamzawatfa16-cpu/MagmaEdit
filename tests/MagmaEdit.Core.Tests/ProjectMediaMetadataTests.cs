using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class ProjectMediaMetadataTests
{
    [Fact]
    public void MediaMetadataSurvivesProjectRoundTrip()
    {
        string root = Path.Combine(Path.GetTempPath(), $"MagmaEdit-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            WorkspaceLayout workspace = WorkspaceLayout.Create(root);
            var store = new ProjectStore(workspace);
            ProjectDocument project = ProjectDocument.Create("Metadata Test");
            MediaProbeResult metadata = new(
                TimeSpan.FromSeconds(8), true, 1080, 1920, 30, true, 48000, 2,
                false, "h264", "aac", "yuv420p", 8, false, false,
                "tv", "bt709", "bt709", "bt709", "420");

            project.Media.Add(MediaAsset.Create(
                Path.Combine(root, "source.mp4"),
                Path.Combine(workspace.Media, "source.mp4"),
                metadata));

            string projectPath = store.GetDefaultPath(project.Name);
            store.Save(project, projectPath);
            ProjectDocument loaded = ProjectStore.Load(projectPath);

            MediaAsset loadedAsset = Assert.Single(loaded.Media);
            Assert.NotNull(loadedAsset.Metadata);
            Assert.Equal(metadata.Duration, loadedAsset.Metadata!.Duration);
            Assert.Equal(metadata.Width, loadedAsset.Metadata.Width);
            Assert.Equal(metadata.Height, loadedAsset.Metadata.Height);
            Assert.Equal(metadata.VideoCodec, loadedAsset.Metadata.VideoCodec);
            Assert.Equal(metadata.AudioCodec, loadedAsset.Metadata.AudioCodec);
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
    public void PublishedStateSurvivesProjectRoundTrip()
    {
        string root = Path.Combine(Path.GetTempPath(), $"MagmaEdit-project-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            WorkspaceLayout workspace = WorkspaceLayout.Create(root);
            var store = new ProjectStore(workspace);
            ProjectDocument project = ProjectDocument.Create("Published State Test");
            MediaAsset asset = MediaAsset.Create(
                Path.Combine(root, "published.mp4"),
                Path.Combine(workspace.Media, "published.mp4"));
            asset.IsPublished = true;
            project.Media.Add(asset);

            string projectPath = store.GetDefaultPath(project.Name);
            store.Save(project, projectPath);
            ProjectDocument loaded = ProjectStore.Load(projectPath);

            MediaAsset loadedAsset = Assert.Single(loaded.Media);
            Assert.True(loadedAsset.IsPublished);
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
