namespace MagmaEdit.Integration;

/// <summary>Read-only snapshot exposed to automation clients without exposing mutable editor objects.</summary>
public sealed record EditorProjectState(
    string ProjectId,
    string ProjectName,
    int SchemaVersion,
    int TimelineWidth,
    int TimelineHeight,
    int FrameRateNumerator,
    int FrameRateDenominator,
    int MediaCount,
    IReadOnlyList<EditorMediaState> Media,
    IReadOnlyList<EditorTrackState> Tracks,
    int UndoCount,
    int RedoCount);

public sealed record EditorMediaState(
    string Id,
    string FileName,
    string SourcePath,
    string LibraryPath,
    bool IsPublished,
    double? DurationSeconds,
    int? Width,
    int? Height,
    double? FrameRate);

public sealed record EditorTrackState(
    string Id,
    string Name,
    IReadOnlyList<EditorClipState> Clips);

public sealed record EditorClipState(
    string Id,
    string MediaId,
    long TimelineStartTicks,
    long SourceInTicks,
    long SourceOutTicks,
    long DurationTicks);
