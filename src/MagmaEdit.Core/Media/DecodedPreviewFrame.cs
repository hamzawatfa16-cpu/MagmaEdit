namespace MagmaEdit.Core.Media;

/// <summary>A decoded RGBA frame for the desktop preview surface.</summary>
public sealed record DecodedPreviewFrame(
    int Width,
    int Height,
    int RowBytes,
    byte[] Pixels,
    TimeSpan PresentationTime = default);
