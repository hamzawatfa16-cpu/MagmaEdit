using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Editing;

/// <summary>Renames a media item's project display name without touching the managed media file.</summary>
public sealed class RenameMediaAssetCommand : IEditCommand
{
    private readonly IList<MediaAsset> _assets;
    private readonly string _mediaId;
    private readonly string _newFileName;
    private string? _originalFileName;

    public RenameMediaAssetCommand(IList<MediaAsset> assets, string mediaId, string newFileName)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);
        _mediaId = mediaId;
        _newFileName = ValidateFileName(newFileName);
    }

    public string Label => "Rename media";

    public void Apply()
    {
        int index = FindMediaIndex();
        MediaAsset current = _assets[index];

        _originalFileName ??= current.FileName;
        if (string.Equals(current.FileName, _newFileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The media already has that name.");
        }

        _assets[index] = current with { FileName = _newFileName };
    }

    public void Revert()
    {
        if (_originalFileName is null)
        {
            throw new InvalidOperationException("The rename command has not been applied.");
        }

        int index = FindMediaIndex();
        MediaAsset current = _assets[index];
        _assets[index] = current with { FileName = _originalFileName };
    }

    private int FindMediaIndex()
    {
        for (int index = 0; index < _assets.Count; index++)
        {
            if (string.Equals(_assets[index].Id, _mediaId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new KeyNotFoundException($"Media asset '{_mediaId}' was not found.");
    }

    private static string ValidateFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        string trimmed = fileName.Trim();
        if (trimmed.Length > 255)
        {
            throw new ArgumentException("Media names cannot exceed 255 characters.", nameof(fileName));
        }

        if (trimmed is "." or ".." || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException("Media names must be normal display names.", nameof(fileName));
        }

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            if (trimmed.Contains(invalidCharacter))
            {
                throw new ArgumentException("Media names cannot contain invalid file-name characters.", nameof(fileName));
            }
        }

        return trimmed;
    }
}
