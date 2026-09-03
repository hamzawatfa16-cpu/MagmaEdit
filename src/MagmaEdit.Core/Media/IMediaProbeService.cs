namespace MagmaEdit.Core.Media;

/// <summary>Immutable media facts exposed by the MagmaEdit editor core.</summary>
public sealed record MediaProbeResult(
    TimeSpan Duration,
    bool HasVideo,
    int Width,
    int Height,
    double FramesPerSecond,
    bool HasAudio,
    int SampleRate,
    int Channels,
    bool HasAlpha,
    string VideoCodec,
    string AudioCodec,
    string PixelFormat,
    int BitDepth,
    bool IsHdr,
    bool IsVariableFrameRate,
    string ColorRange,
    string ColorPrimaries,
    string ColorTransfer,
    string ColorSpace,
    string ChromaSubsampling);

/// <summary>Provides media metadata to the MagmaEdit core without exposing a codec implementation.</summary>
public interface IMediaProbeService
{
    MediaProbeResult Probe(string path);
}
