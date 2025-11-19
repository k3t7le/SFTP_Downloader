# Usage

This guide targets the operator who needs to run the downloader and confirm that files arrive safely.

## 1. Prerequisites

- Network access (firewall, VPN, etc.) to the SFTP host plus credentials stored in `appsettings.json`, user secrets, or environment variables.
- Enough disk space on every `LocalTargetFolder` and `ArchiveFolder`.
- Permission to create folders/files inside the configured local paths and `Logging.LogFolder`.

## 2. Configure `appsettings.json`

Edit the file in the solution root. Set the SFTP host, logging folder, and one or more jobs.

```jsonc
{
  "Sftp": {
    "Host": "192.168.10.79",
    "Port": 2222,
    "Username": "acetech",
    "Password": "acetech"
  },
  "Logging": {
    "LogFolder": "D:/TMP_DATA/LOGS",
    "RetentionDays": 30
  },
  "Jobs": [
    {
      "Name": "AGING",
      "RemoteFolders": [ "/AGING", "/AGING_TMP" ],
      "LocalTargetFolder": "D:/TMP_DATA/AGING",
      "SearchPattern": "*.*",
      "DeleteRemoteAfterSuccess": true,
      "ArchiveFolder": "D:/TMP_DATA/AGING_ARC"
    }
  ]
}
```

Quick rules:

- Each job needs a unique `Name` and its own `LocalTargetFolder`.
- You can list multiple `RemoteFolders`; the downloader processes them in order.
- `DeleteRemoteAfterSuccess` removes the remote file only after download + integrity check succeed.
- Empty `ArchiveFolder` disables archiving.

## 3. Build once

```bash
dotnet restore
dotnet build SFTP_Downloader.sln
```

## 4. Run jobs

```bash
dotnet run --project SFTP_Downloader.csproj -- --job AGING
```

- Omit `--job` to run **all** jobs.
- Pass multiple filters via repeated flags (`--job AGING --job CONFIRM`) or a comma list (`--jobs=AGING,CONFIRM`).
- Stop safely with `Ctrl+C`. The app handles cancellation and prints a summary so you can tell what completed.

## 5. Monitor during execution

- Console output shows at most ten progress lines per folder, e.g. `/CONFIRM progress 381/5943 (~6%) | Success 381 | Fail 0`.
- Detailed steps (download/check/delete) go to the rolling file logs inside `Logging.LogFolder`.
- If a file fails to download, it appears in both the console summary and the log file with stack trace details.

## 6. After the run

1. Review the ASCII summary banner. It lists each job/folder, total/success/fail counts, and the names of failed files.
2. Inspect the target folders (and optional archives) to confirm that the expected files arrived.
3. Check for leftover `.part` files. They should only exist briefly; lingering parts usually indicate the machine stopped mid-transfer.

That’s it—rerun the command whenever you need to poll the SFTP server again.
