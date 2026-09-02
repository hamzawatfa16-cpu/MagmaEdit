using System.Diagnostics;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Export;

namespace MagmaEdit.Core.Tests;

public sealed class VideoExportCommandBuilderTests
{
    [Fact]
    public void CreateBuildsVerticalConcatCommandWithAudio()
    {
        VideoExportSegment first = new(
            "C:\\Media\\first clip.mp4",
            EditTime.FromSeconds(1.25),
            EditTime.FromSeconds(2.5),
            HasAudio: true);
        VideoExportSegment second = new(
            "C:\\Media\\second.mp4",
            EditTime.Zero,
            EditTime.FromSeconds(3),
            HasAudio: false);

        ProcessStartInfo startInfo = VideoExportCommandBuilder.Create(
            "C:\\Tools\\ffmpeg.exe",
            [first, second],
            "C:\\Exports\\short.mp4");

        Assert.Equal("C:\\Tools\\ffmpeg.exe", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Contains("-filter_complex", startInfo.ArgumentList);
        string filter = startInfo.ArgumentList.Single(argument => argument.Contains("concat=n=2", StringComparison.Ordinal));
        Assert.Contains("[v0][a0][v1][a1]concat=n=2:v=1:a=1[v][a]", filter);
        Assert.Contains("[0:a]aresample=48000", filter);
        Assert.Contains("anullsrc=r=48000:cl=stereo", filter);
        Assert.Contains("-c:v", startInfo.ArgumentList);
        Assert.Contains("libx264", startInfo.ArgumentList);
        Assert.Contains("-c:a", startInfo.ArgumentList);
        Assert.Contains("aac", startInfo.ArgumentList);
        Assert.Contains("-b:a", startInfo.ArgumentList);
        Assert.Contains("192k", startInfo.ArgumentList);
        Assert.Contains("-movflags", startInfo.ArgumentList);
        Assert.Contains("+faststart", startInfo.ArgumentList);
        Assert.Equal(Path.GetFullPath("C:\\Exports\\short.mp4"), startInfo.ArgumentList[^1]);
        Assert.Contains("C:\\Media\\first clip.mp4", startInfo.ArgumentList);
        Assert.Contains("1.25", startInfo.ArgumentList);
        Assert.Contains("2.5", startInfo.ArgumentList);
    }

    [Fact]
    public void CreateRejectsEmptySegments()
    {
        Assert.Throws<ArgumentException>(() => VideoExportCommandBuilder.Create(
            "ffmpeg.exe",
            [],
            "output.mp4"));
    }

    [Fact]
    public void CreateRejectsInvalidSegmentDuration()
    {
        VideoExportSegment segment = new(
            "video.mp4",
            EditTime.Zero,
            EditTime.Zero,
            HasAudio: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => VideoExportCommandBuilder.Create(
            "ffmpeg.exe",
            [segment],
            "output.mp4"));
    }
}
