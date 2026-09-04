# MagmaEdit Session Transport

## Purpose

MagmaEdit needs a secure way for an authenticated hosted AI service to reach the correct running desktop editor without giving the service arbitrary access to another user's editor.

The integration layer now defines a vendor-neutral session transport boundary:

```text
Authenticated AI request
        |
        v
UserId + SessionId
        |
        v
MagmaEdit session broker
        |
        v
Session transport
        |
        v
Authenticated desktop connection
        |
        v
MagmaEdit MCP / shared command gateway
```

`MagmaEditSessionBroker` owns the account-to-session lease state. `MagmaEditSessionTransportRegistry` is the development transport adapter that verifies the registered session before forwarding a request to an attached desktop connection.

## Security rules

A transport request must provide a non-empty `UserId`, `SessionId`, operation, and correlation ID. The transport rejects requests when the session is missing, expired, or belongs to a different user.

Desktop connections must be attached to an active broker session before they can receive requests. Detaching a connection immediately makes the session unavailable to the transport registry.

The transport layer does not replace editor command authorization. Mutations continue through the existing MCP and shared command gateway so the same validation, capabilities, history, and persistence rules apply regardless of caller.

## Current implementation boundary

The registry included in this stage is intentionally in-memory. It is a reusable development/test implementation, not a production cloud service.

A production implementation still needs:

- durable shared session state so multiple hosted instances see the same user/session mapping
- authenticated outbound desktop connectivity, preferably initiated by the desktop rather than requiring inbound access to a private Windows machine
- short-lived connection/session credentials and rotation
- TLS termination and network policy
- distributed rate limiting and durable audit storage
- connection health, lease renewal, reconnect, and expiry handling
- monitoring and operational controls

The Windows named pipe remains the local desktop security boundary. A hosted transport must ultimately deliver authenticated traffic to that local boundary rather than bypassing the command architecture.

## Testing

The regression suite covers successful routing, wrong-user rejection, expired-session rejection, and detachment behavior. This keeps the per-user isolation rules testable before a production transport is introduced.
