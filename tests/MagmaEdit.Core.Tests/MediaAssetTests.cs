using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Tests;

public sealed class MediaAssetTests
{
    [Fact]
    public void CreateStoresImportedMetadata()
    {
        MediaProbeResult metadata = new(
            Duration: TimeSpan.FromSeconds(12),
            HasVideo: true,
            Width: 1080,
            Height: 1920,
            FramesPerSecond: 30,
            HasAudio: true,
            SampleRate: 48000,
            Channels: 2,
            HasAlpha: false,
            VideoCodec: "h264",
            AudioCodec: "aac",
            PixelFormat: "yuv420p",
            BitDepth: 8,
            IsHdr: false,
            IsVariableFrameRate: false,
            ColorRange: "tv",
            ColorPrimaries: "bt709",
            ColorTransfer: "bt709",
            ColorSpace: "bt709",
            ChromaSubsampling: "420");

        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4", metadata);

        Assert.NotNull(asset.Metadata);
        Assert.Equal(TimeSpan.FromSeconds(12), asset.Metadata!.Duration);
        Assert.Equal(1080, asset.Metadata.Width);
        Assert.Equal(1920, asset.Metadata.Height);
        Assert.Equal(30, asset.Metadata.FramesPerSecond);
        Assert.Equal("h264", asset.Metadata.VideoCodec);
    }
}
