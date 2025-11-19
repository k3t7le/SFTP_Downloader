using System;
using System.Collections.Generic;
using System.Linq;

namespace SFTP_Downloader.Jobs;

public sealed class FolderRunResult
{
    public FolderRunResult(string folder)
    {
        Folder = folder;
    }

    public string Folder { get; }

    public int TotalCandidates { get; set; }

    public int SuccessCount { get; set; }

    public int FailureCount { get; set; }

    public TimeSpan Duration { get; set; }

    public List<string> FailedFiles { get; } = new();
}

public sealed class JobRunResult
{
    public JobRunResult(string jobName)
    {
        JobName = jobName;
    }

    public string JobName { get; }

    public TimeSpan Duration { get; set; }

    public List<FolderRunResult> FolderResults { get; } = new();

    public int TotalCandidates => FolderResults.Sum(r => r.TotalCandidates);

    public int TotalSuccess => FolderResults.Sum(r => r.SuccessCount);

    public int TotalFailures => FolderResults.Sum(r => r.FailureCount);
}
