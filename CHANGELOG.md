# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project
uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-11-18

### Added

- **Linux and macOS support** for root folders, virtual paths and file names. Names containing
  `:` or `..` (for example `meeting:2026.txt` or `report..final.txt`) now work, while `..` used
  as a path segment is still rejected.
- **Markdown preview** for `.md` / `.markdown` files: headings, lists, task lists, tables, code
  fences, quotes, relative images and links, with a Preview / Source switch. Raw HTML is escaped
  and unsafe URL schemes are blocked.
- `MarkdownRendererTests` (renderer and URL resolution) and `PathCompatibilityTests`
  (platform-specific path rules and case-sensitive ACL matching).

### Changed

- Path validation, ACL caching and upload-name handling are platform-aware: Windows keeps
  case-insensitive matching and rejects `:` / `\`, Linux and macOS match case-sensitively and
  treat those characters as ordinary name characters.
- Absolute root paths containing `=` (for example `/srv/a=b`) are no longer parsed as
  `name=path`.
- The demo uses `App_Data/demo-files` and `App_Data/acl-demo` instead of hard-coded Windows
  drives, so it runs unchanged on all supported platforms.
- Documentation now covers Linux/macOS root syntax and the exact API error contract; added
  `CONTRIBUTING.md`, `SECURITY.md`, this changelog and third-party notices.

### Fixed

- Files and folders whose names contain `..` no longer make a directory unreadable or fail to
  upload.
- Case-different folders (`/public` vs `/Public`) no longer share ACL rules on Linux/macOS.
- Uploaded file names containing `:` are handled correctly on Unix hosts.

## [1.0.0] - 2026-11-17

Initial public release.

- Windows Explorer-style UI: ribbon, folder tree, breadcrumb, seven view layouts, virtual
  scrolling, per-instance state.
- Multiple instances and multiple root folders per instance.
- Server-enforced folder ACLs (path wildcards, deny-wins, file-type filters, quotas).
- Chunked resumable uploads, downloads and ZIP packaging, archive create/extract with progress
  jobs, recursive search.
- Image, video, audio, PDF, text (encoding detection) and optional LibreOffice Office previews.
- Nine UI languages, optional system icons, vanilla JavaScript with no external dependencies.
