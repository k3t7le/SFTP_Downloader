using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace SFTP_Downloader.Configuration;

public sealed class AppSettingsValidator : IValidateOptions<AppSettings>
{
    public ValidateOptionsResult Validate(string? name, AppSettings? options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Configuration section is missing or invalid.");
        }

        var failures = new List<string>();

        ValidateSftp(options.Sftp, failures);
        ValidateJobs(options.Jobs, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateSftp(SftpOptions? sftp, List<string> failures)
    {
        if (sftp is null)
        {
            failures.Add("Sftp settings are required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(sftp.Host))
        {
            failures.Add("Sftp.Host is required.");
        }

        if (sftp.Port is <= 0 or > 65535)
        {
            failures.Add("Sftp.Port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(sftp.Username))
        {
            failures.Add("Sftp.Username is required.");
        }

        if (string.IsNullOrWhiteSpace(sftp.Password) && string.IsNullOrWhiteSpace(sftp.PrivateKeyPath))
        {
            failures.Add("Sftp requires at least one authentication method (Password or PrivateKeyPath).");
        }
    }

    private static void ValidateJobs(IReadOnlyCollection<JobOptions> jobs, List<string> failures)
    {
        if (jobs.Count == 0)
        {
            failures.Add("At least one job must be configured.");
            return;
        }

        var jobNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.Name))
            {
                failures.Add("Each job requires a Name.");
                continue;
            }

            if (!jobNames.Add(job.Name.Trim()))
            {
                failures.Add($"Job '{job.Name}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(job.LocalTargetFolder))
            {
                failures.Add($"Job '{job.Name}' requires a LocalTargetFolder.");
            }
            else if (!localTargets.Add(job.LocalTargetFolder.Trim()))
            {
                failures.Add($"LocalTargetFolder '{job.LocalTargetFolder}' is used by multiple jobs.");
            }

            ValidateRemoteFolders(job, failures);
        }
    }

    private static void ValidateRemoteFolders(JobOptions job, List<string> failures)
    {
        if (job.RemoteFolders.Count == 0)
        {
            failures.Add($"Job '{job.Name}' must declare at least one remote folder.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in job.RemoteFolders)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                failures.Add($"Job '{job.Name}' has an empty remote folder entry.");
                continue;
            }

            if (!seen.Add(folder.Trim()))
            {
                failures.Add($"Job '{job.Name}' declares duplicate remote folder '{folder}'.");
            }
        }
    }
}
