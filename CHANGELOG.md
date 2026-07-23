# Changelog

All notable changes to Copilot Session Tracker are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.1.0] - Unreleased

GitHub Release will be published as **v1.1.0** after this branch is merged and the
signed release workflow runs on the self-hosted runner.

### Added

- **Ignore working directories** in Settings: multi-line list of paths whose sessions
  are hidden from the main table.
- Path matching is case-insensitive and tolerant of separators / trailing slashes;
  sessions under a listed root are also ignored.
- Status line reports how many sessions are currently hidden by the ignore list.
- Shared filter logic in `SessionDirectoryFilter` with unit tests.
- WinUI multiline TextBox load fix so reopening Settings shows every saved path
  (`AcceptsReturn` before `Text` + `\r` line endings).

### Changed

- App and installer version bumped to **1.1.0**.
- Settings persistence now stores ignored directories alongside the terminal command
  template in `%LOCALAPPDATA%\CopilotSessionTracker\settings.json`.

## [1.0.0] - 2026-07-13

### Added

- Initial public release: WinUI 3 session browser for local GitHub Copilot CLI sessions.
- Session table (name, working directory, short id, last activity).
- Terminal launch with configurable command template.
- Peek dialog for recent conversation turns.
- Search, refresh, signed installer and portable ZIP via release pipeline.

[1.1.0]: https://github.com/rogerbarreto/copilot-session-tracker/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/rogerbarreto/copilot-session-tracker/releases/tag/v1.0.0
