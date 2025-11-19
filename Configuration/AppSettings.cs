using System.ComponentModel.DataAnnotations;
using System.IO;

namespace SFTP_Downloader.Configuration;

public sealed class AppSettings
{
    [Required]
    public required SftpOptions Sftp { get; init; }

    [MinLength(1)]
    public List<JobOptions> Jobs { get; init; } = new();

    public LoggingOptions Logging { get; init; } = new();
}

public sealed class SftpOptions
{
    [Required]
    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    [Required]
    public required string Username { get; init; }

    public string? Password { get; init; }

    public string? PrivateKeyPath { get; init; }

    public string? PrivateKeyPassphrase { get; init; }
}

public sealed class JobOptions
{
    [Required]
    public required string Name { get; init; }

    public List<string> RemoteFolders { get; init; } = new();

    [Required]
    public required string LocalTargetFolder { get; init; }

    public string SearchPattern { get; init; } = "*";

    public bool DeleteRemoteAfterSuccess { get; init; }

    public string? ArchiveFolder { get; init; }
}

public sealed class LoggingOptions
{
    public const int DefaultRetentionDays = 30;

    public string LogFolder { get; init; } = Path.Combine(AppContext.BaseDirectory, "logs");

    [Range(1, 3650)]
    public int RetentionDays { get; init; } = DefaultRetentionDays;
}
