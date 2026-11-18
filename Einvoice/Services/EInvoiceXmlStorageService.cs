using API_AMNOTE_WEB.Einvoice.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Text;

namespace API_AMNOTE_WEB.Services;

public sealed class EInvoiceXmlStorageService(ElectronicDocumentStorage files) : IEInvoiceXmlStorageService
{
    public async Task<string> UploadInvoiceXmlAsync(string companyCd, long invoiceId, string xml,
        DateTime? invoiceDate = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(companyCd) || invoiceId <= 0 || string.IsNullOrWhiteSpace(xml))
            throw new ArgumentException("Company, invoice ID and XML are required.");
        cancellationToken.ThrowIfCancellationRequested();
        return files.Write(await files.NamespaceAsync(companyCd), "xml", Encoding.UTF8.GetBytes(xml), "xml");
    }

    public Task<string> DownloadAsync(string xmlFtpPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(xmlFtpPath) && xmlFtpPath.StartsWith(files.Root + "/", StringComparison.Ordinal))
            return Task.FromResult(Encoding.UTF8.GetString(files.Read(xmlFtpPath)));
        if (string.IsNullOrWhiteSpace(xmlFtpPath) || !EInvoiceFtpClient.IsRemotePath(xmlFtpPath))
            throw new InvalidOperationException("Signed XML FTP reference is missing or invalid.");
        // Legacy references remain explicit DB references after cutover; no path guessing or fallback.
        return Task.FromResult(Encoding.UTF8.GetString(EInvoiceFtpClient.DownloadExactBytes(xmlFtpPath)));
    }

    public async Task<string?> ResolveSignedInvoiceXmlAsync(string companyCd, EInvoiceInfo invoice,
        CancellationToken cancellationToken = default)
    {
        if (invoice.IS_SIGNED != 1) return null;
        return await DownloadAsync(invoice.XML_FTP_PATH
            ?? throw new InvalidOperationException("Signed XML FTP reference is missing."), cancellationToken);
    }

    public bool IsRemotePath(string? path) => path?.StartsWith(files.Root + "/", StringComparison.Ordinal) == true || EInvoiceFtpClient.IsRemotePath(path);
}
