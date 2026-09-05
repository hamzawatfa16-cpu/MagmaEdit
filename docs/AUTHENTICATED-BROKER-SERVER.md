# Authenticated broker server

The repository now contains a provider-neutral ASP.NET Core broker host for the desktop session lifecycle. It exposes:

- `POST /v1/broker-credentials/issue` to exchange an upstream authenticated account token for a short-lived broker credential;
- `POST /v1/broker-credentials/revoke` to revoke the current broker credential;
- `POST /v1/desktop-sessions/register` to register one desktop session;
- `POST /v1/desktop-sessions/renew` to renew the exact user/session lease;
- `POST /v1/desktop-sessions/revoke` to remove the exact user/session lease.

## Security rules

Session endpoints require a valid short-lived broker bearer credential. The credential is hashed at rest in the in-memory implementation and is never returned by validation APIs.

The authenticated credential user must exactly match the `UserId` in the session request. A client cannot select another account by changing JSON alone.

Each session request also requires `X-MagmaEdit-Request-Id` and `X-MagmaEdit-Timestamp`. The replay protector accepts a request only inside a bounded clock-skew window and only once per request ID.

The default primary identity validator intentionally rejects every request. A hosted deployment must inject a real account validator, such as the existing Supabase identity boundary, and must replace the in-memory credential/session stores with durable shared infrastructure.

The server does not provide inbound access to Windows named pipes and does not grant editor capabilities. It only establishes the authenticated HTTP session boundary needed by the desktop's outbound broker client.
