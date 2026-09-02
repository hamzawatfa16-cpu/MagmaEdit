# MagmaEdit Security

## Application update trust boundary

MagmaEdit updates only from the project's official GitHub Releases endpoint and only accepts a stable, published release.

The updater requires all of the following before installing an update:

- The release tag starts with `v` and matches the version in the expected installer filename.
- The installer filename is exactly `MagmaEdit-<version>.Setup.exe`.
- The release and asset uploader are both `github-actions[bot]`.
- The installer URL uses HTTPS and matches the exact MagmaEdit GitHub release-download path.
- The declared installer size is positive and within the updater's 250 MiB safety limit.
- The downloaded byte count exactly matches the release asset size.
- The downloaded installer SHA-256 exactly matches the digest published in the GitHub release metadata.
- The downloaded file begins with the Windows PE `MZ` signature before it is executed.

Updates are downloaded to a unique temporary directory and are deleted when download or verification fails.

## Credentials and user data

MagmaEdit does not store authentication tokens in the project workspace. User media and project files remain local-first under the Content Creation workspace.

## Current signing status

The Windows installer is currently not Authenticode-signed. SHA-256 verification protects the update path against an altered download when the trusted GitHub release metadata is intact, but it is not equivalent to publisher code signing. Authenticode signing should be added before broad public distribution.

## Reporting

Security problems should be reported privately to the repository maintainer before public disclosure whenever practical. Do not include passwords, tokens, private media, or other sensitive user data in issue reports.
