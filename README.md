# Copilot Session Tracker

A small **WinUI 3** desktop app for Windows that lists your **local GitHub Copilot CLI
sessions** and lets you jump back into any of them.

It reads the data the Copilot CLI keeps under `~/.copilot`:

- the session folders under `~/.copilot/session-state` (the set of *local* sessions), and
- `~/.copilot/session-store.db` (session metadata + recorded conversation turns).

Only **real, resumable sessions are listed**. A session folder is considered real when it
contains an `events.jsonl` conversation log. Some folders under `session-state` are empty
stub/marker folders (just a `workspace.yaml`, `client_name: github/autopilot`, no
`mc_session_id`) that a sync process pre-registers but where no conversation ever happened
locally — `copilot --resume` rejects those with *"No session, task, or name matched"*, so
the app filters them out.

## Features

- **Session table** — one row per local session showing:
  - **Session name** (from `workspace.yaml`, matching what Copilot CLI shows; falls back to
    the DB `summary` for auto-titled sessions; `(unnamed session)` if none) with the
    **working directory** as a small line beneath it (repository is shown in the tooltip)
  - **Session id** — a short id shown as a clickable link; **click it to copy the full
    GUID to the clipboard** (a brief "Copied!" confirmation appears)
  - **Last activity** — when you last actually interacted with the session
  - Rows are **ordered by last activity, most recent first**. "Last activity" is the
    timestamp of the most recent conversation turn (from the `turns` table), which
    reflects real work far better than the folder/DB `updated_at` (that gets bulk-bumped
    whenever the CLI syncs). Sessions with no turns fall back to `updated_at`.
- **Terminal** button — opens a *standalone* terminal (Windows Terminal if available,
  otherwise `cmd.exe`) that runs a **configurable command template** for the session.
  The template is edited in the **Settings** dialog (⚙ button, top-right) and persisted;
  it supports two tokens:
  - `{id}` — the session id
  - `{cwd}` — the session's working directory

  Everything else is passed through verbatim, so you can add any flags you like. The
  default is:

  ```text
  copilot --resume={id} --yolo --prefer-version 1.0.60
  ```

  (`--prefer-version` pins the CLI version the terminal launches; drop it or change the
  version as needed. Use **Reset to default** in Settings to restore it.)

- **Peek** button — a quick look at a session without opening it: **created** /
  **last-activity** timestamps, the number of turns, and the **last 5 user → assistant
  round trips**.
- **Search** box to filter by name, id, working directory or repository.
- **Refresh** to reload after new sessions appear.

Settings (the command template) are stored in
`%LOCALAPPDATA%\CopilotSessionTracker\settings.json`.

## Requirements

- Windows 10 (build 17763+) or Windows 11
- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- The [Windows App SDK 1.7 runtime](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)
  (installed automatically with most dev setups; the app is framework-dependent)
- The GitHub Copilot CLI (`copilot`) on your `PATH` for the Terminal button

## Build & run

WinUI apps target a specific architecture, so pass a runtime identifier:

```powershell
cd src/CopilotSessionTracker
dotnet build -r win-x64 --tl:off
dotnet run   -r win-x64 --tl:off
```

Or open `CopilotDashboard.sln` in Visual Studio 2022/2026 and press **F5**. The solution
maps the `Any CPU` platform to `x64`, so debugging works out of the box (you can also
pick `x64`, `x86` or `ARM64` from the platform dropdown).

### Publish a standalone build

To produce a self-contained folder that runs without a separate Windows App SDK install:

```powershell
cd src/CopilotSessionTracker
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None
```

## How it reads the database safely

The Copilot CLI may be actively writing to `session-store.db` (WAL mode). To read a
consistent snapshot without contending with the CLI, the app copies the database and its
`-wal`/`-shm` sidecar files to a temporary location (opened with
`FileShare.ReadWrite`), reads from the copy, and deletes it afterwards. It never writes
to your real session store.

## CI

Pull requests and pushes to `main` run GitHub Actions on `windows-latest`:

- `dotnet build src/CopilotSessionTracker/CopilotSessionTracker.csproj -c Release -r win-x64`
- `dotnet test tests/CopilotSessionTracker.Tests/CopilotSessionTracker.Tests.csproj -c Release`

## Project layout

```text
src/CopilotSessionTracker.Core/
  SessionNameResolver.cs     Display-name logic shared with tests
  WorkspaceYamlReader.cs     Reads flat workspace.yaml metadata
src/CopilotSessionTracker/
  App.xaml(.cs)              Application entry point
  MainWindow.xaml(.cs)       Session table + Peek dialog
  Models/                    SessionInfo, ConversationTurn
  Services/
    SessionStore.cs          Reads session-state folders + session-store.db
    DatabaseSnapshot.cs      Safe read-only snapshot of the live SQLite DB
    TerminalLauncher.cs      Runs the configurable command template in a terminal
    AppSettings.cs           Persists the command template (LOCALAPPDATA JSON)
  ViewModels/MainViewModel.cs
tests/CopilotSessionTracker.Tests/
  SessionNameResolverTests.cs
  WorkspaceYamlReaderTests.cs
```
