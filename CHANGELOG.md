# Changelog

## 1.0.2

- Corrected the in-app updater to recognize the actual `MagmaEdit-<version>-Setup.exe` Windows installer filename.
- Added regression coverage for the real GitHub Releases installer naming contract.
- Synchronized the application and Windows release workflow to version 1.0.2.

## 1.0.1

- Added the in-app Update action for stable Windows releases.
- Added release metadata, installer provenance, size, SHA-256, and PE-header validation before update installation.
- Hardened persisted-project and timeline validation failure handling.
- Preserved the normal Windows installer workflow without requiring a terminal.
