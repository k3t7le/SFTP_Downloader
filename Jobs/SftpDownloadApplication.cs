using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFTP_Downloader.Cli;
using SFTP_Downloader.Configuration;
using SFTP_Downloader.Sftp;

namespace SFTP_Downloader.Jobs;

public sealed class SftpDownloadApplication
{
    private readonly CliOptions _cliOptions;
    private readonly JobProcessor _jobProcessor;
    private readonly ISftpClientFactory _clientFactory;
    private readonly IOptions<AppSettings> _settings;
    private readonly ILogger<SftpDownloadApplication> _logger;

    public SftpDownloadApplication(
        CliOptions cliOptions,
        JobProcessor jobProcessor,
        ISftpClientFactory clientFactory,
        IOptions<AppSettings> settings,
        ILogger<SftpDownloadApplication> logger)
    {
        _cliOptions = cliOptions;
        _jobProcessor = jobProcessor;
        _clientFactory = clientFactory;
        _settings = settings;
        _logger = logger;
    }

    public Task RunAsync(CancellationToken cancellationToken)
    {
        var configuration = _settings.Value ?? throw new InvalidOperationException("Missing configuration.");
        Validate(configuration);

        var jobsToRun = configuration.Jobs
            .Where(job => _cliOptions.ShouldRunJob(job.Name))
            .ToList();

        if (jobsToRun.Count == 0)
        {
            _logger.LogWarning("No jobs matched the provided filters.");
            return Task.CompletedTask;
        }

        using var client = _clientFactory.CreateAndConnect(configuration.Sftp);
        var jobResults = new List<JobRunResult>();

        foreach (var job in jobsToRun)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = _jobProcessor.ProcessJob(client, job, cancellationToken);
            jobResults.Add(result);
        }

        JobSummaryPrinter.Print(jobResults, _logger);
        return Task.CompletedTask;
    }

    private static void Validate(AppSettings settings)
    {
        if (settings.Jobs.Count == 0)
        {
            throw new InvalidOperationException("At least one job must be configured.");
        }

        foreach (var job in settings.Jobs)
        {
            if (job.RemoteFolders.Count == 0)
            {
                throw new InvalidOperationException($"Job '{job.Name}' must declare at least one remote folder.");
            }

            if (string.IsNullOrWhiteSpace(job.LocalTargetFolder))
            {
                throw new InvalidOperationException($"Job '{job.Name}' is missing LocalTargetFolder.");
            }
        }
    }
}
