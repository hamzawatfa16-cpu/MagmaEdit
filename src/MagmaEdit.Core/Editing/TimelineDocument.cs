namespace MagmaEdit.Core.Editing;

/// <summary>Integer timeline time. 240000 ticks/second keeps common video rates exact enough for editor math.</summary>
public readonly record struct EditTime(long Ticks) : IComparable<EditTime>
{
    public const long TicksPerSecond = 240000;
    public static EditTime Zero => new(0);
    public static EditTime FromSeconds(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds must be finite and non-negative.");
        if (seconds > (double)long.MaxValue / TicksPerSecond)
            throw new ArgumentOutOfRangeException(nameof(seconds), "Seconds exceed the supported timeline range.");

        return new((long)Math.Round(seconds * TicksPerSecond));
    }
    public double ToSeconds() => (double)Ticks / TicksPerSecond;
    public static EditTime operator +(EditTime left, EditTime right) => new(checked(left.Ticks + right.Ticks));
    public static EditTime operator -(EditTime left, EditTime right) => new(checked(left.Ticks - right.Ticks));
    public static bool operator <(EditTime left, EditTime right) => left.Ticks < right.Ticks;
    public static bool operator >(EditTime left, EditTime right) => left.Ticks > right.Ticks;
    public static bool operator <=(EditTime left, EditTime right) => left.Ticks <= right.Ticks;
    public static bool operator >=(EditTime left, EditTime right) => left.Ticks >= right.Ticks;
    public int CompareTo(EditTime other) => Ticks.CompareTo(other.Ticks);
}

public sealed class TimelineClip
{
    public required string Id { get; init; }
    public required string MediaId { get; init; }
    public EditTime TimelineStart { get; set; }
    public EditTime SourceIn { get; set; }
    public EditTime SourceOut { get; set; }
    public EditTime Duration => new(SourceOut.Ticks - SourceIn.Ticks);
    public EditTime TimelineEnd => TimelineStart + Duration;

    public static TimelineClip Create(string mediaId, EditTime timelineStart, EditTime sourceIn, EditTime sourceOut)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaId);
        if (timelineStart.Ticks < 0 || sourceIn.Ticks < 0 || sourceOut <= sourceIn)
            throw new ArgumentOutOfRangeException(nameof(sourceOut), "Timeline and source ranges must be non-negative and have positive duration.");

        return new TimelineClip
        {
            Id = Guid.NewGuid().ToString("N"),
            MediaId = mediaId,
            TimelineStart = timelineStart,
            SourceIn = sourceIn,
            SourceOut = sourceOut
        };
    }

    public TimelineClip Clone(string? id = null) => new()
    {
        Id = id ?? Id,
        MediaId = MediaId,
        TimelineStart = TimelineStart,
        SourceIn = SourceIn,
        SourceOut = SourceOut
    };
}

public sealed class TimelineTrack
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public List<TimelineClip> Clips { get; init; } = [];

    public static TimelineTrack Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new TimelineTrack { Id = Guid.NewGuid().ToString("N"), Name = name.Trim() };
    }
}

