namespace SFTP_Downloader.Cli;

public sealed class CliOptions
{
    private readonly HashSet<string> _jobFilters;

    private CliOptions(HashSet<string> jobFilters)
    {
        _jobFilters = jobFilters;
    }

    public static CliOptions Parse(string[] args)
    {
        var filters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is "--job" or "-j" || arg.StartsWith("--job=", StringComparison.OrdinalIgnoreCase))
            {
                var jobName = arg.Contains('=') ? arg.Split('=', 2)[1] : (i + 1 < args.Length ? args[++i] : string.Empty);
                if (!string.IsNullOrWhiteSpace(jobName))
                {
                    filters.Add(jobName.Trim());
                }
                continue;
            }

            if (arg.StartsWith("--jobs=", StringComparison.OrdinalIgnoreCase))
            {
                var list = arg.Split('=', 2)[1];
                foreach (var job in list.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    filters.Add(job.Trim());
                }
            }
        }

        return new CliOptions(filters);
    }

    public bool ShouldRunJob(string jobName)
    {
        if (_jobFilters.Count == 0)
        {
            return true;
        }

        return _jobFilters.Contains(jobName);
    }

    public bool HasFilters => _jobFilters.Count > 0;
}
