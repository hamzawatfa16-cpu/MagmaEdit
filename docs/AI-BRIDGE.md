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

Required request header:

```text
Authorization: Bearer <bridge-token>
```

Request body:

```json
{
  "prompt": "Add a video track and tell me what changed",
  "previousResponseId": null,
  "allowMutations": false
}
```

`allowMutations` is deliberately opt-in. A mutating request succeeds only when both the request and the server configuration enable mutations.

The bridge returns the OpenAI response identifier, generated text, whether mutations were enabled, and the number of output items. It does not persist the conversation itself.

## Configuration

The bridge reads these environment variables:

```text
OPENAI_API_KEY=<secret>
MAGMAEDIT_AI_MODEL=gpt-5.2
MAGMAEDIT_REMOTE_MCP_URL=https://mcp.example.com/mcp
MAGMAEDIT_REMOTE_MCP_BEARER_TOKEN=<secret>
MAGMAEDIT_AI_BRIDGE_BEARER_TOKEN=<secret>
MAGMAEDIT_AI_BRIDGE_ALLOW_MUTATIONS=false
```

Secrets must be supplied by the hosting platform's secret store. They must never be committed to Git, embedded in the application, or written to logs.

## Mutation safety

Read-only requests expose only `magmaedit.get_editor_state` to the model.

Mutation-enabled requests expose both:

```text
magmaedit.get_editor_state
magmaedit.execute_editor_command
```

The bridge asks the model to inspect state before mutations and to stay within the user's requested scope. The authoritative validation and capability authorization still happen inside the MagmaEdit integration/router layer; the bridge is not an authorization replacement.

## Deployment boundary

The bridge is an ASP.NET Core service and can be containerized or hosted on a normal HTTPS-capable service. The remote MCP endpoint should be a trusted MagmaEdit MCP deployment or secure tunnel. Do not accept arbitrary MCP URLs from callers; the current implementation uses one configured endpoint to avoid turning the bridge into an SSRF proxy.

Before public multi-user deployment, the service still needs account-aware authentication, per-user authorization, audit logging, rate limiting, secret rotation, TLS termination, and per-user MCP credentials. The bridge bearer token is transport protection, not user identity.

## OpenAI integration

The implementation uses the official OpenAI .NET SDK Responses API remote-MCP support. The SDK supports attaching a remote MCP tool to a response and passing an authorization token for the remote server. Approval policy is kept read-only by default and mutation-capable only when explicitly enabled in configuration.

See also:

- [MCP integration](MCP.md)
- [Security](SECURITY.md)
- [Architecture](ARCHITECTURE.md)
