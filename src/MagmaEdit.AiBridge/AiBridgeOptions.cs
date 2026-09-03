namespace MagmaEdit.AiBridge;

public sealed class AiBridgeOptions
{
    public string OpenAiApiKey { get; init; } = string.Empty;

    public string OpenAiModel { get; init; } = "gpt-5.2";

    public string RemoteMcpUrl { get; init; } = string.Empty;

    public string RemoteMcpBearerToken { get; init; } = string.Empty;

    public string BridgeBearerToken { get; init; } = string.Empty;

    public string SupabaseUrl { get; init; } = string.Empty;

    public string SupabasePublishableKey { get; init; } = string.Empty;

    public IReadOnlySet<string> AllowedUserIds { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public int RateLimitPerMinute { get; init; } = 30;

    public bool AllowMutations { get; init; }

    public static AiBridgeOptions FromEnvironment(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        HashSet<string> allowedUserIds = ParseAllowedUserIds();

        return new AiBridgeOptions
        {
            OpenAiApiKey = Required("OPENAI_API_KEY"),
            OpenAiModel = Optional("MAGMAEDIT_AI_MODEL") ?? "gpt-5.2",
            RemoteMcpUrl = Required("MAGMAEDIT_REMOTE_MCP_URL"),
            RemoteMcpBearerToken = Required("MAGMAEDIT_REMOTE_MCP_BEARER_TOKEN"),
            BridgeBearerToken = Required("MAGMAEDIT_AI_BRIDGE_BEARER_TOKEN"),
            SupabaseUrl = Required("MAGMAEDIT_SUPABASE_URL"),
            SupabasePublishableKey = Required("MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY"),
            AllowedUserIds = allowedUserIds,
            RateLimitPerMinute = ParsePositiveInt("MAGMAEDIT_AI_BRIDGE_RATE_LIMIT_PER_MINUTE", 30),
            AllowMutations = ParseBoolean("MAGMAEDIT_AI_BRIDGE_ALLOW_MUTATIONS")
        };
    }

    private static string Required(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{name} is required.");

        return value.Trim();
    }

    private static string? Optional(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ParseBoolean(string name)
    {
        string? value = Optional(name);
        return value is not null && bool.TryParse(value, out bool parsed) && parsed;
    }

    private static int ParsePositiveInt(string name, int defaultValue)
    {
        string? value = Optional(name);
        return value is not null && int.TryParse(value, out int parsed) && parsed > 0
            ? parsed
            : defaultValue;
    }

    private static HashSet<string> ParseAllowedUserIds()
    {
        string? value = Optional("MAGMAEDIT_AI_BRIDGE_ALLOWED_USER_IDS");
        return value is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static id => id.Length > 0)
                .ToHashSet(StringComparer.Ordinal);
    }
}
