using System.Security.Cryptography;
using System.Text;
using MagmaEdit.McpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

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
string transport = Environment.GetEnvironmentVariable("MAGMAEDIT_MCP_TRANSPORT")?.Trim() ?? "stdio";

if (!string.Equals(transport, "streamable-http", StringComparison.OrdinalIgnoreCase))
{
    HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(settings: null);
    builder.Services.AddSingleton(target);
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools<MagmaEditTools>();

    await builder.Build().RunAsync();
    return;
}

string? bearerToken = Environment.GetEnvironmentVariable("MAGMAEDIT_MCP_HTTP_BEARER_TOKEN");
if (string.IsNullOrWhiteSpace(bearerToken))
{
    throw new InvalidOperationException(
        "MAGMAEDIT_MCP_HTTP_BEARER_TOKEN is required when MAGMAEDIT_MCP_TRANSPORT=streamable-http.");
}

string listenUrl = Environment.GetEnvironmentVariable("MAGMAEDIT_MCP_HTTP_URL")?.Trim()
    ?? "http://127.0.0.1:3001";
if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out Uri? listenUri)
    || (listenUri.Scheme is not "http" and not "https")
    || string.IsNullOrWhiteSpace(listenUri.Host))
{
    throw new InvalidOperationException(
        $"MAGMAEDIT_MCP_HTTP_URL must be an absolute HTTP or HTTPS URL. Received '{listenUrl}'.");
}

WebApplicationBuilder webBuilder = WebApplication.CreateBuilder(args);
webBuilder.Services.AddSingleton(target);
webBuilder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<MagmaEditTools>();

WebApplication app = webBuilder.Build();
string expectedToken = bearerToken;

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/mcp"))
    {
        await next().ConfigureAwait(false);
        return;
    }

    string authorization = context.Request.Headers.Authorization.ToString();
    const string bearerPrefix = "Bearer ";
    if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return;
    }

    string providedToken = authorization[bearerPrefix.Length..].Trim();
    byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
    byte[] providedBytes = Encoding.UTF8.GetBytes(providedToken);
    bool valid = CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);

    if (!valid)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return;
    }

    await next().ConfigureAwait(false);
});

app.MapMcp("/mcp");
app.Run(listenUri);
