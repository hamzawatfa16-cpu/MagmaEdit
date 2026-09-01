using System.Text.Json;
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

    public void Save(ProjectDocument project, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        Validate(project);

        string destination = Path.GetFullPath(path ?? GetDefaultPath(project.Name));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        project.ModifiedUtc = DateTimeOffset.UtcNow;
        string temporary = $"{destination}.{Guid.NewGuid():N}.tmp";

        try
        {
            string json = JsonSerializer.Serialize(project, JsonOptions);
            File.WriteAllText(temporary, json);
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
        ProjectDocument? project = JsonSerializer.Deserialize<ProjectDocument>(json, JsonOptions);
        if (project is null)
        {
            throw new InvalidDataException("The project file is empty or invalid.");
        }

        Validate(project);
        return project;
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