public sealed class TimelineDocument
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int Width { get; init; } = 1080;
    public int Height { get; init; } = 1920;
    public int FrameRateNumerator { get; init; } = 30;
    public int FrameRateDenominator { get; init; } = 1;
    public List<TimelineTrack> Tracks { get; init; } = [];

    public static TimelineDocument CreateDefault() => new();

    public TimelineTrack AddTrack(string name)
    {
        TimelineTrack track = TimelineTrack.Create(name);
        Tracks.Add(track);
        return track;
    }

    public TimelineTrack RemoveTrack(string trackId)
    {
        TimelineTrack track = GetTrack(trackId);
        Tracks.Remove(track);
        return track;
    }

    public TimelineTrack GetTrack(string trackId) =>
        Tracks.FirstOrDefault(track => string.Equals(track.Id, trackId, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Timeline track '{trackId}' does not exist.");
}

/// <summary>Non-destructive timeline operations. All source media remains untouched.</summary>
public sealed class TimelineEditor
{
    private readonly TimelineDocument _timeline;

    public TimelineEditor(TimelineDocument timeline)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
    }

    public TimelineDocument Timeline => _timeline;

    public TimelineClip InsertClip(string trackId, string mediaId, EditTime timelineStart, EditTime sourceIn, EditTime sourceOut)
    {
        TimelineClip clip = TimelineClip.Create(mediaId, timelineStart, sourceIn, sourceOut);
        InsertExistingClip(trackId, clip);
        return clip;
    }

    public void InsertExistingClip(string trackId, TimelineClip clip, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(clip);
        TimelineTrack track = _timeline.GetTrack(trackId);
        EnsureNoOverlap(track, clip, clip.Id);

        if (track.Clips.Any(existing => string.Equals(existing.Id, clip.Id, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Timeline clip '{clip.Id}' already exists.");

        if (index is { } requestedIndex && requestedIndex >= 0 && requestedIndex <= track.Clips.Count)
            track.Clips.Insert(requestedIndex, clip);
        else
            track.Clips.Add(clip);

        Sort(track);
    }

    public TimelineClip RemoveClip(string trackId, string clipId)
    {
        TimelineTrack track = _timeline.GetTrack(trackId);
        TimelineClip clip = FindClip(track, clipId);
        track.Clips.Remove(clip);
        return clip;
    }

    public void TrimClip(string trackId, string clipId, EditTime sourceIn, EditTime sourceOut)
    {
        TimelineTrack track = _timeline.GetTrack(trackId);
        TimelineClip clip = FindClip(track, clipId);
        TimelineClip candidate = clip.Clone();
        candidate.SourceIn = sourceIn;
        candidate.SourceOut = sourceOut;
        ValidateRange(candidate);
        EnsureNoOverlap(track, candidate, clip.Id);
        clip.SourceIn = sourceIn;
        clip.SourceOut = sourceOut;
        Sort(track);
    }

    public void MoveClip(string trackId, string clipId, EditTime timelineStart)
    {
        TimelineTrack track = _timeline.GetTrack(trackId);
        TimelineClip clip = FindClip(track, clipId);
        TimelineClip candidate = clip.Clone();
        candidate.TimelineStart = timelineStart;
        EnsureNoOverlap(track, candidate, clip.Id);
        clip.TimelineStart = timelineStart;
        Sort(track);
    }

    public (TimelineClip Left, TimelineClip Right) SplitClip(string trackId, string clipId, EditTime timelinePosition)
    {
        TimelineTrack track = _timeline.GetTrack(trackId);
        TimelineClip clip = FindClip(track, clipId);
        if (timelinePosition <= clip.TimelineStart || timelinePosition >= clip.TimelineEnd)
            throw new ArgumentOutOfRangeException(nameof(timelinePosition), "Split position must be inside the clip.");

        EditTime sourceSplit = clip.SourceIn + new EditTime(timelinePosition.Ticks - clip.TimelineStart.Ticks);
        TimelineClip left = clip.Clone();
        left.SourceOut = sourceSplit;
        TimelineClip right = TimelineClip.Create(clip.MediaId, timelinePosition, sourceSplit, clip.SourceOut);

        int index = track.Clips.IndexOf(clip);
        track.Clips.RemoveAt(index);
        track.Clips.Insert(index, right);
        track.Clips.Insert(index, left);
        return (left, right);
    }

    public int GetClipIndex(string trackId, string clipId)
    {
        TimelineTrack track = _timeline.GetTrack(trackId);
        return track.Clips.FindIndex(clip => string.Equals(clip.Id, clipId, StringComparison.Ordinal));
    }

    private static TimelineClip FindClip(TimelineTrack track, string clipId) =>
        track.Clips.FirstOrDefault(clip => string.Equals(clip.Id, clipId, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Timeline clip '{clipId}' does not exist.");

    private static void EnsureNoOverlap(TimelineTrack track, TimelineClip candidate, string? ignoreClipId = null)
    {
        ValidateRange(candidate);
        if (track.Clips.Any(existing =>
            !string.Equals(existing.Id, ignoreClipId, StringComparison.Ordinal) &&
            candidate.TimelineStart < existing.TimelineEnd && existing.TimelineStart < candidate.TimelineEnd))
        {
            throw new InvalidOperationException("The clip would overlap another clip on the same track.");
        }
    }

    private static void ValidateRange(TimelineClip clip)
    {
        if (clip.TimelineStart.Ticks < 0 || clip.SourceIn.Ticks < 0 || clip.SourceOut <= clip.SourceIn)
            throw new ArgumentOutOfRangeException(nameof(clip), "Timeline and source ranges are invalid.");

        long duration = clip.SourceOut.Ticks - clip.SourceIn.Ticks;
        if (clip.TimelineStart.Ticks > long.MaxValue - duration)
            throw new ArgumentOutOfRangeException(nameof(clip), "Timeline range exceeds the supported time range.");
    }

    private static void Sort(TimelineTrack track) =>
        track.Clips.Sort((left, right) => left.TimelineStart.CompareTo(right.TimelineStart));
}
