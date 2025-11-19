using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SFTP_Downloader.Cli;
using SFTP_Downloader.Configuration;
using SFTP_Downloader.Jobs;
using SFTP_Downloader.Sftp;
using Serilog;
using Serilog.Core;
using Serilog.Events;

var cliOptions = CliOptions.Parse(args);
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var builder = Host.CreateApplicationBuilder(args);
ConfigureSerilog(builder);
builder.Services.Configure<AppSettings>(builder.Configuration);
builder.Services.AddSingleton(cliOptions);
builder.Services.AddSingleton<JobProcessor>();
builder.Services.AddSingleton<ISftpClientFactory, SftpClientFactory>();
builder.Services.AddSingleton<SftpDownloadApplication>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;
var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SftpDownloader");

try
{
    var app = services.GetRequiredService<SftpDownloadApplication>();
    await app.RunAsync(cts.Token);
    return 0;
}
catch (OperationCanceledException)
{
    logger.LogWarning("Cancellation requested. Exiting gracefully.");
    return 2;
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled exception while running downloader.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

partial class Program
{
    private static void ConfigureSerilog(HostApplicationBuilder builder)
    {
        var loggingOptions = builder.Configuration.GetSection(nameof(AppSettings.Logging)).Get<LoggingOptions>() ?? new LoggingOptions();
        var logFolder = ResolveLogFolder(loggingOptions);
        Directory.CreateDirectory(logFolder);
        var retentionDays = ResolveRetentionDays(loggingOptions);
        const string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] ({Src}) {Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With(new ShortSourceContextEnricher())
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: outputTemplate, restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                Path.Combine(logFolder, "sftp-downloader-.log"),
                outputTemplate: outputTemplate,
                restrictedToMinimumLevel: LogEventLevel.Debug,
                rollingInterval: RollingInterval.Day,
                retainedFileTimeLimit: TimeSpan.FromDays(retentionDays),
                rollOnFileSizeLimit: true,
                shared: true)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger: Log.Logger, dispose: true);
    }

    private static string ResolveLogFolder(LoggingOptions options)
    {
        return string.IsNullOrWhiteSpace(options.LogFolder)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : options.LogFolder;
    }

    private static int ResolveRetentionDays(LoggingOptions options)
    {
        return options.RetentionDays > 0 ? options.RetentionDays : LoggingOptions.DefaultRetentionDays;
    }

    private sealed class ShortSourceContextEnricher : ILogEventEnricher
    {
        private const string PropertyName = "Src";

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                return;
            }

            if (sourceContext is not ScalarValue { Value: string fullName } || string.IsNullOrWhiteSpace(fullName))
            {
                return;
            }

            var shortName = GetShortName(fullName);
            var property = propertyFactory.CreateProperty(PropertyName, shortName);
            logEvent.AddPropertyIfAbsent(property);
        }

        private static string GetShortName(string fullName)
        {
            var trimmed = fullName.Trim();
            var span = trimmed.AsSpan();
            var index = span.LastIndexOf('.');
            return index >= 0 ? span[(index + 1)..].ToString() : trimmed;
        }
    }
}
