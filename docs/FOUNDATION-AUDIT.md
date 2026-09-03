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
- A vendor-neutral `MagmaEdit.Integration` command contract, validation, capability metadata, authorization, and routing for AI/plugin clients.
- Google-first browser authentication with PKCE and Windows current-user DPAPI session persistence.
- A MagmaEdit-owned MCP editor tool contract with local STDIO and authenticated Streamable HTTP transports.
- A MagmaEdit-owned plugin abstraction and collectible plugin host with manifest validation, per-plugin data directories, capability gating, lifecycle handling, and regression tests.
- Strict nullable/analyzer/format/build/test gates in CI, including Windows publishing and installer creation.
- A pinned Sprocket submodule whose revision is verified by CI.

## Not yet implemented

The following are planned but are not currently present as finished MagmaEdit-owned features:

1. A hosted AI bridge and polished end-to-end ChatGPT/Claude/Grok client experience.
2. Complete production hardening of the plugin host, including broader failure-safe lifecycle and isolation coverage.
3. Final product branding, assets, packaging identity, onboarding, and release polish.
4. Full replacement of upstream/Sprocket implementation details behind MagmaEdit-owned interfaces.
5. A professional timeline interaction layer: playhead, drag/drop editing, snapping, zoom, multi-select, and richer track interaction.
6. Broader editing features beyond the current foundation: text/overlays, images, audio, transitions, effects, speed/volume controls, crop/position controls, and expanded export options.

The implemented authentication and MCP transport features above must not be described as unfinished elsewhere in the repository without a specific code-level reason.

## Architecture direction

MagmaEdit Core stays independent of AI vendors, UI frameworks, network services, and authentication providers.

The intended dependency direction is:

`MagmaEdit.App` -> `MagmaEdit.Integration` / `MagmaEdit.Core`

`MagmaEdit.Integration` -> `MagmaEdit.Core`

`MagmaEdit.Media.Sprocket` -> `MagmaEdit.Core` + upstream Sprocket implementation

`MagmaEdit.PluginHost` -> `MagmaEdit.Plugin.Abstractions`

MCP transports, plugins, and future hosted AI integrations must depend on the vendor-neutral command boundary and its authorization layer rather than introducing vendor-specific concepts into Core.

## Strict engineering rules

- Nullable/reference safety remains enabled.
- Warnings are errors.
- Formatting is validated in CI.
- Builds and tests must remain deterministic and green before the next architectural step.
- New dependencies require an explicit reason and license review.
- Editing mutations should flow through the shared command gateway so UI and automation use the same history semantics.
- Third-party notices and required licenses must remain with redistributed upstream code.
- No large upstream source copy is allowed merely for convenience; import only code that passes the audit.
- External automation clients must receive explicit capabilities before their commands reach the editor router.
- MCP transports must use the MagmaEdit-owned command/tool contract so protocol changes do not leak into Core.

## Upstream/Sprocket boundary

Sprocket is currently used as a foundation and submodule, not as MagmaEdit's public product identity.

MagmaEdit should progressively replace direct dependence on upstream application concepts with MagmaEdit-owned contracts. This should happen in small, testable steps so each replacement can be validated by CI.

## Next build stages

The next implementation work should proceed in this order:

1. Harden the MagmaEdit-owned plugin host with failure-safe lifecycle cleanup and additional isolation tests.
2. Build the hosted AI bridge and complete the end-to-end external AI editing experience on top of the existing MCP/command boundary.
3. Improve the professional timeline interaction layer without reintroducing unnecessary complexity or accidental cross-track behavior.
4. Add the next editing feature set: text/overlays, images, audio, transitions/effects, speed/volume, crop/position, and richer export controls.
5. Gradually replace or isolate remaining upstream implementation details while maintaining required licenses/notices.
6. Finish product branding, onboarding, settings, packaging identity, and release polish.

## Rule

Every architectural step must leave the repository buildable, testable, and understandable. Do not claim ownership of a subsystem until its MagmaEdit-owned boundary and implementation are actually present.
