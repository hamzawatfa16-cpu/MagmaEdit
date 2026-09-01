using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Tests;

public sealed class MediaProbeServiceTests
{
    [Fact]
    public void ProbeRejectsMissingFile()
    {
        var service = new MediaProbeService();
        string path = Path.Combine(Path.GetTempPath(), $"MagmaEdit-missing-{Guid.NewGuid():N}.mp4");

        Assert.Throws<FileNotFoundException>(() => service.Probe(path));
    }

    [Fact]
    public void ProbeRejectsEmptyPath()
    {
        var service = new MediaProbeService();

        Assert.Throws<ArgumentException>(() => service.Probe(string.Empty));
    }
}
