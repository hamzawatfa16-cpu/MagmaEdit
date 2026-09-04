namespace MagmaEdit.McpServer;

/// <summary>Holds the authenticated MagmaEdit user and desktop session for the current MCP request flow.</summary>
public sealed class MagmaEditUserContext
{
    private static readonly AsyncLocal<string?> CurrentUser = new();
    private static readonly AsyncLocal<string?> CurrentSession = new();

    public string? UserId
    {
        get => CurrentUser.Value;
        set => CurrentUser.Value = Normalize(value);
    }

    public string? SessionId
    {
        get => CurrentSession.Value;
        set => CurrentSession.Value = Normalize(value);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
