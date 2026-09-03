using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class EditorCommandCatalogTests
{
    [Fact]
    public void CatalogContainsEveryCommandInEnumOrder()
    {
        EditorCommandDefinition[] definitions = EditorCommandCatalog.DefinitionsInOrder.ToArray();

        Assert.Equal(Enum.GetValues<EditorCommandKind>(), definitions.Select(definition => definition.Command).ToArray());
        Assert.Equal(definitions.Length, definitions.Select(definition => definition.Command).Distinct().Count());
    }

    [Fact]
    public void CatalogAssignsCapabilitiesAndRequiredParameters()
    {
        EditorCommandDefinition insertClip = EditorCommandCatalog.DefinitionsInOrder.Single(
            definition => definition.Command == EditorCommandKind.InsertClip);

        Assert.Equal(EditorCommandCapability.TimelineEditing, insertClip.Capability);
        Assert.Equal(
            [
                nameof(EditorCommandRequest.TrackId),
                nameof(EditorCommandRequest.MediaId),
                nameof(EditorCommandRequest.TimelinePositionTicks),
                nameof(EditorCommandRequest.SourceInTicks),
                nameof(EditorCommandRequest.SourceOutTicks)
            ],
            insertClip.RequiredParameters);

        EditorCommandDefinition undo = EditorCommandCatalog.DefinitionsInOrder.Single(
            definition => definition.Command == EditorCommandKind.Undo);
        Assert.Equal(EditorCommandCapability.History, undo.Capability);
        Assert.Empty(undo.RequiredParameters);
    }

    [Fact]
    public void CatalogRejectsMissingRequiredParametersBeforeRouting()
    {
        EditorCommandRequest request = new(EditorCommandKind.MoveClip, TrackId: "track-1", ClipId: "clip-1");

        bool valid = EditorCommandCatalog.TryValidate(request, out string message);

        Assert.False(valid);
        Assert.Contains(nameof(EditorCommandRequest.TimelinePositionTicks), message, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogAcceptsFalseBooleanValues()
    {
        EditorCommandRequest request = new(
            EditorCommandKind.SetMediaPublished,
            MediaId: "media-1",
            IsPublished: false);

        bool valid = EditorCommandCatalog.TryValidate(request, out string message);

        Assert.True(valid);
        Assert.Empty(message);
    }
}
