using System.Security.Cryptography;
using System.Text;
using System.Xml;
using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace API_AMNOTE_WEB.Services;

/// <summary>Immutable, content-addressed files. Database fields contain references, never XSL bodies.</summary>
public sealed class ElectronicDocumentStorage(
    IConfiguration configuration, ICompanyDatabaseResolver databases)
{
    private static readonly MemoryCache XslContents = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    // Used by the offline exporter. Runtime configuration remains in Program.cs.
    public static void ConfigureTransfer(IConfiguration configuration) => EInvoiceFtpClient.Configure(configuration);
    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Hash(string text) => Hash(Encoding.UTF8.GetBytes(text));

    public async Task<string> NamespaceAsync(string company)
        => Hash(await databases.ResolveDatabaseNameAsync(company)) + "/" + Hash(company);

    public string Root => (configuration["ElectronicDocuments:RemoteDirectory"]
        ?? "/home/WEB/amnoteweb/ElectronicDocuments").TrimEnd('/');

    public async Task<string> WriteXslAsync(string company, string content)
    {
        ValidateXsl(content);
        return Write(await NamespaceAsync(company), "xsl", Encoding.UTF8.GetBytes(content), "xsl");
    }

    public string Write(string scope, string category, byte[] bytes, string extension)
    {
        if (!EInvoiceFtpClient.IsConfigured()) throw new InvalidOperationException("Document FTP is not configured.");
        var hash = Hash(bytes);
        // A unique staging/version directory prevents partial overwrites even when two
        // different documents produce identical bytes concurrently. Only the DB reference
        // is published after verification; remote rename support is not assumed.
        var directory = $"{Root}/{scope}/{category}/{hash[..2]}/{Guid.NewGuid():N}";
        var path = $"{directory}/{hash}.{extension}";
        EInvoiceFtpClient.EnsureRemoteDirectoryExists(directory);
        EInvoiceFtpClient.UploadBytes(path, bytes);
        // A reference is published only after verifying the complete remote file.
        if (Hash(EInvoiceFtpClient.DownloadExactBytes(path)) != hash)
            throw new InvalidOperationException("Document FTP checksum verification failed.");
        return path;
    }

    public string ReadXsl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("XSL FTP reference is required.");
        ValidatePath(path, "xsl");
        return XslContents.GetOrCreate("document-xsl:" + path, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var bytes = Read(path);
            entry.Size = bytes.Length;
            var text = EInvoiceFtpClient.DecodeText(bytes);
            ValidateXsl(text);
            return text;
        })!;
    }

    public byte[] Read(string path)
    {
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal) || path.Contains("..") || path.Contains('\\'))
            throw new InvalidOperationException("Invalid document FTP reference.");
        var expected = Path.GetFileNameWithoutExtension(path);
        if (expected.Length != 64 || expected.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new InvalidOperationException("Document FTP reference does not contain a valid SHA-256.");
        var bytes = EInvoiceFtpClient.DownloadExactBytes(path);
        if (Hash(bytes) != expected)
            throw new InvalidOperationException("Document FTP checksum mismatch.");
        return bytes;
    }

    private void ValidatePath(string path, string extension)
    {
        if (!path.StartsWith(Root + "/", StringComparison.Ordinal) || !path.EndsWith("." + extension, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid document file type or root.");
    }

    public static void ValidateXsl(string content)
    {
        using var reader = XmlReader.Create(new StringReader(content.TrimStart('\uFEFF', '\u200B')), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10 * 1024 * 1024
        });
        var document = System.Xml.Linq.XDocument.Load(reader);
        var root = document.Root;
        if (root?.Name.NamespaceName != "http://www.w3.org/1999/XSL/Transform" || root.Name.LocalName is not ("stylesheet" or "transform"))
            throw new ArgumentException("XSL must be an XSLT stylesheet.");
        if (document.Descendants().Any(x => x.Name.NamespaceName == "http://www.w3.org/1999/XSL/Transform"
            && x.Name.LocalName is "include" or "import"))
            throw new ArgumentException("XSL include/import must be bundled into the versioned stylesheet before upload.");
        if (document.Descendants().Any(x => x.Name.NamespaceName == "http://www.w3.org/1999/XSL/Transform"
            && x.Name.LocalName == "result-document"))
            throw new ArgumentException("Secondary XSL output files are prohibited.");
    }
}
