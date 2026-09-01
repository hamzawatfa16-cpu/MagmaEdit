using MagmaEdit.Core.Editing;
using MagmaEdit.Core.Media;

namespace MagmaEdit.Core.Projects;

/// <summary>Versioned, JSON-safe project data. Runtime services and native handles are intentionally excluded.</summary>
public sealed class ProjectDocument
{
    public const int CurrentSchemaVersion = 2;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset ModifiedUtc { get; set; }

    public List<MediaAsset> Media { get; init; } = [];

    public TimelineDocument Timeline { get; init; } = TimelineDocument.CreateDefault();

    public static ProjectDocument Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new ProjectDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name.Trim(),
            SchemaVersion = CurrentSchemaVersion,
            CreatedUtc = now,
            ModifiedUtc = now,
            Timeline = TimelineDocument.CreateDefault()
        };
    }
}
