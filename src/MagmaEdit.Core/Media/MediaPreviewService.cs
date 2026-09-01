using System.Runtime.InteropServices;
using Sprocket.Core.Model;
using Sprocket.Core.Timing;
using Sprocket.Media;

namespace MagmaEdit.Core.Media;

/// <summary>A decoded RGBA frame for the desktop preview surface.</summary>
public sealed record DecodedPreviewFrame(
    int Width,
    int Height,
    int RowBytes,
    byte[] Pixels,
    Timecode Pts = default);

/// <summary>
/// Reads decoded frames from a real FFmpeg-backed Sprocket decoder while serializing the single-consumer
/// read path. The session stays open between frames so playback does not reopen the video for every frame.
/// </summary>
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
        Info = source.Info;
        _ring = new VideoDecodeRing(source);
        _ring.Start();
        _ring.RequestSeek(Timecode.Zero);
    }

    /// <summary>Stream metadata obtained from the same decoder used for playback.</summary>
    public ProbedMediaInfo Info { get; }

    /// <summary>Reads the next frame in presentation order, or null at end of stream.</summary>
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

    /// <summary>Requests a frame-accurate seek and returns the first decoded frame at/after the target.</summary>
    public async Task<DecodedPreviewFrame?> SeekAndReadFrameAsync(
        Timecode target,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ring.RequestSeek(target);
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

            return new DecodedPreviewFrame(frame.Width, frame.Height, destinationStride, pixels, frame.Pts);
        }
        finally
        {
            frame.Dispose();
        }
    }
}

/// <summary>
/// Compatibility helper for callers that only need a single decoded first frame.
/// </summary>
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
