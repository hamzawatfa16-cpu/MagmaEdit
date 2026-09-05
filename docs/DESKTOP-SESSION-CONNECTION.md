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

## Security boundary

The connection manager does not grant editor capabilities. The existing session broker and command authorization remain authoritative. Broker credentials and privileged database credentials belong only in the hosted service; the Windows desktop must never receive privileged database credentials.

The network-specific broker client is intentionally separate from the lifecycle manager so a hosted implementation can use authenticated TLS transport without pulling HTTP or cloud dependencies into MagmaEdit Core.

## Production transport

The current repository provides the lifecycle and persistence abstractions plus the PostgreSQL session store. A production deployment still needs a concrete authenticated broker client, short-lived connection credentials, TLS enforcement, server-side revocation, and operational telemetry before hosted multi-user editing is enabled.
