# Authenticated Desktop Broker Client

MagmaEdit desktop sessions use an outbound HTTPS connection to the hosted session broker. The desktop must never require the hosted service to reach a private Windows named pipe directly.

## Client contract

`AuthenticatedMagmaEditSessionBrokerClient` implements the provider-neutral `IMagmaEditSessionBrokerClient` lifecycle contract used by the desktop session connection manager.

The endpoint contract is:

- `POST /v1/broker-credentials/issue`
- `POST /v1/desktop-sessions/register`
- `POST /v1/desktop-sessions/renew`
- `POST /v1/desktop-sessions/revoke`

`AuthenticatedMagmaEditBrokerCredentialProvider` closes the credential bootstrap loop. It accepts an upstream authenticated access-token provider, exchanges that token for a short-lived broker credential through `POST /v1/broker-credentials/issue`, and keeps the broker credential only in process memory.

The provider refreshes the broker credential inside a 30-second safety window by default and serializes concurrent refreshes so multiple session operations do not stampede the broker.

## Security boundary

- The upstream identity token is supplied by the desktop authentication layer and is never written to disk by this provider.
- The short-lived broker credential is kept in memory only.
- Broker communication requires HTTPS.
- The session client adds a random request ID and Unix timestamp to registration, renewal, and revocation calls for replay protection.
- Server-side broker credentials remain hashed at rest in PostgreSQL.

## Composition

The desktop composition root should provide the authenticated session's current upstream access token through a callback, create `AuthenticatedMagmaEditBrokerCredentialProvider`, and inject it into `AuthenticatedMagmaEditSessionBrokerClient`.

The provider intentionally does not depend on a specific identity platform, keeping Google/Supabase authentication isolated from broker transport.

## Remaining hosted transport work

The remaining production gap is the broker's outbound command/stream routing to a registered desktop connection. That transport should consume the authenticated session identity established here rather than introducing a second editor authorization model.
