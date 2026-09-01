namespace MagmaEdit.Core.Media;

/// <summary>Supported video file extensions for the first MagmaEdit import pipeline.</summary>
public static class VideoFileTypes
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".m4v",
        ".webm",
        ".mkv",
        ".avi"
    };

    public static bool IsSupported(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }
}
