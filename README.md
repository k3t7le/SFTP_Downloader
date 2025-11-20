# SFTP Downloader

Reliable multi-job downloader that pulls files from one or more SFTP folders, stores them locally with `.part` safety, and optionally deletes the remote source or archives the local output. Built as a .NET 8 console app with Serilog logging and SSH.NET for SFTP.

## Features

- **Job-based configuration** — Each job maps remote folders to its own local target and optional archive path.
- **Atomic downloads** — Files arrive as `<name>.part` until the full download finishes, then promote to the final name.
- **Per-file isolation** — One file failure doesn’t stop the rest; progress and failures are summarized at the end.
- **Archive staging** — Archives are created as `tar.gz` in a temp workspace (`%TEMP%/sftp-downloader/archive-temp`), then moved atomically to `ArchiveFolder`; stale temp files are cleaned at startup.
- **Logging & summary** — Throttled console progress, rolling file logs with full debug detail, and an ASCII run summary (`RUN-SUMMARY …`) for quick greps.

## Quick Start

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/).
2. Update `appsettings.json` with your SFTP host, logging folder, and jobs.
3. Build:

   ```bash
   dotnet restore
   dotnet build SFTP_Downloader.sln
   ```

4. Run:

   ```bash
   dotnet run --project SFTP_Downloader.csproj -- --job AGING
   ```

   - Omit `--job` to run every job.
   - Repeat `--job` or use `--jobs=AGING,CONFIRM` to filter.
   - Stop with `Ctrl+C`; the app exits gracefully and prints the final summary.

## Configuration (appsettings.json)

- `Sftp`: `Host`, `Port`, `Username`, and either `Password` or `PrivateKeyPath`/`PrivateKeyPassphrase`.
- `Jobs[]`:
  - `Name` — Job name.
  - `RemoteFolders` — Array of remote folders.
  - `LocalTargetFolder` — Local destination (unique per job).
  - `SearchPattern` — Filter, e.g., `"*.dat"`.
  - `DeleteRemoteAfterSuccess` — Delete remote file after success.
  - `ArchiveFolder` — Optional archive destination (`tar.gz` created in temp, then moved).
- `Logging`: `LogFolder`, `RetentionDays`.

## Logging & Summary

- Console: up to ~10 progress lines per folder plus the ASCII summary.
- Files: `sftp-downloader-*.log` with info/debug, including one-line `RUN-SUMMARY total:succ=...`.

## Documentation

- Operator usage: `docs/Usage.md` / `docs/Usage.ko.md`
- Architecture: `docs/Arch.md` / `docs/Arch.ko.md`
- Concept notes: `concept.md`

## Testing

- Planned xUnit tests under `tests/SFTP_Downloader.Tests`; run with `dotnet test` when available.
