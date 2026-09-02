using System.Diagnostics;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Projects;

namespace MagmaEdit.Core.Export;

/// <summary>Exports the supported MagmaEdit timeline to a real 1080×1920 H.264/AAC MP4.</summary>
public sealed class VideoExportService
{
    private readonly string? _explicitFfmpegPath;

    public VideoExportService(string? ffmpegPath = null)
    {
        _explicitFfmpegPath = ffmpegPath;
    }

    public async Task ExportAsync(
        ProjectDocument project,
        string outputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        IReadOnlyList<VideoExportSegment> segments = BuildSegments(project);
        string destination = Path.GetFullPath(outputPath);
        string ffmpegPath = ResolveFfmpegPath();

        if (segments.Any(segment => string.Equals(
            Path.GetFullPath(segment.SourcePath),
            destination,
            StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("The export destination cannot replace a source media file.");
        }

        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The export destination does not contain a valid directory.");

        Directory.CreateDirectory(directory);
        string temporary = $"{destination}.partial-{Guid.NewGuid():N}.mp4";

        try
        {
            ProcessStartInfo startInfo = VideoExportCommandBuilder.Create(
                ffmpegPath,
                segments,
                temporary,
                project.Timeline.Width,
                project.Timeline.Height,
                GetTimelineFrameRate(project.Timeline));

            using Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
                throw new InvalidOperationException("FFmpeg could not be started.");

            progress?.Report(0d);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                string detail = string.IsNullOrWhiteSpace(error)
                    ? "FFmpeg returned a non-zero exit code."
                    : error.Trim();
                throw new InvalidDataException($"Video export failed: {detail}");
            }

            FileInfo file = new(temporary);
            if (!file.Exists || file.Length <= 0)
                throw new InvalidDataException("FFmpeg completed without creating a valid output file.");

            File.Move(temporary, destination, overwrite: true);
            progress?.Report(1d);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }
    }

    private static List<VideoExportSegment> BuildSegments(ProjectDocument project)
    {
        TimelineTrack[] tracks = project.Timeline.Tracks.Where(track => track.Clips.Count > 0).ToArray();
        if (tracks.Length == 0)
            throw new InvalidOperationException("The timeline does not contain any clips to export.");
        if (tracks.Length != 1)
            throw new NotSupportedException("Export currently supports one populated video track at a time.");

        TimelineTrack track = tracks[0];
        TimelineClip[] orderedClips = track.Clips.OrderBy(item => item.TimelineStart).ToArray();
        if (orderedClips[0].TimelineStart != EditTime.Zero)
            throw new NotSupportedException("Export currently requires the first video clip to start at timeline time zero.");

        List<VideoExportSegment> segments = new(orderedClips.Length);
        EditTime expectedStart = EditTime.Zero;
        foreach (TimelineClip clip in orderedClips)
        {
            if (clip.TimelineStart != expectedStart)
                throw new NotSupportedException("Export currently requires a contiguous single-track timeline with no gaps.");

            MediaAsset media = project.Media.FirstOrDefault(asset =>
                string.Equals(asset.Id, clip.MediaId, StringComparison.Ordinal))
                ?? throw new InvalidDataException($"Timeline clip '{clip.Id}' references missing media '{clip.MediaId}'.");

            if (media.Metadata is not { } metadata || !metadata.HasVideo || metadata.Duration <= TimeSpan.Zero)
                throw new InvalidDataException($"Media '{media.FileName}' does not have usable video metadata for export.");
            if (!File.Exists(media.LibraryPath))
                throw new FileNotFoundException("The timeline media file does not exist.", media.LibraryPath);

            EditTime mediaDuration = EditTime.FromSeconds(metadata.Duration.TotalSeconds);
            if (clip.SourceOut > mediaDuration)
            {
                throw new InvalidDataException(
                    $"Clip '{clip.Id}' uses source time beyond the media duration.");
            }

            segments.Add(new VideoExportSegment(media.LibraryPath, clip.SourceIn, clip.Duration, metadata.HasAudio));
            expectedStart = clip.TimelineEnd;
        }

        return segments;
    }

    private string ResolveFfmpegPath()
    {
        string? candidate = _explicitFfmpegPath;
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = Environment.GetEnvironmentVariable("MAGMAEDIT_FFMPEG_PATH");
        if (string.IsNullOrWhiteSpace(candidate))
            candidate = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");

        if (File.Exists(candidate))
            return Path.GetFullPath(candidate);

        throw new FileNotFoundException(
            "MagmaEdit could not find its bundled FFmpeg executable. Reinstall MagmaEdit or configure MAGMAEDIT_FFMPEG_PATH.",
            candidate);
    }

    private static int GetTimelineFrameRate(TimelineDocument timeline)
    {
        if (timeline.FrameRateNumerator <= 0 || timeline.FrameRateDenominator <= 0)
            throw new InvalidDataException("The timeline contains an invalid frame rate.");

        double frameRate = (double)timeline.FrameRateNumerator / timeline.FrameRateDenominator;
        if (!double.IsFinite(frameRate) || frameRate < 1d || frameRate > int.MaxValue)
            throw new InvalidDataException("The timeline contains an unsupported frame rate.");

        return Math.Max(1, (int)Math.Round(frameRate));
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already exited.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Preserve the original export failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original export failure.
        }
    }
}
