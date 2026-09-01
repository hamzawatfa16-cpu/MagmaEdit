using System.Runtime.InteropServices;
using Sprocket.Core.Timing;
using Sprocket.Media;
using Sprocket.Playback;

namespace MagmaEdit.Core.Media;

/// <summary>A decoded RGBA frame for the desktop preview surface.</summary>
public sealed record DecodedPreviewFrame(int Width, int Height, int RowBytes, byte[] Pixels);

/// <summary>
/// Decodes the first available video frame through Sprocket's real FFmpeg-backed media path.
/// The returned pixels are copied to managed memory so the native decoder can be disposed safely.
/// </summary>
public static class MediaPreviewService
{
    public static async Task<DecodedPreviewFrame> DecodeFirstFrameAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The preview video does not exist.", fullPath);
        }

        await using VideoDecodeRing ring = new(MediaSource.Open(fullPath));
        ring.Start();
        ring.RequestSeek(Timecode.Zero);

        VideoFrame? frame = await ring.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (frame is null)
        {
            throw new InvalidDataException("The video did not produce a preview frame.");
        }

        try
        {
            int bytesPerPixel = 4;
            int destinationStride = checked(frame.Width * bytesPerPixel);
            byte[] pixels = new byte[checked(destinationStride * frame.Height)];

            for (int y = 0; y < frame.Height; y++)
            {
                IntPtr source = IntPtr.Add(frame.Pixels, checked(y * frame.RowBytes));
                Marshal.Copy(source, pixels, checked(y * destinationStride), destinationStride);
            }

            return new DecodedPreviewFrame(frame.Width, frame.Height, destinationStride, pixels);
        }
        finally
        {
            frame.Dispose();
        }
    }
}
