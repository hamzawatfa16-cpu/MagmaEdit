using MagmaEdit.Core.Workspace;

namespace MagmaEdit.Core.Projects;

/// <summary>Owns the active project and enforces the managed workspace boundary for project files.</summary>
public sealed class ProjectSession
{
    private const string ProjectExtension = ".magmaedit.json";

    private readonly WorkspaceLayout _workspace;
    private readonly ProjectStore _store;
    private readonly ProjectCatalog _catalog;

    public ProjectSession(WorkspaceLayout workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _store = new ProjectStore(_workspace);
        _catalog = new ProjectCatalog(_workspace);
    }

    public ProjectDocument? CurrentProject { get; private set; }

    public string? CurrentPath { get; private set; }

    public bool HasProject => CurrentProject is not null && CurrentPath is not null;

    /// <summary>Creates and persists a new project with a collision-free filename in the managed Projects folder.</summary>
    public ProjectDocument CreateNew(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string path = _catalog.GetUniqueProjectPath(name);
        ProjectDocument project = ProjectDocument.Create(name);
        _store.Save(project, path);
        CurrentProject = project;
        CurrentPath = path;
        return project;
    }

    /// <summary>Opens an existing managed project file and makes it the active project.</summary>
    public ProjectDocument Open(string path)
    {
        string fullPath = ValidateProjectPath(path);
        ProjectDocument project = ProjectStore.Load(fullPath);
        CurrentProject = project;
        CurrentPath = fullPath;
        return project;
    }

    /// <summary>Persists the active project using its current project path.</summary>
    public void Save()
    {
        if (!HasProject)
        {
            throw new InvalidOperationException("There is no active project to save.");
        }

        _store.Save(CurrentProject!, CurrentPath!);
    }

    /// <summary>Returns a safe project path for a new project without changing the active session.</summary>
    public string GetNewProjectPath(string preferredName) => _catalog.GetUniqueProjectPath(preferredName);

    private string ValidateProjectPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string fullPath = Path.GetFullPath(path);
        string projectsRoot = Path.GetFullPath(_workspace.Projects)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(projectsRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Project files must remain inside the managed Projects folder.");
        }

        if (!fullPath.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Project files must use the {ProjectExtension} extension.");
        }

        return fullPath;
    }
}
