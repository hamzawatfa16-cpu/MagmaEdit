namespace MagmaEdit.Core.Media;

/// <summary>Recognizes supported video containers by extension and container signature.</summary>
public static class VideoFileTypes
{
    public static bool IsSupported(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return TryGetContainer(path, out _);
    }

    public static bool TryGetContainer(string path, out VideoContainer container)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        container = default;

        string extension = Path.GetExtension(path);
        try
        {
            if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".mov", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase))
            {
                using FileStream stream = File.OpenRead(path);
                if (stream.Length < 12)
                    return false;

                Span<byte> header = stackalloc byte[12];
                if (stream.Read(header) != header.Length ||
                    header[4] != (byte)'f' || header[5] != (byte)'t' ||
                    header[6] != (byte)'y' || header[7] != (byte)'p')
                {
                    return false;
                }

                string majorBrand = new string(new[]
                {
                    (char)header[8], (char)header[9], (char)header[10], (char)header[11]
                });
                container = extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                    ? VideoContainer.QuickTime
                    : extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
                        ? VideoContainer.M4v
                        : majorBrand.Equals("qt  ", StringComparison.OrdinalIgnoreCase)
                            ? VideoContainer.QuickTime
                            : VideoContainer.Mp4;
                return true;
            }

            using FileStream file = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[12];
            int count = file.Read(signature);
            if (count < 4)
                return false;

            if (signature[..4].SequenceEqual(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }))
            {
                container = extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                    ? VideoContainer.WebM
                    : extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
                        ? VideoContainer.Matroska
                        : default;
                return container != default;
            }

            if (count >= 12 && signature[..4].SequenceEqual("RIFF"u8) && signature[8..12].SequenceEqual("AVI "u8))
            {
                container = VideoContainer.Avi;
                return extension.Equals(".avi", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }
}

public enum VideoContainer
{
    Mp4 = 1,
    QuickTime,
    M4v,
    WebM,
    Matroska,
    Avi
}
