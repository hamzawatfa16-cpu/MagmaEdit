using Npgsql;

namespace MagmaEdit.Integration;

/// <summary>PostgreSQL-backed durable implementation of the authenticated desktop session store.</summary>
public sealed class PostgresMagmaEditSessionStore : IMagmaEditSessionStore
{
    private const string TableName = "magmaedit.desktop_sessions";
    private readonly string _connectionString;

    public PostgresMagmaEditSessionStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString.Trim();
    }

    public MagmaEditSessionDescriptor Register(MagmaEditSessionRegistration registration, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ValidateRegistration(registration);

        string userId = registration.UserId.Trim();
        string sessionId = registration.SessionId.Trim();
        string connectionId = registration.ConnectionId.Trim();
        string endpoint = registration.Endpoint.Trim();
        string[] capabilities = NormalizeCapabilities(registration.Capabilities);
        DateTimeOffset expiresAt = now.Add(registration.LeaseDuration);

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName}
                (user_id, session_id, connection_id, endpoint, expires_at, capabilities, created_at, updated_at)
            VALUES
                (@user_id, @session_id, @connection_id, @endpoint, @expires_at, @capabilities, @now, @now)
            ON CONFLICT (user_id) DO UPDATE SET
                session_id = EXCLUDED.session_id,
                connection_id = EXCLUDED.connection_id,
                endpoint = EXCLUDED.endpoint,
                expires_at = EXCLUDED.expires_at,
                capabilities = EXCLUDED.capabilities,
                updated_at = EXCLUDED.updated_at
            WHERE {TableName}.expires_at <= @now
            RETURNING user_id, session_id, connection_id, endpoint, expires_at, capabilities;
            """;
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("session_id", sessionId);
        command.Parameters.AddWithValue("connection_id", connectionId);
        command.Parameters.AddWithValue("endpoint", endpoint);
        command.Parameters.AddWithValue("expires_at", expiresAt);
        command.Parameters.AddWithValue("capabilities", capabilities);
        command.Parameters.AddWithValue("now", now);

        try
        {
            using NpgsqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("An active editor session is already registered for this user.");
            }

            return ReadDescriptor(reader);
        }
        catch (PostgresException exception) when (exception.SqlState == "23505")
        {
            throw new InvalidOperationException("An active editor session is already registered for this user or uses an existing session identifier.", exception);
        }
    }

    public bool TryGet(string userId, DateTimeOffset now, out MagmaEditSessionDescriptor? descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        descriptor = null;

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM {TableName} WHERE user_id = @user_id AND expires_at <= @now;";
        delete.Parameters.AddWithValue("user_id", userId.Trim());
        delete.Parameters.AddWithValue("now", now);
        delete.ExecuteNonQuery();

        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT user_id, session_id, connection_id, endpoint, expires_at, capabilities FROM {TableName} WHERE user_id = @user_id AND expires_at > @now;";
        command.Parameters.AddWithValue("user_id", userId.Trim());
        command.Parameters.AddWithValue("now", now);

        using NpgsqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        descriptor = ReadDescriptor(reader);
        return true;
    }

    public bool TryRenew(
        string userId,
        string sessionId,
        TimeSpan leaseDuration,
        DateTimeOffset now,
        out MagmaEditSessionDescriptor? renewed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ValidateLease(leaseDuration);
        renewed = null;

        DateTimeOffset expiresAt = now.Add(leaseDuration);
        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {TableName}
            SET expires_at = @expires_at, updated_at = @now
            WHERE user_id = @user_id AND session_id = @session_id AND expires_at > @now
            RETURNING user_id, session_id, connection_id, endpoint, expires_at, capabilities;
            """;
        command.Parameters.AddWithValue("expires_at", expiresAt);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("user_id", userId.Trim());
        command.Parameters.AddWithValue("session_id", sessionId.Trim());

        using NpgsqlDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return false;
        }

        renewed = ReadDescriptor(reader);
        return true;
    }

    public bool Unregister(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using NpgsqlConnection connection = OpenConnection();
        using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {TableName} WHERE user_id = @user_id AND session_id = @session_id;";
        command.Parameters.AddWithValue("user_id", userId.Trim());
        command.Parameters.AddWithValue("session_id", sessionId.Trim());
        return command.ExecuteNonQuery() == 1;
    }

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static MagmaEditSessionDescriptor ReadDescriptor(NpgsqlDataReader reader)
    {
        string userId = reader.GetString(0);
        string sessionId = reader.GetString(1);
        string connectionId = reader.GetString(2);
        string endpoint = reader.GetString(3);
        DateTimeOffset expiresAt = reader.GetFieldValue<DateTimeOffset>(4);
        string[] capabilities = reader.IsDBNull(5)
            ? Array.Empty<string>()
            : reader.GetFieldValue<string[]>(5);

        return new MagmaEditSessionDescriptor(
            userId,
            sessionId,
            connectionId,
            endpoint,
            expiresAt,
            capabilities);
    }

    private static void ValidateRegistration(MagmaEditSessionRegistration registration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.ConnectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.Endpoint);
        ArgumentNullException.ThrowIfNull(registration.Capabilities);
        ValidateLease(registration.LeaseDuration);
    }

    private static string[] NormalizeCapabilities(IReadOnlyList<string> capabilities) =>
        capabilities
            .Where(static capability => !string.IsNullOrWhiteSpace(capability))
            .Select(static capability => capability.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static void ValidateLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "The session lease must be greater than zero and no longer than 24 hours.");
        }
    }
}
