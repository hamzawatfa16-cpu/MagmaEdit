# Engineering Rules

MagmaEdit is built as a strict, reviewable software project. Correctness and maintainability are requirements, not cleanup work for later.

## Required for every change

1. Keep modules small and single-purpose.
2. Preserve one-way dependency direction.
3. Enable nullable reference analysis.
4. Treat compiler warnings as errors.
5. Run analyzers during builds.
6. Keep formatting deterministic and CI-verified.
7. Add or update tests for changed behavior.
8. Never commit credentials, tokens, user media, generated build output, or machine-specific paths.
9. Document every new runtime dependency and its license.
10. Do not bypass the command layer for editing mutations.

## Review rule

A feature is not complete when it only works on the developer machine. It is complete when the code builds, tests pass, formatting is clean, and the relevant failure cases are covered.

## Upstream code rule

Code adapted from Sprocket or another open-source project must preserve the applicable license and notices, remain traceable to its source, and be changed deliberately rather than mixed into unrelated original code.
