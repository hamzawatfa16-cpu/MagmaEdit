using MagmaEdit.Media.Sprocket;

namespace MagmaEdit.App;

/// <summary>App-layer preview facade that keeps gallery code independent from the concrete media adapter location.</summary>
internal static class MediaPreviewService
{
    public static Task<DecodedPreviewFrame> DecodeFirstFrameAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        MagmaEdit.Media.Sprocket.MediaPreviewService.DecodeFirstFrameAsync(path, cancellationToken);
}
