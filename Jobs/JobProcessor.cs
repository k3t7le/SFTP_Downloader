using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.IO.Enumeration;
using System.Linq;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SFTP_Downloader.Configuration;

namespace SFTP_Downloader.Jobs;

public sealed class JobProcessor(ILogger<JobProcessor> logger)
{
    private readonly ILogger<JobProcessor> _logger = logger;

    public JobRunResult ProcessJob(SftpClient client, JobOptions job, CancellationToken cancellationToken)
    {
        var jobResult = new JobRunResult(job.Name);
        var jobStopwatch = Stopwatch.StartNew();

        if (job.RemoteFolders.Count == 0)
        {
            _logger.LogWarning("Job {Job} has no remote folders configured; skipping", job.Name);
            jobStopwatch.Stop();
            jobResult.Duration = jobStopwatch.Elapsed;
            return jobResult;
        }

        Directory.CreateDirectory(job.LocalTargetFolder);
        _logger.LogDebug("Starting job {Job} -> {Local}", job.Name, job.LocalTargetFolder);

        foreach (var remoteFolder in job.RemoteFolders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var folderResult = ProcessRemoteFolder(client, job, remoteFolder, cancellationToken);
                jobResult.FolderResults.Add(folderResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {Job}: failed to process folder {Folder}", job.Name, remoteFolder);
            }
        }

        try
        {
            ArchiveJobOutput(job, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {Job}: failed while archiving local folder", job.Name);
        }

        jobStopwatch.Stop();
        jobResult.Duration = jobStopwatch.Elapsed;
        return jobResult;
    }

    private FolderRunResult ProcessRemoteFolder(SftpClient client, JobOptions job, string remoteFolder, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Job {Job}: listing {Folder}", job.Name, remoteFolder);
        var folderResult = new FolderRunResult(remoteFolder);
        var folderStopwatch = Stopwatch.StartNew();
        List<ISftpFile> entries;
        try
        {
            entries = client.ListDirectory(remoteFolder).ToList();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to list remote folder '{remoteFolder}'", ex);
        }

        _logger.LogDebug(
            "Job {Job}: {Folder} contains {EntryCount} entries before filtering",
            job.Name,
            remoteFolder,
            entries.Count);

        var candidates = entries
            .Where(entry => IsCandidate(job, entry))
            .ToList();

        _logger.LogDebug(
            "Job {Job}: {Folder} has {CandidateCount} candidate files matching {Pattern}",
            job.Name,
            remoteFolder,
            candidates.Count,
            string.IsNullOrWhiteSpace(job.SearchPattern) ? "*" : job.SearchPattern);

        folderResult.TotalCandidates = candidates.Count;
        var progressReporter = new ConsoleProgressReporter(remoteFolder, candidates.Count);

        if (candidates.Count == 0)
        {
            folderStopwatch.Stop();
            folderResult.Duration = folderStopwatch.Elapsed;
            return folderResult;
        }

        for (var index = 0; index < candidates.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = candidates[index];

            try
            {
                DownloadFile(client, job, remoteFolder, entry, index, candidates.Count);
                folderResult.SuccessCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {Job}: failed to download {File}", job.Name, entry.FullName);
                folderResult.FailureCount++;
                folderResult.FailedFiles.Add(entry.FullName);
            }
            finally
            {
                progressReporter.TryWrite(
                    processedCount: index + 1,
                    successCount: folderResult.SuccessCount,
                    failureCount: folderResult.FailureCount);
            }
        }

        folderStopwatch.Stop();
        folderResult.Duration = folderStopwatch.Elapsed;
        return folderResult;
    }

    private static bool IsCandidate(JobOptions job, ISftpFile entry)
    {
        if (!entry.IsRegularFile)
        {
            return false;
        }

        var pattern = string.IsNullOrWhiteSpace(job.SearchPattern) ? "*" : job.SearchPattern;
        return FileSystemName.MatchesSimpleExpression(pattern, entry.Name, ignoreCase: true);
    }

    private void DownloadFile(
        SftpClient client,
        JobOptions job,
        string remoteFolder,
        ISftpFile entry,
        int fileIndex,
        int totalCount)
    {
        var remotePath = CombineRemotePath(remoteFolder, entry.Name);
        var localFilePath = Path.Combine(job.LocalTargetFolder, entry.Name);
        var tempFilePath = localFilePath + ".part";
        var progress = FormatProgress(fileIndex, totalCount);

        if (File.Exists(localFilePath))
        {
            _logger.LogDebug("Job {Job}: {Local} already exists; skipping {Remote}", job.Name, localFilePath, entry.FullName);
            return;
        }

        if (File.Exists(tempFilePath))
        {
            _logger.LogWarning("Job {Job}: removing stale temp file {Temp}", job.Name, tempFilePath);
            File.Delete(tempFilePath);
        }

        using (var localStream = File.Open(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            _logger.LogDebug(
                "Job {Job}: {Progress} 1/3[DOWN] {Remote} -> {Temp}",
                job.Name,
                progress,
                remotePath,
                tempFilePath);
            client.DownloadFile(remotePath, localStream);
        }

        var downloadedBytes = new FileInfo(tempFilePath).Length;
        if (downloadedBytes != entry.Attributes.Size)
        {
            File.Delete(tempFilePath);
            throw new IOException($"Size mismatch for {remotePath}. Expected {entry.Attributes.Size}, got {downloadedBytes}.");
        }

        File.Move(tempFilePath, localFilePath, overwrite: false);
        _logger.LogDebug(
            "Job {Job}: {Progress} 2/3[CHEK] promoted temp file to {Local}",
            job.Name,
            progress,
            localFilePath);

        if (job.DeleteRemoteAfterSuccess)
        {
            client.DeleteFile(remotePath);
            _logger.LogDebug(
                "Job {Job}: {Progress} 3/3[DELE] removed remote file {Remote}",
                job.Name,
                progress,
                remotePath);
        }
        else
        {
            _logger.LogDebug(
                "Job {Job}: {Progress} 3/3[KEEP] remote retention requested; leaving {Remote}",
                job.Name,
                progress,
                remotePath);
        }
    }

    private static string FormatProgress(int fileIndex, int totalCount)
    {
        if (totalCount <= 0)
        {
            return "0/0 (~0%)";
        }

        var current = fileIndex + 1;
        var percent = (int)Math.Round((current / (double)totalCount) * 100, MidpointRounding.AwayFromZero);
        percent = Math.Clamp(percent, 0, 100);
        return $"{current}/{totalCount} (~{percent}%)";
    }

    private sealed class ConsoleProgressReporter
    {
        private readonly string _folder;
        private readonly int _totalCount;
        private readonly int _targetLogs;
        private int _emitted;

        public ConsoleProgressReporter(string folder, int totalCount)
        {
            _folder = folder;
            _totalCount = totalCount;
            var desired = totalCount <= 0 ? 0 : (int)Math.Ceiling(totalCount * 0.1);
            _targetLogs = totalCount <= 0 ? 0 : Math.Clamp(desired, 1, 10);
        }

        public void TryWrite(int processedCount, int successCount, int failureCount)
        {
            if (_totalCount <= 0 || _targetLogs == 0 || _emitted >= _targetLogs)
            {
                return;
            }

            var boundedProcessed = Math.Clamp(processedCount, 0, _totalCount);
            var fraction = boundedProcessed / (double)_totalCount;
            var threshold = (_emitted + 1) / (double)_targetLogs;
            var isFinal = boundedProcessed >= _totalCount;

            if (isFinal || fraction >= threshold)
            {
                Write(boundedProcessed, successCount, failureCount, fraction);
            }
        }

        private void Write(int processedCount, int successCount, int failureCount, double fraction)
        {
            if (_totalCount <= 0)
            {
                return;
            }

            var percent = processedCount <= 0
                ? 0
                : (int)Math.Round(fraction * 100, MidpointRounding.AwayFromZero);

            percent = Math.Clamp(percent, 0, 100);
            Console.Out.WriteLine(
                $"{_folder} progress {processedCount}/{_totalCount} (~{percent}%) | Success {successCount} | Fail {failureCount}");
            _emitted = Math.Min(_emitted + 1, _targetLogs);
        }
    }

    private void ArchiveJobOutput(JobOptions job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.ArchiveFolder))
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(job.LocalTargetFolder))
        {
            _logger.LogWarning("Job {Job}: local folder {Folder} does not exist; skipping archive", job.Name, job.LocalTargetFolder);
            return;
        }

