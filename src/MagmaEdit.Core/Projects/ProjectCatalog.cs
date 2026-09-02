using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Projects;

/// <summary>Discovers and summarizes the projects stored in the local MagmaEdit workspace.</summary>
public sealed class ProjectCatalog
{
    private const string ProjectExtension = ".magmaedit.json";
    private readonly WorkspaceLayout _workspace;

    public ProjectCatalog(WorkspaceLayout workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>Lists project files in the workspace without traversing outside the Projects folder.</summary>
    public IReadOnlyList<ProjectSummary> List()
    {
        Directory.CreateDirectory(_workspace.Projects);

        List<ProjectSummary> summaries = [];
        foreach (string path in Directory.EnumerateFiles(_workspace.Projects, $"*{ProjectExtension}", SearchOption.TopDirectoryOnly))
        {
            summaries.Add(ReadSummary(path));
        }

        return summaries
            .OrderByDescending(summary => summary.ModifiedUtc)
            .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Returns a deterministic unused project path inside the managed Projects folder.</summary>
    public string GetUniqueProjectPath(string preferredName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredName);
        Directory.CreateDirectory(_workspace.Projects);

        string baseName = SanitizeFileName(preferredName);
        string candidate = Path.Combine(_workspace.Projects, $"{baseName}{ProjectExtension}");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (int index = 2; index <= int.MaxValue; index++)
        {
            candidate = Path.Combine(_workspace.Projects, $"{baseName} {index}{ProjectExtension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("No unused project filename is available in the managed Projects folder.");
    }

    private static ProjectSummary ReadSummary(string path)
    {
        string fullPath = Path.GetFullPath(path);
        try
        {
            ProjectDocument project = ProjectStore.Load(fullPath);
            DateTimeOffset modifiedUtc = project.ModifiedUtc == default
                ? File.GetLastWriteTimeUtc(fullPath)
                : project.ModifiedUtc;
            return new ProjectSummary(fullPath, project.Name, modifiedUtc, true, null);
        }
        catch (Exception exception) when (
            exception is InvalidDataException or
            IOException or
            UnauthorizedAccessException)
        {
            DateTimeOffset modifiedUtc = File.Exists(fullPath)
                ? File.GetLastWriteTimeUtc(fullPath)
                : DateTimeOffset.MinValue;
            return new ProjectSummary(fullPath, Path.GetFileNameWithoutExtension(fullPath), modifiedUtc, false, exception.Message);
        }
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        HashSet<char> invalidSet = invalid.ToHashSet();
        string sanitized = new(name.Trim().Select(character => invalidSet.Contains(character) ? '_' : character).ToArray());
        sanitized = sanitized.TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? "Untitled Project" : sanitized;
    }
}

/// <summary>Display-safe information about a project file, including corrupt/unreadable files.</summary>
public sealed record ProjectSummary(
    string Path,
    string Name,
    DateTimeOffset ModifiedUtc,
    bool IsValid,
    string? Error);
