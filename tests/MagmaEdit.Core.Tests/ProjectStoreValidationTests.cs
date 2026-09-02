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
}
