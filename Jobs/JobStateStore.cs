using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SFTP_Downloader.Configuration;

namespace SFTP_Downloader.Jobs;

public sealed class JobStateStore
{
    private readonly ILogger<JobStateStore> _logger;
    private readonly string _stateFilePath;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, Dictionary<string, FolderState>> _state;

    public JobStateStore(IOptions<AppSettings> settings, ILogger<JobStateStore> logger)
    {
        _logger = logger;
        var appSettings = settings.Value ?? throw new InvalidOperationException("Missing configuration.");
        var tempRoot = ResolveTempWorkspaceRoot(appSettings.TempWorkspaceRoot);
        var stateFolder = Path.Combine(tempRoot, "state");
        Directory.CreateDirectory(stateFolder);
        _stateFilePath = Path.Combine(stateFolder, "job-state.json");
        _state = LoadState();
    }

    public FolderStateSnapshot GetLastProcessed(string jobName, string remoteFolder)
    {
        if (string.IsNullOrWhiteSpace(jobName) || string.IsNullOrWhiteSpace(remoteFolder))
        {
            return FolderStateSnapshot.Empty;
        }

        var jobKey = Normalize(jobName);
        var folderKey = NormalizeFolder(remoteFolder);

        lock (_syncRoot)
        {
            if (_state.TryGetValue(jobKey, out var folders) &&
                folders.TryGetValue(folderKey, out var state))
            {
                return new FolderStateSnapshot(state.LastProcessedUtc, state.NamesAtTimestamp.ToArray());
            }
        }

        return FolderStateSnapshot.Empty;
    }

    public void RecordProcessed(string jobName, string remoteFolder, DateTimeOffset lastWriteUtc, string fileName)
    {
        if (string.IsNullOrWhiteSpace(jobName) || string.IsNullOrWhiteSpace(remoteFolder))
        {
            return;
        }

        lastWriteUtc = lastWriteUtc.ToUniversalTime();
        var jobKey = Normalize(jobName);
        var folderKey = NormalizeFolder(remoteFolder);
        var normalizedName = fileName?.Trim() ?? string.Empty;

        lock (_syncRoot)
        {
            if (!_state.TryGetValue(jobKey, out var folders))
            {
                folders = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase);
                _state[jobKey] = folders;
            }

            if (!folders.TryGetValue(folderKey, out var state))
            {
                state = new FolderState();
                folders[folderKey] = state;
            }

            if (lastWriteUtc > state.LastProcessedUtc)
            {
                state.LastProcessedUtc = lastWriteUtc;
                state.NamesAtTimestamp.Clear();
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    state.NamesAtTimestamp.Add(normalizedName);
                }
            }
            else if (lastWriteUtc == state.LastProcessedUtc && !string.IsNullOrWhiteSpace(normalizedName))
            {
                state.NamesAtTimestamp.Add(normalizedName);
            }

            TryPersist();
        }
    }

    private Dictionary<string, Dictionary<string, FolderState>> LoadState()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return CreateEmptyState();
            }

            var json = File.ReadAllText(_stateFilePath);

            // Try new format first
            var model = JsonSerializer.Deserialize<StateModel>(json);
            if (model?.Jobs is not null)
            {
                return Normalize(model.Jobs);
            }

            // Try legacy format (timestamp only)
            var legacy = JsonSerializer.Deserialize<LegacyStateModel>(json);
            if (legacy?.Jobs is not null)
            {
                return UpgradeLegacy(legacy.Jobs);
            }

            return CreateEmptyState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load job state from {StateFile}", _stateFilePath);
            return CreateEmptyState();
        }
    }

    private void TryPersist()
    {
        try
        {
            var stateFolder = Path.GetDirectoryName(_stateFilePath) ?? ".";
            Directory.CreateDirectory(stateFolder);
            var model = new StateModel { Jobs = _state };
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _stateFilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _stateFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist job state to {StateFile}", _stateFilePath);
        }
    }

    private static Dictionary<string, Dictionary<string, FolderState>> Normalize(
        Dictionary<string, Dictionary<string, FolderState>> source)
    {
        var jobs = new Dictionary<string, Dictionary<string, FolderState>>(StringComparer.OrdinalIgnoreCase);
        foreach (var jobEntry in source)
        {
            var jobKey = Normalize(jobEntry.Key);
            var folders = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase);
            if (jobEntry.Value is not null)
            {
                foreach (var folderEntry in jobEntry.Value)
                {
                    var folderKey = NormalizeFolder(folderEntry.Key);
                    var state = folderEntry.Value ?? new FolderState();
                    folders[folderKey] = state;
                }
            }

            jobs[jobKey] = folders;
        }

        return jobs;
    }

    private static Dictionary<string, Dictionary<string, FolderState>> UpgradeLegacy(
        Dictionary<string, Dictionary<string, DateTimeOffset>> legacy)
    {
        var upgraded = new Dictionary<string, Dictionary<string, FolderState>>(StringComparer.OrdinalIgnoreCase);

        foreach (var jobEntry in legacy)
        {
            var jobKey = Normalize(jobEntry.Key);
            var folders = new Dictionary<string, FolderState>(StringComparer.OrdinalIgnoreCase);

            if (jobEntry.Value is not null)
            {
                foreach (var folderEntry in jobEntry.Value)
                {
                    var folderKey = NormalizeFolder(folderEntry.Key);
                    var utc = folderEntry.Value.ToUniversalTime();
                    folders[folderKey] = new FolderState
                    {
                        LastProcessedUtc = utc
                    };
                }
            }

            upgraded[jobKey] = folders;
        }

        return upgraded;
    }

    private static Dictionary<string, Dictionary<string, FolderState>> CreateEmptyState()
    {
        return new Dictionary<string, Dictionary<string, FolderState>>(StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) => value.Trim();

    private static string NormalizeFolder(string folder)
    {
        var normalized = folder.Replace('\\', '/').Trim();
        normalized = normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
        return normalized;
    }

    private static string ResolveTempWorkspaceRoot(string? configuredPath)
    {
        var basePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Path.GetTempPath(), "sftp-downloader")
            : configuredPath;

        return Path.GetFullPath(basePath);
    }

    private sealed class StateModel
    {
        public Dictionary<string, Dictionary<string, FolderState>> Jobs { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class LegacyStateModel
    {
        public Dictionary<string, Dictionary<string, DateTimeOffset>> Jobs { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record FolderStateSnapshot(DateTimeOffset? LastProcessedUtc, IReadOnlyCollection<string> NamesAtLastTimestamp)
{
    public static FolderStateSnapshot Empty { get; } = new(null, Array.Empty<string>());
}

public sealed class FolderState
{
    public DateTimeOffset LastProcessedUtc { get; set; } = DateTimeOffset.MinValue;

    public HashSet<string> NamesAtTimestamp { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
