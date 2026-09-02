# MagmaEdit

MagmaEdit is a Windows-first video editor designed for normal editing workflows and AI-assisted editing.

## Project status

**Foundation stage — functional editor core.** The repository now contains a working Windows desktop editor shell with a local video library, 9:16 preview/playback, timeline tracks and clips, reversible edit history, trim/split/remove commands, FFmpeg-backed export, a Windows installer pipeline, and a verified in-app update path. The Sprocket foundation remains under controlled audit; large upstream source import is deliberately not mixed into the repository until the dependency and licensing boundary is approved.

## Current capabilities

- Windows desktop application shell.
- Local-first `Videos\Content Creation` workspace with Media, Projects, Exports, and Cache folders.
- Video-only media import into the managed Media library.
- Gallery search, newest/oldest sorting, and Published/Not Published filtering.
- 9:16 preview surface with first-frame preview and FFmpeg-backed playback/scrubbing.
- Timeline tracks with add/remove/split/trim editing commands.
- Shared Undo/Redo edit history.
- Real FFmpeg-backed MP4 export at the supported 1080×1920 output format.
- Windows installer packaging through GitHub Actions and Inno Setup.
- In-app stable-release updater with release provenance, size, SHA-256, executable-header, and redirect validation.

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
│   ├── MagmaEdit.App/
│   └── MagmaEdit.Core/
├── tests/
│   └── MagmaEdit.Core.Tests/
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