using MagmaEdit.Integration;

namespace MagmaEdit.Core.Tests;

public sealed class EditorCommandAuthorizationTests
{
    [Fact]
    public void AuthorizerRejectsMissingClientIdentifier()
    {
        EditorCommandRequest request = new(EditorCommandKind.Undo);
        AutomationClientContext client = new(
            string.Empty,
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.History });

        EditorCommandAuthorizationResult result = EditorCommandAuthorizer.Authorize(request, client);

        Assert.False(result.Authorized);
        Assert.Contains("client identifier is required", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorizerRejectsCapabilityNotGrantedByClient()
    {
        EditorCommandRequest request = new(
            EditorCommandKind.AddTrack,
            Name: "Video");
        AutomationClientContext client = new(
            "chatgpt-session",
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.History });

        EditorCommandAuthorizationResult result = EditorCommandAuthorizer.Authorize(request, client);

        Assert.False(result.Authorized);
        Assert.Equal(EditorCommandCapability.TimelineEditing, result.RequiredCapability);
        Assert.Contains("not authorized", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthorizerAllowsOnlyExplicitlyGrantedCapability()
    {
        EditorCommandRequest request = new(EditorCommandKind.Undo);
        AutomationClientContext client = new(
            "plugin-session",
            AutomationClientKind.Plugin,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.History });

        EditorCommandAuthorizationResult result = EditorCommandAuthorizer.Authorize(request, client);

        Assert.True(result.Authorized);
        Assert.Empty(result.Message);
        Assert.Null(result.RequiredCapability);
    }

    [Fact]
    public void AuthorizerUsesCommandCatalogCapability()
    {
        EditorCommandRequest request = new(
            EditorCommandKind.RenameMedia,
            MediaId: "media-1",
            Name: "renamed.mp4");
        AutomationClientContext client = new(
            "mcp-session",
            AutomationClientKind.Mcp,
            new HashSet<EditorCommandCapability> { EditorCommandCapability.MediaManagement });

        EditorCommandAuthorizationResult result = EditorCommandAuthorizer.Authorize(request, client);

        Assert.True(result.Authorized);
    }

    [Fact]
    public void McpContractExposesOneStableEditorTool()
    {
        Assert.Equal("1", McpEditorToolContract.ContractVersion);
        Assert.Equal(
            "magmaedit.execute_editor_command",
            McpEditorToolContract.ExecuteEditorCommandToolName);
        Assert.Single(McpEditorToolContract.Definitions);
        Assert.Equal(
            McpEditorToolContract.ExecuteEditorCommand,
            McpEditorToolContract.Definitions[0]);
        Assert.Equal(
            Enum.GetValues<EditorCommandKind>(),
            McpEditorToolContract.ExecuteEditorCommand.Commands);
        Assert.Equal(
            [
                EditorCommandCapability.TimelineEditing,
                EditorCommandCapability.MediaManagement,
                EditorCommandCapability.History
            ],
            McpEditorToolContract.ExecuteEditorCommand.Capabilities);
    }
}
