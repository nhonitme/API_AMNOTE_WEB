using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentFTP;
using FluentFTP.Exceptions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace API_AMNOTE_WEB.Einvoice.Helpers;

/// <summary>
/// Kết nối file server của API. FTP và SFTP tách riêng theo Protocol, không fallback chéo.
/// </summary>
internal static class RemoteFileTransfer
{
    public enum TransferProtocol
    {
        Ftp,
        Sftp,
    }

    public readonly record struct Endpoint(
        string Host,
        int FtpPort,
        int SftpPort,
        string Username,
        string Password,
        TransferProtocol Protocol);

    private static readonly object TransferGate = new();

    public static TransferProtocol ParseProtocol(string? value, string? fallback = null)
    {
        var normalized = (value ?? fallback ?? string.Empty).Trim();
        if (normalized.Equals("SFTP", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("SSH", StringComparison.OrdinalIgnoreCase))
        {
            return TransferProtocol.Sftp;
        }

        return TransferProtocol.Ftp;
    }

    public static void UploadBytes(Endpoint endpoint, string remotePath, byte[] content)
    {
        EnsureDirectory(endpoint, ParentDirectory(remotePath));
        Use(endpoint, transfer => transfer.Upload(NormalizePath(remotePath), content ?? Array.Empty<byte>()));
    }

    public static byte[] DownloadBytes(Endpoint endpoint, string remotePath)
        => Use(endpoint, transfer => transfer.Download(NormalizePath(remotePath)));

    public static void DownloadFile(Endpoint endpoint, string remotePath, string localFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(localFilePath) ?? ".");
        Use(endpoint, transfer => transfer.DownloadToFile(NormalizePath(remotePath), localFilePath));
    }

    public static IReadOnlyList<string> ListNames(Endpoint endpoint, string directory)
        => Use(endpoint, transfer => transfer.ListNames(NormalizeDirectory(directory)));

    public static bool Exists(Endpoint endpoint, string remotePath)
    {
        try
        {
            return Use(endpoint, transfer => transfer.Exists(NormalizePath(remotePath)));
        }
        catch
        {
            return false;
        }
    }

    public static void EnsureDirectory(Endpoint endpoint, string directory)
    {
        var normalized = NormalizeDirectory(directory);
        if (string.IsNullOrWhiteSpace(normalized) || normalized == "/")
        {
            return;
        }

        Use(endpoint, transfer => transfer.EnsureDirectory(normalized));
    }

    private static string ParentDirectory(string remotePath)
    {
        var normalized = NormalizePath(remotePath);
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0 ? "/" : normalized[..lastSlash];
    }

    private static T Use<T>(Endpoint endpoint, Func<ITransfer, T> action)
    {
        T result = default!;
        Use(endpoint, transfer => { result = action(transfer); });
        return result;
    }

    private static void Use(Endpoint endpoint, Action<ITransfer> action)
    {
        if (string.IsNullOrWhiteSpace(endpoint.Host) || string.IsNullOrWhiteSpace(endpoint.Username))
        {
            throw new InvalidOperationException(
                endpoint.Protocol == TransferProtocol.Sftp
                    ? "SFTP is not configured."
                    : "FTP is not configured.");
        }

        lock (TransferGate)
        {
            UseUnlocked(endpoint, action);
        }
    }

    private static void UseUnlocked(Endpoint endpoint, Action<ITransfer> action)
    {
        try
        {
            if (endpoint.Protocol == TransferProtocol.Sftp)
            {
                using var sftp = new SftpTransfer(endpoint);
                action(sftp);
                return;
            }

            using var ftp = new FtpTransfer(endpoint);
            action(ftp);
        }
        catch (Exception ex) when (IsConnectionFailure(ex))
        {
            var protocol = endpoint.Protocol == TransferProtocol.Sftp ? "SFTP" : "FTP";
            var port = endpoint.Protocol == TransferProtocol.Sftp ? SftpPort(endpoint) : FtpPort(endpoint);
            throw new InvalidOperationException(
                $"Cannot connect {protocol} ({endpoint.Host}:{port}).",
                ex);
        }
    }

