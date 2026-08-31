# MagmaEdit Architecture

## Status

Architecture decision draft. No implementation is approved until the upstream foundation audit is complete.

## Core principle

MagmaEdit must have one authoritative editing model. Desktop UI, internal automation, and external AI integrations must call the same command layer so edits behave consistently and remain undoable.

```text
Desktop UI ───────────────┐
                          │
Internal automation ──────┼──> Editing Commands ──> Project Model
                          │            │                  │
External AI / MCP ────────┘            └──> Undo/Redo     └──> Persistence

Media/Codec Layer <──────────────────── Project/Timeline
Render/Preview <─────────────────────── Project/Timeline
Export <─────────────────────────────── Project/Timeline
```

## Boundaries

### Application
Windows shell, startup, routing, dialogs, settings, filesystem integration, installer/update integration.

### Presentation
Editor UI, media browser, timeline UI, inspector, preview, commands and user interactions. Presentation code must not contain codec or persistence implementation details.

### Editing domain
Project, timeline, tracks, clips, effects, markers, time model, commands, validation, undo/redo. This layer is the source of truth for editing behavior and should remain independent from UI concerns.

### Media
Import/probing, thumbnails, decoding, proxy media, waveform generation, media metadata, relinking, and media lifecycle.

### Render
Preview composition, effects, frame scheduling, caching, and render graph behavior shared with export where the upstream architecture supports it.

### Export
Final rendering and delivery formats/presets. Export must consume the same authoritative project state as preview.

### AI integration
MagmaEdit command tools exposed through MCP and future integration adapters. AI integrations must never modify editor state by directly manipulating UI controls.

### Authentication
Account/session handling must be isolated from editing and media code. Authentication may use a hosted provider such as Supabase if the final product requirements justify it, while project/media data remains local-first.

## Storage principle

MagmaEdit will maintain a user-owned local workspace containing media, projects, exports, and application-managed cache. Exact paths are platform-defined and must follow Windows conventions.

## Security principle

Secrets and session credentials must use the safest appropriate OS-backed storage available to the chosen stack. Plain-text application files must not be used for long-lived secrets unless there is a documented, reviewed reason.

## Quality gates

Every implementation language used by MagmaEdit must use its strongest practical strictness settings, formatting, static analysis, unit/integration tests, and CI enforcement.

A change is not complete when it merely compiles. It must pass the repository's required quality gates.
