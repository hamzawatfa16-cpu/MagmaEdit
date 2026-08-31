# MagmaEdit

MagmaEdit is a Windows-first video editor designed for normal editing workflows and AI-assisted editing.

## Project status

**Foundation stage — first executable core scaffold.** The repository now contains the first MagmaEdit-owned Core project, workspace contract, reversible edit history, tests, and Windows CI gates. The Sprocket foundation remains under controlled audit; large upstream source import is deliberately not mixed into the repository until the dependency and licensing boundary is approved.

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
│   └── MagmaEdit.Core/
│       ├── Editing/
│       └── Workspace/
├── tests/
│   └── MagmaEdit.Core.Tests/
├── docs/
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
