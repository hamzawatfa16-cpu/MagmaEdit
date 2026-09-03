using System.Runtime.InteropServices;
using MagmaEdit.Core.Media;
using Sprocket.Core.Model;
using Sprocket.Core.Timing;
using Sprocket.Media;

namespace MagmaEdit.Media.Sprocket;

/// <summary>Reads decoded frames from a real FFmpeg-backed Sprocket decoder.</summary>
public sealed class MediaPlaybackSession : IAsyncDisposable
{
    private readonly VideoDecodeRing _ring;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private bool _disposed;

    public MediaPlaybackSession(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The preview video does not exist.", fullPath);
        }

        MediaSource source = MediaSource.Open(fullPath);
        Info = ToProbeResult(source.Info);
        _ring = new VideoDecodeRing(source);
        _ring.Start();
        _ring.RequestSeek(Timecode.Zero);
    }

    /// <summary>Stream metadata exposed using MagmaEdit-owned values.</summary>
    public MediaProbeResult Info { get; }

    public async Task<DecodedPreviewFrame?> ReadNextFrameAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            VideoFrame? frame = await _ring.ReadAsync(cancellationToken).ConfigureAwait(false);
            return frame is null ? null : CopyFrame(frame);
        }
        finally
        {
            _readGate.Release();
        }
    }

    public async Task<DecodedPreviewFrame?> SeekAndReadFrameAsync(
        TimeSpan target,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (target < TimeSpan.Zero || !double.IsFinite(target.TotalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(target), "The preview seek position must be finite and non-negative.");
        }

        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ring.RequestSeek(Timecode.FromSeconds(target.TotalSeconds));
            VideoFrame? frame = await _ring.ReadAsync(cancellationToken).ConfigureAwait(false);
            return frame is null ? null : CopyFrame(frame);
        }
        finally
        {
            _readGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _ring.DisposeAsync().ConfigureAwait(false);
        _readGate.Dispose();
    }

    private static DecodedPreviewFrame CopyFrame(VideoFrame frame)
    {
        try
        {
            int destinationStride = checked(frame.Width * 4);
            byte[] pixels = new byte[checked(destinationStride * frame.Height)];

            for (int y = 0; y < frame.Height; y++)
            {
                IntPtr source = IntPtr.Add(frame.Pixels, checked(y * frame.RowBytes));
                Marshal.Copy(source, pixels, checked(y * destinationStride), destinationStride);
            }

            return new DecodedPreviewFrame(
                frame.Width,
                frame.Height,
                destinationStride,
                pixels,
                TimeSpan.FromSeconds(frame.Pts.ToSeconds()));
        }
        finally
        {
            frame.Dispose();
        }
    }

    private static MediaProbeResult ToProbeResult(ProbedMediaInfo info)
    {
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

/// <summary>Decodes the first video frame for the static preview surface.</summary>
public static class MediaPreviewService
{
    public static async Task<DecodedPreviewFrame> DecodeFirstFrameAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        await using var session = new MediaPlaybackSession(path);
        DecodedPreviewFrame? frame = await session.ReadNextFrameAsync(cancellationToken).ConfigureAwait(false);
        return frame ?? throw new InvalidDataException("The video did not produce a preview frame.");
    }
}
