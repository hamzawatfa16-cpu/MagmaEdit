# MagmaEdit Session Broker

The session broker maps an authenticated MagmaEdit account to the desktop editor session that belongs to that account.

## Storage boundary

`MagmaEditSessionBroker` now depends on `IMagmaEditSessionStore`. The default implementation remains `InMemoryMagmaEditSessionStore` for local development, while hosted deployments can inject `PostgresMagmaEditSessionStore` backed by the schema in `supabase/migrations/0001_session_broker.sql`.

The broker preserves the existing one-session-per-user, exact session matching, lease expiry, renewal, unregister, and capability normalization rules regardless of storage provider.

## Production flow

```text
Google/Supabase identity
        |
        v
Authenticated AI bridge
        |
        v
Durable PostgreSQL session broker
        |
        v
Authenticated desktop outbound connection
        |
        v
Local MagmaEdit named pipe
        |
        v
Shared editor command gateway
```

The desktop should establish an authenticated outbound connection to the broker. The broker must never assume that a hosted service can directly reach a user's private Windows named pipe.

## PostgreSQL requirements

The durable implementation uses PostgreSQL and relies on conditional database writes for session registration and renewal. Registration can replace an expired row but cannot replace a live row; renewal requires an exact user/session match and a non-expired lease.

The session table has row-level security enabled with no client-facing policies. The hosted broker must use a server-side database credential and the Windows desktop must never receive that credential.

## Remaining hosted transport work

Durable storage is only the persistence half of the production boundary. The next step is authenticated outbound desktop connectivity, including short-lived connection credentials, heartbeat/reconnect, revocation, and routing across multiple bridge instances.
