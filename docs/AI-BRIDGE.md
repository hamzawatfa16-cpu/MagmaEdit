# MagmaEdit AI bridge

## Purpose

`MagmaEdit.AiBridge` is the provider-facing orchestration boundary for hosted AI editing. It keeps model-provider concerns outside MagmaEdit Core and reuses the existing MagmaEdit MCP server as the only editing tool boundary.

The intended flow is:

```text
AI client / product
        |
        v
MagmaEdit.AiBridge
        |
        v
OpenAI Responses API
        |
        v
MagmaEdit MCP /mcp
        |
        v
MagmaEdit Integration command router
        |
        v
MagmaEdit Core project + undo/redo + persistence
```

The same architecture can later support other model providers by implementing a provider adapter above the same MCP endpoint. Core editing code remains vendor-neutral.

## HTTP API

### Health

`GET /health`

Returns a small readiness response without exposing secrets or editor state.

### Edit

`POST /v1/edit`

Required headers:

```text
X-MagmaEdit-Bridge-Token: Bearer <bridge-token>
Authorization: Bearer <supabase-access-token>
```

The bridge token authenticates the trusted service-to-service caller. The Supabase access token identifies the signed-in MagmaEdit user. The bridge validates that user token against the project's Supabase Auth service before starting an AI request. Supabase documents the Auth user lookup as a server-confirmed way to validate access tokens. citeturn755284search1turn755284search3

Request body:

```json
{
  "prompt": "Add a video track and tell me what changed",
  "previousResponseId": null,
  "allowMutations": false
}
```

`allowMutations` is deliberately opt-in. A mutating request also requires server mutation enablement and exactly one configured authorized user.

The bridge returns the OpenAI response identifier, generated text, whether mutations were enabled, and the number of output items. It does not persist the conversation itself.

## Configuration

The bridge reads these environment variables:

```text
OPENAI_API_KEY=<secret>
MAGMAEDIT_AI_MODEL=gpt-5.2
MAGMAEDIT_REMOTE_MCP_URL=https://mcp.example.com/mcp
MAGMAEDIT_REMOTE_MCP_BEARER_TOKEN=<secret>
MAGMAEDIT_AI_BRIDGE_BEARER_TOKEN=<secret>
MAGMAEDIT_SUPABASE_URL=https://<project-ref>.supabase.co
MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY=<publishable-key>
MAGMAEDIT_AI_BRIDGE_ALLOWED_USER_IDS=<optional comma-separated Supabase user IDs>
MAGMAEDIT_AI_BRIDGE_RATE_LIMIT_PER_MINUTE=30
MAGMAEDIT_AI_BRIDGE_ALLOW_MUTATIONS=false
```

Secrets must be supplied by the hosting platform's secret store. They must never be committed to Git, embedded in the application, or written to logs.

## Account authorization

An empty `MAGMAEDIT_AI_BRIDGE_ALLOWED_USER_IDS` value permits authenticated Supabase users. A populated value is an explicit allowlist of Supabase user IDs.

The bridge uses the Auth service user lookup rather than trusting user details supplied by the client.

## Mutation safety

Read-only requests expose only `magmaedit.get_editor_state` to the model.

Mutation-enabled requests expose both:

```text
magmaedit.get_editor_state
magmaedit.execute_editor_command
```

The server additionally requires `MAGMAEDIT_AI_BRIDGE_ALLOW_MUTATIONS=true` and exactly one allowlisted user. That single-user condition is intentional because the current configured remote MCP connection is not yet isolated into per-user editor sessions.

The bridge asks the model to inspect state before mutations and to stay within the user's requested scope. The authoritative validation and capability authorization still happen inside the MagmaEdit integration/router layer; the bridge is not an authorization replacement.

## Rate limiting and audit

Requests are rate-limited per authenticated user using `MAGMAEDIT_AI_BRIDGE_RATE_LIMIT_PER_MINUTE` (default `30`). The current limiter is in-memory and protects one bridge instance; a distributed deployment will need a shared store.

Structured logs include the authenticated user ID, mutation mode, model, response ID, and output length. Access tokens, bridge credentials, and prompt bodies are not logged.

## Deployment boundary

The bridge is an ASP.NET Core service and can be containerized or hosted on a normal HTTPS-capable service. The remote MCP endpoint should be a trusted MagmaEdit MCP deployment or secure tunnel. Do not accept arbitrary MCP URLs from callers; the current implementation uses one configured endpoint to avoid turning the bridge into an SSRF proxy.

Before public multi-user deployment, the service still needs per-user MCP/editor-session binding, distributed rate limiting, durable audit storage, secret rotation, TLS termination, monitoring, and production authentication/authorization policy. The current implementation deliberately does not claim those features are complete.
