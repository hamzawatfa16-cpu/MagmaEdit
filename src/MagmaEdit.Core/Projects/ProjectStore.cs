using System.Text;
using System.Text.Json;
using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;
using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Projects;

/// <summary>Persists project documents as versioned JSON files inside the local project workspace.</summary>
public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false
    };

    private readonly WorkspaceLayout _workspace;

    public ProjectStore(WorkspaceLayout workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public string GetDefaultPath(string projectName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        Directory.CreateDirectory(_workspace.Projects);
        return Path.Combine(_workspace.Projects, $"{SanitizeFileName(projectName)}.magmaedit.json");
    }

    public static string GetBackupPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return $"{Path.GetFullPath(path)}.bak";
    }

    public void Save(ProjectDocument project, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        Validate(project);

        string destination = Path.GetFullPath(path ?? GetDefaultPath(project.Name));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        project.ModifiedUtc = DateTimeOffset.UtcNow;
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        string backup = GetBackupPath(destination);

        try
        {
            if (File.Exists(destination))
            {
                File.Copy(destination, backup, overwrite: true);
            }

            string json = JsonSerializer.Serialize(project, JsonOptions);
            byte[] payload = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            using (FileStream stream = new(
                temporary,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.WriteThrough | FileOptions.SequentialScan,
                    BufferSize = 64 * 1024
                }))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public static ProjectDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The project file does not exist.", fullPath);
        }

        string json = File.ReadAllText(fullPath);
        ProjectDocument? project = DeserializeAndMigrate(json);
        if (project is null)
        {
            throw new InvalidDataException("The project file is empty or invalid.");
        }

        Validate(project);
        return project;
    }

    private static ProjectDocument? DeserializeAndMigrate(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            int schemaVersion = document.RootElement.TryGetProperty("schemaVersion", out JsonElement value)
                ? value.GetInt32()
                : 1;

            ProjectDocument? project = JsonSerializer.Deserialize<ProjectDocument>(json, JsonOptions);
            if (project is null)
            {
                return null;
            }

            return schemaVersion switch
            {
                1 => new ProjectDocument
                {
                    Id = project.Id,
                    Name = project.Name,
                    SchemaVersion = ProjectDocument.CurrentSchemaVersion,
                    CreatedUtc = project.CreatedUtc,
                    ModifiedUtc = project.ModifiedUtc,
                    Media = project.Media,
                    Timeline = TimelineDocument.CreateDefault()
                },
                ProjectDocument.CurrentSchemaVersion => project,
                _ => throw new InvalidDataException($"Unsupported project schema version: {schemaVersion}.")
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The project file contains invalid JSON.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The project file contains an invalid JSON value shape.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The project file contains a malformed value.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("The project file contains an out-of-range value.", exception);
        }
    }

    private static void Validate(ProjectDocument project)
    {
        if (string.IsNullOrWhiteSpace(project.Id))
        {
            throw new InvalidDataException("The project is missing its identifier.");
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            throw new InvalidDataException("The project is missing its name.");
        }

        if (project.SchemaVersion != ProjectDocument.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported project schema version: {project.SchemaVersion}.");
        }

        if (project.CreatedUtc == default)
        {
            throw new InvalidDataException("The project is missing its creation timestamp.");
        }

        if (project.Timeline is null || project.Timeline.Width <= 0 || project.Timeline.Height <= 0 ||
            project.Timeline.FrameRateNumerator <= 0 || project.Timeline.FrameRateDenominator <= 0)
        {
            throw new InvalidDataException("The project contains invalid timeline settings.");
        }

        HashSet<string> mediaIds = new(StringComparer.Ordinal);
        foreach (MediaAsset media in project.Media)
        {
            if (string.IsNullOrWhiteSpace(media.Id) ||
                string.IsNullOrWhiteSpace(media.FileName) ||
                string.IsNullOrWhiteSpace(media.SourcePath) ||
                string.IsNullOrWhiteSpace(media.LibraryPath) ||
                !mediaIds.Add(media.Id))
            {
                throw new InvalidDataException("The project contains an invalid or duplicate media asset.");
            }
        }

        HashSet<string> trackIds = new(StringComparer.Ordinal);
        HashSet<string> clipIds = new(StringComparer.Ordinal);

        foreach (TimelineTrack track in project.Timeline.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.Id) || string.IsNullOrWhiteSpace(track.Name) || !trackIds.Add(track.Id))
                throw new InvalidDataException("The project contains an invalid or duplicate timeline track.");

            EditTime? previousEnd = null;
            foreach (TimelineClip clip in track.Clips.OrderBy(item => item.TimelineStart))
            {
                if (string.IsNullOrWhiteSpace(clip.Id) || !clipIds.Add(clip.Id) ||
                    string.IsNullOrWhiteSpace(clip.MediaId) || !mediaIds.Contains(clip.MediaId) ||
                    clip.TimelineStart.Ticks < 0 || clip.SourceIn.Ticks < 0 || clip.SourceOut <= clip.SourceIn)
                {
                    throw new InvalidDataException("The project contains an invalid timeline clip.");
                }

                EditTime timelineEnd;
                try
                {
                    timelineEnd = clip.TimelineEnd;
                }
                catch (OverflowException exception)
                {
                    throw new InvalidDataException("The project contains a timeline clip outside the supported time range.", exception);
                }

                if (previousEnd is { } end && clip.TimelineStart < end)
                    throw new InvalidDataException("The project contains overlapping clips on a timeline track.");

                previousEnd = timelineEnd;
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        HashSet<char> invalidSet = invalid.ToHashSet();
        string sanitized = new(name.Trim().Select(character => invalidSet.Contains(character) ? '_' : character).ToArray());
        sanitized = sanitized.TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled" : sanitized;
    }
}
