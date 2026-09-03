using System.Globalization;

namespace MagmaEdit.Integration;

/// <summary>Describes the commands exposed through the vendor-neutral integration boundary.</summary>
public sealed record EditorCommandDefinition(
    EditorCommandKind Command,
    EditorCommandCapability Capability,
    IReadOnlyList<string> RequiredParameters);

public enum EditorCommandCapability
{
    TimelineEditing,
    MediaManagement,
    History
}

/// <summary>Provides deterministic command metadata and boundary validation for automation clients.</summary>
public static class EditorCommandCatalog
{
    private static readonly Dictionary<EditorCommandKind, EditorCommandDefinition> Definitions =
        new()
        {
            [EditorCommandKind.AddTrack] = new(
                EditorCommandKind.AddTrack,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.Name)]),
            [EditorCommandKind.RemoveTrack] = new(
                EditorCommandKind.RemoveTrack,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.TrackId)]),
            [EditorCommandKind.InsertClip] = new(
                EditorCommandKind.InsertClip,
                EditorCommandCapability.TimelineEditing,
                [
                    nameof(EditorCommandRequest.TrackId),
                    nameof(EditorCommandRequest.MediaId),
                    nameof(EditorCommandRequest.TimelinePositionTicks),
                    nameof(EditorCommandRequest.SourceInTicks),
                    nameof(EditorCommandRequest.SourceOutTicks)
                ]),
            [EditorCommandKind.DuplicateClip] = new(
                EditorCommandKind.DuplicateClip,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.TrackId), nameof(EditorCommandRequest.ClipId)]),
            [EditorCommandKind.RemoveClip] = new(
                EditorCommandKind.RemoveClip,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.TrackId), nameof(EditorCommandRequest.ClipId)]),
            [EditorCommandKind.TrimClip] = new(
                EditorCommandKind.TrimClip,
                EditorCommandCapability.TimelineEditing,
                [
                    nameof(EditorCommandRequest.TrackId),
                    nameof(EditorCommandRequest.ClipId),
                    nameof(EditorCommandRequest.SourceInTicks),
                    nameof(EditorCommandRequest.SourceOutTicks)
                ]),
            [EditorCommandKind.MoveClip] = new(
                EditorCommandKind.MoveClip,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.TrackId), nameof(EditorCommandRequest.ClipId), nameof(EditorCommandRequest.TimelinePositionTicks)]),
            [EditorCommandKind.MoveClipToTrack] = new(
                EditorCommandKind.MoveClipToTrack,
                EditorCommandCapability.TimelineEditing,
                [
                    nameof(EditorCommandRequest.TrackId),
                    nameof(EditorCommandRequest.DestinationTrackId),
                    nameof(EditorCommandRequest.ClipId),
                    nameof(EditorCommandRequest.TimelinePositionTicks)
                ]),
            [EditorCommandKind.SplitClip] = new(
                EditorCommandKind.SplitClip,
                EditorCommandCapability.TimelineEditing,
                [nameof(EditorCommandRequest.TrackId), nameof(EditorCommandRequest.ClipId), nameof(EditorCommandRequest.TimelinePositionTicks)]),
            [EditorCommandKind.RenameMedia] = new(
                EditorCommandKind.RenameMedia,
                EditorCommandCapability.MediaManagement,
                [nameof(EditorCommandRequest.MediaId), nameof(EditorCommandRequest.Name)]),
            [EditorCommandKind.SetMediaPublished] = new(
                EditorCommandKind.SetMediaPublished,
                EditorCommandCapability.MediaManagement,
                [nameof(EditorCommandRequest.MediaId), nameof(EditorCommandRequest.IsPublished)]),
            [EditorCommandKind.Undo] = new(
                EditorCommandKind.Undo,
                EditorCommandCapability.History,
                []),
            [EditorCommandKind.Redo] = new(
                EditorCommandKind.Redo,
                EditorCommandCapability.History,
                [])
        };

    public static IReadOnlyCollection<EditorCommandDefinition> DefinitionsInOrder { get; } =
        Enum.GetValues<EditorCommandKind>()
            .Select(command => Definitions[command])
            .ToArray();

    public static bool TryGetDefinition(EditorCommandKind command, out EditorCommandDefinition definition) =>
        Definitions.TryGetValue(command, out definition!);

    public static bool TryValidate(EditorCommandRequest request, out string message)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!TryGetDefinition(request.Command, out EditorCommandDefinition? definition))
        {
            message = "Unsupported editor command.";
            return false;
        }

        foreach (string parameter in definition.RequiredParameters)
        {
            string? value = parameter switch
            {
                nameof(EditorCommandRequest.TrackId) => request.TrackId,
                nameof(EditorCommandRequest.DestinationTrackId) => request.DestinationTrackId,
                nameof(EditorCommandRequest.ClipId) => request.ClipId,
                nameof(EditorCommandRequest.MediaId) => request.MediaId,
                nameof(EditorCommandRequest.Name) => request.Name,
                nameof(EditorCommandRequest.SourceInTicks) => request.SourceInTicks,
                nameof(EditorCommandRequest.SourceOutTicks) => request.SourceOutTicks,
                nameof(EditorCommandRequest.TimelinePositionTicks) => request.TimelinePositionTicks,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(value) && parameter != nameof(EditorCommandRequest.IsPublished))
            {
                message = $"Required parameter '{parameter}' is missing.";
                return false;
            }

            if (parameter == nameof(EditorCommandRequest.IsPublished) && request.IsPublished is null)
            {
                message = "Required parameter 'IsPublished' is missing.";
                return false;
            }
        }

        if (!ValidateTimeParameters(request, out message))
        {
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool ValidateTimeParameters(EditorCommandRequest request, out string message)
    {
        switch (request.Command)
        {
            case EditorCommandKind.InsertClip:
            case EditorCommandKind.TrimClip:
                if (!TryParseTicks(request.SourceInTicks, nameof(EditorCommandRequest.SourceInTicks), out long sourceIn, out message) ||
                    !TryParseTicks(request.SourceOutTicks, nameof(EditorCommandRequest.SourceOutTicks), out long sourceOut, out message))
                {
                    return false;
                }

                if (sourceOut <= sourceIn)
                {
                    message = "SourceOutTicks must be greater than SourceInTicks.";
                    return false;
                }

                if (request.Command == EditorCommandKind.InsertClip &&
                    !TryParseTicks(request.TimelinePositionTicks, nameof(EditorCommandRequest.TimelinePositionTicks), out _, out message))
                {
                    return false;
                }

                return true;

            case EditorCommandKind.MoveClip:
            case EditorCommandKind.MoveClipToTrack:
            case EditorCommandKind.SplitClip:
                return TryParseTicks(request.TimelinePositionTicks, nameof(EditorCommandRequest.TimelinePositionTicks), out _, out message);

            default:
                message = string.Empty;
                return true;
        }
    }

    private static bool TryParseTicks(string? value, string parameterName, out long ticks, out string message)
    {
        ticks = 0;
        if (string.IsNullOrWhiteSpace(value) ||
            !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedTicks) ||
            parsedTicks < 0)
        {
            message = $"Parameter '{parameterName}' must be a non-negative integer tick count.";
            return false;
        }

        ticks = parsedTicks;
        message = string.Empty;
        return true;
    }
}
