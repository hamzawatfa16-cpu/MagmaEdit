using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Media;

/// <summary>Describes a user-owned video available to an editing project.</summary>
public sealed record MediaAsset(
    string Id,
    string FileName,
    string SourcePath,
    string LibraryPath)
{
    /// <summary>Real media facts captured by Sprocket/FFmpeg when this asset was imported.</summary>
    public MediaProbeResult? Metadata { get; init; }

    public static MediaAsset Create(string sourcePath, string libraryPath, MediaProbeResult? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        string fullLibraryPath = Path.GetFullPath(libraryPath);

        return new MediaAsset(
            Guid.NewGuid().ToString("N"),
            Path.GetFileName(fullLibraryPath),
            fullSourcePath,
            fullLibraryPath)
        {
            Metadata = metadata
        };
    }
}

/// <summary>Imports videos by copying them into the MagmaEdit media library and probing the copied media.</summary>
public sealed class MediaImportService
{
    private readonly WorkspaceLayout _workspace;

    public MediaImportService(WorkspaceLayout workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public MediaAsset Import(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException("The selected video does not exist.", fullSourcePath);
        }

        if (!VideoFileTypes.IsSupported(fullSourcePath))
        {
            throw new NotSupportedException("The selected file type is not supported as a video.");
        }

        Directory.CreateDirectory(_workspace.Media);
        string destination = GetUniqueDestination(fullSourcePath);
        File.Copy(fullSourcePath, destination, overwrite: false);

        try
        {
            MediaProbeResult metadata = MediaProbeService.Probe(destination);
            if (!metadata.HasVideo)
            {
                throw new InvalidDataException("The selected file does not contain a usable video stream.");
            }

            return MediaAsset.Create(fullSourcePath, destination, metadata);
        }
        catch
        {
            TryDeleteImportedFile(destination);
            throw;
        }
    }

    private static void TryDeleteImportedFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Preserve the original import/probe failure. Cleanup is best effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the original import/probe failure. Cleanup is best effort.
        }
    }

    private string GetUniqueDestination(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);
        string destination = Path.Combine(_workspace.Media, fileName);

        if (!File.Exists(destination))
        {
            return destination;
        }

        string name = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        int index = 2;

        while (true)
        {
            destination = Path.Combine(_workspace.Media, $"{name} ({index}){extension}");
            if (!File.Exists(destination))
            {
                return destination;
            }

            index++;
        }
    }
}
