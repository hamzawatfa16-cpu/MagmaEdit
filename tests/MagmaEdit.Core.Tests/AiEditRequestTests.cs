using MagmaEdit.AiBridge;
using Microsoft.Extensions.Logging;

namespace MagmaEdit.Core.Tests;

public sealed class AiEditRequestTests
{
    [Fact]
    public async Task MutationRequestRequiresDesktopSessionId()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(static builder => builder.SetMinimumLevel(LogLevel.None));
        var bridge = new OpenAiMcpEditingBridge(
            new AiBridgeOptions
            {
                OpenAiApiKey = "test-key",
                OpenAiModel = "test-model",
                RemoteMcpUrl = "https://example.invalid/mcp",
                RemoteMcpBearerToken = "test-mcp-token",
                AllowMutations = true
            },
            loggerFactory.CreateLogger<OpenAiMcpEditingBridge>());

        await Assert.ThrowsAsync<ArgumentException>(() => bridge.EditAsync(
            new AiEditRequest("Add a track", AllowMutations: true),
            "user-123",
            CancellationToken.None));
    }
}
