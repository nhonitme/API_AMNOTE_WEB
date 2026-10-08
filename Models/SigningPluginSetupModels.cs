namespace API_AMNOTE_WEB.Models;

public sealed class SigningPluginSetupOptions
{
    public string Version { get; set; } = "1.2.0";
    public string StorageDirectory { get; set; } = "App_Data/SigningPlugin";
    public string SetupFileName { get; set; } = "AMNOTE-SigningPlugin-Setup.exe";
    public string SetupZipFileName { get; set; } = "AMNOTE-SigningPlugin-Setup.zip";
}

public sealed class SigningPluginSetupInfoDto
{
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool Available { get; set; }
    public string DownloadPath { get; set; } = "/api/EInvoiceSigningPlugin/setup";
}
