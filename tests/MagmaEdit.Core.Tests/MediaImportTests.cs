using MagmaEdit.Core.Media;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Tests;

public sealed class MediaImportTests
{
    [Theory]
    [InlineData("clip.mp4")]
    [InlineData("clip.MOV")]
    [InlineData("clip.mkv")]
    [InlineData("clip.webm")]
    [InlineData("clip.avi")]
    [InlineData("clip.m4v")]
    public void SupportedVideoExtensionsAreAccepted(string fileName)
    {
        Assert.True(VideoFileTypes.IsSupported(fileName));
    }

    [Theory]
    [InlineData("image.png")]
    [InlineData("audio.mp3")]
    [InlineData("document.txt")]
    [InlineData("project")]
    public void UnsupportedExtensionsAreRejected(string fileName)
    {
        Assert.False(VideoFileTypes.IsSupported(fileName));
    }

    [Fact]
    public void ImportCopiesVideoWithoutModifyingSource()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "source.mp4");
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        byte[] content = [1, 2, 3, 4, 5];

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(source, content);

            MediaAsset asset = new MediaImportService(layout).Import(source);

            Assert.True(File.Exists(source));
            Assert.True(File.Exists(asset.LibraryPath));
            Assert.Equal(content, File.ReadAllBytes(asset.LibraryPath));
            Assert.Equal("source.mp4", asset.FileName);
            Assert.Equal(Path.GetFullPath(source), asset.SourcePath);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void ImportCreatesUniqueNameWhenLibraryAlreadyContainsFile()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "clip.mp4");
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            Directory.CreateDirectory(layout.Media);
            File.WriteAllText(source, "source");
            File.WriteAllText(Path.Combine(layout.Media, "clip.mp4"), "existing");

            MediaAsset asset = new MediaImportService(layout).Import(source);

            Assert.Equal("clip (2).mp4", asset.FileName);
            Assert.Equal("existing", File.ReadAllText(Path.Combine(layout.Media, "clip.mp4")));
            Assert.Equal("source", File.ReadAllText(asset.LibraryPath));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void ImportRejectsMissingSource()
    {
        string root = CreateTemporaryRoot();
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            Directory.CreateDirectory(root);

            Assert.Throws<FileNotFoundException>(() =>
                new MediaImportService(layout).Import(Path.Combine(root, "missing.mp4")));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void ImportRejectsUnsupportedFileType()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "image.png");
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(source, "not a video");

            Assert.Throws<NotSupportedException>(() =>
                new MediaImportService(layout).Import(source));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), "MagmaEditTests", Guid.NewGuid().ToString("N"));

    private static void DeleteTemporaryRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
