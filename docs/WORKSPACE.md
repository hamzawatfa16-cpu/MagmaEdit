# MagmaEdit Workspace

## User data boundary

MagmaEdit keeps the editing workspace on the user's Windows machine. Authentication state, project metadata, and media ownership are separate concerns.

## Default location

The default workspace is:

`%USERPROFILE%\Videos\Content Creation\`

The application-managed folders are:

```text
Content Creation/
├── Media/       # Imported source media owned by the user
├── Projects/    # MagmaEdit project files and recovery data
├── Exports/     # Completed renders and delivery files
└── Cache/       # Rebuildable thumbnails, proxies, waveforms, and render cache
```

Cache contents are disposable. Source media is never treated as cache.

## Rules

- Do not silently move or delete user source media.
- Import must preserve the original media unless the user explicitly requests a copy/move operation.
- Projects should use relative paths when media is inside the workspace and retain enough metadata for relinking when media is elsewhere.
- Temporary files belong in `Cache` or a system temp location, never beside source media.
- The workspace root must be created explicitly by the application and must be safe to call repeatedly.
