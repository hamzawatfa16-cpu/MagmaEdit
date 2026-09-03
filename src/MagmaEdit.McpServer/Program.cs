using MagmaEdit.McpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

string? projectPath = args.Length == 1
    ? args[0]
    : Environment.GetEnvironmentVariable("MAGMAEDIT_PROJECT_PATH");

var client = new MagmaEdit.Integration.AutomationClientContext(
    "local-mcp",
    MagmaEdit.Integration.AutomationClientKind.Mcp,
    new HashSet<MagmaEdit.Integration.EditorCommandCapability>
    {
        MagmaEdit.Integration.EditorCommandCapability.TimelineEditing,
        MagmaEdit.Integration.EditorCommandCapability.MediaManagement,
        MagmaEdit.Integration.EditorCommandCapability.History
    });

var target = new MagmaEditAutomationTarget(projectPath, client);

HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services.AddSingleton(target);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<MagmaEditTools>();

await builder.Build().RunAsync();
