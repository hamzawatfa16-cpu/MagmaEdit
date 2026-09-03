using MagmaEdit.Core.Media;
using Sprocket.Core.Model;
using Sprocket.Media;
using Sprocket.Media.Native;

namespace MagmaEdit.Media.Sprocket;

/// <summary>Sprocket-backed media probing adapter. This is the only project boundary that knows Sprocket types for probing.</summary>
public sealed class SprocketMediaProbeService : IMediaProbeService
{
    public MediaProbeResult Probe(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("The media file does not exist.", fullPath);

        try
        {
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
        catch (FFmpegException exception)
        {
            throw new InvalidDataException($"The media file could not be decoded or probed: {exception.Message}", exception);
        }
    }
}