    private static bool IsConnectionFailure(Exception ex)
    {
        if (ex is SocketException or TimeoutException or SshConnectionException or SshOperationTimeoutException)
        {
            return true;
        }

        if (ex is FtpException ftpEx)
        {
            return ftpEx.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || ftpEx.Message.Contains("unable to connect", StringComparison.OrdinalIgnoreCase)
                || ftpEx.Message.Contains("Authentication failed", StringComparison.OrdinalIgnoreCase);
        }

        if (ex is WebException web)
        {
            if (web.Response is FtpWebResponse ftpResponse)
            {
                return ftpResponse.StatusCode is FtpStatusCode.NotLoggedIn
                    or FtpStatusCode.ServiceNotAvailable
                    or FtpStatusCode.ServiceTemporarilyNotAvailable
                    or FtpStatusCode.CommandNotImplemented;
            }

            return web.Status is WebExceptionStatus.ConnectFailure
                or WebExceptionStatus.ConnectionClosed
                or WebExceptionStatus.NameResolutionFailure
                or WebExceptionStatus.Timeout
                or WebExceptionStatus.ReceiveFailure
                or WebExceptionStatus.SendFailure
                or WebExceptionStatus.SecureChannelFailure
                or WebExceptionStatus.TrustFailure;
        }

        return ex.InnerException != null && IsConnectionFailure(ex.InnerException);
    }

    private static int FtpPort(Endpoint endpoint)
        => endpoint.FtpPort > 0 ? endpoint.FtpPort : 21;

    private static int SftpPort(Endpoint endpoint)
        => endpoint.SftpPort > 0 ? endpoint.SftpPort : 22;

