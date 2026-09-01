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

    [Fact]
    public void ProbeNormalizesMalformedContainerToInvalidData()
    {
        string root = Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(root, "malformed.mp4");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(path,
            [
                0, 0, 0, 16,
                (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                (byte)'i', (byte)'s', (byte)'o', (byte)'m',
                0, 0, 0, 0
            ]);

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() => MediaProbeService.Probe(path));
            Assert.Contains("could not be decoded or probed", exception.Message, StringComparison.OrdinalIgnoreCase);
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
