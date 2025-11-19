# Architecture

This document explains the moving pieces of the SFTP Downloader for developers who need to understand or extend the code base.

## 1. High-level flow

```
Program.cs
   └─ Host + DI container (Microsoft.Extensions.Hosting)
        ├─ CliOptions (job filters)
        ├─ IOptions<AppSettings> (configuration binding)
        ├─ SftpClientFactory (SSH.NET wrapper)
        └─ SftpDownloadApplication
                └─ JobProcessor
                        └─ SSH.NET SftpClient
```

The console host wires dependencies, configures Serilog, and hands control to `SftpDownloadApplication`. That class loads the requested jobs, opens a single `SftpClient`, and delegates the actual work to `JobProcessor`.

## 2. Configuration model

- `Configuration/AppSettings.cs` binds the strongly-typed options:
  - `SftpOptions`: host, port, username, password/key settings.
  - `JobOptions`: job name, remote folders, local target, search pattern, delete/archive flags.
  - `LoggingOptions`: local log folder and retention days.
- `appsettings.json` lives beside the solution and is copied to the output directory so `dotnet run` and published builds share the same defaults.

## 3. CLI filtering

- `Cli/CliOptions` parses `--job NAME`, multiple `--job` flags, or `--jobs=NAME1,NAME2`.
- `SftpDownloadApplication` uses `ShouldRunJob` to determine which configured jobs to run. If no filter is provided, every job runs.

## 4. SFTP connectivity

- `Sftp/SftpClientFactory` creates and opens a `Renci.SshNet.SftpClient` using the configured credentials and optional private key.
- A single client instance is reused for every job to avoid reconnecting between folders.

## 5. Job execution pipeline

`JobProcessor.ProcessJob` performs the heavy lifting:

1. Validates that the job has at least one remote folder and creates the local target folder.
2. Iterates each remote folder, listing entries via `client.ListDirectory`.
3. Filters by `SearchPattern` using `FileSystemName.MatchesSimpleExpression`.
4. For each candidate file:
   - Downloads to `<LocalTargetFolder>/<name>.part`.
   - Flushes and compares the byte count with the remote file size.
   - Renames the `.part` file to the final name to guarantee atomic promotion.
   - Optionally deletes the remote file (`DeleteRemoteAfterSuccess`).
   - Handles any exception at the file level so the rest of the folder continues processing.
5. After every folder, optional archiving compresses the local target into a `.gz` under `ArchiveFolder`.

The method returns a `JobRunResult`, which aggregates `FolderRunResult` entries with counts, failures, and timings. `SftpDownloadApplication` collects those results.

## 6. Logging and reporting

- `Program.ConfigureSerilog` sets up Serilog with console + rolling file sinks. Console output only receives informational events (progress, summary), while the file sink captures the full debug stream.
- `JobProcessor` logs download lifecycle steps at debug level and emits periodic progress snapshots to stdout only (throttled to roughly 10 lines per folder).
- After all jobs finish, `JobSummaryPrinter` prints an ASCII banner and uses `ILogger` to persist the same summary lines to the log file.

## 7. Error handling

- Ctrl+C triggers cancellation via `CancellationTokenSource`; the host returns exit code `2`.
- Unhandled exceptions bubble to `Program` and produce exit code `1`.
- Per-file failures increment the folder’s failure count and are listed in the final summary, helping operators rerun specific items without halting the batch.

## 8. Extension points

- Add new helper services under `src/<Feature>` folders (for example, a scheduler or notification module).
- Extend `JobOptions` to support additional behavior (e.g., hash validation) and update `JobProcessor` accordingly.
