# Repository Guidelines

## Project Structure & Module Organization
The solution is a single .NET 8 console application defined by `SFTP_Downloader.sln`/`SFTP_Downloader.csproj`. Runtime logic lives in `Program.cs`; add future modules under `src/<Feature>/` folders that mirror their namespaces so SFTP helpers, job runners, and configuration models stay isolated. Place `appsettings.json` beside the solution for easy binding, check implementation notes in `concept.md`, and keep generated artifacts inside `bin/` and `obj/`.

## Build, Test, and Development Commands
- `dotnet restore` — install all NuGet dependencies.
- `dotnet build SFTP_Downloader.sln` — compile with warning-as-error defaults before shipping.
- `dotnet run --project SFTP_Downloader.csproj -- --job Aging` — execute locally and forward scenario arguments (job names, dry-run flags, etc.).
- `dotnet test` — run the future test suite; add `--collect:"XPlat Code Coverage"` for publishing metrics.

## Coding Style & Naming Conventions
Stick with the .NET SDK formatter: UTF-8 encoding, LF endings, 4-space indents, and braces on new lines. Use PascalCase for public types/members, camelCase for locals, and `_camelCase` only for private readonly fields. Prefer `async` `Task` methods for I/O, guard SFTP calls with cancellation tokens, and keep options strongly typed so `Jobs` from configuration bind cleanly. Run `dotnet format` prior to commits to enforce analyzers and style.

## Testing Guidelines
Adopt xUnit and store tests under `tests/SFTP_Downloader.Tests`. Name files `{Target}Tests.cs`, mirror namespaces, and group facts/theories by scenario (`DownloadJob_ShouldRenamePartFiles`). Include fixture-level coverage for `.part` handling, remote cleanup, and job filtering. Create integration tests that point to a disposable SFTP container and assert resume behavior after simulated failures.

## Commit & Pull Request Guidelines
Author small, purposeful commits named `area: summary` (e.g., `jobs: add aging folders`). Each PR should explain the motivation, list manual verification commands (`dotnet run -- --job CONFIRM`), and attach logs/screenshots for file-transfer changes. Reference related work items and request review from an engineer who owns SFTP infrastructure when touching security or configuration semantics.

## SFTP Configuration & Operations
Never commit credentials; rely on `appsettings.Development.json`, user secrets, or environment variables. Each job must pair its remote folders with a distinct `LocalTargetFolder` to prevent clobbering. During execution write downloads as `<name>.part`, validate size/hash, rename atomically, then delete or archive the remote source—breaking that sequence is only acceptable in explicit dry-run modes.
