using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace MagmaEdit.Broker;

public sealed class PostgresMagmaEditBrokerCredentialStore : IMagmaEditBrokerCredentialStore
{
    private const string TableName = "magmaedit.broker_credentials";
    private readonly string _connectionString;

    public PostgresMagmaEditBrokerCredentialStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString.Trim();
    }

    public MagmaEditBrokerCredentialIssue Issue(string userId, DateTimeOffset now, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ValidateLifetime(lifetime);

        string accessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        string tokenHash = Hash(accessToken);
        DateTimeOffset expiresAt = now.Add(lifetime);

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName} (token_hash, user_id, expires_at, revoked_at, created_at)
            VALUES (@token_hash, @user_id, @expires_at, NULL, @now);
            """;
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("user_id", userId.Trim());
        command.Parameters.AddWithValue("expires_at", expiresAt);
        command.Parameters.AddWithValue("now", now);
        command.ExecuteNonQuery();

        return new MagmaEditBrokerCredentialIssue(accessToken, expiresAt);
    }

    public bool TryAuthenticate(string accessToken, DateTimeOffset now, out string? userId)
    {
        userId = null;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT user_id
            FROM {TableName}
            WHERE token_hash = @token_hash
              AND revoked_at IS NULL
              AND expires_at > @now;
            """;
        command.Parameters.AddWithValue("token_hash", Hash(accessToken));
        command.Parameters.AddWithValue("now", now);

        userId = command.ExecuteScalar() as string;
        return !string.IsNullOrWhiteSpace(userId);
    }

    public bool Revoke(string accessToken, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {TableName}
            SET revoked_at = timezone('utc', now())
            WHERE token_hash = @token_hash
              AND user_id = @user_id
              AND revoked_at IS NULL;
            """;
        command.Parameters.AddWithValue("token_hash", Hash(accessToken));
        command.Parameters.AddWithValue("user_id", userId.Trim());
        return command.ExecuteNonQuery() == 1;
    }

    private NpgsqlConnection OpenConnection()
    {
        NpgsqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }

    private static string Hash(string accessToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(lifetime),
                "Broker credential lifetime must be greater than zero and no longer than one hour.");
        }
    }
}

public sealed class PostgresMagmaEditReplayProtector : IMagmaEditBrokerReplayProtector
{
    private const string TableName = "magmaedit.broker_replay_requests";
    private readonly string _connectionString;
    private readonly TimeSpan _clockSkew;

    public PostgresMagmaEditReplayProtector(string connectionString, TimeSpan? clockSkew = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString.Trim();
        _clockSkew = clockSkew ?? TimeSpan.FromMinutes(5);
        if (_clockSkew <= TimeSpan.Zero || _clockSkew > TimeSpan.FromMinutes(15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(clockSkew),
                "Replay clock skew must be greater than zero and no longer than 15 minutes.");
        }
    }

    public bool TryAccept(string? requestId, string? timestamp, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(requestId)
            || !long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixSeconds))
        {
            return false;
        }

        DateTimeOffset requestTime;
        try
        {
            requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        if (requestTime < now.Subtract(_clockSkew) || requestTime > now.Add(_clockSkew))
        {
            return false;
        }

        string normalizedRequestId = requestId.Trim();
        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand cleanup = connection.CreateCommand();
        cleanup.CommandText = $"DELETE FROM {TableName} WHERE accepted_at < @cutoff;";
        cleanup.Parameters.AddWithValue("cutoff", now.Subtract(_clockSkew));
        cleanup.ExecuteNonQuery();

        using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText = $"""
            INSERT INTO {TableName} (request_id, accepted_at)
            VALUES (@request_id, @accepted_at)
            ON CONFLICT (request_id) DO NOTHING;
            """;
        insert.Parameters.AddWithValue("request_id", normalizedRequestId);
        insert.Parameters.AddWithValue("accepted_at", requestTime);
        return insert.ExecuteNonQuery() == 1;
    }

    private NpgsqlConnection OpenConnection()
    {
        NpgsqlConnection connection = new(_connectionString);
        connection.Open();
        return connection;
    }
}

public interface IMagmaEditBrokerReplayProtector
{
    bool TryAccept(string? requestId, string? timestamp, DateTimeOffset now);
}
