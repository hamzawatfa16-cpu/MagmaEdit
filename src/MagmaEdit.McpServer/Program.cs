using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

string? projectPath = args.Length == 1
    ? args[0]
    : Environment.GetEnvironmentVariable("MAGMAEDIT_PROJECT_PATH");

if (string.IsNullOrWhiteSpace(projectPath))
{
    throw new InvalidOperationException(
        "A MagmaEdit project path is required. Pass the project path as the first argument or set MAGMAEDIT_PROJECT_PATH.");
}

var client = new MagmaEdit.Integration.AutomationClientContext(
    "local-mcp",
    MagmaEdit.Integration.AutomationClientKind.Mcp,
    new HashSet<MagmaEdit.Integration.EditorCommandCapability>
    {
        MagmaEdit.Integration.EditorCommandCapability.TimelineEditing,
        MagmaEdit.Integration.EditorCommandCapability.MediaManagement,
        MagmaEdit.Integration.EditorCommandCapability.History
    });

MagmaEdit.Integration.EditorAutomationSession session =
    MagmaEdit.Integration.EditorAutomationSession.Load(projectPath, client);

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services.AddSingleton(session);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MagmaEditTools>();

await builder.Build().RunAsync();

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
