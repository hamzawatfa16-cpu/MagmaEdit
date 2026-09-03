namespace MagmaEdit.Auth;

public sealed record AuthConfiguration(string SupabaseUrl, string SupabasePublishableKey)
{
    public static bool TryLoadFromEnvironment(out AuthConfiguration? configuration, out string message)
    {
        string? url = Environment.GetEnvironmentVariable("MAGMAEDIT_SUPABASE_URL")?.Trim();
        string? key = Environment.GetEnvironmentVariable("MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY")?.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            configuration = null;
            message = "Authentication is not configured. Set MAGMAEDIT_SUPABASE_URL and MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY.";
            return false;
        }

        try
        {
            configuration = new AuthConfiguration(url, key);
            message = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            configuration = null;
            message = exception.Message;
            return false;
        }
    }
}
