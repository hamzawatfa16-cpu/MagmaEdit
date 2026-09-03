namespace MagmaEdit.Core.Media;

/// <summary>Core-owned abstraction for reading media metadata.</summary>
public interface IMediaProbeService
{
    MediaProbeResult Probe(string path);
}
