# MagmaEdit

MagmaEdit is a Windows-first video editor designed for normal editing workflows and AI-assisted editing.

## Project status

**Foundation stage — functional editor core with Google authentication and AI integration foundations.** The repository now contains a working Windows desktop editor shell with a local video library, 9:16 preview/playback, timeline tracks and clips, reversible edit history, trim/split/remove commands, FFmpeg-backed export, a Windows installer pipeline, a verified in-app update path, and a Google-first authentication boundary. The Sprocket foundation remains under controlled audit; large upstream source import is deliberately not mixed into the repository until the dependency and licensing boundary is approved.

## Current capabilities

- Windows desktop application shell.
- Google-first account sign-in with a **Continue with Google** flow; MagmaEdit does not ask users to type a Gmail address or password.
- Windows-user-scoped encrypted session persistence through DPAPI.
- Local-first `Videos\Content Creation` workspace with Media, Projects, Exports, and Cache folders.
- Video-only media import into the managed Media library.
- Gallery search, newest/oldest sorting, and Published/Not Published filtering.
- 9:16 preview surface with first-frame preview and FFmpeg-backed playback/scrubbing.
- Timeline tracks with add/remove/split/trim editing commands.
- Shared Undo/Redo edit history.
- Real FFmpeg-backed MP4 export at the supported 1080×1920 output format.
- Windows installer packaging through GitHub Actions and Inno Setup.
- In-app stable-release updater with release provenance, size, SHA-256, executable-header, and redirect validation.
- First-party plugin hosting with manifest validation, isolation, lifecycle control, and capability gating.
- Vendor-neutral AI editing commands and read-only editor state exposed through MCP.
- Local MCP STDIO and opt-in authenticated Streamable HTTP transports.

## Product direction

- CapCut/Adobe-style editing workflow without unnecessary complexity.
- Windows desktop application first.
- Local-first media and project storage.
- A predictable `Videos\Content Creation` workspace for Media, Projects, Exports, and Cache.
- One command layer for UI edits, automation, and AI edits.
- Reliable Undo/Redo shared by user and AI actions.
- MCP-based AI integration rather than simulated mouse/keyboard control.
- Future ChatGPT/Claude/other AI-client integrations built above the same editing engine.

## Current repository structure

```text
MagmaEdit/
├── src/
│   ├── MagmaEdit.Auth/
│   ├── MagmaEdit.App/
│   ├── MagmaEdit.Core/
│   ├── MagmaEdit.Integration/
│   ├── MagmaEdit.Media.Sprocket/
│   ├── MagmaEdit.McpServer/
│   ├── MagmaEdit.Plugin.Abstractions/
│   └── MagmaEdit.PluginHost/
├── tests/
│   ├── MagmaEdit.Auth.Tests/
│   ├── MagmaEdit.Core.Tests/
│   └── MagmaEdit.PluginHost.TestPlugin/
├── docs/
├── installer/
├── .github/
│   └── workflows/
├── MagmaEdit.slnx
├── Directory.Build.props
├── .editorconfig
└── global.json
```

## Engineering rules

See [docs/ENGINEERING-RULES.md](docs/ENGINEERING-RULES.md).

The project uses nullable analysis, warnings-as-errors, analyzers, deterministic builds, formatting verification, tests, and Windows CI from the start.

## Authentication

See [docs/AUTH.md](docs/AUTH.md).

Authentication is isolated from editing and media code. OAuth is browser-based and uses PKCE. Long-lived Supabase session credentials are encrypted with the Windows current-user DPAPI scope.

## AI / MCP

See [docs/MCP.md](docs/MCP.md) and [docs/PLUGINS.md](docs/PLUGINS.md).

MagmaEdit exposes a vendor-neutral editing contract to AI clients. The MCP server provides `magmaedit.execute_editor_command` for authorized editing mutations and `magmaedit.get_editor_state` for read-only state inspection.

## Workspace

See [docs/WORKSPACE.md](docs/WORKSPACE.md).

Default workspace:

```text
%USERPROFILE%\Videos\Content Creation\
├── Media/
├── Projects/
├── Exports/
└── Cache/
```

## Open-source foundation

The current preferred foundation is [SprocketVideo/Sprocket](https://github.com/SprocketVideo/Sprocket). Its architecture is a strong fit for MagmaEdit because it already separates the editing core, media, render, audio, playback, persistence, plugins, MCP, and desktop application layers. Its repository is MIT-licensed, but its redistributed third-party dependencies have their own licenses and notices, so those remain separately audited.

See [docs/FOUNDATION-AUDIT.md](docs/FOUNDATION-AUDIT.md).