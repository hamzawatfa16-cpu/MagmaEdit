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

[McpServerToolType]
public sealed class MagmaEditTools
{
    [McpServerTool]
    public static MagmaEdit.Integration.EditorCommandResult ExecuteEditorCommand(
        MagmaEdit.Integration.EditorCommandRequest request,
        MagmaEdit.Integration.EditorAutomationSession session)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(session);
        return session.Execute(request);
    }
}
