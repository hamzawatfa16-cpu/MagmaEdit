namespace MagmaEdit.McpServer;

/// <summary>Holds the authenticated MagmaEdit user for the current MCP request flow.</summary>
public sealed class MagmaEditUserContext
{
    private static readonly AsyncLocal<string?> CurrentUser = new();

    public string? UserId
    {
        get => CurrentUser.Value;
        set => CurrentUser.Value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
