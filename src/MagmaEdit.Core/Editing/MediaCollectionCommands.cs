using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Editing;

/// <summary>Adds a media asset to the project media collection through the shared edit history.</summary>
public sealed class AddMediaAssetCommand : IEditCommand
{
    private readonly IList<MediaAsset> _assets;
    private readonly MediaAsset _asset;

    public AddMediaAssetCommand(IList<MediaAsset> assets, MediaAsset asset)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        ArgumentException.ThrowIfNullOrWhiteSpace(_asset.Id);
    }

    public string Label => "Add media";

    public void Apply()
    {
        if (_assets.Any(asset => string.Equals(asset.Id, _asset.Id, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Media asset '{_asset.Id}' already exists.");
        }

        _assets.Add(_asset);
    }

    public void Revert()
    {
        int index = FindMediaIndex();
        _assets.RemoveAt(index);
    }

    private int FindMediaIndex()
    {
        for (int index = 0; index < _assets.Count; index++)
        {
            if (string.Equals(_assets[index].Id, _asset.Id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new KeyNotFoundException($"Media asset '{_asset.Id}' was not found.");
    }
}

/// <summary>Removes a media asset from the project collection without deleting the managed media file.</summary>
public sealed class RemoveMediaAssetCommand : IEditCommand
{
    private readonly IList<MediaAsset> _assets;
    private readonly string _mediaId;
    private readonly MediaAsset _asset;
    private readonly int _index;

    public RemoveMediaAssetCommand(IList<MediaAsset> assets, string mediaId)
    {
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);

        int index = FindMediaIndex(mediaId);
        _mediaId = mediaId;
        _asset = _assets[index];
        _index = index;
    }

    public string Label => "Remove media";

    public void Apply()
    {
        int index = FindMediaIndex(_mediaId);
        _assets.RemoveAt(index);
    }

    public void Revert()
    {
        if (_assets.Any(asset => string.Equals(asset.Id, _mediaId, StringComparison.Ordinal)))
        {
            return;
        }

        _assets.Insert(Math.Min(_index, _assets.Count), _asset);
    }

    private int FindMediaIndex(string mediaId)
    {
        for (int index = 0; index < _assets.Count; index++)
        {
            if (string.Equals(_assets[index].Id, mediaId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        throw new KeyNotFoundException($"Media asset '{mediaId}' was not found.");
    }
}
