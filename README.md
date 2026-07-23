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
- **Ignore working directories** — in the **Settings** dialog you can list working
  directories (one path per line) whose sessions should be **hidden** from the list. A
  session is hidden when its working directory equals, or lives under, any listed path.
  Matching is case-insensitive and tolerant of separator/trailing-slash differences, so
  you can paste paths however you like. The status line shows how many sessions are
  currently ignored.
- **Refresh** to reload after new sessions appear.

Settings (the command template and the ignored working directories) are stored in
`%LOCALAPPDATA%\CopilotSessionTracker\settings.json`.

## Download

- [Latest release](../../releases/latest) — signed installer and portable ZIP (when published)
- **Installer:** `CopilotSessionTracker-Setup.exe`
- **Portable:** `CopilotSessionTracker-win-x64.zip`

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

```powershell
./scripts/publish.ps1
# Output: ./publish/
```

### Local installer (unsigned)

```powershell
./install.ps1
# Builds publish/ + installer-output/CopilotSessionTracker-Setup.exe
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

## Release and code signing

Releases follow the same pattern as [Copilot Booster](https://github.com/rogerbarreto/copilot-booster):
validation runs on GitHub-hosted runners; signing and publishing run on your self-hosted
runner. Sign in to SimplySign on the runner machine before starting the workflow — the
pipeline only checks that a code-signing certificate is already available.

### One-time setup

1. **Self-hosted runner** at `D:\actions-runner`, registered to this repo as
   `copilot-session-tracker`. To register a fresh machine:

   ```powershell
   cd D:\actions-runner
   .\config.cmd --unattended --url https://github.com/rogerbarreto/copilot-session-tracker --token <RUNNER_TOKEN>
   .\run.cmd
   ```

   Generate `<RUNNER_TOKEN>` from **Settings → Actions → Runners → New self-hosted runner**.
   To serve multiple repos from one machine, install a second runner in a separate folder.

2. **Signing machine prerequisites** on that runner host:
   - Certum SimplySign Desktop
   - Inno Setup 6
   - Windows SDK / Visual Studio build tools (`signtool.exe`)

3. **Optional:** create a `release-signing` environment in GitHub (**Settings →
   Environments**) if you want a manual approval gate before signing starts.

### Release flow

1. Merge to `main`.
2. Bump `Version` in `src/CopilotSessionTracker/CopilotSessionTracker.csproj` if needed.
3. Tag and push:

   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```

4. The `Release` workflow validates the tag (build + tests).
5. On the runner machine, sign in to **SimplySign** so the code-signing certificate is active.
6. Open **Actions → Release → Run workflow** and enter **tag:** `v1.0.0`.
7. The self-hosted runner publishes, signs the EXE and installer, and creates the GitHub
   Release with:
   - `CopilotSessionTracker-Setup.exe` (signed)
   - `CopilotSessionTracker-win-x64.zip` (signed portable build)

## Project layout

```text
src/CopilotSessionTracker.Core/
  SessionNameResolver.cs     Display-name logic shared with tests
  SessionDirectoryFilter.cs  Ignore-list path matching shared with tests
  WorkspaceYamlReader.cs     Reads flat workspace.yaml metadata
src/CopilotSessionTracker/
  App.xaml(.cs)              Application entry point
  MainWindow.xaml(.cs)       Session table + Peek dialog
  Models/                    SessionInfo, ConversationTurn
  Services/
    SessionStore.cs          Reads session-state folders + session-store.db
    DatabaseSnapshot.cs      Safe read-only snapshot of the live SQLite DB
    TerminalLauncher.cs      Runs the configurable command template in a terminal
    AppSettings.cs           Persists command template + ignored dirs (LOCALAPPDATA JSON)
  ViewModels/MainViewModel.cs
tests/CopilotSessionTracker.Tests/
  SessionNameResolverTests.cs
  SessionDirectoryFilterTests.cs
  WorkspaceYamlReaderTests.cs
scripts/
  publish.ps1
  build-installer.ps1
  signing/                   Certificate check + signtool helpers
installer.iss                Inno Setup installer definition
```
