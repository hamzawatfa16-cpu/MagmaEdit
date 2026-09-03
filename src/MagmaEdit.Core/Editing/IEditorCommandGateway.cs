namespace MagmaEdit.Core.Editing;

/// <summary>Provides the single command entry point shared by the desktop UI and future automation clients.</summary>
public interface IEditorCommandGateway
{
    EditHistory History { get; }

    TimelineTrack AddTrack(string name);

    void RemoveTrack(string trackId);

    TimelineClip InsertClip(
        string trackId,
        string mediaId,
        EditTime timelineStart,
        EditTime sourceIn,
        EditTime sourceOut);

    void RemoveClip(string trackId, string clipId);

    void TrimClip(string trackId, string clipId, EditTime sourceIn, EditTime sourceOut);

    void MoveClip(string trackId, string clipId, EditTime timelineStart);

    void SplitClip(string trackId, string clipId, EditTime timelinePosition);

    MediaAsset AddMedia(MediaAsset asset);

    void RemoveMedia(string mediaId);

    void RenameMedia(string mediaId, string newFileName);

    void SetMediaPublished(string mediaId, bool isPublished);

    bool Undo();

    bool Redo();
}
