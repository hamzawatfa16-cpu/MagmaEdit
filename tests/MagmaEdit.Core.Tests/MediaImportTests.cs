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
    public void SupportedVideoContainersAreAcceptedWhenSignatureMatches(string fileName)
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, fileName);

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(source, CreateMinimalContainerSignature(Path.GetExtension(fileName)));

            Assert.True(VideoFileTypes.IsSupported(source));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
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
    public void SupportedExtensionWithInvalidContainerSignatureIsRejected()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "fake.mp4");

        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(source, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);

            Assert.False(VideoFileTypes.IsSupported(source));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void ImportCopiesVideoWithoutModifyingSource()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "source.mp4");
        WorkspaceLayout layout = WorkspaceLayout.Create(Path.Combine(root, "Content Creation"));
        byte[] content = CreateMinimalContainerSignature(".mp4");

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
        byte[] content = CreateMinimalContainerSignature(".mp4");

        try
        {
            Directory.CreateDirectory(layout.Media);
            File.WriteAllBytes(source, content);
            File.WriteAllBytes(Path.Combine(layout.Media, "clip.mp4"), [9, 9, 9]);

            MediaAsset asset = new MediaImportService(layout).Import(source);

            Assert.Equal("clip (2).mp4", asset.FileName);
            Assert.Equal([9, 9, 9], File.ReadAllBytes(Path.Combine(layout.Media, "clip.mp4")));
            Assert.Equal(content, File.ReadAllBytes(asset.LibraryPath));
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
            File.WriteAllText(source, "not an image format");

            Assert.Throws<NotSupportedException>(() =>
                new MediaImportService(layout).Import(source));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public void ImportRejectsFakeVideoWithSupportedExtension()
    {
        string root = CreateTemporaryRoot();
        string source = Path.Combine(root, "fake.mp4");
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

    private static byte[] CreateMinimalContainerSignature(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => [0, 0, 0, 16, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0],
            ".mov" => [0, 0, 0, 16, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'q', (byte)'t',  (byte)' ', (byte)' ', 0, 0, 0, 0],
            ".webm" or ".mkv" => [0x1A, 0x45, 0xDF, 0xA3, 0, 0, 0, 0],
            ".avi" => [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'A', (byte)'V', (byte)'I', (byte)' '],
            _ => throw new ArgumentOutOfRangeException(nameof(extension))
        };

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
