using MagmaEdit.Core.Editing;
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

    [Fact]
    public void RenameChangesOnlyProjectDisplayNameAndCanBeReverted()
    {
        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4");
        var assets = new List<MediaAsset> { asset };
        var command = new RenameMediaAssetCommand(assets, asset.Id, "My Short.mp4");

        command.Apply();

        Assert.Equal("My Short.mp4", assets[0].FileName);
        Assert.Equal(asset.Id, assets[0].Id);
        Assert.Equal(asset.SourcePath, assets[0].SourcePath);
        Assert.Equal(asset.LibraryPath, assets[0].LibraryPath);

        command.Revert();

        Assert.Equal("clip.mp4", assets[0].FileName);
        Assert.Equal(asset.SourcePath, assets[0].SourcePath);
        Assert.Equal(asset.LibraryPath, assets[0].LibraryPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("bad/name.mp4")]
    [InlineData("bad\\name.mp4")]
    public void RenameRejectsInvalidDisplayNames(string name)
    {
        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4");
        var assets = new List<MediaAsset> { asset };

        Assert.ThrowsAny<ArgumentException>(() => new RenameMediaAssetCommand(assets, asset.Id, name));
    }
}
