using System.Diagnostics;
using System.Globalization;
using MagmaEdit.Core.Editing;

namespace MagmaEdit.Core.Export;

/// <summary>One non-destructive source segment that will become part of an exported video.</summary>
public sealed record VideoExportSegment(string SourcePath, EditTime SourceIn, EditTime Duration, bool HasAudio);

/// <summary>Builds a deterministic FFmpeg command for the current vertical single-track export path.</summary>
public static class VideoExportCommandBuilder
{
    public static ProcessStartInfo Create(
        string ffmpegPath,
        IReadOnlyList<VideoExportSegment> segments,
        string outputPath,
        int width = 1080,
        int height = 1920,
        int frameRate = 30)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (segments.Count == 0)
            throw new ArgumentException("At least one export segment is required.", nameof(segments));
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Export dimensions must be positive.");
        if (frameRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRate), "Frame rate must be positive.");

        ProcessStartInfo startInfo = new()
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        List<string> filterParts = new(segments.Count * 2 + 1);
        for (int index = 0; index < segments.Count; index++)
        {
            VideoExportSegment segment = segments[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(segment.SourcePath);
            if (segment.SourceIn < EditTime.Zero || segment.Duration <= EditTime.Zero)
                throw new ArgumentOutOfRangeException(nameof(segments), "Export segments must have non-negative source positions and positive durations.");

            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(FormatSeconds(segment.SourceIn));
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add(FormatSeconds(segment.Duration));
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(Path.GetFullPath(segment.SourcePath));

            filterParts.Add(
                $"[{index}:v]scale={width}:{height}:force_original_aspect_ratio=decrease," +
                $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2,setsar=1,fps={frameRate}," +
                "format=yuv420p,setpts=PTS-STARTPTS[v" + index + "]");

            string audioInput = segment.HasAudio
                ? $"[{index}:a]aresample=48000,aformat=sample_fmts=fltp:sample_rates=48000:channel_layouts=stereo," +
                  $"atrim=duration={FormatSeconds(segment.Duration)},asetpts=PTS-STARTPTS[a{index}]"
                : $"anullsrc=r=48000:cl=stereo,atrim=duration={FormatSeconds(segment.Duration)},asetpts=PTS-STARTPTS[a{index}]";
            filterParts.Add(audioInput);
        }

        string videoInputs = string.Concat(Enumerable.Range(0, segments.Count).Select(index => $"[v{index}][a{index}]"));
        filterParts.Add($"{videoInputs}concat=n={segments.Count}:v=1:a=1[v][a]");

        startInfo.ArgumentList.Add("-filter_complex");
        startInfo.ArgumentList.Add(string.Join(";", filterParts));
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[v]");
        startInfo.ArgumentList.Add("-map");
        startInfo.ArgumentList.Add("[a]");
        startInfo.ArgumentList.Add("-c:v");
        startInfo.ArgumentList.Add("libx264");
        startInfo.ArgumentList.Add("-preset");
        startInfo.ArgumentList.Add("medium");
        startInfo.ArgumentList.Add("-crf");
        startInfo.ArgumentList.Add("18");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("aac");
        startInfo.ArgumentList.Add("-b:a");
        startInfo.ArgumentList.Add("192k");
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("48000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add("-pix_fmt");
        startInfo.ArgumentList.Add("yuv420p");
        startInfo.ArgumentList.Add("-movflags");
        startInfo.ArgumentList.Add("+faststart");
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(Path.GetFullPath(outputPath));

        return startInfo;
    }

    private static string FormatSeconds(EditTime value) =>
        value.ToSeconds().ToString("0.######", CultureInfo.InvariantCulture);
}