    private static string NormalizePath(string remotePath)
    {
        var normalized = (remotePath ?? string.Empty).Trim().Replace('\\', '/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        const string ftpRoot = "/home/WEB/amnoteweb";
        const string legacyAttachRoot = "/DATA2/AttachFile";
        if (normalized.Equals(legacyAttachRoot, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(legacyAttachRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = normalized[legacyAttachRoot.Length..];
            normalized = ftpRoot + (rest.StartsWith('/') ? rest : "/" + rest);
        }

        if (!normalized.StartsWith(ftpRoot, StringComparison.OrdinalIgnoreCase))
        {
            string[] shortFolders =
            [
                "/EinvoiceImage", "/EinvoiceNen", "/EinvoiceVien",
                "/EInvoiceXMLv2", "/EinvoiceXSL", "/EinvoiceXSLCompany", "/EinvoiceXml",
            ];
            foreach (var folder in shortFolders)
            {
                if (normalized.Equals(folder, StringComparison.OrdinalIgnoreCase)
                    || normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                {
                    normalized = ftpRoot + normalized;
                    break;
                }
            }
        }

        return normalized;
    }

    private static string NormalizeDirectory(string remoteDirectory)
        => NormalizePath(remoteDirectory).TrimEnd('/');

    private interface ITransfer
    {
        void Upload(string remotePath, byte[] content);
        byte[] Download(string remotePath);
        void DownloadToFile(string remotePath, string localFilePath);
        IReadOnlyList<string> ListNames(string directory);
        bool Exists(string remotePath);
        void EnsureDirectory(string directory);
    }

    private sealed class FtpTransfer : ITransfer, IDisposable
    {
        private readonly FtpClient _client;

        public FtpTransfer(Endpoint endpoint)
        {
            _client = new FtpClient(endpoint.Host, endpoint.Username, endpoint.Password, FtpPort(endpoint))
            {
                Config =
                {
                    EncryptionMode = FtpEncryptionMode.None,
                    DataConnectionType = FtpDataConnectionType.AutoPassive,
                    ConnectTimeout = 10000,
                    ReadTimeout = 25000,
                    DataConnectionConnectTimeout = 10000,
                    RetryAttempts = 2,
                },
                Encoding = Encoding.UTF8,
            };
            _client.Connect();
        }

        public void Upload(string remotePath, byte[] content)
        {
            var status = _client.UploadBytes(content, remotePath, FtpRemoteExists.Overwrite, createRemoteDir: true);
            if (status == FtpStatus.Failed)
            {
                throw new InvalidOperationException($"FTP upload failed: {remotePath}");
            }
        }

        public byte[] Download(string remotePath)
        {
            foreach (var candidate in EnumerateDownloadPaths(remotePath))
            {
                if (_client.DownloadBytes(out var data, candidate) && data is { Length: > 0 })
                {
                    return data;
                }
            }

            throw new InvalidOperationException($"Cannot download FTP file: {remotePath}");
        }

        public void DownloadToFile(string remotePath, string localFilePath)
        {
            foreach (var candidate in EnumerateDownloadPaths(remotePath))
            {
                var status = _client.DownloadFile(localFilePath, candidate);
                if (status != FtpStatus.Failed && File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 0)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"Cannot download FTP file: {remotePath}");
        }

        public IReadOnlyList<string> ListNames(string directory)
        {
            foreach (var candidate in EnumerateDownloadPaths(directory))
            {
                if (!_client.DirectoryExists(candidate))
                {
                    continue;
                }

                return _client.GetNameListing(candidate)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name) && name is not "." and not "..")
                    .Cast<string>()
                    .ToList();
            }

            return Array.Empty<string>();
        }

        public bool Exists(string remotePath)
            => EnumerateDownloadPaths(remotePath).Any(_client.FileExists);

        public void EnsureDirectory(string directory)
            => _client.CreateDirectory(directory);

        public void Dispose()
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            _client.Dispose();
        }

        private static IEnumerable<string> EnumerateDownloadPaths(string remotePath)
        {
            var normalized = NormalizePath(remotePath);
            yield return normalized;

            const string ftpRoot = "/home/WEB/amnoteweb";
            if (normalized.StartsWith(ftpRoot + "/", StringComparison.OrdinalIgnoreCase)
                && normalized.Length > ftpRoot.Length + 1)
            {
                var relativeFromHome = normalized[ftpRoot.Length..];
                yield return relativeFromHome;
                yield return relativeFromHome.TrimStart('/');
            }
        }
    }

    private sealed class SftpTransfer : ITransfer, IDisposable
    {
        private readonly SftpClient _client;

        public SftpTransfer(Endpoint endpoint)
        {
            _client = new SftpClient(endpoint.Host, SftpPort(endpoint), endpoint.Username, endpoint.Password)
            {
                OperationTimeout = TimeSpan.FromSeconds(20),
            };
            _client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(8);
            _client.Connect();
        }

        public void Upload(string remotePath, byte[] content)
        {
            using var stream = new MemoryStream(content);
            _client.UploadFile(stream, remotePath, true);
        }

        public byte[] Download(string remotePath)
        {
            using var memory = new MemoryStream();
            _client.DownloadFile(remotePath, memory);
            return memory.ToArray();
        }

        public void DownloadToFile(string remotePath, string localFilePath)
        {
            using var file = File.Create(localFilePath);
            _client.DownloadFile(remotePath, file);
        }

        public IReadOnlyList<string> ListNames(string directory)
        {
            if (!_client.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return _client.ListDirectory(directory)
                .Where(item => item.Name is not "." and not "..")
                .Select(item => item.Name)
                .ToList();
        }

        public bool Exists(string remotePath)
            => _client.Exists(remotePath);

        public void EnsureDirectory(string directory)
        {
            var current = string.Empty;
            foreach (var segment in directory.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current += "/" + segment;
                if (!_client.Exists(current))
                {
                    _client.CreateDirectory(current);
                }
            }
        }

        public void Dispose()
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            _client.Dispose();
        }
    }
}
