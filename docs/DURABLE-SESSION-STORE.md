# Durable session store

MagmaEdit keeps the authenticated desktop-session lease state behind `IMagmaEditSessionStore` so the command and transport layers do not depend on a database implementation.

## Production boundary

The default `MagmaEditSessionBroker` remains suitable for local development. Hosted deployments should supply a durable implementation backed by shared PostgreSQL/Supabase state.

A durable store must preserve these invariants:

- one active session per authenticated user;
- session and connection identifiers are opaque and exact-match on renewal/revocation;
- leases expire automatically and cannot be renewed after expiry;
- registration cannot overwrite a live session for the same user;
- capability lists are normalized and deduplicated;
- all writes are atomic so multiple bridge instances cannot race a user into two active sessions.

The desktop connection itself remains separately authenticated. Persisting a session descriptor does not grant the broker permission to impersonate the desktop or bypass the local Windows named-pipe checks.

## Supabase/PostgreSQL

The intended hosted implementation uses PostgreSQL with the schema in `supabase/migrations/0001_session_broker.sql`. The broker service must connect with a server-side database credential; never put a privileged database credential in the Windows desktop application.
