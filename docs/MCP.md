# MagmaEdit MCP integration

MagmaEdit contains a MagmaEdit-owned MCP server boundary and a local STDIO server implementation.

## What is implemented

The MCP server exposes two stable tools:

```text
magmaedit.execute_editor_command
magmaedit.get_editor_state
```

`magmaedit.execute_editor_command` accepts the vendor-neutral `EditorCommandRequest` contract and routes the request through validation, capability authorization, the shared editor command gateway, and persistence.

`magmaedit.get_editor_state` returns a read-only `EditorProjectState` snapshot containing project metadata, media, timeline clips, and undo/redo counts.

The current MCP server targets the `2026-07-28` MCP protocol through the official C# SDK 2.0 line. The SDK's STDIO transport and tool-schema generation are used without putting MCP-specific code into MagmaEdit Core.

## Local server configuration

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

When no desktop session is available, the MCP server falls back to the configured project path. A project path is therefore optional only when a live MagmaEdit desktop session is available.

## AI client connection model

MagmaEdit's AI-facing integration is MCP over STDIO. An MCP-capable AI client starts `MagmaEdit.McpServer.exe` as a local MCP server. The server automatically targets the running MagmaEdit desktop session when its local IPC endpoint is available.

The client can then call the two tools above. No simulated mouse or keyboard control is required.

This repository does not claim a built-in one-click connector for every AI product. Each AI product must support adding a local MCP server and may use its own configuration UI and policy.

## Current authorization model

The first implementation is intentionally local-process scoped. The STDIO process is started by the MCP client and the desktop IPC endpoint accepts only the current Windows user. This is not account authentication.

HTTP/remote deployment must not reuse this trust model. Authentication, token/session handling, and remotely scoped authorization must be added before any network-facing MCP transport is exposed.

## Architecture rule

MCP-specific transport code stays outside MagmaEdit Core. The MCP server depends on the vendor-neutral integration layer, and the integration layer depends on Core. Desktop plugins and the live desktop bridge use the same vendor-neutral command contract and capability authorization boundary.
