# MagmaEdit Engineering Decisions

## 001 — Windows-first

MagmaEdit targets Windows first. Cross-platform support is not a release requirement for the initial product.

## 002 — Command-based editing

All edits flow through a shared command layer. The command layer owns validation and Undo/Redo behavior and is the only supported path for AI-driven editing.

## 003 — Local-first media

Original user media and editable projects stay on the user's computer by default. Cloud services are not part of the media-storage core.

## 004 — Authentication is replaceable

The product may use Supabase Auth or another established provider. Authentication is intentionally isolated so the provider can be changed without redesigning the editor core.

## 005 — AI integration is provider-agnostic

MagmaEdit exposes editing capabilities through a stable integration boundary. ChatGPT, Claude, Grok, and future clients are adapters/clients rather than dependencies of the editing engine.

## 006 — Open-source foundation policy

An upstream project may be used as a foundation only after its license, dependencies, notices, build process, security posture, and architecture are audited. We do not assume that an upstream repository's main license covers every distributed dependency.

## 007 — Quality-first repository

Formatting, static analysis, unit tests, integration tests, dependency/license checks, and build verification are repository requirements. CI must enforce them.
