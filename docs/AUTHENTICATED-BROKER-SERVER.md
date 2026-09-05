# Authenticated broker server

The repository contains a provider-neutral ASP.NET Core broker host for the desktop session lifecycle.

## Endpoints

- `POST /v1/broker-credentials/issue` exchanges an upstream authenticated account token for a short-lived broker credential.
- `POST /v1/broker-credentials/revoke` revokes the current broker credential.
- `POST /v1/desktop-sessions/register` registers one desktop session.
- `POST /v1/desktop-sessions/renew` renews the exact user/session lease.
- `POST /v1/desktop-sessions/revoke` removes the exact user/session lease.

## Primary identity validation

When `MAGMAEDIT_SUPABASE_URL` and `MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY` are configured, credential issuance validates the supplied Supabase access token through the Supabase Auth `GET /auth/v1/user` endpoint and binds the resulting user ID to the broker credential.

The broker requires an HTTPS Supabase URL. Missing configuration keeps the default rejecting validator, so an accidentally incomplete deployment fails closed instead of issuing credentials.

## Security rules

Session endpoints require a valid short-lived broker bearer credential. The credential is hashed at rest in the in-memory implementation and is never returned by validation APIs.

The authenticated credential user must exactly match the `UserId` in the session request. A client cannot select another account by changing JSON alone.

Each session request also requires `X-MagmaEdit-Request-Id` and `X-MagmaEdit-Timestamp`. The replay protector accepts a request only inside a bounded clock-skew window and only once per request ID.

## Remaining production work

The current broker credential store, replay protector, and session broker wiring are still in-memory. Production deployment must replace those with durable shared infrastructure and add multi-instance routing before the service is used as a public multi-user broker.

The server does not provide inbound access to Windows named pipes and does not grant editor capabilities. It only establishes the authenticated HTTP session boundary needed by the desktop's outbound broker client.
