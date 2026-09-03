# MagmaEdit MCP integration

MagmaEdit contains a MagmaEdit-owned MCP server boundary and two supported transports: local STDIO and opt-in Streamable HTTP.

## What is implemented

The MCP server exposes two stable tools:

```text
magmaedit.execute_editor_command
magmaedit.get_editor_state
```

`magmaedit.execute_editor_command` accepts the vendor-neutral `EditorCommandRequest` contract and routes the request through validation, capability authorization, the shared editor command gateway, and persistence.

`magmaedit.get_editor_state` returns a read-only `EditorProjectState` snapshot containing project metadata, media, timeline clips, and undo/redo counts.

The MCP server targets the `2026-07-28` MCP protocol through the official C# SDK 2.2.0 line. The selected MCP transport and tool-schema generation stay outside MagmaEdit Core.

## Local project configuration

The server can be started with an optional project path:

```text
MagmaEdit.McpServer.exe "C:\path\to\Project.magmaedit.json"
```

or:

```text
MAGMAEDIT_PROJECT_PATH=C:\path\to\Project.magmaedit.json
```

When the MagmaEdit desktop application is running, the MCP process first connects to the live desktop session over the current-user-only Windows named pipe:

```text
MagmaEdit.LiveEditor.v1
```

Live commands mutate the same in-memory project used by the open editor, are persisted through the desktop application's save path, and then refresh the editor UI. The shared automation command authorization remains the enforcement boundary.

Each authenticated HTTP MCP request now carries the Supabase user ID from the trusted AI bridge through `X-MagmaEdit-User-Id`. MCP binds that identity to the current request flow and includes it in live-editor IPC requests. The desktop application can enforce `MAGMAEDIT_DESKTOP_USER_ID` so only the expected account may control that desktop session.

The per-user session broker and transport boundary are documented in [`SESSION-TRANSPORT.md`](SESSION-TRANSPORT.md). The current transport registry is intentionally an in-memory development implementation; production multi-user hosting still requires durable shared state and authenticated outbound desktop connectivity.

When no desktop session is available, the MCP server falls back to the configured project path. A project path is therefore optional only when a live MagmaEdit desktop session is available.

## STDIO transport

STDIO is the default transport. An MCP-capable desktop AI client starts `MagmaEdit.McpServer.exe` as a local MCP server. The server automatically targets the running MagmaEdit desktop session when its local IPC endpoint is available.

No simulated mouse or keyboard control is required. The AI client calls the two typed tools directly.

## Streamable HTTP transport

HTTP is opt-in and is intended for remote-capable MCP clients or secure tunnel deployments. Enable it with:

```text
MAGMAEDIT_MCP_TRANSPORT=streamable-http
MAGMAEDIT_MCP_HTTP_BEARER_TOKEN=<strong-secret-token>
```

The default bind URL is:

```text
http://127.0.0.1:3001
```

Override it with:

```text
MAGMAEDIT_MCP_HTTP_URL=http://127.0.0.1:3001
```

The MCP endpoint is `/mcp` and uses stateless Streamable HTTP.

The HTTP server requires a bearer token on every `/mcp` request and validates both the `Host` and `Origin` headers before the MCP handler runs. This protects the local/default deployment against DNS-rebinding and cross-origin browser requests. The MCP security guidance recommends Host/Origin validation for Streamable HTTP servers. citeturn237569search0turn237569search1

For a hosted deployment, explicitly configure the allowlists:

```text
MAGMAEDIT_MCP_HTTP_ALLOWED_HOSTS=mcp.example.com,mcp.example.com:443
MAGMAEDIT_MCP_HTTP_ALLOWED_ORIGINS=https://app.example.com
```

Do not expose the local HTTP listener directly to the public internet. Use TLS and a trusted network boundary or secure tunnel, and keep authentication and authorization scoped to the actual client/account rather than treating a bearer token as account authentication.

## AI client connection model

MagmaEdit does not claim a built-in one-click connector for every AI product. Each AI product must support MCP and may use its own configuration UI and policy.

For ChatGPT specifically, a local/private MCP server is not connected directly; OpenAI's current guidance requires a remote MCP server or a secure MCP tunnel for a server running on a private network or developer machine. citeturn721867search0

## Current authorization model

The local STDIO process is intentionally local-process scoped. The desktop IPC endpoint accepts only the current Windows user. This is not account authentication.

For Streamable HTTP, the bridge authenticates the Supabase user and propagates that identity to MCP; MCP refuses requests without a user identity and the desktop pipe can enforce the configured account identity. The MCP bearer token still protects the transport and must remain secret.

This is the identity-propagation layer, not the final multi-tenant session broker. A future hosted deployment still needs a durable mapping from each account to its own MCP endpoint or desktop session, plus shared/distributed rate limiting, durable audit storage, secret rotation, TLS termination, and production monitoring.

## Architecture rule

MCP-specific transport code stays outside MagmaEdit Core. The MCP server depends on the vendor-neutral integration layer, and the integration layer depends on Core. Desktop plugins and the live desktop bridge use the same vendor-neutral command contract and capability authorization boundary.
