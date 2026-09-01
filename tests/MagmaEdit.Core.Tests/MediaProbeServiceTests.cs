using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Tests;

public sealed class MediaProbeServiceTests
{
    [Fact]
    public void ProbeRejectsMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"MagmaEdit-missing-{Guid.NewGuid():N}.mp4");

        Assert.Throws<FileNotFoundException>(() => MediaProbeService.Probe(path));
    }

    [Fact]
    public void ProbeRejectsEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => MediaProbeService.Probe(string.Empty));
    }
}
