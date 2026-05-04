# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## 2.0.2

- Fixed opening Unity asset usages from Rider by serializing bridge commands and retrying transient pipe failures.
- Added the project source link to the Rider plugin description.

## 2.0.1

- Fixed Unity asmdef rename windows after namespace split by importing the bridge logging namespace.

## 2.0.0

- Added persistent Rider settings under `Settings | Tools | Serialize Reference Dropdown`.
- Added structured Unity asset usage preview with clickable asset rows.
- Added Rider-to-Unity JSON bridge responses and Unity asset opening.
- Added missing Unity bridge package warning.
- Added namespace rename support for Unity YAML SerializeReference entries.
- Added explicit opt-in UI for writing scanned Unity YAML changes to disk.
- Added Unity-side asmdef rename and SerializeReference assembly retargeting tools.
- Added clearer Unity asset scan diagnostics.
- Reduced release diagnostics noise and removed the Rider-side asmdef rename entry point.

## 1.1.1

- Added switch-to-Unity-window support on macOS.

## 1.1.0

- Added Unity bridge named-pipe backend.

## 1.0.0

- Initial version.
