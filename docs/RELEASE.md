# MagmaEdit Release Policy

MagmaEdit publishes the Windows installer as a direct `.exe` asset on GitHub Releases.

Custom ZIP build artifacts are not part of the user installation path.

The application updater checks the official latest stable MagmaEdit GitHub release, validates its release metadata and installer provenance, verifies the published SHA-256 digest, and then launches the verified installer.

The first version containing the in-app updater is **1.0.1**. Users running 1.0.0 must install 1.0.1 once from GitHub Releases. After that, compatible future releases can be installed from the MagmaEdit **Update** button without manually downloading each installer.

The 1.0.1 release is the verification release for the in-app update system.
