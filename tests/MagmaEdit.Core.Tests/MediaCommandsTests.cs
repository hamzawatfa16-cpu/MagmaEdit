using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Tests;

public sealed class MediaCommandsTests
{
    [Fact]
    public void RenameMediaChangesOnlyProjectDisplayNameAndCanBeReverted()
    {
        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4");
        List<MediaAsset> assets = [asset];
        var command = new RenameMediaAssetCommand(assets, asset.Id, "Edited Clip");

        command.Apply();

        Assert.Equal("Edited Clip", assets[0].FileName);
        Assert.Equal("C:\\source\\clip.mp4", assets[0].SourcePath);
        Assert.Equal("C:\\library\\clip.mp4", assets[0].LibraryPath);

        command.Revert();

        Assert.Equal("clip.mp4", assets[0].FileName);
        Assert.Equal("C:\\source\\clip.mp4", assets[0].SourcePath);
        Assert.Equal("C:\\library\\clip.mp4", assets[0].LibraryPath);
    }

    [Fact]
    public void RenameMediaCanBeExecutedThroughSharedHistory()
    {
        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4");
        List<MediaAsset> assets = [asset];
        var history = new EditHistory();

        history.Execute(new RenameMediaAssetCommand(assets, asset.Id, "Final Cut"));
        Assert.Equal("Final Cut", assets[0].FileName);

        Assert.True(history.Undo());
        Assert.Equal("clip.mp4", assets[0].FileName);

        Assert.True(history.Redo());
        Assert.Equal("Final Cut", assets[0].FileName);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("folder/name")]
    [InlineData("folder\\name")]
    [InlineData("name\nwith newline")]
    public void RenameMediaRejectsUnsafeNames(string name)
    {
        MediaAsset asset = MediaAsset.Create("C:\\source\\clip.mp4", "C:\\library\\clip.mp4");
        List<MediaAsset> assets = [asset];

        Assert.Throws<ArgumentException>(() => new RenameMediaAssetCommand(assets, asset.Id, name));
    }
}
