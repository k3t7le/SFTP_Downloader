# SFTP Downloader

Reliable multi-job downloader that pulls files from one or more SFTP folders, stores them locally with `.part` safety, and optionally archives or deletes the remote source. Built as a .NET 8 console app with Serilog logging and SSH.NET for SFTP.

## Features

- **Job-based configuration** – Each job maps remote folders to a dedicated local target and optional archive path.
- **Atomic downloads** – Files land as `<name>.part` until a full download finishes, then promote to the final name.
- **Per-file isolation** – One file failure doesn’t stop the rest; progress and failures are summarized at the end.
- **Verbose logging** – Console shows throttled progress, while the rolling log captures full debug detail.
- **ASCII run summary** – Easy-to-grep `RUN-SUMMARY` line plus a banner listing per-job/folder stats and failed files.

## Getting Started

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/).
2. Update `appsettings.json` with your SFTP host, logging folder, and jobs.
3. Build once:

   ```bash
   dotnet restore
   dotnet build SFTP_Downloader.sln
   ```

4. Run jobs:

   ```bash
   dotnet run --project SFTP_Downloader.csproj -- --job AGING
   ```

   - Omit `--job` to run every job.
   - Repeat the flag or use `--jobs=AGING,CONFIRM` to limit execution.
   - Stop via `Ctrl+C`; the app exits gracefully and prints the final summary.

## Logging

- Configure the log folder and retention days under the `Logging` section of `appsettings.json`.
- Console output shows at most ~10 progress lines per folder plus the ASCII summary.
- Rolling files (`sftp-downloader-*.log`) include full debug events and the one-line `RUN-SUMMARY … total:succ=…` entry for quick greps.

## Documentation

- Operator usage: `docs/Usage.md` (English) / `docs/Usage.ko.md` (Korean).
- Architecture overview: `docs/Arch.md` / `docs/Arch.ko.md`.
- Original concept notes: `concept.md`.

## Testing & CI

- (Planned) xUnit tests live under `tests/SFTP_Downloader.Tests`; run with `dotnet test`.
- No CI pipeline is bundled yet, but the project builds cleanly with `dotnet build`.
