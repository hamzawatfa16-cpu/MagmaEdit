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
    [Description("Execute one validated and authorized MagmaEdit editor command against the live desktop session when available, otherwise the configured project.")]
    public static Task<MagmaEdit.Integration.EditorCommandResult> ExecuteEditorCommand(
        [Description("The MagmaEdit editor command and its command-specific parameters.")]
        MagmaEdit.Integration.EditorCommandRequest request,
        MagmaEditAutomationTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(target);
        return target.ExecuteAsync(request, cancellationToken);
    }

    [McpServerTool(
        Name = MagmaEdit.Integration.McpEditorToolContract.GetEditorStateToolName,
        Title = "Get MagmaEdit editor state",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Return a read-only snapshot of the live MagmaEdit project when the desktop app is running, otherwise the configured project.")]
    public static Task<MagmaEdit.Integration.EditorProjectState> GetEditorState(
        MagmaEditAutomationTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.GetStateAsync(cancellationToken);
    }
}
