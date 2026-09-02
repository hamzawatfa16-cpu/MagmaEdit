namespace MagmaEdit.Core.Projects;

/// <summary>Validates and normalizes project display names before they enter persisted project data.</summary>
public static class ProjectNameRules
{
    public const int MaxLength = 255;

    public static string Normalize(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Project names cannot be empty.", nameof(name));
        }

        if (trimmed.Length > MaxLength)
        {
            throw new ArgumentException($"Project names cannot exceed {MaxLength} characters.", nameof(name));
        }

        if (trimmed is "." or ".." || trimmed.Any(char.IsControl) || trimmed.Contains('/') || trimmed.Contains('\\'))
        {
            throw new ArgumentException("Project names must be normal display names.", nameof(name));
        }

        return trimmed;
    }
}
