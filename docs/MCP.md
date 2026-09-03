# MagmaEdit MCP integration

MagmaEdit now contains a MagmaEdit-owned MCP server boundary and a local STDIO server implementation.

## What is implemented

The MCP server exposes one stable tool:

```text
magmaedit.execute_editor_command
```

The tool accepts the vendor-neutral `EditorCommandRequest` contract and routes it through the same editor command and undo/redo path used by MagmaEdit automation. Capability authorization happens before the editor router is reached.

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

## Current authorization model

The first implementation is intentionally local-process scoped. The STDIO process is started by the MCP client with an explicit local client identity and the current editor capabilities. This is not account authentication.

HTTP/remote deployment must not reuse this trust model. Authentication, token/session handling, and remotely scoped authorization will be added before any network-facing MCP transport is exposed.

## Architecture rule

MCP-specific transport code must stay outside MagmaEdit Core. The MCP server depends on the vendor-neutral integration layer, and the integration layer continues to depend on Core. This keeps future ChatGPT, Claude, plugin, and other client integrations on one editor command path.
