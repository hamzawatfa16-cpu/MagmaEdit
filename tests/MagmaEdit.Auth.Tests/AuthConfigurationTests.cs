using MagmaEdit.Auth;

namespace MagmaEdit.Auth.Tests;

public sealed class AuthConfigurationTests
{
    [Fact]
    public void MissingEnvironmentVariablesReturnConfigurationError()
    {
        const string urlVariable = "MAGMAEDIT_SUPABASE_URL";
        const string keyVariable = "MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY";
        string? originalUrl = Environment.GetEnvironmentVariable(urlVariable);
        string? originalKey = Environment.GetEnvironmentVariable(keyVariable);

        try
        {
            Environment.SetEnvironmentVariable(urlVariable, null);
            Environment.SetEnvironmentVariable(keyVariable, null);

            bool loaded = AuthConfiguration.TryLoadFromEnvironment(out AuthConfiguration? configuration, out string message);

            Assert.False(loaded);
            Assert.Null(configuration);
            Assert.Contains(urlVariable, message, StringComparison.Ordinal);
            Assert.Contains(keyVariable, message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(urlVariable, originalUrl);
            Environment.SetEnvironmentVariable(keyVariable, originalKey);
        }
    }

    [Fact]
    public void ValidEnvironmentVariablesCreateConfiguration()
    {
        const string urlVariable = "MAGMAEDIT_SUPABASE_URL";
        const string keyVariable = "MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY";
        string? originalUrl = Environment.GetEnvironmentVariable(urlVariable);
        string? originalKey = Environment.GetEnvironmentVariable(keyVariable);

        try
        {
            Environment.SetEnvironmentVariable(urlVariable, "https://example.supabase.co");
            Environment.SetEnvironmentVariable(keyVariable, "public-test-key");

            bool loaded = AuthConfiguration.TryLoadFromEnvironment(out AuthConfiguration? configuration, out string message);

            Assert.True(loaded);
            Assert.NotNull(configuration);
            Assert.Equal("https://example.supabase.co", configuration.SupabaseUrl);
            Assert.Equal("public-test-key", configuration.SupabasePublishableKey);
            Assert.Empty(message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(urlVariable, originalUrl);
            Environment.SetEnvironmentVariable(keyVariable, originalKey);
        }
    }
}
