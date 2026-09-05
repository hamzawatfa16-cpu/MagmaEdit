# Desktop session connection

MagmaEdit keeps the desktop-to-broker lifecycle behind provider-neutral integration contracts. `MagmaEditDesktopSessionConnectionManager` owns one authenticated desktop session registration and keeps its lease alive with periodic renewal.

## Lifecycle

```text
Disconnected
    |
    v
Connecting --registration failure--> Reconnecting
    |
    v
Connected --heartbeat failure--> Reconnecting
    |                                |
    |                                v
    |                            Connecting
    |
    v
Revoked
```

A heartbeat renews the exact `UserId + SessionId` lease before it expires. A transient transport failure does not change the session identity; reconnect registration is idempotent when the same session and connection identifiers are presented. A different active session for the same user is still rejected.

## Desktop bootstrap

After a successful Supabase sign-in, the Windows application can start the broker session automatically when `MAGMAEDIT_BROKER_URL` is configured. The desktop requests short-lived broker credentials through the injected upstream access-token provider; broker credentials are kept only in memory.

Set:

- `MAGMAEDIT_BROKER_URL` to the hosted broker HTTPS base URL.
- `MAGMAEDIT_DESKTOP_ENDPOINT` to the desktop's externally reachable HTTPS or WSS endpoint used by the hosted routing layer.
- `MAGMAEDIT_BROKER_LEASE_MINUTES` optionally to an integer from 5 to 60; the default is 15 minutes.

No database credentials are accepted by the desktop bootstrap. The session is revoked during orderly shutdown when the hosted broker is reachable.

## Security boundary

The connection manager does not grant editor capabilities. The existing session broker and command authorization remain authoritative. Broker credentials and privileged database credentials belong only in the hosted service; the Windows desktop must never receive privileged database credentials.

The network-specific broker client is intentionally separate from the lifecycle manager so a hosted implementation can use authenticated TLS transport without pulling HTTP or cloud dependencies into MagmaEdit Core.

## Production transport

The repository now provides the desktop bootstrap, lifecycle and persistence abstractions, PostgreSQL session store, and automatic short-lived credential acquisition. The remaining hosted slice is the concrete broker-to-desktop command/stream relay: authenticated requests must resolve the exact user/session to a live outbound desktop connection and forward only authorized editor operations.