        var hasContent = Directory.EnumerateFileSystemEntries(job.LocalTargetFolder).Any();
        if (!hasContent)
        {
            _logger.LogDebug("Job {Job}: no files to archive in {Folder}", job.Name, job.LocalTargetFolder);
            return;
        }

        Directory.CreateDirectory(job.ArchiveFolder);
        var timestamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        var archiveFileName = $"{job.Name}_{timestamp}.tar.gz";
        var tempWorkspace = Path.Combine(Path.GetTempPath(), "sftp-downloader", "archive-temp");
        Directory.CreateDirectory(tempWorkspace);
        CleanupTempArchives(tempWorkspace);
        var tempArchivePath = Path.Combine(tempWorkspace, archiveFileName + ".tmp");
        var finalArchivePath = Path.Combine(job.ArchiveFolder, archiveFileName);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
            }

            // Create archive in a temp workspace that is not watched by other processes to avoid contention.
            using (var archiveStream = File.Create(tempArchivePath))
            using (var gzipStream = new GZipStream(archiveStream, CompressionLevel.SmallestSize))
            {
                TarFile.CreateFromDirectory(job.LocalTargetFolder, gzipStream, includeBaseDirectory: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

            File.Move(tempArchivePath, finalArchivePath, overwrite: false);
            _logger.LogDebug("Job {Job}: archived {Source} to {Archive}", job.Name, job.LocalTargetFolder, finalArchivePath);
        }
        catch
        {
            if (File.Exists(tempArchivePath))
            {
                File.Delete(tempArchivePath);
            }

            throw;
        }

        DeleteAndRecreate(job.LocalTargetFolder);
        _logger.LogDebug("Job {Job}: cleared source folder {Folder} after archiving", job.Name, job.LocalTargetFolder);
    }

    private void CleanupTempArchives(string tempWorkspace)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(tempWorkspace, "*.tar.gz.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to clean temp archive {TempArchive}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan temp archive workspace {TempWorkspace}", tempWorkspace);
        }
    }

    private static void DeleteAndRecreate(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }

    private static string CombineRemotePath(string folder, string fileName)
    {
        var normalizedFolder = (folder ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        var normalizedFile = (fileName ?? string.Empty).TrimStart('/');

        if (string.IsNullOrEmpty(normalizedFolder))
        {
            return "/" + normalizedFile;
        }

        return normalizedFolder + "/" + normalizedFile;
    }
}
