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

The server requires one project path. It can be supplied as the first command-line argument:

```text
MagmaEdit.McpServer.exe "C:\path\to\Project.magmaedit.json"
```

or through:

```text
MAGMAEDIT_PROJECT_PATH=C:\path\to\Project.magmaedit.json
```

The server loads that project once, keeps the project and edit history alive for the process lifetime, and saves successful commands back to the same project path.

## AI client connection model

MagmaEdit's AI-facing integration is MCP over STDIO. An MCP-capable AI client starts `MagmaEdit.McpServer.exe` as a local MCP server and supplies the project path as an argument or environment variable.

The client can then call the two tools above. No simulated mouse or keyboard control is required.

This repository does not claim a built-in one-click connector for every AI product. Each AI product must support adding a local MCP server and may use its own configuration UI and policy.

## Current authorization model

The first implementation is intentionally local-process scoped. The STDIO process is started by the MCP client with an explicit local client identity and the current editor capabilities. This is not account authentication.

HTTP/remote deployment must not reuse this trust model. Authentication, token/session handling, and remotely scoped authorization must be added before any network-facing MCP transport is exposed.

## Architecture rule

MCP-specific transport code stays outside MagmaEdit Core. The MCP server depends on the vendor-neutral integration layer, and the integration layer depends on Core. Desktop plugins use the same vendor-neutral command contract and capability authorization boundary.
