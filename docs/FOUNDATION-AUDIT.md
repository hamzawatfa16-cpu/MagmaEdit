# MagmaEdit Foundation Audit

Date: 2026-09-03
Status: Current-state audit

## Decision

SprocketVideo/Sprocket remains the current technical foundation for MagmaEdit, but MagmaEdit is being built as its own product boundary rather than as an unchanged copy.

The repository must stay small, reviewable, and strict. We will keep upstream code only where it satisfies MagmaEdit architecture, security, licensing, quality, and product requirements.

## Current MagmaEdit state

MagmaEdit currently contains:

- A Windows Avalonia desktop application.
- A MagmaEdit Core domain layer for projects, timeline editing, media, and undo/redo.
- A Sprocket-backed media probe/export boundary.
- A shared `IEditorCommandGateway` so editor mutations can be exposed through one command path.
- Undoable media-collection and timeline operations with regression tests.
- A vendor-neutral `MagmaEdit.Integration` command contract and router for future AI/plugin clients.
- Strict nullable/analyzer/format/build/test gates in CI, including Windows publishing and installer creation.
- A pinned Sprocket submodule whose revision is verified by CI.

## Not yet implemented

The following are planned but are not currently present as finished MagmaEdit-owned features:

1. Authentication and account/session UI.
2. A MagmaEdit-owned MCP server/tool surface.
3. A MagmaEdit-owned plugin host/API and plugin lifecycle model.
4. A hosted AI bridge for external AI clients.
5. Complete migration of the desktop UI away from direct project-model mutations and its private history instance.
6. Full replacement of upstream/Sprocket implementation details behind MagmaEdit-owned interfaces.
7. Final product branding, assets, packaging identity, and release polish.

These items must not be described as already implemented until the corresponding code and tests exist in this repository.

## Architecture direction

MagmaEdit Core stays independent of AI vendors, UI frameworks, network services, and authentication providers.

The intended dependency direction is:

`MagmaEdit.App` -> `MagmaEdit.Integration` / `MagmaEdit.Core`

`MagmaEdit.Integration` -> `MagmaEdit.Core`

`MagmaEdit.Media.Sprocket` -> `MagmaEdit.Core` + upstream Sprocket implementation

Future MCP/plugin/hosted integrations must depend on the vendor-neutral command boundary rather than introducing vendor-specific concepts into Core.

## Strict engineering rules

- Nullable/reference safety remains enabled.
- Warnings are errors.
- Formatting is validated in CI.
- Builds and tests must remain deterministic and green before the next architectural step.
- New dependencies require an explicit reason and license review.
- Editing mutations should flow through the shared command gateway so UI and automation use the same history semantics.
- Third-party notices and required licenses must remain with redistributed upstream code.
- No large upstream source copy is allowed merely for convenience; import only code that passes the audit.

## Upstream/Sprocket boundary

Sprocket is currently used as a foundation and submodule, not as MagmaEdit's public product identity.

MagmaEdit should progressively replace direct dependence on upstream application concepts with MagmaEdit-owned contracts. This should happen in small, testable steps so each replacement can be validated by CI.

## Next build stages

The next implementation work should proceed in this order:

1. Complete the command-gateway migration in the desktop application so direct timeline/media mutations are removed from UI code where an equivalent gateway operation exists.
2. Strengthen the vendor-neutral integration boundary with explicit validation and capability metadata needed by future plugins and MCP tools.
3. Define the MagmaEdit-owned plugin contract and lifecycle without loading third-party plugins into the Core process.
4. Define the MagmaEdit-owned MCP tool contract and authorization boundary.
5. Build authentication/session infrastructure after the local editor command path is stable.
6. Gradually replace or isolate remaining upstream implementation details while maintaining required licenses/notices.

## Rule

Every architectural step must leave the repository buildable, testable, and understandable. Do not claim ownership of a subsystem until its MagmaEdit-owned boundary and implementation are actually present.
