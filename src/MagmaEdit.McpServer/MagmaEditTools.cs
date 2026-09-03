using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MagmaEdit.McpServer;

[McpServerToolType]
public sealed class MagmaEditTools
{
    [McpServerTool(
        Name = MagmaEdit.Integration.McpEditorToolContract.ExecuteEditorCommandToolName,
        Title = "Execute MagmaEdit editor command",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        OpenWorld = false)]
    [Description("Execute one validated and authorized MagmaEdit editor command against the configured project.")]
    public static MagmaEdit.Integration.EditorCommandResult ExecuteEditorCommand(
        [Description("The MagmaEdit editor command and its command-specific parameters.")]
        MagmaEdit.Integration.EditorCommandRequest request,
        MagmaEdit.Integration.EditorAutomationSession session)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return session.Execute(request);
    }

    [McpServerTool(
        Name = MagmaEdit.Integration.McpEditorToolContract.GetEditorStateToolName,
        Title = "Get MagmaEdit editor state",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Return a read-only snapshot of the loaded MagmaEdit project, timeline, media, and undo/redo counts.")]
    public static MagmaEdit.Integration.EditorProjectState GetEditorState(
        MagmaEdit.Integration.EditorAutomationSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.GetState();
    }
}
