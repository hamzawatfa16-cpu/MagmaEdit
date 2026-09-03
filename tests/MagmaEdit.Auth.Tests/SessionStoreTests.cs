using MagmaEdit.Auth;

namespace MagmaEdit.Auth.Tests;

public sealed class SessionStoreTests
{
    [Fact]
    public void SessionRoundTripsThroughWindowsDpapi()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MagmaEdit.Auth.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, "session.bin");
        var store = new SessionStore(path);
        var expected = new AuthSession(
            "access-token",
            "refresh-token",
            "user-id",
            "user@example.com",
            DateTimeOffset.UtcNow.AddHours(1));

        try
        {
            store.Save(expected);

            Assert.True(File.Exists(path));
            string raw = File.ReadAllText(path);
            Assert.DoesNotContain(expected.AccessToken, raw, StringComparison.Ordinal);
            Assert.DoesNotContain(expected.RefreshToken, raw, StringComparison.Ordinal);

            AuthSession? actual = store.Load();
            Assert.Equal(expected, actual);
        }
        finally
        {
            store.Delete();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingSessionReturnsNull()
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "MagmaEdit.Auth.Tests",
            Guid.NewGuid().ToString("N"),
            "session.bin");

        var store = new SessionStore(path);

        Assert.Null(store.Load());
    }
}
