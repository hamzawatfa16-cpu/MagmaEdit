# MagmaEdit

MagmaEdit is a Windows-first video editor designed for normal editing workflows and AI-assisted editing.

## Project status

Early architecture stage. The repository is intentionally kept small until the upstream foundation and licensing are fully audited.

## Goals

- Clean, maintainable Windows desktop video editor.
- CapCut/Adobe-style workflow without unnecessary complexity.
- Local-first media and project storage.
- A `Content Creation` workspace for user media, projects, and exports.
- A command-based editing core with reliable Undo/Redo.
- AI control through an integration layer based on MCP rather than simulated mouse/keyboard automation.
- Strict formatting, analysis, tests, dependency checks, and CI gates.

## Engineering rules

1. Do not add a dependency without a documented reason.
2. Do not mix UI, editing-domain logic, media/codec logic, authentication, and AI integration in the same project/module.
3. Every user-visible feature must have tests at the appropriate layer.
4. Warnings and static-analysis findings are treated as errors where practical.
5. CI is a required gate before merging.
6. Third-party licenses and notices must be tracked as dependencies change.
7. No secrets are committed to Git.

## Foundation audit

The current candidate foundation is SprocketVideo/Sprocket. It is being audited before any source is copied or adapted.

The audit covers architecture, build requirements, tests, MCP implementation, packaging, storage, dependencies, and licenses.

## Repository structure

The final structure will follow the dependency boundaries of the chosen foundation rather than forcing a premature layout.
