# MagmaEdit Session Broker

The session broker maps an authenticated MagmaEdit account to the desktop editor session that belongs to that account.

## Current implementation

`MagmaEditSessionBroker` is an in-memory, process-local registry in `MagmaEdit.Integration`. It provides:

- one active desktop session per authenticated user;
- opaque session and connection identifiers;
- a bounded lease with explicit expiration;
- renewal only when both user ID and session ID match;
- unregister only when the session ID matches;
- expired-session cleanup before replacement;
- normalized user/session/connection identifiers and deduplicated capabilities.

The broker intentionally has no HTTP, database, cloud, or vendor-specific code. That keeps the command and identity boundaries reusable by the desktop app, MCP server, and hosted AI bridge.

## Intended production flow

```text
Google/Supabase identity
        |
        v
Authenticated AI bridge
        |
        v
Durable session broker
        |
        v
User's desktop outbound connection
        |
        v
Local MagmaEdit named pipe
        |
        v
Shared editor command gateway
```

The desktop should establish an authenticated outbound connection to the broker. The broker should never assume that a hosted service can directly reach a user's private Windows named pipe.

## Production requirements before enabling multi-user hosting

The in-memory implementation is not sufficient for production. A production broker still needs:

1. durable shared session state so multiple bridge instances see the same registrations;
2. authenticated, encrypted transport between desktop and broker;
3. short-lived connection credentials or equivalent proof of session ownership;
4. authorization that binds every routed request to the authenticated account and registered session;
5. expiry, heartbeat, reconnect, and explicit revocation behavior;
6. audit logging, rate limiting, and secret rotation;
7. protection against duplicate registration and stale-session takeover;
8. tests covering concurrent registration, failover, reconnect, and multi-instance routing.

A bearer token by itself is not an account identity. The broker must establish the user identity before it accepts or routes a session-bound command.

## Security boundary

The local Windows named pipe remains a desktop-local boundary. The broker adds the missing hosted mapping of `UserId -> active desktop session`; it does not replace the pipe's current-user and authenticated-session checks.

Until the durable broker and outbound desktop connection exist, the repository should continue to describe hosted multi-user editing as unfinished infrastructure rather than a completed feature.
