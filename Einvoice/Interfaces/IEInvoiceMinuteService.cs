using API_AMNOTE_WEB.Models;
namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceMinuteService
    {
        Task<IReadOnlyList<EInvoiceMinuteDto>> SearchAsync(string companyCd, EInvoiceMinuteSearchRequest request);
        Task<EInvoiceMinuteDto?> GetByIdAsync(string companyCd, long bbanId);
        Task<EInvoiceMinutePublicLookupDto?> LookupPublicAsync(string taxCode, string lookupCode, CancellationToken cancellationToken = default);
        Task<EInvoiceMinutePublicDownloadInfo?> GetPublicDownloadInfoAsync(string taxCode, string lookupCode);
        Task<string?> GetPublicXmlAsync(string taxCode, string lookupCode);
        Task<string?> GetPublicXslAsync(string taxCode, string lookupCode);
        Task<string?> GetPublicHtmlAsync(string taxCode, string lookupCode);
        Task<EInvoiceMinutePublicLookupDto?> SavePublicBuyerSignatureAsync(string taxCode, string lookupCode, EInvoiceMinuteSignRequest request, CancellationToken cancellationToken = default);
        Task<EInvoiceMinuteDto> CreateAsync(string companyCd, string userId, EInvoiceMinuteSaveRequest request);
        Task<EInvoiceMinuteDto> UpdateAsync(string companyCd, string userId, long bbanId, EInvoiceMinuteSaveRequest request);
        Task<EInvoiceMinuteSigningPayloadDto> GetSigningPayloadAsync(string companyCd, string userId, long bbanId);
        Task<EInvoiceMinuteDto> SaveSignatureAsync(string companyCd, string userId, long bbanId, EInvoiceMinuteSignRequest request);
        Task<int> DeleteAsync(string companyCd, string userId, long bbanId);
        Task<int> DeleteManyAsync(string companyCd, string userId, IEnumerable<long> bbanIds);
        Task<string> GetPreviewXmlAsync(string companyCd, long bbanId);
        Task<EInvoiceMinuteSendMailResult> SendMailAsync(string companyCd, string userId, EInvoiceMinuteSendMailRequest request, CancellationToken cancellationToken = default);
    }
}
