using System.Globalization;
using MagmaEdit.Core.Editing;

namespace MagmaEdit.Integration;

/// <summary>Translates vendor-neutral automation requests into the shared MagmaEdit editor command gateway.</summary>
public sealed class EditorCommandRouter
{
    private readonly IEditorCommandGateway _gateway;

    public EditorCommandRouter(IEditorCommandGateway gateway)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    public EditorCommandResult Execute(EditorCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!EditorCommandCatalog.TryValidate(request, out string validationMessage))
        {
            return Failure(validationMessage);
        }

        try
        {
            return request.Command switch
            {
                EditorCommandKind.AddTrack => AddTrack(request),
                EditorCommandKind.RemoveTrack => RemoveTrack(request),
                EditorCommandKind.InsertClip => InsertClip(request),
                EditorCommandKind.RemoveClip => RemoveClip(request),
                EditorCommandKind.TrimClip => TrimClip(request),
                EditorCommandKind.MoveClip => MoveClip(request),
                EditorCommandKind.SplitClip => SplitClip(request),
                EditorCommandKind.RenameMedia => RenameMedia(request),
                EditorCommandKind.SetMediaPublished => SetMediaPublished(request),
                EditorCommandKind.Undo => HistoryAction(_gateway.Undo, "Undo complete.", "Nothing to undo."),
                EditorCommandKind.Redo => HistoryAction(_gateway.Redo, "Redo complete.", "Nothing to redo."),
                _ => Failure("Unsupported editor command.")
            };
        }
        catch (ArgumentException exception)
        {
            return Failure(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Failure(exception.Message);
        }
        catch (KeyNotFoundException exception)
        {
            return Failure(exception.Message);
        }
        catch (OverflowException exception)
        {
            return Failure(exception.Message);
        }
    }

    private EditorCommandResult AddTrack(EditorCommandRequest request)
    {
        string name = Required(request.Name, nameof(request.Name));
        TimelineTrack track = _gateway.AddTrack(name);
        return Success($"Added track '{track.Name}'.", trackId: track.Id);
    }

    private EditorCommandResult RemoveTrack(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        _gateway.RemoveTrack(trackId);
        return Success("Track removed.", trackId: trackId);
    }

    private EditorCommandResult InsertClip(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        string mediaId = Required(request.MediaId, nameof(request.MediaId));
        EditTime timelineStart = ParseTime(request.TimelinePositionTicks, nameof(request.TimelinePositionTicks));
        EditTime sourceIn = ParseTime(request.SourceInTicks, nameof(request.SourceInTicks));
        EditTime sourceOut = ParseTime(request.SourceOutTicks, nameof(request.SourceOutTicks));

        TimelineClip clip = _gateway.InsertClip(trackId, mediaId, timelineStart, sourceIn, sourceOut);
        return Success("Clip inserted.", trackId: trackId, clipId: clip.Id, mediaId: mediaId);
    }

    private EditorCommandResult RemoveClip(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        string clipId = Required(request.ClipId, nameof(request.ClipId));
        _gateway.RemoveClip(trackId, clipId);
        return Success("Clip removed.", trackId: trackId, clipId: clipId);
    }

    private EditorCommandResult TrimClip(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        string clipId = Required(request.ClipId, nameof(request.ClipId));
        EditTime sourceIn = ParseTime(request.SourceInTicks, nameof(request.SourceInTicks));
        EditTime sourceOut = ParseTime(request.SourceOutTicks, nameof(request.SourceOutTicks));
        _gateway.TrimClip(trackId, clipId, sourceIn, sourceOut);
        return Success("Clip trimmed.", trackId: trackId, clipId: clipId);
    }

    private EditorCommandResult MoveClip(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        string clipId = Required(request.ClipId, nameof(request.ClipId));
        EditTime timelinePosition = ParseTime(request.TimelinePositionTicks, nameof(request.TimelinePositionTicks));
        _gateway.MoveClip(trackId, clipId, timelinePosition);
        return Success("Clip moved.", trackId: trackId, clipId: clipId);
    }

    private EditorCommandResult SplitClip(EditorCommandRequest request)
    {
        string trackId = Required(request.TrackId, nameof(request.TrackId));
        string clipId = Required(request.ClipId, nameof(request.ClipId));
        EditTime timelinePosition = ParseTime(request.TimelinePositionTicks, nameof(request.TimelinePositionTicks));
        _gateway.SplitClip(trackId, clipId, timelinePosition);
        return Success("Clip split.", trackId: trackId, clipId: clipId);
    }

    private EditorCommandResult RenameMedia(EditorCommandRequest request)
    {
        string mediaId = Required(request.MediaId, nameof(request.MediaId));
        string name = Required(request.Name, nameof(request.Name));
        _gateway.RenameMedia(mediaId, name);
        return Success("Media renamed.", mediaId: mediaId);
    }

    private EditorCommandResult SetMediaPublished(EditorCommandRequest request)
    {
        string mediaId = Required(request.MediaId, nameof(request.MediaId));
        if (request.IsPublished is not { } isPublished)
        {
            throw new ArgumentException("A publication state is required.", nameof(request));
        }

        _gateway.SetMediaPublished(mediaId, isPublished);
        return Success("Media publication state updated.", mediaId: mediaId);
    }

    private EditorCommandResult HistoryAction(Func<bool> action, string successMessage, string emptyMessage)
    {
        return action()
            ? Success(successMessage)
            : Failure(emptyMessage);
    }

    private EditorCommandResult Success(
        string message,
        string? trackId = null,
        string? clipId = null,
        string? mediaId = null)
    {
        return new EditorCommandResult(
            true,
            message,
            trackId,
            clipId,
            mediaId,
            _gateway.History.UndoCount,
            _gateway.History.RedoCount);
    }

    private EditorCommandResult Failure(string message) => new(
        false,
        message,
        UndoCount: _gateway.History.UndoCount,
        RedoCount: _gateway.History.RedoCount);

    private static string Required(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static EditTime ParseTime(string? value, string parameterName)
    {
        string ticks = Required(value, parameterName);
        if (!long.TryParse(ticks, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsedTicks) || parsedTicks < 0)
        {
            throw new ArgumentException("Time must be a non-negative integer tick count.", parameterName);
        }

        return new EditTime(parsedTicks);
    }
}
