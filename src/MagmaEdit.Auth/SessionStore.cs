using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MagmaEdit.Auth;

public sealed class SessionStore
{
    private static readonly byte[] AdditionalEntropy =
        Encoding.UTF8.GetBytes("MagmaEdit.Auth.Session.v1");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _path;

    public SessionStore(string? path = null)
    {
        _path = Path.GetFullPath(path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MagmaEdit",
            "Auth",
            "session.bin"));
    }

    public string Path => _path;

    public void Save(AuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        Validate(session);

        string? directory = System.IO.Path.GetDirectoryName(_path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("The authentication session path has no parent directory.");
        }

        Directory.CreateDirectory(directory);

        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        byte[] protectedPayload = ProtectedData.Protect(
            plaintext,
            AdditionalEntropy,
            DataProtectionScope.CurrentUser);

        string temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.None,
                    Options = FileOptions.WriteThrough | FileOptions.SequentialScan,
                    BufferSize = 16 * 1024
                }))
            {
                stream.Write(protectedPayload);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public AuthSession? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            byte[] protectedPayload = File.ReadAllBytes(_path);
            byte[] plaintext = ProtectedData.Unprotect(
                protectedPayload,
                AdditionalEntropy,
                DataProtectionScope.CurrentUser);
            AuthSession? session = JsonSerializer.Deserialize<AuthSession>(plaintext, JsonOptions);
            if (session is null)
            {
                Delete();
                return null;
            }

            Validate(session);
            return session;
        }
        catch (CryptographicException)
        {
            Delete();
            return null;
        }
        catch (JsonException)
        {
            Delete();
            return null;
        }
        catch (InvalidDataException)
        {
            Delete();
            return null;
        }
    }

    public void Delete()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static void Validate(AuthSession session)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken) ||
            string.IsNullOrWhiteSpace(session.RefreshToken) ||
            string.IsNullOrWhiteSpace(session.UserId) ||
            string.IsNullOrWhiteSpace(session.Email) ||
            session.ExpiresAtUtc == default)
        {
            throw new InvalidDataException("The authentication session is invalid.");
        }
    }
}
