using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SFTP_Downloader.Jobs;

public static class JobSummaryPrinter
{
    private const int BorderWidth = 80;

    public static void Print(IEnumerable<JobRunResult> results, ILogger? logger)
    {
        var jobResults = results?.ToList() ?? new List<JobRunResult>();
        if (jobResults.Count == 0)
        {
            return;
        }

        LogOneLineSummary(jobResults, logger);

        var border = new string('=', BorderWidth);
        var separator = new string('-', BorderWidth);

        WriteLine(border, logger);
        WriteLine(Center("JOB EXECUTION SUMMARY"), logger);
        WriteLine(border, logger);

        foreach (var job in jobResults)
        {
            WriteLine(Center($"JOB: {job.JobName} | Duration: {Format(job.Duration)} | Success: {job.TotalSuccess} | Fail: {job.TotalFailures}"), logger);
            WriteLine(separator, logger);

            foreach (var folder in job.FolderResults)
            {
                WriteLine(Center($"Folder: {folder.Folder}"), logger);
                WriteLine(Center($"Total: {folder.TotalCandidates} | Success: {folder.SuccessCount} | Fail: {folder.FailureCount} | Duration: {Format(folder.Duration)}"), logger);

                if (folder.FailedFiles.Count > 0)
                {
                    WriteLine(Center("Failed Files:"), logger);
                    foreach (var failed in folder.FailedFiles)
                    {
                        WriteLine(Center($" - {failed}"), logger);
                    }
                }

                WriteLine(separator, logger);
            }
        }

        WriteLine(border, logger);
    }

    private static string Format(TimeSpan span) => span == TimeSpan.Zero ? "00:00:00" : span.ToString(@"hh\:mm\:ss");

    private static string Center(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.Trim();
        if (text.Length >= BorderWidth)
        {
            return text;
        }

        var padding = (BorderWidth - text.Length) / 2;
        return new string(' ', padding) + text;
    }

    private static void WriteLine(string text, ILogger? logger)
    {
        Console.WriteLine(text);
        if (!string.IsNullOrWhiteSpace(text))
        {
            logger?.LogInformation("{SummaryLine}", text.TrimEnd());
        }
    }

    private static void LogOneLineSummary(IEnumerable<JobRunResult> results, ILogger? logger)
    {
        var jobResults = results.ToList();
        var pairs = jobResults
            .Select(job => $"{job.JobName}:succ={job.TotalSuccess},fail={job.TotalFailures},dur={Format(job.Duration)}");
        var totalSuccess = jobResults.Sum(r => r.TotalSuccess);
        var totalFail = jobResults.Sum(r => r.TotalFailures);
        var summaryLine = $"RUN-SUMMARY total:succ={totalSuccess},fail={totalFail} | " + string.Join(" | ", pairs);
        logger?.LogInformation("{RunSummary}", summaryLine);
    }
}
