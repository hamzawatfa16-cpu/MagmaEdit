# Durable broker state

The authenticated broker now supports PostgreSQL-backed state for hosted deployments.

## Configuration

Set:

```text
MAGMAEDIT_BROKER_DATABASE_CONNECTION=<server-side PostgreSQL connection string>
```

The connection string is server-side only and must not be shipped in the Windows desktop application.

When the broker runs outside the `Development` environment, the database connection is required. Development can continue using the in-memory stores for local testing.

## Durable state

The Supabase migration `0002_broker_credentials_and_replay.sql` creates:

- `magmaedit.broker_credentials` for hashed, short-lived broker credentials and revocation state;
- `magmaedit.broker_replay_requests` for one-time request IDs used by replay protection.

The existing `magmaedit.desktop_sessions` table remains the durable session lease store.

## Security boundary

Broker access tokens are random 256-bit values. Only their SHA-256 hashes are stored in PostgreSQL.

Session request replay protection is atomic at the database level through a primary-key conflict check, so separate broker instances cannot accept the same request ID concurrently.

The hosted broker still validates the upstream Supabase user before issuing its own short-lived broker credential, then binds the credential to the requested user ID for session operations.

Production still requires deployment of the broker with the database and Supabase configuration, plus authenticated outbound routing from desktop clients to the broker. No database credential or Supabase secret is embedded in the desktop client.
