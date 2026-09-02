using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Export;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class VideoExportServiceTests
{
    [Fact]
    public async Task ExportRejectsTimelineGapBeforeStartingFfmpeg()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string firstPath = Path.Combine(layout.Media, "first.mp4");
        string secondPath = Path.Combine(layout.Media, "second.mp4");

        try
        {
            Directory.CreateDirectory(layout.Media);
            File.WriteAllBytes(firstPath, [1]);
            File.WriteAllBytes(secondPath, [1]);

            ProjectDocument project = ProjectDocument.Create("Export Gap");
            project.Media.Add(CreateMedia(firstPath, 5));
            project.Media.Add(CreateMedia(secondPath, 5));
            TimelineTrack track = project.Timeline.AddTrack("Video 1");
            TimelineEditor editor = new(project.Timeline);
            editor.InsertClip(track.Id, project.Media[0].Id, EditTime.Zero, EditTime.Zero, EditTime.FromSeconds(5));
            editor.InsertClip(track.Id, project.Media[1].Id, EditTime.FromSeconds(6), EditTime.Zero, EditTime.FromSeconds(5));

            VideoExportService service = new("missing-ffmpeg.exe");
            await Assert.ThrowsAsync<NotSupportedException>(() => service.ExportAsync(
                project,
                Path.Combine(layout.Exports, "gap.mp4")));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ExportRejectsClipStartingAfterTimelineZero()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string mediaPath = Path.Combine(layout.Media, "clip.mp4");

        try
        {
            Directory.CreateDirectory(layout.Media);
            File.WriteAllBytes(mediaPath, [1]);

            ProjectDocument project = ProjectDocument.Create("Export Start");
            project.Media.Add(CreateMedia(mediaPath, 5));
            TimelineTrack track = project.Timeline.AddTrack("Video 1");
            new TimelineEditor(project.Timeline).InsertClip(
                track.Id,
                project.Media[0].Id,
                EditTime.FromSeconds(1),
                EditTime.Zero,
                EditTime.FromSeconds(5));

            VideoExportService service = new("missing-ffmpeg.exe");
            await Assert.ThrowsAsync<NotSupportedException>(() => service.ExportAsync(
                project,
                Path.Combine(layout.Exports, "start.mp4")));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task ExportRejectsClipBeyondMediaDuration()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        string mediaPath = Path.Combine(layout.Media, "clip.mp4");

        try
        {
            Directory.CreateDirectory(layout.Media);
            File.WriteAllBytes(mediaPath, [1]);

            ProjectDocument project = ProjectDocument.Create("Export Bounds");
            project.Media.Add(CreateMedia(mediaPath, 5));
            TimelineTrack track = project.Timeline.AddTrack("Video 1");
            TimelineClip clip = new TimelineEditor(project.Timeline).InsertClip(
                track.Id,
                project.Media[0].Id,
                EditTime.Zero,
                EditTime.Zero,
                EditTime.FromSeconds(4));
            clip.SourceOut = EditTime.FromSeconds(6);

            VideoExportService service = new("missing-ffmpeg.exe");
            await Assert.ThrowsAsync<InvalidDataException>(() => service.ExportAsync(
                project,
                Path.Combine(layout.Exports, "bounds.mp4")));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static MediaAsset CreateMedia(string libraryPath, double durationSeconds)
    {
        MediaProbeResult metadata = new(
            TimeSpan.FromSeconds(durationSeconds),
            HasVideo: true,
            Width: 1080,
            Height: 1920,
            FramesPerSecond: 30,
            HasAudio: false,
            SampleRate: 0,
            Channels: 0,
            HasAlpha: false,
            VideoCodec: "h264",
            AudioCodec: string.Empty,
            PixelFormat: "yuv420p",
            BitDepth: 8,
            IsHdr: false,
            IsVariableFrameRate: false,
            ColorRange: "tv",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            ChromaSubsampling: "4:2:0");

        return MediaAsset.Create(libraryPath, libraryPath, metadata);
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
