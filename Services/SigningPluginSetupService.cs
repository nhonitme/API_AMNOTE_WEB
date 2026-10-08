using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services;

public interface ISigningPluginSetupService
{
    SigningPluginSetupInfoDto GetSetupInfo();

    (string FullPath, string FileName, string ContentType)? ResolveSetupFileForDownload();
}

public sealed class SigningPluginSetupService : ISigningPluginSetupService
{
    private readonly IWebHostEnvironment environment;
    private readonly SigningPluginSetupOptions options;

    public SigningPluginSetupService(IWebHostEnvironment environment, IOptions<SigningPluginSetupOptions> options)
    {
        this.environment = environment;
        this.options = options.Value;
    }

    public SigningPluginSetupInfoDto GetSetupInfo()
    {
        var file = ResolveSetupFile();
        return new SigningPluginSetupInfoDto
        {
            Version = options.Version,
            FileName = file?.Name ?? options.SetupFileName,
            FileSize = file?.Length ?? 0,
            Available = file != null,
            DownloadPath = "/api/EInvoiceSigningPlugin/setup"
        };
    }

    public (string FullPath, string FileName, string ContentType)? ResolveSetupFileForDownload()
    {
        var file = ResolveSetupFile();
        if (file == null)
            return null;

        return (file.FullName, file.Name, ResolveContentType(file.Extension));
    }

    private FileInfo? ResolveSetupFile()
    {
        var directory = ResolveStorageDirectory();
        if (!Directory.Exists(directory))
            return null;

        var candidates = new List<string>
        {
            Path.Combine(directory, $"AMNOTE-SigningPlugin-Setup-{options.Version}.exe"),
            Path.Combine(directory, options.SetupFileName),
            Path.Combine(directory, $"AMNOTE-SigningPlugin-Setup-{options.Version}.zip"),
            Path.Combine(directory, options.SetupZipFileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return new FileInfo(candidate);
        }

        var fallback = Directory.EnumerateFiles(directory, "AMNOTE-SigningPlugin-Setup*.*")
            .OrderByDescending(path => path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return fallback == null ? null : new FileInfo(fallback);
    }

    private string ResolveStorageDirectory()
    {
        return Path.IsPathRooted(options.StorageDirectory)
            ? options.StorageDirectory
            : Path.Combine(environment.ContentRootPath, options.StorageDirectory);
    }

    private static string ResolveContentType(string extension)
    {
        return extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            ? "application/zip"
            : "application/octet-stream";
    }
}
