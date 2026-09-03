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

## HTTP API

`POST /v1/edit` requires:

```text
X-MagmaEdit-Bridge-Token: Bearer <bridge-token>
Authorization: Bearer <supabase-access-token>
```

The bridge token authenticates the trusted service-to-service caller. The Supabase token identifies the signed-in MagmaEdit user. The bridge validates the token against Supabase Auth's user endpoint before starting an AI request. Supabase documents the Auth user lookup as a server-confirmed way to validate an access token. citeturn755284search1turn755284search3

Example request:

```json
{
  "prompt": "Add a video track and tell me what changed",
  "previousResponseId": null,
  "allowMutations": false
}
```

## Account authorization

`MAGMAEDIT_AI_BRIDGE_ALLOWED_USER_IDS` is an optional comma-separated allowlist of Supabase user IDs. When empty, any authenticated Supabase user may use the bridge. When populated, only listed users may use it.

## Mutation safety

Read-only requests expose only `magmaedit.get_editor_state`.

Mutation-enabled requests expose `magmaedit.get_editor_state` and `magmaedit.execute_editor_command`, but only when the server enables mutations, exactly one user ID is configured in the allowlist, and the authenticated user is that user.

The single-user condition prevents multiple accounts from sharing one mutable remote MCP/editor session. Per-user editor-session and MCP-credential isolation is a later stage.

The authoritative validation and capability authorization still happen inside the MagmaEdit integration/router layer.

## Rate limiting and audit

Requests are rate-limited per authenticated user with `MAGMAEDIT_AI_BRIDGE_RATE_LIMIT_PER_MINUTE` (default `30`). The current limiter is in-memory and applies to one bridge instance.

Structured logs include user ID, mutation mode, model, response ID, and output length. Tokens and prompt bodies are not logged.

## Configuration

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

Secrets must come from the hosting platform's secret store and must never be committed to Git or written to logs.

## Public deployment boundary

The current service is intentionally not a complete multi-user production system. Before public multi-user deployment, it still needs per-user MCP/editor-session binding, distributed rate limiting, durable audit storage, secret rotation, TLS termination, monitoring, and production authentication/authorization policy.
