using Sprocket.Core.Model;
using Sprocket.Media;

namespace MagmaEdit.Core.Media;

/// <summary>Immutable media facts exposed by the MagmaEdit core without leaking Sprocket/native runtime types.</summary>
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

/// <summary>Opens the real Sprocket Media layer to probe an imported source. No mock metadata is generated.</summary>
public static class MediaProbeService
{
    public static MediaProbeResult Probe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The media file does not exist.", fullPath);

        ProbedMediaInfo info = MediaSource.ProbeInfo(fullPath);
        double framesPerSecond = info.FrameRate.Den > 0
            ? (double)info.FrameRate.Num / info.FrameRate.Den
            : 0d;

        return new MediaProbeResult(
            Duration: TimeSpan.FromSeconds(info.Duration.ToSeconds()),
            HasVideo: info.HasVideo,
            Width: info.Width,
            Height: info.Height,
            FramesPerSecond: framesPerSecond,
            HasAudio: info.HasAudio,
            SampleRate: info.SampleRate,
            Channels: info.Channels,
            HasAlpha: info.HasAlpha,
            VideoCodec: info.VideoCodec,
            AudioCodec: info.AudioCodec,
            PixelFormat: info.PixelFormatName,
            BitDepth: info.BitDepth,
            IsHdr: info.IsHdr,
            IsVariableFrameRate: info.IsVariableFrameRate,
            ColorRange: info.ColorRange,
            ColorPrimaries: info.ColorPrimaries,
            ColorTransfer: info.ColorTransfer,
            ColorSpace: info.ColorSpace,
            ChromaSubsampling: info.ChromaSubsampling);
    }
}
