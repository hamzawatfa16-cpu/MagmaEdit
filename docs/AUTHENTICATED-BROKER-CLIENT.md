# Authenticated Desktop Broker Client

MagmaEdit desktop sessions use an outbound HTTPS connection to the hosted session broker. The desktop must never require the hosted service to reach a private Windows named pipe directly.

## Client contract

`AuthenticatedMagmaEditSessionBrokerClient` implements the provider-neutral `IMagmaEditSessionBrokerClient` lifecycle contract used by the desktop session connection manager.

The initial endpoint contract is:

- `POST /v1/desktop-sessions/register`
- `POST /v1/desktop-sessions/renew`
- `POST /v1/desktop-sessions/revoke`

The client requires an `https://` base URI. It obtains a short-lived bearer credential from `IMagmaEditBrokerCredentialProvider`; credentials are not persisted by the client.

Every request also carries a cryptographically random request ID and a UTC Unix timestamp. The broker must treat these as replay-protection inputs: reject reused request IDs, enforce a bounded clock-skew window, and bind the authenticated credential to the user/session in the request body.

## Security boundary

The client deliberately fails before network I/O when the broker URI is not HTTPS or the supplied credential is absent/expired. Failed HTTP responses do not echo response bodies into exception messages, preventing accidental leakage of server payloads or credentials through logs.

The client does not claim to authenticate the user by itself. The credential provider and broker remain responsible for account authentication, token issuance, token expiry/revocation, and server-side authorization.

## Remaining hosted transport work

This client establishes the desktop-side network boundary. The next hosted-service slice must implement the corresponding authenticated endpoints, credential issuance/revocation, replay protection, exact user/session authorization, and multi-instance broker routing. WebSocket/stream transport can then be layered on the same authenticated session identity without changing the editor command contract.
