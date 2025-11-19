using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using SFTP_Downloader.Configuration;

namespace SFTP_Downloader.Sftp;

public interface ISftpClientFactory
{
    SftpClient CreateAndConnect(SftpOptions options);
}

public sealed class SftpClientFactory(ILogger<SftpClientFactory> logger) : ISftpClientFactory
{
    private readonly ILogger<SftpClientFactory> _logger = logger;

    public SftpClient CreateAndConnect(SftpOptions options)
    {
        var connectionInfo = BuildConnectionInfo(options);
        var client = new SftpClient(connectionInfo);
        client.Connect();
        _logger.LogInformation("Connected to {Host}:{Port} as {User}", options.Host, options.Port, options.Username);
        return client;
    }

    private ConnectionInfo BuildConnectionInfo(SftpOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new ArgumentException("Host is required", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new ArgumentException("Username is required", nameof(options));
        }

        var authMethods = new List<AuthenticationMethod>();

        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPath))
        {
            var privateKey = string.IsNullOrEmpty(options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(options.PrivateKeyPath)
                : new PrivateKeyFile(options.PrivateKeyPath, options.PrivateKeyPassphrase);
            authMethods.Add(new PrivateKeyAuthenticationMethod(options.Username, privateKey));
        }

        if (!string.IsNullOrWhiteSpace(options.Password))
        {
            authMethods.Add(new PasswordAuthenticationMethod(options.Username, options.Password));
        }

        if (authMethods.Count == 0)
        {
            throw new InvalidOperationException("At least one authentication method must be configured (password or private key).");
        }

        return new ConnectionInfo(options.Host, options.Port, options.Username, authMethods.ToArray());
    }
}
