# MagmaEdit Foundation Audit

Date: 2026-08-31
Status: Initial audit

## Decision

SprocketVideo/Sprocket is the current preferred foundation for MagmaEdit.

This is a foundation decision, not a promise to copy the repository unchanged. MagmaEdit will keep only code and dependencies that pass its architecture, security, licensing, quality, and product requirements.

## Why Sprocket is a strong starting point

- .NET 10 solution split into Core, Media, Render, Audio, Playback, Export, Persistence, Plugins, MCP, and App projects.
- Core is designed as a dependency-light domain layer with no native handles.
- Editing is non-destructive and represented as project data.
- Undo/redo is based on a command stack rather than direct model mutation.
- Preview and export share the render graph.
- Persistence uses versioned JSON DTOs rather than serializing native/runtime objects.
- A plugin host already exists and is isolated from UI/media implementation details.
- An MCP project already exists and routes AI edits through the editor session/model thread and command history.
- The application is already a Windows desktop application and has installer/update infrastructure.
- The repository uses warnings-as-errors in its projects and has CI/build/test workflows.

## Things MagmaEdit must change

1. Product identity: Sprocket naming, branding, UI copy, icons, assets, and package names must be replaced with MagmaEdit branding where legally and technically appropriate.
2. Platform scope: MagmaEdit is Windows-first. Cross-platform code should not be retained merely because it exists; keep abstractions only when they reduce Windows complexity or preserve a future portability boundary.
3. Authentication: add a clean authentication boundary. Initial implementation may use a hosted identity provider such as Supabase Auth. Authentication must not own or upload user media.
4. User workspace: create a predictable local `Content Creation` directory for projects, media, exports, and cache.
5. AI integration: MagmaEdit Core remains independent of any AI vendor. MCP/API contracts sit above the editing engine.
6. ChatGPT in-product experience: build a separate hosted integration for AI clients that support MCP/App integrations. Do not expose a local editor listener directly to the public internet.
7. Security: local MCP remains opt-in, authenticated, loopback-only, and protected against browser-originated requests. Hosted bridges must use explicit authentication and least privilege.
8. Strict quality gates: every project gets nullable/reference safety, analyzers, formatting, deterministic tests, and CI checks. New dependencies require an explicit reason and license record.
9. Third-party notices: retain and maintain required notices for Sprocket-derived code and every redistributed dependency. The Sprocket MIT notice must remain with substantial copies of the original code.
10. Testing: migrate useful upstream tests first, then add MagmaEdit-specific regression tests before changing behavior.

## Current upstream structure reviewed

`Sprocket.slnx` contains separate source projects for Core, Media, Render, Audio, Playback, Export, Persistence, MCP, Plugins, and App, with matching test projects.

`Directory.Build.props` centralizes version/product metadata and configures embedded managed symbols and output cleanup.

`Sprocket.Mcp` targets .NET 10, enables nullable reference types, treats warnings as errors, and references Core/Persistence plus the official MCP C# Core SDK.

`Sprocket.App` is a WinExe, uses Avalonia, Velopack, SkiaSharp, and references the editor layers plus MCP and Plugins.

The upstream CI builds on multiple operating systems and runs the test suite on Windows with FFmpeg 8 supplied by the workflow.

## Licensing note

Sprocket's repository LICENSE is MIT. This permits modification and redistribution subject to retaining the copyright and license notice. This does not automatically relicense every third-party dependency; dependency licenses remain separate and must be tracked.

## Next audit stage

Before importing source code into MagmaEdit:

- inspect all source projects and project references;
- inventory direct and transitive dependencies;
- inspect MCP tool surface and authorization boundaries;
- inspect persistence/schema migration behavior;
- inspect plugin loading and isolation;
- inspect Windows packaging/update configuration;
- identify upstream technical debt or known failing tests;
- define the exact subset to import and the first MagmaEdit vertical slice.

## Rule

No large source copy happens until the next audit stage is complete. This keeps the repository small, reviewable, and recoverable if the foundation fails a requirement.
