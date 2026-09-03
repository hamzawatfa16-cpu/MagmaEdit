namespace MagmaEdit.Integration;

/// <summary>Describes one transport-neutral MCP tool exposed by MagmaEdit.</summary>
public sealed record McpToolDefinition(
    string Name,
    string Description,
    string InputContract,
    IReadOnlyList<EditorCommandKind> Commands,
    IReadOnlyList<EditorCommandCapability> Capabilities);

/// <summary>Owns the stable MCP-facing tool contracts without coupling the editor core to an MCP SDK.</summary>
public static class McpEditorToolContract
{
    public const string ContractVersion = "1";
    public const string ExecuteEditorCommandToolName = "magmaedit.execute_editor_command";
    public const string GetEditorStateToolName = "magmaedit.get_editor_state";

    public static McpToolDefinition ExecuteEditorCommand { get; } = new(
        ExecuteEditorCommandToolName,
        "Validate, authorize, and execute one MagmaEdit editor command through the shared command boundary.",
        $"EditorCommandRequest/v{ContractVersion}",
        Enum.GetValues<EditorCommandKind>(),
        [
            EditorCommandCapability.TimelineEditing,
            EditorCommandCapability.MediaManagement,
            EditorCommandCapability.History
        ]);

    public static McpToolDefinition GetEditorState { get; } = new(
        GetEditorStateToolName,
        "Return a read-only snapshot of the loaded MagmaEdit project, timeline, media, and command history counts.",
        "EditorProjectState/v1",
        [],
        []);

    public static IReadOnlyList<McpToolDefinition> Definitions { get; } =
        [ExecuteEditorCommand, GetEditorState];
}
