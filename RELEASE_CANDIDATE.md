# MagmaEdit v1

This is the first direct Windows installer release of MagmaEdit.

## Included

- Windows x64 self-contained application
- Inno Setup installer
- Bundled FFmpeg 8 runtime
- Video-only 9:16 media library
- Timeline insert, split, trim, undo and redo
- MP4 export
- Published / Not Published state
- Search and gallery sorting

## Installation

Download `MagmaEdit-1.0.0-Setup.exe` from the GitHub Releases page and run it normally. The installed app does not require a terminal.

## Test focus

Please test installation, launching without a terminal, importing 9:16 videos, preview, timeline editing, undo/redo, export, and gallery removal behavior.

The v1 release is built directly by GitHub Actions and publishes the installer as a GitHub Release asset. The release workflow validates formatting, builds, prepares FFmpeg, runs the test suite, publishes the Windows app, and builds the installer before creating the release.
