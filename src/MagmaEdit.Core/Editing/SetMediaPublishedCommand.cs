using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Editing;

/// <summary>Changes media publication state through the shared undoable edit history.</summary>
public sealed class SetMediaPublishedCommand : IEditCommand
{
    private readonly IList<MediaAsset> _assets;
    private readonly string _mediaId;
    private readonly bool _newValue;
    private bool _originalValue;
    private bool _captured;

    public SetMediaPublishedCommand(IList<MediaAsset> assets, string mediaId, bool isPublished)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);
        _mediaId = mediaId;
        _newValue = isPublished;
    }

    public string Label => "Set media publication state";

    public void Apply()
    {
        int index = FindMediaIndex();
        MediaAsset current = _assets[index];
        if (!_captured)
        {
            _originalValue = current.IsPublished;
            _captured = true;
        }

        if (current.IsPublished == _newValue)
        {
            throw new InvalidOperationException("The media already has that publication state.");
        }

        current.IsPublished = _newValue;
    }

    public void Revert()
    {
        if (!_captured)
        {
            throw new InvalidOperationException("The publication command has not been applied.");
        }

        int index = FindMediaIndex();
        _assets[index].IsPublished = _originalValue;
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
}
